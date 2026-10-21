using JobApplicationAgent.Collection.Application.Abstractions;
using JobApplicationAgent.Collection.Application.Imports;
using JobApplicationAgent.Collection.Domain.Entities;
using FluentValidation;
using NSubstitute;

namespace JobApplicationAgent.Collection.UnitTests;

public sealed class OfferRevisionTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Reviewer = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static OfferRevision Create(DateTime? expiry = null) => OfferRevision.From(new OfferNormalizer().Normalize(
        new ImportOfferCommand("Engineer", "Company", Skills: ["SQL"], ExpiresAtUtc: expiry), Now), Guid.NewGuid());

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public void Review_ShouldBeFinalAndIdempotent(string decision)
    {
        var revision = Create();
        revision.Review(decision, Now, Reviewer);
        revision.Review(decision, Now.AddHours(1), Guid.NewGuid());
        Assert.Equal(Now, revision.ReviewedAtUtc);
        Assert.Equal(Reviewer, revision.ReviewedByOwnerId);
        Assert.Throws<RevisionConflictException>(() => revision.Review(decision == "Approved" ? "Rejected" : "Approved", Now, Reviewer));
        Assert.Equal("Engineer", revision.ReadContent().Title);
        Assert.Equal(new[] { "SQL" }, revision.ReadContent().Skills);
    }

    [Fact]
    public void Review_ShouldRejectExpiredApproval_ButAllowRejection()
    {
        var revision = Create(Now);
        Assert.Throws<RevisionConflictException>(() => revision.Review("Approved", Now, Reviewer));
        Assert.Equal("Pending", revision.Decision);
        Assert.Null(revision.ReviewedAtUtc);
        revision.Review("Rejected", Now, Reviewer);
        Assert.Equal(Reviewer, revision.ReviewedByOwnerId);
    }

    [Fact]
    public async Task Handler_ShouldRejectInvalidInputs_BeforeRepositoryAccess()
    {
        var repository = Substitute.For<IOfferRevisionRepository>();
        var handler = new ReviewOfferHandler(repository, TimeProvider.System);
        await Assert.ThrowsAsync<ValidationException>(() => handler.ReviewAsync(Guid.NewGuid(), Guid.NewGuid(), new("approve"), Guid.NewGuid(), default));
        await Assert.ThrowsAsync<ValidationException>(() => handler.ReviewAsync(Guid.NewGuid(), Guid.NewGuid(), new("Approved"), Guid.Empty, default));
        await Assert.ThrowsAsync<ValidationException>(() => handler.GetPageAsync(Guid.Empty, 1, 20, default));
        await Assert.ThrowsAsync<ValidationException>(() => handler.GetPageAsync(Guid.NewGuid(), 1, 101, default));
        Assert.Empty(repository.ReceivedCalls());
    }
}
