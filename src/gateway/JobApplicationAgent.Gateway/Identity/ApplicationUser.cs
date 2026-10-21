using Microsoft.AspNetCore.Identity;

namespace JobApplicationAgent.Gateway.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public Guid? OwnerId { get; set; }
}
