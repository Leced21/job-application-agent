using JobApplicationAgent.Profile.Application.Profiles.DocumentFacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationAgent.Profile.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "DocumentsImport")]
[Route("api/v1/profile/document-facts")]
public sealed class DocumentFactsController(ImportDocumentFactHandler handler, DocumentImportIdentity identity) : ControllerBase
{
    [HttpGet("{factId:guid}")]
    public async Task<IActionResult> Get(Guid factId, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var receipt = await handler.GetAsync(identity.OwnerId, identity.ProfileId, factId, ct);
        return receipt is null ? NotFound() : Ok(receipt);
    }
    [HttpPost]
    [RequestSizeLimit(131072)]
    public async Task<IActionResult> Import(ImportDocumentFactCommand command, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        var result = await handler.HandleAsync(identity.OwnerId, identity.ProfileId, command, ct);
        return result.Created ? CreatedAtAction(nameof(Get), new { factId = result.Receipt.FactId }, result.Receipt) : Ok(result.Receipt);
    }
}
