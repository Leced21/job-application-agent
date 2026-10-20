using System.Security.Claims;

namespace JobApplicationAgent.Applications.Api;

public static class ApplicationsOwner
{
    public static Guid From(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("jobagent_owner_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var ownerId) && ownerId != Guid.Empty
            ? ownerId
            : throw new InvalidOperationException("The authenticated principal has no valid owner identifier.");
    }
}
