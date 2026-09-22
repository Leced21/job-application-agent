namespace JobApplicationAgent.Profile.Application.Profiles.Links.Add;

public sealed record AddLinkCommand(
    string Name,
    string Url);