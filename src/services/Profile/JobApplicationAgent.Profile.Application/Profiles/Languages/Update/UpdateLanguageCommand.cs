namespace JobApplicationAgent.Profile.Application.Profiles.Languages.Update;

public sealed record UpdateLanguageCommand(
    string Name,
    string ProficiencyLevel);