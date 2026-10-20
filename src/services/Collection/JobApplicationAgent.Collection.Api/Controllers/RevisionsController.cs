using JobApplicationAgent.Collection.Application.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace JobApplicationAgent.Collection.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "CollectionAuth")]
[Route("api/v1/collection/imports/{offerId:guid}/revisions")]
public sealed class RevisionsController(ReviewOfferHandler handler, GetImportedOffersHandler offers) : ControllerBase
{
    [HttpPost("{revisionId:guid}/publish")]
    [Authorize(Policy = "CollectionAdmin")]
    public async Task<IActionResult> Publish(Guid offerId, Guid revisionId,
        [FromServices] PublishOfferHandler publisher, CancellationToken ct)
    {
        var receipt = await publisher.HandleAsync(offerId, revisionId, ct);
        return receipt is null ? NotFound() : Ok(receipt);
    }
    [HttpGet]
    public async Task<IActionResult> Get(Guid offerId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (await offers.GetByIdAsync(offerId, cancellationToken) is null) return NotFound();
        return Ok(await handler.GetPageAsync(offerId, page, pageSize, cancellationToken));
    }

    [HttpPost("{revisionId:guid}/review")]
    [Authorize(Policy = "CollectionAdmin")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Review(Guid offerId, Guid revisionId, ReviewOfferCommand command, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("jobagent_owner_id"), out var reviewerOwnerId)) return Forbid();
        var revision = await handler.ReviewAsync(offerId, revisionId, command, reviewerOwnerId, cancellationToken);
        return revision is null ? NotFound() : Ok(revision);
    }
}
