#requires -Version 7.0
param(
    [Parameter(Mandatory)]
    [string]$BackupDirectory,
    [string]$ComposeProjectName = 'job-application-agent',
    [string[]]$DatabaseService,
    [string]$EnvFile = '.env'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$resolvedEnvFile = if ([IO.Path]::IsPathRooted($EnvFile)) {
    [IO.Path]::GetFullPath($EnvFile)
} else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $EnvFile))
}
$composeFiles = @(
    'docker-compose.yml',
    'docker-compose.identity.yml',
    'docker-compose.applications.yml',
    'docker-compose.documents.yml',
    'docker-compose.collection.yml',
    'docker-compose.agents.yml'
)

if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. Configure the local Compose database services first."
}
$resolvedBackupDirectory = (Resolve-Path -LiteralPath $BackupDirectory).Path
$manifestPath = Join-Path $resolvedBackupDirectory 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'Backup manifest.json was not found; no database was created or changed.'
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    throw 'Backup manifest.json is invalid; no database was created or changed.'
}
if ($manifest.FormatVersion -ne 1 -or $manifest.ComposeProjectName -cne $ComposeProjectName -or -not $manifest.Databases) {
    throw 'Backup manifest version, Compose project, or database list is invalid; no database was created or changed.'
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in $composeFiles) {
    $composePrefix += @('-f', (Join-Path $repositoryRoot $composeFile))
}
$composeJsonLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the local database configuration; no database was created or changed.'
}
try {
    $composeConfiguration = (($composeJsonLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the local database configuration; no database was created or changed.'
}

$selectedEntries = @($manifest.Databases)
if ($DatabaseService) {
    $selectedEntries = @($selectedEntries | Where-Object { $_.Service -in $DatabaseService })
    if ($selectedEntries.Count -ne @($DatabaseService | Sort-Object -Unique).Count) {
        throw 'One or more requested database services are not present in this backup.'
    }
}
if ($selectedEntries.Count -eq 0) {
    throw 'The backup contains no database entries to test.'
}

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect Compose services. Start Docker Desktop and the database services, then retry.'
}
$runningServices = @($runningServices | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$validatedEntries = [System.Collections.Generic.List[object]]::new()
foreach ($entry in $selectedEntries) {
    $serviceName = [string]$entry.Service
    if ($serviceName -notmatch '^[a-z0-9][a-z0-9_-]*-db$' -or $serviceName -notin $composeConfiguration.services.PSObject.Properties.Name) {
        throw "The backup contains an unsupported PostgreSQL service '$serviceName'."
    }
    if ($serviceName -notin $runningServices) {
        throw "PostgreSQL service '$serviceName' is not running; no database was created."
    }
    if ([IO.Path]::GetFileName([string]$entry.File) -cne [string]$entry.File) {
        throw "The backup contains an invalid path for '$serviceName'; no database was created."
    }
    $localDumpPath = Join-Path $resolvedBackupDirectory ([string]$entry.File)
    if (-not (Test-Path -LiteralPath $localDumpPath -PathType Leaf)) {
        throw "The dump file for '$serviceName' is missing; no database was created."
    }
    $dumpInfo = Get-Item -LiteralPath $localDumpPath
    if ($dumpInfo.Length -ne [long]$entry.LengthBytes -or (Get-FileHash -LiteralPath $localDumpPath -Algorithm SHA256).Hash -cne [string]$entry.Sha256) {
        throw "The dump file for '$serviceName' does not match its manifest; no database was created."
    }

    $serviceConfiguration = $composeConfiguration.services.$serviceName
    $databaseUser = [string]$serviceConfiguration.environment.POSTGRES_USER
    $databaseName = [string]$serviceConfiguration.environment.POSTGRES_DB
    if ([string]::IsNullOrWhiteSpace($databaseUser) -or [string]::IsNullOrWhiteSpace($databaseName) -or $databaseName -cne [string]$entry.Database) {
        throw "Compose database settings do not match the '$serviceName' backup entry; no database was created."
    }

    $validatedEntries.Add([pscustomobject]@{
        Service = $serviceName
        Database = $databaseName
        User = $databaseUser
        DumpPath = $localDumpPath
    })
}

foreach ($entry in $validatedEntries) {
    $serviceName = [string]$entry.Service
    $databaseName = [string]$entry.Database
    $databaseUser = [string]$entry.User
    $localDumpPath = [string]$entry.DumpPath
    $restoreId = [Guid]::NewGuid().ToString('N')
    $temporaryDatabase = "restorecheck_$($restoreId.Substring(0, 16))"
    $containerDumpPath = "/tmp/job-application-agent-restore-$restoreId.dump"
    $databaseCreated = $false
    try {
        & docker compose @composePrefix cp $localDumpPath "${serviceName}:$containerDumpPath"
        if ($LASTEXITCODE -ne 0) {
            throw "Could not stage the '$serviceName' dump in its PostgreSQL container."
        }

        & docker compose @composePrefix exec -T --user postgres $serviceName createdb --username $databaseUser --owner $databaseUser $temporaryDatabase
        if ($LASTEXITCODE -ne 0) {
            throw "Could not create the isolated restore-check database for '$serviceName'."
        }
        $databaseCreated = $true

        & docker compose @composePrefix exec -T --user postgres $serviceName pg_restore --exit-on-error --no-owner --no-acl --username $databaseUser --dbname $temporaryDatabase $containerDumpPath
        if ($LASTEXITCODE -ne 0) {
            throw "PostgreSQL could not restore the '$serviceName' backup into its isolated check database."
        }

        $tableCountOutput = & docker compose @composePrefix exec -T --user postgres $serviceName psql -X --no-psqlrc --tuples-only --no-align --username $databaseUser --dbname $temporaryDatabase --command "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema')"
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($tableCountOutput -join '').Trim())) {
            throw "The restored '$serviceName' database could not be queried."
        }
        $tableCount = [int](($tableCountOutput -join '').Trim())
        Write-Output "Restore verified: $serviceName restored into a temporary database ($tableCount user tables)."
    } finally {
        if ($databaseCreated) {
            & docker compose @composePrefix exec -T --user postgres $serviceName dropdb --if-exists --username $databaseUser $temporaryDatabase *> $null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Could not remove the isolated restore-check database '$temporaryDatabase' from '$serviceName'."
            }
        }
        & docker compose @composePrefix exec -T --user postgres $serviceName rm -f $containerDumpPath *> $null
    }
}

Write-Output 'All selected database backups were restored and queried successfully. The original databases were not modified.'
