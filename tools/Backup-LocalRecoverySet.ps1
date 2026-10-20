#requires -Version 7.0

[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$BackupDirectory,

    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
    [string]$ComposeProjectName = 'job-application-agent',

    [string]$EnvFile = '.env',

    [switch]$AllowServiceRestart
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
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

if (-not $AllowServiceRestart) {
    throw 'A coherent recovery set briefly stops running application services and RabbitMQ. Re-run with -AllowServiceRestart during a maintenance window.'
}
if (-not [IO.Path]::IsPathFullyQualified($BackupDirectory)) {
    throw 'BackupDirectory must be a fully qualified path outside the repository.'
}
if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. Configure the local Compose services first."
}

$resolvedBackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
$repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if ($resolvedBackupDirectory.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($resolvedBackupDirectory.TrimEnd('\', '/'), $repositoryRoot.TrimEnd('\', '/'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Recovery archives contain personal data and must be stored outside the repository.'
}
if (Test-Path -LiteralPath $resolvedBackupDirectory) {
    throw "Recovery-set destination already exists; refusing to overwrite it: $resolvedBackupDirectory"
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in $composeFiles) {
    $composePath = Join-Path $repositoryRoot $composeFile
    if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
        throw "Compose file is missing: $composeFile"
    }
    $composePrefix += @('-f', $composePath)
}
$recoveryOverridePath = Join-Path $repositoryRoot '.local/rabbitmq-recovery.yml'
if (Test-Path -LiteralPath $recoveryOverridePath -PathType Leaf) {
    $composePrefix += @('-f', $recoveryOverridePath)
}

$configurationLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the full Compose configuration; no service was stopped and no backup was created.'
}
try {
    $configuration = (($configurationLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the full Compose configuration; no service was stopped and no backup was created.'
}

$databaseServices = @(
    $configuration.services.PSObject.Properties |
        Where-Object { $_.Name -match '^[a-z0-9][a-z0-9_-]*-db$' } |
        Sort-Object Name |
        ForEach-Object { [string]$_.Name }
)
if ($databaseServices.Count -ne 8 -or -not $configuration.services.rabbitmq) {
    throw "Expected eight PostgreSQL services and RabbitMQ; found $($databaseServices.Count) databases. No service was stopped."
}

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect running Compose services; no service was stopped and no backup was created.'
}
$runningServices = @($runningServices | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($requiredService in @($databaseServices) + @('rabbitmq')) {
    if ($requiredService -notin $runningServices) {
        throw "Required stateful service '$requiredService' is not running; no service was stopped and no backup was created."
    }
}
$statefulServices = @($databaseServices) + @('rabbitmq')
$applicationServices = @($runningServices | Where-Object { $_ -notin $statefulServices })
$postgresBackupDirectory = Join-Path $resolvedBackupDirectory 'postgresql'
$rabbitMqBackupDirectory = Join-Path $resolvedBackupDirectory 'rabbitmq'

$action = "Create PostgreSQL and RabbitMQ backups under '$resolvedBackupDirectory' and briefly stop running application services"
if (-not $PSCmdlet.ShouldProcess("Compose project '$ComposeProjectName'", $action)) {
    return
}

$servicesMayBeStopped = $false
$setDirectoryCreated = $false
$setComplete = $false
try {
    $parentDirectory = Split-Path -Parent $resolvedBackupDirectory
    if (-not (Test-Path -LiteralPath $parentDirectory -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $parentDirectory -Force
    }
    $null = New-Item -ItemType Directory -Path $resolvedBackupDirectory
    $setDirectoryCreated = $true

    if ($applicationServices.Count -gt 0) {
        $servicesMayBeStopped = $true
        & docker compose @composePrefix stop --timeout 60 @applicationServices
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not stop every running application service; the backup set is incomplete.'
        }
    }

    & (Join-Path $PSScriptRoot 'Backup-LocalDatabases.ps1') -BackupDirectory $postgresBackupDirectory -ComposeProjectName $ComposeProjectName -EnvFile $resolvedEnvFile
    & (Join-Path $PSScriptRoot 'Test-LocalDatabaseBackup.ps1') -BackupDirectory $postgresBackupDirectory -ComposeProjectName $ComposeProjectName -EnvFile $resolvedEnvFile

    & (Join-Path $PSScriptRoot 'Backup-LocalRabbitMqVolume.ps1') -BackupDirectory $rabbitMqBackupDirectory -ComposeProjectName $ComposeProjectName -EnvFile $resolvedEnvFile -AllowServiceRestart
    & (Join-Path $PSScriptRoot 'Test-LocalRabbitMqBackup.ps1') -BackupDirectory $rabbitMqBackupDirectory -ComposeProjectName $ComposeProjectName -EnvFile $resolvedEnvFile -AllowServiceRestart

    $postgresManifestPath = Join-Path $postgresBackupDirectory 'manifest.json'
    $rabbitMqManifestPath = Join-Path $rabbitMqBackupDirectory 'manifest.json'
    foreach ($manifestPath in @($postgresManifestPath, $rabbitMqManifestPath)) {
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
            throw "Expected backup manifest was not created: $manifestPath"
        }
    }

    $gitCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null | Select-Object -First 1)
    if ($LASTEXITCODE -ne 0) {
        $gitCommit = $null
    }
    $gitStatus = & git -C $repositoryRoot status --porcelain 2>$null
    $workingTreeClean = $LASTEXITCODE -eq 0 -and [string]::IsNullOrWhiteSpace(($gitStatus -join ''))
    $manifest = [pscustomobject]@{
        FormatVersion = 1
        CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
        ComposeProjectName = $ComposeProjectName
        RepositoryCommit = $gitCommit
        RepositoryWorkingTreeClean = $workingTreeClean
        DatabaseServices = $databaseServices
        DatabaseManifestSha256 = (Get-FileHash -LiteralPath $postgresManifestPath -Algorithm SHA256).Hash
        RabbitMqManifestSha256 = (Get-FileHash -LiteralPath $rabbitMqManifestPath -Algorithm SHA256).Hash
        DatabaseBackupDirectory = 'postgresql'
        RabbitMqBackupDirectory = 'rabbitmq'
        SecretsIncluded = $false
        EncryptionIncluded = $false
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $resolvedBackupDirectory 'recovery-set.json') -Encoding utf8NoBOM
    $setComplete = $true
    Write-Output "Recovery set complete: $resolvedBackupDirectory"
    Write-Warning 'The recovery set contains personal data and broker state, but no .env, secret files, certificates, TLS keys, or encryption. Store it privately with encryption and tested off-host retention.'
} catch {
    if ($setDirectoryCreated -and (Test-Path -LiteralPath $resolvedBackupDirectory -PathType Container)) {
        try {
            'INCOMPLETE — do not use this directory as a recovery set.' | Set-Content -LiteralPath (Join-Path $resolvedBackupDirectory 'INCOMPLETE.txt') -Encoding utf8NoBOM
        } catch {
            Write-Warning "The recovery-set backup is incomplete and its status marker could not be written. Inspect '$resolvedBackupDirectory' before using or removing it."
        }
    }
    throw
} finally {
    if ($servicesMayBeStopped -and $applicationServices.Count -gt 0) {
        & docker compose @composePrefix start @applicationServices
        if ($LASTEXITCODE -ne 0) {
            throw 'The backup operation ended, but one or more application services could not be restarted. Run docker compose start for the listed services and inspect their logs.'
        }
    }
}

if (-not $setComplete) {
    throw "Recovery set was not completed. Inspect '$resolvedBackupDirectory' before retrying."
}
