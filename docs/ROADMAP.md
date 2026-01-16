# MngNotifier - Geliştirme Yol Haritası

**Son Güncelleme:** 12 Ocak 2026  
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
  - [x] `MngNotifier.sln`
  - [x] `Core/MngNotifier.Domain/MngNotifier.Domain.csproj`
  - [x] `Core/MngNotifier.Application/MngNotifier.Application.csproj`
  - [x] `Infrastructure/MngNotifier.Infrastructure/MngNotifier.Infrastructure.csproj`
  - [x] `Infrastructure/MngNotifier.Persistence/MngNotifier.Persistence.csproj`
  - [x] `Presentation/MngNotifier.Api/MngNotifier.Api.csproj`
- [x] Proje referanslarını ayarla
  - [x] Application → Domain
  - [x] Infrastructure → Application
  - [x] Persistence → Application, Domain
  - [x] Api → Application, Infrastructure, Persistence
- [x] Temel konfigürasyon dosyalarını oluştur
  - [x] `appsettings.json`
  - [x] `appsettings.Development.json`
  - [x] `Program.cs` (temel yapı)
- [x] NuGet paketlerini ekle
  - [x] Serilog (Console + Seq)
  - [x] Asp.Versioning.Mvc
  - [x] Asp.Versioning.Mvc.ApiExplorer
  - [x] Swashbuckle.AspNetCore (Swagger)
  - [x] Scalar.AspNetCore (Scalar API Reference)
  - [ ] FluentValidation (gelecekte)
  - [ ] MediatR (opsiyonel - gelecekte)

**Not:** MongoDB kullanılmıyor (sadece RabbitMQ ve Disk health check)

**Tamamlanma Tarihi:** 12 Ocak 2026

---

### Phase 2: Health Check Servisi - ✅ TAMAMLANDI

**Amaç:** Uygulama sağlık durumunu kontrol eden servis implementasyonu

**Gereksinimler:**
- [x] `IHealthCheckService` interface (Application layer)
- [x] `HealthCheckService` implementation (Persistence layer)
- [x] `HealthController` (Api layer)
- [x] Health check bileşenleri:
  - [x] RabbitMQ connection check (degraded status - implementasyon bekleniyor)
  - [x] Disk space check
  - [ ] Mail provider connection check (opsiyonel - gelecekte)
- [x] Health check endpoint'leri:
  - [x] `GET /api/v1/health` - Comprehensive health check
  - [x] `GET /api/v1/health/live` - Liveness probe
  - [x] `GET /api/v1/health/ready` - Readiness probe

**Not:** MongoDB kullanılmıyor, sadece RabbitMQ ve Disk check mevcut.

**Tamamlanma Tarihi:** 12 Ocak 2026

**Referans:** MngDataGateway Health Check implementasyonu

**Health Check Response Yapısı:**
```json
{
  "status": "healthy|degraded|unhealthy",
  "timestamp": "2026-01-11T10:00:00Z",
  "checks": {
    "mongodb": {
      "status": "healthy",
      "responseTimeMs": 5,
      "message": null
    },
    "rabbitmq": {
      "status": "healthy",
      "responseTimeMs": 3,
      "message": null
    },
    "disk": {
      "status": "healthy",
      "responseTimeMs": null,
      "message": "Available: 50GB"
    }
  }
}
```

**Öncelik:** Yüksek (Monitoring için kritik)

**Tahmini Süre:** 2-3 saat

---

### Phase 3: Version Servisi - ✅ TAMAMLANDI

**Amaç:** API versiyonlama ve uygulama versiyon bilgisi servisi

**Gereksinimler:**
- [x] API Versioning yapılandırması (Program.cs)
  - [x] URL-based versioning (`/api/v1/...`)
  - [x] Query string-based versioning (`?version=1.0`)
  - [x] Header-based versioning (`Api-Version: 1.0`)
  - [x] Default version: v1.0
- [x] `VersionController` (Api layer)
  - [x] `GET /api/v1/version` - Detailed version information
  - [x] `GET /api/v1/version/short` - Simple version string
