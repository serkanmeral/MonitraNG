# MngReactor token yukleme - MngDataGateway auth kullanir (MngKeeper)
# Kullanim: $token = .\load-token.ps1

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$dataGatewayAuth = Join-Path (Split-Path (Split-Path $scriptPath -Parent) -Parent) "MngDataGateway\auth\load-token.ps1"

if (Test-Path $dataGatewayAuth) {
    return & $dataGatewayAuth
} else {
    Write-Host "Hata: MngDataGateway auth bulunamadi: $dataGatewayAuth" -ForegroundColor Red
    return $null
}
