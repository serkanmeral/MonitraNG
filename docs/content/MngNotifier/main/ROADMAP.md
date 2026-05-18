# MngNotifier - Geliştirme Yol Haritası

**Son Güncelleme:** 29 Nisan 2026  
**Versiyon:** 1.0.0 (Temel Özellikler Tamamlandı)  
**Durum:** ✅ Temel Özellikler Tamamlandı

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Mimari Yapı](#mimari-yapı)
3. [Tamamlanacak Özellikler](#tamamlanacak-özellikler)
4. [Gelecek Planlar](#gelecek-planlar)
5. [Teknik Detaylar](#teknik-detaylar)

---

## 🎯 GENEL BAKIŞ

**MngNotifier**, tüm notification işlemlerini tek bir merkezden yöneten bir servistir. İlk aşamada mail notification'ları ile başlayacak, ileride SMS, WhatsApp, Slack gibi notification kanalları da eklenebilecektir.

**Hedefler:**
- ✅ Merkezi notification yönetimi
- ✅ Çoklu kanal desteği (Mail, SMS, WhatsApp, Slack, vb.)
- ✅ Template yönetimi
- ✅ Async notification processing
- ✅ Delivery tracking ve reporting
- ✅ Retry mekanizması

**Referans Mimari:** MngDataGateway (Clean Architecture)

---

## 🏗️ MİMARİ YAPI

### Clean Architecture Katmanları

```
MngNotifier/
├── Core/
│   ├── MngNotifier.Domain/          # Domain entities, exceptions
│   └── MngNotifier.Application/     # Interfaces, configurations, DTOs
├── Infrastructure/
│   ├── MngNotifier.Infrastructure/  # MongoDB, RabbitMQ, Mail services
│   └── MngNotifier.Persistence/     # Repositories
└── Presentation/
    └── MngNotifier.Api/             # API controllers, middleware
```

### Katman Sorumlulukları

**Domain Layer:**
- Notification entity'leri
- Template entity'leri
- Domain exceptions
- Value objects

**Application Layer:**
- Service interfaces (INotificationService, ITemplateService, vb.)
- DTOs (Request/Response)
- Configuration classes
- Events (NotificationSentEvent, NotificationFailedEvent, vb.)

**Infrastructure Layer:**
- MongoDB connection
- RabbitMQ connection
- Mail provider implementations (SMTP, SendGrid, vb.)
- External service integrations

**Persistence Layer:**
- MongoDB repositories
- Data access services
- Query builders

**Presentation Layer:**
- REST API controllers
- Middleware (exception handling, logging)
- Health check endpoints
- Version endpoints

---

## ✅ TAMAMLANACAK ÖZELLİKLER

### Phase 1: Proje Yapısı ve Temel Altyapı - ✅ TAMAMLANDI

**Amaç:** Clean Architecture yapısını kurmak ve temel altyapıyı hazırlamak

**Gereksinimler:**
- [x] Solution ve proje dosyalarını oluştur
- [x] Proje referanslarını ayarla
- [x] Temel konfigürasyon dosyalarını oluştur
- [x] NuGet paketlerini ekle (Serilog, Api Versioning, Swagger, Scalar)

**Not:** MongoDB kullanılmıyor (sadece RabbitMQ ve Disk health check)

**Tamamlanma Tarihi:** 12 Ocak 2026

---

### Phase 2: Health Check Servisi - ✅ TAMAMLANDI

**Amaç:** Uygulama sağlık durumunu kontrol eden servis implementasyonu

**Gereksinimler:**
- [x] IHealthCheckService, HealthCheckService, HealthController
- [x] RabbitMQ connection check, Disk space check
- [x] `GET /api/v1/health`, `/api/v1/health/live`, `/api/v1/health/ready`

**Tamamlanma Tarihi:** 12 Ocak 2026

---

### Phase 3: Version Servisi - ✅ TAMAMLANDI

**Amaç:** API versiyonlama ve uygulama versiyon bilgisi servisi

**Gereksinimler:**
- [x] URL / Query / Header-based versioning
- [x] `GET /api/v1/version`, `GET /api/v1/version/short`
- [x] Product, assembly version, build date, runtime, dependencies

**Tamamlanma Tarihi:** 12 Ocak 2026

---

### Phase 4: Swagger ve Scalar Desteği - ✅ TAMAMLANDI

**Amaç:** API dokümantasyonu için Swagger ve Scalar entegrasyonu

**Gereksinimler:**
- [x] Swagger + Scalar (Purple theme), OpenAPI route, Server path

**Tamamlanma Tarihi:** 12 Ocak 2026

---

### Phase 5: Mail Notification Servisi - ✅ KISMEN TAMAMLANDI

**Amaç:** Mail notification gönderme servisi

**Tamamlanan:**
- [x] IMailProvider, SmtpMailProvider, NotificationController
- [x] `POST /api/v1/notifications/mail` — Direct mail (AllowAnonymous, sync)

**Bekleyen:**
- [ ] RabbitMQ consumer (NotificationEventConsumer), queue `mngnotifier.mail.send`, MailNotificationEvent

**Tamamlanma Tarihi:** 12 Ocak 2026 (Direct API Endpoint)

---

### Phase 6: Template Yönetimi - YÜKSEK ÖNCELİK

**Amaç:** Mail template'leri ve template-based mail gönderme

**Yaklaşım:** Template'ler MngDataGateway dataset/data endpoint'leri ile yönetilir (`@mail_templates`).

**Gereksinimler:**
- [ ] Template dataset schema (MngDataGateway)
- [ ] ITemplateService (MngNotifier), MngDataGateway'den okuma, placeholder replacement
- [ ] `POST /api/v1/notifications/send-template` (JWT ile), `{{variableName}}` placeholder'ları

**Referans:** [Mail Notification Design](../support/guides/MAIL_NOTIFICATION_DESIGN.md)

---

## 🎯 GELECEK PLANLAR

### Chat Room (MVP) — Mention / push bildirimi

- **Hedef:** Sohbet mention’ında anında bildirim (MngHub ile birlikte); e-posta veya ayrı queue ile MngNotifier tetikleme — ürün: [CHAT_ROOM_ROADMAP.md](../../chat_room/CHAT_ROOM_ROADMAP.md) §6 / §8.2.
- **Docker / sıra:** [BACKEND_DOCKER_STEPS.md](../../chat_room/BACKEND_DOCKER_STEPS.md).

### Phase 7: Çoklu Kanal Desteği - DÜŞÜK ÖNCELİK

- INotificationChannel, SMS/WhatsApp/Slack provider'lar, channel selection, multi-channel support.

### Phase 8: Delivery Tracking ve Reporting - ORTA ÖNCELİK

- Notification status (pending, sent, failed, delivered), webhook, reporting, analytics.

### Phase 9: Retry ve Error Handling - YÜKSEK ÖNCELİK

- Retry policy (exponential backoff), Dead letter queue, error logging, failure notifications.

---

## 📋 TEKNİK DETAYLAR

- **Config:** ConnectionStrings.MongoDB, MngNotifierSettings (MongoDB, RabbitMQ, Mail, Serilog).
- **API:** Health, Version, Notifications (send, send-template planlanan).
- **Docker:** Dockerfile, docker-compose (5070), health check, `mng_common_mng_network`.
- **Gateway:** Ocelot `/notifier/api/v1/{everything}` → `http://mngnotifier:5070/api/v1/{everything}`.

Detaylı konfigürasyon ve endpoint örnekleri için [Technical Specs](TECHNICAL_SPECS.md) ve [Configuration](../support/guides/CONFIGURATION.md) sayfalarına bakınız.

---

**Son Güncelleme:** 29 Nisan 2026
