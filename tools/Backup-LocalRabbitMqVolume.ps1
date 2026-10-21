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
    throw 'Backing up a RabbitMQ volume requires a brief broker shutdown. Re-run with -AllowServiceRestart after scheduling that interruption.'
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

$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in @('docker-compose.yml', 'docker-compose.collection.yml')) {
    $composePrefix += @('-f', (Join-Path $repositoryRoot $composeFile))
}
$recoveryOverridePath = Join-Path $repositoryRoot '.local/rabbitmq-recovery.yml'
if (Test-Path -LiteralPath $recoveryOverridePath -PathType Leaf) {
    $composePrefix += @('-f', $recoveryOverridePath)
}

$composeJsonLines = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not read the RabbitMQ Compose configuration; no service was stopped.'
}
try {
    $composeConfiguration = (($composeJsonLines -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse the RabbitMQ Compose configuration; no service was stopped.'
}
if (-not $composeConfiguration.services.rabbitmq) {
    throw "Compose project '$ComposeProjectName' does not define the rabbitmq service."
}

$containerIdOutput = & docker compose @composePrefix ps -q rabbitmq
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the RabbitMQ service. Start Docker Desktop and the Compose stack, then retry.'
}
$containerId = ($containerIdOutput | Select-Object -First 1).Trim()
if ([string]::IsNullOrWhiteSpace($containerId)) {
    throw 'RabbitMQ has no Compose container; no backup was created.'
}
$inspectOutput = & docker inspect $containerId
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect the RabbitMQ container; no service was stopped.'
}
$container = @((($inspectOutput -join [Environment]::NewLine) | ConvertFrom-Json))[0]
if (-not $container.State.Running) {
    throw 'RabbitMQ is not running; start it before backing up its volume.'
}
$rabbitMqMounts = @($container.Mounts | Where-Object { $_.Destination -ceq '/var/lib/rabbitmq' })
if ($rabbitMqMounts.Count -ne 1 -or $rabbitMqMounts[0].Type -cne 'volume') {
    throw 'RabbitMQ must have exactly one named Docker volume mounted at /var/lib/rabbitmq.'
}
$volumeName = [string]$rabbitMqMounts[0].Name
if ($volumeName -notmatch '^[A-Za-z0-9_.-]+$') {
    throw "Unsupported RabbitMQ volume name '$volumeName'; no service was stopped."
}
$hostname = [string]$container.Config.Hostname
if ([string]::IsNullOrWhiteSpace($hostname) -or $hostname -notmatch '^[A-Za-z0-9_.-]+$') {
    throw 'The RabbitMQ container has no supported hostname; no service was stopped.'
}
$nodeNameSetting = @($container.Config.Env | Where-Object { $_.StartsWith('RABBITMQ_NODENAME=', [StringComparison]::Ordinal) } | Select-Object -First 1)
$nodeName = if ($nodeNameSetting.Count -gt 0) { $nodeNameSetting[0].Substring('RABBITMQ_NODENAME='.Length) } else { "rabbit@$hostname" }
$image = [string]$container.Config.Image
$imageId = [string]$container.Image
if ([string]::IsNullOrWhiteSpace($image)) {
    throw 'Could not determine the RabbitMQ image; no service was stopped.'
}
if ($imageId -notmatch '^sha256:[a-f0-9]{64}$') {
    throw 'Could not determine the exact RabbitMQ image ID; no service was stopped.'
}

& docker compose @composePrefix exec -T rabbitmq rabbitmq-diagnostics -q ping *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'RabbitMQ is not healthy; no service was stopped.'
}

