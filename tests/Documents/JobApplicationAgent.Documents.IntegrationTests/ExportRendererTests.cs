using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Documents.Tests;
using UglyToad.PdfPig;

namespace JobApplicationAgent.Documents.IntegrationTests;

public sealed class ExportRendererTests
{
    private static ComposedDocumentContent Content() => VerifiedDocumentComposer.Compose(CompositionSamples.Create(dates: true).Draft);
    [Fact]
    public async Task Pdf_ShouldEmbedFontPreserveAccentsAndKeepAllGlyphsInsideMargins()
    {
        var result = await new ApprovedDocumentRenderer().RenderAsync(Content(), "Pdf", default);
        using var document = PdfDocument.Open(result.Content);
        Assert.Equal(result.PageCount, document.NumberOfPages); Assert.InRange(document.NumberOfPages, 1, 40);
        var text = string.Join(" ", document.GetPages().Select(x => x.Text));
        Assert.Contains("Compétence", text); Assert.Contains("No Azure certification claimed", text); Assert.Contains("2020-03-12", text);
        foreach (var page in document.GetPages())
            Assert.All(page.Letters.Where(x => !string.IsNullOrWhiteSpace(x.Value)), letter =>
            { Assert.InRange(letter.BoundingBox.Left, 50, 545); Assert.InRange(letter.BoundingBox.Right, 50, 545); Assert.InRange(letter.BoundingBox.Bottom, 25, 810); });
        Assert.True(result.Content.Length < 5242880);
    }
    [Fact]
    public async Task Docx_ShouldRoundTripExactTextAndContainOnlyInternalRelationships()
    {
        var content = Content() with { PlainText = Content().PlainText + "\n<script>test & données</script>\n中文" };
        var result = await new ApprovedDocumentRenderer().RenderAsync(content, "Docx", default);
        Assert.Null(result.PageCount);
        using var zip = new ZipArchive(new MemoryStream(result.Content)); XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        using var xml = zip.GetEntry("word/document.xml")!.Open(); var document = XDocument.Load(xml);
        Assert.Equal(content.PlainText, string.Join("\n", document.Descendants(w + "p").Select(x => string.Concat(x.Descendants(w + "t").Select(t => t.Value)))));
        foreach (var entry in zip.Entries.Where(x => x.FullName.EndsWith(".rels")))
        { using var stream = entry.Open(); Assert.DoesNotContain(XDocument.Load(stream).Descendants(), x => (string?)x.Attribute("TargetMode") == "External"); }
        Assert.NotNull(zip.GetEntry("word/fonts/Vera.odttf"));
        Assert.DoesNotContain(zip.Entries, x => x.FullName.Contains("vba", StringComparison.OrdinalIgnoreCase));
        ResumeFileValidator.Validate("export.docx", ResumeFileValidator.DocxType, result.Content);
    }
    [Theory]
    [InlineData("glyph")]
    [InlineData("pages")]
    [InlineData("length")]
    [InlineData("control")]
    public async Task UnsupportedOrExcessivePdf_ShouldFailWithoutTruncation(string invalid)
    {
        var text = invalid switch { "glyph" => "中文", "pages" => string.Join("\n", Enumerable.Repeat("SQL", 2000)),
            "length" => new string('a', 100001), _ => "SQL\0Azure" };
        await Assert.ThrowsAsync<DraftRejectedException>(() => new ApprovedDocumentRenderer().RenderAsync(Content() with { PlainText = text }, "Pdf", default));
    }
    [Fact]
    public async Task LongText_ShouldWrapAcrossPagesWithoutLosingTheLastLine()
    {
        var text = "Titre candidature\n\n" + string.Join("\n\n", Enumerable.Range(0, 100).Select(i => $"Paragraphe {i:D3} " + new string('W', 120))) + "\nFIN vérifiée";
        var result = await new ApprovedDocumentRenderer().RenderAsync(Content() with { PlainText = text }, "Pdf", default);
        using var document = PdfDocument.Open(result.Content); Assert.True(document.NumberOfPages > 1);
        Assert.Contains("FIN vérifiée", document.GetPage(document.NumberOfPages).Text);
        foreach (var page in document.GetPages()) Assert.All(page.Letters, letter => Assert.True(letter.BoundingBox.Right <= 546));
    }
}
