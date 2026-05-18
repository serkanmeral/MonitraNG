# Engine Uygulama Planı

Bu doküman, kullanıcı talepleri doğrultusunda MngEngine tarafında yapılacak geliştirmelerin özet planıdır.

---

## 1. Mevcut Durum Özeti

### Sync Tetikleme (Reactor → Engine)

| Bileşen | Durum | Açıklama |
|---------|-------|----------|
| MQTT Publish (Reactor) | ✅ Var | Agent/Engine/Asset create/update/delete'de `PublishSyncAsync` / `PublishSyncForAssetAsync` çağrılıyor |
| MQTT Subscribe (Engine) | ✅ Var | `MqttSyncTriggerService` sync mesajını alınca config sync çalıştırıyor |
| Sync sonrası job reschedule | ❌ Eksik | MQTT sync sonrası CollectorJob, SendJob vb. yeniden zamanlanmıyor |

### Period / Schedule / Active

| Bileşen | Durum | Açıklama |
|---------|-------|----------|
| Reactor EngineConfigSyncProcessing | ✅ Var | Period, Schedule, Active asset config'e ekleniyor |
| Engine ConfigSyncClient MapToResult | ❌ Eksik | period, schedule, active map edilmiyor |
| Engine EngineConfigAsset model | ❌ Eksik | Period, Schedule, Active property'leri yok |
| ConfigSyncJob ToLegacyEngineAssets | ❌ Eksik | Period, Schedule, Active legacy array'e eklenmiyor |
| AssetService | ❌ Eksik | Bu alanları kullanmıyor |
| Dinamik job'lar | ❌ Eksik | CollectorJob sabit 15 sn; asset bazlı period uygulanmıyor |

### HTTP Veri Toplama

| Bileşen | Durum | Açıklama |
|---------|-------|----------|
| AssetService BuildRequestsFromEngineAssets | ❌ Eksik | `http` / `rest` method atlanıyor ("Bilinmeyen collection method") |
| HttpCollectorHandler | ❌ Yok | HTTP/REST collector implementasyonu yok |
| connection_info (HTTP) | ✅ Var | baseUrl, auth (none/basic/bearer_token) UI'da hazır |

---

## 2. Yapılacaklar (Öncelik Sırasıyla)

### 2.1 Sync Tetikleme ve Job Reschedule

**Hedef:** Agent/Engine/Asset değiştiğinde MQTT sync geldikten sonra job'ların güncel config ile yeniden zamanlanması.

**Adımlar:**
1. `IJobRescheduleService` + `JobRescheduleService` oluştur (QuartzHostedService + IEngineConfigProvider ile)
2. `InitApplicationService.StartQuartz` içinde bu servisi kullan
3. `MqttSyncTriggerService.OnSyncRequested` sonrası `IJobRescheduleService.RescheduleJobsAsync()` çağır
4. `ConfigSyncJob.Execute` sonrası da reschedule (opsiyonel; periyodik sync zaten çalışıyor)

### 2.2 Period, Schedule, Active Alanları

**Hedef:** Reactor'dan gelen period/schedule/active bilgilerinin Engine'de kullanılabilir olması.

**Adımlar:**
1. `EngineConfigAsset` ve `EngineConfigAgent` modellerine `Period`, `Schedule`, `Active` (asset), `DefaultPeriod`, `DefaultSchedule` (agent) ekle
2. `ConfigSyncClient.MapToResult` ile JSON'dan bu alanları map et
3. `ConfigSyncJob.ToLegacyEngineAssets` ile legacy array'e ekle
4. `AssetService.BuildRequestsFromEngineAssets` ile `Active==false` asset'leri atla

### 2.3 Dinamik Collector Job'ları

**Hedef:** Her asset kendi period.expression cron'una göre toplansın; schedule window dışındaysa atlansın.

**Adımlar:**
1. Mevcut tek CollectorJob yerine asset bazlı (veya asset+period bazlı) job'lar
2. Config sync sonrası mevcut collector job'ları iptal, yeni config'e göre oluştur
3. Schedule (çalışma zamanı) kontrolü: `schedule.type` (always / time_window vb.) ve `schedule.config` ile window dışındaysa toplamayı atla

**Not:** İlk aşamada basitleştirilmiş: Tek CollectorJob kalabilir ama her çalıştığında asset'lerin period/schedule'ına göre "bu asset şimdi toplanmalı mı?" kontrolü yapılabilir. Tam dinamik job'lar Faz 2'de.

### 2.4 HTTP Veri Toplama

**Hedef:** HTTP/REST collection method'u için collector handler ve AssetService entegrasyonu.

**Adımlar:**
1. `HttpCollectorRequest`, `HttpCollectorResponse` (veya `RestCollectorRequest/Response`) oluştur
2. `HttpCollectorHandler` (veya `RestCollectorHandler`) implement et:
   - connection_info: baseUrl, auth (none/basic/bearer_token)
   - collectibles: path/endpoint bilgisi (örn. `/api/metrics`)
   - Basic auth: username/password header
   - Bearer: authConfigId üzerinden token alımı (Engine'de token cache gerekebilir; ilk sürümde authConfigId → DG'den token endpoint çağrısı)
3. `AssetService.BuildRequestsFromEngineAssets` içinde `http` ve `rest` için branch ekle
4. `CollectorJob` / MediatR handler registry'ye HTTP handler'ı ekle

---

## 4. RabbitMQ → MQTT Sync (Tamamlandı)

DataGateway'de mon_engines, mon_agents, mon_assets değiştiğinde (publish_mode != "none"):

1. **DataGateway** `monitra.monitoring.sync` exchange'ine event yayınlar (NotificationService)
2. **MngReactor** `MonitoringSyncEventConsumer` bu exchange'i dinler
3. Event alındığında domain + dataset + data'dan engineId/assetId çözülür
4. `IMqttSyncPublisher` ile MQTT sync tetiklenir → Engine config sync çalışır

**Ön koşul:** mon_engines, mon_agents, mon_assets dataset'lerinde `publish_mode = "basic"` (UI'dan ayarlanabilir)

---

## 5. Referanslar

- [MNGENGINE_TODO](MNGENGINE_TODO.md)
- [MONITORING_ENGINE_ARCHITECTURE](MONITORING_ENGINE_ARCHITECTURE.md)
- [MONITORING_IMPLEMENTATION_PLAN](MONITORING_IMPLEMENTATION_PLAN.md)
