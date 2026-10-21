using JobApplicationAgent.Gateway.Identity;
using JobApplicationAgent.Gateway.OpenIddict;
using JobApplicationAgent.Gateway;
using JobApplicationAgent.Observability;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using System.Threading.RateLimiting;
using System.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;
using JobApplicationAgent.Gateway.Security;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(transformBuilder =>
    {
        var clusterId = transformBuilder.Route.ClusterId;
        transformBuilder.AddRequestTransform(async transformContext =>
        {
            if (transformContext.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var correlationId) &&
                correlationId is string value)
            {
                transformContext.ProxyRequest.Headers.Remove(CorrelationIdMiddleware.HeaderName);
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, value);
            }
            // Browser credentials are never forwarded to microservices.
            transformContext.ProxyRequest.Headers.Remove("Cookie");
            transformContext.ProxyRequest.Headers.Remove("Authorization");
            var tokenProvider = transformContext.HttpContext.RequestServices.GetService<GatewayAccessTokenProvider>();
            var token = tokenProvider is not null && clusterId is ("profile-cluster" or "documents-cluster" or "applications-cluster" or "agents-cluster" or "matching-cluster" or "collection-cluster" or "job-cluster")
                ? await tokenProvider.GetAccessTokenAsync(transformContext.HttpContext.RequestAborted)
                : null;
            if (!string.IsNullOrWhiteSpace(token))
                transformContext.ProxyRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        });
    });
var healthChecks = builder.Services.AddHealthChecks();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "JobAgent.Antiforgery"
        : "__Host-JobAgent.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});
builder.Services.AddRateLimiter(options => options.AddPolicy("identity-auth", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        })));

var identityConnectionString = builder.Configuration.GetConnectionString("IdentityDatabase");
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(identityConnectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:IdentityDatabase before running the Gateway outside Development.");

using var openIddictCertificates = !string.IsNullOrWhiteSpace(identityConnectionString) && !builder.Environment.IsDevelopment()
    ? GatewayOpenIddictCertificateLoader.Load(builder.Configuration, builder.Environment.ContentRootPath)
    : null;
if (!string.IsNullOrWhiteSpace(identityConnectionString))
{
    builder.Services.AddScoped<GatewayAccessTokenProvider>();
    var issuerValue = builder.Configuration["OpenIddict:Issuer"];
    if (!Uri.TryCreate(issuerValue, UriKind.Absolute, out var issuer) ||
        (issuer.Scheme != Uri.UriSchemeHttps && !issuer.IsLoopback &&
            !(builder.Environment.IsDevelopment() && issuer.Scheme == Uri.UriSchemeHttp && issuer.Host == "gateway-api" && issuer.Port == 5000)))
        throw new InvalidOperationException("Configure OpenIddict:Issuer with HTTPS (HTTP is limited to loopback development URLs).");

    builder.Services.AddDbContext<GatewayIdentityDbContext>(options => options.UseNpgsql(identityConnectionString));
    healthChecks.AddPostgresReadiness<GatewayIdentityDbContext>("identity-postgres");
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
            options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        })
        .AddIdentityCookies();
    builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = builder.Environment.IsDevelopment()
            ? "JobAgent.Session"
            : "__Host-JobAgent.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = cookieSecurePolicy;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.Path = "/";
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
    builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequireDigit = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddSignInManager()
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<GatewayIdentityDbContext>();
    builder.Services.AddOpenIddict()
        .AddCore(options => options.UseEntityFrameworkCore()
            .UseDbContext<GatewayIdentityDbContext>())
        .AddServer(options =>
        {
            options.SetIssuer(issuer);
            options.SetAuthorizationEndpointUris("connect/authorize");
            options.SetTokenEndpointUris("connect/token");
            options.SetUserInfoEndpointUris("connect/userinfo");
            options.AllowAuthorizationCodeFlow()
                .AllowClientCredentialsFlow()
                .AllowRefreshTokenFlow()
                .RequireProofKeyForCodeExchange();
            options.RegisterScopes(Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, "jobagent_api");
            options.DisableAccessTokenEncryption();
            options.SetAccessTokenLifetime(TimeSpan.FromMinutes(5));
            if (openIddictCertificates is null)
                options.AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey();
            else
            {
                foreach (var certificate in openIddictCertificates.EncryptionCertificates)
                    options.AddEncryptionCertificate(certificate);
                foreach (var certificate in openIddictCertificates.SigningCertificates)
                    options.AddSigningCertificate(certificate);
            }
            var aspNetCore = options.UseAspNetCore();
            if (builder.Environment.IsDevelopment())
                aspNetCore.DisableTransportSecurityRequirement();
            aspNetCore.EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserInfoEndpointPassthrough();
        })
        .AddClient(options =>
        {
            options.AllowClientCredentialsFlow();
            options.DisableTokenStorage();
            options.UseSystemNetHttp();

            var clientId = builder.Configuration["OpenIddict:Bff:ClientId"];
            var clientSecret = builder.Configuration["OpenIddict:Bff:ClientSecret"];
            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                options.AddRegistration(new OpenIddictClientRegistration
                {
                    Issuer = issuer,
                    ClientId = clientId,
                    ClientSecret = clientSecret
                });
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();
if (!string.IsNullOrWhiteSpace(identityConnectionString))
    await OpenIddictBffClientRegistration.EnsureAsync(app.Services, builder.Configuration);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<JobApplicationAgent.Gateway.SecurityHeadersMiddleware>();
if (!string.IsNullOrWhiteSpace(identityConnectionString))
{
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.Use(async (context, next) =>
    {
        var method = context.Request.Method;
        if (context.Request.Path.StartsWithSegments("/api") &&
            !HttpMethods.IsGet(method) && !HttpMethods.IsHead(method) &&
            !HttpMethods.IsOptions(method) && !HttpMethods.IsTrace(method))
        {
            try
            {
                await context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>()
                    .ValidateRequestAsync(context);
            }
            catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new { message = "Antiforgery validation failed." });
                return;
            }
        }
        await next();
    });
    app.UseAuthorization();
    app.MapOpenIddictEndpoints();
    app.MapAccountEndpoints(builder.Configuration, builder.Environment);
    app.MapReverseProxy().RequireAuthorization();
}
else
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new { message = "Gateway Identity is not configured." });
            return;
        }
        await next();
    });
    app.MapReverseProxy();
}
app.MapServiceHealthChecks();
app.Run();
