using JobApplicationAgent.Documents.Api;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Infrastructure;
using JobApplicationAgent.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DocumentExceptionHandler>();
builder.Services.AddSingleton(provider => LocalIdentity.From(provider.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
builder.Services.AddAuthentication(options => options.DefaultScheme = "DocumentsAuth")
    .AddScheme<AuthenticationSchemeOptions, LocalAuthentication>("LocalBearer", _ => { })
    .AddPolicyScheme("DocumentsAuth", null, options =>
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
builder.Services.AddSingleton(p => ApplicationBindingChannel.From(p.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(p => p.GetRequiredService<ApplicationBindingChannel>().Scope);
builder.Services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, ApplicationBindingAuthentication>("ApplicationsChannel", _ => { });
builder.Services.AddScoped<DocumentApplicationBindingService>();
builder.Services.AddScoped<IDocumentApplicationBindingRepository, DocumentApplicationBindingRepository>();
builder.Services.AddAuthorization();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ResumeService>();
builder.Services.AddScoped<PreparedDraftService>();
builder.Services.AddScoped<DocumentCompositionService>();
builder.Services.AddScoped<DocumentExportService>();
builder.Services.AddScoped<IDocumentExportRepository, DocumentExportRepository>();
builder.Services.AddSingleton<IApprovedDocumentRenderer, ApprovedDocumentRenderer>();
builder.Services.AddScoped<IDocumentCompositionRepository, DocumentCompositionRepository>();
builder.Services.AddScoped<IPreparedDraftRepository, PreparedDraftRepository>();
builder.Services.AddHttpClient<IDraftJobClient, DraftJobClient>(client => client.Timeout = TimeSpan.FromSeconds(10))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ResumeSeriesService>();
builder.Services.AddScoped<IResumeSeriesRepository, ResumeSeriesRepository>();
builder.Services.AddScoped<ResumeExtractionService>();
builder.Services.AddScoped<ResumeCandidateFactService>();
builder.Services.AddScoped<ProfileFactImportService>();
builder.Services.AddScoped<ProfileFactMappingService>();
builder.Services.AddScoped<IProfileFactMappingRepository, ResumeCandidateFactRepository>();
builder.Services.AddHttpClient<IProfileFactImportClient, ProfileFactImportClient>(client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IResumeCandidateFactRepository, ResumeCandidateFactRepository>();
builder.Services.AddScoped<IResumeExtractionRepository, ResumeExtractionRepository>();
builder.Services.AddSingleton<IResumeTextExtractor, ProcessResumeTextExtractor>();
builder.Services.AddScoped<IResumeRepository, ResumeRepository>();
builder.Services.AddDbContext<DocumentsDbContext>((provider, options) => options.UseNpgsql(
    provider.GetRequiredService<IConfiguration>().GetConnectionString("DocumentsDatabase")));
builder.Services.AddHealthChecks().AddPostgresReadiness<DocumentsDbContext>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 6 * 1024 * 1024;
    options.MemoryBufferThreshold = 6 * 1024 * 1024;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("document-export", policy =>
    { policy.PermitLimit = 10; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
    options.AddFixedWindowLimiter("composition-write", policy =>
    { policy.PermitLimit = 30; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
    options.AddFixedWindowLimiter("draft-prepare", policy =>
    { policy.PermitLimit = 10; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
    options.AddFixedWindowLimiter("resume-series-write", policy =>
    { policy.PermitLimit = 30; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
    options.AddFixedWindowLimiter("resume-upload", policy =>
    { policy.PermitLimit = 10; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
    options.AddFixedWindowLimiter("resume-extraction", policy =>
    { policy.PermitLimit = 10; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
    options.AddFixedWindowLimiter("resume-fact-write", policy =>
    { policy.PermitLimit = 30; policy.Window = TimeSpan.FromMinutes(1); policy.QueueLimit = 0; policy.AutoReplenishment = true; });
});
var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
_ = app.Services.GetRequiredService<LocalIdentity>();
_ = app.Services.GetRequiredService<ApplicationBindingChannel>();
if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("DocumentsDatabase")))
    throw new InvalidOperationException("ConnectionStrings:DocumentsDatabase is required.");
app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next(context);
});
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapControllers();
app.MapServiceHealthChecks();
app.Run();
public partial class Program;

public sealed class DocumentExceptionHandler(IProblemDetailsService problems, ILogger<DocumentExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var invalid = exception as InvalidResumeException;
        var problem = exception switch
        {
            DocumentExportUnavailableException => new ProblemDetails { Status = 503, Title = "The export renderer is busy. Retry the same format and content hash." },
            JobApplicationAgent.Documents.Domain.DraftConflictException => new ProblemDetails { Status = 409, Title = exception.Message },
            DraftRejectedException => new ProblemDetails { Status = 422, Title = exception.Message },
            DraftJobUnavailableException => new ProblemDetails { Status = 503, Title = "The local Job service is unavailable or returned an invalid response. Retry with the same request ID." },
            JobApplicationAgent.Documents.Domain.ResumeSeriesConflictException => new ProblemDetails { Status = 409, Title = exception.Message },
            ProfileFactImportRejectedException => new ProblemDetails { Status = 422, Title = exception.Message },
            ProfileFactImportUnavailableException => new ProblemDetails { Status = 503, Title = "Profile import is unavailable; retry the same fact after checking configuration or connectivity." },
            JobApplicationAgent.Documents.Domain.FactReviewConflictException => new ProblemDetails { Status = 409, Title = exception.Message },
            TextExtractionRejectedException => new ProblemDetails { Status = 422, Title = exception.Message },
            TextExtractionUnavailableException => new ProblemDetails { Status = 503, Title = exception.Message },
            _ => new ProblemDetails { Status = invalid is null ? 500 : 400, Title = invalid?.Message ?? "Document request failed" }
        };
        if (problem.Status == 500) logger.LogError(exception, "Document request failed");
        context.Response.StatusCode = problem.Status ?? 500;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
    }
}
