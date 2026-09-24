namespace JobApplicationAgent.Job.Application.Exceptions;
public sealed class JobNotFoundException(Guid id) : Exception($"Job offer '{id}' was not found.");
