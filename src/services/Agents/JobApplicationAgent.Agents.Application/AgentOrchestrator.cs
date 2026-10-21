using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.Application;

public sealed class AgentOrchestrator(IAgentRunRepository repository, IAgentSourceReader sources, IAgentModel model, AgentPolicy policy, TimeProvider clock)
{
    public async Task<AgentRun> ExecuteAsync(Guid owner, AgentCommand command, CancellationToken ct = default)
    {
        policy.Validate(); ct.ThrowIfCancellationRequested();
        var candidate = AgentRun.Start(owner, command, clock.GetUtcNow().UtcDateTime);
        using var timeout = new CancellationTokenSource(policy.Timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        var token = linked.Token;
        // Begin is idempotent even if its acknowledgement is lost. Never proceed without ownership of the returned run.
        var run = await repository.BeginAsync(candidate, token).WaitAsync(token);
        if (run.OwnerId != owner || run.Command != command || run.RequestSha256 != candidate.RequestSha256)
            throw new AgentPersistenceUnavailableException();
        if (run.Id != candidate.Id)
        {
            if (run.State == AgentRunState.Running) throw new AgentRunInProgressException();
            return run;
        }
        if (run.State != AgentRunState.Running) throw new AgentPersistenceUnavailableException();
        AgentReservation? reservation = null;
        var accounting = new AgentAccounting(0, 0, true);
        AgentProposal? proposal = null; AgentFailure? failure = null;
        var version = command.Kind switch { AgentKind.JobAnalysis => JobAnalysisPrompt.Version, AgentKind.ProfileAnalysis => ProfileAnalysisPrompt.Version,
            AgentKind.MatchingEnrichment => MatchingEnrichmentPrompt.Version, AgentKind.ResumeAdaptation => ResumeAdaptationPrompt.Version,
            AgentKind.CoverLetter => CoverLetterPrompt.Version, AgentKind.ApplicationPreparation => ApplicationPreparationPrompt.Version,
            AgentKind.FollowUpSuggestion => FollowUpSuggestionPrompt.Version, _ => null };
        var trace = new AgentExecutionTrace(policy.ModelId, version);
        var unexpectedFailure = AgentFailure.SourceUnavailable;
        try
        {
            if (version is null) throw new RunRejected(AgentFailure.UnsupportedAgent);
            var source = await sources.ReadAsync(owner, command, token).WaitAsync(token);
            if (source is null || source.OwnerId != owner || source.ResourceId != command.ResourceId || string.IsNullOrWhiteSpace(source.Text) ||
                source.Text.Length > 16000 || new UTF8Encoding(false, true).GetByteCount(source.Text) > 32768 || AgentRun.Hash(source.Text) != source.Sha256)
                throw new RunRejected(AgentFailure.SourceRejected);
            var prompt = command.Kind switch { AgentKind.ProfileAnalysis => ProfileAnalysisPrompt.Build(source),
                AgentKind.MatchingEnrichment => MatchingEnrichmentPrompt.Build(source), AgentKind.ResumeAdaptation => ResumeAdaptationPrompt.Build(source),
                AgentKind.CoverLetter => CoverLetterPrompt.Build(source), AgentKind.ApplicationPreparation => ApplicationPreparationPrompt.Build(source),
                AgentKind.FollowUpSuggestion => FollowUpSuggestionPrompt.Build(source),
                _ => JobAnalysisPrompt.Build(source) };
            trace = trace with { SourceSha256 = source.Sha256, PromptSha256 = AgentRun.Hash(JsonSerializer.Serialize(prompt)) };
            unexpectedFailure = AgentFailure.ModelUnavailable;
            var prepared = await model.PrepareAsync(policy.ModelId, prompt, token).WaitAsync(token);
            if (prepared is null || prepared.ModelId != policy.ModelId || prepared.Prompt != prompt)
                throw new RunRejected(AgentFailure.ModelUnavailable);
            var count = prepared.InputTokens;
            trace = trace with { PreparationSha256 = prepared.ProviderPayload is null ? null : AgentRun.Hash(prepared.ProviderPayload),
                TokenizerSha256 = prepared.TokenizerSha256 };
            if (count < 1 || count > policy.MaxInputTokens) throw new RunRejected(AgentFailure.InputLimit);
            reservation = new((long)policy.MaxInputTokens + policy.MaxOutputTokens, policy.Cost(policy.MaxInputTokens, policy.MaxOutputTokens));
            if (reservation.MaxUsd > policy.MaxCostUsd) { reservation = null; throw new RunRejected(AgentFailure.BudgetRejected); }
            unexpectedFailure = AgentFailure.BudgetUnavailable;
            if (!await repository.ReserveAsync(owner, run.Id, reservation, token).WaitAsync(token))
            { reservation = null; accounting = new(0, 0, true); throw new RunRejected(AgentFailure.BudgetRejected); }
            unexpectedFailure = AgentFailure.ModelUnavailable;
            // Once invoked, the model may charge even when its acknowledgement is lost.
            accounting = new(reservation.MaxTokens, reservation.MaxUsd, false);
            var response = await model.GenerateAsync(new(policy.ModelId, prompt, policy.MaxInputTokens, policy.MaxOutputTokens, prepared), token).WaitAsync(token);
            if (response is null || response.InputTokens < 0 || response.OutputTokens < 0) throw new RunRejected(AgentFailure.UsageViolation);
            accounting = new((long)response.InputTokens + response.OutputTokens, policy.Cost(response.InputTokens, response.OutputTokens), true);
            if (response.InputTokens > policy.MaxInputTokens || response.OutputTokens > policy.MaxOutputTokens) throw new RunRejected(AgentFailure.UsageViolation);
            if (response.Failure is { } modelFailure)
                throw new RunRejected(modelFailure is AgentFailure.InvalidOutput or AgentFailure.UsageViolation ? modelFailure : AgentFailure.ModelUnavailable);
            var json = command.Kind switch { AgentKind.ProfileAnalysis => ProfileAnalysisPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text),
                AgentKind.MatchingEnrichment => MatchingEnrichmentPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text),
                AgentKind.ResumeAdaptation => ResumeAdaptationPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text),
                AgentKind.CoverLetter => CoverLetterPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text),
                AgentKind.ApplicationPreparation => ApplicationPreparationPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text),
                AgentKind.FollowUpSuggestion => FollowUpSuggestionPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text),
                _ => JobAnalysisPrompt.ValidateAndCanonicalize(response.OutputJson, source.Text) };
            proposal = new(source, policy.ModelId, version, trace.PromptSha256!, json, AgentRun.Hash(json));
        }
        catch (RunRejected e) { failure = e.Failure; }
        catch (InvalidAgentOutputException) { failure = AgentFailure.InvalidOutput; }
        catch (InvalidAgentSourceException) { failure = AgentFailure.SourceRejected; }
        catch (AgentModelNotInvokedException) { failure = AgentFailure.ModelUnavailable; accounting = new(0, 0, true); }
        catch (EncoderFallbackException) { failure = AgentFailure.SourceRejected; }
        catch (OperationCanceledException) { failure = ct.IsCancellationRequested ? AgentFailure.Cancelled : AgentFailure.TimedOut; }
        catch (Exception) { failure = unexpectedFailure; } // Raw adapter errors may contain source text or credentials; do not log/return them.

        // A late cancellation must not leave a valid proposal reviewable after the caller cancelled.
        if (ct.IsCancellationRequested || timeout.IsCancellationRequested)
        { proposal = null; failure = ct.IsCancellationRequested ? AgentFailure.Cancelled : AgentFailure.TimedOut; }
        var now = clock.GetUtcNow().UtcDateTime;
        var completed = run.Finish(proposal, failure, accounting, now < run.UpdatedAtUtc ? run.UpdatedAtUtc : now, trace);
        using var finalize = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await repository.CompleteAsync(completed, finalize.Token).WaitAsync(finalize.Token); }
        catch (Exception) { throw new AgentPersistenceUnavailableException(); }
        return completed;
    }
    private sealed class RunRejected(AgentFailure failure) : Exception
    { public AgentFailure Failure { get; } = failure; }
}

public sealed class AgentReviewService(IAgentRunRepository repository, TimeProvider clock)
{
    public async Task<AgentRun?> ReviewAsync(Guid owner, Guid id, AgentReview review, CancellationToken ct = default)
    {
        var run = await repository.GetAsync(owner, id, ct);
        if (run is null) return null;
        var now = clock.GetUtcNow().UtcDateTime;
        var result = run.Review(owner, review, now < run.UpdatedAtUtc ? run.UpdatedAtUtc : now);
        if (!await repository.TryReviewAsync(result, review.ExpectedRevision, ct)) throw new InvalidOperationException("Stale proposal revision.");
        return result;
    }
}
