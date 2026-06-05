param(
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt",
    [switch]$AutoRefresh = $true
)

$getScript = Join-Path $PSScriptRoot "get-operationcore-token-prod.ps1"

if ($AutoRefresh -or -not (Test-Path $TokenFile)) {
    if (-not (Test-Path $getScript)) {
        Write-Host "get-operationcore-token-prod.ps1 bulunamadi" -ForegroundColor Red
        return $null
    }
    Write-Host "Production Operation Core token aliniyor..." -ForegroundColor Cyan
    $token = & $getScript
    if (-not [string]::IsNullOrEmpty($token)) { return $token.Trim() }
    return $null
}

$token = (Get-Content $TokenFile -Raw).Trim()
if (-not [string]::IsNullOrEmpty($token)) {
    Write-Host "Prod token yuklendi" -ForegroundColor Green
    return $token
}
return $null
