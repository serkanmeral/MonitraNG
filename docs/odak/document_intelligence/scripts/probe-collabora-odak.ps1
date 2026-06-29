# Collabora + WOPI saglik kontrolu (Odak test veya prod)
#
#   .\docs\odak\document_intelligence\scripts\probe-collabora-odak.ps1
#   .\docs\odak\document_intelligence\scripts\probe-collabora-odak.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [string]$Token = $env:DI_TOKEN
)

$ErrorActionPreference = "Stop"
$hostIp = if ($BaseUrl -match "192\.168\.20\.8") { "192.168.20.8" } else { "192.168.20.20" }
$collaboraUrl = "http://${hostIp}:9980"
$wopiHost = "http://${hostIp}:5095"

$token = $Token
if ([string]::IsNullOrEmpty($token)) {
    $loadScript = if ($hostIp -eq "192.168.20.8") {
        Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token-prod.ps1"
    } else {
        Join-Path $PSScriptRoot "..\..\operationcore\scripts\load-operationcore-token.ps1"
    }
    if (Test-Path $loadScript) { $token = & $loadScript }
}
if ([string]::IsNullOrEmpty($token)) {
    Write-Host "Token yok." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $($token.Trim())" }

Write-Host ""
Write-Host "Collabora probe — $hostIp" -ForegroundColor Cyan
Write-Host ""

function Test-Http {
    param([string]$Label, [string]$Uri)
    try {
        $r = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 15
        Write-Host "  OK $Label HTTP $($r.StatusCode)" -ForegroundColor Green
        return $true
    }
    catch {
        $code = $null
        try { $code = [int]$_.Exception.Response.StatusCode } catch { }
        Write-Host "  FAIL $Label HTTP $code — $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Test-Http -Label "Collabora discovery" -Uri "$collaboraUrl/hosting/discovery" | Out-Null
Test-Http -Label "MngDocument health (WOPI host)" -Uri "$wopiHost/health" | Out-Null

try {
    $render = Invoke-RestMethod -Uri "$BaseUrl/documents/api/v1/rendering/status" -Headers $headers
    $gotenberg = [bool]$render.gotenbergReachable
    Write-Host "  $(if ($gotenberg) { 'OK' } else { 'WARN' }) Gotenberg reachable: $gotenberg" -ForegroundColor $(if ($gotenberg) { 'Green' } else { 'Yellow' })
}
catch {
    Write-Host "  FAIL rendering/status — $($_.Exception.Message)" -ForegroundColor Red
}

try {
    $tree = Invoke-RestMethod -Uri "$BaseUrl/documents/api/v1/template-categories/tree" -Headers $headers
    $count = @($tree).Count
    Write-Host "  OK template categories (roots=$count)" -ForegroundColor Green
}
catch {
    Write-Host "  FAIL template-categories — $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "Beklenen mngdocument env (test):" -ForegroundColor Gray
Write-Host "  Collabora__PublicBaseUrl = $collaboraUrl" -ForegroundColor Gray
Write-Host "  Wopi__HostBaseUrl = http://mngdocument:5095 (container ic)" -ForegroundColor Gray
Write-Host ""
