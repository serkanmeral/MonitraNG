# Is paketlerinde legacy sorumlu ID kullanim analizi
#
# odak_is_paketleri uzerindeki legacy*Id alanlarini tarar; compare raporu ile birlestirir.
#
# Kullanim:
#   .\scripts\tests\MngKeeper\users\analyze-legacy-package-person-refs.ps1
#   .\scripts\tests\MngKeeper\users\analyze-legacy-package-person-refs.ps1 -DryRun

param(
    [string]$BaseUrl = "http://192.168.20.8:5040",
    [switch]$UseGateway = $true,
    [string]$CompareJsonPath = "",
    [string]$OutputDir = "",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib/LegacyArchiveUserCommon.ps1")

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Get-LegacyArchiveReportsDir
}
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outJson = Join-Path $OutputDir "legacy-package-person-refs_$stamp.json"
$outLatest = Join-Path $OutputDir "legacy-package-person-refs_LATEST.json"
$outMd = Join-Path $OutputDir "legacy-package-person-refs_LATEST.md"

Write-Host "=== Legacy is paketi sorumlu referans analizi ===" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl" -ForegroundColor Gray

$compare = Load-LegacyCompareReport -CompareJsonPath $CompareJsonPath
$legacyLookup = Build-LegacyKaliteUserLookup -CompareReport $compare
Write-Host "Compare raporu: $($compare.summary.legacyTotal) legacy, $($compare.summary.matched) eslesen" -ForegroundColor Gray

$dg = Initialize-ProdDgAuthContext -BaseUrl $BaseUrl -UseGateway:$UseGateway
Write-Host "odak_is_paketleri yukleniyor..." -ForegroundColor Yellow
$packages = Get-AllDgDatasetRows -DgContext $dg -Dataset "odak_is_paketleri"
Write-Host "  $($packages.Count) paket" -ForegroundColor Green

$usageByLegacyId = @{}
$fieldTotals = @{
    legacyResponsibleId            = 0
    legacyDesignResponsibleId      = 0
    legacyManufactureResponsibleId = 0
}

foreach ($pkg in $packages) {
    $pairs = @(
        @{ Field = "legacyResponsibleId"; Value = $pkg.legacyResponsibleId },
        @{ Field = "legacyDesignResponsibleId"; Value = $pkg.legacyDesignResponsibleId },
        @{ Field = "legacyManufactureResponsibleId"; Value = $pkg.legacyManufactureResponsibleId }
    )
    foreach ($pair in $pairs) {
        $legacyId = Get-LegacyPersonIdFromPackageRow -Value $pair.Value
        if (-not $legacyId) { continue }
        $fieldTotals[$pair.Field]++
        Add-LegacyPersonRefUsage -UsageByLegacyId $usageByLegacyId -LegacyId $legacyId -FieldName $pair.Field
    }
}

$personsToProvision = New-Object System.Collections.Generic.List[object]
$alreadyInKeeper = New-Object System.Collections.Generic.List[object]
$unknownLegacyIds = New-Object System.Collections.Generic.List[object]
$excluded = New-Object System.Collections.Generic.List[object]

foreach ($legacyId in ($usageByLegacyId.Keys | Sort-Object { [int]$_ })) {
    $usage = $usageByLegacyId[$legacyId]
    $info = $legacyLookup[$legacyId]

    if (-not $info) {
        [void]$unknownLegacyIds.Add([pscustomobject]@{
            legacyKaliteUserId = $legacyId
            packageCount       = $usage.packageCount
            fieldsUsed         = @($usage.fieldsUsed | Sort-Object)
        })
        continue
    }

    if (Test-LegacyUsernameExcluded -Username $info.LegacyKaliteUsername) {
        [void]$excluded.Add([pscustomobject]@{
            legacyKaliteUserId   = $legacyId
            legacyKaliteUsername = $info.LegacyKaliteUsername
            legacyName           = $info.LegacyName
            reason               = "excluded_username"
            packageCount         = $usage.packageCount
        })
        continue
    }

    $row = [pscustomobject]@{
        legacyKaliteUserId   = $legacyId
        legacyKaliteUsername = $info.LegacyKaliteUsername
        legacyName           = $info.LegacyName
        legacyActive         = $info.LegacyActive
        keeperStatus         = $info.KeeperStatus
        keeperUserId         = $info.KeeperUserId
        packageCount         = $usage.packageCount
        fieldsUsed           = @($usage.fieldsUsed | Sort-Object)
    }

    if ($info.KeeperStatus -eq "matched" -and $info.KeeperUserId) {
        [void]$alreadyInKeeper.Add($row)
    }
    else {
        [void]$personsToProvision.Add($row)
    }
}

