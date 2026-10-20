#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string]$RecoveryDirectory,

    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
    [string]$ComposeProjectName = 'job-application-agent',

    [string]$EnvFile = '.env',

    [string]$ArchiveHelperImage = 'alpine:3.22',

    [ValidateRange(30, 600)]
    [int]$ReadinessTimeoutSeconds = 240,

    [switch]$AllowServiceRestart
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$recoveryOverridePath = Join-Path $repositoryRoot '.local/rabbitmq-recovery.yml'
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
    throw 'Full recovery starts databases, replaces the RabbitMQ service configuration and restarts application services. Re-run with -AllowServiceRestart during a recovery window.'
}
if (-not [IO.Path]::IsPathFullyQualified($RecoveryDirectory)) {
    throw 'RecoveryDirectory must be a fully qualified path outside the repository.'
}
if (-not (Test-Path -LiteralPath $RecoveryDirectory -PathType Container)) {
    throw "Recovery directory not found: $RecoveryDirectory"
}
$resolvedRecoveryDirectory = (Resolve-Path -LiteralPath $RecoveryDirectory).Path
$repositoryPrefix = $repositoryRoot.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar
if ($resolvedRecoveryDirectory.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::Equals($resolvedRecoveryDirectory.TrimEnd('\', '/'), $repositoryRoot.TrimEnd('\', '/'), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Recovery archives contain personal data and must be read from outside the repository.'
}
if (Test-Path -LiteralPath (Join-Path $resolvedRecoveryDirectory 'INCOMPLETE.txt')) {
    throw 'This recovery set is marked incomplete and cannot be restored.'
}
if (Test-Path -LiteralPath $recoveryOverridePath) {
    throw "A RabbitMQ recovery override already exists. Review '$recoveryOverridePath' before attempting another host recovery."
}

$resolvedEnvFile = if ([IO.Path]::IsPathRooted($EnvFile)) {
    [IO.Path]::GetFullPath($EnvFile)
} else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $EnvFile))
}
if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. Restore the matching local credentials before recovery."
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in $composeFiles) {
    $composePath = Join-Path $repositoryRoot $composeFile
    if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
        throw "Compose file is missing: $composeFile"
    }
    $composePrefix += @('-f', $composePath)
}
$configurationLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the full Compose configuration; no service was started or stopped.'
}
try {
    $configuration = (($configurationLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the full Compose configuration; no service was started or stopped.'
}

$databaseServices = @(
    $configuration.services.PSObject.Properties |
        Where-Object { $_.Name -match '^[a-z0-9][a-z0-9_-]*-db$' } |
        Sort-Object Name |
        ForEach-Object { [string]$_.Name }
)
if ($databaseServices.Count -ne 8 -or -not $configuration.services.rabbitmq) {
    throw "Expected eight PostgreSQL services and RabbitMQ; found $($databaseServices.Count). No service was started or stopped."
}

$recoveryManifestPath = Join-Path $resolvedRecoveryDirectory 'recovery-set.json'
if (-not (Test-Path -LiteralPath $recoveryManifestPath -PathType Leaf)) {
    throw 'Recovery-set manifest.json was not found; no service was started or stopped.'
}
try {
    $recoveryManifest = Get-Content -LiteralPath $recoveryManifestPath -Raw | ConvertFrom-Json
} catch {
    throw 'Recovery-set manifest is invalid; no service was started or stopped.'
}
if ($recoveryManifest.FormatVersion -ne 1 -or
    $recoveryManifest.ComposeProjectName -cne $ComposeProjectName -or
    $recoveryManifest.SecretsIncluded -ne $false -or
    $recoveryManifest.EncryptionIncluded -ne $false) {
    throw 'Recovery-set format, Compose project or security metadata is invalid; no service was started or stopped.'
}
if ([string]$recoveryManifest.DatabaseBackupDirectory -cne 'postgresql' -or
    [string]$recoveryManifest.RabbitMqBackupDirectory -cne 'rabbitmq' -or
    (Compare-Object @($databaseServices | Sort-Object) @($recoveryManifest.DatabaseServices | ForEach-Object { [string]$_ } | Sort-Object)).Count -ne 0) {
    throw 'Recovery-set database services or archive paths do not match this Compose project.'
}
if (-not $recoveryManifest.RepositoryWorkingTreeClean) {
    Write-Warning "The source backup was created with uncommitted repository changes. Its recorded code commit is '$($recoveryManifest.RepositoryCommit)'; compare the deployed code and database migrations before starting APIs."
}

$postgresBackupDirectory = Join-Path $resolvedRecoveryDirectory 'postgresql'
$rabbitMqBackupDirectory = Join-Path $resolvedRecoveryDirectory 'rabbitmq'
$postgresManifestPath = Join-Path $postgresBackupDirectory 'manifest.json'
$rabbitMqManifestPath = Join-Path $rabbitMqBackupDirectory 'manifest.json'
foreach ($manifestPath in @($postgresManifestPath, $rabbitMqManifestPath)) {
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "A child backup manifest is missing: $manifestPath"
    }
}
if ((Get-FileHash -LiteralPath $postgresManifestPath -Algorithm SHA256).Hash -cne [string]$recoveryManifest.DatabaseManifestSha256 -or
    (Get-FileHash -LiteralPath $rabbitMqManifestPath -Algorithm SHA256).Hash -cne [string]$recoveryManifest.RabbitMqManifestSha256) {
    throw 'A child backup manifest does not match the recovery-set index; no service was started or stopped.'
}
try {
    $postgresManifest = Get-Content -LiteralPath $postgresManifestPath -Raw | ConvertFrom-Json
    $rabbitMqManifest = Get-Content -LiteralPath $rabbitMqManifestPath -Raw | ConvertFrom-Json
} catch {
    throw 'A child backup manifest is invalid; no service was started or stopped.'
}
if ($postgresManifest.FormatVersion -ne 1 -or
    $postgresManifest.ComposeProjectName -cne $ComposeProjectName -or
    $postgresManifest.Databases.Count -ne 8 -or
    $rabbitMqManifest.FormatVersion -ne 2 -or
    $rabbitMqManifest.ComposeProjectName -cne $ComposeProjectName -or
    $rabbitMqManifest.Service -cne 'rabbitmq') {
    throw 'This recovery set does not contain a complete PostgreSQL and portable RabbitMQ image backup.'
}
$postgresManifestServices = @($postgresManifest.Databases | ForEach-Object { [string]$_.Service } | Sort-Object)
if ((Compare-Object @($databaseServices | Sort-Object) $postgresManifestServices).Count -ne 0) {
    throw 'The PostgreSQL archive list does not contain each Compose database exactly once.'
}

foreach ($entry in $postgresManifest.Databases) {
    $serviceName = [string]$entry.Service
    if ($serviceName -notin $databaseServices -or [IO.Path]::GetFileName([string]$entry.File) -cne [string]$entry.File) {
        throw "The PostgreSQL backup contains an unsupported service or file path '$serviceName'."
    }
    $dumpPath = Join-Path $postgresBackupDirectory ([string]$entry.File)
    if (-not (Test-Path -LiteralPath $dumpPath -PathType Leaf)) {
        throw "The PostgreSQL archive for '$serviceName' is missing."
    }
    $dumpInfo = Get-Item -LiteralPath $dumpPath
    if ($dumpInfo.Length -ne [long]$entry.LengthBytes -or (Get-FileHash -LiteralPath $dumpPath -Algorithm SHA256).Hash -cne [string]$entry.Sha256) {
        throw "The PostgreSQL archive for '$serviceName' does not match its manifest."
    }
    $databaseConfiguration = $configuration.services.PSObject.Properties[$serviceName].Value
    if ([string]$databaseConfiguration.environment.POSTGRES_DB -cne [string]$entry.Database) {
        throw "The current Compose database name does not match the '$serviceName' archive."
    }
}

foreach ($archiveProperty in @(
    @{ Name = 'Archive'; Length = 'LengthBytes'; Hash = 'Sha256' },
    @{ Name = 'ImageArchive'; Length = 'ImageLengthBytes'; Hash = 'ImageSha256' }
)) {
    $archiveName = [string]$rabbitMqManifest.($archiveProperty.Name)
    if ([IO.Path]::GetFileName($archiveName) -cne $archiveName) {
        throw "RabbitMQ backup contains an invalid $($archiveProperty.Name) path."
    }
    $archivePath = Join-Path $rabbitMqBackupDirectory $archiveName
    if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw "RabbitMQ backup file '$archiveName' is missing."
    }
    $archiveInfo = Get-Item -LiteralPath $archivePath
    if ($archiveInfo.Length -ne [long]$rabbitMqManifest.($archiveProperty.Length) -or
        (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -cne [string]$rabbitMqManifest.($archiveProperty.Hash)) {
        throw "RabbitMQ backup file '$archiveName' does not match its manifest."
    }
}
if ([string]$rabbitMqManifest.Hostname -notmatch '^[A-Za-z0-9_.-]+$' -or
    [string]$rabbitMqManifest.NodeName -notmatch '^rabbit@[A-Za-z0-9_.-]+$' -or
    [string]$rabbitMqManifest.Volume -notmatch '^[A-Za-z0-9_.-]+$' -or
    [string]$rabbitMqManifest.ImageId -notmatch '^sha256:[a-f0-9]{64}$' -or
    [string]::IsNullOrWhiteSpace([string]$rabbitMqManifest.Image)) {
    throw 'RabbitMQ backup contains an unsupported node, volume or image identity.'
}
$rabbitMqMount = @($configuration.services.rabbitmq.volumes | Where-Object { $_.target -ceq '/var/lib/rabbitmq' })
if ($rabbitMqMount.Count -ne 1) {
    throw 'Compose must define exactly one RabbitMQ data volume at /var/lib/rabbitmq.'
}
if ([string]$rabbitMqMount[0].source -cne 'collection_rabbitmq_data') {
    throw 'Compose does not use the expected collection_rabbitmq_data volume key for RabbitMQ.'
}

if (-not $PSCmdlet.ShouldProcess("Compose project '$ComposeProjectName'", "Restore eight PostgreSQL databases and activate the RabbitMQ archive while preserving existing volumes and databases")) {
    return
}

function Wait-ComposeServicesHealthy {
    param(
        [Parameter(Mandatory)][string[]]$ComposeArguments,
        [Parameter(Mandatory)][string[]]$Services,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $pending = [System.Collections.Generic.List[string]]::new()
        foreach ($service in $Services) {
            $containerIds = @(& docker compose @ComposeArguments ps -q $service)
            if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($containerIds -join '').Trim())) {
                $pending.Add($service)
                continue
            }
            $statusOutput = & docker inspect --format '{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' ($containerIds | Select-Object -First 1)
            if ($LASTEXITCODE -ne 0) {
                $pending.Add($service)
                continue
            }
            $statusParts = ($statusOutput -join '').Trim().Split('|')
            if ($statusParts.Count -lt 2 -or $statusParts[0] -cne 'running' -or $statusParts[1] -cnotin @('healthy', 'none')) {
                $pending.Add($service)
            }
        }
        if ($pending.Count -eq 0) {
            return
        }
        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Services did not become healthy before timeout: $($pending -join ', '). Inspect docker compose logs."
}

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect running Compose services; no service was stopped or replaced.'
}
$runningServices = @($runningServices | ForEach-Object { $_.Trim() } | Where-Object { $_ })
$runningApplicationServices = @($runningServices | Where-Object { $_ -notmatch '-db$' -and $_ -cne 'rabbitmq' })
$applicationServices = @(
    $configuration.services.PSObject.Properties |
        Where-Object { $_.Name -notmatch '-db$' -and $_.Name -cne 'rabbitmq' } |
        Sort-Object Name |
        ForEach-Object { [string]$_.Name }
)
$recoveryVolume = "jobagent-rabbit-recovered-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
$temporaryImageTag = "jobagent-rabbitmq-recovery:$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
$helperName = "jobagent-rabbit-recovery-helper-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
$validationContainer = "jobagent-rabbit-recovery-check-$([Guid]::NewGuid().ToString('N').Substring(0, 12))"
$helperCreated = $false
$validationContainerCreated = $false
$recoveryVolumeCreated = $false
$recoveryOverrideWritten = $false
$applicationServicesStopped = $false
$applicationServicesStarting = $false
$containerArchivePath = '/tmp/rabbitmq-volume.tar.gz'

