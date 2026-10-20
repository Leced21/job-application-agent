using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Job.Api;

public sealed record JobLocalIdentity(Guid OwnerId, byte[] TokenHash, bool Enabled)
{
    public static JobLocalIdentity From(IConfiguration config, bool allowLocalAuth)
    {
        if (!allowLocalAuth) return new(Guid.Empty, [], false);
        var token = config["Job:LocalAuth:Token"];
        if (!Guid.TryParse(config["Job:LocalAuth:OwnerId"], out var ownerId) || ownerId == Guid.Empty ||
            token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Configure Job:LocalAuth:OwnerId and a strong Job:LocalAuth:Token (32–512 characters) outside Git.");
        return new(ownerId, SHA256.HashData(Encoding.UTF8.GetBytes(token)), true);
    }
}

public sealed class JobLocalAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, JobLocalIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
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
    { Response.Headers.WWWAuthenticate = "Bearer"; await base.HandleChallengeAsync(properties); }
}

public sealed record CollectionPublisherIdentity(byte[] TokenHash, bool Enabled)
{
    public static CollectionPublisherIdentity From(IConfiguration config)
    {
        var token = config["Job:CollectionPublisher:Token"];
        if (token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Configure a distinct strong Job:CollectionPublisher:Token (32–512 characters) outside Git.");
        return new(SHA256.HashData(Encoding.UTF8.GetBytes(token)), true);
    }
}

public sealed class CollectionPublisherAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, CollectionPublisherIdentity identity) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
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
            return Task.FromResult(AuthenticateResult.Fail("Invalid collection publisher credentials."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "collection-publisher")], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    { Response.Headers.WWWAuthenticate = "Bearer"; await base.HandleChallengeAsync(properties); }
}
