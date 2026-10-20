using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JobApplicationAgent.Agents.Application;
using Microsoft.ML.Tokenizers;

namespace JobApplicationAgent.Agents.Infrastructure;

public interface IOllamaPromptTokenizer
{
    string Render(ModelPrompt prompt);
    int CountTokens(string rendered);
}

public sealed class OllamaPromptTokenizer : IOllamaPromptTokenizer
{
    private readonly TiktokenTokenizer tokenizer;
    private readonly string profile;
    private readonly object gate = new();
    // A caller constructing this directly supplies a trusted tokenizer; production uses Load and validates its artifact.
    public OllamaPromptTokenizer(TiktokenTokenizer tokenizer, string profile)
    {
        if (profile is not ("Llama3" or "Qwen2")) throw new ArgumentException("Unsupported tokenizer profile.");
        this.tokenizer = tokenizer; this.profile = profile;
    }
    public static OllamaPromptTokenizer Load(OllamaOptions options)
    {
        options.Validate();
        using var file = File.OpenRead(options.TokenizerPath);
        if (file.Length is < 1024 or > 16777216 || Convert.ToHexStringLower(SHA256.HashData(file)) != options.TokenizerSha256)
            throw new InvalidOperationException("Tokenizer artifact size or hash mismatch.");
        file.Position = 0;
        ValidateVocabulary(file, options.TokenizerProfile == "Llama3" ? 128000 : 151643);
        file.Position = 0;
        var special = SpecialTokens(options.TokenizerProfile);
        var tokenizer = TiktokenTokenizer.Create(file,
            new RegexPreTokenizer(new Regex(Pattern(options.TokenizerProfile), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), special), null, special);
        return new(tokenizer, options.TokenizerProfile);
    }
    private static void ValidateVocabulary(Stream stream, int size)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, leaveOpen: true);
        var ranks = new HashSet<int>(); var pieces = new HashSet<string>(StringComparer.Ordinal); var bytes = new HashSet<byte>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var parts = line.Split(' ');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var rank) || rank < 0 || rank >= size || !ranks.Add(rank))
                throw new InvalidOperationException("Invalid tokenizer ranks.");
            var piece = Convert.FromBase64String(parts[0]);
            if (piece.Length is < 1 or > 4096 || !pieces.Add(Convert.ToBase64String(piece))) throw new InvalidOperationException("Invalid tokenizer pieces.");
            if (piece.Length == 1) bytes.Add(piece[0]);
        }
        if (ranks.Count != size || bytes.Count != 256) throw new InvalidOperationException("Tokenizer vocabulary does not match the configured profile.");
    }
    public static string Pattern(string profile) => profile == "Llama3"
        ? @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}{1,3}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+"
        : @"(?i:'s|'t|'re|'ve|'m|'ll|'d)|[^\r\n\p{L}\p{N}]?\p{L}+|\p{N}| ?[^\s\p{L}\p{N}]+[\r\n]*|\s*[\r\n]+|\s+(?!\S)|\s+";
    public static IReadOnlyDictionary<string, int> SpecialTokens(string profile) => profile == "Llama3"
        ? new Dictionary<string, int> { ["<|begin_of_text|>"] = 128000, ["<|end_of_text|>"] = 128001,
            ["<|start_header_id|>"] = 128006, ["<|end_header_id|>"] = 128007, ["<|eot_id|>"] = 128009 }
        : new Dictionary<string, int> { ["<|endoftext|>"] = 151643, ["<|im_start|>"] = 151644, ["<|im_end|>"] = 151645 };
    public string Render(ModelPrompt prompt)
    {
        var system = prompt.System + "\nSchéma JSON de sortie obligatoire :\n" + prompt.OutputSchemaJson;
        // Refuse literal role delimiters in any input. The job text is already JSON-escaped by JobAnalysisPrompt.
        if (system.Contains("<|") || prompt.UntrustedDataJson.Contains("<|")) throw new ArgumentException("Literal model control tokens are not permitted.");
        var rendered = profile == "Llama3"
            ? $"<|start_header_id|>system<|end_header_id|>\n\n{system}<|eot_id|><|start_header_id|>user<|end_header_id|>\n\n{prompt.UntrustedDataJson}<|eot_id|><|start_header_id|>assistant<|end_header_id|>\n\n"
            : $"<|im_start|>system\n{system}<|im_end|>\n<|im_start|>user\n{prompt.UntrustedDataJson}<|im_end|>\n<|im_start|>assistant\n";
        if (new UTF8Encoding(false, true).GetByteCount(rendered) > 65536 || Regex.IsMatch(rendered, @"\S{2049}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            throw new ArgumentException("Prompt exceeds tokenizer bounds.");
        return rendered;
    }
    public int CountTokens(string rendered)
    {
        // The tokenizer's bounded cache is shared; serialize access rather than relying on implementation-specific thread safety.
        // Ollama adds BOS for the validated Llama3 profile. Do not put another literal BOS in the raw prompt.
        lock (gate) return tokenizer.CountTokens(rendered) + (profile == "Llama3" ? 1 : 0);
    }
}
