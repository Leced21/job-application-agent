using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;
using JobApplicationAgent.Applications.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Applications.IntegrationTests;

public class ManualFollowUpApiTests(ApplicationsApiFactory factory) : IClassFixture<ApplicationsApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private static async Task<JsonElement> Post(HttpClient client, Guid id, string operation, object command)
    { using var response = await client.PostAsJsonAsync($"/api/v1/applications/{id}/{operation}", command); response.EnsureSuccessStatusCode(); return await response.Content.ReadFromJsonAsync<JsonElement>(); }
    private async Task<Guid> Approved(HttpClient client)
    {
        var job = await factory.NewJobAsync();
        using var created = await client.PostAsJsonAsync("/api/v1/applications", new CreateCandidacyCommand(Guid.NewGuid(), factory.Profile, job)); created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var resume = await factory.ExportAsync(job); var letter = await factory.ExportAsync(job, "CoverLetter");
        await Post(client, id, "documents", new { expectedRevision = 1, exportId = resume.Id });
        await Post(client, id, "documents", new { expectedRevision = 2, exportId = letter.Id });
        await Post(client, id, "review", new { expectedRevision = 3 });
        await Post(client, id, "approve", new { expectedRevision = 4, confirmed = true }); return id;
    }
    private static object Submission(long revision = 5, bool confirmed = true) => new { expectedRevision = revision, confirmed,
        occurredAtUtc = DateTime.UtcNow, channel = "CompanyWebsite", reference = "CASE-123" };
    private static object Attempt(long revision, Guid attemptId, string platformId, string reviewSha256, bool confirmed = true) =>
        new { expectedRevision = revision, confirmed, attemptId, platformId, reviewSha256 };
    [Fact]
    public async Task SubmissionPlatformsExposeOnlyConfiguredSafeIdentifiers()
    {
        using var client = factory.Client();

        using var response = await client.GetAsync("/api/v1/applications/submission-platforms");

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.GetProperty("externalSubmissionEnabled").GetBoolean());
        Assert.Equal("synthetic-official-api", Assert.Single(result.GetProperty("platformIds").EnumerateArray()).GetString());
    }
    [Fact]
    public async Task SubmissionAndRecruiterJourneyPersist_WithBusinessDatesAndNoRemoteCalls()
    {
        using var client = factory.Client(); var id = await Approved(client);
        factory.DocumentsUnavailable = true; factory.SourcesUnavailable = true;
        var submitted = await Post(client, id, "manual-submission", Submission());
        Assert.Equal("Submitted", submitted.GetProperty("status").GetString()); Assert.Equal("CASE-123", submitted.GetProperty("submission").GetProperty("reference").GetString());
        long revision = 6;
        foreach (var status in new[] { "InReview", "Interview", "Interview", "Offer" })
        {
            var result = await Post(client, id, "recruiter-updates", new { expectedRevision = revision++, confirmed = true, status,
                occurredAtUtc = DateTime.UtcNow, channel = "Email", message = $"Response: {status}" });
            Assert.Equal(status, result.GetProperty("status").GetString());
        }
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); read.EnsureSuccessStatusCode();
        var state = await read.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(10, state.GetProperty("events").GetArrayLength());
        Assert.Equal("ManualSubmissionDeclared", state.GetProperty("events")[5].GetProperty("kind").GetString());
        Assert.Equal("Response: Offer", state.GetProperty("events")[9].GetProperty("recruiterUpdate").GetProperty("message").GetString());
        using var list = await client.GetAsync("/api/v1/applications?status=Offer");
        Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task SubmissionPreviewAndExplicitApprovalPersistWithoutSending()
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var previewResponse = await client.GetAsync($"/api/v1/applications/{id}/submission-preview");
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(preview.GetProperty("externalSubmissionEnabled").GetBoolean());
        Assert.Equal(5, preview.GetProperty("revision").GetInt64());
        Assert.Equal("Engineer", preview.GetProperty("jobTitle").GetString());
        var reviewSha256 = preview.GetProperty("reviewSha256").GetString()!;

        using var rejected = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = false, reviewSha256 });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        using var approved = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = true, reviewSha256 });
        approved.EnsureSuccessStatusCode();
        var result = await approved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Approved", result.GetProperty("status").GetString());
        Assert.Equal(6, result.GetProperty("revision").GetInt64());
        Assert.Equal(reviewSha256, result.GetProperty("submissionApproval").GetProperty("reviewSha256").GetString());
        Assert.Equal("ControlledSubmissionApproved", result.GetProperty("events")[5].GetProperty("kind").GetString());

        using var reread = await client.GetAsync($"/api/v1/applications/{id}");
        reread.EnsureSuccessStatusCode();
        var persisted = await reread.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(reviewSha256, persisted.GetProperty("submissionApproval").GetProperty("reviewSha256").GetString());

        using var replay = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = true, reviewSha256 });
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task ConcurrentAttemptPreparationWithSameKeyPersistsOneReservation()
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var previewResponse = await client.GetAsync($"/api/v1/applications/{id}/submission-preview");
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var reviewSha256 = preview.GetProperty("reviewSha256").GetString()!;
        using var approvalResponse = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = true, reviewSha256 });
        approvalResponse.EnsureSuccessStatusCode();

        var attemptId = Guid.NewGuid();
        var platform = new SubmissionPlatformCapability("synthetic-official-api", true, true, false, true);
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<CandidacyRepository>();
            return await repository.MutateAsync(factory.Owner, id,
                candidacy => candidacy.PrepareControlledSubmissionAttempt(factory.Owner, 6, true, true, attemptId, platform, reviewSha256, DateTime.UtcNow),
                CancellationToken.None);
        }));

        Assert.All(results, result => Assert.Equal(attemptId, result!.SubmissionAttempt!.AttemptId));
        Assert.All(results, result => Assert.Equal(7, result!.Revision));
        Assert.Equal(7, results[0]!.Events.Count);
    }

    [Fact]
    public async Task AttemptReservationUsesServerRegistryAndIsIdempotent()
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var previewResponse = await client.GetAsync($"/api/v1/applications/{id}/submission-preview");
        previewResponse.EnsureSuccessStatusCode();
        var hash = (await previewResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reviewSha256").GetString()!;
        using var unapproved = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
            Attempt(5, Guid.NewGuid(), "synthetic-official-api", hash));
        Assert.Equal(HttpStatusCode.Conflict, unapproved.StatusCode);
        using var approval = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = true, reviewSha256 = hash });
        approval.EnsureSuccessStatusCode();

        var attemptId = Guid.NewGuid();
        using var unknownPlatform = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
            Attempt(6, attemptId, "client-invented-platform", hash));
        Assert.Equal(HttpStatusCode.Conflict, unknownPlatform.StatusCode);
        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
            Attempt(6, attemptId, "synthetic-official-api", hash, confirmed: false));
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);

        var command = Attempt(6, attemptId, "synthetic-official-api", hash);
        using var preparedResponse = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts", command);
        preparedResponse.EnsureSuccessStatusCode();
        var prepared = await preparedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(7, prepared.GetProperty("revision").GetInt64());
        Assert.Equal(attemptId, prepared.GetProperty("submissionAttempt").GetProperty("attemptId").GetGuid());

        using var replay = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts", command);
        replay.EnsureSuccessStatusCode();
        Assert.Equal(7, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());
        using var differentKey = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
            Attempt(6, Guid.NewGuid(), "synthetic-official-api", hash));
        Assert.Equal(HttpStatusCode.Conflict, differentKey.StatusCode);
    }

    [Theory]
    [InlineData("Submitted", "Submitted")]
    [InlineData("DefinitelyNotSubmitted", "Approved")]
    [InlineData("Unknown", "Approved")]
    public async Task AuthenticatedAttemptResultIsPersistedAndIdempotent(string outcome, string expectedStatus)
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var previewResponse = await client.GetAsync($"/api/v1/applications/{id}/submission-preview");
        previewResponse.EnsureSuccessStatusCode();
        var preview = await previewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var hash = preview.GetProperty("reviewSha256").GetString()!;
        using var approvalResponse = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = true, reviewSha256 = hash }); approvalResponse.EnsureSuccessStatusCode();
        var attemptId = Guid.NewGuid();
        using var preparation = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
            Attempt(6, attemptId, "synthetic-official-api", hash)); preparation.EnsureSuccessStatusCode();

        var occurredAt = DateTime.UtcNow;
        var command = new { expectedRevision = 7, attemptId, outcome, occurredAtUtc = occurredAt,
            confirmed = true, reference = outcome == "Submitted" ? "receipt-123" : null };
        using var resultResponse = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempt-result", command);
        resultResponse.EnsureSuccessStatusCode();
        var result = await resultResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expectedStatus, result.GetProperty("status").GetString());
        Assert.Equal(outcome, result.GetProperty("submissionAttemptResult").GetProperty("outcome").GetString());

        using var replay = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempt-result", command);
        replay.EnsureSuccessStatusCode();
        Assert.Equal(8, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revision").GetInt64());
        if (outcome == "Unknown")
        {
            using var previewAfterUnknown = await client.GetAsync($"/api/v1/applications/{id}/submission-preview");
            Assert.Equal(HttpStatusCode.Conflict, previewAfterUnknown.StatusCode);
            using var retryAfterUnknown = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
                Attempt(8, Guid.NewGuid(), "synthetic-official-api", hash));
            Assert.Equal(HttpStatusCode.Conflict, retryAfterUnknown.StatusCode);
        }
        using var reread = await client.GetAsync($"/api/v1/applications/{id}");
        var persisted = await reread.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(outcome, persisted.GetProperty("submissionAttemptResult").GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task AnotherOwnerCannotRecordAttemptResult()
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var previewResponse = await client.GetAsync($"/api/v1/applications/{id}/submission-preview");
        var hash = (await previewResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reviewSha256").GetString()!;
        using var approvalResponse = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-approval", new
        { expectedRevision = 5, confirmed = true, reviewSha256 = hash }); approvalResponse.EnsureSuccessStatusCode();
        var attemptId = Guid.NewGuid();
        using var preparation = await client.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempts",
            Attempt(6, attemptId, "synthetic-official-api", hash)); preparation.EnsureSuccessStatusCode();
        using var other = factory.Client(Guid.NewGuid());
        using var response = await other.PostAsJsonAsync($"/api/v1/applications/{id}/submission-attempt-result", new
        { expectedRevision = 7, attemptId, outcome = "Unknown", occurredAtUtc = DateTime.UtcNow, confirmed = true, reference = (string?)null });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task ConfirmationAndExactRevisionAreRequired_ForSubmissionAndFeedback()
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var absent = await client.PostAsJsonAsync($"/api/v1/applications/{id}/manual-submission", Submission(confirmed: false)); Assert.Equal(HttpStatusCode.Conflict, absent.StatusCode);
        using var stale = await client.PostAsJsonAsync($"/api/v1/applications/{id}/manual-submission", Submission(4)); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        await Post(client, id, "manual-submission", Submission());
        using var replay = await client.PostAsJsonAsync($"/api/v1/applications/{id}/manual-submission", Submission()); Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
        using var unconfirmed = await client.PostAsJsonAsync($"/api/v1/applications/{id}/recruiter-updates", new { expectedRevision = 6, status = "Interview", occurredAtUtc = DateTime.UtcNow, channel = "Phone", message = "Interview" });
        Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);
    }
    [Theory]
    [InlineData("future")]
    [InlineData("before-approval")]
    [InlineData("channel")]
    public async Task InvalidDeclarationDoesNotChangeState(string error)
    {
        using var client = factory.Client(); var id = await Approved(client);
        using var response = await client.PostAsJsonAsync($"/api/v1/applications/{id}/manual-submission", new {
            expectedRevision = 5, confirmed = true, occurredAtUtc = error == "future" ? DateTime.UtcNow.AddDays(1) : error == "before-approval" ? DateTime.UtcNow.AddDays(-1) : DateTime.UtcNow,
            channel = error == "channel" ? "Invalid" : "Email" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal("Approved", (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
    [Fact]
    public async Task ConcurrentDeclarationsRecordOneSubmission()
    {
        using var client = factory.Client(); var id = await Approved(client);
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync($"/api/v1/applications/{id}/manual-submission", Submission())));
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK); Assert.Equal(5, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses) response.Dispose();
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(6, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("events").GetArrayLength());
    }
    [Fact]
    public async Task OtherOwnerCannotDeclareOrRecordFeedback()
    {
        using var client = factory.Client(); var id = await Approved(client); using var other = factory.Client(Guid.NewGuid());
        using var submission = await other.PostAsJsonAsync($"/api/v1/applications/{id}/manual-submission", Submission()); Assert.Equal(HttpStatusCode.NotFound, submission.StatusCode);
        using var feedback = await other.PostAsJsonAsync($"/api/v1/applications/{id}/recruiter-updates", new { expectedRevision = 5, confirmed = true, status = "Rejected", occurredAtUtc = DateTime.UtcNow, channel = "Email", message = "Feedback" });
        Assert.Equal(HttpStatusCode.NotFound, feedback.StatusCode);
    }
    [Fact]
    public async Task SubmittedDossierCannotReturnToDraft_AndTerminalOutcomeCannotBeReopened()
    {
        using var client = factory.Client(); var id = await Approved(client); await Post(client, id, "manual-submission", Submission());
        using var draft = await client.PostAsJsonAsync($"/api/v1/applications/{id}/return-to-draft", new { expectedRevision = 6 }); Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        await Post(client, id, "recruiter-updates", new { expectedRevision = 6, confirmed = true, status = "Rejected", occurredAtUtc = DateTime.UtcNow, channel = "Email", message = "Rejected by recruiter" });
        using var reopen = await client.PostAsJsonAsync($"/api/v1/applications/{id}/recruiter-updates", new { expectedRevision = 7, confirmed = true, status = "Interview", occurredAtUtc = DateTime.UtcNow, channel = "Email", message = "Reopen" }); Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);
        var noted = await Post(client, id, "notes", new { expectedRevision = 7, note = "Historical clarification" }); Assert.Equal("Rejected", noted.GetProperty("status").GetString());
    }
    [Fact]
    public async Task DowngradeRejectsHistoricalSubmission_EvenAfterWithdrawal()
    {
        using var client = factory.Client(); var id = await Approved(client); await Post(client, id, "manual-submission", Submission());
        await Post(client, id, "withdraw", new { expectedRevision = 6, confirmed = true, reason = "Withdrawn manually" });
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationsDbContext>();
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261016013140_InitialApplicationsSchema"));
        Assert.Equal("P0001", error.SqlState);
        Assert.Contains("20261016022042_AddManualApplicationFollowUp", await db.Database.GetAppliedMigrationsAsync());
        // Later migrations may already have been reverted before an older downgrade guard fires.
        // Restore the current schema before querying through the current API binary.
        await db.Database.MigrateAsync();
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal("Withdrawn", (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
}
