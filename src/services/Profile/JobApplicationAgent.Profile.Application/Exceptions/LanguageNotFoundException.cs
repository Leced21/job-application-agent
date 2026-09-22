namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class LanguageNotFoundException(Guid languageId)
    : Exception($"Language '{languageId}' was not found.");