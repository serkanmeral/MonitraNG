$ErrorActionPreference = 'Stop'
$Base = 'http://192.168.20.8:5040'
$Ui = 'http://192.168.20.8:3000'
$env:MNG_OC_USE_PROD_TOKEN = '1'
$repoRoot = 'c:\Users\monitra\Dev\MonitraNG\MonitraNG'
$token = & (Join-Path $repoRoot 'docs/odak/operationcore/scripts/load-operationcore-token.ps1') -AutoRefresh
$hdr = @{ Authorization = "Bearer $token"; 'X-Domain-Name' = 'odak' }

Write-Host '=== POST-DEPLOY SIEM VERIFY ===' -ForegroundColor Cyan

foreach ($label in @('gateway-health', 'ui-health', 'reactor-health-ui')) {
    $url = switch ($label) {
        'gateway-health' { "$Base/health" }
        'ui-health' { "$Ui/" }
        'reactor-health-ui' { "$Ui/api/reactor/v1/health" }
    }
    $r = Invoke-WebRequest -Uri $url -Headers $hdr -UseBasicParsing -TimeoutSec 30
    Write-Host "  OK $label -> $($r.StatusCode)" -ForegroundColor Green
}

$sw = [System.Diagnostics.Stopwatch]::StartNew()
$r1 = Invoke-WebRequest -Uri "$Base/reactor/api/v1/sec-events/dashboard-summary?rangeHours=24" -Headers $hdr -UseBasicParsing -TimeoutSec 60
$sw.Stop()
Write-Host "  OK gateway dashboard-summary (1st) -> $($r1.StatusCode) in $($sw.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Green

$sw1b = [System.Diagnostics.Stopwatch]::StartNew()
$r1b = Invoke-WebRequest -Uri "$Base/reactor/api/v1/sec-events/dashboard-summary?rangeHours=24" -Headers $hdr -UseBasicParsing -TimeoutSec 60
$sw1b.Stop()
Write-Host "  OK gateway dashboard-summary (2nd, server cache) -> $($r1b.StatusCode) in $($sw1b.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Green

$sw2 = [System.Diagnostics.Stopwatch]::StartNew()
$r2 = Invoke-WebRequest -Uri "$Ui/api/reactor/v1/sec-events/dashboard-summary?rangeHours=24" -Headers $hdr -UseBasicParsing -TimeoutSec 60
$sw2.Stop()
Write-Host "  OK ui-proxy dashboard-summary -> $($r2.StatusCode) in $($sw2.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Green

# Dedup check: second call should be faster (client cache)
$sw3 = [System.Diagnostics.Stopwatch]::StartNew()
$r3 = Invoke-WebRequest -Uri "$Ui/api/reactor/v1/sec-events/dashboard-summary?rangeHours=24" -Headers $hdr -UseBasicParsing -TimeoutSec 60
$sw3.Stop()
Write-Host "  OK ui-proxy dashboard-summary (2nd) -> $($r3.StatusCode) in $($sw3.Elapsed.TotalSeconds.ToString('F2'))s" -ForegroundColor Green

if ($sw.Elapsed.TotalSeconds -gt 10) {
    Write-Host "  WARN gateway > 10s" -ForegroundColor Yellow
} else {
    Write-Host "  PASS gateway < 10s" -ForegroundColor Green
}

Write-Host "`nVerify tamam." -ForegroundColor Green
