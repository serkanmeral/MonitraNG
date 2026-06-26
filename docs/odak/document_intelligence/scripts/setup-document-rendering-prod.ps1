# Document rendering altyapisi (Gotenberg / headless LibreOffice) — Production
# On kosul: mng_apps stack ayakta; mngdocument Gotenberg'e baglanir.
#
# Kullanim (prod sunucuda, mng_apps dizininde):
#   docker compose -f docker-compose.production.yml -f docker-compose.odak.prod.yml --env-file .env up -d gotenberg mngdocument
#
# Yerel gelistirme PC'den (sync sonrasi SSH deploy script'i tercih edilir):
#   pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services gotenberg,mngdocument -NoCache

param(
    [string]$Gateway = "http://192.168.20.8:5040",
    [string]$TokenFile = "$env:TEMP\operationcore_dg_token_prod.txt"
)

$ErrorActionPreference = "Stop"
Write-Host "=== Document Rendering (Gotenberg) prod probe ===" -ForegroundColor Cyan
Write-Host "Gateway: $Gateway" -ForegroundColor Gray
Write-Host ""
Write-Host "Sunucuda once gotenberg + mngdocument ayaga kalkmali:" -ForegroundColor Yellow
Write-Host "  docker compose ... up -d gotenberg mngdocument" -ForegroundColor Gray
Write-Host ""

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../../..")).Path
$probe = Join-Path $repoRoot "scripts/tests/MngDocument/probe-document-rendering-prod.ps1"
if (Test-Path $probe) {
    & $probe -Gateway $Gateway -TokenFile $TokenFile
} else {
    Write-Host "Probe script bulunamadi: $probe" -ForegroundColor Red
    exit 1
}
