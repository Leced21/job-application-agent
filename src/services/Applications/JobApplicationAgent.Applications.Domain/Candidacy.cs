using System.Security.Cryptography;

namespace JobApplicationAgent.Applications.Domain;

public enum CandidacyStatus { Draft = 1, ReadyForReview = 2, Approved = 3, Withdrawn = 4, Submitted = 5, InReview = 6, Interview = 7, Offer = 8, Rejected = 9 }
public enum ApplicationDocumentKind { Resume = 1, CoverLetter = 2 }
public enum ContactChannel { Email = 1, JobBoard = 2, CompanyWebsite = 3, Phone = 4, InPerson = 5, Other = 6 }
public sealed record ManualSubmission(DateTime OccurredAtUtc, ContactChannel Channel, string? Reference);
public sealed record RecruiterUpdate(DateTime OccurredAtUtc, ContactChannel Channel, string Message);

public sealed record ApplicationDocument(Guid ExportId, Guid OwnerId, Guid ProfileId, Guid JobId,
    ApplicationDocumentKind Kind, string Sha256);
public sealed record CandidacyEvent(int Sequence, string Kind, CandidacyStatus Status,
    long Revision, Guid ActorId, DateTime AtUtc, string? Note, ApplicationDocument? Document,
    ManualSubmission? Submission = null, RecruiterUpdate? RecruiterUpdate = null, FollowUpChange? FollowUp = null,
    SubmissionReviewApproval? SubmissionApproval = null, ControlledSubmissionAttempt? SubmissionAttempt = null,
    ControlledSubmissionResult? SubmissionResult = null);

public sealed class Candidacy
{
    private readonly List<CandidacyEvent> events = [];
    private ApplicationDocument? resume;
    private ApplicationDocument? coverLetter;
    private readonly Dictionary<Guid, FollowUpChange> followUps = [];

    private Candidacy(Guid ownerId, Guid profileId, Guid jobId, string title, string company, DateTime atUtc, Guid? id = null)
    {
        Id = id ?? Guid.NewGuid(); OwnerId = ownerId; ProfileId = profileId; JobId = jobId;
        JobTitle = RequiredText(title, 200); CompanyName = RequiredText(company, 200);
        Status = CandidacyStatus.Draft;
        Record("Created", ownerId, atUtc);
    }

    public Guid Id { get; }
    public Guid OwnerId { get; }
    public Guid ProfileId { get; }
    public Guid JobId { get; }
    public string JobTitle { get; }
    public string CompanyName { get; }
    public CandidacyStatus Status { get; private set; }
    public long Revision { get; private set; }
    public ApplicationDocument? Resume => resume;
    public ApplicationDocument? CoverLetter => coverLetter;
    public ManualSubmission? Submission => events.FirstOrDefault(e => e.Submission is not null)?.Submission;
    public SubmissionReviewApproval? SubmissionApproval => events.LastOrDefault(e => e.SubmissionApproval is not null)?.SubmissionApproval;
    public ControlledSubmissionAttempt? SubmissionAttempt => events.LastOrDefault(e => e.SubmissionAttempt is not null)?.SubmissionAttempt;
    public ControlledSubmissionResult? SubmissionAttemptResult
    {
        get
        {
            var latestAttemptIndex = events.FindLastIndex(e => e.SubmissionAttempt is not null);
            return latestAttemptIndex < 0 ? null : events.Skip(latestAttemptIndex + 1).LastOrDefault(e => e.SubmissionResult is not null)?.SubmissionResult;
        }
    }
    public IReadOnlyList<FollowUpChange> FollowUps => followUps.Values.OrderBy(x => x.Plan.DueAtUtc).ThenBy(x => x.Plan.Id).ToArray();
    public FollowUpChange? PendingFollowUp => followUps.Values.SingleOrDefault(x => x.Status == FollowUpStatus.Planned);
    public IReadOnlyList<CandidacyEvent> Events => events.AsReadOnly();

