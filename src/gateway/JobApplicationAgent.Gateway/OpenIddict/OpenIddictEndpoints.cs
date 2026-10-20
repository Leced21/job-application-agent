using System.Security.Claims;
using JobApplicationAgent.Gateway.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace JobApplicationAgent.Gateway.OpenIddict;

public static class OpenIddictEndpoints
{
    public static IEndpointRouteBuilder MapOpenIddictEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], (Delegate)Authorize).AllowAnonymous();
        endpoints.MapPost("/connect/token", (Delegate)Exchange).AllowAnonymous();
        endpoints.MapMethods("/connect/userinfo", [HttpMethods.Get, HttpMethods.Post], (Delegate)UserInfo).AllowAnonymous();
        return endpoints;
    }

    private static async Task<IResult> Authorize(HttpContext context, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request could not be read.");
        var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!authentication.Succeeded || authentication.Principal is null)
        {
            return Results.Challenge(new AuthenticationProperties
            {
                RedirectUri = context.Request.GetEncodedUrl()
            }, [IdentityConstants.ApplicationScheme]);
        }

        var subject = authentication.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(subject))
            return Results.Forbid(authenticationSchemes: [IdentityConstants.ApplicationScheme]);

        var user = await users.FindByIdAsync(subject);
        if (user is null)
            return Results.Forbid(authenticationSchemes: [IdentityConstants.ApplicationScheme]);
        if (user.OwnerId is not { } ownerId || ownerId == Guid.Empty)
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidRequest,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The account has no configured data owner."
                }));

        var principal = await signIn.CreateUserPrincipalAsync(user);
        var scopes = request.GetScopes().ToArray();
        var includeApi = scopes.Contains("jobagent_api", StringComparer.Ordinal);
        var includeProfile = scopes.Contains(Scopes.Profile, StringComparer.Ordinal);
        var includeEmail = scopes.Contains(Scopes.Email, StringComparer.Ordinal);
        var includeRoles = scopes.Contains(Scopes.Roles, StringComparer.Ordinal);
        principal.SetClaim(Claims.Subject, subject);
        if (includeApi)
            principal.SetClaim("jobagent_owner_id", ownerId.ToString());
        if (includeProfile && !string.IsNullOrWhiteSpace(user.UserName))
            principal.SetClaim(Claims.Name, user.UserName);
        if (includeEmail && !string.IsNullOrWhiteSpace(user.Email))
            principal.SetClaim(Claims.Email, user.Email);
        if (includeRoles)
        {
            foreach (var role in await users.GetRolesAsync(user))
                principal.Identities.First().AddClaim(new Claim(Claims.Role, role));
        }
        principal.SetScopes(scopes);
        if (scopes.Contains("jobagent_api", StringComparer.Ordinal))
            principal.SetResources("jobagent_api");
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            "jobagent_owner_id" when includeApi => [Destinations.AccessToken],
            Claims.Name when includeProfile => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when includeEmail => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Role when includeRoles => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => []
        });
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> UserInfo(HttpContext context, UserManager<ApplicationUser> users)
    {
        var authentication = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var principal = authentication.Principal;
        var subject = principal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);
        if (!authentication.Succeeded || principal is null || user is null)
        {
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken
                }));
        }

        var response = UserInfoClaims.Create(principal, subject!, user.UserName, user.Email,
            await users.IsEmailConfirmedAsync(user), await users.GetRolesAsync(user));

        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        return Results.Json(response);
    }

    private static async Task<IResult> Exchange(HttpContext context, UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn, IConfiguration configuration)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request could not be read.");
        if (request.IsClientCredentialsGrantType())
        {
            var configuredClientId = configuration["OpenIddict:Bff:ClientId"];
            var ownerValue = request.GetParameter("jobagent_owner_id")?.ToString();
            if (string.IsNullOrWhiteSpace(configuredClientId) || request.ClientId != configuredClientId ||
                !Guid.TryParse(ownerValue, out var requestedOwnerId) || requestedOwnerId == Guid.Empty ||
                !request.GetScopes().Contains("jobagent_api", StringComparer.Ordinal))
                return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant
                    }));

            var owner = await users.Users.SingleOrDefaultAsync(user => user.OwnerId == requestedOwnerId);
            if (owner is null || await users.IsLockedOutAsync(owner))
                return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant
                    }));

            var apiPrincipal = GatewayApiTokenPrincipal.Create(await users.GetUserIdAsync(owner), requestedOwnerId);
            return Results.SignIn(apiPrincipal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }
        if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType
                }));

        var ticket = await context.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        var exchangePrincipal = ticket.Principal;
        var subject = exchangePrincipal?.GetClaim(Claims.Subject);
        var user = subject is null ? null : await users.FindByIdAsync(subject);
        if (user is null || exchangePrincipal is null)
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant
                }));
        if (user.OwnerId is not { } ownerId || ownerId == Guid.Empty)
            return Results.Forbid(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme],
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The account has no configured data owner."
                }));

        var principal = await signIn.CreateUserPrincipalAsync(user);
        principal.SetClaim(Claims.Subject, await users.GetUserIdAsync(user));
        var scopes = exchangePrincipal.GetScopes();
        var includeApi = scopes.Contains("jobagent_api", StringComparer.Ordinal);
        if (includeApi)
            principal.SetClaim("jobagent_owner_id", ownerId.ToString());
        var includeProfile = scopes.Contains(Scopes.Profile, StringComparer.Ordinal);
        var includeEmail = scopes.Contains(Scopes.Email, StringComparer.Ordinal);
        var includeRoles = scopes.Contains(Scopes.Roles, StringComparer.Ordinal);
        if (includeProfile && !string.IsNullOrWhiteSpace(user.UserName))
            principal.SetClaim(Claims.Name, user.UserName);
        if (includeEmail && !string.IsNullOrWhiteSpace(user.Email))
            principal.SetClaim(Claims.Email, user.Email);
        if (includeRoles)
        {
            foreach (var role in await users.GetRolesAsync(user))
                principal.Identities.First().AddClaim(new Claim(Claims.Role, role));
        }
        principal.SetScopes(scopes);
        principal.SetResources(exchangePrincipal.GetResources());
        principal.SetDestinations(claim => claim.Type switch
        {
            Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
            "jobagent_owner_id" when includeApi => [Destinations.AccessToken],
            Claims.Name when includeProfile => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Email when includeEmail => [Destinations.AccessToken, Destinations.IdentityToken],
            Claims.Role when includeRoles => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => []
        });
        return Results.SignIn(principal, authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}

public static class UserInfoClaims
{
    public static IReadOnlyDictionary<string, object> Create(ClaimsPrincipal principal, string subject,
        string? userName, string? email, bool emailConfirmed, IEnumerable<string> roles)
    {
        var response = new Dictionary<string, object> { [Claims.Subject] = subject };
        var scopes = principal.GetScopes();
        if (scopes.Contains(Scopes.Profile, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(userName))
            response[Claims.Name] = userName;
        if (scopes.Contains(Scopes.Email, StringComparer.Ordinal) && !string.IsNullOrWhiteSpace(email))
        {
            response[Claims.Email] = email;
            response[Claims.EmailVerified] = emailConfirmed;
        }
        var roleList = roles.ToArray();
        if (scopes.Contains(Scopes.Roles, StringComparer.Ordinal) && roleList.Length > 0)
            response[Claims.Role] = roleList;
        return response;
    }
}
