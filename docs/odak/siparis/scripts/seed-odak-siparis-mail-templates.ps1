# Odak Siparis — @mail_templates seed (is paketi bildirim sablonlari)
#
# Usage (repo kokunden):
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\docs\odak\siparis\scripts\seed-odak-siparis-mail-templates.ps1 -BaseUrl "http://192.168.20.8:5040"
#
# Dev:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\siparis\scripts\seed-odak-siparis-mail-templates.ps1

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
$seedFile = Join-Path $scriptDir "..\datasets\odak_siparis_mail_templates_seed.json"

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }

$loadTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
if (-not (Test-Path $loadTokenScript)) { throw "Token script yok: $loadTokenScript" }
$token = & $loadTokenScript
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

$headers = @{
    Authorization  = "Bearer $token"
    "Content-Type" = "application/json"
}
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $p = @{ Uri = $Uri; Method = $Method; Headers = $headers; ErrorAction = "Stop" }
    if ($Uri.StartsWith("https://") -and (Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck")) {
        $p.SkipCertificateCheck = $true
    }
    if ($null -ne $Body) {
        $p.Body = if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 25 -Compress }
        $p.ContentType = "application/json"
    }
    return Invoke-RestMethod @p
}

function Get-Items($Response) {
    if (-not $Response) { return @() }
    if ($Response -is [Array]) { return @($Response) }
    foreach ($prop in @("items", "data", "Data", "Items")) {
        if ($null -ne $Response.$prop) {
            $arr = $Response.$prop
            if ($arr -is [Array]) { return @($arr) }
            return @($arr)
        }
    }
    return @($Response)
}

function Get-DataId($row) {
    if (-not $row) { return $null }
    if ($row.__dataId) { return [string]$row.__dataId }
    if ($row.dataId) { return [string]$row.dataId }
    return $null
}

if (-not (Test-Path $seedFile)) { throw "Seed dosyasi yok: $seedFile" }
$seed = Get-Content $seedFile -Raw -Encoding UTF8 | ConvertFrom-Json
$dataset = $seed.dataset
$listUri = "$BaseUrl$dataPath/$dataset`?limit=500"

Write-Host "`n=== seed-odak-siparis-mail-templates ===" -ForegroundColor Cyan
Write-Host "Gateway: $BaseUrl | DryRun: $DryRun`n" -ForegroundColor Gray

$existing = @{}
try {
    $list = Invoke-Dg -Method GET -Uri $listUri
    foreach ($row in Get-Items $list) {
        $key = [string]$row.templateKey
        if ($key) { $existing[$key] = Get-DataId $row }
    }
}
catch {
    Write-Host "WARN: Mevcut sablonlar listelenemedi — yalnizca POST denenecek." -ForegroundColor Yellow
}

$created = 0
$updated = 0
$skipped = 0

foreach ($rec in $seed.records) {
    $key = [string]$rec.templateKey
    if (-not $key) { continue }

    if ($DryRun) {
        if ($existing.ContainsKey($key)) {
            Write-Host "[DRY] PUT $key (mevcut)" -ForegroundColor Yellow
        }
        else {
            Write-Host "[DRY] POST $key" -ForegroundColor Yellow
        }
        continue
    }

    if ($existing.ContainsKey($key)) {
        $id = $existing[$key]
        $body = @{}
        foreach ($prop in $rec.PSObject.Properties) {
            if ($prop.Name -eq "_comment") { continue }
            $body[$prop.Name] = $prop.Value
        }
        Invoke-Dg -Method PUT -Uri "$BaseUrl$dataPath/$dataset/$id" -Body $body | Out-Null
        Write-Host "OK: Guncellendi -> $key" -ForegroundColor Green
        $updated++
    }
    else {
        $body = @{}
        foreach ($prop in $rec.PSObject.Properties) {
            if ($prop.Name -eq "_comment") { continue }
            $body[$prop.Name] = $prop.Value
        }
        try {
            Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/$dataset" -Body $body | Out-Null
            Write-Host "OK: Olusturuldu -> $key" -ForegroundColor Green
            $created++
        }
        catch {
            if ($_.Exception.Message -match "409|duplicate|unique|mevcut|zaten") {
                Write-Host "SKIP: $key (zaten var)" -ForegroundColor Yellow
                $skipped++
            }
            else { throw }
        }
    }
}

Write-Host "`nTamamlandi. Olusturulan: $created, Guncellenen: $updated, Atlanan: $skipped" -ForegroundColor Cyan
Write-Host "Politika UI: /apps/odak-siparis/packages/settings (Bildirimler sekmesi)" -ForegroundColor Gray
Write-Host "Sablon onizleme: MngNotifier POST /api/v1/notifications/preview-template" -ForegroundColor Gray
