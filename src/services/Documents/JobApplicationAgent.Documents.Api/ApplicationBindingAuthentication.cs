using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using JobApplicationAgent.Documents.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace JobApplicationAgent.Documents.Api;

public sealed record ApplicationBindingChannel(DocumentApplicationScope Scope, byte[] TokenHash)
{
    public static ApplicationBindingChannel From(IConfiguration config)
    {
        var token = config["Documents:Applications:Token"];
        var ownerValue = config["Documents:Applications:OwnerId"];
        var profileValue = config["Documents:Applications:ProfileId"];
        if (token is null && ownerValue is null && profileValue is null) return new(new(false, Guid.Empty, Guid.Empty), []);
        if (!Guid.TryParse(ownerValue, out var owner) || owner == Guid.Empty ||
            !Guid.TryParse(config["Documents:LocalAuth:OwnerId"], out var localOwner) || owner != localOwner || !Guid.TryParse(profileValue, out var profile) || profile == Guid.Empty ||
            token is null || token.Length is < 32 or > 512 || token.Any(char.IsWhiteSpace) || token == config["Documents:LocalAuth:Token"])
            throw new InvalidOperationException("Configure Documents:Applications owner/profile and a dedicated strong token, or leave all absent.");
        return new(new(true, owner, profile), SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
public sealed class ApplicationBindingAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, ApplicationBindingChannel channel) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var headers = Request.Headers.Authorization;
        if (!channel.Scope.Enabled || headers.Count != 1 || headers[0] is not { } header || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());
        var token = header[7..];
        if (token.Length is < 32 or > 512 || !CryptographicOperations.FixedTimeEquals(channel.TokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(token))))
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials."));
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, channel.Scope.OwnerId.ToString())], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    { Response.Headers.WWWAuthenticate = "Bearer"; await base.HandleChallengeAsync(properties); }
}
