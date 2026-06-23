# Smoke: MngDocument templates API on Odak test server
param(
    [string]$Gateway = "http://192.168.20.20:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token.txt"
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$loadToken = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = if (Test-Path $TokenFile) {
    (Get-Content $TokenFile -Raw).Trim()
} else {
    & $loadToken
}

if ([string]::IsNullOrWhiteSpace($token)) {
    Write-Host "Token bulunamadi." -ForegroundColor Red
    exit 1
}

$headers = @{ Authorization = "Bearer $token" }

foreach ($path in @("/documents/api/v1/health", "/documents/api/v1/templates")) {
    $uri = "$Gateway$path"
    try {
        $r = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec 30
        Write-Host "OK $uri" -ForegroundColor Green
        $r | ConvertTo-Json -Depth 3 -Compress
    } catch {
        $code = $_.Exception.Response.StatusCode.value__
        Write-Host "FAIL $uri -> HTTP $code" -ForegroundColor Red
        if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
    }
}
