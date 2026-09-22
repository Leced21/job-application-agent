namespace JobApplicationAgent.Profile.Application.Exceptions;

public sealed class CertificationNotFoundException(Guid certificationId)
    : Exception($"Certification '{certificationId}' was not found.");