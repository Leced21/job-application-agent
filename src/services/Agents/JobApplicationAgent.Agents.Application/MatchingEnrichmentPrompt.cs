using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.Application;

public sealed record MatchingCriterionSignal(string Name, string Status, decimal? Score, decimal Weight);
public sealed record EligibleMatchingObservation(string Criterion, string Focus);
public sealed record MatchingEnrichmentObservation(string Criterion, string Focus, string Priority);
public sealed record MatchingEnrichmentSnapshot(string ScoringVersion, decimal? OverallScore, decimal EvaluationCoverage, MatchingCriterionSignal[] Criteria);

public static class MatchingEnrichmentPrompt
{
    public const string Version = "matching-enrichment-fr-v1";
    private static readonly string[] Criteria = ["JobTitle", "Skills", "Location"];
    private static readonly string[] Focuses = ["MatchReported", "PartialMatchReported", "NoMatchReported", "InformationMissing"];
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static IReadOnlyDictionary<string, string> Messages { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string> {
        ["MatchReported"] = "Le calcul Matching a signalé une correspondance pour ce critère.",
        ["PartialMatchReported"] = "Le calcul Matching a signalé une correspondance partielle pour ce critère.",
        ["NoMatchReported"] = "Le calcul Matching n'a signalé aucune correspondance pour ce critère.",
        ["InformationMissing"] = "Information manquante pour ce critère ; cette observation ne permet pas de conclure sans supposer." });
    public static string Schema { get; } = JsonSerializer.Serialize(new {
        type = "object", additionalProperties = false, required = new[] { "schemaVersion", "observations" },
        properties = new { schemaVersion = new { @const = "matching-enrichment-v1" }, observations = new {
            type = "array", maxItems = 3, items = new { type = "object", additionalProperties = false,
                required = new[] { "criterion", "focus", "priority" }, properties = new {
                    criterion = new { @enum = Criteria }, focus = new { @enum = Focuses },
                    priority = new { @enum = new[] { "High", "Medium", "Low" } } } } } } });

    public static string Freeze(string scoringVersion, decimal? overallScore, decimal evaluationCoverage, IReadOnlyCollection<MatchingCriterionSignal> criteria)
    {
        if (string.IsNullOrWhiteSpace(scoringVersion) || scoringVersion.Length > 100 || scoringVersion.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_' or ':')) || overallScore is < 0 or > 100 ||
            evaluationCoverage is < 0 or > 100 || criteria.Count is < 1 or > 3) throw new InvalidAgentSourceException();
        var seen = new HashSet<string>(StringComparer.Ordinal); var snapshot = new List<MatchingCriterionSignal>();
        foreach (var item in criteria)
        {
            if (!Criteria.Contains(item.Name, StringComparer.Ordinal) || !seen.Add(item.Name) || item.Weight is < 0 or > 1 ||
                item.Status is not ("Evaluated" or "NotEvaluated" or "NotApplicable") ||
                (item.Status == "Evaluated" ? item.Score is null or < 0 or > 100 : item.Score is not null)) throw new InvalidAgentSourceException();
            snapshot.Add(item);
        }
        return JsonSerializer.Serialize(new { schemaVersion = "matching-signals-v1", scoringVersion, overallScore, evaluationCoverage,
            criteria = snapshot.OrderBy(x => Array.IndexOf(Criteria, x.Name)).Select(x => new { name = x.Name, status = x.Status, score = x.Score, weight = x.Weight }).ToArray() });
    }

