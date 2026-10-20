using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[Authorize]
[ApiController]
[Route("api/v1/documents/resume-series")]
public sealed class ResumeSeriesController(ResumeSeriesService service) : ControllerBase
{
    private Guid Owner => DocumentsOwner.From(User);
    [HttpPost]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("resume-series-write")]
    public async Task<IActionResult> Create(CreateResumeSeriesCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(Owner, command, ct);
        return result is null ? NotFound() : result.Created
            ? CreatedAtAction(nameof(Get), new { seriesId = result.Series.Id }, result) : Ok(result);
    }
    [HttpPost("{seriesId:guid}/versions")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("resume-series-write")]
    public async Task<IActionResult> AddVersion(Guid seriesId, AddResumeVersionCommand command, CancellationToken ct)
    {
        var result = await service.AddVersionAsync(Owner, seriesId, command, ct);
        return result is null ? NotFound() : result.Created
            ? CreatedAtAction(nameof(GetVersion), new { seriesId, number = result.Version.VersionNumber }, result) : Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.GetPageAsync(Owner, page, pageSize, ct));
    [HttpGet("{seriesId:guid}")]
    public async Task<IActionResult> Get(Guid seriesId, CancellationToken ct)
    {
        var series = await service.GetAsync(Owner, seriesId, ct);
        return series is null ? NotFound() : Ok(series);
    }
    [HttpGet("{seriesId:guid}/versions")]
    public async Task<IActionResult> Versions(Guid seriesId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var versions = await service.GetVersionsAsync(Owner, seriesId, page, pageSize, ct);
        return versions is null ? NotFound() : Ok(versions);
    }
    [HttpGet("{seriesId:guid}/versions/{number:int}")]
    public async Task<IActionResult> GetVersion(Guid seriesId, int number, CancellationToken ct)
    {
        var version = await service.GetVersionAsync(Owner, seriesId, number, ct);
        return version is null ? NotFound() : Ok(version);
    }
    [HttpGet("/api/v1/documents/resumes/{resumeId:guid}/series")]
    public async Task<IActionResult> ForResume(Guid resumeId, CancellationToken ct)
    {
        var series = await service.GetForResumeAsync(Owner, resumeId, ct);
        return series is null ? NotFound() : Ok(series);
    }
}
