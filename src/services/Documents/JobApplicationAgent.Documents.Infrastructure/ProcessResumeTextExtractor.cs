using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using JobApplicationAgent.Documents.Application;
using JobApplicationAgent.Documents.Domain;
using Microsoft.Extensions.Configuration;

namespace JobApplicationAgent.Documents.Infrastructure;

public sealed class ProcessResumeTextExtractor(IConfiguration configuration) : IResumeTextExtractor, IDisposable
{
    private readonly SemaphoreSlim capacity = new(2, 2);
    public async Task<ExtractedText> ExtractAsync(ResumeDocument document, CancellationToken ct)
    {
        if (!await capacity.WaitAsync(0, ct)) throw new TextExtractionUnavailableException("Extraction capacity is busy; retry later.");
        try { return await RunAsync(document, ct); }
        finally { capacity.Release(); }
    }
    private async Task<ExtractedText> RunAsync(ResumeDocument document, CancellationToken ct)
    {
        var worker = configuration["Documents:Extraction:WorkerPath"];
        if (string.IsNullOrWhiteSpace(worker) || !Path.IsPathFullyQualified(worker) || !File.Exists(worker) || Path.GetExtension(worker) != ".dll")
            throw new TextExtractionUnavailableException("Configure Documents:Extraction:WorkerPath to the built extraction worker DLL.");
        var timeout = configuration.GetValue("Documents:Extraction:TimeoutMilliseconds", 15000);
        if (timeout is < 1 or > 30000) throw new TextExtractionUnavailableException("Invalid extraction deadline configuration.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(worker)!
        };
        process.StartInfo.ArgumentList.Add(worker);
        process.StartInfo.ArgumentList.Add(document.MediaType == "application/pdf" ? "pdf" : "docx");
        // Do not pass the API's bearer token, database credentials or unrelated environment secrets to the parser.
        process.StartInfo.Environment.Clear();
        foreach (var name in new[] { "PATH", "SystemRoot", "WINDIR", "DOTNET_ROOT", "DOTNET_ROOT_X64", "TEMP", "TMP" })
            if (Environment.GetEnvironmentVariable(name) is { } value) process.StartInfo.Environment[name] = value;
        process.StartInfo.Environment["DOTNET_GCHeapHardLimit"] = "10000000";
        process.StartInfo.Environment["DOTNET_EnableDiagnostics"] = "0";
        var started = false;
        void Kill()
        {
            try { if (started && !process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        async Task<string> ReadAsync(StreamReader reader, int max)
        {
            var output = new StringBuilder();
            var buffer = new char[8192];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), deadline.Token)) != 0)
            {
                if (output.Length + count > max) { Kill(); throw new TextExtractionRejectedException("Extraction output exceeds limits."); }
                output.Append(buffer, 0, count);
            }
            return output.ToString();
        }
        async Task InputAsync()
        {
            await process.StandardInput.BaseStream.WriteAsync(document.Content, deadline.Token);
            process.StandardInput.Close();
        }
        async Task MonitorAsync()
        {
            while (!process.HasExited)
            {
                deadline.Token.ThrowIfCancellationRequested();
                try
                {
                    process.Refresh();
                    if (process.WorkingSet64 > 512L * 1024 * 1024)
                    { Kill(); throw new TextExtractionRejectedException("Extraction memory limit exceeded."); }
                }
                catch (InvalidOperationException) when (process.HasExited) { return; }
                await Task.Delay(50, deadline.Token);
            }
        }
        try
        {
            started = process.Start();
            if (!started) throw new TextExtractionUnavailableException("Extraction worker could not start.");
            using var cancellation = deadline.Token.Register(Kill);
            var output = ReadAsync(process.StandardOutput, 2 * 1024 * 1024);
            await Task.WhenAll(InputAsync(), output, ReadAsync(process.StandardError, 8192), MonitorAsync());
            await process.WaitForExitAsync(deadline.Token);
            if (process.ExitCode != 0) throw new TextExtractionRejectedException("Document cannot be extracted: malformed, encrypted, empty, or beyond limits. OCR is unavailable.");
            var result = JsonSerializer.Deserialize<ExtractedText>(await output)
                ?? throw new TextExtractionRejectedException("Empty extraction response.");
            result.Validate();
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new TextExtractionUnavailableException("Extraction deadline exceeded; no result was stored."); }
        catch (Win32Exception) { throw new TextExtractionUnavailableException("Extraction worker could not start."); }
        catch (JsonException) { throw new TextExtractionRejectedException("Invalid extraction response."); }
        catch (IOException) when (deadline.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
            throw new TextExtractionUnavailableException("Extraction deadline exceeded; no result was stored.");
        }
        catch (IOException) when (!ct.IsCancellationRequested && !deadline.IsCancellationRequested)
        { throw new TextExtractionRejectedException("Extraction worker rejected the document."); }
        finally
        {
            Kill();
            if (started) await process.WaitForExitAsync(CancellationToken.None);
        }
    }
    public void Dispose() => capacity.Dispose();
}