    public static Candidacy Create(Guid ownerId, Guid profileId, Guid jobId, string title, string company, DateTime atUtc)
    {
        if (ownerId == Guid.Empty || profileId == Guid.Empty || jobId == Guid.Empty)
            throw new ArgumentException("Owner, profile and job are required.");
        RequireUtc(atUtc);
        return new(ownerId, profileId, jobId, title, company, atUtc);
    }

    // Rehydrate by replaying validated events rather than exposing mutable state to persistence.
    public static Candidacy Restore(Guid id, Guid ownerId, Guid profileId, Guid jobId, string title, string company,
        IReadOnlyList<CandidacyEvent> history)
    {
        if (id == Guid.Empty || ownerId == Guid.Empty || profileId == Guid.Empty || jobId == Guid.Empty || history.Count is < 1 or > 1000)
            throw new ArgumentException("Invalid stored candidature.");
        RequireUtc(history[0].AtUtc);
        var c = new Candidacy(ownerId, profileId, jobId, title, company, history[0].AtUtc, id);
        if (c.Events[0] != history[0]) throw new ArgumentException("Invalid creation event.");
        foreach (var e in history.Skip(1))
        {
            var previousCount = c.events.Count;
            switch (e.Kind)
            {
                case "DocumentAttached": c.AttachVerifiedDocument(e.Document!, e.ActorId, c.Revision, e.AtUtc); break;
                case "ReviewRequested": c.RequestReview(e.ActorId, c.Revision, e.AtUtc); break;
                case "Approved": c.Approve(e.ActorId, c.Revision, true, e.AtUtc); break;
                case "ReturnedToDraft": c.ReturnToDraft(e.ActorId, c.Revision, e.AtUtc); break;
                case "Withdrawn": c.Withdraw(e.ActorId, c.Revision, true, e.Note!, e.AtUtc); break;
                case "NoteAdded": c.AddNote(e.ActorId, c.Revision, e.Note!, e.AtUtc); break;
                case "ManualSubmissionDeclared": c.DeclareManualSubmission(e.ActorId, c.Revision, true, e.Submission!, e.AtUtc); break;
                case "ControlledSubmissionApproved": c.ApproveControlledSubmission(e.ActorId, c.Revision, true, e.SubmissionApproval!.ReviewSha256, e.AtUtc); break;
                case "ControlledSubmissionAttemptPrepared": c.PrepareControlledSubmissionAttempt(e.ActorId, e.SubmissionAttempt!.ApprovedRevision,
                    true, true, e.SubmissionAttempt.AttemptId, e.SubmissionAttempt.Platform, e.SubmissionAttempt.ReviewSha256, e.AtUtc); break;
                case "ControlledSubmissionResultRecorded": c.RecordControlledSubmissionResult(e.ActorId, c.Revision, true, e.SubmissionResult!, e.AtUtc); break;
                case "RecruiterUpdateRecorded": c.RecordRecruiterUpdate(e.ActorId, c.Revision, true, e.Status, e.RecruiterUpdate!, e.AtUtc); break;
                case "FollowUpPlanned": c.PlanFollowUp(e.ActorId, c.Revision, e.FollowUp!.Plan, e.AtUtc); break;
                case "FollowUpRescheduled": c.RescheduleFollowUp(e.ActorId, c.Revision, e.FollowUp!.Plan, e.AtUtc); break;
                case "FollowUpCompleted": c.CompleteFollowUp(e.ActorId, c.Revision, e.FollowUp!.Plan.Id, true, e.FollowUp.OccurredAtUtc!.Value, e.FollowUp.ActualChannel!.Value, e.FollowUp.Outcome!, e.AtUtc); break;
                case "FollowUpCancelled": c.CancelFollowUp(e.ActorId, c.Revision, e.FollowUp!.Plan.Id, true, e.FollowUp.Outcome!, e.AtUtc); break;
                default: throw new ArgumentException("Unknown candidature event.");
            }
            if (c.events.Count != previousCount + 1 || c.events[^1] != e)
                throw new ArgumentException("Stored event disagrees with domain rules.");
        }
        return c;
    }

