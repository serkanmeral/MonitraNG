# MngEngine Roadmap

Yaptıklarımız, yapacaklarımız ve kararlarımız bu dosyada güncellenecektir.

## Yapılanlar

- **Proje yapısı** — Clean Architecture; Collector (Linux/Windows host), Config, Data, Job (Quartz) bileşenleri; AssetService, InitApplicationService.
- **API** — Config (GET/POST), Job (GET/POST/PUT/DELETE); JWT authentication; Gateway üzerinden `/engine/api/v1/*`.
- **MetricBatchQueue MaxBatches** — Konfigüre edilebilir kuyruk limiti; limit aşıldığında en yeniler tutuluyor.

Proje kökünde ayrı bir **ROADMAP.md** dosyası yoktur. Detaylı endpoint listesi için [Technical Specs](TECHNICAL_SPECS.md), sürüm geçmişi için [Changelog](CHANGELOG.md) dosyasına bakınız.

## Yapılacaklar

Implementasyon planına göre ([MONITORING_IMPLEMENTATION_PLAN](../../monitoring_plans/MONITORING_IMPLEMENTATION_PLAN.md) Faz 2) ve [MNGENGINE_TODO](../../monitoring_plans/MNGENGINE_TODO.md):

- **Sync API – period/schedule** — EngineConfigAgent/EngineConfigAsset modellerine defaultPeriod, defaultSchedule, period, schedule, active alanları (Reactor yanıtıyla uyum).
- **Periyot tanımlarının Engine'de uygulanması** — Asset collection periyotları, izleme aralıkları (schedule) ve agent veri gönderim periyotları; config sync ve job zamanlamasında uygulanacak.
- **Dinamik job'lar** — Asset + period bazlı Quartz job'lar; config sync sonrası job'ların güncellenmesi.
- **Ingest – şifreleme/sıkıştırma** — Batch'ler Reactor'a gönderilmeden önce şifreleme ve sıkıştırma; Reactor endpoint uyumu.
- **ICollector registry** — collectionMethod → ICollector eşlemesi (ssh, wmi, snmp, http).
- **Docker** — Dockerfile ve production compose (Faz 5).
- **UI – Asset listesi** — agentId, assetId, itemId yerine isim gösterimi; satır başına detay modal butonu.
- **UI – Queue listesi** — ID yerine isim gösterimi; satır başına okunan değerleri gösteren modal butonu.
- **Unit/Entegrasyon testleri** — MngEngine.Tests projesi (opsiyonel).
- **Observability** — OpenTelemetry (Faz 5).

## Kararlar

- Config string formatı: engineId, serverUrl, tokenUrl, username, password, sendSchedule, configSyncPeriodMinutes, mqttUrl; şifreleme ve sıkıştırma Reactor ile uyumlu.
- Veri toplama tetiklemesi: periyodik (Quartz/cron) + MQTT sync sinyali; ileride event-driven genişletilebilir.
