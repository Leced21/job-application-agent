using System.Text.Json;
using System.Text.Json.Nodes;
using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public class ManualFollowUpTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static Candidacy State(CandidacyStatus status)
    {
        var c = Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", Now.AddDays(-2));
        if (status == CandidacyStatus.Draft) return c;
        foreach (var kind in new[] { ApplicationDocumentKind.Resume, ApplicationDocumentKind.CoverLetter })
            c.AttachVerifiedDocument(new(Guid.NewGuid(), c.OwnerId, c.ProfileId, c.JobId, kind, new string('a', 64)), c.OwnerId, c.Revision, Now.AddDays(-2));
        c.RequestReview(c.OwnerId, c.Revision, Now.AddDays(-2));
        if (status == CandidacyStatus.ReadyForReview) return c;
        c.Approve(c.OwnerId, c.Revision, true, Now.AddDays(-2));
        if (status == CandidacyStatus.Approved) return c;
        if (status == CandidacyStatus.Withdrawn) { c.Withdraw(c.OwnerId, c.Revision, true, "Withdrawn", Now); return c; }
        c.DeclareManualSubmission(c.OwnerId, c.Revision, true, new(Now.AddDays(-1), ContactChannel.CompanyWebsite, "CASE-1"), Now);
        if (status != CandidacyStatus.Submitted) c.RecordRecruiterUpdate(c.OwnerId, c.Revision, true, status, new(Now.AddHours(-12), ContactChannel.Email, "Feedback"), Now);
        return c;
    }
    public static IEnumerable<object[]> Transitions()
    {
        var allowed = new HashSet<(CandidacyStatus, CandidacyStatus)> {
            (CandidacyStatus.Submitted, CandidacyStatus.Submitted), (CandidacyStatus.Submitted, CandidacyStatus.InReview),
            (CandidacyStatus.Submitted, CandidacyStatus.Interview), (CandidacyStatus.Submitted, CandidacyStatus.Offer), (CandidacyStatus.Submitted, CandidacyStatus.Rejected),
            (CandidacyStatus.InReview, CandidacyStatus.InReview), (CandidacyStatus.InReview, CandidacyStatus.Interview),
            (CandidacyStatus.InReview, CandidacyStatus.Offer), (CandidacyStatus.InReview, CandidacyStatus.Rejected),
            (CandidacyStatus.Interview, CandidacyStatus.Interview), (CandidacyStatus.Interview, CandidacyStatus.Offer), (CandidacyStatus.Interview, CandidacyStatus.Rejected) };
        foreach (var from in Enum.GetValues<CandidacyStatus>()) foreach (var to in Enum.GetValues<CandidacyStatus>()) yield return [from, to, allowed.Contains((from, to))];
    }
    [Theory]
    [MemberData(nameof(Transitions))]
    public void RecruiterTransitionTableIsEnforced(CandidacyStatus from, CandidacyStatus target, bool allowed)
    {
        var c = State(from); var revision = c.Revision;
        var update = new RecruiterUpdate(Now.AddHours(-1), ContactChannel.Phone, " Interview response ");
        if (!allowed)
        {
            Assert.Throws<InvalidOperationException>(() => c.RecordRecruiterUpdate(c.OwnerId, revision, true, target, update, Now));
            Assert.Equal(revision, c.Revision); Assert.Equal(from, c.Status); return;
        }
        c.RecordRecruiterUpdate(c.OwnerId, revision, true, target, update, Now);
        Assert.Equal(target, c.Status); Assert.Equal("Interview response", c.Events[^1].RecruiterUpdate!.Message);
        var restored = Candidacy.Restore(c.Id, c.OwnerId, c.ProfileId, c.JobId, c.JobTitle, c.CompanyName, c.Events);
        Assert.Equal(c.Events, restored.Events); Assert.Equal(c.Submission, restored.Submission);
    }
    [Theory]
    [InlineData(CandidacyStatus.Draft)]
    [InlineData(CandidacyStatus.ReadyForReview)]
    [InlineData(CandidacyStatus.Submitted)]
    [InlineData(CandidacyStatus.InReview)]
    [InlineData(CandidacyStatus.Interview)]
    [InlineData(CandidacyStatus.Offer)]
    [InlineData(CandidacyStatus.Rejected)]
    [InlineData(CandidacyStatus.Withdrawn)]
    public void OnlyApprovedDossierCanBeDeclaredSubmitted(CandidacyStatus status)
    {
        var c = State(status);
        Assert.Throws<InvalidOperationException>(() => c.DeclareManualSubmission(c.OwnerId, c.Revision, true, new(Now, ContactChannel.Email, null), Now));
    }
    [Fact]
    public void ManualDeclarationRequiresConfirmationAndPreservesBusinessAndRecordingDates()
    {
        var c = State(CandidacyStatus.Approved); var revision = c.Revision;
        var declaration = new ManualSubmission(Now.AddDays(-1), ContactChannel.JobBoard, "  REF-123  ");
        Assert.Throws<InvalidOperationException>(() => c.DeclareManualSubmission(c.OwnerId, revision, false, declaration, Now));
        c.DeclareManualSubmission(c.OwnerId, revision, true, declaration, Now);
        Assert.Equal(Now.AddDays(-1), c.Submission!.OccurredAtUtc); Assert.Equal(Now, c.Events[^1].AtUtc);
        Assert.Equal("REF-123", c.Submission.Reference);
        Assert.Throws<InvalidOperationException>(() => c.ReturnToDraft(c.OwnerId, c.Revision, Now));
        Assert.Throws<InvalidOperationException>(() => c.AttachVerifiedDocument(c.Resume!, c.OwnerId, c.Revision, Now));
    }
    [Theory]
    [InlineData("future")]
    [InlineData("before-approval")]
    [InlineData("unspecified")]
    [InlineData("channel")]
    [InlineData("reference")]
    public void InvalidSubmissionLeavesDossierApproved(string invalid)
    {
        var c = State(CandidacyStatus.Approved); var declaration = new ManualSubmission(Now.AddDays(-1), ContactChannel.Email, null);
        declaration = invalid switch {
            "future" => declaration with { OccurredAtUtc = Now.AddTicks(1) }, "before-approval" => declaration with { OccurredAtUtc = Now.AddDays(-3) },
            "unspecified" => declaration with { OccurredAtUtc = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified) },
            "channel" => declaration with { Channel = (ContactChannel)99 }, _ => declaration with { Reference = new string('a', 501) } };
        Assert.Throws<ArgumentException>(() => c.DeclareManualSubmission(c.OwnerId, c.Revision, true, declaration, Now));
        Assert.Equal(CandidacyStatus.Approved, c.Status); Assert.Null(c.Submission);
    }
    [Fact]
    public void FeedbackDatesCannotRewind_AndStaleOrForeignOrUnconfirmedUpdatesFail()
    {
        var c = State(CandidacyStatus.InReview); var revision = c.Revision;
        var update = new RecruiterUpdate(Now.AddHours(-13), ContactChannel.Email, "Feedback");
        Assert.Throws<ArgumentException>(() => c.RecordRecruiterUpdate(c.OwnerId, revision, true, CandidacyStatus.Interview, update, Now));
        update = update with { OccurredAtUtc = Now };
        Assert.Throws<UnauthorizedAccessException>(() => c.RecordRecruiterUpdate(Guid.NewGuid(), revision, true, CandidacyStatus.Interview, update, Now));
        Assert.Throws<InvalidOperationException>(() => c.RecordRecruiterUpdate(c.OwnerId, revision - 1, true, CandidacyStatus.Interview, update, Now));
        Assert.Throws<InvalidOperationException>(() => c.RecordRecruiterUpdate(c.OwnerId, revision, false, CandidacyStatus.Interview, update, Now));
        foreach (var message in new[] { " ", new string('a', 2001), "x\0y" })
            Assert.Throws<ArgumentException>(() => c.RecordRecruiterUpdate(c.OwnerId, revision, true, CandidacyStatus.Interview, update with { Message = message }, Now));
        Assert.Equal(revision, c.Revision);
    }
    [Theory]
    [InlineData(CandidacyStatus.Submitted, true)]
    [InlineData(CandidacyStatus.InReview, true)]
    [InlineData(CandidacyStatus.Interview, true)]
    [InlineData(CandidacyStatus.Offer, false)]
    [InlineData(CandidacyStatus.Rejected, false)]
    public void ActiveFollowUpCanWithdrawButTerminalOutcomesCannot(CandidacyStatus status, bool allowed)
    {
        var c = State(status);
        if (allowed) { c.Withdraw(c.OwnerId, c.Revision, true, "Stopped", Now); Assert.Equal(CandidacyStatus.Withdrawn, c.Status); }
        else Assert.Throws<InvalidOperationException>(() => c.Withdraw(c.OwnerId, c.Revision, true, "Stopped", Now));
        c.AddNote(c.OwnerId, c.Revision, "Historical note", Now);
    }
    [Fact]
    public void OldHistoryWithoutNewOptionalFieldsStillRestores()
    {
        var c = State(CandidacyStatus.Approved);
        var json = JsonNode.Parse(JsonSerializer.Serialize(c.Events))!.AsArray();
        foreach (var item in json) { item!.AsObject().Remove("Submission"); item.AsObject().Remove("RecruiterUpdate"); }
        var history = JsonSerializer.Deserialize<CandidacyEvent[]>(json.ToJsonString())!;
        var restored = Candidacy.Restore(c.Id, c.OwnerId, c.ProfileId, c.JobId, c.JobTitle, c.CompanyName, history);
        Assert.Equal(c.Events, restored.Events); Assert.Null(restored.Submission);
    }
}
