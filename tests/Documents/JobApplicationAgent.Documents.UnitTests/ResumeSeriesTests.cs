using JobApplicationAgent.Documents.Domain;

namespace JobApplicationAgent.Documents.UnitTests;

public sealed class ResumeSeriesTests
{
    [Fact]
    public void SeriesAndVersion_ShouldNormalizeLabelsAndPreserveUtcMetadata()
    {
        var owner = Guid.NewGuid(); var now = DateTime.UtcNow;
        var series = ResumeSeries.Create(owner, "  Backend  ", now);
        var version = ResumeSeriesVersion.Create(owner, series.Id, Guid.NewGuid(), 1, "  Initial  ", now);
        Assert.Equal("Backend", series.Name); Assert.Equal("Initial", version.Label);
        Assert.Equal(owner, version.OwnerId); Assert.Equal(series.Id, version.SeriesId);
        Assert.Equal(0, version.AddedAtUtc.Ticks % 10); Assert.Equal(DateTimeKind.Utc, series.CreatedAtUtc.Kind);
        Assert.Null(ResumeSeriesVersion.Create(owner, series.Id, Guid.NewGuid(), 1000, " ", now).Label);
    }
    [Theory]
    [InlineData("empty")]
    [InlineData("long")]
    [InlineData("control")]
    [InlineData("owner")]
    [InlineData("date")]
    public void InvalidSeries_ShouldBeRejected(string invalid)
    {
        var name = invalid switch { "empty" => " ", "long" => new string('a', 201), "control" => "A\nB", _ => "Backend" };
        Assert.Throws<ArgumentException>(() => ResumeSeries.Create(invalid == "owner" ? Guid.Empty : Guid.NewGuid(), name,
            invalid == "date" ? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) : DateTime.UtcNow));
    }
    [Theory]
    [InlineData("zero")]
    [InlineData("limit")]
    [InlineData("owner")]
    [InlineData("series")]
    [InlineData("resume")]
    [InlineData("label")]
    [InlineData("control")]
    [InlineData("date")]
    public void InvalidVersion_ShouldBeRejected(string invalid)
    {
        Assert.Throws<ArgumentException>(() => ResumeSeriesVersion.Create(invalid == "owner" ? Guid.Empty : Guid.NewGuid(),
            invalid == "series" ? Guid.Empty : Guid.NewGuid(), invalid == "resume" ? Guid.Empty : Guid.NewGuid(),
            invalid == "zero" ? 0 : invalid == "limit" ? 1001 : 1, invalid == "label" ? new string('a', 201) : invalid == "control" ? "\n" : null,
            invalid == "date" ? DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified) : DateTime.UtcNow));
    }
}
