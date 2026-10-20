#requires -Version 7.0
param(
    [string]$ComposeProjectName = 'job-application-agent',
    [switch]$IsolatedDatabase
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
. (Join-Path $PSScriptRoot 'Use-LocalAgents.ps1')
$listener = @(Get-NetTCPConnection -LocalPort 5008 -State Listen -ErrorAction SilentlyContinue)
if (-not $listener.Count) {
    & (Join-Path $PSScriptRoot 'Start-LocalAgents.ps1') -ComposeProjectName $ComposeProjectName -IsolatedDatabase:$IsolatedDatabase
    return
}
$statePath = Join-Path $repositoryRoot '.local/agents/api-process.json'
$state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
if ($state.Configuration -cne 'LocalAgents' -or @($listener.OwningProcess | Select-Object -Unique).Count -ne 1 -or $listener[0].OwningProcess -ne $state.ProcessId) {
    throw 'The listener does not match the instance created by Start-LocalAgents; no process was stopped.'
}
$apiDll = Join-Path $repositoryRoot 'src/services/Agents/JobApplicationAgent.Agents.Api/bin/LocalAgents/net10.0/JobApplicationAgent.Agents.Api.dll'
$process = Get-CimInstance Win32_Process -Filter "ProcessId=$($state.ProcessId)"
if (-not $process -or -not $process.CommandLine.Contains($apiDll)) { throw 'Unexpected executable; no process was stopped.' }
$headers = @{ Authorization = "Bearer $env:Agents__LocalAuth__Token" }
$before = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/budget' -Headers $headers -TimeoutSec 5
Stop-Process -Id $state.ProcessId -ErrorAction Stop
& (Join-Path $PSScriptRoot 'Start-LocalAgents.ps1') -ComposeProjectName $ComposeProjectName -IsolatedDatabase:$IsolatedDatabase
$after = Invoke-RestMethod 'http://127.0.0.1:5008/api/v1/agents/budget' -Headers $headers -TimeoutSec 5
foreach ($field in @('maxTokens','maxUsd','chargedTokens','chargedUsd','heldTokens','heldUsd')) {
    if ($before.$field -ne $after.$field) { throw 'Budget changed during restart; inspect concurrent activity and persisted runs.' }
}
Write-Output 'API Agents redémarrée avec le code courant ; identité et budget conservés.'
