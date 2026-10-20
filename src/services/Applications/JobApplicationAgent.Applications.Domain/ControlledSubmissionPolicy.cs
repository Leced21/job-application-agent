using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace JobApplicationAgent.Applications.Domain;

public sealed record SubmissionPlatformCapability(
    string PlatformId,
    bool HasOfficialApi,
    bool TermsAllowAutomation,
    bool RequiresCaptcha,
    bool SupportsIdempotencyKey);

public sealed record SubmissionReviewApproval(Guid CandidacyId, long Revision, string ReviewSha256, bool Confirmed);
public sealed record ControlledSubmissionAttempt(Guid AttemptId, long ApprovedRevision, string ReviewSha256,
    SubmissionPlatformCapability Platform, DateTime PreparedAtUtc);
public enum ControlledSubmissionOutcome { Submitted = 1, DefinitelyNotSubmitted = 2, Unknown = 3 }
public sealed record ControlledSubmissionResult(Guid AttemptId, ControlledSubmissionOutcome Outcome,
    DateTime OccurredAtUtc, string? Reference);

public enum SubmissionReadinessCode
{
    Ready,
    AutomationDisabled,
    CandidacyNotApproved,
    DocumentsIncomplete,
    PlatformNotSupported,
    PlatformTermsDisallowAutomation,
    CaptchaRequired,
    DuplicateProtectionUnavailable,
    ApprovalRequired,
    ReviewChanged,
    AttemptAlreadyPrepared
}

public sealed record SubmissionReadiness(SubmissionReadinessCode Code, string Message)
{
    public bool IsReady => Code == SubmissionReadinessCode.Ready;
}

/// <summary>Fail-closed policy for a future controlled submission adapter. It never sends data.</summary>
public static class ControlledSubmissionPolicy
{
    public static SubmissionReadiness Evaluate(
        bool automationEnabled,
        Candidacy candidacy,
        SubmissionPlatformCapability platform,
        SubmissionReviewApproval? approval)
    {
        ArgumentNullException.ThrowIfNull(candidacy);
        ArgumentNullException.ThrowIfNull(platform);

        if (!automationEnabled)
            return Block(SubmissionReadinessCode.AutomationDisabled, "Submission automation is disabled.");
        if (candidacy.Status != CandidacyStatus.Approved || candidacy.Submission is not null)
            return Block(SubmissionReadinessCode.CandidacyNotApproved, "Only an approved, not-yet-submitted application is eligible.");
        if (candidacy.SubmissionAttempt is not null && candidacy.SubmissionAttemptResult?.Outcome != ControlledSubmissionOutcome.DefinitelyNotSubmitted)
            return Block(SubmissionReadinessCode.AttemptAlreadyPrepared, "A submission attempt has already been prepared for this application.");
        if (candidacy.Resume is null || candidacy.CoverLetter is null)
            return Block(SubmissionReadinessCode.DocumentsIncomplete, "The approved resume and cover letter are both required.");
        if (string.IsNullOrWhiteSpace(platform.PlatformId) || !platform.HasOfficialApi)
            return Block(SubmissionReadinessCode.PlatformNotSupported, "Only a registered official platform API is supported.");
        if (!platform.TermsAllowAutomation)
            return Block(SubmissionReadinessCode.PlatformTermsDisallowAutomation, "The platform terms do not allow automated submission.");
        if (platform.RequiresCaptcha)
            return Block(SubmissionReadinessCode.CaptchaRequired, "A platform requiring CAPTCHA cannot be automated.");
        if (!platform.SupportsIdempotencyKey)
            return Block(SubmissionReadinessCode.DuplicateProtectionUnavailable, "The platform cannot guarantee duplicate protection.");

        if (approval is null || !approval.Confirmed || approval.CandidacyId != candidacy.Id)
            return Block(SubmissionReadinessCode.ApprovalRequired, "Explicit approval for this application is required.");

        var expectedReviewSha256 = ReviewSha256(candidacy);
        if (approval.Revision != candidacy.Revision || !FixedTimeEquals(approval.ReviewSha256, expectedReviewSha256))
            return Block(SubmissionReadinessCode.ReviewChanged, "The approved review no longer matches the current application revision.");

        return Block(SubmissionReadinessCode.Ready, "The application passed the controlled-submission checks.");
    }

    public static string ReviewSha256(Candidacy candidacy)
    {
        ArgumentNullException.ThrowIfNull(candidacy);
        var resume = candidacy.Resume ?? throw new InvalidOperationException("Resume is required for submission review.");
        var coverLetter = candidacy.CoverLetter ?? throw new InvalidOperationException("Cover letter is required for submission review.");
        var canonicalReview = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            candidacy.Id,
            candidacy.JobTitle,
            candidacy.CompanyName,
            resumeExportId = resume.ExportId,
            resumeSha256 = resume.Sha256,
            coverLetterExportId = coverLetter.ExportId,
            coverLetterSha256 = coverLetter.Sha256
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalReview))).ToLowerInvariant();
    }

    private static SubmissionReadiness Block(SubmissionReadinessCode code, string message) => new(code, message);

    private static bool FixedTimeEquals(string? provided, string expected)
    {
        if (provided is null || provided.Length != expected.Length || provided.Any(c => !char.IsAsciiHexDigit(c))) return false;
        try { return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(provided), Convert.FromHexString(expected)); }
        catch (FormatException) { return false; }
    }
}
