namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class SkillNotFoundException : Exception
{
    public SkillNotFoundException(Guid skillId)
        : base($"Skill '{skillId}' does not exist.")
    {
    }
}