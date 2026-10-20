using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.Application;

public sealed record AgentDefinition(AgentKind Kind, string Purpose, string? PromptVersion, bool Executable);
public static class AgentCatalog
{
    public static IReadOnlyList<AgentDefinition> Definitions { get; } = Array.AsReadOnly(new AgentDefinition[] {
        new(AgentKind.ProfileAnalysis, "Prioriser les questions sur les rubriques non renseignées", ProfileAnalysisPrompt.Version, true),
        new(AgentKind.JobAnalysis, "Extraire les exigences citées dans une offre", JobAnalysisPrompt.Version, true),
        new(AgentKind.MatchingEnrichment, "Prioriser les points à examiner dans une évaluation existante", MatchingEnrichmentPrompt.Version, true),
        new(AgentKind.ResumeAdaptation, "Associer des faits confirmés du profil aux exigences approuvées", ResumeAdaptationPrompt.Version, true),
        new(AgentKind.CoverLetter, "Rédiger des paragraphes de lettre fondés sur le parcours confirmé", CoverLetterPrompt.Version, true),
        new(AgentKind.ApplicationPreparation, "Évaluer les éléments de preuve pour préparer une candidature", ApplicationPreparationPrompt.Version, true),
        new(AgentKind.FollowUpSuggestion, "Suggérer un brouillon de relance ou une attente", FollowUpSuggestionPrompt.Version, true) });
}
public sealed record AgentPolicy(string ModelId, int MaxInputTokens, int MaxOutputTokens, decimal MaxCostUsd,
    decimal InputUsdPerMillion, decimal OutputUsdPerMillion, TimeSpan Timeout)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ModelId) || ModelId.Length > 200 || ModelId.Any(char.IsControl) || MaxInputTokens is < 1 or > 32768 ||
            MaxOutputTokens is < 1 or > 4096 || MaxCostUsd < 0 || InputUsdPerMillion is < 0 or > 1000000 || OutputUsdPerMillion is < 0 or > 1000000 ||
            Timeout < TimeSpan.FromMilliseconds(1) || Timeout > TimeSpan.FromMinutes(2)) throw new ArgumentException("Invalid server model/budget policy.");
    }
    public decimal Cost(int input, int output) => (input * InputUsdPerMillion + output * OutputUsdPerMillion) / 1000000m;
}
public sealed record AgentReservation(long MaxTokens, decimal MaxUsd);
public sealed record ModelPrompt(string System, string UntrustedDataJson, string OutputSchemaJson);
public sealed record PreparedModelInput(string ModelId, ModelPrompt Prompt, int InputTokens, string? ProviderPayload = null, string? TokenizerSha256 = null);
public sealed record ModelRequest(string ModelId, ModelPrompt Prompt, int MaxInputTokens, int MaxOutputTokens, PreparedModelInput? PreparedInput = null);
public sealed record ModelResponse(string OutputJson, int InputTokens, int OutputTokens, AgentFailure? Failure = null);
public interface IAgentSourceReader { Task<FrozenAgentSource?> ReadAsync(Guid owner, AgentCommand command, CancellationToken ct); }
public interface IAgentModel
{
    // Prepare the exact protocol envelope and count it with the configured tokenizer before reserving quota.
    // Preparation must never generate text. Its I/O is cancellable and bounded; payload stays private to the adapter.
    Task<PreparedModelInput> PrepareAsync(string modelId, ModelPrompt prompt, CancellationToken ct);
    Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken ct);
}
public interface IAgentRunRepository
{
    // Atomic owner/request uniqueness; reject a different RequestSha256. Return the original immutable run on replay.
    Task<AgentRun> BeginAsync(AgentRun candidate, CancellationToken ct);
    // Atomic owner quota reservation, once per RUNNING run; reject late reservations after completion.
    // The maximum charge remains reserved until completion, including after lost acknowledgements.
    Task<bool> ReserveAsync(Guid owner, Guid runId, AgentReservation reservation, CancellationToken ct);
    // Atomically finalize state, accounting and AgentAuditEntry.From(completed). Unknown usage charges the full reservation.
    // Real overrun usage must still be accounted for, even if it makes the remaining quota negative.
    Task CompleteAsync(AgentRun completed, CancellationToken ct);
    Task<AgentRun?> GetAsync(Guid owner, Guid id, CancellationToken ct);
    Task<AgentRun?> GetLatestApprovedAsync(Guid owner, AgentKind kind, Guid resourceId, CancellationToken ct);
    // Atomic revision comparison, state write and AgentAuditEntry.From(reviewed); never apply the proposal to another service.
    Task<bool> TryReviewAsync(AgentRun reviewed, long expectedRevision, CancellationToken ct);
}
public sealed class AgentRunInProgressException : Exception;
public sealed class AgentPersistenceUnavailableException : Exception;
public sealed class InvalidAgentOutputException : Exception;
// Only adapters that can prove no generation request was sent may use this exception.
public sealed class AgentModelNotInvokedException : Exception;
public sealed class InvalidAgentSourceException : Exception;