- [x] Version bilgileri:
  - [x] Product name
  - [x] Assembly version
  - [x] Informational version
  - [x] Build date
  - [x] Company/Copyright
  - [x] Environment
  - [x] Runtime information (.NET version, OS, vb.)
  - [x] Dependencies (RabbitMQ, SMTP Mail provider)

**Tamamlanma Tarihi:** 12 Ocak 2026

**Referans:** MngDataGateway Version Controller implementasyonu

**Version Response Yapısı:**
```json
{
  "product": "MngNotifier API",
  "version": "1.0.0",
  "assemblyVersion": "1.0.0.0",
  "buildDate": "2026-01-11T10:00:00Z",
  "company": "MonitraNG",
  "copyright": "Copyright © 2026",
  "environment": "Development",
  "runtime": {
    "framework": "9.0.0",
    "os": "Windows 10.0.26100",
    "machineName": "DEV-MACHINE",
    "processorCount": 8
  },
  "dependencies": {
    "mongodb": "7.0",
    "rabbitmq": "3-management",
    "mailProvider": "SMTP"
  }
}
```

**Öncelik:** Yüksek (API versioning ve monitoring için kritik)

**Tahmini Süre:** 2-3 saat

---

### Phase 4: Swagger ve Scalar Desteği - ✅ TAMAMLANDI

**Amaç:** API dokümantasyonu için Swagger ve Scalar entegrasyonu

**Gereksinimler:**
- [x] Swagger yapılandırması (Program.cs)
  - [x] `AddSwaggerGen` configuration
  - [x] API versioning desteği (SwaggerConfigureOptions)
  - [x] Custom schema ID strategy
- [x] Scalar API Reference yapılandırması
  - [x] `MapScalarApiReference` configuration
  - [x] Theme: Purple (diğer servislerle uyumlu)
  - [x] OpenAPI route pattern: `/api-docs/{documentName}/swagger.json`
  - [x] Server path configuration
- [x] Swagger UI endpoint'leri
  - [x] `/swagger` - Swagger UI
  - [x] `/api-docs/{version}/swagger.json` - OpenAPI JSON
  - [x] Scalar UI (otomatik map edilir)

**Tamamlanma Tarihi:** 12 Ocak 2026

**Referans:** MngDataGateway ve MngLLM Swagger/Scalar implementasyonu

---

### Phase 5: Mail Notification Servisi - ✅ KISMEN TAMAMLANDI

**Amaç:** Mail notification gönderme servisi (iki farklı yöntemle)

**Gereksinimler:**
- [x] Mail provider interface (`IMailProvider`)
- [x] SMTP implementation (`SmtpMailProvider`)
- [x] Notification controller (`NotificationController`)

**Mail Notification Yöntemleri:**

**1. Direct API Endpoint (Unauthenticated):** ✅ TAMAMLANDI
- [x] `POST /api/v1/notifications/mail` - Direct mail gönderme
  - **Authentication:** ❌ No authentication required (AllowAnonymous)
  - Request body: `to`, `cc` (optional), `from` (optional), `subject`, `body`, `isHtml` (optional)
  - `from`: `{ "email": "string", "name": "string (optional)" }` - Default: appsettings'ten alınır
  - Response: Notification ID ve status
  - **Kullanım:** Domain oluşturulduğunda, bootstrap senaryoları, external sistemler
  - **Not:** Şu anda sync gönderim yapılıyor, RabbitMQ queue'ya ekleme gelecekte eklenecek

**2. RabbitMQ Event Consumer:** ❌ BEKLİYOR
- [ ] RabbitMQ consumer service (`NotificationEventConsumer`)
- [ ] Event model: `MailNotificationEvent`
- [ ] Queue: `mngnotifier.mail.send` (veya configurable)
- [ ] Event structure: `to`, `cc`, `subject`, `body`
- [ ] Auto-acknowledgment veya manual acknowledgment (retry için)

**Tamamlanma Tarihi:** 12 Ocak 2026 (Direct API Endpoint)

**Referans:** Detaylı tasarım için `docs/MngNotifier/MAIL_NOTIFICATION_DESIGN.md`

---

### Phase 6: Template Yönetimi - YÜKSEK ÖNCELİK

