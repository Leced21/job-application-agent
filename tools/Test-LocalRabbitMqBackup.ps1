#requires -Version 7.0
param(
    [Parameter(Mandatory)]
    [string]$BackupDirectory,
    [string]$ComposeProjectName = 'job-application-agent',
    [string]$EnvFile = '.env',
    [string]$ArchiveHelperImage = 'alpine:3.22',
    [switch]$AllowServiceRestart
)

$ErrorActionPreference = 'Stop'
if (-not $AllowServiceRestart) {
    throw 'Validating a RabbitMQ volume restore requires a brief broker shutdown. Re-run with -AllowServiceRestart after scheduling that interruption.'
}

$repositoryRoot = Split-Path $PSScriptRoot -Parent
$resolvedEnvFile = if ([IO.Path]::IsPathRooted($EnvFile)) {
    [IO.Path]::GetFullPath($EnvFile)
} else {
    [IO.Path]::GetFullPath((Join-Path $repositoryRoot $EnvFile))
}
if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. Configure the local Compose services first."
}

$resolvedBackupDirectory = (Resolve-Path -LiteralPath $BackupDirectory).Path
$manifestPath = Join-Path $resolvedBackupDirectory 'manifest.json'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw 'RabbitMQ backup manifest.json was not found; no volume or service was changed.'
}
try {
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
} catch {
    throw 'RabbitMQ backup manifest.json is invalid; no volume or service was changed.'
}
if ($manifest.FormatVersion -notin @(1, 2) -or $manifest.ComposeProjectName -cne $ComposeProjectName -or $manifest.Service -cne 'rabbitmq') {
    throw 'RabbitMQ backup format, Compose project, or service is invalid; no volume or service was changed.'
}
if ([IO.Path]::GetFileName([string]$manifest.Archive) -cne [string]$manifest.Archive) {
    throw 'RabbitMQ backup manifest contains an invalid archive path; no volume or service was changed.'
}
$archivePath = Join-Path $resolvedBackupDirectory ([string]$manifest.Archive)
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
    throw 'RabbitMQ volume archive is missing; no volume or service was changed.'
}
$archiveInfo = Get-Item -LiteralPath $archivePath
if ($archiveInfo.Length -ne [long]$manifest.LengthBytes -or (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash -cne [string]$manifest.Sha256) {
    throw 'RabbitMQ volume archive does not match its manifest; no volume or service was changed.'
}
if ([string]$manifest.Hostname -notmatch '^[A-Za-z0-9_.-]+$' -or [string]$manifest.NodeName -notmatch '^rabbit@[A-Za-z0-9_.-]+$') {
    throw 'RabbitMQ backup manifest has an unsupported node identity; no volume or service was changed.'
}
if ($manifest.FormatVersion -eq 2) {
    if ([IO.Path]::GetFileName([string]$manifest.ImageArchive) -cne [string]$manifest.ImageArchive) {
        throw 'RabbitMQ backup manifest contains an invalid image archive path; no volume or service was changed.'
    }
    $imageArchivePath = Join-Path $resolvedBackupDirectory ([string]$manifest.ImageArchive)
    if (-not (Test-Path -LiteralPath $imageArchivePath -PathType Leaf)) {
        throw 'The exact RabbitMQ image archive is missing; no volume or service was changed.'
    }
    $imageArchiveInfo = Get-Item -LiteralPath $imageArchivePath
    if ($imageArchiveInfo.Length -ne [long]$manifest.ImageLengthBytes -or (Get-FileHash -LiteralPath $imageArchivePath -Algorithm SHA256).Hash -cne [string]$manifest.ImageSha256) {
        throw 'The RabbitMQ image archive does not match its manifest; no volume or service was changed.'
    }
    if ([string]$manifest.ImageId -notmatch '^sha256:[a-f0-9]{64}$' -or [string]::IsNullOrWhiteSpace([string]$manifest.Image)) {
        throw 'RabbitMQ backup manifest has invalid image metadata; no volume or service was changed.'
    }
    $imageIds = @(& docker image inspect --format '{{.Id}}' ([string]$manifest.ImageId) 2>$null)
    if ($LASTEXITCODE -ne 0 -or ($imageIds -join '').Trim() -cne [string]$manifest.ImageId) {
        & docker image load --input $imageArchivePath
        if ($LASTEXITCODE -ne 0) {
            throw 'Could not load the exact RabbitMQ image from its backup archive; no service was stopped.'
        }
        $imageIds = @(& docker image inspect --format '{{.Id}}' ([string]$manifest.ImageId))
        if ($LASTEXITCODE -ne 0 -or ($imageIds -join '').Trim() -cne [string]$manifest.ImageId) {
            throw 'The loaded RabbitMQ image does not match the image ID in its manifest; no service was stopped.'
        }
    }
}

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in @('docker-compose.yml', 'docker-compose.collection.yml')) {
    $composePrefix += @('-f', (Join-Path $repositoryRoot $composeFile))
}
$recoveryOverridePath = Join-Path $repositoryRoot '.local/rabbitmq-recovery.yml'
if (Test-Path -LiteralPath $recoveryOverridePath -PathType Leaf) {
    $composePrefix += @('-f', $recoveryOverridePath)
}
$containerIdOutput = & docker compose @composePrefix ps -q rabbitmq
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the RabbitMQ service. Start Docker Desktop and the Compose stack, then retry.'
}
$containerId = ($containerIdOutput | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($containerId)) {
    throw 'RabbitMQ has no Compose container; no volume was changed.'
}
$inspectOutput = & docker inspect $containerId
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the RabbitMQ container; no volume was changed.'
}
$container = @((($inspectOutput -join [Environment]::NewLine) | ConvertFrom-Json))[0]
if (-not $container.State.Running) {
    throw 'RabbitMQ is not running; start it before validating a backup.'
}
$rabbitMqMounts = @($container.Mounts | Where-Object { $_.Destination -ceq '/var/lib/rabbitmq' })
if ($rabbitMqMounts.Count -ne 1 -or $rabbitMqMounts[0].Type -cne 'volume' -or [string]$rabbitMqMounts[0].Name -cne [string]$manifest.Volume) {
    throw 'The running RabbitMQ service does not use the named volume recorded in this backup.'
}
if ([string]$container.Config.Image -cne [string]$manifest.Image -or [string]$container.Image -cne [string]$manifest.ImageId) {
    throw "The running RabbitMQ image '$($container.Config.Image)' differs from the backed-up image '$($manifest.Image)'."
}
& docker compose @composePrefix exec -T rabbitmq rabbitmq-diagnostics -q ping *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'RabbitMQ is not healthy; no service was stopped.'
}

