using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public sealed class ControlledSubmissionPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly SubmissionPlatformCapability Platform = new("official-api", true, true, false, true);

    private static Candidacy Approved()
    {
        var candidacy = Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", Now);
        candidacy.AttachVerifiedDocument(Document(candidacy, ApplicationDocumentKind.Resume), candidacy.OwnerId, candidacy.Revision, Now);
        candidacy.AttachVerifiedDocument(Document(candidacy, ApplicationDocumentKind.CoverLetter), candidacy.OwnerId, candidacy.Revision, Now);
        candidacy.RequestReview(candidacy.OwnerId, candidacy.Revision, Now);
        candidacy.Approve(candidacy.OwnerId, candidacy.Revision, true, Now);
        return candidacy;
    }

    private static ApplicationDocument Document(Candidacy candidacy, ApplicationDocumentKind kind) =>
        new(Guid.NewGuid(), candidacy.OwnerId, candidacy.ProfileId, candidacy.JobId, kind, new string('a', 64));

    private static SubmissionReviewApproval Approval(Candidacy candidacy) =>
        new(candidacy.Id, candidacy.Revision, ControlledSubmissionPolicy.ReviewSha256(candidacy), true);

    [Fact]
    public void ReadinessRequiresEnabledAutomationAndExactExplicitReview()
    {
        var candidacy = Approved();

        var disabled = ControlledSubmissionPolicy.Evaluate(false, candidacy, Platform, Approval(candidacy));
        Assert.Equal(SubmissionReadinessCode.AutomationDisabled, disabled.Code);

        var ready = ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, Approval(candidacy));
        Assert.True(ready.IsReady);
        Assert.Equal(SubmissionReadinessCode.Ready, ready.Code);
    }

    [Fact]
    public void ApprovalIsBoundToCandidateRevisionAndReviewedContent()
    {
        var candidacy = Approved();
        var approval = Approval(candidacy);
        Assert.Equal(SubmissionReadinessCode.ApprovalRequired,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, approval with { Confirmed = false }).Code);
        Assert.Equal(SubmissionReadinessCode.ReviewChanged,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, approval with { Revision = approval.Revision - 1 }).Code);
        Assert.Equal(SubmissionReadinessCode.ReviewChanged,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, approval with { ReviewSha256 = new string('b', 64) }).Code);
        Assert.Equal(SubmissionReadinessCode.ReviewChanged,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, approval with { ReviewSha256 = "not-a-hash" }).Code);
        Assert.Equal(SubmissionReadinessCode.ReviewChanged,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, approval with { ReviewSha256 = null! }).Code);
    }

    [Fact]
    public void ExplicitConsentIsRecordedInHistoryAndInvalidatedWhenDossierRevisionChanges()
    {
        var candidacy = Approved();
        var hash = ControlledSubmissionPolicy.ReviewSha256(candidacy);
        Assert.Throws<InvalidOperationException>(() => candidacy.ApproveControlledSubmission(
            candidacy.OwnerId, candidacy.Revision, false, hash, Now));

        candidacy.ApproveControlledSubmission(candidacy.OwnerId, candidacy.Revision, true, hash, Now);
        var approval = Assert.IsType<SubmissionReviewApproval>(candidacy.SubmissionApproval);
        Assert.Equal(candidacy.Revision, approval.Revision);
        Assert.Equal(hash, approval.ReviewSha256);
        Assert.True(ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, approval).IsReady);

        var restored = Candidacy.Restore(candidacy.Id, candidacy.OwnerId, candidacy.ProfileId, candidacy.JobId,
            candidacy.JobTitle, candidacy.CompanyName, candidacy.Events);
        Assert.Equal(approval, restored.SubmissionApproval);
        restored.AddNote(restored.OwnerId, restored.Revision, "Update", Now);
        Assert.Equal(SubmissionReadinessCode.ReviewChanged,
            ControlledSubmissionPolicy.Evaluate(true, restored, Platform, restored.SubmissionApproval).Code);
    }

    [Fact]
    public void AttemptReservationIsIdempotentAndConsumesTheConsent()
    {
        var candidacy = Approved();
        var reviewSha256 = ControlledSubmissionPolicy.ReviewSha256(candidacy);
        candidacy.ApproveControlledSubmission(candidacy.OwnerId, candidacy.Revision, true, reviewSha256, Now);
        var approvedRevision = candidacy.Revision;
        var attemptId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => candidacy.PrepareControlledSubmissionAttempt(
            candidacy.OwnerId, approvedRevision, false, true, attemptId, Platform, reviewSha256, Now));
        Assert.Null(candidacy.SubmissionAttempt);

        candidacy.PrepareControlledSubmissionAttempt(candidacy.OwnerId, approvedRevision, true, true, attemptId, Platform, reviewSha256, Now);
        var eventCount = candidacy.Events.Count;
        Assert.Equal(attemptId, candidacy.SubmissionAttempt!.AttemptId);
        Assert.Equal(SubmissionReadinessCode.AttemptAlreadyPrepared,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, candidacy.SubmissionApproval).Code);

        candidacy.PrepareControlledSubmissionAttempt(candidacy.OwnerId, approvedRevision, true, true, attemptId, Platform, reviewSha256, Now);
        Assert.Equal(eventCount, candidacy.Events.Count);
        Assert.Throws<UnauthorizedAccessException>(() => candidacy.PrepareControlledSubmissionAttempt(
            Guid.NewGuid(), approvedRevision, true, true, attemptId, Platform, reviewSha256, Now));
        Assert.Throws<InvalidOperationException>(() => candidacy.PrepareControlledSubmissionAttempt(
            candidacy.OwnerId, approvedRevision, true, true, Guid.NewGuid(), Platform, reviewSha256, Now));

        var restored = Candidacy.Restore(candidacy.Id, candidacy.OwnerId, candidacy.ProfileId, candidacy.JobId,
            candidacy.JobTitle, candidacy.CompanyName, candidacy.Events);
        Assert.Equal(candidacy.SubmissionAttempt, restored.SubmissionAttempt);
    }

    [Fact]
    public void UnknownResultIsIdempotentAndPermanentlyBlocksAutomaticRetry()
    {
        var candidacy = Approved();
        var hash = ControlledSubmissionPolicy.ReviewSha256(candidacy);
        candidacy.ApproveControlledSubmission(candidacy.OwnerId, candidacy.Revision, true, hash, Now);
        var attemptId = Guid.NewGuid();
        candidacy.PrepareControlledSubmissionAttempt(candidacy.OwnerId, candidacy.Revision, true, true, attemptId, Platform, hash, Now);
        var preparedRevision = candidacy.Revision;
        var result = new ControlledSubmissionResult(attemptId, ControlledSubmissionOutcome.Unknown, Now, null);

        candidacy.RecordControlledSubmissionResult(candidacy.OwnerId, preparedRevision, true, result, Now);
        var resultRevision = candidacy.Revision;
        candidacy.RecordControlledSubmissionResult(candidacy.OwnerId, preparedRevision, true, result, Now);

        Assert.Equal(resultRevision, candidacy.Revision);
        Assert.Equal(ControlledSubmissionOutcome.Unknown, candidacy.SubmissionAttemptResult!.Outcome);
        Assert.Equal(SubmissionReadinessCode.AttemptAlreadyPrepared,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, candidacy.SubmissionApproval).Code);
        Assert.Throws<InvalidOperationException>(() => candidacy.RecordControlledSubmissionResult(candidacy.OwnerId,
            candidacy.Revision, true, result with { Outcome = ControlledSubmissionOutcome.DefinitelyNotSubmitted }, Now));
        var restored = Candidacy.Restore(candidacy.Id, candidacy.OwnerId, candidacy.ProfileId, candidacy.JobId,
            candidacy.JobTitle, candidacy.CompanyName, candidacy.Events);
        Assert.Equal(result, restored.SubmissionAttemptResult);
    }

    [Fact]
    public void ConfirmedSubmissionChangesStatusAndAllowsRecruiterUpdates()
    {
        var candidacy = Approved();
        var hash = ControlledSubmissionPolicy.ReviewSha256(candidacy);
        candidacy.ApproveControlledSubmission(candidacy.OwnerId, candidacy.Revision, true, hash, Now);
        var attemptId = Guid.NewGuid();
        candidacy.PrepareControlledSubmissionAttempt(candidacy.OwnerId, candidacy.Revision, true, true, attemptId, Platform, hash, Now);

        candidacy.RecordControlledSubmissionResult(candidacy.OwnerId, candidacy.Revision, true,
            new(attemptId, ControlledSubmissionOutcome.Submitted, Now, "receipt-123"), Now);
        Assert.Equal(CandidacyStatus.Submitted, candidacy.Status);
        Assert.Equal("receipt-123", candidacy.SubmissionAttemptResult!.Reference);
        candidacy.RecordRecruiterUpdate(candidacy.OwnerId, candidacy.Revision, true, CandidacyStatus.InReview,
            new(Now, ContactChannel.Email, "Received"), Now);

        var restored = Candidacy.Restore(candidacy.Id, candidacy.OwnerId, candidacy.ProfileId, candidacy.JobId,
            candidacy.JobTitle, candidacy.CompanyName, candidacy.Events);
        Assert.Equal(CandidacyStatus.InReview, restored.Status);
        Assert.Equal("receipt-123", restored.SubmissionAttemptResult!.Reference);
    }

    [Fact]
    public void DefinitelyNotSubmittedRequiresFreshApprovalBeforeANewAttempt()
    {
        var candidacy = Approved();
        var hash = ControlledSubmissionPolicy.ReviewSha256(candidacy);
        candidacy.ApproveControlledSubmission(candidacy.OwnerId, candidacy.Revision, true, hash, Now);
        var attemptId = Guid.NewGuid();
        candidacy.PrepareControlledSubmissionAttempt(candidacy.OwnerId, candidacy.Revision, true, true, attemptId, Platform, hash, Now);
        candidacy.RecordControlledSubmissionResult(candidacy.OwnerId, candidacy.Revision, true,
            new(attemptId, ControlledSubmissionOutcome.DefinitelyNotSubmitted, Now, null), Now);

        Assert.Equal(SubmissionReadinessCode.ReviewChanged,
            ControlledSubmissionPolicy.Evaluate(true, candidacy, Platform, candidacy.SubmissionApproval).Code);
        var newHash = ControlledSubmissionPolicy.ReviewSha256(candidacy);
        candidacy.ApproveControlledSubmission(candidacy.OwnerId, candidacy.Revision, true, newHash, Now);
        var secondAttemptId = Guid.NewGuid();
        candidacy.PrepareControlledSubmissionAttempt(candidacy.OwnerId, candidacy.Revision, true, true, secondAttemptId, Platform, newHash, Now);

        Assert.Equal(secondAttemptId, candidacy.SubmissionAttempt!.AttemptId);
        Assert.Null(candidacy.SubmissionAttemptResult);
        var restored = Candidacy.Restore(candidacy.Id, candidacy.OwnerId, candidacy.ProfileId, candidacy.JobId,
            candidacy.JobTitle, candidacy.CompanyName, candidacy.Events);
        Assert.Equal(secondAttemptId, restored.SubmissionAttempt!.AttemptId);
        Assert.Null(restored.SubmissionAttemptResult);
    }

    [Fact]
    public void PlatformMustMeetOfficialTermsCaptchaAndDuplicateProtectionRequirements()
    {
        var candidacy = Approved();
        var approval = Approval(candidacy);

        Assert.Equal(SubmissionReadinessCode.PlatformNotSupported,
            Evaluate(platform: Platform with { HasOfficialApi = false }));
        Assert.Equal(SubmissionReadinessCode.PlatformTermsDisallowAutomation,
            Evaluate(platform: Platform with { TermsAllowAutomation = false }));
        Assert.Equal(SubmissionReadinessCode.CaptchaRequired,
            Evaluate(platform: Platform with { RequiresCaptcha = true }));
        Assert.Equal(SubmissionReadinessCode.DuplicateProtectionUnavailable,
            Evaluate(platform: Platform with { SupportsIdempotencyKey = false }));
        Assert.Equal(SubmissionReadinessCode.PlatformNotSupported,
            Evaluate(platform: Platform with { PlatformId = " " }));

        SubmissionReadinessCode Evaluate(SubmissionPlatformCapability platform) =>
            ControlledSubmissionPolicy.Evaluate(true, candidacy, platform, approval).Code;
    }

    [Fact]
    public void OnlyApprovedNotPreviouslySubmittedCandidacyCanPass()
    {
        var draft = Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", Now);
        Assert.Equal(SubmissionReadinessCode.CandidacyNotApproved,
            ControlledSubmissionPolicy.Evaluate(true, draft, Platform, null).Code);

        var submitted = Approved();
        submitted.DeclareManualSubmission(submitted.OwnerId, submitted.Revision, true,
            new(Now, ContactChannel.Email, null), Now);
        Assert.Equal(SubmissionReadinessCode.CandidacyNotApproved,
            ControlledSubmissionPolicy.Evaluate(true, submitted, Platform, null).Code);
    }
}
