using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Applications.IntegrationTests;

public class FollowUpsApiTests(ApplicationsApiFactory factory) : IClassFixture<ApplicationsApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static async Task<JsonElement> Post(HttpClient client, Guid id, string operation, object body)
    { using var r = await client.PostAsJsonAsync($"/api/v1/applications/{id}/{operation}", body); r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<JsonElement>(); }
    private async Task<Guid> Submitted(HttpClient client, DateTime? at = null)
    {
        var job = await factory.NewJobAsync(); using var created = await client.PostAsJsonAsync("/api/v1/applications", new CreateCandidacyCommand(Guid.NewGuid(), factory.Profile, job)); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var resume = await factory.ExportAsync(job); var letter = await factory.ExportAsync(job, "CoverLetter");
        await Post(client, id, "documents", new { expectedRevision = 1, exportId = resume.Id });
        await Post(client, id, "documents", new { expectedRevision = 2, exportId = letter.Id });
        await Post(client, id, "review", new { expectedRevision = 3 }); await Post(client, id, "approve", new { expectedRevision = 4, confirmed = true });
        await Post(client, id, "manual-submission", new { expectedRevision = 5, confirmed = true, occurredAtUtc = at ?? DateTime.UtcNow, channel = "Email" }); return id;
    }
    private static object Plan(long revision = 6, DateTime? due = null) => new { expectedRevision = revision, dueAtUtc = due ?? DateTime.UtcNow.AddDays(1), channel = "Email", note = "Ask for progress" };
    private static Guid PendingId(JsonElement state) => state.GetProperty("pendingFollowUp").GetProperty("plan").GetProperty("id").GetGuid();
    [Fact]
    public async Task CandidateJourneyCreatesReviewsSubmitsAndTracksOneDossierAcrossServices()
    {
        using var client = factory.Client();
        var jobId = await factory.NewJobAsync();
        using var created = await client.PostAsJsonAsync("/api/v1/applications",
            new CreateCandidacyCommand(Guid.NewGuid(), factory.Profile, jobId));
        created.EnsureSuccessStatusCode();
        var initial = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = initial.GetProperty("id").GetGuid();
        Assert.Equal("Draft", initial.GetProperty("status").GetString());
        Assert.Equal("Engineer", initial.GetProperty("jobTitle").GetString());

        var resume = await factory.ExportAsync(jobId);
        var coverLetter = await factory.ExportAsync(jobId, "CoverLetter");
        var withResume = await Post(client, id, "documents", new { expectedRevision = 1, exportId = resume.Id });
        var withDocuments = await Post(client, id, "documents", new { expectedRevision = 2, exportId = coverLetter.Id });
        Assert.Equal("Resume", withResume.GetProperty("resume").GetProperty("kind").GetString());
        Assert.Equal("CoverLetter", withDocuments.GetProperty("coverLetter").GetProperty("kind").GetString());

        var review = await Post(client, id, "review", new { expectedRevision = 3 });
        Assert.Equal("ReadyForReview", review.GetProperty("status").GetString());
        var approved = await Post(client, id, "approve", new { expectedRevision = 4, confirmed = true });
        Assert.Equal("Approved", approved.GetProperty("status").GetString());
        var submitted = await Post(client, id, "manual-submission", new
        {
            expectedRevision = 5,
            confirmed = true,
            occurredAtUtc = DateTime.UtcNow,
            channel = "CompanyWebsite",
            reference = "CANDIDATE-E2E-1"
        });
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString());
        Assert.Equal("CANDIDATE-E2E-1", submitted.GetProperty("submission").GetProperty("reference").GetString());

        var recruiterUpdate = await Post(client, id, "recruiter-updates", new
        {
            expectedRevision = 6,
            confirmed = true,
            status = "InReview",
            occurredAtUtc = DateTime.UtcNow,
            channel = "Email",
            message = "The recruiter confirmed receipt."
        });
        Assert.Equal("InReview", recruiterUpdate.GetProperty("status").GetString());

        var planned = await Post(client, id, "follow-ups", new
        {
            expectedRevision = 7,
            dueAtUtc = DateTime.UtcNow.AddDays(1),
            channel = "Email",
            note = "Ask for an update if no reply arrives."
        });
        var followUpId = PendingId(planned);
        var completed = await Post(client, id, $"follow-ups/{followUpId}/complete", new
        {
            expectedRevision = 8,
            confirmed = true,
            occurredAtUtc = DateTime.UtcNow,
            channel = "Phone",
            outcome = "The recruiter shared the next steps."
        });
        Assert.Equal("InReview", completed.GetProperty("status").GetString());
        Assert.Equal("Completed", completed.GetProperty("followUps")[0].GetProperty("status").GetString());
        Assert.Equal("Phone", completed.GetProperty("followUps")[0].GetProperty("actualChannel").GetString());

        using var read = await client.GetAsync($"/api/v1/applications/{id}");
        read.EnsureSuccessStatusCode();
        var dossier = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(9, dossier.GetProperty("revision").GetInt64());
        Assert.Equal(9, dossier.GetProperty("events").GetArrayLength());
        Assert.Equal("ManualSubmissionDeclared", dossier.GetProperty("events")[5].GetProperty("kind").GetString());
        Assert.Equal("RecruiterUpdateRecorded", dossier.GetProperty("events")[6].GetProperty("kind").GetString());
        Assert.Equal("The recruiter shared the next steps.",
            dossier.GetProperty("events")[8].GetProperty("followUp").GetProperty("outcome").GetString());

        using var list = await client.GetAsync("/api/v1/applications?status=InReview");
        list.EnsureSuccessStatusCode();
        Assert.Contains((await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task PlanRescheduleAndCompletePersist_WithActualManualChannel_WithoutRemoteCalls()
    {
        using var client = factory.Client(); var id = await Submitted(client); factory.SourcesUnavailable = true; factory.DocumentsUnavailable = true;
        var planned = await Post(client, id, "follow-ups", Plan()); var reminder = PendingId(planned);
        await Post(client, id, $"follow-ups/{reminder}/reschedule", Plan(7, DateTime.UtcNow.AddDays(2)));
        var result = await Post(client, id, $"follow-ups/{reminder}/complete", new { expectedRevision = 8, confirmed = true, occurredAtUtc = DateTime.UtcNow, channel = "Phone", outcome = "Called recruiter manually" });
        Assert.Equal("Submitted", result.GetProperty("status").GetString()); Assert.Equal(JsonValueKind.Null, result.GetProperty("pendingFollowUp").ValueKind);
        Assert.Equal("Completed", result.GetProperty("followUps")[0].GetProperty("status").GetString());
        Assert.Equal("Phone", result.GetProperty("followUps")[0].GetProperty("actualChannel").GetString());
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); read.EnsureSuccessStatusCode();
        Assert.Equal(result.ToString(), (await read.Content.ReadFromJsonAsync<JsonElement>()).ToString());
        await Post(client, id, "follow-ups", Plan(9));
    }
    [Fact]
    public async Task DueQueryUsesServerClockOwnerFilterAndSqlPagination()
    {
        var clock = new Clock(DateTimeOffset.UtcNow); using var client = factory.Client(clock: clock);
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++) { var id = await Submitted(client, clock.GetUtcNow().UtcDateTime); ids.Add(id); await Post(client, id, "follow-ups", Plan(due: clock.GetUtcNow().UtcDateTime.AddHours(i + 1))); }
        using var early = await client.GetAsync("/api/v1/applications/follow-ups/due"); Assert.Empty((await early.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
        clock.Now = clock.Now.AddHours(4);
        using var first = await client.GetAsync("/api/v1/applications/follow-ups/due?pageSize=2"); first.EnsureSuccessStatusCode();
        using var second = await client.GetAsync("/api/v1/applications/follow-ups/due?pageSize=2&page=2"); second.EnsureSuccessStatusCode();
        var page1 = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("candidacyId").GetGuid()).ToArray();
        var page2 = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().Select(x => x.GetProperty("candidacyId").GetGuid()).ToArray();
        Assert.Equal(ids, page1.Concat(page2));
        using var other = factory.Client(Guid.NewGuid(), clock: clock); using var others = await other.GetAsync("/api/v1/applications/follow-ups/due");
        Assert.Empty((await others.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
    }
    [Theory]
    [InlineData("Offer")]
    [InlineData("Rejected")]
    [InlineData("Withdrawn")]
    public async Task TerminalEventCancelsPendingPlanAndClearsSqlProjection(string status)
    {
        using var client = factory.Client(); var id = await Submitted(client); await Post(client, id, "follow-ups", Plan());
        var result = status == "Withdrawn" ? await Post(client, id, "withdraw", new { expectedRevision = 7, confirmed = true, reason = "Stopped" })
            : await Post(client, id, "recruiter-updates", new { expectedRevision = 7, confirmed = true, status, occurredAtUtc = DateTime.UtcNow, channel = "Email", message = "Outcome" });
        Assert.Equal(8, result.GetProperty("revision").GetInt64()); Assert.Equal("Cancelled", result.GetProperty("events")[7].GetProperty("followUp").GetProperty("status").GetString());
        using var scope = factory.Services.CreateScope(); var row = await scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>().Candidacies.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null(row.NextFollowUpId); Assert.Null(row.NextFollowUpDueAtUtc); Assert.Null(row.NextFollowUpChannel); Assert.Null(row.NextFollowUpNote);
    }
    [Fact]
    public async Task ConcurrentPlansOnlyCreateOnePendingFollowUp()
    {
        using var client = factory.Client(); var id = await Submitted(client);
        var replies = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups", Plan())));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Equal(5, replies.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var reply in replies) reply.Dispose();
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Single((await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("followUps").EnumerateArray());
    }
    [Fact]
    public async Task CompleteAndCancelRaceHasOneWinnerAndNoDuplicateOutcome()
    {
        using var client = factory.Client(); var id = await Submitted(client); var plan = await Post(client, id, "follow-ups", Plan()); var reminder = PendingId(plan);
        var replies = await Task.WhenAll(client.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups/{reminder}/complete", new { expectedRevision = 7, confirmed = true, occurredAtUtc = DateTime.UtcNow, channel = "Email", outcome = "Done" }),
            client.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups/{reminder}/cancel", new { expectedRevision = 7, confirmed = true, reason = "Not needed" }));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Single(replies, r => r.StatusCode == HttpStatusCode.Conflict);
        foreach (var reply in replies) reply.Dispose();
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(8, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("events").GetArrayLength());
    }
    [Fact]
    public async Task CancellationRequiresConfirmation_AndOtherOwnersCannotMutate()
    {
        using var client = factory.Client(); var id = await Submitted(client); var plan = await Post(client, id, "follow-ups", Plan()); var reminder = PendingId(plan);
        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups/{reminder}/cancel", new { expectedRevision = 7, reason = "Stop" }); Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
        using var other = factory.Client(Guid.NewGuid());
        using var forbidden = await other.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups/{reminder}/cancel", new { expectedRevision = 7, confirmed = true, reason = "Stop" }); Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
        await Post(client, id, $"follow-ups/{reminder}/cancel", new { expectedRevision = 7, confirmed = true, reason = "Not needed" });
        using var reuse = await client.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups/{reminder}/reschedule", Plan(8)); Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
    }
    [Theory]
    [InlineData("past")]
    [InlineData("too-far")]
    [InlineData("note")]
    public async Task InvalidPlansLeaveRevisionUnchanged(string error)
    {
        using var client = factory.Client(); var id = await Submitted(client);
        using var response = await client.PostAsJsonAsync($"/api/v1/applications/{id}/follow-ups", new { expectedRevision = 6,
            dueAtUtc = error == "past" ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow.AddDays(error == "too-far" ? 366 : 1), channel = "Email", note = error == "note" ? new string('a', 1001) : "Follow up" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(6, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());
    }
    [Fact]
    public async Task ProjectionConstraintsRejectPartialData_AndDowngradeProtectsCancelledHistory()
    {
        using var client = factory.Client(); var id = await Submitted(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
            var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Candidacies.Where(x => x.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextFollowUpId, Guid.NewGuid())));
            Assert.Equal("23514", error.SqlState);
        }
        var plan = await Post(client, id, "follow-ups", Plan()); var reminder = PendingId(plan);
        await Post(client, id, $"follow-ups/{reminder}/cancel", new { expectedRevision = 7, confirmed = true, reason = "Not needed" });
        using var migrationScope = factory.Services.CreateScope(); var context = migrationScope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        var downgrade = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => context.GetService<IMigrator>().MigrateAsync("20261016022042_AddManualApplicationFollowUp"));
        Assert.Equal("P0001", downgrade.SqlState);
    }
    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=101")]
    public async Task DueQueryRejectsInvalidPagination(string query)
    { using var client = factory.Client(); using var response = await client.GetAsync("/api/v1/applications/follow-ups/due" + query); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); }
    [Fact]
    public async Task DueQueryRequiresAuthentication()
    { using var client = factory.Client(authorized: false); using var response = await client.GetAsync("/api/v1/applications/follow-ups/due"); Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode); }
    private sealed class Clock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
}
