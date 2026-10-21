using System.Security.Claims;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobApplicationAgent.Applications.Api;

public sealed record AddCandidacyNote(long ExpectedRevision, string Note);
public sealed record WithdrawCandidacy(long ExpectedRevision, bool Confirmed, string Reason);
public sealed record AttachCandidacyDocument(long ExpectedRevision, Guid ExportId);
public sealed record ReviewCandidacy(long ExpectedRevision, bool Confirmed = false);
public sealed record DeclareManualSubmission(long ExpectedRevision, bool Confirmed, DateTime OccurredAtUtc, ContactChannel Channel, string? Reference);
public sealed record ApproveControlledSubmission(long ExpectedRevision, bool Confirmed, string ReviewSha256);
public sealed record PrepareControlledSubmissionAttempt(long ExpectedRevision, bool Confirmed, Guid AttemptId,
    string PlatformId, string ReviewSha256);
public sealed record RecordControlledSubmissionAttemptResult(long ExpectedRevision, Guid AttemptId,
    ControlledSubmissionOutcome Outcome, DateTime OccurredAtUtc, bool Confirmed, string? Reference);
public sealed record RecordRecruiterUpdate(long ExpectedRevision, bool Confirmed, CandidacyStatus Status, DateTime OccurredAtUtc, ContactChannel Channel, string Message);

[Authorize]
[ApiController]
[Route("api/v1/applications")]
public sealed class CandidaciesController(CreateCandidacyService create, CandidacyTrackingService tracking,
    CandidacyReviewService review, ISubmissionPlatformCatalog submissionPlatforms) : ControllerBase
{
    private Guid Owner => ApplicationsOwner.From(User);
    [HttpGet("submission-platforms")]
    public IActionResult SubmissionPlatforms() => Ok(new
    {
        externalSubmissionEnabled = submissionPlatforms.AutomationEnabled,
        platformIds = submissionPlatforms.EligiblePlatformIds
    });

    [HttpPost("{id:guid}/manual-submission")]
    [RequestSizeLimit(8192)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> ManualSubmission(Guid id, DeclareManualSubmission command, CancellationToken ct)
    { var value = await tracking.DeclareSubmissionAsync(Owner, id, command.ExpectedRevision, command.Confirmed, new(command.OccurredAtUtc, command.Channel, command.Reference), ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/submission-approval")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> ApproveControlled(Guid id, ApproveControlledSubmission command, CancellationToken ct)
    { var value = await tracking.ApproveControlledSubmissionAsync(Owner, id, command.ExpectedRevision, command.Confirmed, command.ReviewSha256, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/submission-attempts")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> PrepareSubmissionAttempt(Guid id, PrepareControlledSubmissionAttempt command, CancellationToken ct)
    {
        var value = await tracking.PrepareControlledSubmissionAttemptAsync(Owner, id, command.ExpectedRevision, command.Confirmed,
            command.AttemptId, command.PlatformId, command.ReviewSha256, ct);
        return value is null ? NotFound() : Ok(value);
    }
    [HttpGet("{id:guid}/submission-preview")]
    public async Task<IActionResult> SubmissionPreview(Guid id, CancellationToken ct)
    {
        var value = await tracking.GetAsync(Owner, id, ct);
        if (value is null) return NotFound();
        if (value.Status != CandidacyStatus.Approved || value.Submission is not null ||
            value.SubmissionAttempt is not null && value.SubmissionAttemptResult?.Outcome != ControlledSubmissionOutcome.DefinitelyNotSubmitted) return Conflict();
        if (value.Resume is null || value.CoverLetter is null) return Conflict();
        return Ok(new
        {
            value.Id, value.Revision, value.JobTitle, value.CompanyName,
            resume = new { value.Resume.ExportId, value.Resume.Sha256 },
            coverLetter = new { value.CoverLetter.ExportId, value.CoverLetter.Sha256 },
            reviewSha256 = ControlledSubmissionPolicy.ReviewSha256(value),
            externalSubmissionEnabled = false
        });
    }
    [HttpPost("{id:guid}/submission-attempt-result")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> RecordSubmissionAttemptResult(Guid id, RecordControlledSubmissionAttemptResult command, CancellationToken ct)
    {
        var value = await tracking.RecordControlledSubmissionResultAsync(Owner, id, command.ExpectedRevision, command.Confirmed,
            new(command.AttemptId, command.Outcome, command.OccurredAtUtc, command.Reference), ct);
        return value is null ? NotFound() : Ok(value);
    }
    [HttpPost("{id:guid}/recruiter-updates")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> RecruiterUpdate(Guid id, RecordRecruiterUpdate command, CancellationToken ct)
    { var value = await tracking.RecruiterUpdateAsync(Owner, id, command.ExpectedRevision, command.Confirmed, command.Status, new(command.OccurredAtUtc, command.Channel, command.Message), ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/documents")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Attach(Guid id, AttachCandidacyDocument command, CancellationToken ct)
    { var value = await review.AttachAsync(Owner, id, command.ExpectedRevision, command.ExportId, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/review")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Review(Guid id, ReviewCandidacy command, CancellationToken ct)
    { var value = await review.RequestReviewAsync(Owner, id, command.ExpectedRevision, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/approve")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Approve(Guid id, ReviewCandidacy command, CancellationToken ct)
    { var value = await review.ApproveAsync(Owner, id, command.ExpectedRevision, command.Confirmed, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost("{id:guid}/return-to-draft")]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Return(Guid id, ReviewCandidacy command, CancellationToken ct)
    { var value = await review.ReturnToDraftAsync(Owner, id, command.ExpectedRevision, ct); return value is null ? NotFound() : Ok(value); }
    [HttpPost]
    [RequestSizeLimit(4096)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Create(CreateCandidacyCommand command, CancellationToken ct)
    {
        var result = await create.CreateAsync(Owner, command, ct);
        Response.Headers.Location = Url.Action(nameof(Get), new { id = result.Id });
        return Ok(result); // Same representation and status for creation and idempotent replay.
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    { var result = await tracking.GetAsync(Owner, id, ct); return result is null ? NotFound() : Ok(result); }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] CandidacyStatus? status = null, CancellationToken ct = default) => Ok(await tracking.ListAsync(Owner, page, pageSize, status, ct));
    [HttpPost("{id:guid}/notes")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Note(Guid id, AddCandidacyNote command, CancellationToken ct)
    { var result = await tracking.NoteAsync(Owner, id, command.ExpectedRevision, command.Note, ct); return result is null ? NotFound() : Ok(result); }
    [HttpPost("{id:guid}/withdraw")]
    [RequestSizeLimit(16384)]
    [EnableRateLimiting("application-write")]
    public async Task<IActionResult> Withdraw(Guid id, WithdrawCandidacy command, CancellationToken ct)
    { var result = await tracking.WithdrawAsync(Owner, id, command.ExpectedRevision, command.Confirmed, command.Reason, ct); return result is null ? NotFound() : Ok(result); }
}
