using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.Application;

public sealed record CreateCandidacyCommand(Guid RequestId, Guid ProfileId, Guid JobId);
public sealed record VerifiedApplicationSources(Guid OwnerId, Guid ProfileId, Guid JobId,
    string JobTitle, string CompanyName, bool AcceptsApplications);

/// <summary>Must verify profile ownership and fetch the real Job; never accept browser snapshots.</summary>
public interface IApplicationSources
{
    Task<VerifiedApplicationSources> VerifyAsync(Guid ownerId, Guid profileId, Guid jobId, CancellationToken cancellationToken);
}

/// <summary>
/// All operations are owner scoped. InsertOrGet must atomically reserve the request key and the
/// owner/profile/job identity. Existing identity is returned and the new request key bound to it.
/// A request key already bound to another identity must throw CandidacyRequestConflictException.
/// PostgreSQL constraints/transactions are required; read-then-insert alone is insufficient.
/// </summary>
public interface ICandidacyCreationRepository
{
    Task<Candidacy?> FindByRequestAsync(Guid ownerId, Guid requestId, CancellationToken cancellationToken);
    Task<Candidacy?> FindByIdentityAsync(Guid ownerId, Guid profileId, Guid jobId, CancellationToken cancellationToken);
    Task<Candidacy> InsertOrGetAsync(Guid requestId, Candidacy candidacy, CancellationToken cancellationToken);
}

public sealed class CandidacyRequestConflictException() : InvalidOperationException("Request key belongs to another candidature.");

public sealed class CreateCandidacyService(ICandidacyCreationRepository repository,
    IApplicationSources sources, TimeProvider timeProvider)
{
    // ownerId must be derived from server authentication, never from the command body.
    public async Task<Candidacy> CreateAsync(Guid ownerId, CreateCandidacyCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (ownerId == Guid.Empty || command.RequestId == Guid.Empty || command.ProfileId == Guid.Empty || command.JobId == Guid.Empty)
            throw new ArgumentException("Owner, request, profile and job are required.");
        var replay = await repository.FindByRequestAsync(ownerId, command.RequestId, cancellationToken);
        if (replay is not null)
        {
            if (replay.OwnerId != ownerId || replay.ProfileId != command.ProfileId || replay.JobId != command.JobId)
                throw new CandidacyRequestConflictException();
            return replay;
        }

        var existing = await repository.FindByIdentityAsync(ownerId, command.ProfileId, command.JobId, cancellationToken);
        if (existing is not null)
        {
            EnsureIdentity(existing, ownerId, command);
            var result = await repository.InsertOrGetAsync(command.RequestId, existing, cancellationToken);
            EnsureIdentity(result, ownerId, command);
            return result;
        }

        var verified = await sources.VerifyAsync(ownerId, command.ProfileId, command.JobId, cancellationToken);
        if (verified.OwnerId != ownerId || verified.ProfileId != command.ProfileId || verified.JobId != command.JobId)
            throw new UnauthorizedAccessException("Source identity mismatch.");
        if (!verified.AcceptsApplications) throw new InvalidOperationException("Job no longer accepts applications.");
        var draft = Candidacy.Create(ownerId, command.ProfileId, command.JobId,
            verified.JobTitle, verified.CompanyName, timeProvider.GetUtcNow().UtcDateTime);
        var stored = await repository.InsertOrGetAsync(command.RequestId, draft, cancellationToken);
        EnsureIdentity(stored, ownerId, command);
        return stored;
    }

    private static void EnsureIdentity(Candidacy candidacy, Guid ownerId, CreateCandidacyCommand command)
    {
        if (candidacy.OwnerId != ownerId || candidacy.ProfileId != command.ProfileId || candidacy.JobId != command.JobId)
            throw new InvalidOperationException("Repository returned a different candidature identity.");
    }
}