$report = [ordered]@{
    generatedAt = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
    baseUrl     = $BaseUrl
    compareSource = if ($CompareJsonPath) { $CompareJsonPath } else { (Find-LatestLegacyCompareJson) }
    summary     = [ordered]@{
        packageCount              = $packages.Count
        uniqueLegacyPersonIds     = $usageByLegacyId.Count
        fieldReferenceCounts      = $fieldTotals
        alreadyMatchedInKeeper    = $alreadyInKeeper.Count
        toProvision               = $personsToProvision.Count
        unknownLegacyIds          = $unknownLegacyIds.Count
        excluded                  = $excluded.Count
    }
    alreadyMatchedInKeeper = @([object[]]($alreadyInKeeper | Sort-Object packageCount -Descending))
    personsToProvision     = @([object[]]($personsToProvision | Sort-Object packageCount -Descending))
    unknownLegacyIds       = @([object[]]($unknownLegacyIds | Sort-Object packageCount -Descending))
    excluded               = @([object[]]$excluded)
}

$libPath = Join-Path $LegacyArchiveRepoRoot "docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1"
. $libPath
Write-Utf8JsonFile -Path $outJson -Object $report -Depth 8
Write-Utf8JsonFile -Path $outLatest -Object $report -Depth 8

$md = @"
# Legacy is paketi — sorumlu referans analizi

**Olusturulma:** $($report.generatedAt)  
**Kaynak:** ``$BaseUrl`` · ``odak_is_paketleri``  
**Compare:** ``$($report.compareSource)``

## Ozet

| Metrik | Deger |
|--------|------:|
| Is paketi | $($report.summary.packageCount) |
| Benzersiz legacy kisi ID | $($report.summary.uniqueLegacyPersonIds) |
| Keeper'da zaten eslesen | $($report.summary.alreadyMatchedInKeeper) |
| **Olusturulacak arsiv kullanici** | **$($report.summary.toProvision)** |
| Bilinmeyen legacy ID | $($report.summary.unknownLegacyIds) |
| Haric tutulan | $($report.summary.excluded) |

### Alan referans sayilari

| Alan | Referans |
|------|----------:|
| legacyResponsibleId | $($fieldTotals.legacyResponsibleId) |
| legacyDesignResponsibleId | $($fieldTotals.legacyDesignResponsibleId) |
| legacyManufactureResponsibleId | $($fieldTotals.legacyManufactureResponsibleId) |

## Olusturulacak arsiv kullanicilar ($($personsToProvision.Count))

| Legacy ID | Username | Ad | Paket | Alanlar | Legacy aktif |
|-----------|----------|-----|------:|---------|:------------:|
"@

foreach ($p in ($personsToProvision | Sort-Object packageCount -Descending)) {
    $fields = ($p.fieldsUsed -join ", ")
    $active = if ($p.legacyActive) { "Evet" } else { "Hayir" }
    $md += "`n| $($p.legacyKaliteUserId) | $($p.legacyKaliteUsername) | $($p.legacyName) | $($p.packageCount) | $fields | $active |"
}

if ($alreadyInKeeper.Count -gt 0) {
    $md += "`n`n## Zaten Keeper'da eslesen ($($alreadyInKeeper.Count))`n`n"
    $md += "| Legacy ID | Username | Keeper userId | Paket |`n|-----------|----------|---------------|------:|`n"
    foreach ($p in ($alreadyInKeeper | Sort-Object packageCount -Descending)) {
        $md += "| $($p.legacyKaliteUserId) | $($p.legacyKaliteUsername) | $($p.keeperUserId) | $($p.packageCount) |`n"
    }
}

if ($unknownLegacyIds.Count -gt 0) {
    $md += "`n`n## Bilinmeyen legacy ID ($($unknownLegacyIds.Count))`n`n"
    $md += "Compare raporunda bulunamadi; muhtemelen kalite.users disi referans.`n`n"
    foreach ($u in $unknownLegacyIds) {
        $md += "- ``$($u.legacyKaliteUserId)`` — $($u.packageCount) paket`n"
    }
}

$md | Out-File -FilePath $outMd -Encoding utf8

Write-Host "`nOzet:" -ForegroundColor Cyan
Write-Host "  Paket: $($packages.Count)" -ForegroundColor White
Write-Host "  Benzersiz legacy kisi: $($usageByLegacyId.Count)" -ForegroundColor White
Write-Host "  Keeper eslesen: $($alreadyInKeeper.Count)" -ForegroundColor Green
Write-Host "  Olusturulacak arsiv: $($personsToProvision.Count)" -ForegroundColor Yellow
Write-Host "  Bilinmeyen ID: $($unknownLegacyIds.Count)" -ForegroundColor $(if ($unknownLegacyIds.Count) { "Red" } else { "Gray" })
Write-Host "`nJSON: $outLatest" -ForegroundColor Gray
Write-Host "MD:   $outMd" -ForegroundColor Gray

if ($DryRun) {
    Write-Host "`n(DryRun — dosyalar yazildi, Keeper/DG degisikligi yok)" -ForegroundColor DarkGray
}
