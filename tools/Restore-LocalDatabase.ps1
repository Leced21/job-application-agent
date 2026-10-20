#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [string]$BackupDirectory,
    [Parameter(Mandatory)]
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*-db$')]
    [string]$DatabaseService,
    [string]$ComposeProjectName = 'job-application-agent',
    [string]$EnvFile = '.env',
    [switch]$ReplaceExisting
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
    'docker-compose.applications-api.yml',
    'docker-compose.documents.yml',
    'docker-compose.documents-api.yml',
    'docker-compose.collection.yml',
    'docker-compose.collection-api.yml',
    'docker-compose.agents.yml',
    'docker-compose.agents-api.yml',
    'docker-compose.edge.yml'
)

if (-not $ReplaceExisting) {
    throw 'Restoring replaces the selected PostgreSQL database. Review the backup and re-run with -ReplaceExisting to confirm intent.'
}
if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. Configure the local Compose services first."
}
$resolvedBackupDirectory = (Resolve-Path -LiteralPath $BackupDirectory).Path
$manifestPath = Join-Path $resolvedBackupDirectory 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'Backup manifest.json was not found; no database or service was changed.'
}

try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    throw 'Backup manifest.json is invalid; no database or service was changed.'
}
if ($manifest.FormatVersion -ne 1 -or $manifest.ComposeProjectName -cne $ComposeProjectName -or -not $manifest.Databases) {
    throw 'Backup manifest version, Compose project, or database list is invalid; no database or service was changed.'
}

