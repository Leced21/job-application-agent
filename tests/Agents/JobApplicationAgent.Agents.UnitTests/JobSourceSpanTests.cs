using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.UnitTests;

public sealed class JobSourceSpanTests
{
    [Fact] public void PromptProvidesCamelCaseSourceCoordinatesWithoutInventingRequirements()
    {
        var text = "SQL required. Rust optional.";
        var prompt = JobAnalysisPrompt.Build(new(Guid.NewGuid(), Guid.NewGuid(), text, AgentRun.Hash(text)));
        using var json = JsonDocument.Parse(prompt.UntrustedDataJson);
        var rust = json.RootElement.GetProperty("candidateSpans").EnumerateArray().Single(x => x.GetProperty("value").GetString() == "Rust");
        Assert.Equal(14, rust.GetProperty("start").GetInt32()); Assert.Equal(4, rust.GetProperty("length").GetInt32());
        Assert.DoesNotContain("category", rust.ToString()); Assert.Equal("job-analysis-fr-v2", JobAnalysisPrompt.Version);
    }
    [Fact] public void OffsetsRemainUtf16WithEmojiAccentsAndProgrammingLanguagePunctuation()
    {
        var text = "🙂 SQL et C#. Français 有"; var spans = JobAnalysisPrompt.CandidateSpans(text);
        Assert.Contains(spans, x => x.Value == "SQL" && x.Start == 3 && x.Length == 3);
        Assert.Contains(spans, x => x.Value == "C#" && x.Start == 10 && x.Length == 2);
        Assert.Contains(spans, x => x.Value == "有");
        foreach (var span in spans) Assert.Equal(span.Value, text.Substring(span.Start, span.Length));
    }
    [Fact] public void HintsAreBoundedAndDoNotTruncateTheFrozenText()
    {
        var text = string.Join(' ', Enumerable.Repeat("SQL", 200));
        Assert.Equal(96, JobAnalysisPrompt.CandidateSpans(text).Count);
        var prompt = JobAnalysisPrompt.Build(new(Guid.NewGuid(), Guid.NewGuid(), text, AgentRun.Hash(text)));
        using var json = JsonDocument.Parse(prompt.UntrustedDataJson); Assert.Equal(text, json.RootElement.GetProperty("untrustedJobText").GetString());
    }
}
