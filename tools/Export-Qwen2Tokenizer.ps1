#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$VocabPath,
    [Parameter(Mandatory)][string]$MergesPath,
    [Parameter(Mandatory)][string]$OutputPath
)
$ErrorActionPreference = 'Stop'
# Offline conversion only. Never fetch model assets, execute tokenizer code or overwrite an existing artifact.
if (Test-Path -LiteralPath $OutputPath) { throw 'Output already exists; choose a new artifact path.' }
$vocab = Get-Content -LiteralPath $VocabPath -Raw | ConvertFrom-Json -AsHashtable
# A real vocabulary contains tokens named Count/Keys; use the CLR getter rather than PowerShell member adaptation.
if ($vocab.get_Count() -ne 151643) { throw 'Expected the standard Qwen2 151643-token vocabulary.' }
$byteMap = [Collections.Generic.Dictionary[char,byte]]::new()
$initialBytes = @((33..126) + (161..172) + (174..255))
foreach ($byteValue in $initialBytes) { $byteMap[[char]$byteValue] = [byte]$byteValue }
$extraIndex = 0
foreach ($byteValue in 0..255) {
    if ($byteValue -notin $initialBytes) { $byteMap[[char](256 + $extraIndex)] = [byte]$byteValue; $extraIndex++ }
}
$mergeTokens = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$lastRank = -1
foreach ($line in Get-Content -LiteralPath $MergesPath) {
    if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#version:')) { continue }
    $parts = $line.Split(' ')
    if ($parts.Count -ne 2) { throw 'Unsupported merge entry.' }
    $merged = $parts[0] + $parts[1]
    if (-not $vocab.Contains($merged) -or -not $vocab.Contains($parts[0]) -or -not $vocab.Contains($parts[1])) { throw 'Merge absent from vocabulary.' }
    $rank = [int]$vocab[$merged]
    if ($rank -le $lastRank -or $rank -le [int]$vocab[$parts[0]] -or $rank -le [int]$vocab[$parts[1]] -or -not $mergeTokens.Add($merged)) {
        throw 'Merge priorities are incompatible with rank-based BPE.'
    }
    $lastRank = $rank
}
if ($mergeTokens.Count -ne (151643 - 256)) { throw 'Incomplete Qwen2 merge table.' }
$rows = [string[]]::new(151643)
foreach ($entry in $vocab.GetEnumerator()) {
    $rank = [int]$entry.Value
    if ($rank -lt 0 -or $rank -ge $rows.Length -or $null -ne $rows[$rank]) { throw 'Duplicate or invalid token rank.' }
    $bytes = [Collections.Generic.List[byte]]::new()
    foreach ($symbol in $entry.Key.ToCharArray()) {
        if (-not $byteMap.ContainsKey($symbol)) { throw 'Unsupported vocabulary symbol.' }
        $bytes.Add($byteMap[$symbol])
    }
    if ($bytes.Count -eq 0 -or ($bytes.Count -gt 1 -and -not $mergeTokens.Contains($entry.Key))) { throw 'Token is missing its merge rule.' }
    $rows[$rank] = [Convert]::ToBase64String($bytes.ToArray()) + ' ' + $rank
}
$outputFile = [IO.Path]::GetFullPath($OutputPath)
$stream = [IO.File]::Open($outputFile, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
try {
    $writer = [IO.StreamWriter]::new($stream, [Text.UTF8Encoding]::new($false))
    try { foreach ($row in $rows) { $writer.WriteLine($row) } } finally { $writer.Dispose() }
} finally { $stream.Dispose() }
Get-FileHash -LiteralPath $outputFile -Algorithm SHA256 | Select-Object Path, Hash
