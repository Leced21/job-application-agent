using JobApplicationAgent.Matching.Application.Abstractions;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using NSubstitute;

namespace JobApplicationAgent.Matching.UnitTests.Application.Matching;

public sealed class GetMatchingResultHandlerTests
{
    private readonly IMatchingResultRepository _repository = Substitute.For<IMatchingResultRepository>();

    private readonly GetMatchingResultHandler _handler;

    public GetMatchingResultHandlerTests()
    {
        _handler = new GetMatchingResultHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnResult_WhenMatchingExists()
    {
        // Arrange
        var matchingId = Guid.NewGuid();

        var expectedResult = new MatchingResultDetails(
            matchingId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Completed",
            90m,
            100m,
            Array.Empty<MatchingCriterionDetails>(),
            DateTime.UtcNow,
            DateTime.UtcNow);

        _repository.GetByIdAsync(
                matchingId,
                Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        // Act
        var result = await _handler.HandleAsync(matchingId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(matchingId, result.Id);
        Assert.Equal(90m, result.OverallScore);
        Assert.Equal("Completed", result.Status);

        await _repository.Received(1).GetByIdAsync(
            matchingId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenMatchingDoesNotExist()
    {
        // Arrange
        var matchingId = Guid.NewGuid();

        _repository.GetByIdAsync(
                matchingId,
                Arg.Any<CancellationToken>())
            .Returns((MatchingResultDetails?)null);

        // Act
        var result = await _handler.HandleAsync(matchingId);

        // Assert
        Assert.Null(result);

        await _repository.Received(1).GetByIdAsync(
            matchingId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ShouldRejectEmptyMatchingId()
    {
        // Act
        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.HandleAsync(Guid.Empty));

        // Assert
        await _repository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }
}
