using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Models;
using JobApplicationAgent.Matching.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using JobApplicationAgent.Matching.Application.Abstractions;

namespace JobApplicationAgent.Matching.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/matching")]
public sealed class MatchingController(CalculateMatchingHandler handler, GetMatchingResultHandler getMatchingResultHandler, SearchMatchingResultsHandler searchMatchingResultsHandler, RecommendationHandler recommendationHandler, IProfileMatchingDataClient profileClient) : ControllerBase
{
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate([FromBody] CalculateMatchingRequest request, CancellationToken cancellationToken)
    {
        if (request.JobId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Job ID is required."
            });
        }


        MatchingResult? result;

        try
        {
            result = await handler.HandleAsync(
                request.JobId,
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Matching service dependency unavailable",
                detail: "The Job or Profile service could not be reached.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Matching service dependency timeout",
                detail: "The Job or Profile service did not respond in time.");
        }

        if (result is null)
        {
            return NotFound(new
            {
                message = "Job or candidate profile not found."
            });
        }

        return Ok(new CalculateMatchingResponse(
            result.Id,
            result.CandidateProfileId,
            result.JobId,
            result.Status.ToString(),
            result.OverallScore,
            result.EvaluationCoverage,
            result.Criteria.Select(c => new MatchingCriterionResponse(
                c.Name,
                c.Score,
                c.Weight,
                c.Status.ToString(),
                c.Explanation)).ToArray(),
            result.CreatedAtUtc,
            result.CompletedAtUtc)
        {
            ScoringVersion = result.ScoringVersion
        });
    }

    [HttpGet("results/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Matching result ID is required."
            });
        }

        var profile = await profileClient.GetAsync(cancellationToken);
        if (profile is null) return NotFound(new { message = "Matching result not found." });
        var result = await getMatchingResultHandler.HandleAsync(id, cancellationToken);

        if (result is null || result.CandidateProfileId != profile.CandidateProfileId)
        {
            return NotFound(new
            {
                message = "Matching result not found."
            });
        }

        return Ok(result);
    }

    [HttpGet("results")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? candidateProfileId,
        [FromQuery] Guid? jobId,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new
            {
                message = "Page must be >= 1 and pageSize must be between 1 and 100."
            });
        }

        if (candidateProfileId == Guid.Empty || jobId == Guid.Empty)
        {
            return BadRequest(new
            {
                message = "Candidate profile ID and Job ID cannot be empty."
            });
        }

        var validStatuses = new[]
        {
            "Pending",
            "Completed",
            "InsufficientData",
            "Failed"
        };

        if (!string.IsNullOrWhiteSpace(status) &&
            !validStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = "Invalid matching status."
            });
        }

        var normalizedStatus = validStatuses.FirstOrDefault(
            value => string.Equals(
                value,
                status,
                StringComparison.OrdinalIgnoreCase));

        var profile = await profileClient.GetAsync(cancellationToken);
        if (profile is null) return NotFound(new { message = "Candidate profile not found." });
        if (candidateProfileId is { } requestedProfile && requestedProfile != profile.CandidateProfileId)
            return BadRequest(new { message = "The requested profile is unavailable." });

        var filter = new MatchingResultFilter(
            profile.CandidateProfileId,
            jobId,
            normalizedStatus,
            page,
            pageSize);

        var result = await searchMatchingResultsHandler.HandleAsync(
            filter,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("recommendations")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetRecommendations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || page > 1_000_000)
        {
            return BadRequest(new
            {
                Message = "Page must be between 1 and 1000000."
            });
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new
            {
                Message = "Page size must be between 1 and 100."
            });
        }

        RecommendationResponse? result;
        try
        {
            result = await recommendationHandler.HandleAsync(page, pageSize, cancellationToken);
        }
        catch (RecommendationCapacityExceededException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Recommendation processing capacity exceeded",
                detail: "The catalogue exceeds the synchronous recommendation processing limits.");
        }
        catch (System.Text.Json.JsonException)
        {
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Matching service dependency invalid response",
                detail: "The Job or Profile service returned an invalid response.");
        }
        catch (HttpRequestException)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Matching service dependency unavailable",
                detail: "The Job or Profile service could not be reached.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Matching service dependency timeout",
                detail: "The recommendation request or a dependency exceeded its processing time limit.");
        }

        return result is null
            ? NotFound(new
            {
                Message = "Candidate profile not found."
            })
            : Ok(result);
    }
}

public sealed record CalculateMatchingRequest(Guid JobId);

public sealed record MatchingCriterionResponse(
    string Name,
    decimal? Score,
    decimal Weight,
    string Status,
    string Explanation);

public sealed record CalculateMatchingResponse(
    Guid Id,
    Guid CandidateProfileId,
    Guid JobId,
    string Status,
    decimal? OverallScore,
    decimal EvaluationCoverage,
    IReadOnlyCollection<MatchingCriterionResponse> Criteria,
    DateTime CreatedAtUtc,
    DateTime? CompletedAtUtc)
{
    public string ScoringVersion { get; init; } = "legacy-v1";
}
