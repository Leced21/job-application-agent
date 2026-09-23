namespace JobApplicationAgent.Profile.Application.Profiles.Links;

public sealed record LinkDto(
    Guid Id,
    string Name,
    string Url,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);