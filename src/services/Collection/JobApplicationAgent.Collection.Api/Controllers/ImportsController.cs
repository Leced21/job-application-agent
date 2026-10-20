using JobApplicationAgent.Collection.Application.Imports;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace JobApplicationAgent.Collection.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "CollectionAuth")]
[Route("api/v1/collection/imports")]
public sealed class ImportsController(ImportOfferHandler importHandler, GetImportedOffersHandler readHandler, TimeProvider clock) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "CollectionAdmin")]
    [RequestSizeLimit(262144)]
    public async Task<IActionResult> Import(ImportOfferCommand command, CancellationToken cancellationToken)
    {
        var result = await importHandler.HandleAsync(command, cancellationToken);
        var response = new ImportOfferResponse(ImportedOfferDto.From(result.Offer, clock.GetUtcNow().UtcDateTime),
            result.IsDuplicate, result.ContentChanged);
        return result.IsDuplicate ? Ok(response) : CreatedAtAction(nameof(GetById), new { id = result.Offer.Id }, response);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var offer = await readHandler.GetByIdAsync(id, cancellationToken);
        return offer is null ? NotFound() : Ok(offer);
    }
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => Ok(await readHandler.GetPageAsync(page, pageSize, cancellationToken));
}
