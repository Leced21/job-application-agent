using JobApplicationAgent.Collection.Application.Connectors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Collection.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "CollectionAuth")]
[Route("api/v1/collection/sources")]
public sealed class SourcesController(CollectSourceHandler handler) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(handler.GetSources());

    [HttpPost("{sourceId}/collect")]
    [Authorize(Policy = "CollectionAdmin")]
    [RequestSizeLimit(1048576)]
    [EnableRateLimiting("source-collection")]
    public async Task<IActionResult> Collect(string sourceId, SourceCollectionRequest request, CancellationToken ct) =>
        Ok(await handler.HandleAsync(sourceId, request, ct));
}
