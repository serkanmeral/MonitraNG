# Monitoring Observability (OpenTelemetry)

Bu doküman, **MngEngine** ve **MngReactor** uygulamalarının kendi gözlemlenebilirliği (observability) için OpenTelemetry kullanımını planlar. Kaynaklardan (SNMP, WMI vb.) veri toplama **değildir**; Engine ve Reactor servislerinin izlenmesidir.

Planlama özeti için [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md) dokümanına bakınız.

---

## 1. Amaç

- **Engine ve Reactor** servislerinin performansını, hata oranlarını ve davranışını izlemek
- Ingest, config sync, job çalışması gibi kritik akışların trace edilmesi
- Sorun tespiti ve performans analizi için merkezi log, metrik ve trace

---

## 2. OpenTelemetry ile Toplanacak Veriler

| Veri türü | Açıklama | Örnek |
|-----------|----------|-------|
| **Trace** | İstek akışı, servisler arası çağrı zinciri | Engine → Keeper (token) → Reactor (ingest) |
| **Metric** | Sayısal ölçümler | Ingest istek sayısı, batch boyutu, yanıt süresi |
| **Log** | Olay kayıtları | Structured log, hata mesajları, debug |

---

## 3. İzlenecek Senaryolar

### 3.1 MngReactor

| Senaryo | Trace | Metric | Log |
|---------|-------|--------|-----|
| **Ingest** | Ingest endpoint çağrısı, decrypt, MongoDB yazma | `ingest.requests_total`, `ingest.duration_ms`, `ingest.batch_size`, `ingest.saved_count` | Hata, validation fail |
| **Config Sync** | Config API çağrısı | `config_sync.requests_total`, `config_sync.duration_ms` | Engine bulunamadı, cache hit |
| **Domain Init** | RabbitMQ event → varsayılan kayıt oluşturma | `domain_init.events_total`, `domain_init.duration_ms` | Event alındı, hata |
| **Auth** | Token doğrulama | `auth.validation_total`, `auth.failures_total` | Geçersiz token |

### 3.2 MngEngine

| Senaryo | Trace | Metric | Log |
|---------|-------|--------|-----|
| **Config Sync** | Reactor API çağrısı, config işleme | `config_sync.duration_ms`, `config_sync.success` | Sync tamamlandı, hata |
| **Collector Job** | Job çalışması, collector çağrısı | `collector.job_duration_ms`, `collector.metrics_collected` | Toplama başarısız |
| **Ingest Gönderim** | HTTP POST Reactor'a | `ingest.send_duration_ms`, `ingest.batch_count` | Gönderim başarısız |
| **MQTT** | Sync/command mesajı alındı | `mqtt.messages_received` | Sync tetiklendi |

---

## 4. Export / Backend Hedefleri

| Hedef | Veri türü | Mevcut / Not |
|-------|-----------|---------------|
| **Seq** | Log | Projede zaten kullanılıyor; Serilog → Seq. OpenTelemetry log da Seq'e yönlendirilebilir. |
| **Prometheus** | Metric | Opsiyonel. Prometheus scrape veya OTLP export. |
| **Jaeger / Zipkin** | Trace | Dağıtık trace görselleştirme. OTLP ile. |

**Öneri (ilk sürüm):** Seq ile devam; OpenTelemetry metric ve trace için OTLP export eklenebilir. Prometheus/Jaeger altyapı varsa OTLP collector ile alınır.

---

## 5. .NET Entegrasyonu

| Paket | Amaç |
|-------|------|
| `OpenTelemetry` | Core SDK |
| `OpenTelemetry.Instrumentation.AspNetCore` | HTTP istekleri (trace) |
| `OpenTelemetry.Instrumentation.Http` | HttpClient çağrıları (Engine → Reactor) |
| `OpenTelemetry.Instrumentation.MongoDB` | MongoDB işlemleri |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | OTLP export (Collector, Seq, vb.) |
| `OpenTelemetry.Extensions.Hosting` | Host entegrasyonu |

**Serilog ile uyum:** OpenTelemetry log exporter veya Serilog sink (OpenTelemetry) ile mevcut Serilog akışı korunabilir.

---

## 6. Konfigürasyon

| Ayar | Açıklama | Örnek |
|------|----------|-------|
| `OTEL_SERVICE_NAME` | Servis adı | `MngReactor`, `MngEngine` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTLP collector URL | `http://otel-collector:4317` |
| `OTEL_TRACES_SAMPLING_RATIO` | Trace örnekleme (0–1) | `1` (hepsi) veya `0.1` (örn. %10) |

Ortam değişkeni veya `appsettings` ile yapılandırılır.

---

## 7. Öncelik Sırası

1. **İlk sürüm:** Ingest metrikleri (Reactor), config sync metrikleri, structured logging mevcut Serilog ile
2. **Sonra:** OpenTelemetry SDK eklenmesi; trace (HTTP, MongoDB), metric export (OTLP)
3. **İleride:** Prometheus/Grafana dashboard, Jaeger trace görselleştirme, alerting

---

## 8. Açık Kararlar

1. **OTLP Collector:** Merkezi bir OpenTelemetry Collector deploy edilecek mi, yoksa her servis doğrudan Seq/Prometheus'a mı gönderecek?
2. **Trace sampling:** Production'da tüm trace mi, yoksa örnekleme mi?
3. **Engine trace:** Engine edge'de (RPi); trace backend'e ulaşır mı? Gecikme, bant genişliği dikkate alınmalı.

---

## 9. Referanslar

- [MonitraNG Monitoring planlama](monitrang_monitoring_planlama.md)
- [Monitoring Engine Architecture](MONITORING_ENGINE_ARCHITECTURE.md)
- [Monitoring Reactor Architecture](MONITORING_REACTOR_ARCHITECTURE.md)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/instrumentation/net/)
- [OpenTelemetry OTLP](https://opentelemetry.io/docs/specs/otlp/)
