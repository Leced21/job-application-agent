using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public sealed class SubmissionPlatformCatalogTests
{
    [Fact]
    public void EmptyCatalogIsDisabledAndContainsNoPlatforms()
    {
        var catalog = new SubmissionPlatformCatalog(false, []);

        Assert.False(catalog.AutomationEnabled);
        Assert.Empty(catalog.EligiblePlatformIds);
        Assert.False(catalog.TryGet("official-api", out _));
    }

    [Fact]
    public void LookupReturnsOnlyServerRegisteredCapabilities()
    {
        var registered = new SubmissionPlatformCapability("official-api", true, true, false, true);
        var catalog = new SubmissionPlatformCatalog(true, [registered]);

        Assert.True(catalog.TryGet("official-api", out var actual));
        Assert.Same(registered, actual);
        Assert.Equal(["official-api"], catalog.EligiblePlatformIds);
        Assert.False(catalog.TryGet("client-claims-official-api", out _));
    }

    [Fact]
    public void EligiblePlatformIdsExcludeCapabilitiesThatFailSafetyChecks()
    {
        var catalog = new SubmissionPlatformCatalog(true,
        [
            new("supported-api", true, true, false, true),
            new("captcha-api", true, true, true, true),
            new("terms-blocked", true, false, false, true),
            new("no-idempotency", true, true, false, false),
            new("unofficial", false, true, false, true)
        ]);

        Assert.Equal(["supported-api"], catalog.EligiblePlatformIds);
    }

    [Theory]
    [InlineData("Official-API")]
    [InlineData("official api")]
    [InlineData("")]
    public void InvalidPlatformIdentifiersAreRejected(string platformId)
    {
        Assert.Throws<ArgumentException>(() => new SubmissionPlatformCatalog(true,
            [new SubmissionPlatformCapability(platformId, true, true, false, true)]));
    }

    [Fact]
    public void DuplicatePlatformIdentifiersAreRejected()
    {
        var capability = new SubmissionPlatformCapability("official-api", true, true, false, true);

        Assert.Throws<ArgumentException>(() => new SubmissionPlatformCatalog(true, [capability, capability]));
    }

    [Fact]
    public async Task TrackingServiceRejectsUnknownPlatformBeforeMutation()
    {
        var repository = new NoMutationRepository();
        var service = new CandidacyTrackingService(repository, TimeProvider.System,
            new SubmissionPlatformCatalog(true, []));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareControlledSubmissionAttemptAsync(
            Guid.NewGuid(), Guid.NewGuid(), 1, true, Guid.NewGuid(), "client-platform", new string('a', 64), CancellationToken.None));
        Assert.False(repository.MutationCalled);
    }

    private sealed class NoMutationRepository : ICandidacyTrackingRepository
    {
        public bool MutationCalled { get; private set; }
        public Task<Candidacy?> GetAsync(Guid ownerId, Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<CandidacySummary>> ListAsync(Guid ownerId, int page, int pageSize, CandidacyStatus? status, CancellationToken ct) => throw new NotImplementedException();
        public Task<Candidacy?> MutateAsync(Guid ownerId, Guid id, Action<Candidacy> mutation, CancellationToken ct)
        {
            MutationCalled = true;
            throw new NotImplementedException();
        }
    }
}
