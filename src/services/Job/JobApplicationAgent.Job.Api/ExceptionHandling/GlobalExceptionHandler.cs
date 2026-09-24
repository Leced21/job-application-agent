using FluentValidation;
using JobApplicationAgent.Job.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
namespace JobApplicationAgent.Job.Api.ExceptionHandling;
public sealed class GlobalExceptionHandler(IProblemDetailsService service, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new HttpValidationProblemDetails(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray())) { Status = 400, Title = "Validation failed", Detail = validation.Message },
            JobNotFoundException => new ProblemDetails { Status = 404, Title = "Job offer not found" },
            _ => new ProblemDetails { Status = 500, Title = "An unexpected error occurred" }
        };
        if (problem.Status == 500) logger.LogError(exception, "Job request failed");
        context.Response.StatusCode = problem.Status!.Value;
        return await service.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
