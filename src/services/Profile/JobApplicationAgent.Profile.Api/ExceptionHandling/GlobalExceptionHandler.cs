using FluentValidation;
using JobApplicationAgent.Profile.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JobApplicationAgent.Profile.Api.ExceptionHandling
{
    public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            logger.LogError(
                exception, "Unhandled exception while processing {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path
            );
            var problemDetails = exception switch
            {
                ValidationException validationException => CreateValidationProblem(validationException),

                CandidateProfileAlreadyExistsException => new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Candidate profile already exists",
                    Detail = exception.Message
                },
                CandidateProfileNotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Candidate profile not found",
                    Detail = exception.Message
                },
                ProfessionalExperienceNotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Professional experience not found",
                    Detail = exception.Message
                },
                EducationNotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Education not found",
                    Detail = exception.Message
                },
                SkillNotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Skill not found",
                    Detail = exception.Message
                },
                LanguageNotFoundException => new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Language not found",
                    Detail = exception.Message
                },
                _ => new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = "An unexpected error occurred while processing the request."
                }

            };

            httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception
            });
        }

        private static ProblemDetails CreateValidationProblem(ValidationException exception)
        {
            var errors = exception.Errors
                            .GroupBy(error => error.PropertyName)
                            .ToDictionary(
                                group => group.Key,
                                group => group
                                    .Select(error => error.ErrorMessage)
                                    .ToArray());

            return new HttpValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed"
            };
        }
    }
}
