# MngReactor token alma - MngDataGateway auth kullanir (MngKeeper)
# Kullanim: $token = .\get-token.ps1

$scriptPath = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $PSCommandPath }
$dataGatewayAuth = Join-Path (Split-Path (Split-Path $scriptPath -Parent) -Parent) "MngDataGateway\auth\get-token.ps1"

if (Test-Path $dataGatewayAuth) {
    return & $dataGatewayAuth
} else {
    Write-Host "Hata: MngDataGateway auth bulunamadi: $dataGatewayAuth" -ForegroundColor Red
    return $null
}
