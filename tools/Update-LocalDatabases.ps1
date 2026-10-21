#requires -Version 7.0
[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
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
$migrations = @(
    [pscustomobject]@{ Service = 'identity-db'; Context = 'GatewayIdentityDbContext'; ConnectionName = 'IdentityDatabase'; Project = 'src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj'; Startup = 'src/gateway/JobApplicationAgent.Gateway/JobApplicationAgent.Gateway.csproj' },
    [pscustomobject]@{ Service = 'profile-db'; Context = 'ProfileDbContext'; ConnectionName = 'ProfileDatabase'; Project = 'src/services/Profile/JobApplicationAgent.Profile.Infrastructure/JobApplicationAgent.Profile.Infrastructure.csproj'; Startup = 'src/services/Profile/JobApplicationAgent.Profile.Api/JobApplicationAgent.Profile.Api.csproj' },
    [pscustomobject]@{ Service = 'job-db'; Context = 'JobDbContext'; ConnectionName = 'JobDatabase'; Project = 'src/services/Job/JobApplicationAgent.Job.Infrastructure/JobApplicationAgent.Job.Infrastructure.csproj'; Startup = 'src/services/Job/JobApplicationAgent.Job.Api/JobApplicationAgent.Job.Api.csproj' },
    [pscustomobject]@{ Service = 'matching-db'; Context = 'MatchingDbContext'; ConnectionName = 'MatchingDb'; Project = 'src/services/Matching/JobApplicationAgent.Matching.Infrastructure/JobApplicationAgent.Matching.Infrastructure.csproj'; Startup = 'src/services/Matching/JobApplicationAgent.Matching.Api/JobApplicationAgent.Matching.Api.csproj' },
    [pscustomobject]@{ Service = 'collection-db'; Context = 'CollectionDbContext'; ConnectionName = 'CollectionDatabase'; Project = 'src/services/Collection/JobApplicationAgent.Collection.Infrastructure/JobApplicationAgent.Collection.Infrastructure.csproj'; Startup = 'src/services/Collection/JobApplicationAgent.Collection.Api/JobApplicationAgent.Collection.Api.csproj' },
    [pscustomobject]@{ Service = 'documents-db'; Context = 'DocumentsDbContext'; ConnectionName = 'DocumentsDatabase'; Project = 'src/services/Documents/JobApplicationAgent.Documents.Infrastructure/JobApplicationAgent.Documents.Infrastructure.csproj'; Startup = 'src/services/Documents/JobApplicationAgent.Documents.Api/JobApplicationAgent.Documents.Api.csproj' },
    [pscustomobject]@{ Service = 'applications-db'; Context = 'ApplicationsDbContext'; ConnectionName = 'ApplicationsDatabase'; Project = 'src/services/Applications/JobApplicationAgent.Applications.Infrastructure/JobApplicationAgent.Applications.Infrastructure.csproj'; Startup = 'src/services/Applications/JobApplicationAgent.Applications.Api/JobApplicationAgent.Applications.Api.csproj' },
    [pscustomobject]@{ Service = 'agents-db'; Context = 'AgentsDbContext'; ConnectionName = 'AgentsDatabase'; Project = 'src/services/Agents/JobApplicationAgent.Agents.Infrastructure/JobApplicationAgent.Agents.Infrastructure.csproj'; Startup = 'src/services/Agents/JobApplicationAgent.Agents.Api/JobApplicationAgent.Agents.Api.csproj' }
)

if (-not (Test-Path -LiteralPath $resolvedEnvFile -PathType Leaf)) {
    throw "Environment file not found: $resolvedEnvFile. Copy .env.example to .env and configure local values first."
}
$composePrefix = @('--project-name', $ComposeProjectName, '--env-file', $resolvedEnvFile)
foreach ($composeFile in $composeFiles) {
    $composePath = Join-Path $repositoryRoot $composeFile
    if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
        throw "Compose file is missing: $composeFile"
    }
    $composePrefix += @('-f', $composePath)
}

$composeOutput = & docker compose @composePrefix config --format json
if ($LASTEXITCODE -ne 0) {
    throw 'Could not load local Compose database settings; no containers or databases were changed.'
}
try {
    $composeConfiguration = (($composeOutput -join [Environment]::NewLine) | ConvertFrom-Json)
} catch {
    throw 'Could not parse local Compose database settings; no containers or databases were changed.'
}

