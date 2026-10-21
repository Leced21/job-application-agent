#requires -Version 5.1
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scriptPath = Join-Path $repositoryRoot 'tools/Get-LocalOwnerDataInventory.ps1'
$environmentFile = Join-Path $repositoryRoot '.env.example'
$expectedServices = @('profile-db', 'job-db', 'matching-db', 'applications-db', 'documents-db', 'collection-db', 'agents-db', 'identity-db')

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$global:OwnerInventoryMock = [pscustomobject]@{
    RunningServices = $expectedServices
    Calls = [Collections.Generic.List[object]]::new()
    MutatingCalls = 0
}

function global:docker {
    $arguments = @($args | ForEach-Object { [string]$_ })
    $global:OwnerInventoryMock.Calls.Add($arguments)
    $global:LASTEXITCODE = 0
    if ('config' -in $arguments) {
        $services = [ordered]@{}
        foreach ($serviceName in $expectedServices) {
            $db = switch ($serviceName) {
                'profile-db' { 'profile_db' }
                'job-db' { 'job_db' }
                'matching-db' { 'matching_db' }
                'applications-db' { 'applications_db' }
                'documents-db' { 'documents_db' }
                'collection-db' { 'collection_db' }
                'agents-db' { 'agents_db' }
                'identity-db' { 'identity_db' }
            }
            $services[$serviceName] = @{ environment = @{ POSTGRES_DB = $db; POSTGRES_USER = 'local_user' } }
        }
        return @{ services = $services } | ConvertTo-Json -Depth 10 -Compress
    }
    if ('ps' -in $arguments) { return $global:OwnerInventoryMock.RunningServices }
    if ('--command' -notin $arguments) { $global:LASTEXITCODE = 1; return }

    $sqlIndex = [Array]::IndexOf($arguments, '--command')
    $sql = $arguments[$sqlIndex + 1]
    if ($sql -notmatch '^\s*SELECT\b') {
        $global:OwnerInventoryMock.MutatingCalls++
        throw 'The inventory sent a command other than a read-only SELECT.'
    }
    if ($sql -match '^SELECT table_schema') {
        $serviceIndex = [Array]::IndexOf($arguments, 'exec') + 4
        $service = $arguments[$serviceIndex]
        if ($service -eq 'profile-db') { return 'public|candidate_profiles|owner_id' }
        if ($service -eq 'applications-db') { return 'public|Candidacies|OwnerId' }
        return
    }
    if ($sql -match '^SELECT COUNT\(\*\)::text') { return '2|1|<NULL>:1,44444444-4444-4444-8444-444444444444:1' }
    if ($sql -match '\b(INSERT|UPDATE|DELETE|CREATE|ALTER|DROP|TRUNCATE)\b') {
        $global:OwnerInventoryMock.MutatingCalls++
        throw 'The inventory issued a mutating SQL command.'
    }
    throw "Unexpected SQL in owner inventory test: $sql"
}

function Reset-InventoryMock {
    $global:OwnerInventoryMock.Calls.Clear()
    $global:OwnerInventoryMock.MutatingCalls = 0
    $global:OwnerInventoryMock.RunningServices = $expectedServices
}

Reset-InventoryMock
$result = & $scriptPath -EnvFile $environmentFile
Assert-True ($global:OwnerInventoryMock.MutatingCalls -eq 0) 'The inventory attempted to change database data.'
Assert-True ($global:OwnerInventoryMock.Calls.Count -ge 12) 'The inventory did not inspect every selected database.'
foreach ($serviceName in $expectedServices) {
    $serviceWasInspected = @($global:OwnerInventoryMock.Calls | Where-Object { $_ -contains $serviceName }).Count -gt 0
    Assert-True $serviceWasInspected "The inventory did not inspect '$serviceName'."
}

Reset-InventoryMock
$global:OwnerInventoryMock.RunningServices = @('profile-db')
$stoppedDatabaseRejected = $false
$stoppedDatabaseError = ''
try { & $scriptPath -EnvFile $environmentFile -Service 'profile-db', 'job-db' | Out-Null }
catch { $stoppedDatabaseError = $_.Exception.Message; $stoppedDatabaseRejected = $stoppedDatabaseError -like '*not running:*job-db*' }
Assert-True $stoppedDatabaseRejected "The inventory did not clearly reject an unavailable selected database. Actual: $stoppedDatabaseError"
Assert-True ($global:OwnerInventoryMock.Calls.Count -eq 2) 'The inventory queried databases after its running-service preflight failed.'

Remove-Item Function:\docker -ErrorAction SilentlyContinue
Remove-Variable OwnerInventoryMock -Scope Global -ErrorAction SilentlyContinue
Write-Output 'Local owner inventory safety tests passed: all selected databases are preflighted and every executed SQL query is read-only.'