$resolvedBackupDirectory = [IO.Path]::GetFullPath($BackupDirectory)
if (Test-Path -LiteralPath $resolvedBackupDirectory) {
    throw "Backup destination already exists; refusing to overwrite it: $resolvedBackupDirectory"
}
$backupParent = Split-Path -Parent $resolvedBackupDirectory
if (-not (Test-Path -LiteralPath $backupParent -PathType Container)) {
    $null = New-Item -ItemType Directory -Path $backupParent -Force
}
$null = New-Item -ItemType Directory -Path $resolvedBackupDirectory
$backupId = [Guid]::NewGuid().ToString('N')
$helperName = "jobagent-rabbit-backup-$($backupId.Substring(0, 12))"
$containerArchivePath = '/tmp/rabbitmq-volume.tar.gz'
$archiveName = 'rabbitmq-volume.tar.gz'
$archivePath = Join-Path $resolvedBackupDirectory $archiveName
$imageArchiveName = 'rabbitmq-image.tar'
$imageArchivePath = Join-Path $resolvedBackupDirectory $imageArchiveName
$serviceStopped = $false
$helperCreated = $false

try {
    & docker image save --output $imageArchivePath $imageId
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $imageArchivePath -PathType Leaf)) {
        throw 'Could not save the exact RabbitMQ image used by the broker.'
    }
    $imageArchiveInfo = Get-Item -LiteralPath $imageArchivePath
    if ($imageArchiveInfo.Length -eq 0) {
        throw 'The RabbitMQ image archive is empty.'
    }

    & docker compose @composePrefix stop --timeout 60 rabbitmq
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not stop RabbitMQ cleanly; no volume archive was created.'
    }
    $serviceStopped = $true

    $helperOutput = & docker create --name $helperName --mount "type=volume,source=$volumeName,target=/source,readonly" $ArchiveHelperImage sh -c "tar -C /source -czpf $containerArchivePath ."
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($helperOutput -join '').Trim())) {
        throw "Could not create the temporary volume archive container using '$ArchiveHelperImage'."
    }
    $helperCreated = $true
    & docker start --attach $helperName *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not archive the RabbitMQ volume.'
    }
    & docker cp "${helperName}:$containerArchivePath" $archivePath *> $null
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $archivePath -PathType Leaf)) {
        throw 'Could not copy the RabbitMQ volume archive to the backup directory.'
    }
    $archiveInfo = Get-Item -LiteralPath $archivePath
    if ($archiveInfo.Length -eq 0) {
        throw 'The RabbitMQ volume archive is empty.'
    }

    $manifest = [pscustomobject]@{
        FormatVersion = 2
        CreatedAtUtc = [DateTime]::UtcNow.ToString('O')
        ComposeProjectName = $ComposeProjectName
        Service = 'rabbitmq'
        Volume = $volumeName
        Image = $image
        ImageId = $imageId
        Hostname = $hostname
        NodeName = $nodeName
        ImageArchive = $imageArchiveName
        ImageLengthBytes = $imageArchiveInfo.Length
        ImageSha256 = (Get-FileHash -LiteralPath $imageArchivePath -Algorithm SHA256).Hash
        Archive = $archiveName
        LengthBytes = $archiveInfo.Length
        Sha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
    }
    $manifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $resolvedBackupDirectory 'manifest.json') -Encoding utf8NoBOM
    Write-Output "RabbitMQ volume backed up: $resolvedBackupDirectory ($($archiveInfo.Length) bytes)."
} finally {
    if ($helperCreated) {
        & docker rm --force $helperName *> $null
    }
    if ($serviceStopped) {
        & docker compose @composePrefix start rabbitmq *> $null
        if ($LASTEXITCODE -ne 0) {
            throw 'The RabbitMQ backup was attempted, but the original broker could not be restarted. Run docker compose start rabbitmq and inspect its logs.'
        }
        $ready = $false
        for ($attempt = 0; $attempt -lt 60; $attempt++) {
            & docker compose @composePrefix exec -T rabbitmq rabbitmq-diagnostics -q ping *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Seconds 2
        }
        if (-not $ready) {
            throw 'The RabbitMQ backup was attempted, but the original broker did not become healthy again. Inspect docker compose logs rabbitmq.'
        }
    }
}

Write-Warning 'RabbitMQ backups may contain queued messages and broker credentials. Store this directory privately with encryption and retention controls; never publish it as a build artifact.'
