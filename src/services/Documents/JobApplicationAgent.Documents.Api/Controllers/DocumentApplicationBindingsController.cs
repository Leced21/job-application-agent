using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[ApiController]
public sealed class DocumentApplicationBindingsController(DocumentApplicationBindingService service) : ControllerBase
{
    private Guid Owner => DocumentsOwner.From(User);
    [Authorize(AuthenticationSchemes = "DocumentsAuth")]
    [HttpPost("api/v1/documents/exports/{id:guid}/application-binding")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("composition-write")]
    public async Task<IActionResult> Confirm(Guid id, ConfirmDocumentApplicationBinding command, CancellationToken ct)
    { var result = await service.ConfirmAsync(Owner, id, command, ct); return result is null ? NotFound() : Ok(result); }
    [Authorize(AuthenticationSchemes = "DocumentsAuth")]
    [HttpGet("api/v1/documents/exports/{id:guid}/application-binding")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var result = await service.GetVerifiedAsync(Owner, id, ct); return result is null ? NotFound() : Ok(result); }
    [Authorize(AuthenticationSchemes = "ApplicationsChannel")]
    [HttpGet("api/v1/documents/application-bindings/{id:guid}")]
    public async Task<IActionResult> Verify(Guid id, CancellationToken ct)
    { var result = await service.GetVerifiedAsync(Owner, id, ct); return result is null ? NotFound() : Ok(result); }
}
