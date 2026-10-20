using JobApplicationAgent.Matching.Infrastructure;
using JobApplicationAgent.Matching.Application;
using JobApplicationAgent.Matching.Application.Matching;
using JobApplicationAgent.Matching.Application.Scoring;
using JobApplicationAgent.Matching.Infrastructure.Events;
using JobApplicationAgent.Matching.Api;
using Microsoft.AspNetCore.Authentication;
using OpenIddict.Validation.AspNetCore;
using JobApplicationAgent.Observability;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddApplication();
var recommendationLimits = builder.Configuration.GetSection("Recommendations:Processing")
    .Get<RecommendationProcessingLimits>() ?? new RecommendationProcessingLimits();
recommendationLimits.Validate();
builder.Services.AddSingleton(recommendationLimits);
var scoringRules = builder.Configuration.GetSection("Matching:Scoring").Get<ScoringRules>() ?? new ScoringRules();
scoringRules.Validate();
builder.Services.AddSingleton(scoringRules);
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
builder.Services.AddAuthentication(options => options.DefaultScheme = "MatchingAuth")
    .AddScheme<AuthenticationSchemeOptions, RejectMatchingAuthentication>("MatchingUnavailable", _ => { })
    .AddPolicyScheme("MatchingAuth", null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var header = context.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : string.Empty;
            return hasOpenIddictIssuer && token.Count(character => character == '.') == 2
                ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
                : "MatchingUnavailable";
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
builder.Services.AddProblemDetails();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddPostgresReadiness<JobApplicationAgent.Matching.Infrastructure.Persistence.MatchingDbContext>()
    .AddRabbitMqReadiness(builder.Configuration, "Matching:EventTransport:Enabled", "Matching:EventTransport:RabbitMq:Uri", "Matching.HealthCheck");
builder.Services.AddHostedService<JobPublishedEventConsumerWorker>();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapServiceHealthChecks();
app.Run();
