# Legacy ncs/cpas -> odak_ncr + odak_capa (DG, kurum semasi, idempotent)
#
# Usage:
#   .\export-legacy-ncs-from-mysql.ps1
#   .\migrate-legacy-ncs-to-dg.ps1
#   .\migrate-legacy-ncs-to-dg.ps1 -RepairText   # mevcut kayitlarda metin alanlarini duzelt

param(
    [string]$LegacyJsonPath = "",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun,
    [switch]$RepairText
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrEmpty($LegacyJsonPath)) {
    $LegacyJsonPath = Join-Path $scriptDir "..\datasets\legacy-ncs-cpas.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$reportPath = Join-Path $scriptDir "..\datasets\legacy-ncs-migration-report.json"

if (-not (Test-Path $LegacyJsonPath)) {
    throw "JSON yok: $LegacyJsonPath — once export-legacy-ncs-from-mysql.ps1"
}

$token = & $ocTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $p.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 10 -Compress }
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Test-LegacyNumericId {
    param([string]$Value)
    return ($Value -match '^\d+$')
}

function To-IsoDate {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -eq "NULL") { return $null }
    try {
        return ([datetime]$Value).ToUniversalTime().ToString("o")
    }
    catch { return $null }
}

function Map-CapaStatus {
    param([string]$ClosedDate, [string]$Result, [string]$LegacyStatus)
    if ($LegacyStatus -eq 'Kapalı' -or $LegacyStatus -eq 'Kapali') { return "Kapali" }
    if ($ClosedDate -and $ClosedDate -ne "NULL") { return "Kapali" }
    if ($Result -and $Result -ne "0" -and $Result -ne "NULL") { return "Takip" }
    return "Acik"
}

function Build-NcrBody {
    param($nc, [string]$LegacyNcrId, [string]$ParentPackageId)
    $descriptor = Limit-LegacyText $nc.descriptor 500
    if ([string]::IsNullOrWhiteSpace($descriptor) -or $descriptor.Trim().Length -lt 2) {
        $descriptor = if ($nc.nc_no) { "NCR $([string]$nc.nc_no)" } else { "NCR $LegacyNcrId" }
    }

    $body = @{
        legacyNcrId     = $LegacyNcrId
        parentPackageId = $ParentPackageId
        ncStatus        = if ($nc.nc_status) { Limit-LegacyText $nc.nc_status 200 } else { "Değerlendirme Bekleniyor" }
        descriptor      = $descriptor.Trim()
    }
    if ($nc.nc_no) { $body.legacyNcNo = [string]$nc.nc_no }
    if ($nc.nc_date) { $d = To-IsoDate ([string]$nc.nc_date); if ($d) { $body.ncDate = $d } }
    if ($nc.control_type) { $body.controlType = Limit-LegacyText $nc.control_type 200 }
    if ($nc.explanation) { $body.explanation = Limit-LegacyText $nc.explanation 4000 }
    if ($nc.product_code) { $body.productCode = Limit-LegacyText $nc.product_code 128 }
    if ($nc.job_no) { $body.jobNo = Limit-LegacyText $nc.job_no 128 }
    foreach ($numField in @("part_count", "rework_count", "repair_count", "observe_count", "scrap_count", "asis_count", "return_count", "other_count")) {
        $val = $nc.$numField
        if ($null -ne $val -and [string]$val -ne "NULL") {
            $camel = ($numField -split '_') | ForEach-Object { $_.Substring(0,1).ToUpper() + $_.Substring(1) }
            $prop = ($camel -join '')
            $prop = $prop.Substring(0,1).ToLower() + $prop.Substring(1)
            try { $body[$prop] = [double]$val } catch { }
        }
    }
    if ($nc.fai_status) { $body.faiStatus = Limit-LegacyText $nc.fai_status 200 }
    if ($nc.error_code) { $body.errorCode = Limit-LegacyText $nc.error_code 128 }
    if ($nc.nc_action) { $body.ncAction = Limit-LegacyText $nc.nc_action 2000 }
    if ($nc.responsible) { $body.responsible = Limit-LegacyText $nc.responsible 256 }
    if ($nc.closure_date) { $d = To-IsoDate ([string]$nc.closure_date); if ($d) { $body.closureDate = $d } }
    if ($nc.notes) { $body.notes = Limit-LegacyText $nc.notes 4000 }
    return $body
}

$raw = Get-Content $LegacyJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$ncsList = @($raw.ncs)
$cpasList = @($raw.cpas)
$linksList = @($raw.cpasNcs)

Write-Host "`n=== migrate-legacy-ncs-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $LegacyJsonPath ($($ncsList.Count) ncs, $($cpasList.Count) cpas)" -ForegroundColor Cyan
Write-Host "DryRun: $DryRun  RepairText: $RepairText`n" -ForegroundColor Cyan

