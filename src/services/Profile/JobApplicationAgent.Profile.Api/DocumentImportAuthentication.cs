using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Profile.Api;

public sealed record DocumentImportIdentity(Guid OwnerId, Guid ProfileId, byte[] TokenHash, bool Enabled)
{
    public static DocumentImportIdentity From(IConfiguration config)
    {
        var token = config["Profile:DocumentImport:Token"];
        if (!Guid.TryParse(config["Profile:DocumentImport:OwnerId"], out var owner) || owner == Guid.Empty ||
            !Guid.TryParse(config["Profile:DocumentImport:ProfileId"], out var profile) || profile == Guid.Empty ||
            token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            return new(Guid.Empty, Guid.Empty, [], false);
        return new(owner, profile, SHA256.HashData(Encoding.UTF8.GetBytes(token)), true);
    }
}
public sealed class DocumentImportAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, DocumentImportIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (!identity.Enabled || headers.Count != 1 || headers[0] is not { } header || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var token = header[7..];
        if (token.Length is < 32 or > 512 || !CryptographicOperations.FixedTimeEquals(identity.TokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(token))))
            return Task.FromResult(AuthenticateResult.Fail("Invalid import credentials."));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, identity.OwnerId.ToString())], Scheme.Name)), Scheme.Name)));
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    { Response.Headers.WWWAuthenticate = "Bearer"; await base.HandleChallengeAsync(properties); }
}

public sealed record ProfileServiceIdentity(Guid OwnerId, byte[] TokenHash, bool Enabled)
{
    public static ProfileServiceIdentity From(IConfiguration config, bool allowLocalAuth)
    {
        if (!allowLocalAuth)
            return new(Guid.Empty, [], false);

        var token = config["Profile:LocalAuth:Token"];
        if (!Guid.TryParse(config["Profile:LocalAuth:OwnerId"], out var ownerId) || ownerId == Guid.Empty ||
            token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            return new(Guid.Empty, [], false);
        return new(ownerId, SHA256.HashData(Encoding.UTF8.GetBytes(token)), true);
    }
}

public sealed class CurrentProfileOwner(IHttpContextAccessor accessor) : JobApplicationAgent.Profile.Application.Abstractions.ICurrentProfileOwner
{
    public Guid OwnerId => Guid.TryParse(
        accessor.HttpContext?.User.FindFirstValue("jobagent_owner_id") ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var ownerId)
        ? ownerId
        : throw new InvalidOperationException("An authenticated profile owner is required.");
}

public sealed class ProfileServiceAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, ProfileServiceIdentity identity)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (!identity.Enabled || headers.Count != 1 || headers[0] is not { } header ||
            !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var token = header[7..];
        if (token.Length is < 32 or > 512 ||
            !CryptographicOperations.FixedTimeEquals(identity.TokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(token))))
            return Task.FromResult(AuthenticateResult.Fail("Invalid profile service credentials."));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, identity.OwnerId.ToString())], Scheme.Name)), Scheme.Name)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await base.HandleChallengeAsync(properties);
    }
}
