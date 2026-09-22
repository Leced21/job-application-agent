namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Add;

public sealed record AddLanguageCommand(
    string Name,
    string ProficiencyLevel);