    // Only a trusted adapter may supply a reference after verifying export approval and source bindings.
    public void AttachVerifiedDocument(ApplicationDocument document, Guid actorId, long expectedRevision, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        if (Status != CandidacyStatus.Draft) throw new InvalidOperationException("Return to Draft before changing documents.");
        ArgumentNullException.ThrowIfNull(document);
        if (document.ExportId == Guid.Empty || document.OwnerId != OwnerId || document.ProfileId != ProfileId || document.JobId != JobId
            || !Enum.IsDefined(document.Kind) || document.Sha256 is null || document.Sha256.Length != 64
            || document.Sha256.Any(c => !char.IsAsciiHexDigit(c)))
            throw new ArgumentException("Invalid verified document binding.");
        document = document with { Sha256 = document.Sha256.ToLowerInvariant() };
        var other = document.Kind == ApplicationDocumentKind.Resume ? coverLetter : resume;
        if (other?.ExportId == document.ExportId) throw new ArgumentException("Two distinct exports are required.");
        var current = document.Kind == ApplicationDocumentKind.Resume ? resume : coverLetter;
        if (current == document) return;
        if (document.Kind == ApplicationDocumentKind.Resume) resume = document; else coverLetter = document;
        Record("DocumentAttached", actorId, atUtc, document.Kind.ToString(), document);
    }

    public void RequestReview(Guid actorId, long expectedRevision, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        if (Status != CandidacyStatus.Draft || resume is null || coverLetter is null)
            throw new InvalidOperationException("A draft with both verified exports is required.");
        Status = CandidacyStatus.ReadyForReview;
        Record("ReviewRequested", actorId, atUtc);
    }

    public void Approve(Guid actorId, long expectedRevision, bool confirmed, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        if (Status != CandidacyStatus.ReadyForReview || !confirmed)
            throw new InvalidOperationException("Explicit confirmation of the reviewed revision is required.");
        Status = CandidacyStatus.Approved;
        Record("Approved", actorId, atUtc);
    }

    public void ReturnToDraft(Guid actorId, long expectedRevision, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        if (Status is not (CandidacyStatus.ReadyForReview or CandidacyStatus.Approved))
            throw new InvalidOperationException("Only a reviewed dossier can return to Draft.");
        Status = CandidacyStatus.Draft;
        Record("ReturnedToDraft", actorId, atUtc);
    }

