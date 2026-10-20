using JobApplicationAgent.Job.Application.Jobs.Status;
using FluentValidation;
using JobApplicationAgent.Job.Application.Jobs.Create;
using JobApplicationAgent.Job.Application.Jobs.Delete;
using JobApplicationAgent.Job.Application.Jobs.Get;
using JobApplicationAgent.Job.Application.Jobs.Update;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace JobApplicationAgent.Job.Api.Controllers;

[ApiController]
[Route("api/v1/jobs")]
public sealed class JobsController : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "JobAdmin")]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobCommand command,
        [FromServices] CreateJobHandler handler,
        [FromServices] IValidator<CreateJobCommand> validator,
        CancellationToken cancellationToken)
    {
        var validationResult =
            await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors.Select(error => new
            {
                error.PropertyName,
                error.ErrorMessage
            }));
        }

        var job =
            await handler.HandleAsync(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = job.Id },
            job);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll(
        [FromServices] GetJobsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var jobs =
            await handler.HandleAsync(page, pageSize, cancellationToken);

        return Ok(jobs);
    }

    [HttpGet("recommendable")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecommendable(
        [FromServices] GetRecommendableJobsHandler handler,
        CancellationToken cancellationToken,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await handler.HandleAsync(page, pageSize, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] GetJobByIdHandler handler,
        CancellationToken cancellationToken)
    {
        var job =
            await handler.HandleAsync(id, cancellationToken);

        return job is null
            ? NotFound()
            : Ok(job);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "JobAdmin")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateJobCommand command,
        [FromServices] UpdateJobHandler handler,
        [FromServices] IValidator<UpdateJobCommand> validator,
        CancellationToken cancellationToken)
    {
        var validationResult =
            await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors.Select(error => new
            {
                error.PropertyName,
                error.ErrorMessage
            }));
        }

        var job =
            await handler.HandleAsync(id, command, cancellationToken);

        return job is null
            ? NotFound()
            : Ok(job);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "JobAdmin")]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeJobStatusCommand command,
        [FromServices] ChangeJobStatusHandler handler, CancellationToken cancellationToken)
    {
        return Ok(await handler.HandleAsync(id, command, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "JobAdmin")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] DeleteJobHandler handler,
        CancellationToken cancellationToken)
    {
        var deleted =
            await handler.HandleAsync(id, cancellationToken);

        return deleted
            ? NoContent()
            : NotFound();
    }
}
