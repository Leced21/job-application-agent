namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class LinkNotFoundException(Guid linkId)
    : Exception($"Link '{linkId}' was not found.");