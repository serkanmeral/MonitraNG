# Legacy firms (musteri) -> odak_musteriler (DG, idempotent)
#
# Usage:
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\export-legacy-firms-from-mysql.ps1
#   .\migrate-legacy-firms-to-dg.ps1
#   .\migrate-legacy-firms-to-dg.ps1 -LegacyFirmsJsonPath .\datasets\legacy-firms-customers.json -DryRun

param(
    [string]$LegacyFirmsJsonPath = "",
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")

if ([string]::IsNullOrEmpty($LegacyFirmsJsonPath)) {
    $LegacyFirmsJsonPath = Join-Path $scriptDir "..\datasets\legacy-firms-customers.json"
}

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"
$mappingFile = Join-Path $scriptDir "..\datasets\migration-firm-mapping.json"

if (-not (Test-Path $LegacyFirmsJsonPath)) {
    throw "Firms JSON yok: $LegacyFirmsJsonPath — once export-legacy-firms-from-mysql.ps1"
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

function Get-DgItems {
    param($Response)
    if ($Response -is [Array]) { return @($Response) }
    if ($Response.items) { return @($Response.items) }
    if ($Response.data) { return @($Response.data) }
    return @()
}

function Format-MusteriKod {
    param([string]$LegacyFirmId)
    $num = [int]$LegacyFirmId
    return "MUS-{0:D3}" -f $num
}

function Find-ExistingMusteri {
    param([string]$LegacyFirmId)
    $filter = "legacyFirmId eq '$LegacyFirmId'"
    try {
        $uri = "$BaseUrl$dataPath/odak_musteriler?limit=1&filter=$([Uri]::EscapeDataString($filter))"
        $items = Get-DgItems (Invoke-Dg -Method GET -Uri $uri)
        if ($items.Count -gt 0) { return $items[0] }
    }
    catch { }
    return $null
}

$raw = Get-Content $LegacyFirmsJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
$firms = @($raw.firms)
if (-not $firms.Count) { throw "Firms listesi bos." }

Write-Host "`n=== migrate-legacy-firms-to-dg ===" -ForegroundColor Cyan
Write-Host "Kaynak: $LegacyFirmsJsonPath ($($firms.Count) firma)" -ForegroundColor Cyan
Write-Host "DryRun: $DryRun`n" -ForegroundColor Cyan

$mapping = @{}
$created = 0
$skipped = 0

foreach ($firm in $firms) {
    $legacyId = [string]$firm.id
    $unvan = Limit-LegacyText $firm.name 500
    if ([string]::IsNullOrWhiteSpace($unvan)) {
        Write-Host "SKIP: legacyFirmId=$legacyId (bos unvan)" -ForegroundColor Yellow
        $skipped++
        continue
    }

    $existing = Find-ExistingMusteri -LegacyFirmId $legacyId
    if ($existing) {
        $dgId = $existing.__dataId; if (-not $dgId) { $dgId = $existing.dataId }
        $mapping[$legacyId] = $dgId
        $skipped++
        continue
    }

    $isCustomer = $true
    $isSupplier = $false
    if ($null -ne $firm.is_customer) { $isCustomer = [string]$firm.is_customer -in @('1', 'true', 'True') }
    if ($null -ne $firm.is_supplier) { $isSupplier = [string]$firm.is_supplier -in @('1', 'true', 'True') }

    $kod = Format-MusteriKod -LegacyFirmId $legacyId
    $body = @{
        legacyFirmId = $legacyId
        kod          = $kod
        unvan        = $unvan.Trim()
        isMusteri    = $isCustomer
        isTedarikci  = $isSupplier
        aktif        = $true
    }
    if ($firm.country) { $body.ulke = Limit-LegacyText $firm.country 64 }

    if ($DryRun) {
        Write-Host "[DRY] $kod -> $unvan" -ForegroundColor Yellow
    }
    else {
        $resp = Invoke-Dg -Method POST -Uri "$BaseUrl$dataPath/odak_musteriler" -Body $body
        $dgId = Get-DataId $resp
        $mapping[$legacyId] = $dgId
        $created++
        if ($created % 50 -eq 0) { Write-Host "  ... $created olusturuldu" -ForegroundColor Gray }
    }
}

if (-not $DryRun) {
    foreach ($firm in $firms) {
        $legacyId = [string]$firm.id
        if (-not $mapping.ContainsKey($legacyId)) {
            $hit = Find-ExistingMusteri -LegacyFirmId $legacyId
            if ($hit) {
                $dgId = $hit.__dataId; if (-not $dgId) { $dgId = $hit.dataId }
                $mapping[$legacyId] = $dgId
            }
        }
    }
    @{
        migratedAt = (Get-Date).ToUniversalTime().ToString("o")
        count      = $mapping.Count
        firms      = $mapping
    } | ConvertTo-Json -Depth 4 | Set-Content -Path $mappingFile -Encoding UTF8
    Write-Host "`nMapping: $mappingFile ($($mapping.Count) kayit)" -ForegroundColor Cyan
}

Write-Host "Olusturulan: $created · Atlanan/mevcut: $skipped" -ForegroundColor Green