$report = @{
    runAt          = (Get-Date).ToUniversalTime().ToString("o")
    sourceFile     = $LegacyJsonPath
    dryRun         = [bool]$DryRun
    repairText     = [bool]$RepairText
    ncr            = @{ created = 0; skippedExisting = 0; repaired = 0; failed = 0; invalidId = 0; noPackage = 0; packageNotInDg = 0 }
    capa           = @{ created = 0; skippedExisting = 0; failed = 0; noNcrLink = 0; packageNotInDg = 0 }
    ncrFailures    = @()
    capaFailures   = @()
}

$ncPackageMap = @{}
foreach ($nc in $ncsList) {
    $lid = [string]$nc.id
    if (Test-LegacyNumericId $lid) {
        $ncPackageMap[$lid] = [string]$nc.package_id
    }
}

Write-Host "DG haritalari yukleniyor..." -ForegroundColor Gray
$script:PackageLegacyMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
$script:NcrLegacyMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_ncr" -LegacyField "legacyNcrId"
$script:CapaLegacyMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_capa" -LegacyField "legacyCapaId"
Write-Host "  paket=$($script:PackageLegacyMap.Count) ncr=$($script:NcrLegacyMap.Count) capa=$($script:CapaLegacyMap.Count)" -ForegroundColor Gray

$ncrMapping = @{}
foreach ($kv in $script:NcrLegacyMap.GetEnumerator()) {
    $ncrMapping[$kv.Key] = $kv.Value
}

foreach ($nc in $ncsList) {
    $legacyNcrId = [string]$nc.id
    $legacyPackageId = [string]$nc.package_id

    if (-not (Test-LegacyNumericId $legacyNcrId)) {
        $report.ncr.invalidId++
        $report.ncrFailures += @{ legacyNcrId = $legacyNcrId; reason = "invalid_id" }
        continue
    }

    if ([string]::IsNullOrWhiteSpace($legacyPackageId)) {
        $report.ncr.noPackage++
        $report.ncrFailures += @{ legacyNcrId = $legacyNcrId; reason = "empty_package_id" }
        continue
    }

    if ($script:NcrLegacyMap.ContainsKey($legacyNcrId)) {
        $dgId = $script:NcrLegacyMap[$legacyNcrId]
        $ncrMapping[$legacyNcrId] = $dgId

        if ($RepairText) {
            $parentPackageId = $script:PackageLegacyMap[$legacyPackageId]
            if (-not $parentPackageId) { continue }
            $body = Build-NcrBody -nc $nc -LegacyNcrId $legacyNcrId -ParentPackageId $parentPackageId
            $textFields = @("ncStatus", "descriptor", "explanation", "notes", "ncAction", "responsible", "errorCode", "faiStatus", "controlType", "productCode", "jobNo")
            $patch = @{}
            foreach ($f in $textFields) {
                if ($body.ContainsKey($f) -and $null -ne $body[$f]) { $patch[$f] = $body[$f] }
            }
            if ($patch.Count -gt 0 -and -not $DryRun) {
                try {
                    Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_ncr/$dgId" -Body $patch | Out-Null
                    $report.ncr.repaired++
                }
                catch {
                    $report.ncrFailures += @{ legacyNcrId = $legacyNcrId; reason = "repair_failed"; detail = "$_" }
                }
            }
            elseif ($patch.Count -gt 0) {
                $report.ncr.repaired++
            }
        }
        else {
            $report.ncr.skippedExisting++
        }
        continue
    }

    $parentPackageId = $script:PackageLegacyMap[$legacyPackageId]
    if (-not $parentPackageId) {
        $report.ncr.packageNotInDg++
        $report.ncr.failed++
        $report.ncrFailures += @{ legacyNcrId = $legacyNcrId; legacyPackageId = $legacyPackageId; reason = "package_not_in_dg" }
        continue
    }

    $body = Build-NcrBody -nc $nc -LegacyNcrId $legacyNcrId -ParentPackageId $parentPackageId

    if ($DryRun) {
        Write-Host "[DRY NCR] legacy=$legacyNcrId" -ForegroundColor Yellow
        $report.ncr.created++
    }
    else {
        try {
            $resp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_ncr" -Body $body
            $dgId = Get-DataId $resp
            $ncrMapping[$legacyNcrId] = $dgId
            $script:NcrLegacyMap[$legacyNcrId] = $dgId
            $report.ncr.created++
        }
        catch {
            $report.ncr.failed++
            $report.ncrFailures += @{ legacyNcrId = $legacyNcrId; reason = "post_failed"; detail = "$_" }
        }
    }
}

$cpaToNc = @{}
foreach ($link in $linksList) {
    $cpaToNc[[string]$link.cpa_id] = [string]$link.nc_id
}

