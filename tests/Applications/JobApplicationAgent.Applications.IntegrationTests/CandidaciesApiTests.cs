using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Applications.IntegrationTests;

public class CandidaciesApiTests(ApplicationsApiFactory factory) : IClassFixture<ApplicationsApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private CreateCandidacyCommand Command(Guid job) => new(Guid.NewGuid(), factory.Profile, job);
    private static async Task<JsonElement> Success(HttpResponseMessage response)
    { response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }

    [Fact]
    public async Task CreatesFromRealJobAndProfile_AndPersistsFrozenSnapshot()
    {
        using var client = factory.Client(); var job = await factory.NewJobAsync(); var command = Command(job);
        using var response = await client.PostAsJsonAsync("/api/v1/applications", command); var first = await Success(response);
        Assert.Equal("Draft", first.GetProperty("status").GetString());
        Assert.Equal("Engineer", first.GetProperty("jobTitle").GetString()); Assert.Equal("Acme", first.GetProperty("companyName").GetString());
        Assert.Equal(factory.Owner, first.GetProperty("ownerId").GetGuid());
        Assert.NotNull(response.Headers.Location); Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.True(factory.SawForwardedAuthorization);
        Assert.False(factory.SawForwardedCookie);
        using var read = await client.GetAsync(response.Headers.Location);
        Assert.Equal(first.ToString(), (await Success(read)).ToString());
        factory.SourcesUnavailable = true;
        using var replay = await client.PostAsJsonAsync("/api/v1/applications", command);
        Assert.Equal(first.GetProperty("id").GetGuid(), (await Success(replay)).GetProperty("id").GetGuid());
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCreatesProduceOneCandidature_WithSameOrDistinctRequestKeys(bool sameKey)
    {
        using var client = factory.Client(); var command = Command(await factory.NewJobAsync());
        var responses = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => client.PostAsJsonAsync("/api/v1/applications",
            sameKey ? command : command with { RequestId = Guid.NewGuid() })));
        var ids = new List<Guid>();
        foreach (var response in responses) { using (response) ids.Add((await Success(response)).GetProperty("id").GetGuid()); }
        Assert.Single(ids.Distinct());
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        Assert.Equal(1, await db.Candidacies.CountAsync()); Assert.Equal(sameKey ? 1 : 12, await db.Requests.CountAsync());
    }
    [Fact]
    public async Task ConcurrentSameRequestForDifferentJobs_HasOneWinnerAndOneConflict()
    {
        using var client = factory.Client(); var command = Command(await factory.NewJobAsync()); var other = await factory.NewJobAsync();
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/v1/applications", command),
            client.PostAsJsonAsync("/api/v1/applications", command with { JobId = other }));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        using var scope = factory.Services.CreateScope(); Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Candidacies.CountAsync());
    }
    [Fact]
    public async Task NotesAndWithdrawalArePersisted_WithRevisionAndExplicitConfirmation()
    {
        using var client = factory.Client(); var command = Command(await factory.NewJobAsync());
        using var created = await client.PostAsJsonAsync("/api/v1/applications", command); var id = (await Success(created)).GetProperty("id").GetGuid();
        using var note = await client.PostAsJsonAsync($"/api/v1/applications/{id}/notes", new { expectedRevision = 1, note = "  Contact pending  " });
        Assert.Equal(2, (await Success(note)).GetProperty("revision").GetInt64());
        using var stale = await client.PostAsJsonAsync($"/api/v1/applications/{id}/notes", new { expectedRevision = 1, note = "Lost update" });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/applications/{id}/withdraw", new { expectedRevision = 2, reason = "Changed plans" });
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        using var withdraw = await client.PostAsJsonAsync($"/api/v1/applications/{id}/withdraw", new { expectedRevision = 2, confirmed = true, reason = "Changed plans" });
        Assert.Equal("Withdrawn", (await Success(withdraw)).GetProperty("status").GetString());
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); var state = await Success(read);
        Assert.Equal(3, state.GetProperty("events").GetArrayLength());
        Assert.Equal("Contact pending", state.GetProperty("events")[1].GetProperty("note").GetString());
        Assert.Equal("Changed plans", state.GetProperty("events")[2].GetProperty("note").GetString());
        factory.SourcesUnavailable = true;
        using var duplicate = await client.PostAsJsonAsync("/api/v1/applications", command with { RequestId = Guid.NewGuid() });
        Assert.Equal("Withdrawn", (await Success(duplicate)).GetProperty("status").GetString());
    }
    [Fact]
    public async Task ConcurrentNotesWithSameRevision_DoNotLoseUpdates()
    {
        using var client = factory.Client(); using var created = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync()));
        var id = (await Success(created)).GetProperty("id").GetGuid();
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => client.PostAsJsonAsync($"/api/v1/applications/{id}/notes", new { expectedRevision = 1, note = $"Note {i}" })));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Equal(7, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(2, (await Success(read)).GetProperty("events").GetArrayLength());
    }
    [Fact]
    public async Task OwnerIsolationAppliesToReadListAndMutation()
    {
        using var client = factory.Client(); using var created = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync()));
        var id = (await Success(created)).GetProperty("id").GetGuid(); using var other = factory.Client(Guid.NewGuid());
        using var read = await other.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        using var note = await other.PostAsJsonAsync($"/api/v1/applications/{id}/notes", new { expectedRevision = 1, note = "Forbidden" }); Assert.Equal(HttpStatusCode.NotFound, note.StatusCode);
        using var withdraw = await other.PostAsJsonAsync($"/api/v1/applications/{id}/withdraw", new { expectedRevision = 1, confirmed = true, reason = "Forbidden" }); Assert.Equal(HttpStatusCode.NotFound, withdraw.StatusCode);
        using var list = await other.GetAsync("/api/v1/applications"); Assert.Empty((await Success(list)).GetProperty("items").EnumerateArray());
    }
    [Theory]
    [InlineData("anonymous")]
    [InlineData("bad-token")]
    [InlineData("duplicate-header")]
    public async Task AuthenticationProtectsAllRoutes(string mode)
    {
        using var client = factory.Client(authorized: false);
        if (mode == "bad-token") client.DefaultRequestHeaders.Authorization = new("Bearer", new string('x', 64));
        if (mode == "duplicate-header") client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", new[] { "Bearer " + factory.Token, "Bearer " + factory.Token });
        using var response = await client.GetAsync("/api/v1/applications"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, h => h.Scheme == "Bearer");
        using var post = await client.PostAsJsonAsync("/api/v1/applications", Command(Guid.NewGuid())); Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
    }
    [Theory]
    [InlineData("/api/v1/applications?page=0")]
    [InlineData("/api/v1/applications?pageSize=101")]
    [InlineData("/api/v1/applications?status=99")]
    [InlineData("/api/v1/applications?status=UnknownStatus")]
    public async Task InvalidPagingOrUnavailableStatesAreRejected(string uri)
    { using var client = factory.Client(); using var response = await client.GetAsync(uri); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
    [Fact]
    public async Task PaginationAndStatusFiltersRunInPostgres()
    {
        using var client = factory.Client();
        for (var i = 0; i < 3; i++) { using var r = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync())); r.EnsureSuccessStatusCode(); }
        using var page1 = await client.GetAsync("/api/v1/applications?pageSize=2"); using var page2 = await client.GetAsync("/api/v1/applications?pageSize=2&page=2");
        var first = (await Success(page1)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        var second = (await Success(page2)).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal(2, first.Length); Assert.Single(second); Assert.Equal(3, first.Concat(second).Distinct().Count());
        using var empty = await client.GetAsync("/api/v1/applications?status=Withdrawn"); Assert.Empty((await Success(empty)).GetProperty("items").EnumerateArray());
    }
    [Fact]
    public async Task MissingJobUnboundProfileAndUnavailableSourceCannotCreate()
    {
        using var client = factory.Client(); var command = Command(Guid.NewGuid());
        using var missing = await client.PostAsJsonAsync("/api/v1/applications", command); Assert.Equal(HttpStatusCode.UnprocessableEntity, missing.StatusCode);
        using var unbound = await client.PostAsJsonAsync("/api/v1/applications", command with { ProfileId = Guid.NewGuid() }); Assert.Equal(HttpStatusCode.UnprocessableEntity, unbound.StatusCode);
        factory.SourcesUnavailable = true;
        using var unavailable = await client.PostAsJsonAsync("/api/v1/applications", command); Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        using var scope = factory.Services.CreateScope(); Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Candidacies.CountAsync());
    }
    [Fact]
    public async Task EmptyAndOversizedNotesAreRejectedWithoutHistoryChanges()
    {
        using var client = factory.Client(); using var created = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync()));
        var id = (await Success(created)).GetProperty("id").GetGuid();
        foreach (var note in new[] { " ", new string('a', 2001), "x\0y" })
        { using var r = await client.PostAsJsonAsync($"/api/v1/applications/{id}/notes", new { expectedRevision = 1, note }); Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode); }
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(1, (await Success(read)).GetProperty("revision").GetInt64());
    }
    [Fact]
    public async Task SourceOutageDoesNotConsumeRequestKey_AndSameRequestCanRecover()
    {
        using var client = factory.Client(); var command = Command(await factory.NewJobAsync());
        factory.SourcesUnavailable = true;
        using var unavailable = await client.PostAsJsonAsync("/api/v1/applications", command);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Requests.CountAsync());
        factory.SourcesUnavailable = false;
        using var recovered = await client.PostAsJsonAsync("/api/v1/applications", command);
        Assert.Equal("Draft", (await Success(recovered)).GetProperty("status").GetString());
    }
    [Fact]
    public async Task MaximumUnicodeNotesFitStorage_AndWithdrawalRemainsAvailable()
    {
        using var client = factory.Client(); using var created = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync()));
        var id = (await Success(created)).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<CandidacyRepository>();
        var note = new string('\u4e2d', 2000);
        await repository.MutateAsync(factory.Owner, id, c => {
            for (var i = 0; i < 998; i++) c.AddNote(factory.Owner, c.Revision, note, c.Events[^1].AtUtc);
            c.Withdraw(factory.Owner, c.Revision, true, "Withdraw", c.Events[^1].AtUtc);
        }, default);
        var restored = await repository.GetAsync(factory.Owner, id, default);
        Assert.Equal(1000, restored!.Events.Count); Assert.Equal(note, restored.Events[998].Note);
        Assert.Equal(Domain.CandidacyStatus.Withdrawn, restored.Status);
    }
    [Fact]
    public void MigrationAndModelAreAligned()
    {
        using var scope = factory.Services.CreateScope();
        Assert.False(scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task DatabaseRejectsDuplicateBusinessIdentityWithoutRepositoryLocks()
    {
        using var client = factory.Client(); using var created = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync()));
        created.EnsureSuccessStatusCode();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        var row = await db.Candidacies.AsNoTracking().SingleAsync(); row.Id = Guid.NewGuid(); db.Candidacies.Add(row);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }
    [Fact]
    public async Task DatabaseRejectsDuplicateRequestKeyWithoutRepositoryLocks()
    {
        using var client = factory.Client(); var command = Command(await factory.NewJobAsync());
        using var created = await client.PostAsJsonAsync("/api/v1/applications", command); var id = (await Success(created)).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        db.Requests.Add(new() { OwnerId = factory.Owner, RequestId = command.RequestId, CandidacyId = id });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23505", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }
    [Fact]
    public async Task DatabaseRejectsRequestLinkToAnotherOwnersCandidature()
    {
        using var client = factory.Client(); using var created = await client.PostAsJsonAsync("/api/v1/applications", Command(await factory.NewJobAsync()));
        var id = (await Success(created)).GetProperty("id").GetGuid();
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        db.Requests.Add(new() { OwnerId = Guid.NewGuid(), RequestId = Guid.NewGuid(), CandidacyId = id });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23503", Assert.IsType<Npgsql.PostgresException>(error.InnerException).SqlState);
    }
    [Theory]
    [InlineData("Ignored", false)]
    [InlineData("Expired", false)]
    [InlineData("Saved", true)]
    public async Task CurrentJobStatusControlsNewCreation(string status, bool allowed)
    {
        using var client = factory.Client(); var job = await factory.NewJobAsync();
        using var scope = factory.Services.CreateScope();
        // Exercise the real Job API contract after changing its persisted status.
        var connection = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Database.GetDbConnection().ConnectionString;
        using var db = new JobApplicationAgent.Job.Infrastructure.Persistence.JobDbContext(new DbContextOptionsBuilder<JobApplicationAgent.Job.Infrastructure.Persistence.JobDbContext>().UseNpgsql(connection).Options);
        var jobStatus = Enum.Parse<JobApplicationAgent.Job.Domain.Enums.JobStatus>(status);
        await db.Jobs.Where(x => x.Id == job).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, jobStatus));
        using var response = await client.PostAsJsonAsync("/api/v1/applications", Command(job));
        Assert.Equal(allowed ? HttpStatusCode.OK : HttpStatusCode.Conflict, response.StatusCode);
    }
    [Fact]
    public async Task RateLimiterReturns429ForExcessiveWrites()
    {
        using var client = factory.Client();
        for (var i = 0; i < 60; i++)
        { using var response = await client.PostAsJsonAsync("/api/v1/applications", Command(Guid.Empty)); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
        using var limited = await client.PostAsJsonAsync("/api/v1/applications", Command(Guid.Empty));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
    [Fact]
    public async Task NoSubmissionRouteExists()
    {
        using var client = factory.Client(); var id = Guid.NewGuid();
        foreach (var operation in new[] { "submit", "send" })
        { using var response = await client.PostAsJsonAsync($"/api/v1/applications/{id}/{operation}", new { confirmed = true }); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); }
    }
}
