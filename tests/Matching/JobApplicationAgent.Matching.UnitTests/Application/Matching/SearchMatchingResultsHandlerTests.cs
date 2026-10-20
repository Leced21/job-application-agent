
using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using NSubstitute;
using Xunit;

namespace JobApplicationAgent.Matching.UnitTests.Application.Matching;

public sealed class SearchMatchingResultsHandlerTests
{
    private readonly IMatchingResultRepository _repository =
        Substitute.For<IMatchingResultRepository>();

    private readonly SearchMatchingResultsHandler _handler;

    public SearchMatchingResultsHandlerTests()
    {
        _handler = new SearchMatchingResultsHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPage_WhenFilterIsValid()
    {
        // Arrange
        var filter = new MatchingResultFilter(
            CandidateProfileId: Guid.NewGuid(),
            JobId: Guid.NewGuid(),
            Status: "Completed",
            Page: 1,
            PageSize: 20);

        var expected = new MatchingResultPage(
            new[]
            {
                new MatchingResultSummary(
                    Guid.NewGuid(),
                    filter.CandidateProfileId!.Value,
                    filter.JobId!.Value,
                    "Completed",
                    90m,
                    100m,
                    DateTime.UtcNow,
                    DateTime.UtcNow)
            },
            TotalCount: 1,
            Page: 1,
            PageSize: 20,
            TotalPages: 1);

        _repository.SearchAsync(
                filter,
                Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _handler.HandleAsync(filter);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal(90m, result.Items.Single().OverallScore);

        await _repository.Received(1).SearchAsync(
            filter,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectInvalidPage()
    {
        var filter = new MatchingResultFilter(
            null, null, null, Page: 0, PageSize: 20);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.HandleAsync(filter));

        await _repository.DidNotReceive().SearchAsync(
            Arg.Any<MatchingResultFilter>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task HandleAsync_ShouldRejectInvalidPageSize(
        int pageSize)
    {
        var filter = new MatchingResultFilter(
            null, null, null, Page: 1, PageSize: pageSize);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _handler.HandleAsync(filter));

        await _repository.DidNotReceive().SearchAsync(
            Arg.Any<MatchingResultFilter>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectEmptyCandidateProfileId()
    {
        var filter = new MatchingResultFilter(
            Guid.Empty, null, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.HandleAsync(filter));

        await _repository.DidNotReceive().SearchAsync(
            Arg.Any<MatchingResultFilter>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectEmptyJobId()
    {
        var filter = new MatchingResultFilter(
            null, Guid.Empty, null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.HandleAsync(filter));

        await _repository.DidNotReceive().SearchAsync(
            Arg.Any<MatchingResultFilter>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmptyPage_WhenNoResultExists()
    {
        // Arrange
        var filter = new MatchingResultFilter(
            null, null, "Completed");

        var expected = new MatchingResultPage(
            Array.Empty<MatchingResultSummary>(),
            TotalCount: 0,
            Page: 1,
            PageSize: 20,
            TotalPages: 0);

        _repository.SearchAsync(
                filter,
                Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _handler.HandleAsync(filter);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }
}
