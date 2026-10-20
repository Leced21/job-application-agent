using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[Authorize]
[ApiController]
[Route("api/v1/documents/drafts/{draftId:guid}/composition")]
public sealed class DocumentCompositionsController(DocumentCompositionService service) : ControllerBase
{
    private Guid Owner => DocumentsOwner.From(User);
    [HttpPost]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("composition-write")]
    public async Task<IActionResult> Compose(Guid draftId, CancellationToken ct)
    {
        var result = await service.ComposeAsync(Owner, draftId, ct);
        return result is null ? NotFound() : result.Created ? CreatedAtAction(nameof(Get), new { draftId }, result) : Ok(result);
    }
    [HttpGet]
    public async Task<IActionResult> Get(Guid draftId, CancellationToken ct)
    {
        var result = await service.GetAsync(Owner, draftId, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpPost("review")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("composition-write")]
    public async Task<IActionResult> Review(Guid draftId, CompositionReviewCommand command, CancellationToken ct)
    {
        var result = await service.ReviewAsync(Owner, draftId, command, ct);
        return result is null ? NotFound() : Ok(result);
    }
    [HttpGet("/api/v1/documents/compositions")]
    public async Task<IActionResult> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.GetPageAsync(Owner, page, pageSize, ct));
}
