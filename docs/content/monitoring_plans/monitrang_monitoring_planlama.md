# MonitraNG Monitoring – Mimari Planlama

Bu plan, **infrastructure** tamamlandıktan sonra MonitraNG'in **Monitoring Tool** olarak geliştirilmesi için mimari çerçeveyi tanımlar. Asset, organizasyon, agent, veri üretme ve veri işleme mimarileri; MngEngine / MngReactor roller ve veri akışı; sonrasında Simulator planlaması için zemin oluşturulacaktır.

---

## 1. Mevcut Durum Özeti

- **Altyapı:** MngKeeper (IAM, domain), MngDataGateway (generic data), MngHub (real-time), RabbitMQ, MongoDB, Keycloak. Domain = tenant; `mng_data_{domain}`, `mng_common`.
- **MngEngine:** Clean Architecture, Quartz job'lar, Linux/Windows collector'lar, config + Reactor'dan asset listesi. Toplanan veri şu an **sadece loglanıyor**, Reactor'a gönderilmiyor.
- **MngReactor:** Asset tree, engine/assets API, CRUD (engines, data), MongoDB + MQTT. Engine'e asset listesi sağlıyor; **toplanan veriyi alan endpoint yok**.
- **Asset:** `mng_common`: `asset_type_families`, `asset_types`; `mng_data_{domain}`: `assets`, `engines`. Tree: asset → asset_type → family. Engine tarafında `AssetInfo` (ConnectionInfo, Collectibles).
- **Agent:** Kod tabanında özel bir "agent" kavramı yok; kullanıcı tanımı planda ayrıca netleştirilecek.

---

## 2. Asset Mimarisi

**Amaç:** İzlenen varlıkların (asset) hiyerarşisi, tipleri, toplanacak veri tanımları (collectibles) ve bağlantı bilgilerinin tutarlı modeli.

### 2.1 Dataset'ler ve depolama

- **İsimlendirme:** `mon_` prefix'i monitoring dataset'leri için kullanılır. Dataset adları: `mon_asset_type_family`, `mon_asset_types`, `mon_items`, `mon_assets`.
- **Konum:** Tüm monitoring dataset'leri ilgili tenant veritabanında: **`mng_{domain_name}`**. Family, type, item ve asset verileri domain bazında izole edilir.
- **Veri erişimi:** Tüm veri **MngDataGateway (DG)** üzerinden; doğrudan MongoDB erişimi yok. Reactor, asset CRUD için DG data endpoint'lerini kullanır.
- **Referanslar:** Tüm referanslar **__dataId (GUID)**. DG **Relation** alanları kullanılır: `fieldType: "relation"`, `relationDataset` = hedef dataset adı, değer = ilgili dokümanın `__dataId`'si (string). [DatasetSchema](MngDataGateway/Core/MngDataGateway.Domain/Entities/DatasetSchema.cs) `FieldDefinition.relationDataset`.

### 2.2 Hiyerarşi ve tip modeli

- **Asset Type Family** (örn. Operating Systems) → **Asset Type** (örn. Linux); type, family'ye referans verir.
- **Asset** → izlenen somut varlık; **type** ile `mon_asset_types`'a, **itemId** ile `mon_items`'a referans. Her Asset bir Item içinde olmalı.
- **Engine–Asset:** Asset tarafında **engineId** yok. Engine'in asset'leri olacak; bağlantı Engine bölümünde tanımlanacak.

### 2.3 asset_info (metadata)

| Alan | Açıklama |
|------|----------|
| name | Asset adı |
| type | `mon_asset_types` referansı (__dataId) |
| itemId | `mon_items` referansı (__dataId). Zorunlu; Asset hangi Item içinde. |
| description | Açıklama |
| tags | Key-value yapısı (örn. `[{ "key": "...", "value": "..." }]`) |
| status | `active` \| `maintenance` \| `decommissioned` |

Lokasyon Asset'te yok; Item'ın effective location kullanılır (Organizasyon Mimarisi).

### 2.4 connection_info (generic)

- **Yapı:** Asset'te **protocol yok**. Sadece `endpoint` (host, port?) + `auth` (type'ın `collection_method`'una göre: username/password, community, apiKey, vb.). **Bağlantı yöntemi** type'ın `collection_method`'undan gelir (WMI, SSH, SNMP, REST, …); asset yalnızca "nereye, hangi kimlik bilgileriyle" bilgisini verir.
- **Saklama:** Reactor, şifre vb. hassas alanları **kendisi şifreler** (CryptProcessing); şifrelenmiş hali DG'ye yazılır.
- **Karar (madde 7 – C):** Protocol yalnızca type'ta; asset'te yok. Uyumsuzluk olmaz; Engine, type'ın method'unu kullanır, asset'in connection_info'sundan endpoint + auth alır.