    public static string Freeze(JsonElement result)
    {
        try
        {
            var criteria = result.GetProperty("criteria").EnumerateArray().Select(c => new MatchingCriterionSignal(
                c.GetProperty("name").GetString()!, c.GetProperty("status").GetString()!,
                c.GetProperty("score").ValueKind == JsonValueKind.Null ? null : c.GetProperty("score").GetDecimal(), c.GetProperty("weight").GetDecimal())).ToArray();
            return Freeze(result.GetProperty("scoringVersion").GetString()!,
                result.GetProperty("overallScore").ValueKind == JsonValueKind.Null ? null : result.GetProperty("overallScore").GetDecimal(),
                result.GetProperty("evaluationCoverage").GetDecimal(), criteria);
        }
        catch (InvalidAgentSourceException) { throw; }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentException)
        { throw new InvalidAgentSourceException(); }
    }

    public static MatchingEnrichmentSnapshot Read(string text)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(text) > 4096) throw new InvalidAgentSourceException();
            using var doc = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 6 }); var root = doc.RootElement;
            SourceExact(root, ["schemaVersion", "scoringVersion", "overallScore", "evaluationCoverage", "criteria"]);
            var scoringVersion = root.GetProperty("scoringVersion").GetString();
            if (root.GetProperty("schemaVersion").GetString() != "matching-signals-v1" || string.IsNullOrWhiteSpace(scoringVersion) || scoringVersion.Length > 100 ||
                scoringVersion.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_' or ':')))
                throw new InvalidAgentSourceException();
            var overall = root.GetProperty("overallScore"); decimal? overallScore = overall.ValueKind == JsonValueKind.Null ? null : overall.GetDecimal();
            if (overallScore is < 0 or > 100) throw new InvalidAgentSourceException();
            var coverage = root.GetProperty("evaluationCoverage").GetDecimal(); if (coverage is < 0 or > 100) throw new InvalidAgentSourceException();
            var array = root.GetProperty("criteria"); if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 3) throw new InvalidAgentSourceException();
            var seen = new HashSet<string>(StringComparer.Ordinal); var result = new List<MatchingCriterionSignal>();
            foreach (var item in array.EnumerateArray())
            {
                SourceExact(item, ["name", "status", "score", "weight"]);
                var name = item.GetProperty("name").GetString(); var status = item.GetProperty("status").GetString();
                var scoreElement = item.GetProperty("score"); decimal? score = scoreElement.ValueKind == JsonValueKind.Null ? null : scoreElement.GetDecimal();
                var weight = item.GetProperty("weight").GetDecimal();
                if (name is null || !Criteria.Contains(name, StringComparer.Ordinal) || !seen.Add(name) ||
                    status is not ("Evaluated" or "NotEvaluated" or "NotApplicable") || weight is < 0 or > 1 ||
                    (status == "Evaluated" ? score is null or < 0 or > 100 : score is not null))
                    throw new InvalidAgentSourceException();
                result.Add(new(name, status, score, weight));
            }
            return new(root.GetProperty("scoringVersion").GetString()!, overallScore, coverage, result.ToArray());
        }
        catch (InvalidAgentSourceException) { throw; }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { throw new InvalidAgentSourceException(); }
    }

    public static ModelPrompt Build(FrozenAgentSource source)
    {
        var snapshot = Read(source.Text); var observations = Eligible(snapshot.Criteria);
        return new("Restitue fidèlement le signal déjà calculé par Matching et priorise les observations pour la revue humaine. " +
            "Ne modifie aucun score et n'ajoute aucun fait. InformationMissing signifie seulement qu'une évaluation manque ; ne déduis aucune qualification et ne suppose rien. " +
            "Utilise exactement les critères et focus fournis. Assigne une priorité High, Medium ou Low. Retourne une liste vide si aucun critère n'est éligible.",
            JsonSerializer.Serialize(new { scoringVersion = snapshot.ScoringVersion, overallScore = snapshot.OverallScore,
                evaluationCoverage = snapshot.EvaluationCoverage, eligibleObservations = observations.Select(x => new { criterion = x.Criterion, focus = x.Focus }).ToArray() }), Schema);
    }

    public static string ValidateAndCanonicalize(string json, string source)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(json) > 8192) throw new InvalidAgentOutputException();
            var eligible = Eligible(Read(source).Criteria).ToDictionary(x => x.Criterion, StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 6 }); var root = doc.RootElement;
            OutputExact(root, ["schemaVersion", "observations"]);
            if (root.GetProperty("schemaVersion").GetString() != "matching-enrichment-v1") throw new InvalidAgentOutputException();
            var items = root.GetProperty("observations"); if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 3) throw new InvalidAgentOutputException();
            var seen = new HashSet<string>(StringComparer.Ordinal); var result = new List<MatchingEnrichmentObservation>();
            foreach (var item in items.EnumerateArray())
            {
                OutputExact(item, ["criterion", "focus", "priority"]);
                var criterion = item.GetProperty("criterion").GetString(); var focus = item.GetProperty("focus").GetString(); var priority = item.GetProperty("priority").GetString();
                if (criterion is null || !eligible.TryGetValue(criterion, out var expected) || focus != expected.Focus || !seen.Add(criterion) ||
                    priority is not ("High" or "Medium" or "Low")) throw new InvalidAgentOutputException();
                result.Add(new(criterion, focus, priority));
            }
            if (eligible.Count > 0 && result.Count == 0 || result.Count != eligible.Count) throw new InvalidAgentOutputException();
            return JsonSerializer.Serialize(new { schemaVersion = "matching-enrichment-v1", observations = result }, Json);
        }
        catch (InvalidAgentOutputException) { throw; }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or InvalidAgentSourceException)
        { throw new InvalidAgentOutputException(); }
    }

    private static EligibleMatchingObservation[] Eligible(IEnumerable<MatchingCriterionSignal> criteria) => criteria
        .Where(x => x.Status != "NotApplicable" && x.Weight > 0)
        .Select(x => new EligibleMatchingObservation(x.Name, x.Status == "NotEvaluated" ? "InformationMissing" : x.Score switch {
            0 => "NoMatchReported", 100 => "MatchReported", _ => "PartialMatchReported" })).ToArray();

    private static void SourceExact(JsonElement element, IEnumerable<string> expected)
    {
        ExactProperties(element, expected, InvalidAgentSourceExceptionFactory);
    }
    private static void OutputExact(JsonElement element, IEnumerable<string> expected)
    {
        ExactProperties(element, expected, InvalidAgentOutputExceptionFactory);
    }
    private static void ExactProperties(JsonElement element, IEnumerable<string> expected, Func<Exception> error)
    {
        if (element.ValueKind != JsonValueKind.Object) throw error();
        var allowed = expected.ToHashSet(StringComparer.Ordinal); var actual = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject()) if (!allowed.Contains(property.Name) || !actual.Add(property.Name)) throw error();
        if (!allowed.SetEquals(actual)) throw error();
    }
    private static Exception InvalidAgentSourceExceptionFactory() => new InvalidAgentSourceException();
    private static Exception InvalidAgentOutputExceptionFactory() => new InvalidAgentOutputException();
}
