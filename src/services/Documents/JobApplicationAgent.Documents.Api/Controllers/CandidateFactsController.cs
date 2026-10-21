using System.Security.Claims;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Documents.Api;

[Authorize]
[ApiController]
[Route("api/v1/documents/resumes/{resumeId:guid}/facts")]
public sealed class CandidateFactsController(ResumeCandidateFactService service) : ControllerBase
{
    [HttpPost("{factId:guid}/profile-mapping")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("resume-fact-write")]
    public async Task<IActionResult> ConfirmMapping(Guid resumeId, Guid factId, ConfirmProfileDatesCommand command,
        [FromServices] ProfileFactMappingService mappings, CancellationToken ct)
    {
        var mapping = await mappings.ConfirmAsync(Owner, resumeId, factId, command, ct);
        return mapping is null ? NotFound() : Ok(mapping);
    }
    [HttpGet("{factId:guid}/profile-mapping")]
    public async Task<IActionResult> GetMapping(Guid resumeId, Guid factId,
        [FromServices] ProfileFactMappingService mappings, CancellationToken ct)
    {
        var mapping = await mappings.GetAsync(Owner, resumeId, factId, ct);
        return mapping is null ? NotFound() : Ok(mapping);
    }
    [HttpPost("{factId:guid}/profile-import")]
    [EnableRateLimiting("resume-fact-write")]
    public async Task<IActionResult> Import(Guid resumeId, Guid factId, [FromServices] ProfileFactImportService imports, CancellationToken ct)
    {
        var receipt = await imports.ImportAsync(Owner, resumeId, factId, ct);
        return receipt is null ? NotFound() : Ok(receipt);
    }
    private Guid Owner => DocumentsOwner.From(User);
    [HttpGet("/api/v1/documents/fact-schema")]
    public IActionResult Schema() => Ok(CandidateFactBuilder.Schemas);
    [HttpPost]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("resume-fact-write")]
    public async Task<IActionResult> Create(Guid resumeId, CreateCandidateFactCommand command, CancellationToken ct)
    {
        var result = await service.CreateAsync(Owner, resumeId, command, ct);
        return result is null ? NotFound() : result.Created
            ? CreatedAtAction(nameof(Get), new { resumeId, factId = result.Fact.Id }, result.Fact) : Ok(result.Fact);
    }
    [HttpGet("{factId:guid}")]
    public async Task<IActionResult> Get(Guid resumeId, Guid factId, CancellationToken ct)
    {
        var fact = await service.GetAsync(Owner, resumeId, factId, ct);
        return fact is null ? NotFound() : Ok(fact);
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid resumeId, [FromQuery] string? decision = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var facts = await service.GetPageAsync(Owner, resumeId, decision, page, pageSize, ct);
        return facts is null ? NotFound() : Ok(facts);
    }
    [HttpPost("{factId:guid}/review")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("resume-fact-write")]
    public async Task<IActionResult> Review(Guid resumeId, Guid factId, ReviewCandidateFactCommand command, CancellationToken ct)
    {
        var fact = await service.ReviewAsync(Owner, resumeId, factId, command, ct);
        return fact is null ? NotFound() : Ok(fact);
    }
}