### 2.5 Type collectibles ve override

- **mon_asset_types:** `collection_method` + `collectibles`. Her collectible: `code`, `name`, `data_type`, kaynak (örn. `metric_key` / `oid` / `path`). Type seviyesinde **overridable_params** (örn. `["oid","interval"]`) tanımlanır; hangi alanlar asset'te override edilebilir.
- **mon_assets – collectible_config:** `[{ "code", "enabled", "params"? }]`. **Öneri B:** Varsayılan hepsi açık; sadece kapatma veya parametre değiştirme yapılacaksa ilgili collectible config'e eklenir. `params`, type'taki `overridable_params` ile sınırlı.

### 2.6 Asset CRUD

- **Endpoint'ler:** Reactor içinde. Reactor, asset verisini işler (credential şifreleme vb.), ardından **DG data endpoint'leri** ile yazar/günceller/siler.
- **connection_info validasyonu:** Reactor, `type.collection_method`'a göre (WMI, SSH, SNMP vb.) method-specific şema doğrulaması yapmalı. DG object alanı serbest kabul eder; validasyon uygulama katmanında.

### 2.7 Referanslar

- [MngReactor ARCHITECTURE_GUIDE](../MngReactor/support/architecture/ARCHITECTURE_GUIDE.md), [Engine ARCHITECTURE_GUIDE](../MngEngine/support/architecture/ARCHITECTURE_GUIDE.md), [DatasetSchema](MngDataGateway/Core/MngDataGateway.Domain/Entities/DatasetSchema.cs).
- **DG şemaları ve örnekler:** [Monitoring Asset Datasets](MONITORING_ASSET_DATASETS.md) — `mon_asset_type_family`, `mon_asset_types`, `mon_items`, `mon_assets` dataset şemaları ve örnek veri dokümanları.

---

## 3. Organizasyon Mimarisi

**Amaç:** İç içe organizasyon ağacı; Item hiyerarşisi ve Asset'lerin Item'lara bağlanması.

**Karar – Tek tip Item modeli (`mon_items`):**

- **Hiyerarşi:** İç içe tree. Her Item alt Item'lar ve/veya Asset'ler içerebilir. `parentId` → mon_items | null (kök için).
- **Alanlar:** `name`, `description`, `location` (opsiyonel `{ lat, lon }`), `kind`, `tags` (key-value).
- **Asset bağlantısı:** Her Asset **bir Item içinde** (`itemId` zorunlu). Asset'te `location` yok; Item'ın effective location kullanılır.
- **Effective location:** Item'da `location` tanımlı değilse, en yakın lokasyon tanımlı parent'tan cascade. Okuma sırasında hesaplanır.
- **Örnek:** Istanbul → Çamlıca Bölge → 1. Sistem odası → 2 nolu kabin → sunucu1 (Item). Sunucu1 Item'ı altında OS, DB vb. Asset'ler.

**Referans:** [MngKeeper ARCHITECTURE](../MngKeeper/support/architecture/ARCHITECTURE_GUIDE.md), [INFRASTRUCTURE_OVERVIEW](../infrastructure/INFRASTRUCTURE_OVERVIEW.md).

---

## 4. Agent Mimarisi

**Amaç:** Agent tanımı (sunucu tarafı kayıt) ve Engine ilişkisi.

**Karar:**

- **Agent:** Sunucu tarafında (Reactor / DG) oluşturulan bir **kayıt**. Veri toplama tanımını tutar.
- **Engine:** Agent tanımına göre asset'lerden veri okuyan ve Reactor'a gönderen **servis**. Detayları ileride netleştirilecek.

**Agent alanları:**

