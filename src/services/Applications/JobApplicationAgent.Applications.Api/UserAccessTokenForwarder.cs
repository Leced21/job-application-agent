using System.Net.Http.Headers;

namespace JobApplicationAgent.Applications.Api;

public sealed class UserAccessTokenForwarder(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri?.AbsolutePath;
        var authorization = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (path is not null && (path.StartsWith("/api/v1/profile", StringComparison.Ordinal) ||
                path.StartsWith("/api/v1/documents/application-bindings", StringComparison.Ordinal)) &&
            request.Headers.Authorization is null &&
            AuthenticationHeaderValue.TryParse(authorization, out var bearer) &&
            string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(bearer.Parameter))
            request.Headers.Authorization = bearer;

        return base.SendAsync(request, cancellationToken);
    }
}
