using System.Security.Claims;

namespace JobApplicationAgent.Agents.Api;

public static class AgentsOwner
{
    public static string? Value(ClaimsPrincipal principal) =>
        principal.FindFirstValue("jobagent_owner_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static Guid Id(ClaimsPrincipal principal)
    {
        var value = Value(principal);
        return Guid.TryParse(value, out var ownerId) && ownerId != Guid.Empty
            ? ownerId
            : throw new InvalidOperationException("The authenticated principal has no valid owner identifier.");
    }
}