- **name:** Agent adı (domain içinde unique)
- **defaultPeriodId, defaultScheduleId:** Opsiyonel varsayılanlar (asset_config'ta yoksa kullanılır)
- **tags:** Opsiyonel; raporlama/filtreleme
- **asset_configs:** Asset bazlı yapılandırma — her asset için: `assetId`, `periodId?`, `scheduleId?`, `active`, `description?`
- **status:** `active` \| `inactive` \| `maintenance` — Engine davranışını belirler

**Validasyonlar:** Agent name unique; asset_configs içinde assetId unique.

**Yan tanımlar (ayrı dataset'ler):**

- **mon_collection_periods:** Toplama periyotları (cron ifadeleri) — kullanıcı tanımlar, asset'te seçer
- **mon_schedules:** İzleme aralıkları (sürekli, hafta içi 08–19, vb.) — kullanıcı tanımlar, asset'te seçer

**Referans:** [Monitoring Agent Architecture](MONITORING_AGENT_ARCHITECTURE.md) — Detaylı agent yapısı, DG şemaları ve varsayılan değerler.

---

## 5. Veri Üretme Mimarisi

**Amaç:** Monitoring verisinin **nerede**, **nasıl** ve **hangi formatta** üretildiğini tanımlamak.

**Kaynaklar:**

1. **MngEngine collector'lar (mevcut):** Linux/Windows host'lardan WMI, shell vb. ile metrik toplama. Asset'e ait `ConnectionInfo` ve `Collectibles` kullanılır.
2. **İleride:** Simulator (sentetik veri); ayrı planlama.

**Tetikleme:** Periyodik (Quartz/cron) ve ileride event-driven.

**Veri formatı (Engine → Reactor):** Batch payload. Her metrik `meta` (domain, assetId, itemId, agentId, engineId, collectibleCode) + `value` + opsiyonel `unit`.

**Referans:** [Monitoring Data Production](MONITORING_DATA_PRODUCTION.md) — Batch format, MongoDB Time Series şeması, örnek dokümanlar.

---

## 6. Üretilen Verinin İşlenmesi (Engine → Reactor)

**Amaç:** Engine'de üretilen ham/metrik verinin Reactor'a iletimi, kalıcılığı ve gerekirse işlenmesi.

**Akış (hedeflenen):**

```
[Asset] --> [MngEngine Collectors] --> (kanal) --> [MngReactor] --> [MngDataGateway / MQTT]
                                                         |
                                                         v
                                            [Opsiyonel: aggregation, alerting, …]
```

**6.1 Engine tarafı**

- Collector'lar topladığı veriyi **standart bir formata** (örn. DTO / JSON) dönüştürür.
- **Gönderim sorumluluğu:** Toplama tamamlandıktan sonra (job içinde veya ayrı servis) veriyi Reactor'a **iletecek** bileşen eklenecek. Mevcut [CollectorJob](MngEngine/MngEngine.Service/Infrastructure/MngEngine.Persistence/Jobs/CollectorJob.cs) sadece collector'ları çalıştırıp sonucu logluyor; buraya "Reactor'a gönder" adımı eklenecek.

**6.2 İletişim kanalı**

- **Seçenek A – REST:** Engine, Reactor'a `POST /api/v1/data/...` (veya yeni bir "ingest" endpoint'i) ile batch/tekil ölçüm gönderir. Basit, mevcut Reactor API'leri ile uyumlu.
- **Seçenek B – Kuyruk (RabbitMQ):** Engine ölçümü kuyruğa yazar; Reactor (veya ayrı worker) tüketir. Ölçeklenebilir, kopuk çalışmaya uygun. Altyapıda RabbitMQ zaten var.

**Öneri:** Başlangıç için REST ile **tek tip ingest endpoint**; yük ve gecikme artarsa kuyruk tabanlı mimariye geçiş değerlendirilsin.

**6.3 Reactor tarafı**

- **Alım:** "Engine ingest" endpoint'i. Domain / engine / asset bilgisi zorunlu. Engine, Keeper'dan access_token alır; Bearer token ile Reactor'a gider.
- **Kalıcılık:** Gelen batch **MongoDB Time Series** koleksiyonuna (`mon_metrics`) yazılır. Reactor, DG yerine **doğrudan MongoDB** kullanır (Time Series optimizasyonu için). Her metrik ayrı dokümana dönüştürülür.
- **TTL:** `MONITORING_METRICS_TTL_DAYS` env değişkeni ile retention süresi yapılandırılır.
- **İşleme (opsiyonel):** İlk fazda sadece yazma; aggregation, alerting ileride eklenebilir.

**6.4 Veri şeması**

- **meta:** domain, assetId, itemId, agentId, engineId, collectibleCode — dashboard/query builder için boyutlar.
- **value:** number | string | object — metrik değeri.
- **unit:** Opsiyonel; sayısal metriklerde.

**Referans:** [Monitoring Data Production](MONITORING_DATA_PRODUCTION.md).

---

## 7. MngEngine ve MngReactor – Rol Özeti

**MngEngine:**

- **Sorumluluk:** Sunucudan kendisine atanmış **agent** tanımlarını alır; asset'lerden veri okur; Reactor'a gönderir. Logic sunucuda; Engine yalnızca toplama ve iletim yapar.
- **Yapı:** Backend (config sync, job'lar, ingest) + Frontend (config, durum). Raspberry Pi'de çalışacak şekilde hafif.
- **Config:** Sunucu URL, kullanıcı adı, şifre. engineId config'te veya ilk bağlantıda alınır.
- **Referans:** [Monitoring Engine Architecture](MONITORING_ENGINE_ARCHITECTURE.md) — Detaylı mimari, sync API, job inşası.

**MngReactor:**

- **Sorumluluk:** Engine'den gelen veriyi **almak**; asset ve engine yönetimi (CRUD); config sync ve ingest endpoint'leri; gerekiyorsa **veri işleme** (aggregation, alerting vb.).
- **Auth:** Engine, Keeper'dan access_token alır; Reactor endpoint'lerine Bearer token ile gider. Reactor, token'ı Keeper ile doğrular (JWT verify veya introspect).
- **Referans:** [Monitoring Reactor Architecture](MONITORING_REACTOR_ARCHITECTURE.md) — Ingest, config sync, config string, event sync detayları.

**Dağıtım:** MngReactor production compose'ta yer alıyor ([docker-compose.production.yml](ApplicationResources/mng_apps/docker-compose.production.yml)). MngEngine henüz production compose'ta yok; izolasyon ve ölçeklenme ihtiyacına göre eklenmeli.

---

## 8. Uygulama Standartları ve Genel Kararlar

**Karar:** MngReactor ve MngEngine **sıfırdan** plana uygun geliştirilecek. Mevcut deneysel kod referans için tutulabilir; model ve mimari farklı olduğundan yeniden yazım tercih edilir.

**Standartlar:** Yeniden yazımda MngDataGateway, MngKeeper, MngAdmin ile uyum sağlanacak:

| Standart | Açıklama |
|----------|----------|
| **API versioning** | Asp.Versioning (QueryString, Header, UrlSegment) |
| **Health check** | HealthController, IHealthCheckService |
| **Serilog** | InitSerilog(settings), yapılandırılmış log |
| **Settings** | MngXxxSettings, strongly typed configuration |
| **Error handling** | GlobalExceptionHandlerMiddleware |
| **MediatR** | Features altında Command/Query handler yapısı |

---

## 9. Simulator

Sentetik veri üretimi için plan: [Monitoring Simulator](MONITORING_SIMULATOR.md).

**Özet:**

- Simulator, Host (Linux/Windows) ve SNMP kaynaklarını simüle eder; aynı ingest API'sine batch gönderir.
- Veri formatı metric şeması ile uyumlu; Engine ile aynı auth ve batch yapısı.
- Başlangıç metrikleri: `cpu_usage`, `memory_used`, `disk_usage` (Host); `sysdescr`, `hrProcessorLoad` (SNMP).

---

## 10. Öneriler ve Sonraki Adımlar

- **Agent tanımı:** [Bölüm 4](#4-agent-mimarisi) içeriği, senin tarafından netleştirildikten sonra güncellenmeli ve gerekirse ayrı bir "Agent Mimarisi" dokümanına taşınabilir.
- **Observability:** Engine ve Reactor için OpenTelemetry ile trace, metric ve log. Detay: [Monitoring Observability](MONITORING_OBSERVABILITY.md).
- **Güvenlik:** Engine–Reactor iletişiminde JWT veya API key; credential'ların güvenli saklanması (config, vault).
- **Şema ve versiyonlama:** Ölçüm modeli için net alan adları ve opsiyonel versiyon alanı; ileride şema evrimi kolaylaşır.
- **Öncelik sırası:** (1) Engine → Reactor veri gönderimi + Reactor ingest, (2) Ortak metric şeması ve kalıcılık, (3) Event-driven ve agent mimarisi, (4) Simulator planı, (5) Workflow planı.
- **Implementasyon planı:** Fazlar, görevler ve bağımlılıklar: [Monitoring Implementasyon Planı](MONITORING_IMPLEMENTATION_PLAN.md).
- **Workflow:** Koşul–Aksiyon sistemi; Reactor metrik yazarken RabbitMQ'ya publish eder; MngWorkflow queue'dan consume edip koşul kontrolü ve aksiyon çalıştırır. Detay: [Monitoring Workflow](MONITORING_WORKFLOW.md).

Bu plan onaylandıktan sonra, **Agent Mimarisi** ve **Simulator** için senin ek açıklamalarınla detaylı alt planlar çıkarılabilir.