**Amaç:** Mail template'lerini yönetmek ve template-based mail gönderme

**Yaklaşım:** Template'ler MngDataGateway'in dataset ve data endpoint'leri kullanılarak yönetilir.

**Gereksinimler:**
- [ ] Template dataset schema oluştur (MngDataGateway'de)
  - [ ] Dataset name: `@mail_templates`
  - [ ] Fields: `templateId`, `name`, `subject`, `body`, `variables`, `isActive`
  - [ ] Indexes: `templateId` (unique), `isActive`
- [ ] Template service (`ITemplateService`) - MngNotifier'da
  - [ ] MngDataGateway API'den template okuma
  - [ ] Template cache (opsiyonel - performans için)
- [ ] Placeholder replacement engine
  - [ ] `{{variableName}}` formatında placeholder'lar
  - [ ] `messageObject` içindeki değerlerle replace
  - [ ] Missing variable validation

**Template Dataset Schema (MngDataGateway'de):**
- Dataset name: `@mail_templates`
- Fields: `templateId` (unique), `name`, `subject`, `body`, `variables` (array), `isActive`
- Template CRUD: MngDataGateway API üzerinden (`/api/v1/data/@mail_templates`)

**Template-Based Mail Gönderme (MngNotifier'da):**
- [ ] `POST /api/v1/notifications/send-template` - Template ile mail gönderme
  - **Authentication:** ✅ Authentication required (JWT token)
  - Request body: `to`, `cc` (optional), `from` (optional), `templateId`, `messageObject`
  - `from`: `{ "email": "string", "name": "string (optional)" }` - Default: appsettings'ten alınır
  - İş akışı:
    1. Authentication kontrolü (JWT token validation)
    2. MngDataGateway API'den template'i getir (`templateId` ile, token ile)
    3. Template validation (isActive kontrolü)
    4. Placeholder replacement
    5. Final subject ve body oluştur
    6. From bilgisini belirle (request'te varsa kullan, yoksa appsettings'ten al)
    7. Direct API endpoint ile aynı akış (queue'ya ekle)
  - Response: Notification ID ve status
  - **Kullanım:** Authenticated kullanıcılar için template-based mail gönderme

**Placeholder Replacement:**
- [ ] `{{variableName}}` formatında placeholder'lar
- [ ] `messageObject` içindeki değerlerle replace
- [ ] Missing variable validation
- [ ] Nested object desteği (gelecekte): `{{user.name}}`, `{{order.total}}`
- [ ] Array iteration desteği (gelecekte)

**Referans:** Detaylı tasarım için `docs/MngNotifier/MAIL_NOTIFICATION_DESIGN.md`

**Öncelik:** Yüksek (Template yönetimi kritik)

**Tahmini Süre:** 4-6 saat

---

## 🎯 GELECEK PLANLAR


---

### Phase 7: Çoklu Kanal Desteği - DÜŞÜK ÖNCELİK

**Amaç:** SMS, WhatsApp, Slack gibi kanalları eklemek

**Gereksinimler:**
- [ ] `INotificationChannel` interface
- [ ] SMS provider implementation
- [ ] WhatsApp provider implementation
- [ ] Slack provider implementation
- [ ] Channel selection logic
- [ ] Multi-channel notification support

**Öncelik:** Düşük (Mail sonrası)

---

### Phase 8: Delivery Tracking ve Reporting - ORTA ÖNCELİK

**Amaç:** Notification gönderim durumunu takip etmek ve raporlama

**Gereksinimler:**
- [ ] Notification status tracking (pending, sent, failed, delivered)
- [ ] Delivery confirmation (webhook support)
- [ ] Reporting endpoints
- [ ] Analytics (success rate, delivery time, vb.)

**Öncelik:** Orta

---

### Phase 9: Retry ve Error Handling - YÜKSEK ÖNCELİK

**Amaç:** Başarısız notification'ları yeniden denemek

**Gereksinimler:**
- [ ] Retry policy (exponential backoff)
- [ ] Dead letter queue
- [ ] Error logging ve monitoring
- [ ] Notification failure notifications

**Öncelik:** Yüksek (Production için kritik)

---

## 📋 TEKNİK DETAYLAR

### Configuration

**appsettings.json Yapısı:**
```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://admin:admin123@localhost:27017"
  },
  "MngNotifierSettings": {
    "MongoDB": {
      "DatabaseName": "mngnotifier"
    },
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "admin",
      "Password": "admin123",
      "VirtualHost": "/"
    },
    "Mail": {
      "Provider": "SMTP",
      "DefaultFrom": {
        "email": "noreply@example.com",
        "name": "MonitraNG"  // Optional
      },
      "Smtp": {
        "Host": "smtp.gmail.com",
        "Port": 587,
        "Username": "",
        "Password": "",
        "EnableSsl": true
      }
    },
    "Serilog": {
      "MinimumLevel": "Information",
      "WriteTo": [
        {
          "Name": "Console"
        },
        {
          "Name": "Seq",
          "Args": {
            "serverUrl": "http://localhost:5341"
          }
        }
      ]
    }
  }
}
```

### MongoDB Structure

**Database:** `mngnotifier` (veya domain-based: `mngnotifier_{domain}`)

**Collections:**
- `@templates` - Notification templates
- `@notifications` - Notification history
- `@notification_status` - Delivery status tracking

### API Endpoints (Planlanan)

**Health Check:**
- `GET /api/v1/health` - Comprehensive health check
- `GET /api/v1/health/live` - Liveness probe
- `GET /api/v1/health/ready` - Readiness probe

**Version:**
- `GET /api/v1/version` - Detailed version information
- `GET /api/v1/version/short` - Simple version string

**Notifications (Gelecekte):**
- `POST /api/v1/notifications/send` - Send notification
- `GET /api/v1/notifications/{id}` - Get notification status
- `GET /api/v1/notifications` - List notifications

**Templates (MngDataGateway API üzerinden):**
- `GET /api/v1/data/@mail_templates` - List templates
- `GET /api/v1/data/@mail_templates?filter=templateId:eq:{id}` - Get template
- `POST /api/v1/data/@mail_templates` - Create template
- `PUT /api/v1/data/@mail_templates/{__dataId}` - Update template
- `DELETE /api/v1/data/@mail_templates/{__dataId}` - Delete template

### Dependencies

- .NET 9.0
- MongoDB.Driver 3.3.0
- RabbitMQ.Client 7.0.0
- Serilog 8.0.0
- Asp.Versioning.Mvc 8.1.0
- FluentValidation 11.3.1
- MediatR 13.0.0 (opsiyonel)

---

## 📝 NOTLAR

### Proje İsimlendirme

- **Servis Adı:** MngNotifier
- **Namespace Pattern:** `MngNotifier.{Layer}.{Component}`
- **API Route Pattern:** `/api/v{version:apiVersion}/...`

### Referans Projeler

- **MngDataGateway:** Clean Architecture yapısı, Health Check, Version servisi
- **MngKeeper:** Authentication/Authorization pattern'leri
- **MngHub:** Event handling ve SignalR integration

### Development Port

- **HTTP:** `http://localhost:5070`
- **HTTPS:** `https://localhost:5070` (opsiyonel - Gateway'de SSL termination)
- **Not:** Port 5060 Chrome'un unsafe ports listesinde olduğu için 5070'e değiştirildi

### Docker Integration - ✅ TAMAMLANDI

- **Dockerfile:** `Presentation/MngNotifier.Api/Dockerfile` ✅
- **Docker Compose:** `ApplicationResources/mng_apps/docker-compose.yml` ✅
- **Port Mapping:** `5070:5070`
- **Health Check:** `/api/v1/health` endpoint'i kullanılıyor
- **Network:** `mng_common_mng_network` (external network)

### API Gateway Integration - ✅ TAMAMLANDI

- **Ocelot Routes:** `/notifier/api/v1/{everything}` → `http://mngnotifier:5070/api/v1/{everything}`
- **Gateway URL:** `https://localhost:5040/notifier/api/v1/...`
- **Test Edildi:** ✅ Mail gönderme testi başarılı

---

**Son Güncelleme:** 12 Ocak 2026
