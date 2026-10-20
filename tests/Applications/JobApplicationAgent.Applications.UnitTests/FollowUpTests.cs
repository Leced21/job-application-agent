using System.Text.Json;
using System.Text.Json.Nodes;
using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public class FollowUpTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
    private static Candidacy State(CandidacyStatus status = CandidacyStatus.Submitted)
    {
        var c = Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", Now.AddDays(-2));
        if (status == CandidacyStatus.Draft) return c;
        foreach (var kind in new[] { ApplicationDocumentKind.Resume, ApplicationDocumentKind.CoverLetter })
            c.AttachVerifiedDocument(new(Guid.NewGuid(), c.OwnerId, c.ProfileId, c.JobId, kind, new string('a', 64)), c.OwnerId, c.Revision, Now.AddDays(-2));
        c.RequestReview(c.OwnerId, c.Revision, Now.AddDays(-2));
        if (status == CandidacyStatus.ReadyForReview) return c;
        c.Approve(c.OwnerId, c.Revision, true, Now.AddDays(-2));
        if (status == CandidacyStatus.Approved) return c;
        if (status == CandidacyStatus.Withdrawn) { c.Withdraw(c.OwnerId, c.Revision, true, "Stopped", Now); return c; }
        c.DeclareManualSubmission(c.OwnerId, c.Revision, true, new(Now.AddDays(-1), ContactChannel.Email, null), Now.AddDays(-1));
        if (status != CandidacyStatus.Submitted) c.RecordRecruiterUpdate(c.OwnerId, c.Revision, true, status, new(Now.AddHours(-1), ContactChannel.Email, "Response"), Now.AddHours(-1));
        return c;
    }
    private static FollowUpPlan Plan() => new(Guid.NewGuid(), Now.AddDays(1), ContactChannel.Email, " Ask for progress ");
    private static Candidacy Restore(Candidacy c) => Candidacy.Restore(c.Id, c.OwnerId, c.ProfileId, c.JobId, c.JobTitle, c.CompanyName,
        JsonSerializer.Deserialize<CandidacyEvent[]>(JsonSerializer.Serialize(c.Events))!);
    [Theory]
    [InlineData(CandidacyStatus.Draft, false)]
    [InlineData(CandidacyStatus.ReadyForReview, false)]
    [InlineData(CandidacyStatus.Approved, false)]
    [InlineData(CandidacyStatus.Withdrawn, false)]
    [InlineData(CandidacyStatus.Submitted, true)]
    [InlineData(CandidacyStatus.InReview, true)]
    [InlineData(CandidacyStatus.Interview, true)]
    [InlineData(CandidacyStatus.Offer, false)]
    [InlineData(CandidacyStatus.Rejected, false)]
    public void OnlyActiveSubmittedDossiersAcceptPlans(CandidacyStatus status, bool allowed)
    {
        var c = State(status);
        if (!allowed) { Assert.Throws<InvalidOperationException>(() => c.PlanFollowUp(c.OwnerId, c.Revision, Plan(), Now)); Assert.Empty(c.FollowUps); return; }
        c.PlanFollowUp(c.OwnerId, c.Revision, Plan(), Now);
        Assert.Equal(status, c.Status); Assert.Equal("Ask for progress", c.PendingFollowUp!.Plan.Note);
        Assert.Equal(c.FollowUps, Restore(c).FollowUps);
    }
    [Theory]
    [InlineData("past")]
    [InlineData("too-far")]
    [InlineData("utc")]
    [InlineData("channel")]
    [InlineData("note")]
    [InlineData("control")]
    public void InvalidPlanDoesNotChangeHistory(string invalid)
    {
        var c = State(); var plan = Plan(); var revision = c.Revision;
        plan = invalid switch {
            "past" => plan with { DueAtUtc = Now.AddSeconds(-1) }, "too-far" => plan with { DueAtUtc = Now.AddDays(366) },
            "utc" => plan with { DueAtUtc = DateTime.SpecifyKind(Now, DateTimeKind.Unspecified) },
            "channel" => plan with { Channel = (ContactChannel)99 }, "note" => plan with { Note = new string('a', 1001) }, _ => plan with { Note = "x\0y" } };
        Assert.Throws<ArgumentException>(() => c.PlanFollowUp(c.OwnerId, revision, plan, Now));
        Assert.Equal(revision, c.Revision); Assert.Empty(c.FollowUps);
    }
    [Fact]
    public void ASecondPendingPlanAndStaleOrForeignWritesAreRejected()
    {
        var c = State(); var plan = Plan(); c.PlanFollowUp(c.OwnerId, c.Revision, plan, Now); var revision = c.Revision;
        Assert.Throws<InvalidOperationException>(() => c.PlanFollowUp(c.OwnerId, revision, Plan(), Now));
        Assert.Throws<InvalidOperationException>(() => c.CancelFollowUp(c.OwnerId, revision - 1, plan.Id, true, "Stop", Now));
        Assert.Throws<UnauthorizedAccessException>(() => c.CancelFollowUp(Guid.NewGuid(), revision, plan.Id, true, "Stop", Now));
        Assert.Throws<InvalidOperationException>(() => c.CancelFollowUp(c.OwnerId, revision, Guid.NewGuid(), true, "Stop", Now));
    }
    [Fact]
    public void ReschedulePreservesTheOriginalPlanAndRequiresCompletionAfterTheNewPlan()
    {
        var c = State(); var plan = Plan(); c.PlanFollowUp(c.OwnerId, c.Revision, plan, Now);
        c.RescheduleFollowUp(c.OwnerId, c.Revision, plan with { DueAtUtc = Now.AddDays(2), Note = "Updated plan" }, Now.AddHours(1));
        Assert.Equal(plan.DueAtUtc, c.Events[^2].FollowUp!.Plan.DueAtUtc);
        Assert.Throws<ArgumentException>(() => c.CompleteFollowUp(c.OwnerId, c.Revision, plan.Id, true, Now, ContactChannel.Phone, "Done", Now.AddHours(2)));
        c.CompleteFollowUp(c.OwnerId, c.Revision, plan.Id, true, Now.AddHours(2), ContactChannel.Phone, " Called recruiter ", Now.AddHours(3));
        Assert.Null(c.PendingFollowUp); Assert.Equal(ContactChannel.Phone, c.FollowUps[0].ActualChannel);
        Assert.Equal("Called recruiter", c.FollowUps[0].Outcome); Assert.Equal(Now.AddHours(2), c.FollowUps[0].OccurredAtUtc);
        Assert.Equal(c.Events, Restore(c).Events);
    }
    [Theory]
    [InlineData("unconfirmed")]
    [InlineData("future")]
    [InlineData("before-plan")]
    [InlineData("outcome")]
    [InlineData("channel")]
    public void InvalidCompletionKeepsPlanPending(string invalid)
    {
        var c = State(); var plan = Plan(); c.PlanFollowUp(c.OwnerId, c.Revision, plan, Now);
        var occurred = invalid == "future" ? Now.AddDays(1) : invalid == "before-plan" ? Now.AddHours(-1) : Now;
        var action = () => c.CompleteFollowUp(c.OwnerId, c.Revision, plan.Id, invalid != "unconfirmed", occurred,
            invalid == "channel" ? (ContactChannel)99 : ContactChannel.Email, invalid == "outcome" ? new string('a', 1001) : "Done", Now);
        if (invalid == "unconfirmed") Assert.Throws<InvalidOperationException>(action); else Assert.Throws<ArgumentException>(action);
        Assert.Equal(FollowUpStatus.Planned, c.PendingFollowUp!.Status);
    }
    [Fact]
    public void CancellationIsExplicitFinalAndAllowsANewPlan()
    {
        var c = State(); var plan = Plan(); c.PlanFollowUp(c.OwnerId, c.Revision, plan, Now);
        Assert.Throws<InvalidOperationException>(() => c.CancelFollowUp(c.OwnerId, c.Revision, plan.Id, false, "Stop", Now));
        Assert.Throws<ArgumentException>(() => c.CancelFollowUp(c.OwnerId, c.Revision, plan.Id, true, " ", Now));
        c.CancelFollowUp(c.OwnerId, c.Revision, plan.Id, true, "Not needed", Now);
        Assert.Throws<InvalidOperationException>(() => c.RescheduleFollowUp(c.OwnerId, c.Revision, plan, Now));
        Assert.Throws<InvalidOperationException>(() => c.CompleteFollowUp(c.OwnerId, c.Revision, plan.Id, true, Now, ContactChannel.Email, "Done", Now));
        c.PlanFollowUp(c.OwnerId, c.Revision, Plan(), Now); Assert.Equal(2, c.FollowUps.Count);
        Assert.Equal(c.FollowUps, Restore(c).FollowUps);
    }
    [Theory]
    [InlineData(CandidacyStatus.Withdrawn)]
    [InlineData(CandidacyStatus.Offer)]
    [InlineData(CandidacyStatus.Rejected)]
    public void ClosingDossierCancelsPlanInTheSameEvent(CandidacyStatus target)
    {
        var c = State(); c.PlanFollowUp(c.OwnerId, c.Revision, Plan(), Now); var revision = c.Revision;
        if (target == CandidacyStatus.Withdrawn) c.Withdraw(c.OwnerId, revision, true, "Stopped", Now);
        else c.RecordRecruiterUpdate(c.OwnerId, revision, true, target, new(Now, ContactChannel.Email, "Outcome"), Now);
        Assert.Equal(revision + 1, c.Revision); Assert.Null(c.PendingFollowUp);
        Assert.Equal(FollowUpStatus.Cancelled, c.Events[^1].FollowUp!.Status); Assert.Equal(c.Events, Restore(c).Events);
    }
    [Fact]
    public void PrecisionAtImmediateDueBoundarySurvivesReplay_AndOldHistoriesStillLoad()
    {
        var c = State(); var at = Now.AddTicks(7); c.PlanFollowUp(c.OwnerId, c.Revision, Plan() with { DueAtUtc = at }, at);
        Assert.Equal(0, c.PendingFollowUp!.Plan.DueAtUtc.Ticks % 10); Assert.Equal(c.Events, Restore(c).Events);
        var old = State(); var json = JsonNode.Parse(JsonSerializer.Serialize(old.Events))!.AsArray();
        foreach (var entry in json) entry!.AsObject().Remove("FollowUp");
        Assert.Empty(Candidacy.Restore(old.Id, old.OwnerId, old.ProfileId, old.JobId, old.JobTitle, old.CompanyName,
            JsonSerializer.Deserialize<CandidacyEvent[]>(json.ToJsonString())!).FollowUps);
    }
}
