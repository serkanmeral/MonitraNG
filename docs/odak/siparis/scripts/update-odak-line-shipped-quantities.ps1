# odak_siparis_kalemleri.shippedQuantity — tamamlanan sevkiyatlardan toplam (filter API kullanmaz)
#
# Usage:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"
#   .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\siparis\scripts\update-odak-line-shipped-quantities.ps1 -BaseUrl http://192.168.20.8:5040

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$UseGateway = $true,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../../../..")).Path
. (Join-Path $scriptDir "lib/DgMigrationCommon.ps1")
. (Join-Path $scriptDir "lib/UpdateOdakLineShippedQuantities.ps1")

$dataPath = if ($UseGateway) { "/data/api/v1/data" } else { "/api/v1/data" }
$ocTokenScript = Join-Path $repoRoot "docs/odak/operationcore/scripts/load-operationcore-token.ps1"

$token = (& $ocTokenScript).Trim()
if ([string]::IsNullOrEmpty($token)) { throw "Token alinamadi." }

function Update-MigrationToken {
    param([switch]$ForceRefresh)
    if ($ForceRefresh) {
        Write-Host "  Token yenileniyor (Keycloak)..." -ForegroundColor Yellow
        $script:token = (& $ocTokenScript -AutoRefresh).Trim()
    }
    else {
        $tokenFile = if ($env:MNG_OC_USE_PROD_TOKEN -eq "1") { "$env:TEMP\operationcore_dg_token_prod.txt" } else { "$env:TEMP\operationcore_dg_token.txt" }
        if (Test-Path $tokenFile) {
            $script:token = (Get-Content $tokenFile -Raw).Trim()
        }
        else {
            $script:token = (& $ocTokenScript -AutoRefresh:$false).Trim()
        }
        if ([string]::IsNullOrEmpty($script:token)) {
            $script:token = (& $ocTokenScript -AutoRefresh).Trim()
        }
    }
    if ([string]::IsNullOrEmpty($script:token)) { throw "Token alinamadi." }
    $script:headers["Authorization"] = "Bearer $($script:token)"
}

$script:headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type"  = "application/json"
}
$script:token = $token
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function Invoke-Dg {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    for ($attempt = 0; $attempt -lt 2; $attempt++) {
        Update-MigrationToken
        $skipCert = $Uri.StartsWith("https://")
        try {
            return Invoke-DgRestMethod -Method $Method -Uri $Uri -Headers $script:headers -Body $Body -JsonDepth 10 -SkipCertificateCheck:$skipCert
        }
        catch {
            $detail = [string]$_.Exception.Message
            if ($attempt -eq 0 -and ($detail -match '401|Unauthorized')) {
                Write-Host "  401 — Keycloak token yenileniyor..." -ForegroundColor Yellow
                Update-MigrationToken -ForceRefresh
                continue
            }
            throw
        }
    }
}

Invoke-OdakLineShippedQuantityBackfill -InvokeDg ${function:Invoke-Dg} -BaseUrl $BaseUrl -DataPath $dataPath -DryRun:$DryRun
