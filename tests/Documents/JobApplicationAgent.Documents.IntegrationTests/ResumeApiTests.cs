using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class ResumeApiTests(DocumentsApiFactory factory) : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/documents/resumes";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();
    private static MultipartFormDataContent File(byte[] bytes, string name, string type)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(type);
        form.Add(part, "file", name);
        return form;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UploadAndDownload_ShouldPreserveBytesAndProtectedMetadata(bool docx)
    {
        using var client = factory.AuthorizedClient();
        var bytes = docx ? ResumeSamples.Docx() : ResumeSamples.Pdf();
        using var form = File(bytes, docx ? "cv.docx" : "cv.pdf", docx ? ResumeFileValidator.DocxType : "application/pdf");
        using var response = await client.PostAsync(Route, form);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var metadata = (await response.Content.ReadFromJsonAsync<ResumeMetadata>())!;
        Assert.Equal("UploadedUnanalysed", metadata.Status);
        Assert.Equal(bytes.Length, metadata.Length);
        Assert.Equal(metadata, await client.GetFromJsonAsync<ResumeMetadata>(response.Headers.Location));
        using var download = await client.GetAsync($"{Route}/{metadata.Id}/content");
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition!.DispositionType);
        Assert.True(download.Headers.CacheControl!.NoStore);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        Assert.Equal(factory.OwnerId, (await db.Resumes.SingleAsync()).OwnerId);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task MissingOrInvalidCredentials_ShouldDenyAllDocumentOperations()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"{Route}/{Guid.NewGuid()}/content")).StatusCode);
        using var form = File(ResumeSamples.Pdf(), "cv.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync(Route, form)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", new string('x', 64));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(Route)).StatusCode);
    }
    [Fact]
    public async Task AnotherOwnersDocument_ShouldRemainInvisible_EvenWithSpoofedOwnerHeader()
    {
        var otherOwner = Guid.NewGuid();
        var document = ResumeDocument.Create(otherOwner, "private.pdf", "application/pdf", ResumeSamples.Pdf(), DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
            db.Resumes.Add(document);
            await db.SaveChangesAsync();
        }
        using var client = factory.AuthorizedClient();
        client.DefaultRequestHeaders.Add("X-Owner-Id", otherOwner.ToString());
        Assert.Empty((await client.GetFromJsonAsync<ResumeMetadata[]>(Route))!);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{document.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{Route}/{document.Id}/content")).StatusCode);
    }
    [Theory]
    [InlineData("mismatch")]
    [InlineData("empty")]
    [InlineData("oversize")]
    [InlineData("macro")]
    public async Task InvalidUpload_ShouldNeverPersist(string invalid)
    {
        using var client = factory.AuthorizedClient();
        var bytes = invalid switch { "empty" => [], "oversize" => new byte[ResumeFileValidator.MaxBytes + 1],
            "macro" => ResumeSamples.Docx("word/vbaProject.bin"), _ => ResumeSamples.Pdf() };
        using var form = File(bytes, invalid == "macro" ? "cv.docx" : "cv.pdf",
            invalid == "macro" ? ResumeFileValidator.DocxType : invalid == "mismatch" ? "text/plain" : "application/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(Route, form)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Resumes.CountAsync());
    }
    [Fact]
    public async Task SeparateUploads_ShouldRetainBothDocuments_AndPaginationShouldBeBounded()
    {
        using var client = factory.AuthorizedClient();
        for (var i = 0; i < 2; i++)
        {
            using var form = File(ResumeSamples.Pdf(), $"cv-{i}.pdf", "application/pdf");
            (await client.PostAsync(Route, form)).EnsureSuccessStatusCode();
        }
        Assert.Equal(2, (await client.GetFromJsonAsync<ResumeMetadata[]>(Route))!.Length);
        Assert.Single((await client.GetFromJsonAsync<ResumeMetadata[]>(Route + "?pageSize=1&page=2"))!);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Route + "?pageSize=101")).StatusCode);
    }
}
