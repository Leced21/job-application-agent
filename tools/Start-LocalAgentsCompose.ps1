param([string]$ComposeProjectName = 'job-application-agent')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
Set-Location $repositoryRoot

$envFile = Join-Path $repositoryRoot '.env'
if (-not (Test-Path -LiteralPath $envFile)) { throw 'The project .env file is required. No service was changed.' }
$composeFiles = @(
    'docker-compose.yml', 'docker-compose.identity.yml',
    'docker-compose.applications.yml', 'docker-compose.applications-api.yml',
    'docker-compose.documents.yml', 'docker-compose.documents-api.yml',
    'docker-compose.collection.yml', 'docker-compose.collection-api.yml',
    'docker-compose.agents.yml', 'docker-compose.agents-api.yml',
    'docker-compose.edge.yml', 'docker-compose.local-agents.yml'
)
foreach ($file in $composeFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $file))) { throw "Required Compose file is missing: $file. No service was changed." }
}

$provenancePath = Join-Path $repositoryRoot '.local/agents/qwen2.5-7b-845dbda0ea48-qualified/provenance.json'
$tokenizerPath = Join-Path (Split-Path $provenancePath -Parent) 'qwen2.tiktoken'
if (-not (Test-Path -LiteralPath $provenancePath) -or -not (Test-Path -LiteralPath $tokenizerPath)) {
    throw 'The qualified local Qwen model/tokenizer files are missing. No service was changed.'
}
$provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
if ($provenance.model -cne 'qwen2.5:7b' -or $provenance.modelDigest -cne '845dbda0ea48ed749caafd9e6037047aa19acfcfd82e704d7ca97d631a0b697e' -or
    (Get-FileHash -LiteralPath $tokenizerPath -Algorithm SHA256).Hash.ToLowerInvariant() -cne $provenance.tokenizerSha256 -or
    $provenance.tokenizerSha256 -cne '6cf5f2a2560a7d581548aeb0c31c6a60de2d3e873152346e421affc871699be4') {
    throw 'The qualified Ollama model or tokenizer digest does not match. No service was changed.'
}

try {
    $status = Invoke-RestMethod 'http://127.0.0.1:11434/api/status' -TimeoutSec 5
    $models = @((Invoke-RestMethod 'http://127.0.0.1:11434/api/tags' -TimeoutSec 5).models | Where-Object { $_.name -ceq $provenance.model })
} catch { throw 'The local Ollama server is unavailable. Start Ollama with cloud features disabled; no service was changed.' }
if ($status.cloud.disabled -ne $true) { throw 'The local Ollama server must report cloud.disabled=true. No service was changed.' }
if ($models.Count -ne 1 -or $models[0].digest -cne $provenance.modelDigest) { throw 'The installed Ollama model differs from the qualified local artifact. No service was changed.' }

$modelStore = if (-not [string]::IsNullOrWhiteSpace($env:OLLAMA_MODELS)) { $env:OLLAMA_MODELS } else { Join-Path $env:USERPROFILE '.ollama/models' }
$modelStore = [IO.Path]::GetFullPath($modelStore)
$manifestPath = Join-Path $modelStore 'manifests/registry.ollama.ai/library/qwen2.5/7b'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'The expected local Ollama model store was not found. No service was changed.' }

