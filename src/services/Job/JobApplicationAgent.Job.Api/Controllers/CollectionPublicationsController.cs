using JobApplicationAgent.Job.Application.Jobs.Create;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace JobApplicationAgent.Job.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "CollectionPublisher")]
[Route("api/v1/jobs/collection-publications")]
public sealed class CollectionPublicationsController(PublishCollectionHandler handler) : ControllerBase
{
    [HttpGet("{offerId:guid}")]
    public async Task<IActionResult> Get(Guid offerId, CancellationToken ct)
    {
        var receipt = await handler.GetAsync(offerId, ct);
        return receipt is null ? NotFound() : Ok(receipt);
    }
    [HttpPost]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Publish(PublishCollectionCommand command, CancellationToken ct)
    {
        var outcome = await handler.HandleAsync(command, ct);
        return outcome.Created ? CreatedAtAction(nameof(Get), new { offerId = command.OfferId }, outcome.Receipt) : Ok(outcome.Receipt);
    }
}
