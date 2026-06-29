# Probe: Gotenberg rendering status via MngDocument API (prod)
param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token-prod.ps1"

$token = if (Test-Path $TokenFile) {
    (Get-Content $TokenFile -Raw).Trim()
} else {
    & $loadToken
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Prod token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }
$uri = "$Gateway/documents/api/v1/rendering/status"

try {
    $status = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 30
} catch {
    $code = $_.Exception.Response.StatusCode.value__
    Write-Host "FAIL $uri -> HTTP $code" -ForegroundColor Red
    if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
    exit 1
}

Write-Host "OK $uri" -ForegroundColor Green
$status | ConvertTo-Json -Depth 3

if (-not $status.gotenbergReachable) {
    Write-Host "Gotenberg erisilemiyor. Sunucuda: docker compose up -d gotenberg" -ForegroundColor Yellow
    exit 1
}

Write-Host "Document rendering altyapisi hazir." -ForegroundColor Cyan
