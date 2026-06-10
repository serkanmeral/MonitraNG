# Odak Uretim — master veri seed (musteri, urun grubu, urun)
#
# Usage:
#   .\docs\odak\is_surecleri\scripts\setup-odak-master-datasets.ps1
#   .\docs\odak\is_surecleri\scripts\seed-odak-master-data.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [string]$SeedFile = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if ([string]::IsNullOrEmpty($SeedFile)) {
    $SeedFile = Join-Path $scriptDir "../seed/odak_master_seed.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

$ocScripts = Join-Path (Resolve-Path (Join-Path $scriptDir "../../../..")).Path "docs/odak/operationcore/scripts"
$token = & (Join-Path $ocScripts "load-operationcore-token.ps1")
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$irmParams = @{ Headers = $headers; ErrorAction = "Stop" }
if ($BaseUrl.StartsWith("https://") -and (Get-Command Invoke-RestMethod -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" })) {
    $irmParams.SkipCertificateCheck = $true
}

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $params = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and $irmParams.ContainsKey("SkipCertificateCheck")) { $params.SkipCertificateCheck = $true }
    if ($null -ne $Body) {
        $params.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 20 -Compress }
        $params.ContentType = "application/json"
    }
    return Invoke-RestMethod @params
}

function Get-Items {
    param($Response)
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return $Response }
    foreach ($prop in @("data", "Data", "items", "Items")) {
        if ($null -ne $Response.$prop) {
            $items = $Response.$prop
            if ($items -is [Array]) { return $items }
            return @($items)
        }
    }
    return @($Response)
}

function Get-DataId {
    param($Response)
    if (-not $Response) { return $null }
    $d = $Response.data; if (-not $d) { $d = $Response.Data }; if (-not $d) { $d = $Response }
    $id = $d.__dataId; if (-not $id) { $id = $d.dataId }
    return $id
}

function Upsert-Record {
    param([string]$Collection, [string]$FilterField, [string]$FilterValue, [object]$Body, [string]$Label)
    $filter = "${FilterField}:eq:$FilterValue"
    $uri = "$BaseUrl$dataPath/$Collection" + "?limit=1&filter=" + [Uri]::EscapeDataString($filter)
    $existing = @(Get-Items (Invoke-Dg -Method GET -Uri $uri))
    if ($existing.Count -gt 0) {
        $id = $existing[0].__dataId; if (-not $id) { $id = $existing[0].dataId }
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/$Collection/$id" -Body $Body | Out-Null
        Write-Host "  SYNC: $Label ($id)" -ForegroundColor Yellow
        return $id
    }
    $created = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/$Collection" -Body $Body
    $id = Get-DataId $created
    Write-Host "  OK: $Label -> $id" -ForegroundColor Green
    return $id
}

Write-Host "`nOdak Uretim — master veri seed`n" -ForegroundColor Cyan
$seed = Get-Content $SeedFile -Raw -Encoding UTF8 | ConvertFrom-Json

$idByMusteriKod = @{}
foreach ($row in $seed.musteriler) {
    $body = @{}
    $row.PSObject.Properties | ForEach-Object { $body[$_.Name] = $_.Value }
    $idByMusteriKod[$row.kod] = Upsert-Record -Collection "odak_musteriler" -FilterField "kod" -FilterValue $row.kod -Body $body -Label $row.kod
}

$idByGrupKod = @{}
foreach ($row in $seed.urunGruplari) {
    $body = @{}
    $row.PSObject.Properties | ForEach-Object { $body[$_.Name] = $_.Value }
    $idByGrupKod[$row.kod] = Upsert-Record -Collection "odak_urun_gruplari" -FilterField "kod" -FilterValue $row.kod -Body $body -Label $row.kod
}

$idByPart = @{}
foreach ($row in $seed.urunler) {
    $body = @{
        partNumber = $row.partNumber
        ad         = $row.ad
        revizyon   = $row.revizyon
        birim      = $row.birim
        aktif      = $row.aktif
    }
    if ($row.urunGrubuKod -and $idByGrupKod.ContainsKey($row.urunGrubuKod)) {
        $body.urunGrubuId = $idByGrupKod[$row.urunGrubuKod]
    }
    if ($row.musteriKod -and $idByMusteriKod.ContainsKey($row.musteriKod)) {
        $body.musteriId = $idByMusteriKod[$row.musteriKod]
    }
    $idByPart[$row.partNumber] = Upsert-Record -Collection "odak_urunler" -FilterField "partNumber" -FilterValue $row.partNumber -Body $body -Label $row.partNumber
}

$out = @{
    musteriler   = $idByMusteriKod
    urunGruplari = $idByGrupKod
    urunler      = $idByPart
    seededAt     = (Get-Date).ToUniversalTime().ToString("o")
}
$outFile = Join-Path $scriptDir "../seed/odak_master_ids.json"
$out | ConvertTo-Json -Depth 5 | Set-Content -Path $outFile -Encoding UTF8
Write-Host "`nID ozeti: $outFile" -ForegroundColor Cyan
Write-Host "Tamamlandi.`n" -ForegroundColor Green
