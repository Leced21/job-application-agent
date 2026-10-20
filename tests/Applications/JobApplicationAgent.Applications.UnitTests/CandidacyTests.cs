using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public class CandidacyTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static Candidacy Draft() => Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", Now);
    private static ApplicationDocument Document(Candidacy c, ApplicationDocumentKind kind) => new(Guid.NewGuid(), c.OwnerId, c.ProfileId, c.JobId, kind, new string('a', 64));
    private static void AttachBoth(Candidacy c)
    {
        c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.Resume), c.OwnerId, c.Revision, Now);
        c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.CoverLetter), c.OwnerId, c.Revision, Now);
    }

    [Fact]
    public void ReviewRequiresBothDocuments_AndExplicitCurrentRevisionConfirmation()
    {
        var c = Draft();
        Assert.Throws<InvalidOperationException>(() => c.RequestReview(c.OwnerId, c.Revision, Now));
        c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.Resume), c.OwnerId, c.Revision, Now);
        Assert.Throws<InvalidOperationException>(() => c.RequestReview(c.OwnerId, c.Revision, Now));
        c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.CoverLetter), c.OwnerId, c.Revision, Now);
        Assert.Throws<InvalidOperationException>(() => c.Approve(c.OwnerId, c.Revision, true, Now));
        c.RequestReview(c.OwnerId, c.Revision, Now);
        var revision = c.Revision;
        Assert.Throws<InvalidOperationException>(() => c.Approve(c.OwnerId, revision, false, Now));
        Assert.Throws<InvalidOperationException>(() => c.Approve(c.OwnerId, revision - 1, true, Now));
        c.Approve(c.OwnerId, revision, true, Now);
        Assert.Equal(CandidacyStatus.Approved, c.Status);
        Assert.Equal(["Created", "DocumentAttached", "DocumentAttached", "ReviewRequested", "Approved"], c.Events.Select(e => e.Kind));
        Assert.Equal(Enumerable.Range(1, 5), c.Events.Select(e => e.Sequence));
        Assert.All(c.Events, e => Assert.Equal(c.OwnerId, e.ActorId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReviewedDocumentsCannotChange_UntilReturnToDraft(bool approved)
    {
        var c = Draft(); AttachBoth(c); c.RequestReview(c.OwnerId, c.Revision, Now);
        if (approved) c.Approve(c.OwnerId, c.Revision, true, Now);
        Assert.Throws<InvalidOperationException>(() => c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.Resume), c.OwnerId, c.Revision, Now));
        c.ReturnToDraft(c.OwnerId, c.Revision, Now);
        c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.Resume), c.OwnerId, c.Revision, Now);
        Assert.Throws<InvalidOperationException>(() => c.Approve(c.OwnerId, c.Revision, true, Now));
        Assert.Equal(CandidacyStatus.Draft, c.Status);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("profile")]
    [InlineData("job")]
    [InlineData("id")]
    [InlineData("hash")]
    [InlineData("kind")]
    public void InvalidDocumentBindingLeavesDossierUnchanged(string field)
    {
        var c = Draft(); var d = Document(c, ApplicationDocumentKind.Resume);
        d = field switch {
            "owner" => d with { OwnerId = Guid.NewGuid() }, "profile" => d with { ProfileId = Guid.NewGuid() },
            "job" => d with { JobId = Guid.NewGuid() }, "id" => d with { ExportId = Guid.Empty },
            "hash" => d with { Sha256 = new string('z', 64) }, _ => d with { Kind = (ApplicationDocumentKind)99 }
        };
        Assert.Throws<ArgumentException>(() => c.AttachVerifiedDocument(d, c.OwnerId, c.Revision, Now));
        Assert.Null(c.Resume); Assert.Single(c.Events); Assert.Equal(1, c.Revision);
    }

    [Fact]
    public void HistoryLimitAlwaysReservesOneEventForWithdrawal()
    {
        var c = Draft();
        for (var i = 0; i < 998; i++) c.AddNote(c.OwnerId, c.Revision, "Note", Now);
        Assert.Throws<InvalidOperationException>(() => c.AddNote(c.OwnerId, c.Revision, "Excess note", Now));
        c.Withdraw(c.OwnerId, c.Revision, true, "Withdraw at limit", Now);
        Assert.Equal(1000, c.Events.Count);
        var restored = Candidacy.Restore(c.Id, c.OwnerId, c.ProfileId, c.JobId, c.JobTitle, c.CompanyName, c.Events);
        Assert.Equal(CandidacyStatus.Withdrawn, restored.Status);
    }

    [Fact]
    public void ReplacingAnExportPreservesThePreviousReferenceInHistory()
    {
        var c = Draft(); var first = Document(c, ApplicationDocumentKind.Resume);
        c.AttachVerifiedDocument(first, c.OwnerId, c.Revision, Now);
        var second = Document(c, ApplicationDocumentKind.Resume) with { Sha256 = new string('b', 64) };
        c.AttachVerifiedDocument(second, c.OwnerId, c.Revision, Now.AddMinutes(1));
        Assert.Equal(second, c.Resume);
        Assert.Equal(first, c.Events[1].Document);
        Assert.Equal(second, c.Events[2].Document);
        Assert.Equal(Now, c.Events[1].AtUtc);
        Assert.Equal(Now.AddMinutes(1), c.Events[2].AtUtc);
    }

    [Fact]
    public void SameExportCannotBeBothResumeAndLetter()
    {
        var c = Draft(); var d = Document(c, ApplicationDocumentKind.Resume);
        c.AttachVerifiedDocument(d, c.OwnerId, c.Revision, Now);
        Assert.Throws<ArgumentException>(() => c.AttachVerifiedDocument(d with { Kind = ApplicationDocumentKind.CoverLetter }, c.OwnerId, c.Revision, Now));
        Assert.Null(c.CoverLetter);
        c.AttachVerifiedDocument(d with { Sha256 = d.Sha256.ToUpperInvariant() }, c.OwnerId, c.Revision, Now);
        Assert.Equal(2, c.Revision); // Identical attachment is a no-op.
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void WithdrawalIsExplicitAndFinal_InEveryPreparationState(int state)
    {
        var c = Draft(); AttachBoth(c);
        if (state > 0) c.RequestReview(c.OwnerId, c.Revision, Now);
        if (state > 1) c.Approve(c.OwnerId, c.Revision, true, Now);
        Assert.Throws<InvalidOperationException>(() => c.Withdraw(c.OwnerId, c.Revision, false, "Reason", Now));
        Assert.Throws<ArgumentException>(() => c.Withdraw(c.OwnerId, c.Revision, true, " ", Now));
        c.Withdraw(c.OwnerId, c.Revision, true, " Chosen withdrawal ", Now);
        Assert.Throws<InvalidOperationException>(() => c.ReturnToDraft(c.OwnerId, c.Revision, Now));
        Assert.Throws<InvalidOperationException>(() => c.RequestReview(c.OwnerId, c.Revision, Now));
        Assert.Throws<InvalidOperationException>(() => c.Approve(c.OwnerId, c.Revision, true, Now));
        Assert.Throws<InvalidOperationException>(() => c.Withdraw(c.OwnerId, c.Revision, true, "Again", Now));
        Assert.Throws<InvalidOperationException>(() => c.AttachVerifiedDocument(Document(c, ApplicationDocumentKind.Resume), c.OwnerId, c.Revision, Now));
        c.AddNote(c.OwnerId, c.Revision, "Follow-up note", Now);
        Assert.Equal(CandidacyStatus.Withdrawn, c.Status);
        Assert.Equal("Chosen withdrawal", c.Events[^2].Note);
    }

    [Fact]
    public void NotesRejectWrongOwnerStaleRevisionInvalidTextAndBackwardsTime()
    {
        var c = Draft();
        Assert.Throws<UnauthorizedAccessException>(() => c.AddNote(Guid.NewGuid(), c.Revision, "Note", Now));
        Assert.Throws<InvalidOperationException>(() => c.AddNote(c.OwnerId, 0, "Note", Now));
        Assert.Throws<ArgumentException>(() => c.AddNote(c.OwnerId, c.Revision, "Note", Now.AddTicks(-1)));
        Assert.Throws<ArgumentException>(() => c.AddNote(c.OwnerId, c.Revision, "Note", DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
        foreach (var invalid in new[] { "", " ", new string('a', 2001), "x\0y" })
            Assert.Throws<ArgumentException>(() => c.AddNote(c.OwnerId, c.Revision, invalid, Now));
        Assert.Single(c.Events);
        c.AddNote(c.OwnerId, c.Revision, "  Valid note\nsecond line  ", Now.AddSeconds(1));
        Assert.Equal("Valid note\nsecond line", c.Events[^1].Note);
        Assert.Equal(CandidacyStatus.Draft, c.Status);
        Assert.Throws<NotSupportedException>(() => ((IList<CandidacyEvent>)c.Events).Clear());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EmptyIdentityIsRejected(int empty)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }; ids[empty] = Guid.Empty;
        Assert.Throws<ArgumentException>(() => Candidacy.Create(ids[0], ids[1], ids[2], "Engineer", "Company", Now));
    }

    [Fact]
    public void CreationRejectsInvalidSnapshotsAndLocalTime()
    {
        Assert.Throws<ArgumentException>(() => Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "", "Company", Now));
        Assert.Throws<ArgumentException>(() => Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", new string('a', 201), Now));
        Assert.Throws<ArgumentException>(() => Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", Now.ToLocalTime()));
    }
}
