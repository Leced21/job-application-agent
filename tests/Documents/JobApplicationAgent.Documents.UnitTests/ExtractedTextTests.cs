using JobApplicationAgent.Documents.Application;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class ExtractedTextTests
{
    [Theory]
    [InlineData("version")]
    [InlineData("unit")]
    [InlineData("index")]
    [InlineData("empty")]
    [InlineData("length")]
    [InlineData("pages")]
    public void InvalidResults_ShouldBeRejectedBeforePersistence(string invalid)
    {
        var result = new ExtractedText(invalid == "version" ? "unknown" : ExtractedText.Version,
            invalid == "unit" ? "Experience" : "Page", invalid == "pages"
                ? Enumerable.Range(1, 41).Select(x => new TextSegment(x, "Text")).ToArray()
                : [new(invalid == "index" ? 0 : 1, invalid == "empty" ? " " : invalid == "length" ? new string('x', 200001) : "Text")]);
        Assert.Throws<TextExtractionRejectedException>(result.Validate);
    }
    [Fact]
    public void SourceSegments_ShouldKeepEvidenceWithoutInferringQualifications()
    {
        var result = new ExtractedText(ExtractedText.Version, "Paragraph", [new(1, "Not certified in Azure."), new(2, "SQL experience")]);
        result.Validate();
        Assert.Equal("Not certified in Azure.", result.Segments[0].Text);
        Assert.Equal("SQL experience", result.Segments[1].Text);
    }
}
