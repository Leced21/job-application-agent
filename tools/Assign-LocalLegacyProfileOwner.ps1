#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [Guid]$OwnerId,

    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
    [string]$ComposeProjectName = 'job-application-agent',

    [string]$EnvFile = '.env',

    [switch]$AllowDataUpdate
)

$ErrorActionPreference = 'Stop'
if (-not $AllowDataUpdate) {
    throw 'Assigning a legacy profile changes candidate data. Back up Profile first, inspect with -WhatIf, then re-run with -AllowDataUpdate.'
}
if ($OwnerId -eq [Guid]::Empty) {
    throw 'OwnerId must be a non-empty GUID. Use the APPLICATIONS_OWNER_ID configured for the account that should own the existing profile.'
}

$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resolvedEnvFile = if ([IO.Path]::IsPathRooted($EnvFile)) {
    [IO.Path]::GetFullPath($EnvFile)
} else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $EnvFile))
}
if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. No database was changed."
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in @(
    'docker-compose.yml',
    'docker-compose.identity.yml',
    'docker-compose.applications.yml',
    'docker-compose.applications-api.yml',
    'docker-compose.documents.yml',
    'docker-compose.documents-api.yml',
    'docker-compose.collection.yml',
    'docker-compose.collection-api.yml',
    'docker-compose.agents.yml',
    'docker-compose.agents-api.yml',
    'docker-compose.edge.yml'
)) {
    $composePath = Join-Path $repositoryRoot $composeFile
    if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
        throw "Compose file is missing: $composeFile. No database was changed."
    }
    $composePrefix += @('-f', $composePath)
}
$configurationLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the Compose database configuration. No database was changed.'
}
try {
    $configuration = (($configurationLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the Compose database configuration. No database was changed.'
}

$profileDatabase = $configuration.services.'profile-db'
if ($null -eq $profileDatabase) {
    throw "Compose project '$ComposeProjectName' does not define profile-db. No database was changed."
}
$databaseName = [string]$profileDatabase.environment.POSTGRES_DB
$databaseUser = [string]$profileDatabase.environment.POSTGRES_USER
if ([string]::IsNullOrWhiteSpace($databaseName) -or [string]::IsNullOrWhiteSpace($databaseUser)) {
    throw 'Compose does not provide the Profile database name and user. No database was changed.'
}
$gatewayInitialOwner = [string]$configuration.services.'gateway-api'.environment.Identity__InitialOwnerId
$gatewayInitialOwnerId = [Guid]::Empty
if (-not [Guid]::TryParse($gatewayInitialOwner, [ref]$gatewayInitialOwnerId) -or $gatewayInitialOwnerId -ne $OwnerId) {
    throw 'OwnerId must match Gateway Identity:InitialOwnerId from this Compose environment. No database was changed.'
}

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect Compose services. Start Docker Desktop and Profile PostgreSQL, then retry.'
}
$runningServices = @($runningServices | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ('profile-db' -notin $runningServices) {
    throw "PostgreSQL service 'profile-db' is not running. No database was changed."
}

function Invoke-ProfileOwnerQuery {
    param([Parameter(Mandatory)][string]$Sql)

    $output = & docker compose @composePrefix exec -T --user postgres profile-db psql -X --no-psqlrc `
        --set ON_ERROR_STOP=1 --tuples-only --no-align --field-separator '|' `
        --username $databaseUser --dbname $databaseName --command $Sql
    if ($LASTEXITCODE -ne 0) {
        throw 'Profile owner inspection failed. No database was changed.'
    }
    return @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
}

$countsOutput = @(Invoke-ProfileOwnerQuery @'
SELECT COUNT(*)::text || '|' ||
       COUNT(*) FILTER (WHERE owner_id IS NULL)::text || '|' ||
       COUNT(DISTINCT owner_id)::text
FROM candidate_profiles;
'@)
if ($countsOutput.Count -ne 1) {
    throw 'Could not read the Profile ownership counts. No database was changed.'
}
$counts = $countsOutput[0].Split('|')
if ($counts.Count -ne 3) {
    throw 'Profile ownership counts had an unexpected format. No database was changed.'
}
$profileCount = [long]$counts[0]
$unownedCount = [long]$counts[1]
$distinctOwnerCount = [long]$counts[2]

if ($unownedCount -eq 0) {
    Write-Output "No legacy profile needs an owner assignment. Profiles: $profileCount; distinct owners: $distinctOwnerCount. No data changed."
    return
}
if ($unownedCount -ne 1) {
    throw "Found $unownedCount profiles without an owner. Automatic assignment is ambiguous; no data changed. Review the profiles on a protected copy and assign them individually."
}

$unownedProfileIds = @(Invoke-ProfileOwnerQuery 'SELECT "Id"::text FROM candidate_profiles WHERE owner_id IS NULL ORDER BY "Id";')
if ($unownedProfileIds.Count -ne 1 -or $unownedProfileIds[0] -notmatch '^[a-f0-9-]{36}$') {
    throw 'The unowned profile could not be identified unambiguously. No data was changed.'
}
$profileId = [Guid]::Parse($unownedProfileIds[0])
$existingOwnerCountOutput = @(Invoke-ProfileOwnerQuery "SELECT COUNT(*)::text FROM candidate_profiles WHERE owner_id = '$($OwnerId.ToString('D'))'::uuid;")
if ($existingOwnerCountOutput.Count -ne 1 -or [long]$existingOwnerCountOutput[0] -ne 0) {
    throw "Owner '$OwnerId' already has a Profile record. No data was changed."
}

$action = "Assign the single unowned legacy profile '$profileId' to owner '$($OwnerId.ToString('D'))'"
if (-not $PSCmdlet.ShouldProcess("Profile database '$databaseName'", $action)) {
    return
}

$updateSql = 'UPDATE candidate_profiles SET owner_id = ''{0}''::uuid WHERE "Id" = ''{1}''::uuid AND owner_id IS NULL;' -f $OwnerId.ToString('D'), $profileId.ToString('D')
$null = Invoke-ProfileOwnerQuery $updateSql
$updatedProfileOutput = @(Invoke-ProfileOwnerQuery ('SELECT COUNT(*)::text FROM candidate_profiles WHERE "Id" = ''{0}''::uuid AND owner_id = ''{1}''::uuid;' -f $profileId.ToString('D'), $OwnerId.ToString('D')))
if ($updatedProfileOutput.Count -ne 1 -or [long]$updatedProfileOutput[0] -ne 1) {
    throw 'The profile changed during assignment; expected exactly one row to be updated. Inspect the database before retrying.'
}

$verificationOutput = @(Invoke-ProfileOwnerQuery "SELECT COUNT(*) FILTER (WHERE owner_id = '$($OwnerId.ToString('D'))'::uuid)::text || '|' || COUNT(*) FILTER (WHERE owner_id IS NULL)::text FROM candidate_profiles;")
if ($verificationOutput.Count -ne 1) {
    throw 'The owner assignment completed, but its verification query failed. Inspect the Profile database before restarting services.'
}
$verification = $verificationOutput[0].Split('|')
if ($verification.Count -ne 2 -or [long]$verification[0] -lt 1 -or [long]$verification[1] -ne ($unownedCount - 1)) {
    throw 'The owner assignment completed, but the resulting ownership counts did not match expectations. Inspect the Profile database before restarting services.'
}

Write-Output "Assigned the legacy Profile record to '$($OwnerId.ToString('D'))' and verified the ownership counts."
