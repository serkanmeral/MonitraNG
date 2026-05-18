# Monitoring Implementasyon Planı

Bu doküman, MonitraNG Monitoring uygulamasının **implementasyon sırası** ve **aşamalarını** tanımlar. Her fazın çıktıları, bağımlılıkları ve görevleri listelenir.

Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanına bakınız.

---

## 1. Genel Bakış

| Faz | İçerik | Tahmini Öncelik |
|-----|--------|------------------|
| **0** | Veri katmanı (DG datasets, tenant varsayılanları) | Temel |
| **1** | MngReactor (Ingest, Config Sync, Config String, CRUD, MQTT) | Temel |
| **2** | MngEngine (Backend + Frontend, Collector'lar, veri gönderimi) | Temel |
| **3** | MngWorkflow (Queue consumer, koşul motoru, aksiyonlar) | Orta |
| **4** | MngSimulator (Host/SNMP simülasyonu, Blazor UI) | Orta |
| **5** | Tamamlama (Observability, Rate limiting, Production compose, UI) | Sonra |

**Bağımlılık özeti:**
```
[Faz 0: DG Datasets] → [Faz 1: Reactor] → [Faz 2: Engine]
                              ↓
                       [Faz 3: Workflow]
                              ↑
                       [Faz 4: Simulator] (Reactor ingest'e bağlı)
```

---

## 2. Ön Koşullar

- MngDataGateway, MngKeeper, MongoDB, RabbitMQ, MQTT Broker çalışır durumda
- Domain (tenant) oluşturulmuş; Keeper `system.mngkeeper.domain.created` event'i yayınlıyor
- Planlama dokümanları onaylanmış; implementasyona geçiş kararı alınmış

---

## 3. Faz 0: Veri Katmanı

**Amaç:** DG dataset'lerinin oluşturulması ve tenant varsayılanlarının hazırlanması.

### 3.1 Görevler

| # | Görev | Doküman | Not |
|---|-------|---------|-----|
| 0.1 | `mon_asset_type_family` dataset oluştur | [MONITORING_ASSET_DATASETS](MONITORING_ASSET_DATASETS.md) | CreateDataset API |
| 0.2 | `mon_asset_types` dataset oluştur | MONITORING_ASSET_DATASETS | relation: family |
| 0.3 | `mon_items` dataset oluştur | MONITORING_ASSET_DATASETS | parentId, location, kind, tags |
| 0.4 | `mon_assets` dataset oluştur | MONITORING_ASSET_DATASETS | relation: type, itemId; connection_info, collectible_config |
| 0.5 | `mon_collection_periods` dataset oluştur | [MONITORING_AGENT_ARCHITECTURE](MONITORING_AGENT_ARCHITECTURE.md) | cron expression |
| 0.6 | `mon_schedules` dataset oluştur | MONITORING_AGENT_ARCHITECTURE | type, config |
| 0.7 | `mon_engines` dataset oluştur | [MONITORING_ENGINE_ARCHITECTURE](MONITORING_ENGINE_ARCHITECTURE.md) | username, password, sendSchedule, configSyncPeriodMinutes, lastSeenAt |
| 0.8 | `mon_agents` dataset oluştur | MONITORING_AGENT_ARCHITECTURE | engineId, asset_configs |

### 3.2 Script

**Faz 0 script:** `scripts/tests/MngDataGateway/dataset/setup-monitoring-datasets.ps1`

- Token gerekli (`get-token.ps1` veya `load-token.ps1`; domain claim olmalı)
- **DG Gateway arkasında:** varsayılan `https://localhost:5040` + `-UseGateway` (DG artık API Gateway arkasında)
- **DG direkt (dev):** `-BaseUrl "http://localhost:5010" -UseGateway:$false`
- **Token:** `load-token.ps1` → `get-token.ps1`. Varsayılan: KeeperBaseUrl=`https://localhost:5040`, Domain=`meral`, Username=`meral_admin`, Password=`Admin123!`

### 3.3 Çıktılar

- Tüm monitoring dataset'leri DG'de tanımlı
- **Dataset Kategorisi (manuel):** "Monitoring Datasets" adında kategori oluşturulup 8 dataset bu kategori altına alındı
- Seed verisi (opsiyonel): `mon_asset_type_family`, `mon_asset_types` için örnek kayıtlar (Host, SNMP)
- Tenant oluşturulduğunda otomatik varsayılan kayıt mantığı tasarlanmış (Reactor Faz 1'de implement edilecek)

### 3.4 Tahmini süre

1–2 gün (dataset oluşturma + test)

---

## 4. Faz 1: MngReactor

**Amaç:** Reactor'ı sıfırdan plana uygun yazmak; ingest, config sync, config string, CRUD, MQTT event sync.

### 4.1 Görevler

| # | Görev | Doküman | Not |
|---|-------|---------|-----|
| 1.1 | Proje yapısı | [monitrang_monitoring_planlama](monitrang_monitoring_planlama.md) Bölüm 8 | API versioning, Health, Serilog, MediatR, MngReactorSettings |
| 1.2 | Keeper token doğrulama | [MONITORING_REACTOR_ARCHITECTURE](MONITORING_REACTOR_ARCHITECTURE.md) | JWT verify veya introspect |
| 1.3 | Ingest endpoint | MONITORING_REACTOR_ARCHITECTURE Bölüm 3 | POST /api/v1/ingest/metrics, decrypt, MongoDB Time Series |
| 1.4 | mon_metrics Time Series koleksiyonu | [MONITORING_DATA_PRODUCTION](MONITORING_DATA_PRODUCTION.md) | Schema, TTL (MONITORING_METRICS_TTL_DAYS) |
| 1.5 | RabbitMQ publish (metrik) | MONITORING_REACTOR_ARCHITECTURE | Ingest ile paralel; Workflow için |
| 1.6 | lastSeenAt güncelleme | MONITORING_REACTOR_ARCHITECTURE | Her başarılı ingest'te mon_engines |
| 1.7 | Config Sync API | MONITORING_REACTOR_ARCHITECTURE Bölüm 4 | GET /api/v1/engine/config?engineId= |
| 1.8 | Config String üretimi | MONITORING_REACTOR_ARCHITECTURE Bölüm 5 | Şifreleme, sıkıştırma; engineId, serverUrl, tokenUrl, mqttUrl vb. |
| 1.9 | Engine/Agent/Asset CRUD | DG data endpoint'leri üzerinden | Reactor API → DG |
| 1.10 | connection_info şifreleme | MONITORING_ENGINE_ARCHITECTURE | Reactor CryptProcessing; DG'ye şifreli yaz |
| 1.11 | Tenant varsayılanları | MONITORING_REACTOR_ARCHITECTURE Bölüm 9 | RabbitMQ `system.mngkeeper.domain.created` dinle; mon_schedules, mon_collection_periods "Sürekli", "1 dakika" |
| 1.12 | MQTT event sync | MONITORING_REACTOR_ARCHITECTURE Bölüm 7 | Topic: monitoring/{domain}/engine/{engineId}/sync, command |

### 4.2 Çıktılar

- Reactor API çalışır; ingest, config sync, config string endpoint'leri hazır
- Engine tanımı, agent, asset CRUD UI'a bağlanabilir (UI ayrı faz)
- RabbitMQ'ya metrik publish edilir (Workflow için)
- MQTT ile Engine'e sync sinyali gönderilir

### 4.3 Tahmini süre

3–4 hafta

---

## 5. Faz 2: MngEngine

**Amaç:** Engine Backend + Frontend; config sync, veri toplama, ingest gönderimi.

### 5.1 Görevler

| # | Görev | Doküman | Not |
|---|-------|---------|-----|
| 2.1 | Proje yapısı | monitrang_monitoring_planlama Bölüm 8 | .NET 9, minimal host, Quartz.NET |
| 2.2 | Keeper'dan token alma | MONITORING_ENGINE_ARCHITECTURE | username/password → access_token |
| 2.3 | Config string decode | MONITORING_ENGINE_ARCHITECTURE | engineId, serverUrl, tokenUrl, mqttUrl vb. |
| 2.4 | Config Sync job | MONITORING_ENGINE_ARCHITECTURE | Periyodik + MQTT subscribe ile tetikleme |
| 2.5 | Job inşası | MONITORING_ENGINE_ARCHITECTURE | agent → asset_configs → period/schedule; Quartz job'lar |
| 2.6 | ICollector abstraction | MONITORING_ENGINE_ARCHITECTURE | Poll modu; Host (Linux/Windows) collector |
| 2.7 | Veri gönderim job'u | MONITORING_ENGINE_ARCHITECTURE | In-memory batch, encrypt, compress, HTTP POST ingest |
| 2.8 | Nuxt 3 Frontend | MONITORING_ENGINE_ARCHITECTURE | Config string girişi, status, log |
| 2.9 | Engine UI → Backend API | MONITORING_ENGINE_ARCHITECTURE Bölüm 16.3 | POST /api/config, GET /api/config/status, GET /api/status |

### 5.2 Çıktılar

- Engine Backend çalışır; config sync alır, veri toplar, Reactor'a gönderir
- Engine Frontend (Nuxt) config ve durum gösterir
- RPi'de test edilebilir

### 5.3 Tahmini süre

3–4 hafta

---

## 6. Faz 3: MngWorkflow

**Amaç:** RabbitMQ'dan metrik consume etme; koşul kontrolü; aksiyon tetikleme.

### 6.1 Görevler

| # | Görev | Doküman | Not |
|---|-------|---------|-----|
| 3.1 | Proje yapısı | monitrang_monitoring_planlama Bölüm 8 | .NET 9, standalone |
| 3.2 | mon_workflows dataset | [MONITORING_WORKFLOW](MONITORING_WORKFLOW.md) | scope, collectibleCode, condition, actions |
| 3.3 | RabbitMQ queue + consumer | MONITORING_WORKFLOW | Exchange, routing key; mesaj okuma, ACK |
| 3.4 | Workflow cache | MONITORING_WORKFLOW | DG'den mon_workflows çekme; periyodik veya event refresh |
| 3.5 | Koşul motoru | MONITORING_WORKFLOW | gt, lt, between, outside; basit operatörler |
| 3.6 | Notification aksiyonu | MONITORING_WORKFLOW | MngNotifier entegrasyonu |
| 3.7 | HTTP aksiyonu | MONITORING_WORKFLOW | Webhook POST |
| 3.8 | Workflow UI (MonitraNG) | — | CRUD için dataset UI; ayrı sayfa veya modül |

### 6.2 Çıktılar

- Workflow backend çalışır; metrik geldiğinde koşul kontrol edilir, aksiyon tetiklenir
- Notification ve HTTP aksiyonları hazır
- Email, UI alert ileride

### 6.3 Tahmini süre

2–3 hafta

---

## 7. Faz 4: MngSimulator

**Amaç:** Sentetik metrik üretimi; Reactor ingest'e gönderim.

### 7.1 Görevler

| # | Görev | Doküman | Not |
|---|-------|---------|-----|
| 4.1 | Proje yapısı | [MONITORING_SIMULATOR](MONITORING_SIMULATOR.md) | .NET 9, standalone |
| 4.2 | Host simülasyonu | MONITORING_SIMULATOR | cpu_usage, memory_used, disk_usage |
| 4.3 | SNMP simülasyonu | MONITORING_SIMULATOR | sysdescr, hrProcessorLoad, ifInOctets, ifOutOctets |
| 4.4 | Keeper auth | MONITORING_SIMULATOR | Engine ile aynı; config string veya bağımsız config |
| 4.5 | Ingest gönderimi | MONITORING_SIMULATOR | Batch format, encrypt (açık karar: zorunlu mu?) |
| 4.6 | Blazor UI | MONITORING_SIMULATOR | Config, start/stop, log |

### 7.2 Çıktılar

- Simulator Host ve SNMP metrik üretir; Reactor'a gönderir
- Test ve demo için kullanılabilir

### 7.3 Tahmini süre

1–2 hafta

---

## 8. Faz 5: Tamamlama

**Amaç:** Observability, rate limiting, production compose, MonitraNG UI entegrasyonu.

### 8.1 Görevler

| # | Görev | Doküman | Not |
|---|-------|---------|-----|
| 5.1 | OpenTelemetry (Reactor, Engine) | [MONITORING_OBSERVABILITY](MONITORING_OBSERVABILITY.md) | Trace, metric export; OTLP |
| 5.2 | Rate limiting (Ingest) | MONITORING_REACTOR_ARCHITECTURE | Token–engineId doğrulama sonrası; 30–60 req/dk |
| 5.3 | MngEngine production compose | monitrang_monitoring_planlama | docker-compose.production.yml |
| 5.4 | MonitraNG UI – Asset/Item CRUD | — | Organizasyon, asset yönetimi |
| 5.5 | MonitraNG UI – Engine/Agent CRUD | — | Engine tanımı, config string butonu |
| 5.6 | MonitraNG UI – Dashboard | — | Metrik görselleştirme (widget, grafik) |
| 5.7 | Workflow Email, UI alert | MONITORING_WORKFLOW | İkinci aşama aksiyonlar |

### 8.2 Çıktılar

- Production ortamına deploy edilebilir
- UI üzerinden tam yönetim
- Observability altyapısı hazır

### 8.3 Tahmini süre

2–3 hafta (UI kapsamına göre değişir)

---

## 9. Özet Zaman Çizelgesi

| Faz | İçerik | Süre |
|-----|--------|------|
| 0 | Veri katmanı | 1–2 gün |
| 1 | MngReactor | 3–4 hafta |
| 2 | MngEngine | 3–4 hafta |
| 3 | MngWorkflow | 2–3 hafta |
| 4 | MngSimulator | 1–2 hafta |
| 5 | Tamamlama | 2–3 hafta |

**Paralel çalışma:** Faz 2 (Engine) ve Faz 3 (Workflow) kısmen paralel yürütülebilir; her ikisi de Faz 1 (Reactor) tamamlandıktan sonra başlar. Faz 4 (Simulator) da Reactor ingest hazır olduktan sonra başlayabilir.

---

## 10. Kontrol Listesi

Implementasyon başlamadan önce:

- [ ] Tüm planlama dokümanları gözden geçirildi
- [ ] Açık kararlar (Simulator asset type, Workflow exchange, vb.) implementasyon sırasında netleştirilecek olarak kabul edildi
- [ ] DG, Keeper, RabbitMQ, MQTT erişilebilir
- [ ] Test domain (tenant) hazır

Her faz tamamlandığında:

- [ ] Birim/integrasyon testleri yazıldı
- [ ] Dokümantasyon güncellendi
- [ ] Sonraki faza geçiş onaylandı

---

## 11. Referanslar

- [MngReactor Test Planı](MNGREACTOR_TEST_PLAN.md)
- [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md)
- [Monitoring Asset Datasets](MONITORING_ASSET_DATASETS.md)
- [Monitoring Agent Architecture](MONITORING_AGENT_ARCHITECTURE.md)
- [Monitoring Engine Architecture](MONITORING_ENGINE_ARCHITECTURE.md)
- [Monitoring Reactor Architecture](MONITORING_REACTOR_ARCHITECTURE.md)
- [Monitoring Data Production](MONITORING_DATA_PRODUCTION.md)
- [Monitoring Workflow](MONITORING_WORKFLOW.md)
- [Monitoring Simulator](MONITORING_SIMULATOR.md)
- [Monitoring Observability](MONITORING_OBSERVABILITY.md)
