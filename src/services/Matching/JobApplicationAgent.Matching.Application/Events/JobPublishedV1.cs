using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JobApplicationAgent.Matching.Application.Events;

public sealed record JobPublishedV1(Guid EventId, string EventType, int SchemaVersion, DateTime OccurredAtUtc,
    string Producer, Guid JobId, Guid CollectionOfferId, Guid CollectionRevisionId, string Fingerprint)
{
    public const string Type = "job.published.v1";

    public void Validate()
    {
        if (EventId == Guid.Empty || EventType != Type || SchemaVersion != 1 ||
            OccurredAtUtc.Kind != DateTimeKind.Utc || Producer != "Job" || JobId == Guid.Empty ||
            CollectionOfferId == Guid.Empty || CollectionRevisionId == Guid.Empty ||
            string.IsNullOrEmpty(Fingerprint) || Fingerprint.Length != 64 || !Fingerprint.All(Uri.IsHexDigit))
            throw new InvalidOperationException("Invalid job.published.v1 event envelope.");
    }

    public static JobPublishedV1 ParseAndValidate(string payload)
    {
        var envelope = JsonSerializer.Deserialize<JobPublishedV1>(payload, JsonSerializerOptions.Web)
            ?? throw new InvalidOperationException("The Job event payload is empty.");
        envelope.Validate();
        return envelope;
    }

    public string GetPayloadSha256(string rawPayload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPayload)));
}
