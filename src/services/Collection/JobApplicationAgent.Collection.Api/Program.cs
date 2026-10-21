using FluentValidation;
using JobApplicationAgent.Collection.Application;
using JobApplicationAgent.Collection.Infrastructure;
using JobApplicationAgent.Collection.Infrastructure.Events;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using JobApplicationAgent.Observability;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
// Adzuna's API key is carried in the query string by its documented contract; suppress request-URI logs.
builder.Logging.AddFilter("System.Net.Http.HttpClient.Adzuna", LogLevel.None);
builder.Services.AddSingleton(provider => JobApplicationAgent.Collection.Api.LocalIdentity.From(
    provider.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
builder.Services.AddAuthentication(options => options.DefaultScheme = "CollectionAuth")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        JobApplicationAgent.Collection.Api.LocalAuthentication>("Local", _ => { })
    .AddPolicyScheme("CollectionAuth", null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : string.Empty;
            return hasOpenIddictIssuer && token.Count(character => character == '.') == 2
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : "Local";
        };
    });
if (hasOpenIddictIssuer)
{
    builder.Services.AddOpenIddict().AddValidation(options =>
    {
        options.SetIssuer(issuer!);
        options.AddAudiences("jobagent_api");
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });
}
builder.Services.AddAuthorization(options => options.AddPolicy("CollectionAdmin", policy => policy
    .RequireAuthenticatedUser()
    .RequireAssertion(context =>
    {
        var ownerClaim = context.User.FindFirstValue("jobagent_owner_id");
        if (!Guid.TryParse(ownerClaim, out var ownerId) || context.Resource is not HttpContext httpContext) return false;
        var allowedOwnerIds = httpContext.RequestServices.GetRequiredService<IConfiguration>()
            ["Collection:Admin:OwnerIds"] ?? string.Empty;
        return allowedOwnerIds
            .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(value => Guid.TryParse(value, out var allowedOwnerId) && allowedOwnerId == ownerId);
    })));
builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddHealthChecks()
    .AddPostgresReadiness<JobApplicationAgent.Collection.Infrastructure.Persistence.CollectionDbContext>()
    .AddRabbitMqReadiness(builder.Configuration, "Collection:EventTransport:Enabled", "Collection:EventTransport:RabbitMq:Uri", "Collection.HealthCheck");
builder.Services.AddHostedService<CollectionOutboxDispatchWorker>();
builder.Services.AddHostedService<JobApplicationAgent.Collection.Infrastructure.Connectors.AdzunaCollectionWorker>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<CollectionExceptionHandler>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("source-collection", policy =>
    {
        policy.PermitLimit = 10;
        policy.Window = TimeSpan.FromMinutes(1);
        policy.QueueLimit = 0;
        policy.AutoReplenishment = true;
    });
});
var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("CollectionDatabase")))
    throw new InvalidOperationException("ConnectionStrings:CollectionDatabase is required.");
app.UseExceptionHandler();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapServiceHealthChecks();
app.Run();

public partial class Program;

public sealed class CollectionExceptionHandler(IProblemDetailsService problems, ILogger<CollectionExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails problem;
        if (exception is ValidationException validation)
        {
            problem = new HttpValidationProblemDetails(validation.Errors.Any()
                ? validation.Errors.GroupBy(error => error.PropertyName).ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())
                : new Dictionary<string, string[]> { ["request"] = [validation.Message] })
            { Status = 400, Title = "Invalid collection request" };
        }
        else if (exception is JobApplicationAgent.Collection.Domain.Entities.RevisionConflictException conflict)
        {
            problem = new ProblemDetails { Status = 409, Title = "Revision review conflict", Detail = conflict.Message };
        }
        else if (exception is JobApplicationAgent.Collection.Application.Imports.PublicationDependencyException dependency)
        {
            problem = new ProblemDetails { Status = dependency.StatusCode, Title = dependency.Message };
        }
        else if (exception is JobApplicationAgent.Collection.Application.Connectors.CollectionSourceNotFoundException)
        {
            problem = new ProblemDetails { Status = 404, Title = "Collection source not found" };
        }
        else if (exception is JobApplicationAgent.Collection.Application.Connectors.CollectionSourceTimeoutException)
        {
            problem = new ProblemDetails { Status = 503, Title = "Collection deadline exceeded; retry the same batch" };
        }
        else if (exception is JobApplicationAgent.Collection.Application.Connectors.CollectionSourceUnavailableException unavailable)
        {
            problem = new ProblemDetails { Status = 503, Title = unavailable.Message };
        }
        else
        {
            logger.LogError(exception, "Collection request failed");
            problem = new ProblemDetails { Status = 500, Title = "Collection request failed" };
        }
        context.Response.StatusCode = problem.Status!.Value;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
