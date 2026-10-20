using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using JobApplicationAgent.Agents.Api;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;
using JobApplicationAgent.Agents.Infrastructure;
using JobApplicationAgent.Observability;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 65536);
builder.Services.AddOpenApi();
builder.Services.AddSingleton(s => LocalIdentity.From(s.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
builder.Services.AddHttpContextAccessor();
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
builder.Services.AddAuthentication(options => options.DefaultScheme = "AgentsAuth")
    .AddScheme<AuthenticationSchemeOptions, LocalAuthentication>("Local", _ => { })
    .AddPolicyScheme("AgentsAuth", null, options =>
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
builder.Services.AddAuthorization();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(o => {
    o.RejectionStatusCode = 429;
    o.AddPolicy("runs", context => RateLimitPartition.GetFixedWindowLimiter(
        AgentsOwner.Value(context.User) ?? "anonymous", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.AddPolicy("reads", context => RateLimitPartition.GetFixedWindowLimiter(
        AgentsOwner.Value(context.User) ?? "anonymous", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddDbContextFactory<AgentsDbContext>((s, o) => o.UseNpgsql(s.GetRequiredService<IConfiguration>().GetConnectionString("AgentsDatabase")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:AgentsDatabase outside Git.")));
builder.Services.AddHealthChecks().AddPostgresReadiness<AgentsDbContext>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(s => {
    var quota = s.GetRequiredService<IConfiguration>().GetSection("Agents:Quota").Get<AgentQuota>()
        ?? throw new InvalidOperationException("Configure Agents:Quota:MaxTokens and MaxUsd.");
    quota.Validate(); return quota;
});
builder.Services.AddSingleton(s => OllamaOptions.From(s.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(s => s.GetRequiredService<OllamaOptions>().Policy);
builder.Services.AddSingleton<IOllamaPromptTokenizer>(s => OllamaPromptTokenizer.Load(s.GetRequiredService<OllamaOptions>()));
builder.Services.AddHttpClient("Ollama", (s, http) => {
    var model = s.GetRequiredService<OllamaOptions>();
    http.BaseAddress = OllamaOptions.ModelOrigin(model.BaseUrl); http.Timeout = model.Policy.Timeout;
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
    .RemoveAllLoggers();
builder.Services.AddScoped<IAgentModel>(s => {
    var options = s.GetRequiredService<OllamaOptions>();
    return options.Enabled ? new OllamaAgentModel(s.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama"),
        s.GetRequiredService<IOllamaPromptTokenizer>(), options) : new DisabledAgentModel();
});
builder.Services.AddScoped<PostgresAgentRuns>();
builder.Services.AddScoped<IAgentRunRepository>(s => s.GetRequiredService<PostgresAgentRuns>());
builder.Services.AddHttpClient("JobSource", (s, http) => {
    http.BaseAddress = LocalJobSource.ValidateOrigin(s.GetRequiredService<IConfiguration>()["Agents:JobBaseUrl"] ?? "http://localhost:5002");
    http.Timeout = TimeSpan.FromSeconds(10);
})
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddScoped<LocalJobSource>(s => new LocalJobSource(s.GetRequiredService<IHttpClientFactory>().CreateClient("JobSource")));
builder.Services.AddSingleton(s => ProfileAnalysisOptions.From(s.GetRequiredService<IConfiguration>()));
// Profile accepts only the current user's validated OpenIddict token for these private reads.
builder.Services.AddHttpClient("ProfileSource", (s, http) => {
    http.BaseAddress = OllamaOptions.Origin(s.GetRequiredService<ProfileAnalysisOptions>().BaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<AgentUserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddScoped<LocalProfileSource>(s => new(s.GetRequiredService<IHttpClientFactory>().CreateClient("ProfileSource"),
    s.GetRequiredService<ProfileAnalysisOptions>()));
builder.Services.AddSingleton(s => MatchingEnrichmentOptions.From(s.GetRequiredService<IConfiguration>()));
builder.Services.AddHttpClient("MatchingSource", (s, http) => {
    http.BaseAddress = OllamaOptions.Origin(s.GetRequiredService<MatchingEnrichmentOptions>().BaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<AgentUserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddScoped<LocalMatchingSource>(s => new(s.GetRequiredService<IHttpClientFactory>().CreateClient("MatchingSource"),
    s.GetRequiredService<MatchingEnrichmentOptions>()));
builder.Services.AddSingleton(s => ResumeAdaptationOptions.From(s.GetRequiredService<IConfiguration>()));
builder.Services.AddHttpClient("ResumeProfileSource", (s, http) => {
    http.BaseAddress = OllamaOptions.Origin(s.GetRequiredService<ResumeAdaptationOptions>().ProfileBaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<AgentUserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddHttpClient("ResumeJobSource", (s, http) => {
    http.BaseAddress = LocalJobSource.ValidateOrigin(s.GetRequiredService<ResumeAdaptationOptions>().JobBaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddScoped<LocalResumeSource>(s => new(s.GetRequiredService<IHttpClientFactory>().CreateClient("ResumeProfileSource"),
    s.GetRequiredService<IHttpClientFactory>().CreateClient("ResumeJobSource"), s.GetRequiredService<IAgentRunRepository>(),
    s.GetRequiredService<ResumeAdaptationOptions>()));
builder.Services.AddSingleton(s => CoverLetterOptions.From(s.GetRequiredService<IConfiguration>()));
builder.Services.AddHttpClient("CoverLetterProfileSource", (s, http) => {
    http.BaseAddress = OllamaOptions.Origin(s.GetRequiredService<CoverLetterOptions>().ProfileBaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<AgentUserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddHttpClient("CoverLetterJobSource", (s, http) => {
    http.BaseAddress = LocalJobSource.ValidateOrigin(s.GetRequiredService<CoverLetterOptions>().JobBaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddScoped<LocalCoverLetterSource>(s => new(s.GetRequiredService<IHttpClientFactory>().CreateClient("CoverLetterProfileSource"),
    s.GetRequiredService<IHttpClientFactory>().CreateClient("CoverLetterJobSource"), s.GetRequiredService<IAgentRunRepository>(),
    s.GetRequiredService<CoverLetterOptions>()));
builder.Services.AddSingleton(s => ApplicationPreparationOptions.From(s.GetRequiredService<IConfiguration>()));
builder.Services.AddHttpClient("ApplicationPreparationProfileSource", (s, http) => {
    http.BaseAddress = OllamaOptions.Origin(s.GetRequiredService<ApplicationPreparationOptions>().ProfileBaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<AgentUserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddHttpClient("ApplicationPreparationJobSource", (s, http) => {
    http.BaseAddress = LocalJobSource.ValidateOrigin(s.GetRequiredService<ApplicationPreparationOptions>().JobBaseUrl); http.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddScoped<LocalApplicationPreparationSource>(s => new(
    s.GetRequiredService<IHttpClientFactory>().CreateClient("ApplicationPreparationProfileSource"),
    s.GetRequiredService<IHttpClientFactory>().CreateClient("ApplicationPreparationJobSource"), s.GetRequiredService<IAgentRunRepository>(),
    s.GetRequiredService<ApplicationPreparationOptions>()));
builder.Services.AddSingleton(s => FollowUpSuggestionOptions.From(s.GetRequiredService<IConfiguration>()));
builder.Services.AddTransient<AgentUserAccessTokenForwarder>();
builder.Services.AddHttpClient("ApplicationsFollowUpSource", (s, http) => {
    http.BaseAddress = s.GetRequiredService<FollowUpSuggestionOptions>().ApplicationsOrigin; http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<AgentUserAccessTokenForwarder>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }).RemoveAllLoggers();
builder.Services.AddScoped<LocalFollowUpSuggestionSource>(s => new(
    s.GetRequiredService<IHttpClientFactory>().CreateClient("ApplicationsFollowUpSource"),
    s.GetRequiredService<FollowUpSuggestionOptions>(), s.GetRequiredService<TimeProvider>()));
builder.Services.AddScoped<IAgentSourceReader, LocalAgentSources>();
builder.Services.AddScoped<AgentOrchestrator>(); builder.Services.AddScoped<AgentReviewService>();
var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();
_ = app.Services.GetRequiredService<LocalIdentity>();
_ = app.Services.GetRequiredService<AgentQuota>();
_ = app.Services.GetRequiredService<IDbContextFactory<AgentsDbContext>>();
_ = app.Services.GetRequiredService<ProfileAnalysisOptions>();
_ = app.Services.GetRequiredService<MatchingEnrichmentOptions>();
_ = app.Services.GetRequiredService<ResumeAdaptationOptions>();
_ = app.Services.GetRequiredService<CoverLetterOptions>();
_ = app.Services.GetRequiredService<ApplicationPreparationOptions>();
_ = app.Services.GetRequiredService<FollowUpSuggestionOptions>();
if (app.Services.GetRequiredService<OllamaOptions>().Enabled)
    _ = app.Services.GetRequiredService<IOllamaPromptTokenizer>();
using (var validatedClient = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("JobSource")) { }
app.Use(async (context, next) => {
    context.Response.Headers.CacheControl = "no-store"; context.Response.Headers.XContentTypeOptions = "nosniff";
    try { await next(); }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        var code = exception switch {
            AgentRunInProgressException => 409, AgentPersistenceUnavailableException => 503,
            ArgumentException => 400, InvalidOperationException => 409,
            OperationCanceledException => context.RequestAborted.IsCancellationRequested ? 499 : 504, _ => 503 };
        context.Response.StatusCode = code;
        await context.Response.WriteAsJsonAsync(new { title = "Agent request could not be completed.", status = code }, CancellationToken.None);
    }
});
app.UseAuthentication(); app.UseAuthorization(); app.UseRateLimiter();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
var api = app.MapGroup("/api/v1/agents").RequireAuthorization();
static Guid Owner(ClaimsPrincipal user) => AgentsOwner.Id(user);
api.MapGet("/catalog", (OllamaOptions model, ProfileAnalysisOptions profile, MatchingEnrichmentOptions matching, ResumeAdaptationOptions resume,
    CoverLetterOptions coverLetter, ApplicationPreparationOptions applicationPreparation, FollowUpSuggestionOptions followUp) => Results.Ok(new { definitions = AgentCatalog.Definitions,
    matchingEnrichmentEnabled = matching.Enabled, matchingMessages = MatchingEnrichmentPrompt.Messages,
    resumeAdaptationEnabled = resume.Enabled,
    coverLetterEnabled = coverLetter.Enabled,
    applicationPreparationEnabled = applicationPreparation.Enabled,
    followUpSuggestionEnabled = followUp.Enabled,
    profileAnalysisEnabled = profile.Enabled, profileQuestions = ProfileInformationCatalog.Questions,
    modelAvailable = model.Enabled, provider = model.Provider, modelId = model.Enabled ? model.ModelId : null,
    availabilityVerified = false,
    tokenizerProfile = model.Enabled ? model.TokenizerProfile : null, tokenizerSha256 = model.Enabled ? model.TokenizerSha256 : null,
    inputLimit = model.Policy.MaxInputTokens, outputLimit = model.Policy.MaxOutputTokens })).RequireRateLimiting("reads");
api.MapGet("/budget", async (ClaimsPrincipal user, PostgresAgentRuns runs, CancellationToken ct) => Results.Ok(await runs.GetBudgetAsync(Owner(user), ct))).RequireRateLimiting("reads");
api.MapPost("/runs", async (AgentCommand command, ClaimsPrincipal user, AgentOrchestrator orchestrator, CancellationToken ct) =>
    Results.Ok(await orchestrator.ExecuteAsync(Owner(user), command, ct))).RequireRateLimiting("runs");
api.MapGet("/runs", async (ClaimsPrincipal user, PostgresAgentRuns runs, CancellationToken ct, int page = 1, int pageSize = 20) =>
    Results.Ok(await runs.ListAsync(Owner(user), page, pageSize, ct))).RequireRateLimiting("reads");
api.MapGet("/requests/{request:guid}", async (Guid request, ClaimsPrincipal user, PostgresAgentRuns runs, CancellationToken ct) =>
    await runs.GetByRequestAsync(Owner(user), request, ct) is { } run ? Results.Ok(run) : Results.NotFound()).RequireRateLimiting("reads");
api.MapGet("/runs/{id:guid}", async (Guid id, ClaimsPrincipal user, IAgentRunRepository runs, CancellationToken ct) =>
    await runs.GetAsync(Owner(user), id, ct) is { } run ? Results.Ok(run) : Results.NotFound()).RequireRateLimiting("reads");
api.MapPost("/runs/{id:guid}/review", async (Guid id, AgentReview review, ClaimsPrincipal user, AgentReviewService reviews, CancellationToken ct) =>
    await reviews.ReviewAsync(Owner(user), id, review, ct) is { } run ? Results.Ok(run) : Results.NotFound());
api.MapPost("/runs/{id:guid}/recover", async (Guid id, ClaimsPrincipal user, PostgresAgentRuns runs, CancellationToken ct) =>
    await runs.RecoverAsync(Owner(user), id, ct) is { } run ? Results.Ok(run) : Results.NotFound());
api.MapGet("/runs/{id:guid}/audit", async (Guid id, ClaimsPrincipal user, PostgresAgentRuns runs, CancellationToken ct) =>
    await runs.GetAsync(Owner(user), id, ct) is null ? Results.NotFound() : Results.Ok(await runs.GetAuditAsync(Owner(user), id, ct))).RequireRateLimiting("reads");
app.MapServiceHealthChecks();
app.Run();
public partial class Program { }
