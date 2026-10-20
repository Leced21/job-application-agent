#requires -Version 7.0
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$listener = Get-NetTCPConnection -LocalPort 5002 -State Listen -ErrorAction SilentlyContinue
if ($listener) {
    $null = Invoke-RestMethod 'http://127.0.0.1:5002/api/v1/jobs?page=1&pageSize=1' -TimeoutSec 5
    Write-Output 'API Job déjà accessible sur 5002 ; aucun processus modifié.'; return
}
$apiDirectory = Join-Path $repositoryRoot 'src/services/Job/JobApplicationAgent.Job.Api'
& dotnet build $apiDirectory --configuration LocalJob -m:1 --disable-build-servers
if ($LASTEXITCODE -ne 0) { throw 'Job API compilation failed.' }
$apiDll = Join-Path $apiDirectory 'bin/LocalJob/net10.0/JobApplicationAgent.Job.Api.dll'
$logs = Join-Path $repositoryRoot '.local/agents'; [IO.Directory]::CreateDirectory($logs) | Out-Null
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$process = Start-Process -FilePath (Get-Command dotnet).Source -ArgumentList @(('"'+$apiDll+'"'),'--urls','http://127.0.0.1:5002') `
    -WorkingDirectory $apiDirectory -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logs 'job-api.stdout.log') -RedirectStandardError (Join-Path $logs 'job-api.stderr.log')
@{ProcessId=$process.Id;Url='http://127.0.0.1:5002';Configuration='LocalJob';StartedAtUtc=[DateTime]::UtcNow.ToString('O')} | ConvertTo-Json | Set-Content (Join-Path $logs 'job-api-process.json')
for($attempt=0; $attempt -lt 30; $attempt++) {
    if($process.HasExited){throw 'Job API exited; inspect .local/agents/job-api.stderr.log.'}
    try {
        $null=Invoke-RestMethod 'http://127.0.0.1:5002/api/v1/jobs?page=1&pageSize=1' -TimeoutSec 2
        Write-Output 'API Job accessible sur 5002 ; compilation LocalJob isolée. Aucune migration Job appliquée.'; return
    } catch { Start-Sleep -Seconds 1 }
}
throw 'Job API did not become ready; inspect local logs.'
