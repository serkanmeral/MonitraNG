# SIEM / alarm — gecici E2E test kurallarini temizle (siem-mvp-v1 paket kurallari korunur)
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$Domain = "odak",
    [switch]$Apply
)

$ErrorActionPreference = "Stop"
$tokenScript = Join-Path $PSScriptRoot "..\tests\MngDataGateway\auth\get-token.ps1"
$null = & $tokenScript -KeeperBaseUrl $Gateway -DomainName $Domain -Username "odak_admin" -Password "Admin123!"
$tokenFile = Join-Path $env:TEMP "serkan_token.txt"
if (-not (Test-Path $tokenFile)) { throw "Token dosyasi yok: $tokenFile" }
$token = (Get-Content -Path $tokenFile -Raw).Trim()
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = $Domain; "Content-Type" = "application/json" }
$rulesApi = "$Gateway/alarm/api/v1/rules"

$e2eNamePatterns = @(
    '^(U([1-9]|10)|U1 Linux) (SIEM|WF|Block) E2E\b',
    '^P4 auth failure correlation$',
    '^crud-e2e-',
    '^Bench lag\b',
    '^U1 probe\b'
)

function Get-RuleList {
    param([string]$Uri, [hashtable]$Headers)
    $raw = Invoke-RestMethod -Uri $Uri -Headers $Headers
    if ($null -eq $raw) { return @() }
    if ($raw -is [System.Array]) { return @($raw) }
    return @($raw)
}

Write-Host "=== SIEM E2E alarm rule cleanup ===" -ForegroundColor Cyan
if (-not $Apply) {
    Write-Host "   Dry-run (silmek icin -Apply)" -ForegroundColor Yellow
}

$rules = Get-RuleList -Uri $rulesApi -Headers $hdr
$toDelete = @($rules | Where-Object {
    if ($_.metadata -and $_.metadata.packageId) { return $false }
    $name = [string]$_.name
    if ($name -match ' E2E\b') { return $true }
    foreach ($pat in $e2eNamePatterns) {
        if ($name -match $pat) { return $true }
    }
    return $false
})

Write-Host "   Toplam kural=$($rules.Count) silinecek E2E=$($toDelete.Count)" -ForegroundColor DarkGray

foreach ($rule in $toDelete) {
    if ($Apply) {
        Invoke-RestMethod -Uri "$rulesApi/$($rule.id)" -Method DELETE -Headers $hdr | Out-Null
        Write-Host "   DEL $($rule.name) ($($rule.id))" -ForegroundColor Green
    } else {
        Write-Host "   WOULD DEL $($rule.name) ($($rule.id))" -ForegroundColor DarkGray
    }
}

if ($Apply) {
    Write-Host "`nOK $($toDelete.Count) E2E alarm rule silindi" -ForegroundColor Green
} else {
    Write-Host "`nOK dry-run tamam (-Apply ile silinir)" -ForegroundColor Green
}
exit 0
