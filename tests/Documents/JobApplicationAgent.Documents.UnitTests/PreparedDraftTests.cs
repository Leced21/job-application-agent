using System.Security.Cryptography;
using System.Text;
using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class PreparedDraftTests
{
    [Fact]
    public void Draft_ShouldFreezeExactSnapshotHashAndRemainPendingReview()
    {
        const string json = "{\"source\":\"données vérifiées\"}";
        var draft = PreparedDocumentDraft.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Resume", new string('a', 64), json, DateTime.UtcNow);
        Assert.Equal("PreparedForReview", draft.Status); Assert.Equal(json, draft.SnapshotJson);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json))), draft.SnapshotSha256);
        Assert.Equal(0, draft.CreatedAtUtc.Ticks % 10);
    }
    [Theory]
    [InlineData("owner")]
    [InlineData("request")]
    [InlineData("resume")]
    [InlineData("job")]
    [InlineData("kind")]
    [InlineData("hash")]
    [InlineData("empty")]
    [InlineData("size")]
    [InlineData("date")]
    public void InvalidDraft_ShouldBeRejected(string invalid)
    {
        Assert.Throws<ArgumentException>(() => PreparedDocumentDraft.Create(invalid == "owner" ? Guid.Empty : Guid.NewGuid(),
            invalid == "request" ? Guid.Empty : Guid.NewGuid(), invalid == "resume" ? Guid.Empty : Guid.NewGuid(),
            invalid == "job" ? Guid.Empty : Guid.NewGuid(), invalid == "kind" ? "Unknown" : "Resume",
            invalid == "hash" ? "invalid" : new string('a', 64), invalid == "empty" ? " " : invalid == "size" ? new string('é', 1048577) : "{}",
            invalid == "date" ? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) : DateTime.UtcNow));
    }
}
