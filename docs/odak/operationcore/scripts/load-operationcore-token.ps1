# Operation Core - token yukle veya get-operationcore-token.ps1 ile yenile
# Usage: $token = .\load-operationcore-token.ps1

param(
    [string]$TokenFile = "",
    [switch]$AutoRefresh = $false
)

$useProd = ($env:MNG_OC_USE_PROD_TOKEN -eq "1")
if ([string]::IsNullOrEmpty($TokenFile)) {
    $TokenFile = if ($useProd) { "$env:TEMP\operationcore_dg_token_prod.txt" } else { "$env:TEMP\operationcore_dg_token.txt" }
}

$getOcTokenScript = if ($useProd) {
    Join-Path $PSScriptRoot "get-operationcore-token-prod.ps1"
}
else {
    Join-Path $PSScriptRoot "get-operationcore-token.ps1"
}

if ($AutoRefresh -or -not (Test-Path $TokenFile)) {
    if (-not (Test-Path $getOcTokenScript)) {
        Write-Host "get-operationcore-token.ps1 bulunamadi: $getOcTokenScript" -ForegroundColor Red
        return $null
    }
    Write-Host "Operation Core token aliniyor..." -ForegroundColor Cyan
    $token = & $getOcTokenScript
    if (-not [string]::IsNullOrEmpty($token)) { return $token.Trim() }
    return $null
}

$token = (Get-Content $TokenFile -Raw).Trim()
if (-not [string]::IsNullOrEmpty($token)) {
    Write-Host "Token yuklendi: $TokenFile" -ForegroundColor Green
    return $token
}

Write-Host "Token dosyasi bos; once get-operationcore-token.ps1 calistirin." -ForegroundColor Red
return $null
