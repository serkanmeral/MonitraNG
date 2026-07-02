# legacyLineId olmayan / gecersiz test kalemlerini siler (POC, diag, idx cakismasi)
#
# Usage:
#   .\remove-orphan-siparis-kalemleri.ps1 -DryRun
#   .\remove-orphan-siparis-kalemleri.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun,
    [switch]$IncludeMissingLegacyPackageId
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../../..")).Path
. (Join-Path $PSScriptRoot "lib/DgMigrationCommon.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$dgAuth = Initialize-DgMigrationHeaders -TokenScriptPath $ocTokenScript
$headers = $dgAuth.Headers

function Invoke-Dg {
    param([string]$Method, [string]$Uri)
    return Invoke-DgMigrationApi -AuthContext $dgAuth -Method $Method -Uri $Uri -RetryOnUnauthorized
}

function Test-OrphanLine {
    param($Item)
    $legacyLineId = [string]$Item.legacyLineId
    if ([string]::IsNullOrWhiteSpace($legacyLineId)) { return $true }
    if ($legacyLineId -notmatch '^\d+$') { return $true }
    if ($legacyLineId -match '(?i)^(test|diag)[\-_]') { return $true }
    if ($legacyLineId -match '-') { return $true }
    if ($IncludeMissingLegacyPackageId -and [string]::IsNullOrWhiteSpace([string]$Item.legacyPackageId)) { return $true }
    return $false
}

Write-Host "`n=== remove-orphan-siparis-kalemleri ===" -ForegroundColor Cyan
Write-Host "DryRun: $DryRun`n" -ForegroundColor Gray

$scanned = 0
$orphans = 0
$deleted = 0
$skip = 0
$pageSkip = 0
$limit = 500

while ($true) {
    $uri = '{0}{1}/odak_siparis_kalemleri?skip={2}&limit={3}' -f $BaseUrl, $dataPath, $pageSkip, $limit
    $raw = Invoke-Dg -Method GET -Uri $uri
    $items = @()
    if ($raw -is [Array]) { $items = @($raw) }
    elseif ($raw.items) { $items = @($raw.items) }
    if (-not $items.Count) { break }

    foreach ($item in $items) {
        $scanned++
        if (-not (Test-OrphanLine -Item $item)) { continue }
        $orphans++
        $id = $item.__dataId; if (-not $id) { $id = $item.dataId }
        $parentId = Get-RelationId $item.parentPackageId
        if (-not $parentId) { $parentId = Get-RelationId $item.parentWorkItemId }
        $label = "id=$id lineNo=$($item.lineNo) legacyLineId='$($item.legacyLineId)' parent=$parentId"
        if ($DryRun) {
            Write-Host "[DRY] $label" -ForegroundColor Yellow
            continue
        }
        try {
            Invoke-Dg -Method DELETE -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri/$id" | Out-Null
            $deleted++
            if ($deleted % 25 -eq 0) { Write-Host "  silindi: $deleted" -ForegroundColor Gray }
        }
        catch {
            Write-Host "  HATA $label : $($_.Exception.Message)" -ForegroundColor Red
            $skip++
        }
    }

    if ($items.Count -lt $limit) { break }
    $pageSkip += $limit
}

Write-Host "`nTaranan: $scanned  Orphan: $orphans  Silinen: $deleted  Hata: $skip" -ForegroundColor $(if ($deleted -gt 0 -or $DryRun) { "Green" } else { "Gray" })
