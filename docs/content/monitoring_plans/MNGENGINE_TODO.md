# MngEngine Yapılacaklar Listesi

Bu doküman, MngEngine projesi için tespit edilen iyileştirme ve uyumluluk görevlerini içerir. Referans: [MONITORING_IMPLEMENTATION_PLAN](MONITORING_IMPLEMENTATION_PLAN.md) Faz 2, [MONITORING_ENGINE_ARCHITECTURE](MONITORING_ENGINE_ARCHITECTURE.md).

---

## 1. Plan Dışı / Legacy Kod Temizliği

Plan dışında kalan veya eski mimariye ait bileşenlerin tespiti ve kaldırılması.

### Tamamlanan (Legacy temizliği)

| # | Görev | Durum |
|---|-------|-------|
| 1.1 | **InitAssets kaldırıldı** | `InitApplicationService` – config sync başarısız/engineId yok ise artık sadece log; eski `/api/v1/engine/assets` çağrısı kaldırıldı. |
| 1.2 | **DataProcessing, DataRepository kaldırıldı** | `IDataProcessing`, `IDataRepository`, `DataProcessing`, `DataRepository` silindi. |
| 1.3 | **GetDataQuery zinciri kaldırıldı** | `GetDataQueryRequest`, `GetDataQueryHandler`, `GetDataQueryResponse` silindi. |
| 1.4 | **AssetService sadeleştirildi** | Hardcoded dev asset listesi kaldırıldı. Sadece `engineAssets` cache (config sync kaynaklı) kullanılıyor. |
| 1.5 | **Boş klasörler temizlendi** | Kullanılmayan boş klasörler kaldırıldı. |

---

## 2. .NET ve NuGet Güncellemeleri

| # | Paket / Bileşen | Mevcut | Hedef | Proje | Durum |
|---|-----------------|--------|-------|-------|-------|
| 2.1 | **TargetFramework** | net9.0 | net9.0 (güncel) | Tüm csproj | ✓ |
| 2.2 | Serilog.AspNetCore | 10.0.0 | En son stabil | Application, Api | |
| 2.3 | Serilog.Sinks.Console | 6.1.1 | En son stabil | Application | |
| 2.4 | Serilog.Settings.Configuration | 10.0.0 | En son stabil | Application | |
| 2.5 | Quartz / Extensions | 3.15.1 | En son stabil | Application | |
| 2.6 | RestSharp | 113.1.0 | En son stabil | Application | |
| 2.7 | MQTTnet | 4.3.7.1207 | 4.x stabil (5.x breaking) | Infrastructure | ✓ |
| 2.8 | MediatR | 14.0.0 | En son stabil | Application | |
| 2.9 | Microsoft.AspNetCore.OpenApi | 9.0.10 | 9.0.x | Api | ✓ |
| 2.10 | Swashbuckle.AspNetCore | 6.6.2 | 6.x (OpenAPI 9 ile uyumlu) | Api | ✓ |
| 2.11 | SSH.NET | 2025.1.0 | En son stabil | Application | |
| 2.12 | System.Management | 10.0.2 | En son stabil | Application | ✓ |
| 2.13 | **global.json (opsiyonel)** | Yok | SDK sabitleme | Repo kökü | |
| 2.14 | **Directory.Build.props (opsiyonel)** | Yok | Ortak Version, LangVersion vb. | Repo kökü | |

**Not:** MQTTnet 5.x `MQTTnet.Client` namespace'ini kaldırdı; mevcut kodla uyum için 4.3.7.1207 kullanılıyor. NuGet güncellemesi için `dotnet list package --outdated` kullanılabilir.

---

## 3. Mimari Uyum – Config String ve Sync

[MONITORING_ENGINE_ARCHITECTURE](MONITORING_ENGINE_ARCHITECTURE.md) dokümanına göre config string formatı ve akışı.

### Tamamlanan