$restoreId = [Guid]::NewGuid().ToString('N')
$restoreVolumeName = "jobagent-rabbit-restore-$($restoreId.Substring(0, 16))"
$helperName = "jobagent-rabbit-restore-helper-$($restoreId.Substring(0, 12))"
$restoreContainerName = "jobagent-rabbit-restore-check-$($restoreId.Substring(0, 12))"
$containerArchivePath = '/tmp/rabbitmq-volume.tar.gz'
$volumeCreated = $false
$helperCreated = $false
$restoreContainerCreated = $false
$serviceStopped = $false

try {
    $volumeOutput = & docker volume create $restoreVolumeName
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($volumeOutput -join '').Trim())) {
        throw 'Could not create the temporary RabbitMQ restore-check volume.'
    }
    $volumeCreated = $true

    $helperOutput = & docker create --name $helperName --mount "type=volume,source=$restoreVolumeName,target=/target" $ArchiveHelperImage sh -c "tar -C /target -xzpf $containerArchivePath"
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($helperOutput -join '').Trim())) {
        throw "Could not create the temporary archive extraction container using '$ArchiveHelperImage'."
    }
    $helperCreated = $true
    & docker cp $archivePath "${helperName}:$containerArchivePath" *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stage the RabbitMQ backup in the temporary restore volume.'
    }
    & docker start --attach $helperName *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not extract the RabbitMQ backup into its temporary volume.'
    }

    & docker compose @composePrefix stop --timeout 60 rabbitmq
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stop RabbitMQ cleanly; the restore check was not started.'
    }
    $serviceStopped = $true

    $runArguments = @(
        '--detach',
        '--name', $restoreContainerName,
        '--hostname', [string]$manifest.Hostname,
        '--network', 'bridge',
        '--mount', "type=volume,source=$restoreVolumeName,target=/var/lib/rabbitmq"
    )
    foreach ($rabbitMqSetting in @($container.Config.Env | Where-Object { $_.StartsWith('RABBITMQ_', [StringComparison]::Ordinal) -and -not $_.StartsWith('RABBITMQ_NODENAME=', [StringComparison]::Ordinal) })) {
        $runArguments += @('--env', [string]$rabbitMqSetting)
    }
    $runArguments += @('--env', "RABBITMQ_NODENAME=$($manifest.NodeName)", [string]$manifest.ImageId)
    $runOutput = & docker run @runArguments
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($runOutput -join '').Trim())) {
        throw 'Could not start a temporary RabbitMQ broker from the restored volume.'
    }
    $restoreContainerCreated = $true

    $ready = $false
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        & docker exec $restoreContainerName rabbitmq-diagnostics -q ping *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }
        $stateOutput = & docker inspect --format '{{.State.Status}}' $restoreContainerName
        if ($LASTEXITCODE -eq 0 -and ($stateOutput -join '').Trim() -eq 'exited') {
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $ready) {
        $logs = & docker logs --tail 80 $restoreContainerName 2>&1
        throw "The restored RabbitMQ broker did not become ready. Container output:`n$($logs -join [Environment]::NewLine)"
    }
    & docker exec $restoreContainerName rabbitmqctl list_vhosts --quiet *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'The restored RabbitMQ broker started but could not read its virtual-host metadata.'
    }
    Write-Output "RabbitMQ volume restore verified: the isolated broker started with node '$($manifest.NodeName)' and queried its virtual hosts. The original volume was not modified."
} finally {
    if ($restoreContainerCreated) {
        & docker rm --force $restoreContainerName *> $null
    }
    if ($helperCreated) {
        & docker rm --force $helperName *> $null
    }
    if ($volumeCreated) {
        & docker volume rm $restoreVolumeName *> $null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Could not remove temporary RabbitMQ restore volume '$restoreVolumeName'; inspect it and remove it after verification."
        }
    }
    if ($serviceStopped) {
        & docker compose @composePrefix start rabbitmq *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'The restore check finished, but the original RabbitMQ broker could not be restarted. Run docker compose start rabbitmq and inspect its logs.'
        }
        $originalReady = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            & docker compose @composePrefix exec -T rabbitmq rabbitmq-diagnostics -q ping *> $null
            if ($LASTEXITCODE -eq 0) {
                $originalReady = $true
                break
            }
            Start-Sleep -Seconds 2
        }
        if (-not $originalReady) {
            throw 'The restore check finished, but the original RabbitMQ broker did not become healthy again. Inspect docker compose logs rabbitmq.'
        }
    }
}
