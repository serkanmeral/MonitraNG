# MngEngine Changelog

Tüm önemli değişiklikler bu dosyada dokümante edilir.

Format [Keep a Changelog](https://keepachangelog.com/en/1.0.0/) standardına uygundur.
Versiyonlama [Semantic Versioning](https://semver.org/spec/v2.0.0.html) kullanır.

## [Unreleased]

### Added
- **MetricBatchQueue MaxBatches** — Konfigüre edilebilir kuyruk limiti (appsettings: `MngEngine:Queue:MaxBatches`, env: `MngEngine__Queue__MaxBatches`). Limit aşıldığında en eski batch'ler atılıyor, en yeniler tutuluyor.
- **MngReactor mon_metrics** — Reactor doğrudan MongoDB Time Series collection'a yazıyor; DG devre dışı. `mng_{domain}` veritabanı, TTL: `Monitoring.MetricsTtlDays`.
- **MngReactor IngestProcessing JsonNode fix** — Chunk oluştururken "The node already has a parent" hatası; `JsonNode.Parse(bulkItems[i]!.ToJsonString())` ile kopya eklenerek düzeltildi.
- **MngReactor Timestamp BSON DateTime** — `EnsureTimestampAsBsonDateTime()` ile JSON string → BSON DateTime dönüşümü.

### Changed
- (henüz release edilmemiş değişiklikler)

### Fixed
- (henüz release edilmemiş düzeltmeler)
