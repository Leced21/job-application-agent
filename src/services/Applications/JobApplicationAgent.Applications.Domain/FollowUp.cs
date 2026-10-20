namespace JobApplicationAgent.Applications.Domain;

public enum FollowUpStatus { Planned = 1, Completed = 2, Cancelled = 3 }
public sealed record FollowUpPlan(Guid Id, DateTime DueAtUtc, ContactChannel Channel, string Note);
public sealed record FollowUpChange(FollowUpPlan Plan, FollowUpStatus Status,
    DateTime? OccurredAtUtc = null, ContactChannel? ActualChannel = null, string? Outcome = null);