try {
    if ($runningApplicationServices.Count -gt 0) {
        $applicationServicesStopped = $true
        & docker compose @composePrefix stop --timeout 60 @runningApplicationServices
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not stop all running application services. No database restore has started.'
        }
    }

    & docker compose @composePrefix up -d @databaseServices
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not start the eight PostgreSQL services. No database has been restored.'
    }
    Wait-ComposeServicesHealthy -ComposeArguments $composePrefix -Services $databaseServices -TimeoutSeconds $ReadinessTimeoutSeconds

    & (Join-Path $PSScriptRoot 'Test-LocalDatabaseBackup.ps1') -BackupDirectory $postgresBackupDirectory -ComposeProjectName $ComposeProjectName -EnvFile $resolvedEnvFile

    $helperImageId = @(& docker image inspect --format '{{.Id}}' $ArchiveHelperImage 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($helperImageId -join '').Trim())) {
        & docker pull $ArchiveHelperImage
        if ($LASTEXITCODE -ne 0) {
            throw "Could not obtain RabbitMQ archive helper image '$ArchiveHelperImage'."
        }
    }
    $rabbitImageIds = @(& docker image inspect --format '{{.Id}}' ([string]$rabbitMqManifest.ImageId) 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($rabbitImageIds -join '').Trim() -cne [string]$rabbitMqManifest.ImageId) {
        $imageArchivePath = Join-Path $rabbitMqBackupDirectory ([string]$rabbitMqManifest.ImageArchive)
        & docker image load --input $imageArchivePath
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not load the exact RabbitMQ image from the recovery set.'
        }
        $rabbitImageIds = @(& docker image inspect --format '{{.Id}}' ([string]$rabbitMqManifest.ImageId))
        if ($LASTEXITCODE -ne 0 -or ($rabbitImageIds -join '').Trim() -cne [string]$rabbitMqManifest.ImageId) {
            throw 'The loaded RabbitMQ image does not match the image ID recorded in the recovery set.'
        }
    }
    & docker image tag ([string]$rabbitMqManifest.ImageId) $temporaryImageTag
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not create a local immutable tag for the RabbitMQ image.'
    }

    $volumeOutput = & docker volume create $recoveryVolume
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($volumeOutput -join '').Trim())) {
        throw 'Could not create a separate RabbitMQ recovery volume; the existing volume was not changed.'
    }
    $recoveryVolumeCreated = $true

    $helperOutput = & docker create --name $helperName --mount "type=volume,source=$recoveryVolume,target=/target" $ArchiveHelperImage sh -c "tar -C /target -xzpf $containerArchivePath"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($helperOutput -join '').Trim())) {
        throw "Could not create the temporary archive extraction container using '$ArchiveHelperImage'."
    }
    $helperCreated = $true
    $archivePath = Join-Path $rabbitMqBackupDirectory ([string]$rabbitMqManifest.Archive)
    & docker cp $archivePath "${helperName}:$containerArchivePath" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stage the RabbitMQ volume archive in its new volume.'
    }
    & docker start --attach $helperName *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not extract the RabbitMQ archive into the new volume; the original volume remains intact.'
    }
    & docker rm $helperName *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not remove the completed RabbitMQ archive helper.'
    }
    $helperCreated = $false

    $validationArguments = @(
        '--detach',
        '--name', $validationContainer,
        '--hostname', [string]$rabbitMqManifest.Hostname,
        '--network', 'bridge',
        '--mount', "type=volume,source=$recoveryVolume,target=/var/lib/rabbitmq"
    )
    foreach ($setting in $configuration.services.rabbitmq.environment.PSObject.Properties) {
        $validationArguments += @('--env', "$($setting.Name)=$($setting.Value)")
    }
    $validationArguments += @('--env', "RABBITMQ_NODENAME=$($rabbitMqManifest.NodeName)", $temporaryImageTag)
    $validationOutput = & docker run @validationArguments
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($validationOutput -join '').Trim())) {
        throw 'Could not start an isolated broker from the restored RabbitMQ volume.'
    }
    $validationContainerCreated = $true
    $rabbitReady = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        & docker exec $validationContainer rabbitmq-diagnostics -q ping *> $null
        if ($LASTEXITCODE -eq 0) {
            $rabbitReady = $true
            break
        }
        $state = & docker inspect --format '{{.State.Status}}' $validationContainer
        if ($LASTEXITCODE -eq 0 -and ($state -join '').Trim() -eq 'exited') {
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $rabbitReady) {
        $logs = & docker logs --tail 80 $validationContainer 2>&1
        throw "The recovered RabbitMQ broker did not become healthy. Container output:`n$($logs -join [Environment]::NewLine)"
    }
    & docker exec $validationContainer rabbitmqctl list_vhosts --quiet *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'The recovered RabbitMQ broker could not read its virtual-host metadata.'
    }
    & docker rm --force $validationContainer *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not remove the temporary RabbitMQ validation container.'
    }
    $validationContainerCreated = $false

    foreach ($databaseService in $databaseServices) {
        & (Join-Path $PSScriptRoot 'Restore-LocalDatabase.ps1') -BackupDirectory $postgresBackupDirectory -DatabaseService $databaseService -ComposeProjectName $ComposeProjectName -EnvFile $resolvedEnvFile -ReplaceExisting -Confirm:$false
    }

    $localStateDirectory = Split-Path -Parent $recoveryOverridePath
    if (-not (Test-Path -LiteralPath $localStateDirectory -PathType Container)) {
        $null = New-Item -ItemType Directory -Path $localStateDirectory -Force
    }
    $overrideContent = @"
