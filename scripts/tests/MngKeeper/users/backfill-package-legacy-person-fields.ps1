# Legacy sorumlu ID -> designContactId / manufactureContactId / responsibleContactId backfill
#
# Onkosul:
#   legacy-kalite-user-id-map.json (provision-legacy-archive-users.ps1)
#
# Kullanim:
#   .\backfill-package-legacy-person-fields.ps1 -DryRun
#   .\backfill-package-legacy-person-fields.ps1

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [string]$MapFile = "",
    [switch]$DryRun,
    [switch]$OnlyMissing
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

$mapState = Load-LegacyKaliteUserIdMap -MapFile $MapFile
if ($mapState.entries.Count -eq 0) {
    throw "legacy-kalite-user-id-map.json bos veya yok. Once provision-legacy-archive-users.ps1 calistirin."
}

$idToKeeper = @{}
foreach ($legacyId in $mapState.entries.Keys) {
    $entry = $mapState.entries[$legacyId]
    $keeperId = [string]$entry.keeperUserId
    if ($keeperId -and $keeperId -ne "DRY-RUN") {
        $idToKeeper[[string]$legacyId] = $keeperId
    }
}
Write-Host "Map: $($idToKeeper.Count) legacyKaliteUserId -> keeperUserId" -ForegroundColor Gray

$dg = Initialize-ProdDgAuthContext -BaseUrl $BaseUrl -UseGateway:$UseGateway
Write-Host "odak_is_paketleri yukleniyor..." -ForegroundColor Yellow
$packages = Get-AllDgDatasetRows -DgContext $dg -Dataset "odak_is_paketleri"
Write-Host "  $($packages.Count) paket" -ForegroundColor Green

function Get-PersonFieldId {
    param($Value)
    if ($null -eq $Value -or $Value -eq "") { return "" }
    if ($Value -is [string]) { return $Value.Trim() }
    if ($Value -is [pscustomobject] -or $Value -is [hashtable]) {
        $o = $Value
        return [string]($o.__dataId ?? $o.dataId ?? $o.userId ?? $o.id ?? "").Trim()
    }
    return [string]$Value
}

$stats = @{
    scanned = 0
    patched = 0
    skipped = 0
    noMap   = 0
    fields  = @{
        designContactId       = 0
        manufactureContactId  = 0
        responsibleContactId  = 0
    }
}

foreach ($pkg in $packages) {
    $stats.scanned++
    $dataId = [string]($pkg.__dataId ?? $pkg.dataId ?? "")
    if (-not $dataId) { continue }

    $patch = @{}
    $changes = @{}

    $designLegacy = Get-LegacyPersonIdFromPackageRow -Value $pkg.legacyDesignResponsibleId
    $manLegacy = Get-LegacyPersonIdFromPackageRow -Value $pkg.legacyManufactureResponsibleId
    $respLegacy = Get-LegacyPersonIdFromPackageRow -Value $pkg.legacyResponsibleId
    $currentDesign = Get-PersonFieldId -Value $pkg.designContactId
    $currentMan = Get-PersonFieldId -Value $pkg.manufactureContactId
    $currentResp = Get-PersonFieldId -Value $pkg.responsibleContactId

    if ($designLegacy) {
        if ($OnlyMissing -and $currentDesign) {
            # skip
        }
        elseif ($idToKeeper.ContainsKey($designLegacy)) {
            $target = $idToKeeper[$designLegacy]
            if ($currentDesign -ne $target) {
                $patch.designContactId = $target
                $changes.designContactId = @{ from = $currentDesign; legacyId = $designLegacy; to = $target }
                $stats.fields.designContactId++
            }
        }
        elseif (-not $currentDesign) {
            $stats.noMap++
        }
    }

    if ($manLegacy) {
        if ($OnlyMissing -and $currentMan) {
            # skip
        }
        elseif ($idToKeeper.ContainsKey($manLegacy)) {
            $target = $idToKeeper[$manLegacy]
            if ($currentMan -ne $target) {
                $patch.manufactureContactId = $target
                $changes.manufactureContactId = @{ from = $currentMan; legacyId = $manLegacy; to = $target }
                $stats.fields.manufactureContactId++
            }
        }
        elseif (-not $currentMan) {
            $stats.noMap++
        }
    }

    if ($respLegacy) {
        if ($OnlyMissing -and $currentResp) {
            # skip
        }
        elseif ($idToKeeper.ContainsKey($respLegacy)) {
            $target = $idToKeeper[$respLegacy]
            if ($currentResp -ne $target) {
                $patch.responsibleContactId = $target
                $changes.responsibleContactId = @{ from = $currentResp; legacyId = $respLegacy; to = $target }
                $stats.fields.responsibleContactId++
            }
        }
        elseif (-not $currentResp) {
            $stats.noMap++
        }
    }

    if ($patch.Count -eq 0) {
        $stats.skipped++
        continue
    }

    $label = [string]($pkg.packageNo ?? $dataId)
    if ($DryRun) {
        Write-Host "[DRY] $label" -ForegroundColor Cyan
        foreach ($key in $patch.Keys) {
            $c = $changes[$key]
            Write-Host "  $key : legacy=$($c.legacyId) $($c.from) -> $($c.to)" -ForegroundColor Yellow
        }
        $stats.patched++
        continue
    }

    $uri = "{0}{1}/odak_is_paketleri/{2}" -f $dg.BaseUrl, $dg.DataPath, $dataId
    Invoke-ProdDgApi -DgContext $dg -Method PUT -Uri $uri -Body $patch | Out-Null
    Write-Host "[OK] $label" -ForegroundColor Green
    foreach ($key in $patch.Keys) {
        $c = $changes[$key]
        Write-Host "  $key : legacy=$($c.legacyId) -> $($c.to)" -ForegroundColor Gray
    }
    $stats.patched++
}

Write-Host "`nOzet: scanned=$($stats.scanned) patched=$($stats.patched) skipped=$($stats.skipped) noMap=$($stats.noMap)" -ForegroundColor Cyan
Write-Host "  designContactId=$($stats.fields.designContactId) manufactureContactId=$($stats.fields.manufactureContactId) responsibleContactId=$($stats.fields.responsibleContactId)" -ForegroundColor Gray

if ($DryRun) {
    Write-Host "(DryRun — DG guncellenmedi)" -ForegroundColor DarkGray
}
