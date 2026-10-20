#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scriptPath = Join-Path $repositoryRoot 'tools/Assign-LocalLegacyProfileOwner.ps1'
$environmentFile = Join-Path $repositoryRoot '.env.example'
$testOwnerId = [Guid]::Parse('11111111-1111-4111-8111-111111111111')
$testProfileId = [Guid]::Parse('22222222-2222-4222-8222-222222222222')

$global:LegacyProfileOwnerMock = [pscustomobject]@{
    Total = 1
    Unowned = 1
    DistinctOwners = 0
    GatewayInitialOwnerId = $testOwnerId
    UpdateCount = 0
    DockerCalls = 0
}

function global:docker {
    $global:LegacyProfileOwnerMock.DockerCalls++
    $arguments = @($args | ForEach-Object { [string]$_ })
    if ('config' -in $arguments) {
        $global:LASTEXITCODE = 0
        $gatewayOwner = $global:LegacyProfileOwnerMock.GatewayInitialOwnerId.ToString('D')
        return "{`"services`":{`"profile-db`":{`"environment`":{`"POSTGRES_DB`":`"profile_db`",`"POSTGRES_USER`":`"jobagent`"}},`"gateway-api`":{`"environment`":{`"Identity__InitialOwnerId`":`"$gatewayOwner`"}}}}"
    }
    if ('ps' -in $arguments) {
        $global:LASTEXITCODE = 0
        return 'profile-db'
    }
    if ('--command' -notin $arguments) {
        $global:LASTEXITCODE = 1
        return
    }

    $commandIndex = [Array]::IndexOf($arguments, '--command')
    $sql = $arguments[$commandIndex + 1]
    $global:LASTEXITCODE = 0
    if ($sql -match 'COUNT\(\*\)::text \|\|' -and $sql -match 'FROM candidate_profiles;') {
        return "$($global:LegacyProfileOwnerMock.Total)|$($global:LegacyProfileOwnerMock.Unowned)|$($global:LegacyProfileOwnerMock.DistinctOwners)"
    }
    if ($sql -match '^SELECT id::text FROM candidate_profiles WHERE owner_id IS NULL') {
        if ($global:LegacyProfileOwnerMock.Unowned -eq 1) { return $testProfileId.ToString('D') }
        return @($testProfileId.ToString('D'), [Guid]::NewGuid().ToString('D'))
    }
    if ($sql -match '^SELECT COUNT\(\*\)::text FROM candidate_profiles WHERE owner_id =') { return '0' }
    if ($sql -match '^UPDATE candidate_profiles SET owner_id =') {
        $global:LegacyProfileOwnerMock.UpdateCount++
        return $testProfileId.ToString('D')
    }
    if ($sql -match '^SELECT COUNT\(\*\) FILTER') { return '1|0' }

    $global:LASTEXITCODE = 1
    throw "Unexpected SQL in the owner assignment test: $sql"
}

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

function Reset-Mock {
    param([long]$Total = 1, [long]$Unowned = 1, [long]$DistinctOwners = 0, [Guid]$GatewayInitialOwnerId = $testOwnerId)
    $global:LegacyProfileOwnerMock.Total = $Total
    $global:LegacyProfileOwnerMock.Unowned = $Unowned
    $global:LegacyProfileOwnerMock.DistinctOwners = $DistinctOwners
    $global:LegacyProfileOwnerMock.GatewayInitialOwnerId = $GatewayInitialOwnerId
    $global:LegacyProfileOwnerMock.UpdateCount = 0
    $global:LegacyProfileOwnerMock.DockerCalls = 0
}

Reset-Mock
$consentRejected = $false
try {
    & $scriptPath -OwnerId $testOwnerId -EnvFile $environmentFile -Confirm:$false
} catch {
    $consentRejected = $_.Exception.Message -like '*-AllowDataUpdate*'
}
Assert-True $consentRejected 'The script did not require explicit data-update consent.'
Assert-True ($global:LegacyProfileOwnerMock.DockerCalls -eq 0) 'The consent guard ran Docker before rejecting the operation.'

Reset-Mock -GatewayInitialOwnerId ([Guid]::NewGuid())
$mismatchedOwnerRejected = $false
try {
    & $scriptPath -OwnerId $testOwnerId -EnvFile $environmentFile -AllowDataUpdate -WhatIf -Confirm:$false
} catch {
    $mismatchedOwnerRejected = $_.Exception.Message -like '*must match Gateway Identity*'
}
Assert-True $mismatchedOwnerRejected 'The script accepted an owner that differs from Gateway Identity:InitialOwnerId.'
Assert-True ($global:LegacyProfileOwnerMock.UpdateCount -eq 0) 'The script updated a profile with a mismatched Gateway owner.'

Reset-Mock -Total 1 -Unowned 0 -DistinctOwners 1
& $scriptPath -OwnerId $testOwnerId -EnvFile $environmentFile -AllowDataUpdate -Confirm:$false | Out-Null
Assert-True ($global:LegacyProfileOwnerMock.UpdateCount -eq 0) 'The script changed an already-owned profile.'

Reset-Mock
& $scriptPath -OwnerId $testOwnerId -EnvFile $environmentFile -AllowDataUpdate -WhatIf -Confirm:$false | Out-Null
Assert-True ($global:LegacyProfileOwnerMock.UpdateCount -eq 0) 'WhatIf issued an UPDATE against the mocked database.'

Reset-Mock -Total 2 -Unowned 2
$ambiguousRejected = $false
try {
    & $scriptPath -OwnerId $testOwnerId -EnvFile $environmentFile -AllowDataUpdate -Confirm:$false
} catch {
    $ambiguousRejected = $_.Exception.Message -like '*ambiguous*'
}
Assert-True $ambiguousRejected 'The script did not reject multiple unowned profiles.'
Assert-True ($global:LegacyProfileOwnerMock.UpdateCount -eq 0) 'The script updated data despite ambiguous ownership.'

Reset-Mock
& $scriptPath -OwnerId $testOwnerId -EnvFile $environmentFile -AllowDataUpdate -Confirm:$false | Out-Null
Assert-True ($global:LegacyProfileOwnerMock.UpdateCount -eq 1) 'The single unowned profile was not assigned exactly once.'

Remove-Item Function:\docker -ErrorAction SilentlyContinue
Remove-Variable LegacyProfileOwnerMock -Scope Global -ErrorAction SilentlyContinue
Write-Output 'Legacy Profile owner assignment safety tests passed: consent, Gateway owner match, already-owned no-op, WhatIf, ambiguity rejection and single-row update.'
