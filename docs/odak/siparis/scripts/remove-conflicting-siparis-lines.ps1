# Dump ile uyumsuz parent+lineNo slot doldurucu kalemleri siler (yanlis migrasyon artigi)
param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
. (Join-Path $PSScriptRoot "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $PSScriptRoot "lib/DgMigrationCommon.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$headers = @{ Authorization = "Bearer $token" }

function Invoke-Dg {
    param([string]$Method, [string]$Uri)
    Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers -ErrorAction Stop
}

Write-Host "`n=== remove-conflicting-siparis-lines ===" -ForegroundColor Cyan
$dump = Get-LegacySqlDumpPath
$itemRows = Read-SqlInsertRows -Path $dump -TableName "packageitems"
$dumpSlotMap = @{}
$dumpParentSlotMap = @{}
$pkgMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
foreach ($item in $itemRows) {
    $legacyLineId = [string]$item[0]
    $legacyPkgId = [string]$item[1]
    $lineNo = [string]$item[3]
    if ($legacyLineId -notmatch '^\d+$' -or -not $lineNo) { continue }
    $dumpSlotMap["$legacyPkgId|$lineNo"] = $legacyLineId
    if ($pkgMap.ContainsKey($legacyPkgId)) {
        $parentId = $pkgMap[$legacyPkgId]
        $dumpParentSlotMap["$parentId|$lineNo"] = $legacyLineId
    }
}

$lineMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"

$scanned = 0
$conflicts = 0
$deleted = 0
$skip = 0
$pageSkip = 0
$limit = 500

while ($true) {
    $uri = '{0}{1}/odak_siparis_kalemleri?skip={2}&limit={3}' -f $BaseUrl, $dataPath, $pageSkip, $limit
    $raw = Invoke-Dg -Method GET -Uri $uri
    $items = if ($raw -is [Array]) { @($raw) } elseif ($raw.items) { @($raw.items) } else { @() }
    if (-not $items.Count) { break }

    foreach ($line in $items) {
        $scanned++
        $lineNo = [string]$line.lineNo
        $parentId = Get-RelationId $line.parentPackageId
        if (-not $parentId) { $parentId = Get-RelationId $line.parentWorkItemId }
        if (-not $lineNo -or -not $parentId) { continue }
        if (-not $dumpParentSlotMap.ContainsKey("$parentId|$lineNo")) { continue }

        $expected = $dumpParentSlotMap["$parentId|$lineNo"]
        $actual = [string]$line.legacyLineId
        if ($actual -eq $expected) { continue }
        if ($lineMap.ContainsKey($expected)) { continue }
        if ($actual -eq $expected) { continue }
        if ($lineMap.ContainsKey($expected)) { continue }

        $conflicts++
        $id = $line.__dataId; if (-not $id) { $id = $line.dataId }
        $label = "parent=$parentId lineNo=$lineNo expected=$expected actual=$actual id=$id"
        if ($DryRun) {
            Write-Host "[DRY] $label" -ForegroundColor Yellow
            continue
        }
        try {
            Invoke-Dg -Method DELETE -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri/$id" | Out-Null
            $deleted++
        }
        catch {
            Write-Host "  HATA $label : $($_.Exception.Message)" -ForegroundColor Red
            $skip++
        }
    }

    if ($items.Count -lt $limit) { break }
    $pageSkip += $limit
}

Write-Host "Taranan=$scanned Cakisma=$conflicts Silinen=$deleted Hata=$skip DryRun=$DryRun" -ForegroundColor Green
