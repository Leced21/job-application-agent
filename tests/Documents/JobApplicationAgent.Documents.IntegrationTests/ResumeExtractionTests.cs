using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using UglyToad.PdfPig.Writer;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class ResumeExtractionTests(DocumentsApiFactory factory) : IClassFixture<DocumentsApiFactory>, IAsyncLifetime
{
    private const string Route = "/api/v1/documents/resumes";
    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => factory.ResetAsync();
    private static byte[] Pdf(string? text, int pages = 1)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        for (var index = 0; index < pages; index++)
        {
            var page = builder.AddPage(PageSize.A4);
            if (text is not null) page.AddText(text, 12, new PdfPoint(20, 700), font);
        }
        return builder.Build();
    }
    private static async Task<ResumeMetadata> Upload(HttpClient client, byte[] bytes, bool pdf)
    {
        using var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(pdf ? "application/pdf" : ResumeFileValidator.DocxType);
        form.Add(part, "file", pdf ? "cv.pdf" : "cv.docx");
        using var response = await client.PostAsync(Route, form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResumeMetadata>())!;
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Extraction_ShouldPersistSourceEvidence_AndRemainAwaitingReview(bool pdf)
    {
        using var client = factory.AuthorizedClient();
        var document = await Upload(client, pdf ? Pdf("SQL Engineer - not certified in Azure") : ResumeSamples.Docx(), pdf);
        var path = $"{Route}/{document.Id}/extraction";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
        using var response = await client.PostAsync(path, null);
        response.EnsureSuccessStatusCode();
        var extraction = (await response.Content.ReadFromJsonAsync<ResumeExtractionDto>())!;
        Assert.Equal(document.Sha256, extraction.SourceSha256);
        Assert.Equal("PendingReview", extraction.ReviewStatus);
        Assert.Equal(pdf ? "Page" : "Paragraph", extraction.UnitKind);
        Assert.Contains(pdf ? "not certified in Azure" : "Synthetic CV", string.Join("\n", extraction.Segments.Select(x => x.Text)));
        Assert.Equal(extraction.Id, (await client.GetFromJsonAsync<ResumeExtractionDto>(path))!.Id);
        var repeated = await client.PostAsync(path, null);
        Assert.Equal(extraction.Id, (await repeated.Content.ReadFromJsonAsync<ResumeExtractionDto>())!.Id);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        Assert.Equal(1, await db.Extractions.CountAsync());
        Assert.Equal("UploadedUnanalysed", (await db.Resumes.SingleAsync()).Status);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Theory]
    [InlineData("malformed")]
    [InlineData("no-text")]
    [InlineData("too-many-pages")]
    public async Task UnsupportedPdf_ShouldNotPersistPartialExtraction(string kind)
    {
        using var client = factory.AuthorizedClient();
        var document = await Upload(client, kind switch
        { "malformed" => ResumeSamples.Pdf(), "no-text" => Pdf(null), _ => Pdf("Text", 41) }, true);
        var response = await client.PostAsync($"{Route}/{document.Id}/extraction", null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Extractions.CountAsync());
    }
    [Fact]
    public async Task AnotherOwnerAndAnonymousCaller_ShouldNotExtractOrReadText()
    {
        using var client = factory.AuthorizedClient();
        var document = await Upload(client, ResumeSamples.Docx(), false);
        var path = $"{Route}/{document.Id}/extraction";
        (await client.PostAsync(path, null)).EnsureSuccessStatusCode();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        var anotherOwner = Guid.NewGuid();
        await db.Extractions.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OwnerId, anotherOwner));
        await db.Resumes.ExecuteUpdateAsync(setters => setters.SetProperty(x => x.OwnerId, anotherOwner));
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(path, null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
    [Fact]
    public async Task ConcurrentExtractions_ShouldPersistOneResultForTheSourceAndEngine()
    {
        using var host = factory.WithWebHostBuilder(_ => { });
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.Token);
        var document = await Upload(client, ResumeSamples.Docx(), false);
        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsync($"{Route}/{document.Id}/extraction", null)));
        foreach (var response in responses) response.EnsureSuccessStatusCode();
        var results = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<ResumeExtractionDto>()));
        Assert.Single(results.Select(x => x!.Id).Distinct());
        foreach (var response in responses) response.Dispose();
        using var scope = factory.Services.CreateScope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Extractions.CountAsync());
    }

    [Fact]
    public async Task MissingWorkerAndDeadline_ShouldFailWithoutPersistingResults()
    {
        using var client = factory.AuthorizedClient();
        var metadata = await Upload(client, ResumeSamples.Docx(), false);
        using var scope = factory.Services.CreateScope();
        var document = (await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Resumes.SingleAsync());
        using var absent = new ProcessResumeTextExtractor(new ConfigurationBuilder().Build());
        await Assert.ThrowsAsync<TextExtractionUnavailableException>(() => absent.ExtractAsync(document, default));
        var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Documents:Extraction:WorkerPath"] = Path.Combine(AppContext.BaseDirectory, "ExtractionWorker", "JobApplicationAgent.Documents.ExtractionWorker.dll"),
            ["Documents:Extraction:TimeoutMilliseconds"] = "1"
        }).Build();
        using var timed = new ProcessResumeTextExtractor(settings);
        await Assert.ThrowsAsync<TextExtractionUnavailableException>(() => timed.ExtractAsync(document, default));
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Extractions.CountAsync());
    }
}
