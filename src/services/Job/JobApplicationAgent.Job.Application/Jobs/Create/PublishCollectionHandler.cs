using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using JobApplicationAgent.Job.Domain.Entities;

namespace JobApplicationAgent.Job.Application.Jobs.Create;

public sealed record PublishCollectionCommand(Guid OfferId, Guid RevisionId, string Fingerprint,
    CreateJobCommand Job, string[] Skills, DateTime? ExpiresAtUtc);
public sealed record CollectionPublicationDto(Guid OfferId, Guid RevisionId, Guid JobId, string Fingerprint, DateTime PublishedAtUtc)
{
    public static CollectionPublicationDto From(CollectionPublication receipt) => new(receipt.OfferId, receipt.RevisionId,
        receipt.JobId, receipt.Fingerprint, receipt.PublishedAtUtc);
}
public sealed record PublicationOutcome(CollectionPublicationDto Receipt, bool Created);
public sealed class PublicationConflictException(string message) : Exception(message);

public interface ICollectionPublicationRepository
{
    Task<CollectionPublication?> GetAsync(Guid offerId, CancellationToken ct);
    Task<PublicationOutcome> PublishAsync(PublishCollectionCommand command, string hash, string json, CancellationToken ct);
}

public sealed class PublishCollectionHandler(ICollectionPublicationRepository repository, IValidator<CreateJobCommand> validator)
{
    public async Task<PublicationOutcome> HandleAsync(PublishCollectionCommand command, CancellationToken ct)
    {
        if (command.OfferId == Guid.Empty || command.RevisionId == Guid.Empty || command.Fingerprint is null ||
            !System.Text.RegularExpressions.Regex.IsMatch(command.Fingerprint, "^[A-Fa-f0-9]{64}$") || command.Job is null ||
            command.Skills is null || command.Skills.Length > 100 || command.Skills.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > 100) ||
            command.ExpiresAtUtc is { Kind: not DateTimeKind.Utc } ||
            (command.ExpiresAtUtc.HasValue && command.Job.PublishedAtUtc > command.ExpiresAtUtc))
            throw new ValidationException("Invalid Collection publication.");
        await validator.ValidateAndThrowAsync(command.Job, ct);
        var json = JsonSerializer.Serialize(command);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        return await repository.PublishAsync(command, hash, json, ct);
    }
    public async Task<CollectionPublicationDto?> GetAsync(Guid offerId, CancellationToken ct)
    {
        if (offerId == Guid.Empty) throw new ValidationException("Offer ID cannot be empty.");
        var receipt = await repository.GetAsync(offerId, ct);
        return receipt is null ? null : CollectionPublicationDto.From(receipt);
    }
}
