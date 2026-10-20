using System.Security.Claims;

namespace JobApplicationAgent.Documents.Api;

public static class DocumentsOwner
{
    public static Guid From(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("jobagent_owner_id") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var ownerId) && ownerId != Guid.Empty
            ? ownerId
            : throw new InvalidOperationException("The authenticated principal has no valid owner identifier.");
    }
}
