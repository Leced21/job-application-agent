using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Documents.Application;

namespace JobApplicationAgent.Applications.IntegrationTests;

public class CandidacyReviewApiTests(ApplicationsApiFactory factory) : IClassFixture<ApplicationsApiFactory>, IAsyncLifetime
{
    public Task InitializeAsync() => factory.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;
    private async Task<Guid> Create(HttpClient client, Guid job)
    { using var r = await client.PostAsJsonAsync("/api/v1/applications", new CreateCandidacyCommand(Guid.NewGuid(), factory.Profile, job)); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); }
    private static async Task<JsonElement> Post(HttpClient client, Guid id, string action, object command)
    { using var r = await client.PostAsJsonAsync($"/api/v1/applications/{id}/{action}", command); r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<JsonElement>(); }
    private async Task<Guid> Ready(HttpClient client, Guid job)
    {
        var id = await Create(client, job);
        var resume = await factory.ExportAsync(job); var letter = await factory.ExportAsync(job, "CoverLetter");
        await Post(client, id, "documents", new { expectedRevision = 1, exportId = resume.Id });
        await Post(client, id, "documents", new { expectedRevision = 2, exportId = letter.Id });
        await Post(client, id, "review", new { expectedRevision = 3 }); return id;
    }
    [Fact]
    public async Task RealDocumentsBindingsAllowReviewAndExplicitApproval_WithPersistedHistory()
    {
        using var client = factory.Client(); var job = await factory.NewJobAsync(); var id = await Ready(client, job);
        using var missingConfirmation = await client.PostAsJsonAsync($"/api/v1/applications/{id}/approve", new { expectedRevision = 4 });
        Assert.Equal(HttpStatusCode.Conflict, missingConfirmation.StatusCode);
        var approved = await Post(client, id, "approve", new { expectedRevision = 4, confirmed = true });
        Assert.Equal("Approved", approved.GetProperty("status").GetString()); Assert.Equal(5, approved.GetProperty("revision").GetInt64());
        Assert.Equal("Resume", approved.GetProperty("resume").GetProperty("kind").GetString());
        Assert.Equal("CoverLetter", approved.GetProperty("coverLetter").GetProperty("kind").GetString());
        Assert.Equal(5, approved.GetProperty("events").GetArrayLength());
        using var get = await client.GetAsync($"/api/v1/applications/{id}"); get.EnsureSuccessStatusCode();
        Assert.Equal(approved.ToString(), (await get.Content.ReadFromJsonAsync<JsonElement>()).ToString());
        var revised = await Post(client, id, "return-to-draft", new { expectedRevision = 5 }); Assert.Equal("Draft", revised.GetProperty("status").GetString());
        using var directApproval = await client.PostAsJsonAsync($"/api/v1/applications/{id}/approve", new { expectedRevision = 6, confirmed = true });
        Assert.Equal(HttpStatusCode.Conflict, directApproval.StatusCode);
    }
    [Fact]
    public async Task MissingPiecesAndUnboundExportCannotEnterReview()
    {
        using var client = factory.Client(); var job = await factory.NewJobAsync(); var id = await Create(client, job);
        using var empty = await client.PostAsJsonAsync($"/api/v1/applications/{id}/review", new { expectedRevision = 1 }); Assert.Equal(HttpStatusCode.Conflict, empty.StatusCode);
        var unbound = await factory.ExportAsync(job, bind: false);
        using var attach = await client.PostAsJsonAsync($"/api/v1/applications/{id}/documents", new { expectedRevision = 1, exportId = unbound.Id });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, attach.StatusCode);
    }
    [Fact]
    public async Task DifferentJobsExportCannotBeAttached()
    {
        using var client = factory.Client(); var id = await Create(client, await factory.NewJobAsync()); var wrong = await factory.ExportAsync(await factory.NewJobAsync());
        using var attach = await client.PostAsJsonAsync($"/api/v1/applications/{id}/documents", new { expectedRevision = 1, exportId = wrong.Id });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, attach.StatusCode);
    }
    [Theory]
    [InlineData("approval", false)]
    [InlineData("bytes", false)]
    [InlineData("approval", true)]
    [InlineData("bytes", true)]
    public async Task ReviewAndApprovalRevalidateDocumentIntegrity(string corruption, bool atApproval)
    {
        using var client = factory.Client(); var job = await factory.NewJobAsync(); var id = await Create(client, job);
        var resume = await factory.ExportAsync(job); var letter = await factory.ExportAsync(job, "CoverLetter");
        await Post(client, id, "documents", new { expectedRevision = 1, exportId = resume.Id });
        await Post(client, id, "documents", new { expectedRevision = 2, exportId = letter.Id });
        if (atApproval) await Post(client, id, "review", new { expectedRevision = 3 });
        await factory.CorruptExportAsync(resume.Id, corruption);
        using var transition = await client.PostAsJsonAsync($"/api/v1/applications/{id}/{(atApproval ? "approve" : "review")}", new { expectedRevision = atApproval ? 4 : 3, confirmed = true });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, transition.StatusCode);
        using var read = await client.GetAsync($"/api/v1/applications/{id}");
        Assert.Equal(atApproval ? "ReadyForReview" : "Draft", (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }
    [Fact]
    public async Task DocumentsOutageBlocksApproval_ButAllowsReturnToDraft()
    {
        using var client = factory.Client(); var id = await Ready(client, await factory.NewJobAsync()); factory.DocumentsUnavailable = true;
        using var approve = await client.PostAsJsonAsync($"/api/v1/applications/{id}/approve", new { expectedRevision = 4, confirmed = true }); Assert.Equal(HttpStatusCode.ServiceUnavailable, approve.StatusCode);
        var returned = await Post(client, id, "return-to-draft", new { expectedRevision = 4 }); Assert.Equal("Draft", returned.GetProperty("status").GetString());
    }
    [Fact]
    public async Task ReviewedDossierRejectsAttachment_AndStaleApprovalCannotCommit()
    {
        using var client = factory.Client(); var job = await factory.NewJobAsync(); var id = await Ready(client, job);
        var another = await factory.ExportAsync(job);
        using var attach = await client.PostAsJsonAsync($"/api/v1/applications/{id}/documents", new { expectedRevision = 4, exportId = another.Id }); Assert.Equal(HttpStatusCode.Conflict, attach.StatusCode);
        await Post(client, id, "notes", new { expectedRevision = 4, note = "Review note" });
        using var stale = await client.PostAsJsonAsync($"/api/v1/applications/{id}/approve", new { expectedRevision = 4, confirmed = true }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }
    [Fact]
    public async Task ConcurrentApprovalsWithSameRevisionProduceOneEvent()
    {
        using var client = factory.Client(); var id = await Ready(client, await factory.NewJobAsync());
        var replies = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PostAsJsonAsync($"/api/v1/applications/{id}/approve", new { expectedRevision = 4, confirmed = true })));
        Assert.Single(replies, r => r.StatusCode == HttpStatusCode.OK); Assert.Equal(5, replies.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var reply in replies) reply.Dispose();
        using var read = await client.GetAsync($"/api/v1/applications/{id}"); Assert.Equal(5, (await read.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("events").GetArrayLength());
    }
    [Fact]
    public async Task OtherOwnerCannotAttachReviewApproveOrReturn()
    {
        using var client = factory.Client(); var id = await Create(client, await factory.NewJobAsync()); using var other = factory.Client(Guid.NewGuid());
        foreach (var action in new[] { "documents", "review", "approve", "return-to-draft" })
        { using var r = await other.PostAsJsonAsync($"/api/v1/applications/{id}/{action}", new { expectedRevision = 1, exportId = Guid.NewGuid(), confirmed = true }); Assert.Equal(HttpStatusCode.NotFound, r.StatusCode); }
    }
    [Fact]
    public async Task BindingRequiresExplicitProfileAndExactHash_AndRetryIsStable()
    {
        var file = await factory.ExportAsync(await factory.NewJobAsync(), bind: false);
        var path = $"/api/v1/documents/exports/{file.Id}/application-binding";
        using var unconfirmed = await factory.DocumentsClient.PostAsJsonAsync(path, new { profileId = factory.Profile, fileSha256 = file.FileSha256 }); Assert.Equal(HttpStatusCode.UnprocessableEntity, unconfirmed.StatusCode);
        using var wrongProfile = await factory.DocumentsClient.PostAsJsonAsync(path, new { profileId = Guid.NewGuid(), fileSha256 = file.FileSha256, confirmed = true }); Assert.Equal(HttpStatusCode.UnprocessableEntity, wrongProfile.StatusCode);
        using var wrongHash = await factory.DocumentsClient.PostAsJsonAsync(path, new { profileId = factory.Profile, fileSha256 = new string('0', 64), confirmed = true }); Assert.Equal(HttpStatusCode.Conflict, wrongHash.StatusCode);
        using var first = await factory.DocumentsClient.PostAsJsonAsync(path, new { profileId = factory.Profile, fileSha256 = file.FileSha256, confirmed = true }); first.EnsureSuccessStatusCode();
        using var replay = await factory.DocumentsClient.PostAsJsonAsync(path, new { profileId = factory.Profile, fileSha256 = file.FileSha256, confirmed = true }); replay.EnsureSuccessStatusCode();
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task ConcurrentBindingConfirmationsReturnTheSameImmutableReceipt()
    {
        var file = await factory.ExportAsync(await factory.NewJobAsync(), bind: false);
        var path = $"/api/v1/documents/exports/{file.Id}/application-binding";
        var replies = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => factory.DocumentsClient.PostAsJsonAsync(path,
            new { profileId = factory.Profile, fileSha256 = file.FileSha256, confirmed = true })));
        var receipts = new List<string>();
        foreach (var reply in replies) { using (reply) { reply.EnsureSuccessStatusCode(); receipts.Add(await reply.Content.ReadAsStringAsync()); } }
        Assert.Single(receipts.Distinct());
    }
    [Theory]
    [InlineData("approval")]
    [InlineData("bytes")]
    public async Task CorruptedExportCannotReceiveABinding(string corruption)
    {
        var file = await factory.ExportAsync(await factory.NewJobAsync(), bind: false);
        await factory.CorruptExportAsync(file.Id, corruption);
        using var reply = await factory.DocumentsClient.PostAsJsonAsync($"/api/v1/documents/exports/{file.Id}/application-binding",
            new { profileId = factory.Profile, fileSha256 = file.FileSha256, confirmed = true });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, reply.StatusCode);
    }
    [Fact]
    public async Task DedicatedChannelIsReadOnly_AndOwnerTokenCannotUseIt()
    {
        var file = await factory.ExportAsync(await factory.NewJobAsync());
        using var ownerOnChannel = await factory.DocumentsClient.GetAsync($"/api/v1/documents/application-bindings/{file.Id}"); Assert.Equal(HttpStatusCode.Unauthorized, ownerOnChannel.StatusCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/documents/application-bindings/{file.Id}"); request.Headers.Authorization = new("Bearer", factory.DocumentsChannelToken);
        using var channel = await factory.DocumentsClient.SendAsync(request); channel.EnsureSuccessStatusCode();
        using var write = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/documents/exports/{file.Id}/application-binding") { Content = JsonContent.Create(new { profileId = factory.Profile, fileSha256 = file.FileSha256, confirmed = true }) };
        write.Headers.Authorization = new("Bearer", factory.DocumentsChannelToken);
        using var rejected = await factory.DocumentsClient.SendAsync(write); Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }
}
