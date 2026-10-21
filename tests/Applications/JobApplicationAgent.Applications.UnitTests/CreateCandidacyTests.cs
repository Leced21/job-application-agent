using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;

namespace JobApplicationAgent.Applications.UnitTests;

public class CreateCandidacyTests
{
    private readonly Guid owner = Guid.NewGuid();
    private readonly CreateCandidacyCommand command = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    private readonly MemoryRepository repository = new();
    private readonly Sources sources = new();
    private CreateCandidacyService Service => new(repository, sources, TimeProvider.System);

    [Fact]
    public async Task RetryAndAlternateRequestReturnOriginalSnapshot_WhenSourcesBecomeUnavailable()
    {
        var first = await Service.CreateAsync(owner, command);
        sources.Unavailable = true;
        Assert.Same(first, await Service.CreateAsync(owner, command));
        var alternate = command with { RequestId = Guid.NewGuid() };
        Assert.Same(first, await Service.CreateAsync(owner, alternate));
        Assert.Same(first, await Service.CreateAsync(owner, alternate));
        Assert.Equal(1, sources.Calls);
        Assert.Equal("Engineer", first.JobTitle);
        Assert.Equal(CandidacyStatus.Draft, first.Status);
        Assert.Single(first.Events);
    }

    [Fact]
    public async Task WithdrawnIdentityStillDeduplicates()
    {
        var c = await Service.CreateAsync(owner, command);
        c.Withdraw(owner, c.Revision, true, "No longer interested", DateTime.UtcNow);
        sources.Unavailable = true;
        Assert.Same(c, await Service.CreateAsync(owner, command with { RequestId = Guid.NewGuid() }));
        Assert.Equal(CandidacyStatus.Withdrawn, c.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SameRequestCannotChangeProfileOrJob(bool profile)
    {
        await Service.CreateAsync(owner, command);
        var conflict = profile ? command with { ProfileId = Guid.NewGuid() } : command with { JobId = Guid.NewGuid() };
        await Assert.ThrowsAsync<CandidacyRequestConflictException>(() => Service.CreateAsync(owner, conflict));
        Assert.Equal(1, sources.Calls);
    }

    [Fact]
    public async Task RequestAndBusinessIdentityAreScopedToOwner()
    {
        var first = await Service.CreateAsync(owner, command);
        var second = await Service.CreateAsync(Guid.NewGuid(), command);
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.OwnerId, second.OwnerId);
        Assert.Equal(2, sources.Calls);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("profile")]
    [InlineData("job")]
    public async Task SourceBindingMismatchIsRejectedBeforeStorage(string mismatch)
    {
        sources.Mismatch = mismatch;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Service.CreateAsync(owner, command));
        Assert.Empty(repository.Requests);
    }

    [Fact]
    public async Task InactiveJobAndUnavailableSourceDoNotCreateDraft()
    {
        sources.Active = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service.CreateAsync(owner, command));
        sources.Unavailable = true;
        await Assert.ThrowsAsync<HttpRequestException>(() => Service.CreateAsync(owner, command));
        Assert.Empty(repository.Requests);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("request")]
    [InlineData("profile")]
    [InlineData("job")]
    public async Task EmptyIdentitiesFailBeforeCallingSources(string empty)
    {
        var input = empty switch {
            "request" => command with { RequestId = Guid.Empty },
            "profile" => command with { ProfileId = Guid.Empty },
            "job" => command with { JobId = Guid.Empty }, _ => command
        };
        await Assert.ThrowsAsync<ArgumentException>(() => Service.CreateAsync(empty == "owner" ? Guid.Empty : owner, input));
        Assert.Equal(0, sources.Calls);
    }

    [Fact]
    public async Task CancellationFlowsToSourceAndRepository()
    {
        using var cancellation = new CancellationTokenSource();
        await Service.CreateAsync(owner, command, cancellation.Token);
        Assert.Equal(cancellation.Token, sources.LastToken);
        Assert.Equal(cancellation.Token, repository.LastToken);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.CreateAsync(owner, command, cancellation.Token));
    }

    [Fact]
    public async Task ConcurrentWinnerIsReturnedRatherThanLosingDraft()
    {
        var winner = Candidacy.Create(owner, command.ProfileId, command.JobId, "Original", "Company", DateTime.UtcNow);
        repository.InsertWinner = winner;
        var actual = await Service.CreateAsync(owner, command);
        Assert.Same(winner, actual);
        Assert.Equal("Original", actual.JobTitle);
    }

    [Fact]
    public async Task RequestConflictDetectedAtAtomicInsertPropagates()
    {
        repository.ConflictAtInsert = true;
        await Assert.ThrowsAsync<CandidacyRequestConflictException>(() => Service.CreateAsync(owner, command));
        Assert.Empty(repository.Requests);
    }

    // Test double only: PostgreSQL concurrency and unique constraints belong to phase 87.2.
    private sealed class MemoryRepository : ICandidacyCreationRepository
    {
        public Dictionary<(Guid, Guid), Candidacy> Requests { get; } = [];
        public CancellationToken LastToken { get; private set; }
        public Candidacy? InsertWinner { get; set; }
        public bool ConflictAtInsert { get; set; }
        private void Check(CancellationToken token) { LastToken = token; token.ThrowIfCancellationRequested(); }
        public Task<Candidacy?> FindByRequestAsync(Guid ownerId, Guid requestId, CancellationToken token)
        {
            Check(token); Requests.TryGetValue((ownerId, requestId), out var c); return Task.FromResult(c);
        }
        public Task<Candidacy?> FindByIdentityAsync(Guid ownerId, Guid profileId, Guid jobId, CancellationToken token)
        {
            Check(token);
            return Task.FromResult(Requests.Values.FirstOrDefault(c => c.OwnerId == ownerId && c.ProfileId == profileId && c.JobId == jobId));
        }
        public Task<Candidacy> InsertOrGetAsync(Guid requestId, Candidacy c, CancellationToken token)
        {
            Check(token);
            if (ConflictAtInsert) throw new CandidacyRequestConflictException();
            if (InsertWinner is not null) c = InsertWinner;
            Requests[(c.OwnerId, requestId)] = c;
            return Task.FromResult(c);
        }
    }

    private sealed class Sources : IApplicationSources
    {
        public bool Unavailable { get; set; }
        public bool Active { get; set; } = true;
        public string? Mismatch { get; set; }
        public int Calls { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Task<VerifiedApplicationSources> VerifyAsync(Guid ownerId, Guid profileId, Guid jobId, CancellationToken token)
        {
            Calls++; LastToken = token; token.ThrowIfCancellationRequested();
            if (Unavailable) throw new HttpRequestException("Unavailable");
            return Task.FromResult(new VerifiedApplicationSources(Mismatch == "owner" ? Guid.NewGuid() : ownerId,
                Mismatch == "profile" ? Guid.NewGuid() : profileId, Mismatch == "job" ? Guid.NewGuid() : jobId,
                "Engineer", "Company", Active));
        }
    }
}
