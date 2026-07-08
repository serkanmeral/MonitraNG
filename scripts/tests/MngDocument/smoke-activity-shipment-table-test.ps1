# Smoke: G2 — Activity raporu + sevkiyat tablosu (queryPage + DocxTableExpander)
# Kabul: context-types, preview, generate/run, shipmentLines placeholder kalmamali
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [string]$ProfileCode = "odak.line.activity.fromLine",
    [string]$TemplateCode = "LINE-ACTIVITY-STD",
    [string]$LineId = "",
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

function Find-LineWithShipmentRows {
    $shipRows = Invoke-Dg -Path "/data/${ShipmentDataset}?limit=10"
    $items = Get-DgListItems -Response $shipRows
    foreach ($row in $items) {
        $parentLineId = if ($row.parentLineId) {
            if ($row.parentLineId -is [string]) { $row.parentLineId }
            elseif ($row.parentLineId.__dataId) { [string]$row.parentLineId.__dataId }
            elseif ($row.parentLineId.dataId) { [string]$row.parentLineId.dataId }
            else { [string]$row.parentLineId }
        } else { $null }
        if (-not [string]::IsNullOrWhiteSpace($parentLineId)) {
            return @{
                LineId = $parentLineId.Trim()
                ShipmentCount = 1
            }
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

function Test-TemplateHasShipmentTableParam {
    param([string]$TemplateId)
    $detail = Invoke-Docs -Path "/templates/$([uri]::EscapeDataString($TemplateId))"
    $shipment = @($detail.parameters) | Where-Object { $_.key -eq "shipmentLines" } | Select-Object -First 1
    if (-not $shipment) { return $false }
    $kind = if ($shipment.kind) { [string]$shipment.kind } else { "scalar" }
    $region = if ($shipment.docBinding) { [string]$shipment.docBinding.regionKind } else { "" }
    return ($kind -eq "table") -and ($region -eq "table")
}

$token = Get-Token
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi." }
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host ""
Write-Host "DI smoke G2: Activity shipment table ($Gateway)" -ForegroundColor Cyan
Write-Host ""

Write-Host "1) context-types (G3)" -ForegroundColor Yellow
$types = @(Invoke-Docs -Path "/generate/context-types")
$requiredTypes = @("odak.siparis.line", "odak.siparis.package")
foreach ($rt in $requiredTypes) {
    $found = $types | Where-Object { $_.type -eq $rt } | Select-Object -First 1
    if (-not $found) { throw "context-types eksik: $rt" }
    Write-Host "  OK $rt ($($found.displayName))" -ForegroundColor Green
}

Write-Host "2) LINE-ACTIVITY-STD published + shipmentLines param" -ForegroundColor Yellow
$template = Find-PublishedTemplate -Code $TemplateCode
if (-not $template) {
    throw "Yayimlanmis sablon yok: $TemplateCode. Once update-line-activity-template-test.ps1 + patch-line-activity-standard-test.ps1 calistirin."
}
if (-not (Test-TemplateHasShipmentTableParam -TemplateId $template.id)) {
    throw "Sablon modelinde kind=table shipmentLines yok. patch-line-activity-standard-test.ps1 (draft) veya -Replace seed gerekli."
}
Write-Host "  OK template id=$($template.id)" -ForegroundColor Green

Write-Host "3) test line (sevkiyat kaydi olan)" -ForegroundColor Yellow
$lineId = $LineId.Trim()
if ([string]::IsNullOrWhiteSpace($lineId)) {
    $pick = Find-LineWithShipmentRows
    if (-not $pick) { throw "Sevkiyat kalemi bulunamadi ($ShipmentDataset). -LineId verin." }
    $lineId = $pick.LineId
}
Write-Host "  OK lineId=$lineId" -ForegroundColor Green

Write-Host "4) generate preview" -ForegroundColor Yellow
$preview = Invoke-Docs -Path "/generate/preview?profileCode=$([uri]::EscapeDataString($ProfileCode))&contextId=$([uri]::EscapeDataString($lineId))"
if ($preview.contextType -ne "odak.siparis.line") {
    throw "preview contextType beklenmiyor: $($preview.contextType)"
}
if (-not $preview.values.lineNo) {
    throw "preview values.lineNo bos."
}
Write-Host "  OK preview lineNo=$($preview.values.lineNo)" -ForegroundColor Green

Write-Host "5) generate/run (RuntimeEnvelope)" -ForegroundColor Yellow
$runBody = @{
    producerCode = $ProfileCode
    context      = @{
        type = "odak.siparis.line"
        id   = $lineId
    }
    trigger      = @{
        kind          = "api"
        correlationId = "smoke-g2-$(Get-Date -Format 'yyyyMMddHHmmss')"
    }
}
$generated = Invoke-Docs -Path "/generate/run" -Method POST -Body $runBody
if ([string]::IsNullOrWhiteSpace($generated.resourceId)) { throw "generate/run resourceId bos." }
if ($generated.profileCode -ne $ProfileCode) { throw "profileCode uyumsuz: $($generated.profileCode)" }

$shipmentPlaceholders = @($generated.remainingPlaceholderKeys) | Where-Object { $_ -like "shipmentLines*" }
if ($shipmentPlaceholders.Count -gt 0) {
    throw "shipmentLines placeholder kaldi: $($shipmentPlaceholders -join ', ')"
}
Write-Host "  OK resourceId=$($generated.resourceId) file=$($generated.fileName)" -ForegroundColor Green
Write-Host "  OK remaining shipmentLines placeholders: 0" -ForegroundColor Green

if (-not $KeepArtifacts) {
    Write-Host "6) cleanup" -ForegroundColor Yellow
    try {
        Invoke-Docs -Path "/resources/$([uri]::EscapeDataString($generated.resourceId))" -Method DELETE | Out-Null
        Write-Host "  OK silindi" -ForegroundColor Green
    }
    catch {
        Write-Host "  Uyari: silinemedi - $($_.Exception.Message)" -ForegroundColor DarkYellow
    }
}

Write-Host ""
Write-Host "G2 Activity shipment table smoke PASSED" -ForegroundColor Green
