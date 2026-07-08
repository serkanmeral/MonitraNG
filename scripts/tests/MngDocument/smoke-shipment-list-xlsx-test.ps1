# Smoke: G5 — İş paketi sevkiyat listesi XLSX (XlsxTableExpander + outputFormat)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [string]$ProfileCode = "odak.shipmentList.fromPackage",
    [string]$TemplateCode = "SHIPMENT-LIST-STD",
    [string]$PackageId = "",
    [string]$ShipmentDataset = "odak_sevkiyat_kalemleri",
    [switch]$KeepArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

function Get-Token {
    if (Test-Path $TokenFile) {
        $t = (Get-Content $TokenFile -Raw).Trim()
        if ($t) { return $t }
    }
    return (& $loadToken).Trim()
}

function Invoke-Docs {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [object]$Body = $null
    )
    $uri = "$Gateway/documents/api/v1$Path"
    $params = @{
        Uri        = $uri
        Method     = $Method
        Headers    = $script:Headers
        TimeoutSec = 180
    }
    if ($Body -ne $null) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress)
    }
    if ((Get-Command Invoke-RestMethod).Parameters.ContainsKey('SkipCertificateCheck')) {
        $params.SkipCertificateCheck = $true
    }
    return Invoke-RestMethod @params
}

function Invoke-Dg {
    param([string]$Path)
    $uri = "$Gateway/data/api/v1$Path"
    $dgParams = @{
        Uri        = $uri
        Headers    = $script:Headers
        TimeoutSec = 120
    }
    if ((Get-Command Invoke-RestMethod).Parameters.ContainsKey('SkipCertificateCheck')) {
        $dgParams.SkipCertificateCheck = $true
    }
    return Invoke-RestMethod @dgParams
}

