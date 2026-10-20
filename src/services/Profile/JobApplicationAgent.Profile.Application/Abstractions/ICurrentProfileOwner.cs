namespace JobApplicationAgent.Profile.Application.Abstractions;

public interface ICurrentProfileOwner
{
    Guid OwnerId { get; }
}