| # | Görev | Açıklama |
|---|-------|----------|
| 3.1 | **EngineConfigPayload modeli** | `engineId`, `serverUrl`, `tokenUrl`, `username`, `password`, `sendSchedule`, `configSyncPeriodMinutes`, `mqttUrl` – mimariyle uyumlu model eklendi. |
| 3.2 | **IEngineConfigProvider** | Decrypt edilmiş config'e tek noktadan erişim; tüm tüketiciler bu arayüzü kullanıyor. |
| 3.3 | **ConfigService güncellemesi** | Yeni format (serverUrl, tokenUrl, sendSchedule vb.) ve legacy format (host, http_username, collectIntervalSegment/Value) desteği. ApplyConfig async düzeltildi. |
| 3.4 | **Token ve URL çözümleme** | AccessTokenProvider, ConfigSyncClient, IngestClient – TokenUrl, ServerUrl, Username, Password kullanımı. |
| 3.5 | **MQTT ve Quartz** | MqttEngineSubscriber mqttUrl; InitApplicationService sendSchedule (SendJob) ve configSyncPeriodMinutes (ConfigSyncJob) ile job zamanlaması. |

### Yapılacak

| # | Görev | Öncelik | Açıklama |
|---|-------|---------|----------|
| 3.6 | **Sync API – period/schedule** | Orta | `EngineConfigAgent` ve `EngineConfigAsset` modellerine `defaultPeriod`, `defaultSchedule`, `period`, `schedule`, `active` alanları eklenmeli (Reactor yanıt formatıyla tam uyum). |
| 3.7 | **Dinamik job'lar** | Yüksek | Asset + period bazlı Quartz job'lar; config sync sonrası mevcut job'lar iptal, yeni config'e göre job'lar oluşturulmalı. |
| 3.8 | **Ingest – şifreleme/sıkıştırma** | Yüksek | Batch'ler sunucuya gönderilmeden önce şifrelenmeli ve sıkıştırılmalı; Reactor endpoint uyumu. |
| 3.9 | **ICollector registry** | Orta | `collectionMethod` → `ICollector` eşlemesi; ssh, wmi, snmp, http için genişletilebilir altyapı. |
| 3.10 | **Push modu** | Düşük | SNMP trap, webhook, MQTT ile event-driven toplama (kademeli). |
| 3.11 | **Periyot tanımlarının Engine’de uygulanması** | Yüksek | Asset’ler için collection periyotları, izleme aralıkları (schedule) ve agent’lar için veri gönderim periyotları tanımlanıyor; bu tanımların MngEngine config sync ve job zamanlamasında **gerçekten uygulanması** sağlanacak. |

---

## 4. MngReactor ile Uyumluluk

MngReactor'ta mevcut olup MngEngine'de eksik olan yapılar.

| # | Özellik | MngReactor | MngEngine | Aksiyon |
|---|---------|------------|-----------|---------|
| 4.1 | **API Versioning** | Asp.Versioning.Mvc 8.1.1, route: api/v1/... | Yok | API versioning eklenmeli (opsiyonel; Engine tek consumer olduğu için zorunlu değil) |
| 4.2 | **Version property** | `Version` 1.0.4 (AssemblyInfo) | `Version` 1.0.1 | Versiyonlama politikası belirlenmeli; MngReactor ile uyumlu sürüm takibi |
| 4.3 | **Docker** | Dockerfile, docker-compose.yml | Yok | MngEngine için Dockerfile ve docker-compose eklenmeli (Faz 5 / production compose kapsamında) |
| 4.4 | **Health checks** | Microsoft.Extensions.Diagnostics.HealthChecks | HealthController var, lib yok | Gerekirse resmi HealthChecks middleware entegrasyonu |
| 4.5 | **Scalar API Reference** | Scalar.AspNetCore | Swagger UI | Opsiyonel; Scalar daha modern API dokümantasyonu |
| 4.6 | **GenerateDocumentationFile** | true (XML doc) | Yok | Opsiyonel; API dokümantasyonu için |
| 4.7 | **Unit / Integration Tests** | MngReactor.Tests | Yok | Opsiyonel; test projesi eklenmeli |
| 4.8 | **Config yapısı** | MngReactorSettings, appsettings bölümleri | appsettings dağınık | Opsiyonel; MngEngineSettings benzeri merkezi config |

---

## 5. Frontend (MngEngine.UI)

