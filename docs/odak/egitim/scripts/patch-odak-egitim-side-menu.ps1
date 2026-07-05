# Odak Egitim — side menu (Odak Siparis altinda)
#
# Egitim menusu artik Odak Siparis basligi altinda "Egitim" item olarak tanimlanir.
# Bu script patch-odak-siparis-side-menu.ps1 cagirir (legacy Odak Egitim header devre disi).
#
# Usage (repo kokunden):
#   .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
#   .\docs\odak\egitim\scripts\patch-odak-egitim-side-menu.ps1
#
# Production:
#   $env:MNG_OC_USE_PROD_TOKEN = "1"; .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
#   .\docs\odak\egitim\scripts\patch-odak-egitim-side-menu.ps1 -BaseUrl "http://192.168.20.8:5040"

param(
    [string]$BaseUrl = "http://192.168.20.20:5040",
    [switch]$WhatIf = $false
)

$ErrorActionPreference = "Stop"
$siparisPatch = Join-Path (Split-Path $PSScriptRoot -Parent) "..\siparis\scripts\patch-odak-siparis-side-menu.ps1"
if (-not (Test-Path $siparisPatch)) { throw "Siparis side menu patch yok: $siparisPatch" }

Write-Host "Odak Egitim menusu -> Odak Siparis altina (patch-odak-siparis-side-menu.ps1)" -ForegroundColor Cyan
& $siparisPatch -BaseUrl $BaseUrl -WhatIf:$WhatIf
