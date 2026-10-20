using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using JobApplicationAgent.Documents.Tests;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class ResumeValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ValidEnvelopes_ShouldKeepSafeBasename_WithoutClaimingAnalysis(bool docx)
    {
        var result = ResumeFileValidator.Validate(docx ? "C:\\fakepath\\cv.docx" : "../../cv.PDF",
            docx ? ResumeFileValidator.DocxType : "application/pdf", docx ? ResumeSamples.Docx() : ResumeSamples.Pdf());
        Assert.Equal(docx ? "cv.docx" : "cv.PDF", result.FileName);
    }
    [Theory]
    [InlineData("cv.exe", "application/pdf")]
    [InlineData("cv.pdf", "text/plain")]
    [InlineData("cv.docx", "application/pdf")]
    [InlineData("bad\r\n.pdf", "application/pdf")]
    [InlineData("cv\u202Efdp.exe.pdf", "application/pdf")]
    public void MismatchedOrUnsafeFileNames_ShouldBeRejected(string name, string type) =>
        Assert.Throws<InvalidResumeException>(() => ResumeFileValidator.Validate(name, type, ResumeSamples.Pdf()));

    [Theory]
    [InlineData("../escape.xml", "<x />")]
    [InlineData("word/vbaProject.bin", "macro")]
    [InlineData("word/embeddings/object.bin", "object")]
    [InlineData("word/document.xml", "<duplicate />")]
    [InlineData("word/unsafe.xml", "<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///private'>]><x>&e;</x>")]
    [InlineData("word/_rels/document.xml.rels", "<Relationships><Relationship TargetMode='External' Target='https://example.invalid' /></Relationships>")]
    public void UnsafeDocxPackages_ShouldBeRejected(string path, string content) =>
        Assert.Throws<InvalidResumeException>(() => ResumeFileValidator.Validate("cv.docx", ResumeFileValidator.DocxType, ResumeSamples.Docx(path, content)));

    [Fact]
    public void ExpandedZipBomb_ShouldBeRejected() => Assert.Throws<InvalidResumeException>(() =>
        ResumeFileValidator.Validate("cv.docx", ResumeFileValidator.DocxType, ResumeSamples.Docx("word/large.xml", new string('x', ResumeFileValidator.MaxBytes + 1))));

    [Fact]
    public async Task OversizedStream_ShouldFailBeforeRepositoryAccess()
    {
        var repository = new StubRepository();
        var service = new ResumeService(repository, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidResumeException>(() => service.UploadAsync(Guid.NewGuid(), "cv.pdf", "application/pdf",
            new MemoryStream(new byte[ResumeFileValidator.MaxBytes + 1]), default));
        Assert.Null(repository.Saved);
    }
    [Fact]
    public async Task Upload_ShouldRetainOriginalBytesHashAndUnanalysedState()
    {
        var repository = new StubRepository();
        var owner = Guid.NewGuid();
        var bytes = ResumeSamples.Pdf();
        var metadata = await new ResumeService(repository, TimeProvider.System).UploadAsync(owner, "cv.pdf", "application/pdf", new MemoryStream(bytes), default);
        Assert.Equal(owner, repository.Saved!.OwnerId);
        Assert.Equal(bytes, repository.Saved.Content);
        Assert.Equal("UploadedUnanalysed", metadata.Status);
        Assert.Equal(64, metadata.Sha256.Length);
        bytes[0] = 0;
        Assert.NotEqual(bytes[0], repository.Saved.Content[0]);
    }
    private sealed class StubRepository : IResumeRepository
    {
        public ResumeDocument? Saved { get; private set; }
        public Task AddAsync(ResumeDocument document, CancellationToken ct) { Saved = document; return Task.CompletedTask; }
        public Task<ResumeDocument?> GetAsync(Guid ownerId, Guid id, CancellationToken ct) => Task.FromResult<ResumeDocument?>(null);
        public Task<IReadOnlyList<ResumeMetadata>> GetPageAsync(Guid ownerId, int page, int pageSize, CancellationToken ct) => Task.FromResult<IReadOnlyList<ResumeMetadata>>([]);
    }
}
