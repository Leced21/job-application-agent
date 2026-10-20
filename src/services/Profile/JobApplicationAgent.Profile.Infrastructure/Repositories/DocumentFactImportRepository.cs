using JobApplicationAgent.Profile.Application.Exceptions;
using JobApplicationAgent.Profile.Application.Profiles.DocumentFacts;
using JobApplicationAgent.Profile.Domain.Entities;
using JobApplicationAgent.Profile.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JobApplicationAgent.Profile.Infrastructure.Repositories;

public sealed class DocumentFactImportRepository(ProfileDbContext db) : IDocumentFactImportRepository
{
    public Task<DocumentFactImport?> GetAsync(Guid ownerId, Guid profileId, Guid factId, CancellationToken ct) =>
        db.DocumentFactImports.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.ProfileId == profileId && x.FactId == factId, ct);
    public async Task<DocumentFactImportOutcome> ImportAsync(ImportDocumentFactCommand command, string hash, string json, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({command.FactId.ToString()}, 1))", ct);
        // Imports serialize per profile so distinct source facts can reconcile with one target.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({command.TargetProfileId.ToString()}, 0))", ct);
        var existing = await db.DocumentFactImports.AsNoTracking().SingleOrDefaultAsync(x => x.FactId == command.FactId, ct);
        if (existing is not null)
        {
            if (existing.OwnerId != command.OwnerId || existing.ProfileId != command.TargetProfileId || existing.RequestHash != hash)
                throw new DocumentFactImportConflictException("This fact was already imported with a different payload or target.");
            await transaction.CommitAsync(ct);
            return new(DocumentFactReceipt.From(existing), false);
        }
        if (!await db.CandidateProfiles.AnyAsync(x => x.Id == command.TargetProfileId, ct)) throw new CandidateProfileNotFoundException();
        var target = command.Kind == "Skill" ? await ReconcileSkill(command, ct) : await ReconcileDated(command, ct);
        var receipt = DocumentFactImport.Create(command.FactId, command.OwnerId, command.TargetProfileId, target.Id,
            command.SourceSha256, target.Created ? "Created" : "LinkedExisting", hash, json, DateTime.UtcNow, command.Kind);
        // No cascading FK: deleting the target must not resurrect it on a network retry.
        db.DocumentFactImports.Add(receipt);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(DocumentFactReceipt.From(receipt), true);
    }
    private async Task<(Guid Id, bool Created)> ReconcileSkill(ImportDocumentFactCommand command, CancellationToken ct)
    {
        var name = command.Fields[0].Value.Trim();
        var candidates = await db.Skills.AsNoTracking().Where(x => x.CandidateProfileId == command.TargetProfileId)
            .Select(x => new { x.Id, x.Name }).Take(10001).ToArrayAsync(ct);
        if (candidates.Length > 10000) throw new DocumentFactImportConflictException("Too many skills for bounded reconciliation.");
        var matches = candidates.Where(x => x.Name.Trim().Equals(name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        if (matches.Length > 1) throw new DocumentFactImportConflictException("Several existing skills match; reconcile them explicitly first.");
        Guid skillId;
        if (matches.Length == 1) skillId = matches[0].Id;
        else
        {
            var skill = new Skill(command.TargetProfileId, name);
            db.Skills.Add(skill);
            skillId = skill.Id;
        }
        return (skillId, matches.Length == 0);
    }
    private sealed record Candidate(Guid Id, string Label, string Organization, DateOnly Start, DateOnly? End, bool Current, string? Credential);
    private async Task<(Guid Id, bool Created)> ReconcileDated(ImportDocumentFactCommand command, CancellationToken ct)
    {
        var fields = command.Fields.ToDictionary(x => x.Field, x => x.Value);
        var dates = command.Dates!;
        var label = fields[command.Kind == "Experience" ? "Title" : command.Kind == "Education" ? "Degree" : "Name"];
        var organization = fields[command.Kind == "Experience" ? "Company" : command.Kind == "Education" ? "Institution" : "Issuer"];
        var start = command.Kind == "Certification" ? dates.IssueDate!.Value : dates.StartDate!.Value;
        var end = command.Kind == "Certification" ? dates.ExpirationDate : dates.EndDate;
        IQueryable<Candidate> query = command.Kind switch
        {
            "Experience" => db.ProfessionalExperiences.AsNoTracking().Where(x => x.CandidateProfileId == command.TargetProfileId)
                .Select(x => new Candidate(x.Id, x.JobTitle, x.CompanyName, x.StartDate, x.EndDate, x.IsCurrent, null)),
            "Education" => db.Educations.AsNoTracking().Where(x => x.CandidateProfileId == command.TargetProfileId)
                .Select(x => new Candidate(x.Id, x.Degree, x.InstitutionName, x.StartDate, x.EndDate, x.IsCurrent, null)),
            _ => db.Certifications.AsNoTracking().Where(x => x.CandidateProfileId == command.TargetProfileId)
                .Select(x => new Candidate(x.Id, x.Name, x.IssuingOrganization, x.IssueDate, x.ExpirationDate, false, x.CredentialId))
        };
        var candidates = await query.Take(10001).ToArrayAsync(ct);
        if (candidates.Length > 10000) throw new DocumentFactImportConflictException("Too many records for bounded reconciliation.");
        bool Same(string a, string b) => a.Trim().Equals(b.Trim(), StringComparison.OrdinalIgnoreCase);
        var matches = candidates.Where(x => Same(x.Label, label) && Same(x.Organization, organization) && x.Start == start).Take(2).ToArray();
        if (matches.Length > 1) throw new DocumentFactImportConflictException("Several existing records match; reconcile them explicitly first.");
        if (matches.Length == 1)
        {
            var match = matches[0];
            if (match.End != end || match.Current != (dates.IsCurrent ?? false) ||
                command.Kind == "Certification" && !string.IsNullOrEmpty(match.Credential) &&
                    fields.TryGetValue("Identifier", out var identifier) && !StringComparer.Ordinal.Equals(match.Credential, identifier))
                throw new DocumentFactImportConflictException("Existing verified dates, status or credential identifier conflict with the source mapping.");
            return (match.Id, false);
        }
        var description = fields.GetValueOrDefault("Description");
        Guid id;
        if (command.Kind == "Experience")
        {
            var entity = new ProfessionalExperience(command.TargetProfileId, organization, label, start, end, dates.IsCurrent!.Value, description: description);
            db.ProfessionalExperiences.Add(entity); id = entity.Id;
        }
        else if (command.Kind == "Education")
        {
            var entity = new Education(command.TargetProfileId, organization, label, start, end, dates.IsCurrent!.Value, description: description);
            db.Educations.Add(entity); id = entity.Id;
        }
        else
        {
            var entity = new Certification(command.TargetProfileId, label, organization, start, end, fields.GetValueOrDefault("Identifier"), null);
            db.Certifications.Add(entity); id = entity.Id;
        }
        return (id, true);
    }
}
