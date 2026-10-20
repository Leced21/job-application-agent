using System.Security.Claims;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Applications.Api;

public sealed record PlanFollowUp(long ExpectedRevision, DateTime DueAtUtc, ContactChannel Channel, string Note);
public sealed record CompleteFollowUp(long ExpectedRevision, bool Confirmed, DateTime OccurredAtUtc, ContactChannel Channel, string Outcome);
public sealed record CancelFollowUp(long ExpectedRevision, bool Confirmed, string Reason);
[Authorize]
[ApiController]
[Route("api/v1/applications")]
public sealed class FollowUpsController(FollowUpService service) : ControllerBase
{
    private Guid Owner => ApplicationsOwner.From(User);
    [HttpGet("follow-ups/due")]
    public async Task<IActionResult> Due([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => Ok(await service.DueAsync(Owner, page, pageSize, ct));
    [HttpPost("{id:guid}/follow-ups")]
    [RequestSizeLimit(8192)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Plan(Guid id, PlanFollowUp command, CancellationToken ct)
    { var value = await service.PlanAsync(Owner, id, command.ExpectedRevision, command.DueAtUtc, command.Channel, command.Note, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/follow-ups/{followUpId:guid}/reschedule")]
    [RequestSizeLimit(8192)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Reschedule(Guid id, Guid followUpId, PlanFollowUp command, CancellationToken ct)
    { var value = await service.RescheduleAsync(Owner, id, followUpId, command.ExpectedRevision, command.DueAtUtc, command.Channel, command.Note, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/follow-ups/{followUpId:guid}/complete")]
    [RequestSizeLimit(8192)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Complete(Guid id, Guid followUpId, CompleteFollowUp command, CancellationToken ct)
    { var value = await service.CompleteAsync(Owner, id, followUpId, command.ExpectedRevision, command.Confirmed, command.OccurredAtUtc, command.Channel, command.Outcome, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/follow-ups/{followUpId:guid}/cancel")]
    [RequestSizeLimit(8192)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Cancel(Guid id, Guid followUpId, CancelFollowUp command, CancellationToken ct)
    { var value = await service.CancelAsync(Owner, id, followUpId, command.ExpectedRevision, command.Confirmed, command.Reason, ct); return value is null ? NotFound() : Ok(value); }
}
