# legacyContactId -> customerContactId backfill (odak_is_paketleri)
#
# Onkosul:
#   migrate-legacy-contacts-to-dg.ps1 (migration-contact-mapping.json)
#
# Kullanim:
#   .\backfill-package-customer-contacts.ps1 -DryRun
#   .\backfill-package-customer-contacts.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [string]$ContactMapFile = "",
    [switch]$DryRun,
    [switch]$OnlyMissing
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

$repoRoot = $LegacyArchiveRepoRoot
if ([string]::IsNullOrWhiteSpace($ContactMapFile)) {
    $ContactMapFile = Join-Path $repoRoot "docs/odak/siparis/datasets/migration-contact-mapping.json"
}
if (-not (Test-Path $ContactMapFile)) {
    throw "Contact mapping yok: $ContactMapFile — once migrate-legacy-contacts-to-dg.ps1"
}

$rawMap = Get-Content $ContactMapFile -Raw -Encoding UTF8 | ConvertFrom-Json
$legacyToDg = @{}
foreach ($prop in $rawMap.PSObject.Properties) {
    $dgId = [string]$prop.Value
    if ($dgId -and $dgId -ne "DRY-RUN") {
        $legacyToDg[$prop.Name] = $dgId
    }
}
Write-Host "Map: $($legacyToDg.Count) legacyContactId -> odak_musteri_kisileri" -ForegroundColor Gray

$dg = Initialize-ProdDgAuthContext -BaseUrl $BaseUrl -UseGateway:$UseGateway
Write-Host "odak_is_paketleri yukleniyor..." -ForegroundColor Yellow
$packages = Get-AllDgDatasetRows -DgContext $dg -Dataset "odak_is_paketleri"
Write-Host "  $($packages.Count) paket" -ForegroundColor Green

function Get-RelationFieldId {
    param($Value)
    if ($null -eq $Value -or $Value -eq "") { return "" }
    if ($Value -is [string]) { return $Value.Trim() }
    if ($Value -is [pscustomobject] -or $Value -is [hashtable]) {
        $o = $Value
        return [string]($o.__dataId ?? $o.dataId ?? $o.id ?? "").Trim()
    }
    return [string]$Value
}

$stats = @{
    scanned = 0
    patched = 0
    skipped = 0
    noMap   = 0
}

foreach ($pkg in $packages) {
    $stats.scanned++
    $dataId = [string]($pkg.__dataId ?? $pkg.dataId ?? "")
    if (-not $dataId) { continue }

    $legacyContact = Get-LegacyPersonIdFromPackageRow -Value $pkg.legacyContactId
    if (-not $legacyContact) {
        $stats.skipped++
        continue
    }

    $current = Get-RelationFieldId -Value $pkg.customerContactId
    if ($OnlyMissing -and $current) {
        $stats.skipped++
        continue
    }

    if (-not $legacyToDg.ContainsKey($legacyContact)) {
        $stats.noMap++
        continue
    }

    $target = $legacyToDg[$legacyContact]
    if ($current -eq $target) {
        $stats.skipped++
        continue
    }

    $label = [string]($pkg.packageNo ?? $dataId)
    if ($DryRun) {
        Write-Host "[DRY] $label legacyContactId=$legacyContact $($current) -> $target" -ForegroundColor Yellow
        $stats.patched++
        continue
    }

    $uri = "{0}{1}/odak_is_paketleri/{2}" -f $dg.BaseUrl, $dg.DataPath, $dataId
    Invoke-ProdDgApi -DgContext $dg -Method PUT -Uri $uri -Body @{ customerContactId = $target } | Out-Null
    Write-Host "[OK] $label legacyContactId=$legacyContact -> $target" -ForegroundColor Green
    $stats.patched++
}

Write-Host "`nOzet: scanned=$($stats.scanned) patched=$($stats.patched) skipped=$($stats.skipped) noMap=$($stats.noMap)" -ForegroundColor Cyan
if ($DryRun) {
    Write-Host "(DryRun — DG guncellenmedi)" -ForegroundColor DarkGray
}
