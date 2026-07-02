# Legacy Kalite SQL dump -> DG (odak_musteriler + odak_is_paketleri + odak_siparis_kalemleri)
# MySQL/Docker gerekmez — kalite-legacy-docker/db/init/01-kalite.sql
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\setup-odak-siparis-datasets.ps1
#   .\migrate-legacy-from-sql-dump.ps1
#   .\migrate-legacy-from-sql-dump.ps1 -Limit 5 -DryRun

param(
    [string]$SqlDumpPath = "",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [int]$Limit = 0,
    [int]$Skip = 0,
    [switch]$SkipFirms,
    [switch]$LinesOnly,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/LegacySqlDumpCommon.ps1")
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

$dumpPath = Get-LegacySqlDumpPath -SqlDumpPath $SqlDumpPath
$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$mappingFile = Join-Path $scriptDir "..\datasets\migration-mapping-dg.json"
$firmMappingFile = Join-Path $scriptDir "..\datasets\migration-firm-mapping.json"

$dgAuth = Initialize-DgMigrationHeaders -TokenScriptPath $ocTokenScript
$headers = $dgAuth.Headers
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    return Invoke-DgMigrationApi -AuthContext $dgAuth -Method $Method -Uri $Uri -Body $Body -RetryOnUnauthorized
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }; if (-not $id) { $id = $d.DataId }
    return $id
}

