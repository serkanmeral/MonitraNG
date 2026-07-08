# Smoke: G4 — producer catalog + dataSourceRef table generation
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt",
    [string]$ProfileCode = "odak.line.activity.fromLine",
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
    param([string]$Method = "GET", [string]$Path, [object]$Body = $null)
    $uri = "$Gateway/documents/api/v1$Path"
    $params = @{ Uri = $uri; Method = $Method; Headers = $script:Headers; TimeoutSec = 180 }
    if ($Body -ne $null) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 12 -Compress)
    }
    if ((Get-Command Invoke-RestMethod).Parameters.ContainsKey('SkipCertificateCheck')) {
        $params.SkipCertificateCheck = $true
    }
    return Invoke-RestMethod @params
}

$token = Get-Token
if ([string]::IsNullOrWhiteSpace($token)) { throw "Token alinamadi." }
$script:Headers = @{ Authorization = "Bearer $token" }

Write-Host ""
Write-Host "DI smoke G4: producers + dataSourceRef ($Gateway)" -ForegroundColor Cyan
Write-Host ""

Write-Host "1) producers API" -ForegroundColor Yellow
$producers = @(Invoke-Docs -Path "/generate/producers")
$activity = $producers | Where-Object { $_.code -eq $ProfileCode } | Select-Object -First 1
$coc = $producers | Where-Object { $_.code -eq "odak.coc.fromLine" } | Select-Object -First 1
if (-not $activity) { throw "Producer eksik: $ProfileCode" }
if (-not $coc) { throw "Producer eksik: odak.coc.fromLine" }
Write-Host "  OK producers count=$($producers.Count) activity=$($activity.templateCode)" -ForegroundColor Green

Write-Host "2) producer detail" -ForegroundColor Yellow
$detail = Invoke-Docs -Path "/generate/producers/$([uri]::EscapeDataString($ProfileCode))"
if ($detail.contextType -ne "odak.siparis.line") { throw "producer contextType beklenmiyor" }
Write-Host "  OK detail template=$($detail.templateCode)" -ForegroundColor Green

Write-Host "3) activity template dataSourceRef" -ForegroundColor Yellow
$list = Invoke-Docs -Path "/templates"
$tpl = @($list.items) | Where-Object { $_.code -eq "LINE-ACTIVITY-STD" } | Select-Object -First 1
if (-not $tpl) { throw "LINE-ACTIVITY-STD bulunamadi" }
$tplDetail = Invoke-Docs -Path "/templates/$([uri]::EscapeDataString($tpl.id))"
$shipment = @($tplDetail.parameters) | Where-Object { $_.key -eq "shipmentLines" } | Select-Object -First 1
if (-not $shipment) { throw "shipmentLines parametresi yok" }
$ref = if ($shipment.dataSourceRef) { [string]$shipment.dataSourceRef } else { "" }
if ($ref -ne "odak.shipmentLines.byParentLine") {
    throw "dataSourceRef beklenmiyor: '$ref' (deploy-line-activity-design-test.ps1 calistirin)"
}
Write-Host "  OK dataSourceRef=$ref" -ForegroundColor Green

Write-Host "4) generate/run (catalog producer)" -ForegroundColor Yellow
& (Join-Path $PSScriptRoot "smoke-activity-shipment-table-test.ps1") -Gateway $Gateway -TokenFile $TokenFile -ProfileCode $ProfileCode -KeepArtifacts:$KeepArtifacts

Write-Host ""
Write-Host "G4 producers + dataSourceRef smoke PASSED" -ForegroundColor Green
