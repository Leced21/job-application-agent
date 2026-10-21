using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[Authorize]
[ApiController]
[Route("api/v1/documents/resumes")]
public sealed class ResumesController(ResumeService service) : ControllerBase
{
    private Guid Owner => DocumentsOwner.From(User);
    [HttpPost("{id:guid}/extraction")]
    [EnableRateLimiting("resume-extraction")]
    public async Task<IActionResult> Extract(Guid id, [FromServices] ResumeExtractionService extraction, CancellationToken ct)
    {
        var result = await extraction.ExtractAsync(Owner, id, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpGet("{id:guid}/extraction")]
    public async Task<IActionResult> GetExtraction(Guid id, [FromServices] ResumeExtractionService extraction, CancellationToken ct)
    {
        var result = await extraction.GetAsync(Owner, id, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [EnableRateLimiting("resume-upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var metadata = await service.UploadAsync(Owner, file.FileName, file.ContentType, stream, ct);
        return CreatedAtAction(nameof(Get), new { id = metadata.Id }, metadata);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.GetPageAsync(Owner, page, pageSize, ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var document = await service.GetAsync(Owner, id, ct);
        return document is null ? NotFound() : Ok(ResumeMetadata.From(document));
    }
    [HttpGet("{id:guid}/content")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var document = await service.GetAsync(Owner, id, ct);
        return document is null ? NotFound() : File(document.Content, document.MediaType, document.FileName);
    }
}
