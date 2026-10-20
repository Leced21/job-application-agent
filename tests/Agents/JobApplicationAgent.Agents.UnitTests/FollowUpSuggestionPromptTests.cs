using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public sealed class FollowUpSuggestionPromptTests
{
    private static readonly DateTime SubmittedAt = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AsOf = new(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void FreezeAndReadKeepOnlyBoundedActiveCandidacySnapshot()
    {
        var source = FollowUpSuggestionPrompt.Freeze("Ingénieur logiciel", "Exemple", "InReview", SubmittedAt,
            SubmittedAt.AddDays(2), null, null, AsOf);

        var snapshot = FollowUpSuggestionPrompt.Read(source);

        Assert.Equal("Ingénieur logiciel", snapshot.JobTitle);
        Assert.Equal("Exemple", snapshot.CompanyName);
        Assert.Equal("InReview", snapshot.Status);
        Assert.Equal(SubmittedAt.AddDays(2), snapshot.LastRecruiterUpdateAtUtc);
        Assert.Null(snapshot.PendingDueAtUtc);
    }

    [Fact]
    public void FreezeRejectsInactiveCandidacyAndNonUtcDates()
    {
        Assert.Throws<InvalidAgentSourceException>(() => FollowUpSuggestionPrompt.Freeze("Développeur", "Exemple", "Draft",
            SubmittedAt, null, null, null, AsOf));
        Assert.Throws<InvalidAgentSourceException>(() => FollowUpSuggestionPrompt.Freeze("Développeur", "Exemple", "Submitted",
            DateTime.SpecifyKind(SubmittedAt, DateTimeKind.Local), null, null, null, AsOf));
    }

    [Fact]
    public void FuturePlannedFollowUpRequiresWaitAndEmptyDraft()
    {
        var source = FollowUpSuggestionPrompt.Freeze("Développeur", "Exemple", "Submitted", SubmittedAt,
            null, AsOf.AddDays(2), "Email", AsOf);

        var result = FollowUpSuggestionPrompt.ValidateAndCanonicalize(
            "{\"schemaVersion\":\"follow-up-suggestion-v1\",\"recommendation\":\"Wait\",\"draftMessage\":\"\"}", source);

        Assert.Contains("\"recommendation\":\"Wait\"", result, StringComparison.Ordinal);
        Assert.Throws<InvalidAgentOutputException>(() => FollowUpSuggestionPrompt.ValidateAndCanonicalize(
            "{\"schemaVersion\":\"follow-up-suggestion-v1\",\"recommendation\":\"DraftMessage\",\"draftMessage\":\"Bonjour, je souhaite savoir où en est ma candidature.\"}", source));
    }

    [Fact]
    public void DraftMessageRequiresGenericBoundedTextAndWaitCannotContainText()
    {
        var source = FollowUpSuggestionPrompt.Freeze("Développeur", "Exemple", "Submitted", SubmittedAt,
            null, null, null, AsOf);

        Assert.Throws<InvalidAgentOutputException>(() => FollowUpSuggestionPrompt.ValidateAndCanonicalize(
            "{\"schemaVersion\":\"follow-up-suggestion-v1\",\"recommendation\":\"DraftMessage\",\"draftMessage\":\"Bonjour <script>\"}", source));
        Assert.Throws<InvalidAgentOutputException>(() => FollowUpSuggestionPrompt.ValidateAndCanonicalize(
            "{\"schemaVersion\":\"follow-up-suggestion-v1\",\"recommendation\":\"Wait\",\"draftMessage\":\"Merci pour votre retour.\"}", source));
    }
}
