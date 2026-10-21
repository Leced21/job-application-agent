using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Agents.Application;
using JobApplicationAgent.Agents.Domain;

namespace JobApplicationAgent.Agents.Infrastructure;

public sealed class OllamaAgentModel(HttpClient http, IOllamaPromptTokenizer tokenizer, OllamaOptions options) : IAgentModel
{
    public async Task<PreparedModelInput> PrepareAsync(string modelId, ModelPrompt prompt, CancellationToken ct)
    {
        options.Validate();
        if (!options.Enabled || modelId != options.ModelId) throw new InvalidOperationException("Model configuration mismatch.");
        var rendered = tokenizer.Render(prompt);
        ct.ThrowIfCancellationRequested(); var count = tokenizer.CountTokens(rendered); ct.ThrowIfCancellationRequested();
        // No prompt data is sent during these metadata checks, and no model download is requested.
        await CheckModelAsync(ct);
        return new(modelId, prompt, count, rendered, options.TokenizerSha256);
    }
    public async Task<ModelResponse> GenerateAsync(ModelRequest request, CancellationToken ct)
    {
        var prepared = request.PreparedInput;
        try
        {
            options.Validate();
            if (!options.Enabled || request.ModelId != options.ModelId || request.MaxInputTokens != options.MaxInputTokens ||
                request.MaxOutputTokens != options.MaxOutputTokens || prepared is null || prepared.ModelId != request.ModelId || prepared.Prompt != request.Prompt ||
                prepared.TokenizerSha256 != options.TokenizerSha256 || prepared.ProviderPayload is null || prepared.InputTokens is < 1 || prepared.InputTokens > request.MaxInputTokens ||
                prepared.ProviderPayload != tokenizer.Render(request.Prompt) || prepared.InputTokens != tokenizer.CountTokens(prepared.ProviderPayload))
                throw new InvalidOperationException("Unprepared or modified model request.");
            await CheckModelAsync(ct); // Recheck the selected alias immediately before transmitting the prompt.
        }
        catch (Exception) { throw new AgentModelNotInvokedException(); }
        using var schema = JsonDocument.Parse(request.Prompt.OutputSchemaJson);
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/generate") { Content = JsonContent.Create(new {
            model = options.ModelName, prompt = prepared!.ProviderPayload, raw = true, stream = false, think = false,
            format = schema.RootElement, keep_alive = "30m", truncate = false, shift = false,
            options = new { num_ctx = options.ContextSize, num_predict = request.MaxOutputTokens, temperature = 0, seed = 0,
                stop = options.TokenizerProfile == "Llama3" ? new[] { "<|eot_id|>", "<|end_of_text|>" } : new[] { "<|im_end|>", "<|endoftext|>" } }
        }) };
        using var response = await ReadJsonAsync(message, 262144, ct);
        var root = response.RootElement;
        if (root.TryGetProperty("error", out _) || Text(root, "model") != options.ModelName ||
            !root.GetProperty("done").GetBoolean() || HasRemote(root)) throw new InvalidOperationException("Unconfirmed local model response.");
        var input = root.GetProperty("prompt_eval_count").GetInt32();
        var output = root.GetProperty("eval_count").GetInt32();
        if (input < 0 || output < 0) throw new InvalidOperationException("Unknown model usage.");
        var failure = input != prepared.InputTokens ? AgentFailure.UsageViolation : (AgentFailure?)null;
        var text = Text(root, "response");
        if (Text(root, "done_reason") != "stop" || text is null || Encoding.UTF8.GetByteCount(text) > 32768 ||
            root.TryGetProperty("tool_calls", out _) || (root.TryGetProperty("thinking", out var thinking) && !string.IsNullOrEmpty(thinking.GetString())))
            failure ??= AgentFailure.InvalidOutput;
        return new(failure is null ? text! : "", input, output, failure);
    }
    private async Task CheckModelAsync(CancellationToken ct)
    {
        using var statusRequest = new HttpRequestMessage(HttpMethod.Get, "api/status");
        using var status = await ReadJsonAsync(statusRequest, 16384, ct);
        if (!status.RootElement.GetProperty("cloud").GetProperty("disabled").GetBoolean())
            throw new InvalidOperationException("The running Ollama server must disable cloud features.");
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "api/tags");
        using var list = await ReadJsonAsync(listRequest, 1048576, ct);
        var matches = list.RootElement.GetProperty("models").EnumerateArray()
            .Where(x => Text(x, "name") == options.ModelName || Text(x, "model") == options.ModelName).ToArray();
        if (matches.Length != 1 || Text(matches[0], "digest") != options.ModelDigest || HasRemote(matches[0]))
            throw new InvalidOperationException("Configured local model digest not found.");
        using var showRequest = new HttpRequestMessage(HttpMethod.Post, "api/show") { Content = JsonContent.Create(new { model = options.ModelName, verbose = false }) };
        using var show = await ReadJsonAsync(showRequest, 2097152, ct);
        var root = show.RootElement;
        if (HasRemote(root) || root.TryGetProperty("error", out _)) throw new InvalidOperationException("Remote models are not allowed.");
        var info = root.GetProperty("model_info");
        var architecture = options.TokenizerProfile == "Llama3" ? "llama" : "qwen2";
        if (Text(info, "general.architecture") != architecture || Text(info, "tokenizer.ggml.model") != "gpt2" ||
            Text(info, "tokenizer.ggml.pre") != (options.TokenizerProfile == "Llama3" ? "llama-bpe" : "qwen2") ||
            info.GetProperty("tokenizer.ggml.add_bos_token").GetBoolean() != (options.TokenizerProfile == "Llama3") ||
            info.GetProperty(architecture + ".context_length").GetInt32() < options.ContextSize ||
            !root.GetProperty("capabilities").EnumerateArray().Any(c => c.GetString() == "completion"))
            throw new InvalidOperationException("Unsupported model tokenizer or context profile.");
    }
    private static string? Text(JsonElement root, string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool HasRemote(JsonElement root) => new[] { "remote_host", "remote_model" }.Any(name =>
        root.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null &&
        (value.ValueKind != JsonValueKind.String || !string.IsNullOrEmpty(value.GetString())));
    private async Task<JsonDocument> ReadJsonAsync(HttpRequestMessage request, int maxBytes, CancellationToken ct)
    {
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        // Never include server error bodies: they may echo source data.
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Local model transport unavailable.");
        if (response.Content.Headers.ContentLength > maxBytes) throw new InvalidOperationException("Local model response too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var chunk = new byte[8192]; int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes) throw new InvalidOperationException("Local model response too large.");
            buffer.Write(chunk, 0, read);
        }
        var document = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 16 });
        try { CheckDuplicateProperties(document.RootElement); return document; }
        catch { document.Dispose(); throw; }
    }
    private static void CheckDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            { if (!seen.Add(property.Name)) throw new InvalidOperationException("Ambiguous model response."); CheckDuplicateProperties(property.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) CheckDuplicateProperties(child);
    }
}