function Get-DgItems {
    param($Response)
    if ($Response -is [Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    return @()
}

function To-IsoDate {
    param($Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    try { return ([datetime]$Value).ToUniversalTime().ToString("o") }
    catch { return $null }
}

function To-IntOrNull { param($Value); if ($null -eq $Value -or $Value -eq "") { return $null }; if ([string]$Value -notmatch '^\d+$') { return $null }; return [int]$Value }
function To-DoubleOrNull { param($Value); if ($null -eq $Value -or $Value -eq "") { return $null }; return [double]$Value }

function Get-RelationId {
    param($Value)
    if ($null -eq $Value) { return $null }
    if ($Value -is [string]) { return $Value }
    if ($Value.__dataId) { return [string]$Value.__dataId }
    if ($Value.dataId) { return [string]$Value.dataId }
    return [string]$Value
}

function Map-Currency {
    param([string]$LegacyCurrency)
    if ([string]::IsNullOrWhiteSpace($LegacyCurrency)) { return $null }
    switch ($LegacyCurrency.Trim().ToUpperInvariant()) {
        "TL" { return "TRY" }
        "TRY" { return "TRY" }
        "USD" { return "USD" }
        "EUR" { return "EUR" }
        "GBP" { return "GBP" }
        default { return $null }
    }
}

function Get-DgErrorMessage {
    param($ErrorRecord)
    if ($ErrorRecord.ErrorDetails -and $ErrorRecord.ErrorDetails.Message) {
        return [string]$ErrorRecord.ErrorDetails.Message
    }
    if ($ErrorRecord.Exception -and $ErrorRecord.Exception.Response) {
        try {
            $reader = New-Object System.IO.StreamReader($ErrorRecord.Exception.Response.GetResponseStream())
            $body = $reader.ReadToEnd()
            if ($body) { return $body }
        }
        catch { }
    }
    return [string]$ErrorRecord.Exception.Message
}

function Load-ParentLineNoMap {
    param(
        [scriptblock]$InvokeDg,
        [string]$BaseUrl,
        [string]$DataPath
    )
    $map = @{}
    $skip = 0
    $limit = 500
    while ($true) {
        $uri = '{0}{1}/odak_siparis_kalemleri?skip={2}&limit={3}' -f $BaseUrl, $DataPath, $skip, $limit
        $raw = & $InvokeDg -Method GET -Uri $uri
        $items = Get-DgItems $raw
        if (-not $items.Count) { break }
        foreach ($item in $items) {
            $parentId = Get-RelationId $item.parentPackageId
            if (-not $parentId) { $parentId = Get-RelationId $item.parentWorkItemId }
            $lineNo = [string]$item.lineNo
            if ($parentId -and $lineNo) { $map["$parentId|$lineNo"] = $true }
        }
        if ($items.Count -lt $limit) { break }
        $skip += $limit
    }
    return $map
}

function Map-Unit {
    param([string]$LegacyUnit)
    if ([string]::IsNullOrWhiteSpace($LegacyUnit)) { return "adet" }
    $u = $LegacyUnit.Trim().ToLowerInvariant()
    switch -Regex ($u) {
        "^(adet|ad|pcs|ea)$" { return "adet" }
        "^(takim|takım|set)$" { return "takim" }
        "^(kg|kilogram)$" { return "kg" }
        "^(m|metre)$" { return "m" }
        "^(m2|m²)$" { return "m2" }
        default { return "adet" }
    }
}

function Format-MusteriKod {
    param([string]$LegacyFirmId)
    return "MUS-{0:D3}" -f ([int]$LegacyFirmId)
}

function Find-MusteriByLegacyId {
    param([string]$LegacyFirmId, [hashtable]$ExistingMap)
    if ($ExistingMap.ContainsKey($LegacyFirmId)) {
        return @{ __dataId = $ExistingMap[$LegacyFirmId] }
    }
    return $null
}

function Find-PackageByLegacyId {
    param([string]$LegacyPackageId, [hashtable]$ExistingMap)
    if ($ExistingMap.ContainsKey($LegacyPackageId)) {
        return @{ __dataId = $ExistingMap[$LegacyPackageId] }
    }
    return $null
}

Write-Host "`n=== migrate-legacy-from-sql-dump ===" -ForegroundColor Cyan
Write-Host "Dump: $dumpPath" -ForegroundColor Cyan
Write-Host "DryRun: $DryRun  Limit: $Limit  Skip: $Skip`n" -ForegroundColor Cyan

Write-Host "[1] SQL parse..." -ForegroundColor Yellow
$firmRows = Read-SqlInsertRows -Path $dumpPath -TableName "firms"
$packageRows = Read-SqlInsertRows -Path $dumpPath -TableName "packages"
$itemRows = Read-SqlInsertRows -Path $dumpPath -TableName "packageitems"
Write-Host "  firms=$($firmRows.Count) packages=$($packageRows.Count) packageitems=$($itemRows.Count)" -ForegroundColor Green

$itemsByPackage = @{}
foreach ($row in $itemRows) {
    $pkgId = [string]$row[1]
    if (-not $itemsByPackage.ContainsKey($pkgId)) { $itemsByPackage[$pkgId] = @() }
    $itemsByPackage[$pkgId] += ,$row
}

$firmMap = @{}
if (-not $SkipFirms) {
    Write-Host "[2] Musteri migrasyonu..." -ForegroundColor Yellow
    $firmMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_musteriler" -LegacyField "legacyFirmId"
    Write-Host "  Mevcut legacyFirmId map: $($firmMap.Count)" -ForegroundColor Gray
    $customers = @($firmRows | Where-Object { [string]$_[2] -eq '1' })
    Write-Host "  is_customer=1: $($customers.Count)" -ForegroundColor Gray
    $createdFirms = 0
    foreach ($f in $customers) {
        $legacyId = [string]$f[0]
        $unvan = Limit-LegacyText $f[4] 500
        if ([string]::IsNullOrWhiteSpace($unvan)) { continue }
        if ($firmMap.ContainsKey($legacyId)) { continue }

        $body = @{
            legacyFirmId = $legacyId
            kod          = (Format-MusteriKod -LegacyFirmId $legacyId)
            unvan        = $unvan.Trim()
            aktif        = $true
        }
        if ($DryRun) { continue }
        $resp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_musteriler" -Body $body
        $firmMap[$legacyId] = Get-DataId $resp
        $createdFirms++
        if ($createdFirms % 50 -eq 0) { Write-Host "  ... $createdFirms musteri" -ForegroundColor Gray }
    }
    if (-not $DryRun) {
        @{
            migratedAt = (Get-Date).ToUniversalTime().ToString("o")
            count      = $firmMap.Count
            firms      = $firmMap
            source     = "sql-dump"
        } | ConvertTo-Json -Depth 4 | Set-Content -Path $firmMappingFile -Encoding UTF8
    }
    Write-Host "  Musteri map: $($firmMap.Count) (yeni: $createdFirms)" -ForegroundColor Green
}
else {
    if (Test-Path $firmMappingFile) {
        $fm = Get-Content $firmMappingFile -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($prop in $fm.firms.PSObject.Properties) { $firmMap[$prop.Name] = [string]$prop.Value }
    }
    if (-not $firmMap.Count) {
        $firmMap = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_musteriler" -LegacyField "legacyFirmId"
    }
}

Write-Host "[3] Is paketi migrasyonu..." -ForegroundColor Yellow
$existingPackages = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_is_paketleri" -LegacyField "legacyPackageId"
Write-Host "  Mevcut legacyPackageId map: $($existingPackages.Count)" -ForegroundColor Gray
$existingLineIds = @{}
$existingParentLines = @{}
if ($LinesOnly) {
    $existingLineIds = Load-LegacyIdMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -Dataset "odak_siparis_kalemleri" -LegacyField "legacyLineId"
    $existingParentLines = Load-ParentLineNoMap -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath
    Write-Host "  Mevcut legacyLineId map: $($existingLineIds.Count)" -ForegroundColor Gray
    Write-Host "  Mevcut parent+lineNo map: $($existingParentLines.Count)" -ForegroundColor Gray
}
$pkgList = @($packageRows)
if ($Skip -gt 0) { $pkgList = $pkgList | Select-Object -Skip $Skip }
if ($Limit -gt 0) { $pkgList = @($pkgList | Select-Object -First $Limit) }

$migrations = @()
$ok = 0
$skipped = 0
$fail = 0
$lineOk = 0
$lineSkip = 0
$lineFail = 0
$i = 0

foreach ($p in $pkgList) {
    $i++
    $legacyPackageId = [string]$p[0]
    $packageNo = [string]$p[1]
    if ($i % 25 -eq 0 -or $i -eq 1) {
        Write-Host "  [$i/$($pkgList.Count)] $packageNo" -ForegroundColor Cyan
    }

    try {
        $existing = Find-PackageByLegacyId -LegacyPackageId $legacyPackageId -ExistingMap $existingPackages
        if ($existing -and -not $LinesOnly) {
            $skipped++
            continue
        }

        $packageDataId = if ($existing) { [string]$existing.__dataId } else { $null }

        if (-not $LinesOnly) {
            $legacyCustomerId = [string]$p[3]
            $customerId = if ($firmMap.ContainsKey($legacyCustomerId)) { $firmMap[$legacyCustomerId] } else { $null }
            $status = if ([string]$p[19] -eq '1') { 'closed' } else { 'open' }

            $packageBody = @{
                legacyPackageId                = $legacyPackageId
                packageNo                      = $packageNo
                name                           = if ($p[4]) { Limit-LegacyText $p[4] 500 } else { "Is paketi $packageNo" }
                customerId                     = $customerId
                status                         = $status
                closedAt                       = if ($status -eq 'closed') { To-IsoDate $p[21] } else { $null }
                beginDate                      = To-IsoDate $p[20]
                deliveryDate                   = To-IsoDate $p[21]
                deliveryAddress                = if ($p[10]) { Limit-LegacyText $p[10] 500 } else { $null }
                notes                          = if ($p[11]) { Limit-LegacyText $p[11] 2000 } else { $null }
                paymentDetail                  = if ($p[15]) { Limit-LegacyText $p[15] 500 } else { $null }
                partCount                      = To-IntOrNull $p[16]
                stockCount                     = To-IntOrNull $p[17]
                shippedCount                   = To-IntOrNull $p[18]
                poDocumentPath                 = if ($p[6]) { [string]$p[6] } else { $null }
                poDocumentPathRedacted         = if ($p[7]) { [string]$p[7] } else { $null }
                poVersion                      = if ($p[8]) { [string]$p[8] } else { $null }
                legacyResponsibleId            = if ($p[9]) { [string]$p[9] } else { $null }
                legacyDesignResponsibleId      = if ($p[12]) { [string]$p[12] } else { $null }
                legacyManufactureResponsibleId = if ($p[13]) { [string]$p[13] } else { $null }
                legacyContactId                = if ($p[14]) { [string]$p[14] } else { $null }
                legacyCreatedAt                = To-IsoDate $p[23]
                legacyCreatedBy                = if ($p[24]) { [string]$p[24] } else { $null }
                lineCount                      = 0
            }

            if ($DryRun) {
                $ok++
                continue
            }

            $pkgResp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_is_paketleri" -Body $packageBody
            $packageDataId = Get-DataId $pkgResp
            $existingPackages[$legacyPackageId] = $packageDataId
        }
        elseif (-not $packageDataId) {
            $skipped++
            continue
        }

        if ($DryRun) { continue }

        $lineCount = 0
        $legacyItems = if ($itemsByPackage.ContainsKey($legacyPackageId)) {
            @($itemsByPackage[$legacyPackageId])
        }
        else {
            @()
        }
        foreach ($item in $legacyItems) {
            if ($null -eq $item) { continue }
            $legacyLineId = [string]$item[0]
            if ($legacyLineId -notmatch '^\d+$') {
                Write-Host "  LINE SKIP $packageNo invalid legacyLineId='$legacyLineId'" -ForegroundColor DarkYellow
                $lineSkip++
                continue
            }
            if ($existingLineIds.ContainsKey($legacyLineId)) {
                $lineSkip++
                continue
            }
            $lineNo = To-IntOrNull $item[3]
            if (-not $lineNo -or $lineNo -lt 1) {
                Write-Host "  LINE SKIP $packageNo invalid lineNo='$($item[3])' legacyLineId=$legacyLineId" -ForegroundColor DarkYellow
                $lineSkip++
                continue
            }
            $parentLineKey = "$packageDataId|$lineNo"
            if ($existingParentLines.ContainsKey($parentLineKey)) {
                $lineSkip++
                continue
            }

            $description = Limit-LegacyText $item[6] 2000
            if ([string]::IsNullOrWhiteSpace($description)) {
                $description = "Legacy kalem $lineNo"
            }
            $mappedCurrency = Map-Currency ([string]$item[13])

            $lineBody = @{
                parentPackageId   = $packageDataId
                parentWorkItemId  = $packageDataId
                lineNo            = $lineNo
                customerProjectNo = Limit-LegacyText $item[2] 64
                customerPoNo      = Limit-LegacyText $item[4] 64
                customerPoItemNo  = To-IntOrNull $item[5]
                description       = $description
                poItemRevNo       = Limit-LegacyText $item[7] 32
                customerJobNo     = Limit-LegacyText $item[8] 64
                quantity          = if ($null -ne $item[9] -and $item[9] -ne "") { [double]$item[9] } else { 0 }
                unit              = Map-Unit ([string]$item[10])
                unitCost          = To-DoubleOrNull $item[11]
                totalCost         = To-DoubleOrNull $item[12]
                qualityReqs       = Limit-LegacyText $item[14] 1000
                isFai             = [bool]([int]$item[15] -eq 1)
                isFaiComplete     = [bool]([int]$item[16] -eq 1)
                shipmentDate      = To-IsoDate $item[17]
                shipmentAddress   = Limit-LegacyText $item[18] 500
                legacyLineId      = $legacyLineId
                legacyPackageId   = $legacyPackageId
                shippedQuantity   = 0
            }
            if ($mappedCurrency) { $lineBody.currency = $mappedCurrency }

            try {
                Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_siparis_kalemleri" -Body $lineBody | Out-Null
                $existingLineIds[$legacyLineId] = "1"
                $existingParentLines[$parentLineKey] = $true
                $lineCount++
                $lineOk++
            }
            catch {
                $detail = Get-DgErrorMessage $_
                Write-Host "  LINE HATA $packageNo #$lineNo (legacyLineId=$legacyLineId): $detail" -ForegroundColor Red
                $lineFail++
            }
        }

        if ($lineCount -gt 0) {
            try {
                Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/odak_is_paketleri/$packageDataId" -Body @{ lineCount = $lineCount } | Out-Null
            }
            catch {
                Write-Host "  lineCount PUT HATA $packageNo : $(Get-DgErrorMessage $_)" -ForegroundColor Yellow
            }
        }

        $migrations += [pscustomobject]@{
            legacyPackageId = $legacyPackageId
            legacyPackageNo = $packageNo
            packageDataId   = $packageDataId
            lineCount       = $lineCount
        }
        $existingPackages[$legacyPackageId] = $packageDataId
        $ok++
    }
    catch {
        Write-Host "  HATA $packageNo : $(Get-DgErrorMessage $_)" -ForegroundColor Red
        $fail++
    }
}

if (-not $DryRun -and $migrations.Count -gt 0) {
    $existing = @()
    if (Test-Path $mappingFile) {
        try {
            $raw = Get-Content $mappingFile -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($raw.migrations) { $existing = @($raw.migrations) }
        }
        catch { }
    }
    $merged = @($existing + $migrations)
    @{ migrations = $merged; source = "sql-dump"; migratedAt = (Get-Date).ToUniversalTime().ToString("o") } |
        ConvertTo-Json -Depth 6 | Set-Content -Path $mappingFile -Encoding UTF8
}

Write-Host "`nBitti: OK=$ok SKIP=$skipped HATA=$fail | Kalemler OK=$lineOk SKIP=$lineSkip HATA=$lineFail" -ForegroundColor $(if ($fail -eq 0 -and $lineFail -eq 0) { "Green" } else { "Yellow" })
Write-Host "Dogrulama: .\verify-legacy-dg-migration.ps1 -UseSqlDump" -ForegroundColor Gray
