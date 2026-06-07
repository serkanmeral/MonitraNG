# Tek seferlik: mnghub + mngoperations + mngui deploy (T1-T3 toaster)
$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
Set-Location $RepoRoot

Write-Host "=== Toast deploy: sync ===" -ForegroundColor Cyan
& (Join-Path $RepoRoot "scripts/odak/sync-odak-source.ps1") -Paths @(
    "MngHub",
    "MngOperations",
    "Mng.Ui",
    "ApplicationResources/mng_apps"
)

Write-Host "=== Toast deploy: build + up ===" -ForegroundColor Cyan
& (Join-Path $RepoRoot "scripts/odak/deploy-odak-apps.ps1") -Services @(
    "mnghub",
    "mngoperations",
    "mngui"
) -NoCache

Write-Host "=== Smoke: Hub user-notify ===" -ForegroundColor Cyan
$probeUrls = @(
    "http://192.168.20.20:5040/hub/api/v1/internal/user-notify",
    "http://192.168.20.20:5020/api/v1/internal/user-notify"
)
$body = '{"userId":"6a0f8fd13d6ba5d774ee37c7","payload":{"title":"Deploy probe","message":"ok","notificationType":"DeployProbe"}}'
foreach ($url in $probeUrls) {
    try {
        $r = Invoke-WebRequest -Method POST -Uri $url -ContentType "application/json" -Body $body -UseBasicParsing
        Write-Host "  OK $url -> $($r.StatusCode)" -ForegroundColor Green
    }
    catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "  $url -> HTTP $code" -ForegroundColor Yellow
    }
}

Write-Host "=== UI health ===" -ForegroundColor Cyan
try {
    $ui = Invoke-WebRequest -Uri "http://192.168.20.20:3000/" -UseBasicParsing -TimeoutSec 20
    Write-Host "  mngui: $($ui.StatusCode)" -ForegroundColor Green
}
catch {
    Write-Host "  mngui FAIL: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Deploy tamamlandi." -ForegroundColor Cyan
