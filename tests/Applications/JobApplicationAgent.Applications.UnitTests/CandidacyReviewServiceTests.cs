using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public class CandidacyReviewServiceTests
{
    private static Candidacy Draft() => Candidacy.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Engineer", "Company", DateTime.UtcNow);
    private static void Both(Candidacy c)
    {
        foreach (var kind in new[] { ApplicationDocumentKind.Resume, ApplicationDocumentKind.CoverLetter })
            c.AttachVerifiedDocument(new(Guid.NewGuid(), c.OwnerId, c.ProfileId, c.JobId, kind, new string('a', 64)), c.OwnerId, c.Revision, DateTime.UtcNow);
    }
    [Fact]
    public async Task ConcurrentMutationAfterRemoteVerificationCannotApproveOldRevision()
    {
        var c = Draft(); Both(c); c.RequestReview(c.OwnerId, c.Revision, DateTime.UtcNow);
        var repo = new Repository(c); var docs = new Documents(c) { AfterVerify = () => c.AddNote(c.OwnerId, c.Revision, "Concurrent note", DateTime.UtcNow) };
        var service = new CandidacyReviewService(repo, docs, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApproveAsync(c.OwnerId, c.Id, 4, true, default));
        Assert.Equal(CandidacyStatus.ReadyForReview, c.Status); Assert.DoesNotContain(c.Events, e => e.Kind == "Approved");
    }
    [Fact]
    public async Task ChangedReceiptHashBlocksReview()
    {
        var c = Draft(); Both(c); var docs = new Documents(c) { ChangeHash = true };
        var service = new CandidacyReviewService(new Repository(c), docs, TimeProvider.System);
        await Assert.ThrowsAsync<ApplicationSourceRejectedException>(() => service.RequestReviewAsync(c.OwnerId, c.Id, 3, default));
        Assert.Equal(CandidacyStatus.Draft, c.Status);
    }
    [Fact]
    public async Task MissingPiecesAndStaleRevisionsFailBeforeRemoteCalls()
    {
        var c = Draft(); var docs = new Documents(c); var service = new CandidacyReviewService(new Repository(c), docs, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RequestReviewAsync(c.OwnerId, c.Id, 1, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AttachAsync(c.OwnerId, c.Id, 0, Guid.NewGuid(), default));
        Assert.Equal(0, docs.Calls);
    }
    [Fact]
    public async Task ReturnToDraftDoesNotReadRemoteSources()
    {
        var c = Draft(); Both(c); c.RequestReview(c.OwnerId, c.Revision, DateTime.UtcNow);
        var docs = new Documents(c); var service = new CandidacyReviewService(new Repository(c), docs, TimeProvider.System);
        await service.ReturnToDraftAsync(c.OwnerId, c.Id, 4, default);
        Assert.Equal(CandidacyStatus.Draft, c.Status); Assert.Equal(0, docs.Calls);
    }
    private sealed class Repository(Candidacy c) : ICandidacyTrackingRepository
    {
        public Task<Candidacy?> GetAsync(Guid owner, Guid id, CancellationToken ct) => Task.FromResult<Candidacy?>(owner == c.OwnerId && id == c.Id ? c : null);
        public Task<IReadOnlyList<CandidacySummary>> ListAsync(Guid owner, int page, int size, CandidacyStatus? status, CancellationToken ct) => Task.FromResult<IReadOnlyList<CandidacySummary>>([]);
        public Task<Candidacy?> MutateAsync(Guid owner, Guid id, Action<Candidacy> action, CancellationToken ct) { action(c); return Task.FromResult<Candidacy?>(c); }
    }
    private sealed class Documents(Candidacy c) : IApplicationDocumentClient
    {
        public Action? AfterVerify { get; set; }
        public bool ChangeHash { get; set; }
        public int Calls { get; private set; }
        public Task<ApplicationDocument> VerifyAsync(Guid ownerId, Guid profileId, Guid jobId, Guid exportId, CancellationToken ct)
        {
            Calls++; var reference = c.Resume?.ExportId == exportId ? c.Resume : c.CoverLetter;
            AfterVerify?.Invoke(); return Task.FromResult(ChangeHash ? reference! with { Sha256 = new string('b', 64) } : reference!);
        }
    }
}