foreach ($cp in $cpasList) {
    $legacyCapaId = [string]$cp.id
    if (-not (Test-LegacyNumericId $legacyCapaId)) { continue }

    if ($script:CapaLegacyMap.ContainsKey($legacyCapaId)) {
        $report.capa.skippedExisting++
        continue
    }

    $legacyCapaNo = if ($cp.form_no) { [string]$cp.form_no } elseif ($cp.cpa_no) { [string]$cp.cpa_no } else { "$($cp.form_year)-$($cp.id)" }
    $description = Limit-LegacyText $cp.descript 2000
    if ([string]::IsNullOrWhiteSpace($description)) { $description = "CAPA $legacyCapaId" }

    $legacyNcrId = $cpaToNc[$legacyCapaId]
    $legacyPackageId = $null
    if ($legacyNcrId -and $ncPackageMap.ContainsKey($legacyNcrId)) {
        $legacyPackageId = $ncPackageMap[$legacyNcrId]
    }
    if (-not $legacyPackageId) {
        $report.capa.noNcrLink++
        $report.capa.skippedExisting++
        continue
    }

    $parentPackageId = $script:PackageLegacyMap[$legacyPackageId]
    if (-not $parentPackageId) {
        $report.capa.packageNotInDg++
        $report.capa.failed++
        $report.capaFailures += @{ legacyCapaId = $legacyCapaId; legacyPackageId = $legacyPackageId; reason = "package_not_in_dg" }
        continue
    }

    $body = @{
        legacyCapaId    = $legacyCapaId
        legacyCapaNo    = $legacyCapaNo
        parentPackageId = $parentPackageId
        capaStatus      = (Map-CapaStatus -ClosedDate ([string]$cp.closed_date) -Result ([string]$cp.result) -LegacyStatus ([string]$cp.status))
        description     = $description.Trim()
    }
    if ($cp.cpa_date) { $d = To-IsoDate ([string]$cp.cpa_date); if ($d) { $body.cpaDate = $d } }
    if ($cp.source) { $body.source = Limit-LegacyText $cp.source 500 }
    if ($cp.request_division) { $body.requestDivision = Limit-LegacyText $cp.request_division 256 }
    if ($cp.nonconformity) { $body.nonconformity = Limit-LegacyText $cp.nonconformity 4000 }
    if ($cp.tecnique) { $body.tecnique = Limit-LegacyText $cp.tecnique 2000 }
    if ($cp.error_code) { $body.errorCode = Limit-LegacyText $cp.error_code 128 }
    if ($cp.first_followup_date) { $d = To-IsoDate ([string]$cp.first_followup_date); if ($d) { $body.firstFollowupDate = $d } }
    if ($cp.second_followup_date) { $d = To-IsoDate ([string]$cp.second_followup_date); if ($d) { $body.secondFollowupDate = $d } }
    if ($cp.closed_date) { $d = To-IsoDate ([string]$cp.closed_date); if ($d) { $body.closedDate = $d } }

    if ($legacyNcrId) {
        if (-not $ncrMapping.ContainsKey($legacyNcrId)) {
            if ($script:NcrLegacyMap.ContainsKey($legacyNcrId)) {
                $ncrMapping[$legacyNcrId] = $script:NcrLegacyMap[$legacyNcrId]
            }
        }
        if ($ncrMapping.ContainsKey($legacyNcrId)) {
            $body.parentNcrId = $ncrMapping[$legacyNcrId]
        }
    }

    if ($DryRun) {
        Write-Host "[DRY CAPA] legacy=$legacyCapaId" -ForegroundColor Yellow
        $report.capa.created++
    }
    else {
        try {
            Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_capa" -Body $body | Out-Null
            $report.capa.created++
        }
        catch {
            $report.capa.failed++
            $report.capaFailures += @{ legacyCapaId = $legacyCapaId; reason = "post_failed"; detail = "$_" }
        }
    }
}

Write-Host "`nNCR: yeni=$($report.ncr.created) mevcut=$($report.ncr.skippedExisting) duzeltildi=$($report.ncr.repaired) hata=$($report.ncr.failed)" -ForegroundColor Green
Write-Host "     gecersizId=$($report.ncr.invalidId) paketYok=$($report.ncr.noPackage) dgPaketYok=$($report.ncr.packageNotInDg)" -ForegroundColor Green
Write-Host "CAPA: yeni=$($report.capa.created) atlanan=$($report.capa.skippedExisting) hata=$($report.capa.failed)" -ForegroundColor Green
Write-Host "      ncrBaglantisiYok=$($report.capa.noNcrLink) dgPaketYok=$($report.capa.packageNotInDg)" -ForegroundColor Green

. (Join-Path $scriptDir "lib/LegacyMysqlCommon.ps1")
Write-Utf8JsonFile -Path $reportPath -Object $report -Depth 6
Write-Host "Rapor: $reportPath" -ForegroundColor Gray
