using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;
using NSubstitute;

namespace JobApplicationAgent.Collection.UnitTests;

public sealed class PublishOfferTests
{
    private static OfferRevision Revision(DateTime? expiry = null) => OfferRevision.From(new OfferNormalizer().Normalize(
        new ImportOfferCommand("Engineer", "Company", ExpiresAtUtc: expiry), DateTime.UtcNow), Guid.NewGuid());
    [Theory]
    [InlineData("Pending")]
    [InlineData("Rejected")]
    public async Task Publish_ShouldRequireApprovalBeforeContactingJob(string state)
    {
        var revision = Revision();
        if (state == "Rejected") revision.Review(state, DateTime.UtcNow, Guid.NewGuid());
        var repo = Substitute.For<IOfferRevisionRepository>();
        repo.GetAsync(revision.OfferId, revision.Id, default).Returns(revision);
        var jobs = Substitute.For<IJobPublicationClient>();
        await Assert.ThrowsAsync<RevisionConflictException>(() => new PublishOfferHandler(repo, jobs, TimeProvider.System)
            .HandleAsync(revision.OfferId, revision.Id, default));
        Assert.Empty(jobs.ReceivedCalls());
    }
    [Fact]
    public async Task Publish_ShouldRecoverExistingReceiptAfterExpiry_WithoutPostingAgain()
    {
        var revision = Revision(DateTime.UtcNow.AddMinutes(-1));
        revision.Review("Approved", DateTime.UtcNow.AddHours(-1), Guid.NewGuid());
        var repo = Substitute.For<IOfferRevisionRepository>();
        repo.GetAsync(revision.OfferId, revision.Id, default).Returns(revision);
        var jobs = Substitute.For<IJobPublicationClient>();
        var receipt = new PublicationReceipt(revision.OfferId, revision.Id, Guid.NewGuid(), revision.Fingerprint, DateTime.UtcNow);
        jobs.GetAsync(revision.OfferId, default).Returns(receipt);
        var handler = new PublishOfferHandler(repo, jobs, TimeProvider.System);
        Assert.Equal(receipt, await handler.HandleAsync(revision.OfferId, revision.Id, default));
        await jobs.DidNotReceive().PublishAsync(Arg.Any<OfferRevision>(), Arg.Any<CancellationToken>());
        jobs.GetAsync(revision.OfferId, default).Returns((PublicationReceipt?)null);
        await Assert.ThrowsAsync<RevisionConflictException>(() => handler.HandleAsync(revision.OfferId, revision.Id, default));
    }
    [Fact]
    public async Task Publish_ShouldRejectReceiptForAnotherRevision()
    {
        var revision = Revision();
        revision.Review("Approved", DateTime.UtcNow, Guid.NewGuid());
        var repo = Substitute.For<IOfferRevisionRepository>();
        repo.GetAsync(revision.OfferId, revision.Id, default).Returns(revision);
        var jobs = Substitute.For<IJobPublicationClient>();
        jobs.GetAsync(revision.OfferId, default).Returns(new PublicationReceipt(revision.OfferId, Guid.NewGuid(), Guid.NewGuid(), revision.Fingerprint, DateTime.UtcNow));
        await Assert.ThrowsAsync<RevisionConflictException>(() => new PublishOfferHandler(repo, jobs, TimeProvider.System)
            .HandleAsync(revision.OfferId, revision.Id, default));
    }
}
