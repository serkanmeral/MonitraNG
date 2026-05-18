# MonitraNG mng_apps - Local Docker Desktop build ve compose up
# Tüm backend servisleri + MngUI + MngDomainUI image'larını build eder ve ayağa kaldırır.
#
# Ön koşul: Ortak altyapı (mng_common) çalışıyor olmalı — MongoDB, Redis, RabbitMQ, Keycloak, MinIO vb.
#            Aksi halde servisler ayağa kalksa bile veritabanı/Keycloak bağlantı hataları alırsınız.
#
# mng_common'ı başlatmak için (ayrı terminalde):
#   cd ApplicationResources\mng_common
#   docker-compose up -d
#
# Bu scripti proje kökünden (MonitraNG) veya ApplicationResources\mng_apps içinden çalıştırın.

$ErrorActionPreference = "Stop"
$AppsDir = (Get-Item $PSScriptRoot).FullName
Set-Location $AppsDir

# Ağlar yoksa oluştur (mng_common genelde bunları kendi compose ile tanımlıyor; yoksa oluştur)
$networks = @("mng_common_mng_network", "mng_network")
foreach ($n in $networks) {
    if (-not (docker network inspect $n 2>$null)) {
        Write-Host "Ağ oluşturuluyor: $n"
        docker network create $n
    } else {
        Write-Host "Ağ zaten mevcut: $n"
    }
}

Write-Host "`n--- Docker Compose BUILD (backend + MngUI + MngDomainUI) ---`n"
docker-compose -f docker-compose.yml build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build hata ile sonlandı. Çıkış kodu: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`n--- Docker Compose UP -d ---`n"
docker-compose -f docker-compose.yml up -d

if ($LASTEXITCODE -ne 0) {
    Write-Host "Compose up hata ile sonlandı. Çıkış kodu: $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "`nServisler ayağa kalktı. Özet portlar (docker-compose.yml):"
Write-Host "  MngGateway:    http://localhost:5040"
Write-Host "  MngKeeper:     http://localhost:5001"
Write-Host "  MngDataGateway: http://localhost:5010"
Write-Host "  MngReactor:    http://localhost:5003"
Write-Host "  MngHub:        http://localhost:5020"
Write-Host "  MngLLM:        http://localhost:5030"
Write-Host "  MngNotifier:   http://localhost:5070"
Write-Host "  MngAdmin:      http://localhost:5080"
Write-Host "  MngScheduler:  http://localhost:5090"
Write-Host "  MngUI:         http://localhost:4000"
Write-Host "  MngDomainUI:   http://localhost:3001"
Write-Host "  Ollama:        http://localhost:11434"
Write-Host "  MkDocs:        http://localhost:6010"
Write-Host "`nDurum: docker-compose -f docker-compose.yml ps"
