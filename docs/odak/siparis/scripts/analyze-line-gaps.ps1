$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

function To-IntOrNull { param($Value); if ($null -eq $Value -or $Value -eq "") { return $null }; if ([string]$Value -notmatch '^\d+$') { return $null }; return [int]$Value }

$dump = Get-LegacySqlDumpPath
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$h = @{ Authorization = "Bearer $token" }
$base = "http://192.168.20.20:5040"
$data = "/data/api/v1/data"

$invokeDg = {
    param($Method, $Uri)
    Invoke-RestMethod -Uri $Uri -Method $Method -Headers $h
}

$pkgRows = Read-SqlInsertRows -Path $dump -TableName "packages"
$itemRows = Read-SqlInsertRows -Path $dump -TableName "packageitems"
$pkgMap = Load-LegacyIdMap -InvokeDg $invokeDg -BaseUrl $base -DataPath $data -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
$lineMap = Load-LegacyIdMap -InvokeDg $invokeDg -BaseUrl $base -DataPath $data -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"
$parentLineMap = Load-ParentLineNoMap -InvokeDg $invokeDg -BaseUrl $base -DataPath $data

$stats = @{
    valid = 0; inDg = 0; noPackage = 0; blockedLineNo = 0; invalid = 0; ready = 0
}

foreach ($item in $itemRows) {
    $legacyLineId = [string]$item[0]
    $legacyPkgId = [string]$item[1]
    $lineNo = To-IntOrNull $item[3]
    if ($legacyLineId -notmatch '^\d+$' -or -not $lineNo -or $lineNo -lt 1) { $stats.invalid++; continue }
    $stats.valid++
    if ($lineMap.ContainsKey($legacyLineId)) { $stats.inDg++; continue }
    if (-not $pkgMap.ContainsKey($legacyPkgId)) { $stats.noPackage++; continue }
    $parentId = $pkgMap[$legacyPkgId]
    if ($parentLineMap.ContainsKey("$parentId|$lineNo")) { $stats.blockedLineNo++; continue }
    $stats.ready++
}

Write-Host "Valid legacy lines: $($stats.valid)"
Write-Host "Already in DG (legacyLineId): $($stats.inDg)"
Write-Host "Invalid parse rows: $($stats.invalid)"
Write-Host "Missing parent package in DG: $($stats.noPackage)"
Write-Host "Blocked by existing lineNo slot: $($stats.blockedLineNo)"
Write-Host "Ready to POST: $($stats.ready)"
