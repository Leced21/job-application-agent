#requires -Version 7.0
param(
    [string]$ComposeProjectName = 'job-application-agent',
    [switch]$IsolatedDatabase
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$stateDirectory = Join-Path $repositoryRoot '.local/agents'
[IO.Directory]::CreateDirectory($stateDirectory) | Out-Null
$settingsPath = Join-Path $stateDirectory 'runtime-settings.json'
if (-not (Test-Path -LiteralPath $settingsPath)) {
    $settings = @{ OwnerId = [guid]::NewGuid().ToString(); Token = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant()
        DatabaseUser = 'agent_' + [guid]::NewGuid().ToString('N').Substring(0,12)
        DatabasePassword = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(32)).ToLowerInvariant(); MaxTokens = 100000 }
    $stream = [IO.File]::Open($settingsPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes(($settings | ConvertTo-Json)); $stream.Write($bytes,0,$bytes.Length)
    } finally { $stream.Dispose() }
}
. (Join-Path $PSScriptRoot 'Use-LocalAgents.ps1')
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$listener = Get-NetTCPConnection -LocalPort 5008 -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    try {
        $catalog = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/catalog' -Headers $headers -TimeoutSec 5
        $null = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/budget' -Headers $headers -TimeoutSec 5
        if ($catalog.modelId -cne $env:Agents__Model__ModelName + '@sha256:' + $env:Agents__Model__ModelDigest) { throw 'Unexpected model.' }
        Write-Output 'API Agents déjà active avec cette configuration : http://127.0.0.1:5008'; return
    } catch { throw 'Port 5008 is occupied by another Agents API. Stop only the agents-api service if you want the local Ollama API; its PostgreSQL service can stay running. No process was stopped.' }
}
$agentsComposeFile = Join-Path $repositoryRoot 'docker-compose.agents.yml'
$envFile = Join-Path $repositoryRoot '.env'
$useMainComposeDatabase = (Test-Path -LiteralPath $envFile) -and -not $IsolatedDatabase
if ($useMainComposeDatabase) {
    $runningProjects = @(& docker ps --all --filter 'label=com.docker.compose.service=agents-db' --format '{{.Label "com.docker.compose.project"}}')
    if ($LASTEXITCODE -ne 0) { throw 'Unable to inspect running Docker containers; no Agents database was started.' }
    $otherProjects = @($runningProjects | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -cne $ComposeProjectName } | Sort-Object -Unique)
    if ($otherProjects.Count -gt 0) {
        throw "An agents-db container already exists in another Compose project ($($otherProjects -join ', ')). Reuse its volume with -IsolatedDatabase or reconcile the database before starting another; no container or volume was changed."
    }

    $composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $envFile, '-f', $agentsComposeFile)
    $composeConfiguration = & docker compose @composePrefix config --format json
    if ($LASTEXITCODE -ne 0) { throw 'Could not read Agents database settings from .env; no database was started.' }
    $databaseEnvironment = (($composeConfiguration -join [Environment]::NewLine) | ConvertFrom-Json).services.'agents-db'.environment
    $databaseName = [string]$databaseEnvironment.POSTGRES_DB
    $databaseUser = [string]$databaseEnvironment.POSTGRES_USER
    $databasePassword = [string]$databaseEnvironment.POSTGRES_PASSWORD
    if ([string]::IsNullOrWhiteSpace($databaseName) -or [string]::IsNullOrWhiteSpace($databaseUser) -or [string]::IsNullOrWhiteSpace($databasePassword)) {
        throw 'Agents database settings are incomplete in .env; no database was started.'
    }
    $quoteConnectionValue = { param([string]$value) '"' + $value.Replace('"', '""') + '"' }
    $env:AGENTS_DB_NAME = $databaseName
    $env:AGENTS_DB_USER = $databaseUser
    $env:AGENTS_DB_PASSWORD = $databasePassword
    $env:ConnectionStrings__AgentsDatabase = 'Host=127.0.0.1;Port=5438;Database={0};Username={1};Password={2}' -f (& $quoteConnectionValue $databaseName), (& $quoteConnectionValue $databaseUser), (& $quoteConnectionValue $databasePassword)
    $composeArguments = $composePrefix + @('up', '-d', '--wait', '--wait-timeout', '60', 'agents-db')
} else {
    $composeArguments = @('--project-name', 'job-application-agent-agents', '-f', $agentsComposeFile, 'up', '-d', '--wait', '--wait-timeout', '60', 'agents-db')
}
& docker compose @composeArguments
if ($LASTEXITCODE -ne 0) { throw 'Agents PostgreSQL startup failed; the existing volume was preserved.' }
$infrastructure = Join-Path $repositoryRoot 'src/services/Agents/JobApplicationAgent.Agents.Infrastructure'
$api = Join-Path $repositoryRoot 'src/services/Agents/JobApplicationAgent.Agents.Api'
& dotnet ef database update --project $infrastructure --startup-project $api --configuration LocalAgents --context AgentsDbContext
if ($LASTEXITCODE -ne 0) { throw 'Agents migration failed; the API was not started.' }
$apiDll = Join-Path $api 'bin/LocalAgents/net10.0/JobApplicationAgent.Agents.Api.dll'
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source
$process = Start-Process -FilePath $dotnet -ArgumentList @(('"' + $apiDll + '"'),'--urls','http://127.0.0.1:5008') -WorkingDirectory $api -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $stateDirectory 'agents-api.stdout.log') -RedirectStandardError (Join-Path $stateDirectory 'agents-api.stderr.log')
$processInfo = @{ ProcessId=$process.Id; StartedAtUtc=[DateTime]::UtcNow.ToString('O'); Url='http://127.0.0.1:5008'; Configuration='LocalAgents' }
$processInfo | ConvertTo-Json | Set-Content (Join-Path $stateDirectory 'api-process.json')
$ready = $false
for ($attempt = 0; $attempt -lt 30; $attempt++) {
    if ($process.HasExited) { throw 'Agents API stopped at startup; inspect .local/agents/agents-api.stderr.log.' }
    try {
        $null = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/budget' -Headers $headers -TimeoutSec 2
        $ready=$true; break
    } catch { Start-Sleep -Seconds 1 }
}
if (-not $ready) { throw 'Agents API did not become ready; inspect the local logs.' }
if ($useMainComposeDatabase) {
    Write-Output "API Agents active : http://127.0.0.1:5008 ; PostgreSQL partagé avec le projet Compose '$ComposeProjectName' sur 127.0.0.1:5438."
} else {
    Write-Output 'API Agents active : http://127.0.0.1:5008 ; PostgreSQL isolé : 127.0.0.1:5438.'
}
