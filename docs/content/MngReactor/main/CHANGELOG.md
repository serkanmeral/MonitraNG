# MngReactor Changelog

Tüm önemli değişiklikler bu dosyada dokümante edilir.

Format [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardına uygundur.
Versiyonlama [Semantic Versioning](https://semver.org/spec/v2.0.0.html) kullanır.

## [Unreleased]

### Added
- **NXLog / Linux syslog ingest guards** — `AcceptNxlogIngest` / `AcceptLinuxSyslogIngest` (varsayılan false); host telemetrisi MngLogs agent yoluna.
- **RDP query helpers** — `SecEventRdpActionCodes`; `eventActionPrefix=rdp.` → code 21–25 / `rdp-session` OR.
- **Parse rule seed** — RDP + agent product eşlemeleri (revision bump).
- **Entegrasyon testleri** — WebApplicationFactory ile uyumlu; Health, Ingest, Engine, MonAgents, MonAssets, MonAssetsEncryption testleri (48 test)
- **Docker testleri** — Docker container üzerinde çalışan MngReactor'a karşı HTTP tabanlı testler (`Category=Docker`)
- **Configuration rehberi** — MQTT yapılandırması ve production güvenlik dokümanı
- **Docker support** — mng_apps docker-compose.yml ve docker-compose.production.yml'e MngReactor servisi eklendi
- **Smoke test scripti** — `ApplicationResources/mng_apps/test-mngreactor-docker.ps1` ile Docker container doğrulama

### Changed
- **MQTT ayarları** — appsettings.json'da `monitrang` / `!2345qawsedrf` ile Mosquitto kimlik doğrulaması
- **env.example** — ApplicationResources/mng_common ve mng_apps'e MQTT değişkenleri eklendi
- **AppBootstrapper** — Test ortamında InitAuthentication atlanarak WebApplicationFactory uyumluluğu

### Fixed
- (henüz release edilmemiş düzeltmeler)