$migrationTargets = [System.Collections.Generic.List[object]]::new()
foreach ($migration in $migrations) {
    $serviceConfiguration = $composeConfiguration.services.($migration.Service)
    if ($null -eq $serviceConfiguration) {
        throw "Compose does not define '$($migration.Service)'; no containers or databases were changed."
    }

    $databaseEnvironment = $serviceConfiguration.environment
    $databaseName = [string]$databaseEnvironment.POSTGRES_DB
    $databaseUser = [string]$databaseEnvironment.POSTGRES_USER
    $databasePassword = [string]$databaseEnvironment.POSTGRES_PASSWORD
    $portMapping = @($serviceConfiguration.ports | Where-Object { [int]$_.target -eq 5432 }) | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($databaseName) -or [string]::IsNullOrWhiteSpace($databaseUser) -or
        [string]::IsNullOrWhiteSpace($databasePassword) -or $null -eq $portMapping -or
        [string]$portMapping.host_ip -notin @('127.0.0.1', '::1')) {
        throw "Local database configuration for '$($migration.Service)' is incomplete or not loopback-bound; no containers or databases were changed."
    }

    $projectPath = Join-Path $repositoryRoot $migration.Project
    $startupPath = Join-Path $repositoryRoot $migration.Startup
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf) -or -not (Test-Path -LiteralPath $startupPath -PathType Leaf)) {
        throw "EF project files for '$($migration.Context)' are missing; no containers or databases were changed."
    }

    $connectionBuilder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connectionBuilder['Host'] = '127.0.0.1'
    $connectionBuilder['Port'] = [int]$portMapping.published
    $connectionBuilder['Database'] = $databaseName
    $connectionBuilder['Username'] = $databaseUser
    $connectionBuilder['Password'] = $databasePassword
    $migrationTargets.Add([pscustomobject]@{
        Service = $migration.Service
        Context = $migration.Context
        ConnectionName = $migration.ConnectionName
        ConnectionString = $connectionBuilder.ConnectionString
        Database = $databaseName
        HostPort = [int]$portMapping.published
        Project = $projectPath
        Startup = $startupPath
    })
}

if ($migrations.Count -ne 8 -or $migrationTargets.Count -ne 8) {
    throw 'The local migration plan must contain exactly eight database contexts.'
}
& dotnet ef --version *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'The dotnet-ef tool is not available. Install it before starting database services; no database was changed.'
}

foreach ($target in $migrationTargets) {
    Write-Output ("Planned: {0} / {1} -> {2} at 127.0.0.1:{3}" -f $target.Service, $target.Context, $target.Database, $target.HostPort)
}

if (-not $PSCmdlet.ShouldProcess("the eight PostgreSQL databases in Compose project '$ComposeProjectName'", 'Start local databases and apply EF Core migrations')) {
    return
}

foreach ($migration in $migrations) {
    $otherProjects = @(& docker ps --all --filter "label=com.docker.compose.service=$($migration.Service)" --format '{{.Label "com.docker.compose.project"}}')
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to inspect Docker containers. No migration was applied for '$($migration.Service)'."
    }
    $conflictingProjects = @($otherProjects | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -cne $ComposeProjectName } | Sort-Object -Unique)
    if ($conflictingProjects.Count -gt 0) {
        throw "A '$($migration.Service)' container belongs to another Compose project ($($conflictingProjects -join ', ')). Reconcile it before migrating; no volume was removed."
    }
}

$databaseServices = @($migrations | ForEach-Object { $_.Service })
& docker compose @composePrefix up -d --wait --wait-timeout 90 @databaseServices
if ($LASTEXITCODE -ne 0) {
    throw 'Local PostgreSQL startup failed; Compose volumes were preserved and no EF migrations were applied.'
}

$environmentNames = @('ASPNETCORE_ENVIRONMENT', 'DOTNET_ENVIRONMENT') + @($migrationTargets | ForEach-Object { "ConnectionStrings__$($_.ConnectionName)" })
$previousEnvironment = @{}
foreach ($name in $environmentNames) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
}

try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $env:DOTNET_ENVIRONMENT = 'Development'
    foreach ($target in $migrationTargets) {
        [Environment]::SetEnvironmentVariable("ConnectionStrings__$($target.ConnectionName)", $target.ConnectionString, 'Process')
        Write-Output "Applying $($target.Context) migrations to $($target.Database)..."
        & dotnet ef database update --project $target.Project --startup-project $target.Startup --context $target.Context
        if ($LASTEXITCODE -ne 0) {
            throw "EF migration failed for '$($target.Context)'. Earlier database migrations may already be applied; Compose volumes were preserved."
        }
    }
} finally {
    foreach ($name in $environmentNames) {
        $previousValue = $previousEnvironment[$name]
        if ($null -eq $previousValue) {
            Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
        } else {
            [Environment]::SetEnvironmentVariable($name, $previousValue, 'Process')
        }
    }
}

Write-Output "All eight local PostgreSQL contexts were migrated for Compose project '$ComposeProjectName'. No volume was removed."
