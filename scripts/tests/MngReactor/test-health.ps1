# MngReactor Health Endpoint Test
# GET /api/v1/health, /api/v1/health/live, /api/v1/health/ready
# Onkosullar: MngReactor calisiyor (port 15010)

param(
    [string]$BaseUrl = "http://localhost:15010"
)

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
try {
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls11
} catch { }

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$hasSkipCertCheck = $false
try {
    $hasSkipCertCheck = Get-Command Invoke-RestMethod | Select-Object -ExpandProperty Parameters | Where-Object { $_.Name -eq "SkipCertificateCheck" }
} catch { }

Write-Host ""
Write-Host "========================================"
Write-Host "MngReactor Health Test"
Write-Host "========================================"
Write-Host "BaseUrl: $BaseUrl"
Write-Host ""

$results = @()
$params = @{ Uri = ""; Method = "GET"; ErrorAction = "Stop" }
if ($hasSkipCertCheck) { $params.SkipCertificateCheck = $true }

# Test 1: Ana health endpoint
Write-Host "[1] GET /api/v1/health ..."
try {
    $params.Uri = "$BaseUrl/api/v1/health"
    $health = Invoke-RestMethod @params
    if ($health.Status -eq "healthy") {
        Write-Host "  PASS: Status = $($health.Status)" -ForegroundColor Green
        $results += "PASS"
    } else {
        Write-Host "  WARN: Status = $($health.Status)" -ForegroundColor Yellow
        $results += "WARN"
    }
    if ($health.Checks) {
        $health.Checks.PSObject.Properties | ForEach-Object {
            Write-Host "    $($_.Name): $($_.Value.Status) - $($_.Value.Message)"
        }
    }
} catch {
    Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
    $results += "FAIL"
}
Write-Host ""

# Test 2: Liveness
Write-Host "[2] GET /api/v1/health/live ..."
try {
    $params.Uri = "$BaseUrl/api/v1/health/live"
    $live = Invoke-RestMethod @params
    if ($live.status -eq "alive") {
        Write-Host "  PASS: status = $($live.status)" -ForegroundColor Green
        $results += "PASS"
    } else {
        Write-Host "  FAIL: Unexpected status = $($live.status)" -ForegroundColor Red
        $results += "FAIL"
    }
} catch {
    Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
    $results += "FAIL"
}
Write-Host ""

# Test 3: Readiness
Write-Host "[3] GET /api/v1/health/ready ..."
try {
    $params.Uri = "$BaseUrl/api/v1/health/ready"
    $ready = Invoke-RestMethod @params
    if ($ready.status -eq "ready") {
        Write-Host "  PASS: status = $($ready.status)" -ForegroundColor Green
        $results += "PASS"
    } else {
        Write-Host "  FAIL: Unexpected status = $($ready.status)" -ForegroundColor Red
        $results += "FAIL"
    }
} catch {
    Write-Host "  FAIL: $($_.Exception.Message)" -ForegroundColor Red
    $results += "FAIL"
}
Write-Host ""

# Ozet
Write-Host "========================================"
Write-Host "Ozet: $($results | Where-Object { $_ -eq 'PASS' } | Measure-Object | Select-Object -ExpandProperty Count)/$($results.Count) test basarili"
if (($results | Where-Object { $_ -eq "FAIL" }).Count -gt 0) {
    exit 1
}
exit 0
