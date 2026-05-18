# Monitoring Planları

Bu klasör, MonitraNG Monitoring uygulamasına ait **tüm planlama ve tasarım dokümanlarını** içerir.

## Monitoring Roadmap Özeti

| Faz | İçerik | Durum | Referans |
|-----|--------|--------|-----------|
| **0** | Veri katmanı (DG datasets: mon_asset_*, mon_items, mon_agents, mon_engines, mon_schedules, mon_collection_periods) | Script: `scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1` | [MONITORING_IMPLEMENTATION_PLAN](MONITORING_IMPLEMENTATION_PLAN.md) §3 |
| **1** | MngReactor (Ingest, Config Sync, Config String, CRUD, RabbitMQ publish, Domain init, MQTT sync) | Temel API ve testler mevcut | [MngReactor Roadmap](../MngReactor/main/ROADMAP.md) |
| **2** | MngEngine (Backend + Frontend, config sync, collector'lar, ingest gönderimi) | Temel yapı; dinamik job, şifreleme/sıkıştırma devam ediyor | [MngEngine Roadmap](../MngEngine/main/ROADMAP.md), [MNGENGINE_TODO](MNGENGINE_TODO.md) |
| **3** | MngWorkflow (RabbitMQ consumer, koşul motoru, aksiyonlar) | Planlı | [MONITORING_WORKFLOW](MONITORING_WORKFLOW.md) |
| **4** | MngSimulator (Host/SNMP simülasyonu, Reactor ingest'e gönderim) | MngSim: HTTP/SNMP/MQTT sanal cihaz sunucusu mevcut; Reactor ingest entegrasyonu planlı | [MONITORING_SIMULATOR](MONITORING_SIMULATOR.md), `MngSim/README.md` |
| **5** | Tamamlama (Observability, rate limiting, production compose, MonitraNG UI) | Sonraki aşama | [MONITORING_IMPLEMENTATION_PLAN](MONITORING_IMPLEMENTATION_PLAN.md) §8 |

Ana planlama: [monitrang_monitoring_planlama](monitrang_monitoring_planlama.md). Detaylı görev listesi: [MONITORING_IMPLEMENTATION_PLAN](MONITORING_IMPLEMENTATION_PLAN.md).

## Konum ve Kapsam

- **Kök:** `docs/content/monitoring_plans/`
- **Kapsam:** Monitoring uygulaması ile ilgili planlar, şemalar, mimari kararlar ve ilgili dokümanlar
- **Güncelleme:** Bu klasördeki dokümanlar güncel tutulmalıdır

## Mevcut Dokümanlar

| Dosya | Açıklama |
|-------|----------|
| [monitrang_monitoring_planlama.md](monitrang_monitoring_planlama.md) | Mimari planlama: Asset, Organizasyon, Agent, Veri Üretme, Engine–Reactor |
| [MONITORING_AGENT_ARCHITECTURE.md](MONITORING_AGENT_ARCHITECTURE.md) | Agent tanımı, mon_collection_periods, mon_schedules, mon_agents, DG şemaları, varsayılan değerler |
| [MONITORING_DATA_PRODUCTION.md](MONITORING_DATA_PRODUCTION.md) | Veri üretme, MongoDB Time Series (mon_metrics), batch format, TTL, örnek dokümanlar |
| [MONITORING_ENGINE_ARCHITECTURE.md](MONITORING_ENGINE_ARCHITECTURE.md) | Engine mimarisi: Backend/Frontend, config sync, job inşası, mon_engines, sync API |
| [MONITORING_REACTOR_ARCHITECTURE.md](MONITORING_REACTOR_ARCHITECTURE.md) | Reactor mimarisi: ingest, config sync API, config string, auth, event sync |
| [MONITORING_ASSET_DATASETS.md](MONITORING_ASSET_DATASETS.md) | MngDataGateway dataset şemaları ve örnekler (mon_asset_type_family, mon_asset_types, mon_items, mon_assets) |
| [MONITORING_OBSERVABILITY.md](MONITORING_OBSERVABILITY.md) | Observability: OpenTelemetry ile Engine ve Reactor izleme (trace, metric, log) |
| [MONITORING_SIMULATOR.md](MONITORING_SIMULATOR.md) | Simulator: Sentetik veri üretimi (Host, SNMP), ingest entegrasyonu |
| [MONITORING_WORKFLOW.md](MONITORING_WORKFLOW.md) | Workflow: Koşul–Aksiyon, RabbitMQ queue, Reactor publish |
| [MONITORING_IMPLEMENTATION_PLAN.md](MONITORING_IMPLEMENTATION_PLAN.md) | Implementasyon planı: Fazlar, görevler, bağımlılıklar |

## Navigasyon

MkDocs üzerinden: **DevOps → Monitoring Planları**

## Yeni Doküman Ekleme

Monitoring ile ilgili yeni dokümanlar **bu klasöre** eklenmelidir. Örnek alanlar:

- Agent Mimarisi (detaylı)
- Veri Üretme Mimarisi (detaylı)
- Engine–Reactor Veri Akışı
- Simulator Planı
- Metrik/Measurement Şeması
