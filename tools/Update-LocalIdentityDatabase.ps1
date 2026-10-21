#requires -Version 5.1
param(
    [string]$ComposeProjectName = 'job-application-agent'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$envFile = Join-Path $repositoryRoot '.env'
$composeFile = Join-Path $repositoryRoot 'docker-compose.identity.yml'
$gatewayProject = Join-Path $repositoryRoot 'src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj'

if (-not (Test-Path -LiteralPath $envFile)) {
    throw "Missing .env at repository root. Copy .env.example to .env and configure the local Identity database first."
}

$existingLabelSets = @(& docker ps --all --filter 'label=com.docker.compose.service=identity-db' --format '{{.Labels}}')
if ($LASTEXITCODE -ne 0) {
    throw 'Unable to inspect Docker containers; no Identity database was started and no migration was applied.'
}
$existingProjects = @(
    foreach ($labelSet in $existingLabelSets) {
        foreach ($label in ($labelSet -split ',')) {
            $projectLabelPrefix = 'com.docker.compose.project='
            if ($label.StartsWith($projectLabelPrefix, [StringComparison]::Ordinal)) {
                $label.Substring($projectLabelPrefix.Length)
            }
        }
    }
)
$otherProjects = @($existingProjects | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -cne $ComposeProjectName } | Sort-Object -Unique)
if ($otherProjects.Count -gt 0) {
    throw "An identity-db container exists in another Compose project ($($otherProjects -join ', ')). Reconcile the active database project first; this script made no changes."
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $envFile, '-f', $composeFile)
$composeConfiguration = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read Identity database settings from .env; no database was started and no migration was applied.'
}

try {
    $databaseEnvironment = (($composeConfiguration -join [Environment]::NewLine) | ConvertFrom-Json).services.'identity-db'.environment
} catch {
    throw 'Could not parse Identity database settings from Compose configuration; no database was started and no migration was applied.'
}

$databaseName = [string]$databaseEnvironment.POSTGRES_DB
$databaseUser = [string]$databaseEnvironment.POSTGRES_USER
$databasePassword = [string]$databaseEnvironment.POSTGRES_PASSWORD
if ([string]::IsNullOrWhiteSpace($databaseName) -or [string]::IsNullOrWhiteSpace($databaseUser) -or [string]::IsNullOrWhiteSpace($databasePassword)) {
    throw 'Identity database settings are incomplete in .env; no database was started and no migration was applied.'
}

$composeArguments = $composePrefix + @('up', '-d', '--wait', '--wait-timeout', '60', 'identity-db')
& docker compose @composeArguments
if ($LASTEXITCODE -ne 0) {
    throw 'Identity PostgreSQL startup failed; the existing volume was preserved and no migration was applied.'
}

$environmentNames = @('ConnectionStrings__IdentityDatabase', 'IDENTITY_DB_NAME', 'IDENTITY_DB_USER', 'IDENTITY_DB_PASSWORD')
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    # Clear any inherited connection string so this command targets the Compose database parsed above.
    $env:ConnectionStrings__IdentityDatabase = $null
    $env:IDENTITY_DB_NAME = $databaseName
    $env:IDENTITY_DB_USER = $databaseUser
    $env:IDENTITY_DB_PASSWORD = $databasePassword

    & dotnet ef database update --project $gatewayProject --startup-project $gatewayProject --context GatewayIdentityDbContext
    if ($LASTEXITCODE -ne 0) {
        throw 'Gateway Identity migration failed. The Identity database volume was preserved; inspect the EF error and retry after correcting it.'
    }
} finally {
    foreach ($name in $environmentNames) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}

Write-Output "Gateway Identity migrations applied to the '$ComposeProjectName' Compose project's Identity database."
