using JobApplicationAgent.Profile.Api.ExceptionHandling;
using JobApplicationAgent.Profile.Application;
using JobApplicationAgent.Profile.Infrastructure;
using OpenIddict.Validation.AspNetCore;
using JobApplicationAgent.Observability;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<JobApplicationAgent.Profile.Application.Abstractions.ICurrentProfileOwner, JobApplicationAgent.Profile.Api.CurrentProfileOwner>();
builder.Services.AddSingleton(provider => JobApplicationAgent.Profile.Api.DocumentImportIdentity.From(provider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(provider => JobApplicationAgent.Profile.Api.ProfileServiceIdentity.From(
    provider.GetRequiredService<IConfiguration>(),
    builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing")));
builder.Services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
    JobApplicationAgent.Profile.Api.DocumentImportAuthentication>("DocumentsImport", _ => { });
var authentication = builder.Services.AddAuthentication();
var issuerValue = builder.Configuration["OpenIddict:Issuer"];
var hasOpenIddictIssuer = Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) &&
    (issuer.Scheme == Uri.UriSchemeHttps || issuer.IsLoopback ||
        (builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000));
authentication.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
    JobApplicationAgent.Profile.Api.ProfileServiceAuthentication>("ProfileService", _ => { });
authentication.AddPolicyScheme("ProfileAuth", null, options =>
{
    options.ForwardDefaultSelector = context =>
    {
        var header = context.Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header[7..] : string.Empty;
        return hasOpenIddictIssuer && token.Count(character => character == '.') == 2
            ? OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme
            : "ProfileService";
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
builder.Services.AddScoped<JobApplicationAgent.Profile.Application.Profiles.DocumentFacts.ImportDocumentFactHandler>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks().AddPostgresReadiness<JobApplicationAgent.Profile.Infrastructure.Persistence.ProfileDbContext>();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceHealthChecks();

app.Run();

public partial class Program;
