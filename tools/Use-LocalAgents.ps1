#requires -Version 7.0
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$settingsPath = Join-Path $repositoryRoot '.local/agents/runtime-settings.json'
if (-not (Test-Path -LiteralPath $settingsPath)) { throw 'Run tools/Start-LocalAgents.ps1 first to initialize the local settings.' }
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
if ($settings.OwnerId -notmatch '^[0-9a-f-]{36}$' -or $settings.Token.Length -lt 32 -or $settings.DatabasePassword.Length -lt 32) {
    throw 'Invalid local Agents configuration; do not regenerate identities or credentials silently.'
}
$env:AGENTS_DB_USER = $settings.DatabaseUser
$env:AGENTS_DB_PASSWORD = $settings.DatabasePassword
$env:AGENTS_DB_NAME = 'agents_db'
$env:ConnectionStrings__AgentsDatabase = "Host=127.0.0.1;Port=5438;Database=agents_db;Username=$($settings.DatabaseUser);Password=$($settings.DatabasePassword)"
$env:Agents__LocalAuth__OwnerId = $settings.OwnerId
$env:Agents__LocalAuth__Token = $settings.Token
$env:Agents__Quota__MaxTokens = [string]$settings.MaxTokens
$env:Agents__Quota__MaxUsd = '0'
$env:Agents__JobBaseUrl = 'http://localhost:5002'
. (Join-Path $PSScriptRoot 'Use-QualifiedOllama.ps1') -ProvenancePath (Join-Path $repositoryRoot '.local/agents/qwen2.5-7b-845dbda0ea48-qualified/provenance.json')
Write-Output 'Identité, connexion et quotas Agents chargés dans ce terminal. Les secrets ne sont pas affichés.'
