namespace JobApplicationAgent.Profile.Application.Profiles.Links.Update;

public sealed record UpdateLinkCommand(
    string Name,
    string Url);