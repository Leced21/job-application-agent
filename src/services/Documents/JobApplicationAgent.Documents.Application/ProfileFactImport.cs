using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.Application;

public sealed record ProfileFactReceipt(Guid FactId, Guid OwnerId, Guid ProfileId, Guid SkillId, string SourceSha256, string Action, DateTime ImportedAtUtc,
    string Kind = "Skill", Guid EntityId = default);
public sealed class ProfileFactImportRejectedException(string message) : Exception(message);
public sealed class ProfileFactImportUnavailableException : Exception;
public interface IProfileFactImportClient
{
    Task<ProfileFactReceipt> ImportAsync(ResumeCandidateFact fact, CancellationToken ct);
}
public sealed class ProfileFactImportService(IResumeCandidateFactRepository facts, IProfileFactImportClient profile)
{
    public async Task<ProfileFactReceipt?> ImportAsync(Guid ownerId, Guid resumeId, Guid factId, CancellationToken ct)
    {
        var fact = await facts.GetAsync(ownerId, resumeId, factId, ct);
        if (fact is null) return null;
        if (fact.Decision != "Approved" || fact.ReviewedByOwnerId != ownerId || fact.ReviewedAtUtc is null)
            throw new ProfileFactImportRejectedException("Approve the source fact before importing it into Profile.");
        var dto = CandidateFactDto.From(fact);
        if (fact.Kind == "Skill")
        {
            if (dto.Fields.Length != 1 || dto.Fields[0].Field != "Name" || dto.Fields[0].Value.Length > 150)
                throw new ProfileFactImportRejectedException("A skill requires a source name of at most 150 characters.");
        }
        else
        {
            if (fact.ProfileDatesJson is null || fact.ProfileDatesConfirmedByOwnerId != ownerId || fact.ProfileDatesConfirmedAtUtc is null)
                throw new ProfileFactImportRejectedException("Confirm the source mapping and exact dates before importing this category.");
            ProfileFactMappingService.Validate(fact, System.Text.Json.JsonSerializer.Deserialize<ConfirmProfileDatesCommand>(fact.ProfileDatesJson)!);
        }
        return await profile.ImportAsync(fact, ct);
    }
}