services:
  rabbitmq:
    image: '$temporaryImageTag'
    pull_policy: never
    hostname: '$($rabbitMqManifest.Hostname)'
    environment:
      RABBITMQ_NODENAME: '$($rabbitMqManifest.NodeName)'
    volumes:
      - collection_rabbitmq_data:/var/lib/rabbitmq
volumes:
  collection_rabbitmq_data:
    external: true
    name: '$recoveryVolume'
"@
    Set-Content -LiteralPath $recoveryOverridePath -Value $overrideContent -Encoding utf8NoBOM
    $recoveryOverrideWritten = $true
    $recoveryComposePrefix = @($composePrefix) + @('-f', $recoveryOverridePath)
    & docker compose @recoveryComposePrefix config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "The recovered RabbitMQ Compose override is invalid. Inspect '$recoveryOverridePath'."
    }

    if ('rabbitmq' -in $runningServices) {
        & docker compose @composePrefix stop --timeout 60 rabbitmq
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not stop the previous RabbitMQ service before switching volumes.'
        }
    }
    & docker compose @recoveryComposePrefix up -d --no-deps rabbitmq
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not start RabbitMQ with the recovered volume. The previous project volume remains preserved.'
    }
    Wait-ComposeServicesHealthy -ComposeArguments $recoveryComposePrefix -Services @('rabbitmq') -TimeoutSeconds $ReadinessTimeoutSeconds

    $rabbitMqContainerIds = @(& docker compose @recoveryComposePrefix ps -q rabbitmq)
    if ($LASTEXITCODE -ne 0 -or $rabbitMqContainerIds.Count -eq 0) {
        throw 'Could not inspect RabbitMQ after activating the recovered volume.'
    }
    & docker exec ($rabbitMqContainerIds | Select-Object -First 1) rabbitmqctl list_vhosts --quiet *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'RabbitMQ started from the recovered volume but could not query its virtual hosts.'
    }

    if ($applicationServices.Count -gt 0) {
        $applicationServicesStarting = $true
        & docker compose @recoveryComposePrefix up -d --build @applicationServices
        if ($LASTEXITCODE -ne 0) {
            throw 'The databases and RabbitMQ were restored, but application services could not all be started. Inspect docker compose logs.'
        }
        Wait-ComposeServicesHealthy -ComposeArguments $recoveryComposePrefix -Services $applicationServices -TimeoutSeconds $ReadinessTimeoutSeconds
    }
    try {
        $null = Invoke-RestMethod -Uri 'http://127.0.0.1:5000/health/ready' -TimeoutSec 10
        $null = Invoke-WebRequest -Uri 'http://127.0.0.1:4200/' -TimeoutSec 10
    } catch {
        throw "The recovered services started but Gateway or frontend readiness failed: $($_.Exception.Message)"
    }

    Write-Output "Recovery complete. Eight databases and RabbitMQ were restored. Any replaced PostgreSQL databases and the previous Compose RabbitMQ volume remain available for rollback."
    Write-Output "Keep and use the RabbitMQ Compose override at '$recoveryOverridePath' for subsequent Compose commands."
    Write-Warning 'The recovery set excludes .env, certificates, TLS keys, local runtime settings and Ollama model data. Restore those separately before considering the host fully recovered.'
} catch {
    if ($applicationServicesStopped -or $applicationServicesStarting) {
        Write-Warning 'Application services were left stopped or in their current recovery state to avoid writes against partially restored databases. Inspect Compose status and logs before restarting them.'
    }
    if ($recoveryOverrideWritten) {
        Write-Warning "The recovered RabbitMQ volume and its local Compose override were retained for inspection: '$recoveryOverridePath'."
    }
    throw
} finally {
    if ($validationContainerCreated) {
        & docker rm --force $validationContainer *> $null
    }
    if ($helperCreated) {
        & docker rm --force $helperName *> $null
    }
    if ($recoveryVolumeCreated -and -not $recoveryOverrideWritten) {
        & docker volume rm $recoveryVolume *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not remove temporary RabbitMQ volume '$recoveryVolume'; inspect it before removing it."
        }
    }
}
