using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Matching.Infrastructure.HttpClients;

public sealed class MatchingUserTokenForwarder(IHttpContextAccessor accessor, IConfiguration configuration) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath;
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (path is not null && path.StartsWith("/api/v1/profile/", StringComparison.Ordinal) && request.Headers.Authorization is null)
        {
            if (AuthenticationHeaderValue.TryParse(authorization, out var bearer) &&
                string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(bearer.Parameter))
                request.Headers.Authorization = bearer;
            else if (accessor.HttpContext is null && configuration["Profile:LocalAuth:Token"] is { Length: >= 32 and <= 512 } serviceToken &&
                !serviceToken.Any(char.IsWhiteSpace))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceToken);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
