#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$ModelName,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$ExpectedDigest,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
# Extract only from the installed local GGUF, never from a remote model or a registry.
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false; $handler.UseCookies = $false; $handler.UseProxy = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.BaseAddress = [uri]'http://127.0.0.1:11434/'
$client.Timeout = [TimeSpan]::FromSeconds(30)
function Read-LocalJson([string]$Route, [string]$Body = '') {
    $method = if ($Body) { [Net.Http.HttpMethod]::Post } else { [Net.Http.HttpMethod]::Get }
    $request = [Net.Http.HttpRequestMessage]::new($method, $Route)
    $deadline = [Threading.CancellationTokenSource]::new([TimeSpan]::FromSeconds(30))
    if ($Body) { $request.Content = [Net.Http.StringContent]::new($Body, [Text.Encoding]::UTF8, 'application/json') }
    try {
        $response = $client.SendAsync($request, [Net.Http.HttpCompletionOption]::ResponseHeadersRead, $deadline.Token).GetAwaiter().GetResult()
        try {
            $response.EnsureSuccessStatusCode() | Out-Null
            $stream = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
            $buffer = [IO.MemoryStream]::new(); $chunk = [byte[]]::new(8192)
            try {
                while (($read = $stream.ReadAsync($chunk, 0, $chunk.Length, $deadline.Token).GetAwaiter().GetResult()) -gt 0) {
                    if ($buffer.Length + $read -gt 16777216) { throw 'Local metadata exceeds 16 MiB.' }
                    $buffer.Write($chunk, 0, $read)
                }
                [Text.Encoding]::UTF8.GetString($buffer.ToArray()) | ConvertFrom-Json -Depth 32
            } finally { $stream.Dispose(); $buffer.Dispose() }
        } finally { $response.Dispose() }
    } finally { $request.Dispose(); $deadline.Dispose() }
}
function Confirm-Model {
    $models = @((Read-LocalJson 'api/tags').models | Where-Object { $_.name -ceq $ModelName })
    if ($models.Count -ne 1 -or $models[0].digest -cne $ExpectedDigest) { throw 'Local model digest mismatch.' }
}
try {
    if ((Read-LocalJson 'api/status').cloud.disabled -ne $true) { throw 'Disable cloud in the running Ollama server first.' }
    Confirm-Model
    $metadata = Read-LocalJson 'api/show' (@{model=$ModelName;verbose=$true} | ConvertTo-Json)
    $info = $metadata.model_info
    if ($metadata.remote_host -or $metadata.remote_model -or $info.'general.architecture' -cne 'qwen2' -or
        $info.'tokenizer.ggml.pre' -cne 'qwen2' -or $info.'tokenizer.ggml.add_bos_token' -ne $false -or
        $info.'tokenizer.ggml.tokens'.Count -lt 151646 -or $info.'tokenizer.ggml.tokens'[151643] -cne '<|endoftext|>' -or
        $info.'tokenizer.ggml.tokens'[151644] -cne '<|im_start|>' -or $info.'tokenizer.ggml.tokens'[151645] -cne '<|im_end|>') {
        throw 'Unsupported Qwen2 model/tokenizer metadata.'
    }
    Confirm-Model
    $directory = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $directory) { throw 'Choose a new output directory; existing artifacts are not overwritten.' }
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $vocabulary = [Collections.Specialized.OrderedDictionary]::new([StringComparer]::Ordinal)
    for ($index = 0; $index -lt 151643; $index++) { $vocabulary.Add($info.'tokenizer.ggml.tokens'[$index], $index) }
    $vocabPath = Join-Path $directory 'vocab.json'; $mergesPath = Join-Path $directory 'merges.txt'
    $outputPath = Join-Path $directory 'qwen2.tiktoken'
    [IO.File]::WriteAllText($vocabPath, ($vocabulary | ConvertTo-Json -Depth 3 -Compress), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllLines($mergesPath, [string[]]$info.'tokenizer.ggml.merges', [Text.UTF8Encoding]::new($false))
    & (Join-Path $PSScriptRoot 'Export-Qwen2Tokenizer.ps1') -VocabPath $vocabPath -MergesPath $mergesPath -OutputPath $outputPath
    $provenance = @{model=$ModelName;modelDigest=$ExpectedDigest;tokenizerSha256=(Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant();source='installed-ollama-gguf';exportedAtUtc=[DateTime]::UtcNow.ToString('O')}
    [IO.File]::WriteAllText((Join-Path $directory 'provenance.json'), ($provenance | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
} finally { $client.Dispose() }