$matchingEntries = @($manifest.Databases | Where-Object { $_.Service -ceq $DatabaseService })
if ($matchingEntries.Count -ne 1) {
    throw "The backup must contain exactly one entry for '$DatabaseService'; no database or service was changed."
}
$entry = $matchingEntries[0]
if ([IO.Path]::GetFileName([string]$entry.File) -cne [string]$entry.File) {
    throw "The backup contains an invalid path for '$DatabaseService'; no database or service was changed."
}
$dumpPath = Join-Path $resolvedBackupDirectory ([string]$entry.File)
if (-not (Test-Path -LiteralPath $dumpPath -PathType Leaf)) {
    throw "The dump for '$DatabaseService' is missing; no database or service was changed."
}
$dumpInfo = Get-Item -LiteralPath $dumpPath
if ($dumpInfo.Length -ne [long]$entry.LengthBytes -or (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash -cne [string]$entry.Sha256) {
    throw "The dump for '$DatabaseService' does not match its manifest; no database or service was changed."
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in $composeFiles) {
    $composePrefix += @('-f', (Join-Path $repositoryRoot $composeFile))
}
$composeJsonLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the local Compose configuration; no database or service was changed.'
}
try {
    $composeConfiguration = (($composeJsonLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the local Compose configuration; no database or service was changed.'
}
$serviceProperty = $composeConfiguration.services.PSObject.Properties[$DatabaseService]
if ($null -eq $serviceProperty) {
    throw "Compose does not define PostgreSQL service '$DatabaseService'; no database or service was changed."
}
$serviceConfiguration = $serviceProperty.Value
$databaseUser = [string]$serviceConfiguration.environment.POSTGRES_USER
$databaseName = [string]$serviceConfiguration.environment.POSTGRES_DB
if ([string]::IsNullOrWhiteSpace($databaseUser) -or [string]::IsNullOrWhiteSpace($databaseName) -or $databaseName -cne [string]$entry.Database) {
    throw "Compose database settings do not match the '$DatabaseService' backup entry; no database or service was changed."
}

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect Compose services; no database or service was changed.'
}
$runningServices = @($runningServices | ForEach-Object { $_.Trim() } | Where-Object { $_ })
if ($DatabaseService -notin $runningServices) {
    throw "PostgreSQL service '$DatabaseService' is not running; no database or service was changed."
}
$applicationServices = @($runningServices | Where-Object { $_ -notmatch '-db$' -and $_ -cne 'rabbitmq' -and $_ -cne 'ollama' })

$restoreId = [Guid]::NewGuid().ToString('N')
$stagingDatabase = "jobagent_restore_$($restoreId.Substring(0, 20))"
$previousDatabase = "jobagent_previous_$($restoreId.Substring(0, 20))"
$containerDumpPath = "/tmp/job-application-agent-restore-$restoreId.dump"
if (-not $PSCmdlet.ShouldProcess("$DatabaseService/$databaseName", "Restore the backup and retain the current database as '$previousDatabase'")) {
    return
}

$databaseCreated = $false
$previousDatabaseRenamed = $false
$restorePromoted = $false
$servicesStopped = $false
try {
    & docker compose @composePrefix cp $dumpPath "${DatabaseService}:$containerDumpPath"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not stage the '$DatabaseService' dump in its PostgreSQL container."
    }
    & docker compose @composePrefix exec -T --user postgres $DatabaseService pg_restore --list $containerDumpPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "The '$DatabaseService' dump is not a readable PostgreSQL archive."
    }

    & docker compose @composePrefix exec -T --user postgres $DatabaseService createdb --username $databaseUser --owner $databaseUser $stagingDatabase
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create a staging database for '$DatabaseService'; the current database was not changed."
    }
    $databaseCreated = $true

    & docker compose @composePrefix exec -T --user postgres $DatabaseService pg_restore --exit-on-error --no-owner --no-acl --username $databaseUser --dbname $stagingDatabase $containerDumpPath
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL could not restore the '$DatabaseService' backup into its staging database; the current database was not changed."
    }
    $tableCountOutput = & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --tuples-only --no-align --username $databaseUser --dbname $stagingDatabase --command "SELECT count(*) FROM pg_catalog.pg_tables WHERE schemaname NOT IN ('pg_catalog', 'information_schema')"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($tableCountOutput -join '').Trim())) {
        throw "The staged '$DatabaseService' database could not be queried; the current database was not changed."
    }
    $tableCount = [int](($tableCountOutput -join '').Trim())
    if ($tableCount -lt 1) {
        throw "The staged '$DatabaseService' database contains no application tables; the current database was not changed."
    }

    $databaseExistsOutput = & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --tuples-only --no-align --username $databaseUser --dbname postgres --command "SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_catalog.pg_database WHERE datname = '$($databaseName.Replace("'", "''"))') THEN 't' ELSE 'f' END"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not inspect the existing '$databaseName' database; it was not changed."
    }
    $databaseExists = (($databaseExistsOutput -join '').Trim() -ceq 't')
    $previousExistsOutput = & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --tuples-only --no-align --username $databaseUser --dbname postgres --command "SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_catalog.pg_database WHERE datname = '$previousDatabase') THEN 't' ELSE 'f' END"
    if ($LASTEXITCODE -ne 0) {
        throw "Could not verify the rollback database name '$previousDatabase'; the current database was not changed."
    }
    if (($previousExistsOutput -join '').Trim() -ceq 't') {
        throw "Rollback database '$previousDatabase' already exists; choose another restore attempt. The current database was not changed."
    }

    if ($applicationServices.Count -gt 0) {
        $servicesStopped = $true
        & docker compose @composePrefix stop --timeout 60 @applicationServices
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not stop every running application service; the current database was not changed.'
        }
    }

    $quotedDatabaseName = '"' + $databaseName.Replace('"', '""') + '"'
    $quotedStagingDatabase = '"' + $stagingDatabase + '"'
    $quotedPreviousDatabase = '"' + $previousDatabase + '"'
    $escapedDatabaseName = $databaseName.Replace("'", "''")
    $terminateConnections = "SELECT pg_terminate_backend(pid) FROM pg_catalog.pg_stat_activity WHERE datname = '$escapedDatabaseName' AND pid <> pg_backend_pid()"
    & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --set ON_ERROR_STOP=1 --username $databaseUser --dbname postgres --command $terminateConnections | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not disconnect clients from '$databaseName'; its contents were not changed."
    }

    if ($databaseExists) {
        $renamePrevious = "ALTER DATABASE $quotedDatabaseName RENAME TO $quotedPreviousDatabase"
        & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --set ON_ERROR_STOP=1 --username $databaseUser --dbname postgres --command $renamePrevious | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not preserve the existing '$databaseName' database; it was not changed."
        }
        $previousDatabaseRenamed = $true
    }

    $renameStaging = "ALTER DATABASE $quotedStagingDatabase RENAME TO $quotedDatabaseName"
    & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --set ON_ERROR_STOP=1 --username $databaseUser --dbname postgres --command $renameStaging | Out-Null
    if ($LASTEXITCODE -ne 0) {
        if ($previousDatabaseRenamed) {
            $restorePrevious = "ALTER DATABASE $quotedPreviousDatabase RENAME TO $quotedDatabaseName"
            & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --set ON_ERROR_STOP=1 --username $databaseUser --dbname postgres --command $restorePrevious | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $previousDatabaseRenamed = $false
            } else {
                Write-Warning "Could not rename the preserved database '$previousDatabase' back to '$databaseName'; keep both databases and inspect PostgreSQL before restarting applications."
            }
        }
        throw "Could not promote the restored '$DatabaseService' database."
    }
    $restorePromoted = $true
} catch {
    if ($previousDatabaseRenamed -and -not $restorePromoted) {
        $targetExistsOutput = & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --tuples-only --no-align --username $databaseUser --dbname postgres --command "SELECT CASE WHEN EXISTS (SELECT 1 FROM pg_catalog.pg_database WHERE datname = '$($databaseName.Replace("'", "''"))') THEN 't' ELSE 'f' END"
        if ($LASTEXITCODE -eq 0 -and (($targetExistsOutput -join '').Trim() -cne 't')) {
            $restorePrevious = "ALTER DATABASE $quotedPreviousDatabase RENAME TO $quotedDatabaseName"
            & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc --set ON_ERROR_STOP=1 --username $databaseUser --dbname postgres --command $restorePrevious | Out-Null
            if ($LASTEXITCODE -eq 0) {
                $previousDatabaseRenamed = $false
            } else {
                Write-Warning "Could not roll back to '$previousDatabase'; keep the preserved database and inspect PostgreSQL before restarting applications."
            }
        } else {
            Write-Warning "The prior database is preserved as '$previousDatabase'; inspect PostgreSQL before removing either database."
        }
    }
    throw
} finally {
    if ($databaseCreated -and -not $restorePromoted) {
        & docker compose @composePrefix exec -T --user postgres $DatabaseService dropdb --if-exists --username $databaseUser $stagingDatabase *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not remove staging database '$stagingDatabase'; inspect it before retrying."
        }
    }
    & docker compose @composePrefix exec -T --user postgres $DatabaseService rm -f $containerDumpPath *> $null
    if ($servicesStopped -and $applicationServices.Count -gt 0) {
        & docker compose @composePrefix start @applicationServices
        if ($LASTEXITCODE -ne 0) {
            Write-Warning 'The database operation finished, but one or more application containers did not restart. Inspect Compose status and service logs.'
        }
    }
}

if ($restorePromoted) {
    if ($previousDatabaseRenamed) {
        Write-Output "Restore complete: '$DatabaseService' now uses the verified backup. Previous data is retained in '$previousDatabase'."
    } else {
        Write-Output "Restore complete: '$DatabaseService' now uses the verified backup; no previous database existed to retain."
    }
}