| # | Görev | Açıklama |
|---|-------|----------|
| 5.1 | **Nuxt / Vue / @nuxt/ui** | package.json: nuxt ^3.13.0, @nuxt/ui ^2.15.0 – en son stabil sürümlere güncellenmeli |
| 5.2 | **TypeScript** | ^5.3.3 – en son stabil |
| 5.3 | **@nuxt/devtools** | latest – proje gereksinimlerine göre sabitlenmeli |
| 5.4 | **Asset listesi – ID yerine isim** | `agentId`, `assetId`, `itemId` sütunları şu an ID olarak görünüyor; bu alanlar için ilgili entity’lerden (Agent, Asset, Item) **name** bilgisi gösterilecek. |
| 5.5 | **Asset listesi – detay modal** | Her satıra bir buton eklenerek asset’e ait bilgilerin modal içinde gösterilmesi sağlanacak. |
| 5.6 | **Queue listesi – ID yerine isim** | Queue satırlarında ID değerleri yerine ilgili entity’lerin **name** bilgileri gösterilecek. |
| 5.7 | **Queue listesi – okunan değerler modal** | Her satıra bir buton eklenerek modal açılıp queue’daki okunan (metric) değerleri gösterilecek. |

---

## 6. Versiyonlama ve Sürüm Takibi

| # | Görev | Açıklama |
|---|-------|----------|
| 6.1 | **Semantic versioning** | Major.Minor.Patch (örn. 1.0.2) – MngReactor (1.0.4) ile uyumlu politika |
| 6.2 | **AssemblyInformationalVersion** | Program.cs'te log’da kullanılıyor; Version ile senkron tutulmalı |
| 6.3 | **Changelog** | CHANGELOG.md veya Release Notes – önemli değişikliklerin kaydı |

---

## 7. Özet Öncelik Sırası

1. **Yüksek:** Mimari uyum – dinamik job'lar (3.7), Ingest şifreleme/sıkıştırma (3.8), periyot uygulaması (3.11); legacy temizliği (1.x), NuGet güncellemeleri (2.x)
2. **Orta:** Sync API period/schedule (3.6), ICollector registry (3.9); MngReactor uyumluluk (4.1–4.3); UI – ID yerine isim, modallar (5.4–5.7)
3. **Düşük:** Push modu (3.10); Frontend güncellemeleri (5.1–5.3); Test projesi (4.7); Scalar (4.5)

---

## 8. Son Geliştirmeler (Tamamlanan / Referans)

Yakın zamanda tamamlanan geliştirmeler:

| Bileşen | Geliştirme | Açıklama |
|---------|------------|----------|
| **MngEngine** | MetricBatchQueue MaxBatches | Konfigüre edilebilir `MaxBatches` (appsettings: `MngEngine:Queue:MaxBatches`, env: `MngEngine__Queue__MaxBatches`). Limit aşıldığında en eski batch’ler atılıyor, en yeniler tutuluyor. |
| **MngReactor** | mon_metrics – doğrudan MongoDB Time Series | DG devre dışı; Reactor doğrudan MongoDB’ye Time Series collection’a yazıyor. `mng_{domain}` veritabanı, TTL: `Monitoring.MetricsTtlDays`. |
| **MngReactor** | IngestProcessing JsonNode fix | Chunk oluştururken aynı `JsonNode` iki array’e ekleniyordu (“The node already has a parent”); `JsonNode.Parse(bulkItems[i]!.ToJsonString())` ile kopya eklenerek düzeltildi. |
| **MngReactor** | Timestamp BSON DateTime | `EnsureTimestampAsBsonDateTime()` ile JSON string → BSON DateTime dönüşümü yapılıyor. |

---

## 9. Referanslar

- [MONITORING_ENGINE_ARCHITECTURE](MONITORING_ENGINE_ARCHITECTURE.md)
- [MONITORING_IMPLEMENTATION_PLAN](MONITORING_IMPLEMENTATION_PLAN.md) – Faz 2
- [monitrang_monitoring_planlama](monitrang_monitoring_planlama.md)
- MngReactor proje yapısı (Dockerfile, Extensions.cs, AppBootstrapper.cs, Api versioning)