    public void Withdraw(Guid actorId, long expectedRevision, bool confirmed, string reason, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc, reserveWithdrawal: false);
        if (Status is CandidacyStatus.Withdrawn or CandidacyStatus.Offer or CandidacyStatus.Rejected || !confirmed) throw new InvalidOperationException("Explicit withdrawal from an active dossier is required.");
        reason = RequiredText(reason, 2000);
        Status = CandidacyStatus.Withdrawn;
        Record("Withdrawn", actorId, atUtc, reason, followUp: ClosureCancellation(atUtc));
    }

    public void AddNote(Guid actorId, long expectedRevision, string note, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        Record("NoteAdded", actorId, atUtc, RequiredText(note, 2000));
    }

    public void DeclareManualSubmission(Guid actorId, long expectedRevision, bool confirmed, ManualSubmission submission, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        if (Status != CandidacyStatus.Approved || !confirmed) throw new InvalidOperationException("Confirm manual submission of the approved dossier.");
        ArgumentNullException.ThrowIfNull(submission);
        ValidateOccurrence(submission.OccurredAtUtc, submission.Channel, events.Last(e => e.Kind == "Approved").AtUtc, atUtc);
        var reference = submission.Reference is null ? null : RequiredText(submission.Reference, 500);
        Status = CandidacyStatus.Submitted;
        Record("ManualSubmissionDeclared", actorId, atUtc, submission: submission with { Reference = reference });
    }

    public void ApproveControlledSubmission(Guid actorId, long expectedRevision, bool confirmed, string reviewSha256, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        if (Status != CandidacyStatus.Approved || Submission is not null || !confirmed)
            throw new InvalidOperationException("Explicit review approval requires an approved, not-yet-submitted application.");
        var expected = ControlledSubmissionPolicy.ReviewSha256(this);
        if (reviewSha256 is null || reviewSha256.Length != 64 || reviewSha256.Any(c => !char.IsAsciiHexDigit(c)) ||
            !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(reviewSha256), Convert.FromHexString(expected)))
            throw new InvalidOperationException("The reviewed application content has changed.");
        var approval = new SubmissionReviewApproval(Id, Revision + 1, expected, true);
        Record("ControlledSubmissionApproved", actorId, atUtc, submissionApproval: approval);
    }

    public void PrepareControlledSubmissionAttempt(Guid actorId, long expectedRevision, bool automationEnabled, bool confirmed, Guid attemptId,
        SubmissionPlatformCapability platform, string reviewSha256, DateTime atUtc)
    {
        if (actorId != OwnerId) throw new UnauthorizedAccessException("Owner required.");
        ArgumentNullException.ThrowIfNull(platform);
        if (SubmissionAttempt is { } existing && SubmissionAttemptResult?.Outcome != ControlledSubmissionOutcome.DefinitelyNotSubmitted)
        {
            if (existing.AttemptId == attemptId && existing.ApprovedRevision == expectedRevision &&
                existing.ReviewSha256 == reviewSha256 && existing.Platform == platform && confirmed)
                return;
            throw new InvalidOperationException("A submission attempt is already reserved for this application.");
        }

        Check(actorId, expectedRevision, atUtc);
        if (attemptId == Guid.Empty || !confirmed)
            throw new InvalidOperationException("Explicit confirmation and an idempotency key are required.");
        var approval = SubmissionApproval;
        var readiness = ControlledSubmissionPolicy.Evaluate(automationEnabled, this, platform, approval);
        var requestMatchesApproval = approval is not null && reviewSha256 is { Length: 64 } && reviewSha256.All(char.IsAsciiHexDigit) &&
            CryptographicOperations.FixedTimeEquals(Convert.FromHexString(reviewSha256), Convert.FromHexString(approval.ReviewSha256));
        if (!readiness.IsReady || !requestMatchesApproval)
            throw new InvalidOperationException(readiness.IsReady ? "Review approval does not match the prepared submission." : readiness.Message);

        var attempt = new ControlledSubmissionAttempt(attemptId, expectedRevision, approval!.ReviewSha256, platform, atUtc);
        Record("ControlledSubmissionAttemptPrepared", actorId, atUtc, submissionAttempt: attempt);
    }

    public void RecordControlledSubmissionResult(Guid actorId, long expectedRevision, bool confirmed,
        ControlledSubmissionResult result, DateTime recordedAtUtc)
    {
        if (actorId != OwnerId) throw new UnauthorizedAccessException("Owner required.");
        ArgumentNullException.ThrowIfNull(result);
        if (result.AttemptId == Guid.Empty || !Enum.IsDefined(result.Outcome))
            throw new ArgumentException("A valid attempt ID and outcome are required.");
        var attempt = SubmissionAttempt;
        if (attempt is null || attempt.AttemptId != result.AttemptId)
            throw new InvalidOperationException("The result must refer to the reserved attempt.");
        if (SubmissionAttemptResult is { } existing)
        {
            if (existing == result && confirmed) return;
            throw new InvalidOperationException("A result has already been recorded for this attempt.");
        }

        Check(actorId, expectedRevision, recordedAtUtc);
        RequireUtc(result.OccurredAtUtc);
        if (!confirmed || result.OccurredAtUtc < attempt.PreparedAtUtc || result.OccurredAtUtc > recordedAtUtc)
            throw new InvalidOperationException("Explicit confirmation and a valid result time are required.");
        var reference = result.Reference is null ? null : RequiredText(result.Reference, 500);
        if (result.Outcome != ControlledSubmissionOutcome.Submitted && reference is not null)
            throw new ArgumentException("A platform reference is only valid for a confirmed submission.");
        var normalized = result with { Reference = reference };
        if (normalized.Outcome == ControlledSubmissionOutcome.Submitted)
            Status = CandidacyStatus.Submitted;
        Record("ControlledSubmissionResultRecorded", actorId, recordedAtUtc, submissionResult: normalized);
    }

    public void RecordRecruiterUpdate(Guid actorId, long expectedRevision, bool confirmed, CandidacyStatus target, RecruiterUpdate update, DateTime atUtc)
    {
        Check(actorId, expectedRevision, atUtc);
        var allowed = Status switch {
            CandidacyStatus.Submitted => target is CandidacyStatus.Submitted or CandidacyStatus.InReview or CandidacyStatus.Interview or CandidacyStatus.Offer or CandidacyStatus.Rejected,
            CandidacyStatus.InReview => target is CandidacyStatus.InReview or CandidacyStatus.Interview or CandidacyStatus.Offer or CandidacyStatus.Rejected,
            CandidacyStatus.Interview => target is CandidacyStatus.Interview or CandidacyStatus.Offer or CandidacyStatus.Rejected,
            _ => false
        };
        if (!confirmed || !allowed) throw new InvalidOperationException("Invalid or unconfirmed recruiter transition.");
        ArgumentNullException.ThrowIfNull(update);
        var lower = events.LastOrDefault(e => e.RecruiterUpdate is not null)?.RecruiterUpdate?.OccurredAtUtc ??
            (Submission?.OccurredAtUtc ?? events.LastOrDefault(e => e.SubmissionResult?.Outcome == ControlledSubmissionOutcome.Submitted)?.SubmissionResult?.OccurredAtUtc)
            ?? throw new InvalidOperationException("A submitted application date is required before recruiter updates.");
        ValidateOccurrence(update.OccurredAtUtc, update.Channel, lower, atUtc);
        var message = RequiredText(update.Message, 2000);
        Status = target;
        Record("RecruiterUpdateRecorded", actorId, atUtc, recruiterUpdate: update with { Message = message },
            followUp: target is CandidacyStatus.Offer or CandidacyStatus.Rejected ? ClosureCancellation(atUtc) : null);
    }

    public void PlanFollowUp(Guid actor, long revision, FollowUpPlan plan, DateTime atUtc)
    {
        Check(actor, revision, atUtc); RequireActiveFollowUp();
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Id == Guid.Empty || followUps.ContainsKey(plan.Id) || PendingFollowUp is not null)
            throw new InvalidOperationException("A unique follow-up with no other pending follow-up is required.");
        plan = ValidPlan(plan, atUtc);
        Record("FollowUpPlanned", actor, atUtc, followUp: new(plan, FollowUpStatus.Planned));
    }
    public void RescheduleFollowUp(Guid actor, long revision, FollowUpPlan plan, DateTime atUtc)
    {
        Check(actor, revision, atUtc); RequireActiveFollowUp(); ArgumentNullException.ThrowIfNull(plan);
        _ = Pending(plan.Id); plan = ValidPlan(plan, atUtc);
        Record("FollowUpRescheduled", actor, atUtc, followUp: new(plan, FollowUpStatus.Planned));
    }
    public void CompleteFollowUp(Guid actor, long revision, Guid id, bool confirmed, DateTime occurredAtUtc, ContactChannel channel, string outcome, DateTime atUtc)
    {
        Check(actor, revision, atUtc); RequireActiveFollowUp();
        if (!confirmed) throw new InvalidOperationException("Confirm the manual follow-up was performed.");
        var pending = Pending(id);
        var plannedAt = events.Last(e => e.FollowUp?.Plan.Id == id && e.Kind is "FollowUpPlanned" or "FollowUpRescheduled").AtUtc;
        ValidateOccurrence(occurredAtUtc, channel, plannedAt, atUtc);
        outcome = RequiredText(outcome, 1000);
        Record("FollowUpCompleted", actor, atUtc, followUp: new(pending.Plan, FollowUpStatus.Completed, occurredAtUtc, channel, outcome));
    }
    public void CancelFollowUp(Guid actor, long revision, Guid id, bool confirmed, string reason, DateTime atUtc)
    {
        Check(actor, revision, atUtc); RequireActiveFollowUp();
        if (!confirmed) throw new InvalidOperationException("Confirm cancellation.");
        var pending = Pending(id); reason = RequiredText(reason, 1000);
        Record("FollowUpCancelled", actor, atUtc, followUp: new(pending.Plan, FollowUpStatus.Cancelled, atUtc, null, reason));
    }
    private void RequireActiveFollowUp()
    {
        if (Status is not (CandidacyStatus.Submitted or CandidacyStatus.InReview or CandidacyStatus.Interview))
            throw new InvalidOperationException("Follow-ups require an active, submitted candidature.");
    }
    private FollowUpChange Pending(Guid id) => PendingFollowUp is { } pending && pending.Plan.Id == id ? pending :
        throw new InvalidOperationException("This follow-up is not pending.");
    private static FollowUpPlan ValidPlan(FollowUpPlan plan, DateTime atUtc)
    {
        RequireUtc(plan.DueAtUtc);
        static DateTime Microsecond(DateTime value) => new(value.Ticks - value.Ticks % 10, DateTimeKind.Utc);
        var due = Microsecond(plan.DueAtUtc);
        if (!Enum.IsDefined(plan.Channel) || due < Microsecond(atUtc) || due > Microsecond(atUtc.AddDays(365)))
            throw new ArgumentException("A supported channel and UTC due date within the next 365 days are required.");
        return plan with { Note = RequiredText(plan.Note, 1000), DueAtUtc = due };
    }
    private FollowUpChange? ClosureCancellation(DateTime atUtc) => PendingFollowUp is { } pending
        ? new(pending.Plan, FollowUpStatus.Cancelled, atUtc, null, "Candidature clôturée") : null;

    private static void ValidateOccurrence(DateTime occurred, ContactChannel channel, DateTime lower, DateTime recorded)
    {
        RequireUtc(occurred);
        if (!Enum.IsDefined(channel) || occurred < lower || occurred > recorded)
            throw new ArgumentException("A supported channel and chronological, non-future UTC event time are required.");
    }

    private void Check(Guid actorId, long expectedRevision, DateTime atUtc, bool reserveWithdrawal = true)
    {
        if (actorId != OwnerId) throw new UnauthorizedAccessException("Owner required.");
        if (expectedRevision != Revision) throw new InvalidOperationException("Stale dossier revision.");
        if (events.Count >= (reserveWithdrawal ? 999 : 1000)) throw new InvalidOperationException("Dossier history limit reached.");
        RequireUtc(atUtc);
        if (atUtc < events[^1].AtUtc) throw new ArgumentException("Event time cannot move backwards.");
    }

    private void Record(string kind, Guid actorId, DateTime atUtc, string? note = null, ApplicationDocument? document = null,
        ManualSubmission? submission = null, RecruiterUpdate? recruiterUpdate = null, FollowUpChange? followUp = null,
        SubmissionReviewApproval? submissionApproval = null, ControlledSubmissionAttempt? submissionAttempt = null,
        ControlledSubmissionResult? submissionResult = null)
    {
        Revision++;
        events.Add(new(events.Count + 1, kind, Status, Revision, actorId, atUtc, note, document, submission, recruiterUpdate, followUp, submissionApproval, submissionAttempt, submissionResult));
        if (followUp is not null) followUps[followUp.Plan.Id] = followUp;
    }
    private static void RequireUtc(DateTime atUtc)
    {
        if (atUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC timestamp required.");
    }
    private static string RequiredText(string text, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > maximum || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Invalid text.");
        return text.Trim();
    }
}