$env:AGENTS_OLLAMA_MODELS_PATH = $modelStore.Replace('\', '/')
$env:AGENTS_OLLAMA_TOKENIZER_PATH = [IO.Path]::GetFullPath($tokenizerPath).Replace('\', '/')
$composeArgs = @('--project-name', $ComposeProjectName, '--env-file', $envFile)
foreach ($file in $composeFiles) { $composeArgs += @('-f', $file) }
& docker compose @composeArgs config --quiet
if ($LASTEXITCODE -ne 0) { throw 'Compose configuration validation failed. No service was changed.' }

& docker compose @composeArgs build agents-api frontend
if ($LASTEXITCODE -ne 0) { throw 'Agents API or frontend image build failed. Running services were not replaced.' }
& docker compose @composeArgs up -d --wait --wait-timeout 300 agents-api gateway-api frontend
if ($LASTEXITCODE -ne 0) { throw 'The local Agents stack did not become healthy. Inspect docker compose logs for agents-api and agents-ollama.' }

$apiContainerOutput = & docker compose @composeArgs ps --all --format json agents-api 2>&1
if ($LASTEXITCODE -ne 0) { throw "Could not inspect the Agents API container: $($apiContainerOutput -join ' ')" }
$apiContainerJson = ($apiContainerOutput -join [Environment]::NewLine).Trim()
if ([string]::IsNullOrWhiteSpace($apiContainerJson)) { throw 'Compose returned no container information for agents-api. Inspect `docker compose ps --all agents-api`.' }
try { $apiContainers = @($apiContainerJson | ConvertFrom-Json) }
catch { throw 'Compose returned invalid JSON while inspecting the Agents API container.' }
$apiContainer = $apiContainers | Where-Object { $_.Service -eq 'agents-api' } | Select-Object -First 1
if ($null -eq $apiContainer -or [string]::IsNullOrWhiteSpace($apiContainer.ID)) {
    throw "Compose did not find the agents-api container. Inspect `docker compose ps --all agents-api`. Output: $apiContainerJson"
}
if ($apiContainer.State -ne 'running') {
    throw "Agents API container is not running (state: $($apiContainer.State)). Inspect `docker compose logs agents-api`."
}
$apiContainerId = $apiContainer.ID
$containerStatusText = & docker exec $apiContainerId curl --fail --silent --show-error http://agents-ollama:11434/api/status
if ($LASTEXITCODE -ne 0) { throw 'The private Ollama service did not return its status. No warm-up was attempted.' }
$containerModelText = & docker exec $apiContainerId curl --fail --silent --show-error http://agents-ollama:11434/api/tags
if ($LASTEXITCODE -ne 0) { throw 'The private Ollama service did not return its model catalog. No warm-up was attempted.' }
$containerStatus = ($containerStatusText -join [Environment]::NewLine) | ConvertFrom-Json
$containerModels = @((($containerModelText -join [Environment]::NewLine) | ConvertFrom-Json).models | Where-Object { $_.name -ceq $provenance.model })
if ($containerStatus.cloud.disabled -ne $true -or $containerModels.Count -ne 1 -or $containerModels[0].digest -cne $provenance.modelDigest) {
    throw 'The private Ollama service does not match the qualified offline model. No warm-up was attempted.'
}
$warmupBody = '{"model":"qwen2.5:7b","prompt":"Return only the word READY.","stream":false,"raw":true,"think":false,"keep_alive":"30m","options":{"num_ctx":8192,"num_predict":8,"temperature":0,"seed":0}}'
$warmupBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($warmupBody))
$warmupCommand = "printf '%s' '$warmupBase64' | base64 -d | curl --fail --silent --show-error --max-time 300 -H 'Content-Type: application/json' --data-binary @- http://agents-ollama:11434/api/generate"
$warmupResponseText = & docker exec $apiContainerId sh -c $warmupCommand 2>&1
if ($LASTEXITCODE -ne 0) { throw "Local Ollama warm-up failed: $($warmupResponseText -join ' ')" }
try { $warmupResponse = (($warmupResponseText -join [Environment]::NewLine) | ConvertFrom-Json) }
catch { throw 'Local Ollama warm-up returned invalid JSON.' }
if ($warmupResponse.done -ne $true -or [string]::IsNullOrWhiteSpace($warmupResponse.response)) { throw 'Local Ollama warm-up did not complete.' }

Write-Output 'Agents are configured with the qualified local Qwen model, cloud disabled, and the six local connectors enabled.'
Write-Output 'Ollama is internal to the Agents Compose network and the existing local model store is mounted read-only.'
Write-Output 'A synthetic warm-up keeps the model ready in GPU memory for 30 minutes; it creates no Agents run or quota charge.'
Write-Output 'The frontend image has been refreshed with the browser run and review workflow.'
Write-Output 'Open http://localhost:4200/agents to launch agents and review proposals.'