function Get-DgListItems {
    param([object]$Response)
    if ($Response -is [System.Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    return @($Response)
}

function Get-RelationId {
    param([object]$Field)
    if (-not $Field) { return $null }
    if ($Field -is [string]) { return $Field.Trim() }
    if ($Field.__dataId) { return [string]$Field.__dataId }
    if ($Field.dataId) { return [string]$Field.dataId }
    return [string]$Field
}

function Find-PackageWithShipmentRows {
    $shipRows = Invoke-Dg -Path "/data/${ShipmentDataset}?limit=20"
    $items = Get-DgListItems -Response $shipRows
    foreach ($row in $items) {
        $packageId = Get-RelationId $row.parentPackageId
        if (-not [string]::IsNullOrWhiteSpace($packageId)) {
            return @{ PackageId = $packageId }
        }
    }
    return $null
}

function Find-PublishedTemplate {
    param([string]$Code)
    $list = Invoke-Docs -Path "/templates"
    return @($list.items) | Where-Object {
        ($_.code -eq $Code) -and (([string]$_.status).ToLower() -eq "published")
    } | Select-Object -First 1
}

function Test-TemplateHasShipmentSheetParam {
    param([string]$TemplateId)
    $detail = Invoke-Docs -Path "/templates/$([uri]::EscapeDataString($TemplateId))"
    $shipment = @($detail.parameters) | Where-Object { $_.key -eq "shipmentLines" } | Select-Object -First 1
    if (-not $shipment) { return $false }
    $kind = if ($shipment.kind) { [string]$shipment.kind } else { "scalar" }
    $region = if ($shipment.docBinding) { [string]$shipment.docBinding.regionKind } else { "" }
    $ref = if ($shipment.dataSourceRef) { [string]$shipment.dataSourceRef } else { "" }
    return ($kind -eq "table") -and ($region -eq "sheet") -and ($ref -eq "odak.packageShipmentLines.byPackage")
}

$token = Get-Token
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi." }
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host ""
Write-Host "DI smoke G5: Shipment list XLSX ($Gateway)" -ForegroundColor Cyan
Write-Host ""

Write-Host "1) producer catalog" -ForegroundColor Yellow
$producer = Invoke-Docs -Path "/generate/producers/$([uri]::EscapeDataString($ProfileCode))"
if ($producer.code -ne $ProfileCode) { throw "Producer bulunamadi: $ProfileCode" }
Write-Host "  OK $($producer.displayName)" -ForegroundColor Green

Write-Host "2) SHIPMENT-LIST-STD published + shipmentLines sheet param" -ForegroundColor Yellow
$template = Find-PublishedTemplate -Code $TemplateCode
if (-not $template) {
    throw "Yayimlanmis sablon yok: $TemplateCode. deploy-shipment-list-design-test.ps1 calistirin."
}
if (-not (Test-TemplateHasShipmentSheetParam -TemplateId $template.id)) {
    throw "Sablon modelinde kind=table shipmentLines (sheet) yok."
}
Write-Host "  OK template id=$($template.id)" -ForegroundColor Green

Write-Host "3) test package (sevkiyat kaydi olan)" -ForegroundColor Yellow
$packageId = $PackageId.Trim()
if ([string]::IsNullOrWhiteSpace($packageId)) {
    $pick = Find-PackageWithShipmentRows
    if (-not $pick) { throw "Sevkiyat kalemi bulunamadi ($ShipmentDataset). -PackageId verin." }
    $packageId = $pick.PackageId
}
Write-Host "  OK packageId=$packageId" -ForegroundColor Green

Write-Host "4) generate preview" -ForegroundColor Yellow
$preview = Invoke-Docs -Path "/generate/preview?profileCode=$([uri]::EscapeDataString($ProfileCode))&contextId=$([uri]::EscapeDataString($packageId))"
if ($preview.contextType -ne "odak.siparis.package") {
    throw "preview contextType beklenmiyor: $($preview.contextType)"
}
if (-not $preview.values.packageNo) {
    throw "preview values.packageNo bos."
}
Write-Host "  OK preview packageNo=$($preview.values.packageNo)" -ForegroundColor Green

Write-Host "5) generate/run (RuntimeEnvelope)" -ForegroundColor Yellow
$runBody = @{
    producerCode = $ProfileCode
    context      = @{
        type = "odak.siparis.package"
        id   = $packageId
    }
    trigger      = @{
        kind          = "api"
        correlationId = "smoke-g5-$(Get-Date -Format 'yyyyMMddHHmmss')"
    }
}
$generated = Invoke-Docs -Path "/generate/run" -Method POST -Body $runBody
if ([string]::IsNullOrWhiteSpace($generated.resourceId)) { throw "generate/run resourceId bos." }
if ($generated.profileCode -ne $ProfileCode) { throw "profileCode uyumsuz: $($generated.profileCode)" }
if (-not $generated.fileName.EndsWith(".xlsx")) { throw "fileName .xlsx degil: $($generated.fileName)" }

$shipmentPlaceholders = @($generated.remainingPlaceholderKeys) | Where-Object { $_ -like "shipmentLines*" }
if ($shipmentPlaceholders.Count -gt 0) {
    throw "shipmentLines placeholder kaldi: $($shipmentPlaceholders -join ', ')"
}
Write-Host "  OK resourceId=$($generated.resourceId) file=$($generated.fileName)" -ForegroundColor Green
Write-Host "  OK remaining shipmentLines placeholders: 0" -ForegroundColor Green

Write-Host "6) writeback (G5+ — odak_is_paketleri)" -ForegroundColor Yellow
$pkg = Invoke-Dg -Path "/data/odak_is_paketleri/$([uri]::EscapeDataString($packageId))"
$wbResourceId = [string]$pkg.shipmentListDiResourceId
$wbFileName = [string]$pkg.shipmentListFileName
if ([string]::IsNullOrWhiteSpace($wbResourceId)) {
    throw "writeback shipmentListDiResourceId bos."
}
if ($wbResourceId -ne $generated.resourceId) {
    throw "writeback resourceId uyumsuz: beklenen=$($generated.resourceId) gercek=$wbResourceId"
}
if ([string]::IsNullOrWhiteSpace($wbFileName)) {
    throw "writeback shipmentListFileName bos."
}
Write-Host "  OK shipmentListDiResourceId=$wbResourceId" -ForegroundColor Green
Write-Host "  OK shipmentListFileName=$wbFileName" -ForegroundColor Green

if (-not $KeepArtifacts) {
    Write-Host "7) cleanup" -ForegroundColor Yellow
    try {
        Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($generated.resourceId))" -Method DELETE | Out-Null
        Write-Host "  OK silindi" -ForegroundColor Green
    }
    catch {
        Write-Host "  Uyari: silinemedi - $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "G5 Shipment list XLSX smoke PASSED" -ForegroundColor Green
