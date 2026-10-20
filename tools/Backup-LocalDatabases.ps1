#requires -Version 7.0
param(
    [string]$BackupDirectory,
    [string]$ComposeProjectName = 'job-application-agent',
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
    throw "Environment file not found: $resolvedEnvFile. Copy .env.example to .env and configure the local database credentials first."
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in $composeFiles) {
    $composePrefix += @('-f', (Join-Path $repositoryRoot $composeFile))
}

$composeJsonLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the local database configuration from Compose; no backup was created.'
}

try {
    $composeConfiguration = (($composeJsonLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the local database configuration from Compose; no backup was created.'
}

$databaseServices = @(
    $composeConfiguration.services.PSObject.Properties |
        Where-Object {
            $_.Name.EndsWith('-db', [StringComparison]::Ordinal) -and
            $_.Value.environment.POSTGRES_DB -and
            $_.Value.environment.POSTGRES_USER
        } |
        Sort-Object Name
)
if ($databaseServices.Count -ne 8) {
    throw "Expected the eight configured PostgreSQL services but found $($databaseServices.Count); no backup was created."
}

if ([string]::IsNullOrWhiteSpace($BackupDirectory)) {
    $backupParent = Join-Path $repositoryRoot 'backups/local'
    $BackupDirectory = Join-Path $backupParent (Get-Date -Format 'yyyyMMdd-HHmmss-fff')
}
$resolvedBackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
if (Test-Path -LiteralPath $resolvedBackupDirectory) {
    throw "Backup destination already exists; refusing to overwrite it: $resolvedBackupDirectory"
}
$null = New-Item -ItemType Directory -Path $resolvedBackupDirectory -Force

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect Compose services. Start Docker Desktop and the database services, then retry.'
}
$runningServices = @($runningServices | ForEach-Object { $_.Trim() } | Where-Object { $_ })

$backupId = [Guid]::NewGuid().ToString('N')
$manifestEntries = [System.Collections.Generic.List[object]]::new()
foreach ($service in $databaseServices) {
    $serviceName = [string]$service.Name
    if ($serviceName -notin $runningServices) {
        throw "PostgreSQL service '$serviceName' is not running. Start it before backing up; existing data was not changed."
    }

    $databaseName = [string]$service.Value.environment.POSTGRES_DB
    $databaseUser = [string]$service.Value.environment.POSTGRES_USER
    $containerDumpPath = "/tmp/job-application-agent-backup-$backupId.dump"
    $localDumpPath = Join-Path $resolvedBackupDirectory "$serviceName.dump"

    & docker compose @composePrefix exec -T --user postgres $serviceName pg_isready --username $databaseUser --dbname $databaseName *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL service '$serviceName' is not ready; no source data was changed."
    }

    try {
        & docker compose @composePrefix exec -T --user postgres $serviceName pg_dump --username $databaseUser --format=custom --no-owner --no-acl --file $containerDumpPath --dbname $databaseName
        if ($LASTEXITCODE -ne 0) {
            throw "pg_dump failed for '$serviceName'."
        }

        & docker compose @composePrefix cp "${serviceName}:$containerDumpPath" $localDumpPath
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $localDumpPath -PathType Leaf)) {
            throw "Could not copy the '$serviceName' dump to the backup directory."
        }

        $dumpInfo = Get-Item -LiteralPath $localDumpPath
        if ($dumpInfo.Length -eq 0) {
            throw "The '$serviceName' dump is empty."
        }
        $null = & docker compose @composePrefix cp $localDumpPath "${serviceName}:$containerDumpPath"
        if ($LASTEXITCODE -ne 0) {
            throw "Could not stage the '$serviceName' dump for archive validation."
        }
        $null = & docker compose @composePrefix exec -T --user postgres $serviceName pg_restore --list $containerDumpPath
        if ($LASTEXITCODE -ne 0) {
            throw "The '$serviceName' dump is not a readable PostgreSQL archive."
        }

        $hash = (Get-FileHash -LiteralPath $localDumpPath -Algorithm SHA256).Hash
        $manifestEntries.Add([pscustomobject]@{
            Service = $serviceName
            Database = $databaseName
            File = [IO.Path]::GetFileName($localDumpPath)
            LengthBytes = $dumpInfo.Length
            Sha256 = $hash
        })
        Write-Output "Backed up $serviceName ($databaseName, $($dumpInfo.Length) bytes)."
    } finally {
        & docker compose @composePrefix exec -T --user postgres $serviceName rm -f $containerDumpPath *> $null
    }
}

$manifest = [pscustomobject]@{
    FormatVersion = 1
    CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
    ComposeProjectName = $ComposeProjectName
    Databases = @($manifestEntries)
}
$manifestPath = Join-Path $resolvedBackupDirectory 'manifest.json'
$manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
Write-Output "Backup complete: $resolvedBackupDirectory"
Write-Warning 'PostgreSQL dumps contain application data. Keep this directory private and outside source control.'
