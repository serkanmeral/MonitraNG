# MngReactor Docker build ve compose up
# Sadece mngreactor servisini build eder ve ayağa kaldırır.
# Ön koşul: mng_common ayağa kalkmış olmalı (MongoDB, RabbitMQ, Mosquitto vb.)
#            mngkeeper ve mngdatagateway de çalışıyor olmalı (Reactor bunlara bağımlı).
#
# Kullanım (MonitraNG proje kökünden):
#   .\ApplicationResources\mng_apps\build-and-up-mngreactor.ps1
#
# Veya mng_apps içinden:
#   .\build-and-up-mngreactor.ps1

$ErrorActionPreference = "Stop"
$AppsDir = (Get-Item $PSScriptRoot).FullName
Set-Location $AppsDir

Write-Host ""
Write-Host "=== MngReactor Docker Build ===" -ForegroundColor Cyan
docker-compose -f docker-compose.yml build mngreactor --no-cache

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build hata ile sonlandı. Çıkış kodu: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "=== MngReactor Docker Compose Up ===" -ForegroundColor Cyan
docker-compose -f docker-compose.yml up -d mngreactor

if ($LASTEXITCODE -ne 0) {
    Write-Host "Compose up hata ile sonlandı. Çıkış kodu: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "MngReactor ayağa kalktı." -ForegroundColor Green
Write-Host "  Port: http://localhost:5003"
Write-Host "  Health: http://localhost:5003/api/v1/health"
Write-Host "  Config: http://localhost:5003/api/v1/engine/config?engineId=..."
Write-Host ""
Write-Host "Not: Reactor, mngkeeper ve mngdatagateway'e bağımlıdır. Bunlar çalışmıyorsa önce:"
Write-Host "  cd ApplicationResources\mng_common && docker-compose up -d"
Write-Host "  docker-compose -f docker-compose.yml up -d mngkeeper mngdatagateway"
Write-Host ""
