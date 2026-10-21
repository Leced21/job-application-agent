using System.Text.Json.Serialization;
using JobApplicationAgent.Job.Api.ExceptionHandling;
using JobApplicationAgent.Job.Application;
using JobApplicationAgent.Job.Infrastructure;
using JobApplicationAgent.Job.Infrastructure.Events;
using JobApplicationAgent.Observability;
using OpenIddict.Validation.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(provider => JobApplicationAgent.Job.Api.JobLocalIdentity.From(
    provider.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
builder.Services.AddSingleton(provider => JobApplicationAgent.Job.Api.CollectionPublisherIdentity.From(
    provider.GetRequiredService<IConfiguration>()));
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
builder.Services.AddAuthentication(options => options.DefaultScheme = "JobAuth")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, JobApplicationAgent.Job.Api.JobLocalAuthentication>("JobLocal", _ => { })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, JobApplicationAgent.Job.Api.CollectionPublisherAuthentication>("CollectionPublisher", _ => { })
    .AddPolicyScheme("JobAuth", null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : string.Empty;
            return hasOpenIddictIssuer && token.Count(character => character == '.') == 2
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : "JobLocal";
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
builder.Services.AddAuthorization(options => options.AddPolicy("JobAdmin", policy => policy
    .RequireAuthenticatedUser()
    .RequireAssertion(context =>
    {
        var ownerClaim = context.User.FindFirstValue("jobagent_owner_id");
        if (!Guid.TryParse(ownerClaim, out var ownerId)) return false;
        if (context.Resource is not HttpContext httpContext) return false;
        var configuration = httpContext.RequestServices.GetRequiredService<IConfiguration>();
        return (configuration["Job:Admin:OwnerIds"] ?? string.Empty)
            .Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Any(value => Guid.TryParse(value, out var allowedOwnerId) && allowedOwnerId == ownerId);
    })));

builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddPostgresReadiness<JobApplicationAgent.Job.Infrastructure.Persistence.JobDbContext>()
    .AddRabbitMqReadiness(builder.Configuration, "Job:EventTransport:Enabled", "Job:EventTransport:RabbitMq:Uri", "Job.HealthCheck");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHostedService<JobOutboxDispatchWorker>();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapServiceHealthChecks();

app.Run();

public partial class Program;
