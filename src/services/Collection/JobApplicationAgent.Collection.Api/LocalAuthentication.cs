using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Collection.Api;

public sealed record LocalIdentity(Guid OwnerId, byte[] TokenHash, bool Enabled)
{
    public static LocalIdentity From(IConfiguration config, bool allowLocalAuth)
    {
        if (!allowLocalAuth) return new(Guid.Empty, [], false);

        var token = config["Collection:LocalAuth:Token"];
        if (!Guid.TryParse(config["Collection:LocalAuth:OwnerId"], out var ownerId) || ownerId == Guid.Empty ||
            token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Configure Collection:LocalAuth:OwnerId and a strong Collection:LocalAuth:Token (32–512 characters) outside Git.");
        return new(ownerId, SHA256.HashData(Encoding.UTF8.GetBytes(token)), true);
    }
}

public sealed class LocalAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, LocalIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
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
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials."));

        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, identity.OwnerId.ToString("D")),
            new Claim("jobagent_owner_id", identity.OwnerId.ToString("D"))], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        await base.HandleChallengeAsync(properties);
    }
}
