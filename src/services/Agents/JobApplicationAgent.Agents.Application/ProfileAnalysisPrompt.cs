using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.Application;

public static class ProfileInformationCatalog
{
    // Questions are authored by the application, never invented by the model or used to update Profile.
    public static IReadOnlyDictionary<string, string> Questions { get; } = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string> {
        ["JobTitle"] = "Quel intitulé professionnel souhaitez-vous renseigner ?",
        ["Summary"] = "Souhaitez-vous ajouter une présentation de votre parcours ?",
        ["Skills"] = "Quelles compétences confirmées souhaitez-vous renseigner ?",
        ["Experiences"] = "Souhaitez-vous renseigner une expérience, ou confirmer que cette rubrique est sans objet ?",
        ["Educations"] = "Souhaitez-vous renseigner une formation, ou confirmer que cette rubrique est sans objet ?",
        ["Languages"] = "Quelles langues et quels niveaux confirmés souhaitez-vous renseigner ?",
        ["DesiredJobTitles"] = "Quels postes souhaitez-vous rechercher ?",
        ["PreferredLocations"] = "Quelles zones géographiques souhaitez-vous renseigner ?",
        ["ContractTypes"] = "Quels types de contrat recherchez-vous ?",
        ["WorkModes"] = "Quelles modalités de travail souhaitez-vous privilégier ?",
        ["AvailableFrom"] = "Souhaitez-vous préciser une date de disponibilité ?" });
}
public sealed record ProfileQuestion(string Field, string Priority);
public sealed record ProfileAnalysisOutput(string SchemaVersion, ProfileQuestion[] Questions);

public static class ProfileAnalysisPrompt
{
    public const string Version = "profile-analysis-fr-v1";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static string Schema { get; } = JsonSerializer.Serialize(new {
        type = "object", additionalProperties = false, required = new[] { "schemaVersion", "questions" },
        properties = new { schemaVersion = new { @const = "profile-analysis-v1" }, questions = new {
            type = "array", maxItems = 5, items = new { type = "object", additionalProperties = false,
                required = new[] { "field", "priority" }, properties = new {
                    field = new { @enum = ProfileInformationCatalog.Questions.Keys },
                    priority = new { @enum = new[] { "High", "Medium", "Low" } } } } } } });
    public static string FreezePresence(IReadOnlyDictionary<string, bool> present)
    {
        if (present.Count != ProfileInformationCatalog.Questions.Count || present.Keys.Any(key => !ProfileInformationCatalog.Questions.ContainsKey(key)))
            throw new ArgumentException("Exact profile presence fields required.");
        return JsonSerializer.Serialize(new { schemaVersion = "profile-presence-v1",
            present = ProfileInformationCatalog.Questions.Keys.ToDictionary(field => field, field => present[field]) });
    }
    public static IReadOnlyDictionary<string, bool> ReadPresence(string text)
    {
        try
        {
            if (Encoding.UTF8.GetByteCount(text) > 4096) throw new InvalidAgentSourceException();
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 4 });
            Exact(document.RootElement, ["schemaVersion", "present"]);
            if (document.RootElement.GetProperty("schemaVersion").GetString() != "profile-presence-v1") throw new InvalidAgentSourceException();
            var present = document.RootElement.GetProperty("present"); Exact(present, ProfileInformationCatalog.Questions.Keys);
            return present.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.GetBoolean());
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { throw new InvalidAgentSourceException(); }
    }
    public static ModelPrompt Build(FrozenAgentSource source)
    {
        var present = ReadPresence(source.Text);
        return new("Propose au plus cinq demandes de complément pour un profil candidat à partir des indicateurs fournis. " +
            "Un champ false signifie seulement non renseigné, jamais une absence de compétence ou de qualification. " +
            "Sélectionne uniquement des champs false, sans doublon, avec une priorité High, Medium ou Low. " +
            "Ne propose aucune valeur, aucun diplôme, aucune expérience et aucun texte libre. Aucune action ni outil. " +
            "Retourne uniquement le JSON du schéma. Si tous les champs sont true, questions doit être vide ; sinon propose au moins une question. " +
            "Les questions seront affichées avec les formulations fixes de l'application et soumises à validation humaine.",
            JsonSerializer.Serialize(new { resourceId = source.ResourceId, sourceSha256 = source.Sha256, present }), Schema);
    }
    public static string ValidateAndCanonicalize(string output, string source)
    {
        var present = ReadPresence(source);
        try
        {
            if (output is null || Encoding.UTF8.GetByteCount(output) > 8192) throw new InvalidAgentOutputException();
            using var document = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 5 });
            Exact(document.RootElement, ["schemaVersion", "questions"]);
            if (document.RootElement.GetProperty("schemaVersion").GetString() != "profile-analysis-v1") throw new InvalidAgentOutputException();
            var array = document.RootElement.GetProperty("questions");
            if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() > 5 || (array.GetArrayLength() == 0 && present.Values.Any(value => !value)))
                throw new InvalidAgentOutputException();
            var fields = new HashSet<string>(StringComparer.Ordinal); var questions = new List<ProfileQuestion>();
            foreach (var item in array.EnumerateArray())
            {
                Exact(item, ["field", "priority"]);
                var field = item.GetProperty("field").GetString(); var priority = item.GetProperty("priority").GetString();
                if (field is null || !present.TryGetValue(field, out var hasValue) || hasValue || !fields.Add(field) || priority is not ("High" or "Medium" or "Low"))
                    throw new InvalidAgentOutputException();
                questions.Add(new(field, priority));
            }
            return JsonSerializer.Serialize(new ProfileAnalysisOutput("profile-analysis-v1", questions.ToArray()), Json);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { throw new InvalidAgentOutputException(); }
    }
    private static void Exact(JsonElement value, IEnumerable<string> expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidOperationException();
        var names = value.EnumerateObject().Select(property => property.Name).ToArray(); var required = expected.ToArray();
        if (names.Length != required.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length || required.Except(names, StringComparer.Ordinal).Any())
            throw new InvalidOperationException();
    }
}
