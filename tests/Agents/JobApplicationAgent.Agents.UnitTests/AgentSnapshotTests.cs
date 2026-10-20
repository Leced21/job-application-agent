using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public sealed class AgentSnapshotTests
{
    [Fact] public void RestoreRejectsCorruptFingerprint()
    {
        var run = AgentRun.Start(Guid.NewGuid(), new(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()), DateTime.UtcNow);
        var snapshot = AgentRunSnapshot.From(run);
        Assert.Equal(run, AgentRun.Restore(snapshot));
        Assert.Throws<ArgumentException>(() => AgentRun.Restore(snapshot with { RequestSha256 = new string('0', 64) }));
    }
    [Fact] public void RestoreRejectsCorruptRevisionAndState()
    {
        var run = AgentRun.Start(Guid.NewGuid(), new(Guid.NewGuid(), AgentKind.JobAnalysis, Guid.NewGuid()), DateTime.UtcNow);
        var snapshot = AgentRunSnapshot.From(run);
        Assert.Throws<ArgumentException>(() => AgentRun.Restore(snapshot with { Revision = 2 }));
        Assert.Throws<ArgumentException>(() => AgentRun.Restore(snapshot with { CompletedAtUtc = DateTime.UtcNow }));
        Assert.Throws<ArgumentException>(() => AgentRun.Restore(snapshot with { State = (AgentRunState)99 }));
    }
}
