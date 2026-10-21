using System.Text.Json.Serialization;
using JobApplicationAgent.Applications.Api;
using JobApplicationAgent.Applications.Application;
using JobApplicationAgent.Applications.Infrastructure;
using JobApplicationAgent.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(p => LocalIdentity.From(p.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
builder.Services.AddSingleton(p => ApplicationSourceBinding.From(p.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<ISubmissionPlatformCatalog>(p => SubmissionPlatformConfiguration.From(p.GetRequiredService<IConfiguration>()));
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
builder.Services.AddAuthentication(options => options.DefaultScheme = "ApplicationsAuth")
    .AddScheme<AuthenticationSchemeOptions, LocalAuthentication>("LocalBearer", _ => { })
    .AddPolicyScheme("ApplicationsAuth", null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : string.Empty;
            return hasOpenIddictIssuer && token.Count(character => character == '.') == 2
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : "LocalBearer";
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
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<ApplicationsDbContext>((p, o) => o.UseNpgsql(p.GetRequiredService<IConfiguration>().GetConnectionString("ApplicationsDatabase")));
builder.Services.AddHealthChecks().AddPostgresReadiness<ApplicationsDbContext>();
builder.Services.AddScoped<CandidacyRepository>();
builder.Services.AddScoped<ICandidacyCreationRepository>(p => p.GetRequiredService<CandidacyRepository>());
builder.Services.AddScoped<ICandidacyTrackingRepository>(p => p.GetRequiredService<CandidacyRepository>());
builder.Services.AddScoped<CreateCandidacyService>();
builder.Services.AddScoped<CandidacyTrackingService>();
builder.Services.AddScoped<ICandidacyFollowUpQuery>(p => p.GetRequiredService<CandidacyRepository>());
builder.Services.AddScoped<FollowUpService>();
builder.Services.AddScoped<CandidacyReviewService>();
builder.Services.AddTransient<UserAccessTokenForwarder>();
builder.Services.AddHttpClient<IApplicationDocumentClient, ApplicationDocumentClient>(c => c.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHttpClient<IApplicationSources, LocalApplicationSources>(c => c.Timeout = TimeSpan.FromSeconds(10))
    .AddHttpMessageHandler<UserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddFixedWindowLimiter("application-write", p => { p.PermitLimit = 60; p.Window = TimeSpan.FromMinutes(1); p.QueueLimit = 0; });
});
var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
_ = app.Services.GetRequiredService<LocalIdentity>();
_ = app.Services.GetRequiredService<ApplicationSourceBinding>();
_ = app.Services.GetRequiredService<ISubmissionPlatformCatalog>();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("ApplicationsDatabase")))
    throw new InvalidOperationException("ConnectionStrings:ApplicationsDatabase is required.");
app.UseExceptionHandler();
app.Use(async (c, next) => { c.Response.Headers.CacheControl = "no-store"; c.Response.Headers["X-Content-Type-Options"] = "nosniff"; await next(c); });
app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers(); app.MapServiceHealthChecks(); app.Run();
public partial class Program;

public sealed class ApplicationExceptionHandler(IProblemDetailsService problems, ILogger<ApplicationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            ApplicationSourceUnavailableException => (503, "Local sources unavailable or invalid. Retry with the same request ID."),
            ApplicationSourceRejectedException => (422, "Source or configured owner/profile/document binding is unavailable."),
            UnauthorizedAccessException => (403, "Owner required."),
            ArgumentException => (400, "Invalid candidature request."),
            InvalidOperationException => (409, "Request identity, dossier revision or status conflicts with this operation."),
            _ => (500, "Candidature request failed.")
        };
        if (status == 500) logger.LogError(exception, "Candidature request failed");
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = new() { Status = status, Title = title } });
    }
}
