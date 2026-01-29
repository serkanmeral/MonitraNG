# MkDocs'u Docker ile serve et — Python kurulumu gerekmez
# Çalıştırma: docs/ klasöründeyken .\run-docs-docker.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "MkDocs Docker serve baslatiliyor (live reload)..." -ForegroundColor Green
Write-Host "Tarayici: http://localhost:6010" -ForegroundColor Cyan
Write-Host "Durdurmak icin: Ctrl+C" -ForegroundColor Yellow
Write-Host ""

docker compose -f docker-compose.serve.yml up --build
