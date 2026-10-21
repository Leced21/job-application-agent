#requires -Version 7.0
param([Parameter(Mandatory)][string]$ProvenancePath)
$ErrorActionPreference = 'Stop'
$provenanceFile = Get-Item -LiteralPath $ProvenancePath
$provenance = Get-Content -LiteralPath $provenanceFile.FullName -Raw | ConvertFrom-Json
$tokenizerPath = Join-Path $provenanceFile.DirectoryName 'qwen2.tiktoken'
if ($provenance.source -cne 'installed-ollama-gguf' -or $provenance.model -cne 'qwen2.5:7b' -or
    $provenance.modelDigest -cnotmatch '^[a-f0-9]{64}$' -or $provenance.tokenizerSha256 -cnotmatch '^[a-f0-9]{64}$' -or
    (Get-FileHash -LiteralPath $tokenizerPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $provenance.tokenizerSha256) {
    throw 'Qualified model/tokenizer provenance mismatch.'
}
$status = Invoke-RestMethod 'http://127.0.0.1:11434/api/status' -TimeoutSec 5
if ($status.cloud.disabled -ne $true) { throw 'The running Ollama server must disable cloud features.' }
$models = @((Invoke-RestMethod 'http://127.0.0.1:11434/api/tags' -TimeoutSec 5).models | Where-Object { $_.name -ceq $provenance.model })
if ($models.Count -ne 1 -or $models[0].digest -cne $provenance.modelDigest) { throw 'Installed model differs from the qualified artifact.' }
$settings = @{
    'Provider'='Ollama'; 'BaseUrl'='http://127.0.0.1:11434'; 'ModelName'=$provenance.model; 'ModelDigest'=$provenance.modelDigest
    'TokenizerProfile'='Qwen2'; 'TokenizerPath'=$tokenizerPath; 'TokenizerSha256'=$provenance.tokenizerSha256
    'MaxInputTokens'='4096'; 'MaxOutputTokens'='1024'; 'ContextSize'='8192'; 'TimeoutSeconds'='120'
    'AllowJobDescriptions'='true'; 'LocalServerCloudDisabled'='true'
}
foreach ($setting in $settings.GetEnumerator()) { [Environment]::SetEnvironmentVariable('Agents__Model__' + $setting.Key, $setting.Value, 'Process') }
Write-Output 'Configuration Ollama chargée pour ce terminal. La connexion Agents, son identité et ses quotas se configurent séparément.'
