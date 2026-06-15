# Tek paket/kalem POST hatalarini teşhis eder
param(
    [string]$PackageNo = "2018-004",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$dumpPath = Get-LegacySqlDumpPath
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$token = & (Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1")
$headers = @{ Authorization = "Bearer $token"; "Content-Type" = "application/json" }

function Invoke-DgPost {
    param([object]$Body, [string]$Uri)
    try {
        $json = $Body | ConvertTo-Json -Depth 20 -Compress
        return Invoke-RestMethod -Uri $Uri -Method POST -Headers $headers -Body $json -ContentType "application/json"
    }
    catch {
        $detail = $_.ErrorDetails.Message
        if (-not $detail) { $detail = $_.Exception.Message }
        return @{ error = $detail; body = ($Body | ConvertTo-Json -Depth 5) }
    }
}

$packages = Read-SqlInsertRows -Path $dumpPath -TableName "packages"
$items = Read-SqlInsertRows -Path $dumpPath -TableName "packageitems"
$pkg = @($packages | Where-Object { [string]$_[1] -eq $PackageNo })[0]
if (-not $pkg) { throw "Paket bulunamadi: $PackageNo" }
$legacyPkgId = [string]$pkg[0]
Write-Host "Legacy package id: $legacyPkgId" -ForegroundColor Cyan

$pkgMap = Load-LegacyIdMap -InvokeDg {
    param($Method, $Uri)
    Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers
} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"

$parentId = $pkgMap[$legacyPkgId]
if (-not $parentId) { throw "DG paket bulunamadi legacyPackageId=$legacyPkgId" }
Write-Host "DG parentPackageId: $parentId" -ForegroundColor Cyan

$lineMap = Load-LegacyIdMap -InvokeDg {
    param($Method, $Uri)
    Invoke-RestMethod -Uri $Uri -Method $Method -Headers $headers
} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"

$pkgItems = @($items | Where-Object { [string]$_[1] -eq $legacyPkgId })
Write-Host "Legacy kalemler: $($pkgItems.Count), DG'de mevcut: $(@($pkgItems | Where-Object { $lineMap.ContainsKey([string]$_[0]) }).Count)" -ForegroundColor Yellow

foreach ($item in $pkgItems) {
    $legacyLineId = [string]$item[0]
    if ($lineMap.ContainsKey($legacyLineId)) { continue }
    $desc = [string]$item[6]
    $body = @{
        parentPackageId   = $parentId
        parentWorkItemId  = $parentId
        lineNo            = [int]$item[3]
        customerProjectNo = if ($item[2]) { [string]$item[2] } else { $null }
        customerPoNo      = if ($item[4]) { [string]$item[4] } else { $null }
        customerPoItemNo  = if ($item[5] -match '^\d+$') { [int]$item[5] } else { $null }
        description       = $desc
        poItemRevNo       = if ($item[7]) { [string]$item[7] } else { $null }
        customerJobNo     = if ($item[8]) { [string]$item[8] } else { $null }
        quantity          = if ($null -ne $item[9] -and $item[9] -ne "") { [double]$item[9] } else { 0 }
        unit              = "adet"
        legacyLineId      = $legacyLineId
        legacyPackageId   = $legacyPkgId
        shippedQuantity   = 0
    }
    Write-Host "`n--- lineNo=$($item[3]) legacyLineId=$legacyLineId descLen=$($desc.Length) ---" -ForegroundColor Gray
    $r = Invoke-DgPost -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri" -Body $body
    if ($r.error) {
        Write-Host $r.error -ForegroundColor Red
        break
    }
    Write-Host "OK" -ForegroundColor Green
    break
}
