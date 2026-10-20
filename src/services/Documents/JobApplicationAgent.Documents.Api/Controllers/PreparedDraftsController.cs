using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[Authorize]
[ApiController]
[Route("api/v1/documents/drafts")]
public sealed class PreparedDraftsController(PreparedDraftService service) : ControllerBase
{
    private Guid Owner => DocumentsOwner.From(User);
    [HttpPost]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("draft-prepare")]
    public async Task<IActionResult> Prepare(PrepareDraftCommand command, CancellationToken ct)
    {
        var result = await service.PrepareAsync(Owner, command, ct);
        return result is null ? NotFound() : result.Created
            ? CreatedAtAction(nameof(Get), new { id = result.Document.Draft.Id }, result) : Ok(result);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var document = await service.GetAsync(Owner, id, ct);
        return document is null ? NotFound() : Ok(document);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await service.GetPageAsync(Owner, page, pageSize, ct));
}
