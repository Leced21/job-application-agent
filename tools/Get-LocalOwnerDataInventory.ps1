#requires -Version 5.1
[CmdletBinding()]
param(
    [ValidatePattern('^[a-z0-9][a-z0-9_-]*$')]
    [string]$ComposeProjectName = 'job-application-agent',

    [string]$EnvFile = '.env',

    [ValidateSet('profile-db', 'job-db', 'matching-db', 'applications-db', 'documents-db', 'collection-db', 'agents-db', 'identity-db')]
    [string[]]$Service = @('profile-db', 'job-db', 'matching-db', 'applications-db', 'documents-db', 'collection-db', 'agents-db', 'identity-db')
)

$ErrorActionPreference = 'Stop'
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

$runningServices = @(& docker compose @composePrefix ps --status running --services)
if ($LASTEXITCODE -ne 0) {
    throw 'Could not inspect Compose services. Start Docker Desktop and the selected PostgreSQL services, then retry.'
}
$runningServices = @($runningServices | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
$notRunning = @($Service | Where-Object { $_ -notin $runningServices })
if ($notRunning.Count -gt 0) {
    throw "Selected PostgreSQL services are not running: $($notRunning -join ', '). No database was changed."
}

function ConvertTo-SqlIdentifier {
    param([Parameter(Mandatory)][string]$Name)
    return '"' + $Name.Replace('"', '""') + '"'
}

function Invoke-OwnerInventoryQuery {
    param(
        [Parameter(Mandatory)][string]$DatabaseService,
        [Parameter(Mandatory)][string]$DatabaseName,
        [Parameter(Mandatory)][string]$DatabaseUser,
        [Parameter(Mandatory)][string]$Sql
    )

    $output = & docker compose @composePrefix exec -T --user postgres $DatabaseService psql -X --no-psqlrc `
        --set ON_ERROR_STOP=1 --tuples-only --no-align --field-separator '|' `
        --username $DatabaseUser --dbname $DatabaseName --command $Sql
    if ($LASTEXITCODE -ne 0) {
        throw "Read-only owner inventory failed for '$DatabaseService'. No database was changed."
    }
    return @($output | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ })
}

$inventory = [Collections.Generic.List[object]]::new()
foreach ($databaseService in $Service) {
    $database = $configuration.services.$databaseService
    if ($null -eq $database) {
        throw "Compose project '$ComposeProjectName' does not define '$databaseService'. No database was changed."
    }
    $databaseName = [string]$database.environment.POSTGRES_DB
    $databaseUser = [string]$database.environment.POSTGRES_USER
    if ([string]::IsNullOrWhiteSpace($databaseName) -or [string]::IsNullOrWhiteSpace($databaseUser)) {
        throw "Compose does not provide the database name and user for '$databaseService'. No database was changed."
    }

    $metadata = @(Invoke-OwnerInventoryQuery -DatabaseService $databaseService -DatabaseName $databaseName -DatabaseUser $databaseUser -Sql @'
SELECT table_schema || '|' || table_name || '|' || column_name
FROM information_schema.columns
WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
  AND regexp_replace(lower(column_name), '[^a-z0-9]', '', 'g') = 'ownerid'
ORDER BY table_schema, table_name, column_name;
'@)

    if ($metadata.Count -eq 0) {
        $inventory.Add([pscustomobject]@{
            Service = $databaseService
            Database = $databaseName
            Table = '(aucune colonne OwnerId)'
            Column = ''
            Rows = 0L
            UnownedRows = 0L
            OwnerCounts = ''
        })
        continue
    }

    foreach ($entry in $metadata) {
        $parts = $entry.Split('|')
        if ($parts.Count -ne 3) {
            throw "Unexpected metadata format for '$databaseService'. No database was changed."
        }
        $schemaName, $tableName, $columnName = $parts
        $qualifiedTable = (ConvertTo-SqlIdentifier $schemaName) + '.' + (ConvertTo-SqlIdentifier $tableName)
        $ownerColumn = ConvertTo-SqlIdentifier $columnName
        $summarySql = @"
SELECT COUNT(*)::text || '|' ||
       COUNT(*) FILTER (WHERE $ownerColumn IS NULL)::text || '|' ||
       COALESCE((
           SELECT string_agg(COALESCE(owner_value, '<NULL>') || ':' || row_count::text, ',' ORDER BY owner_value NULLS FIRST)
           FROM (
               SELECT $ownerColumn::text AS owner_value, COUNT(*)::bigint AS row_count
               FROM $qualifiedTable
               GROUP BY $ownerColumn
           ) owner_counts
       ), '')
FROM $qualifiedTable;
"@
        $summary = @(Invoke-OwnerInventoryQuery -DatabaseService $databaseService -DatabaseName $databaseName -DatabaseUser $databaseUser -Sql $summarySql)
        if ($summary.Count -ne 1) {
            throw "Could not summarize '$schemaName.$tableName' in '$databaseService'. No database was changed."
        }
        $summaryParts = $summary[0].Split('|', 3)
        if ($summaryParts.Count -ne 3) {
            throw "Unexpected summary format for '$schemaName.$tableName'. No database was changed."
        }
        $inventory.Add([pscustomobject]@{
            Service = $databaseService
            Database = $databaseName
            Table = "$schemaName.$tableName"
            Column = $columnName
            Rows = [long]$summaryParts[0]
            UnownedRows = [long]$summaryParts[1]
            OwnerCounts = $summaryParts[2]
        })
    }
}

$inventory | Format-List
Write-Output 'Read-only inventory complete. Owner IDs are reported per table; review their meaning in each service before treating an unfamiliar ID as an error.'
