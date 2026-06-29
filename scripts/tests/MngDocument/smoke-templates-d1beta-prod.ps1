# Smoke: MngDocument D1-beta templates + categories on production
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

$paths = @(
    "/documents/api/v1/templates",
    "/documents/api/v1/template-categories/tree"
)

foreach ($path in $paths) {
    $uri = "$Gateway$path"
    try {
        $r = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 30
        Write-Host "OK $uri" -ForegroundColor Green
        $r | ConvertTo-Json -Depth 4 -Compress
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "FAIL $uri -> HTTP $code" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
        exit 1
    }
}

Write-Host "D1-beta smoke tamam." -ForegroundColor Cyan
