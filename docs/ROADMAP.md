# MngNotifier - Geliştirme Yol Haritası

**Son Güncelleme:** 11 Ocak 2026  
**Versiyon:** 0.1.0 (Planlama Aşaması)  
**Durum:** 📋 Planlama

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

### Phase 1: Proje Yapısı ve Temel Altyapı - YÜKSEK ÖNCELİK

**Amaç:** Clean Architecture yapısını kurmak ve temel altyapıyı hazırlamak

**Gereksinimler:**
- [ ] Solution ve proje dosyalarını oluştur
  - [ ] `MngNotifier.sln`
  - [ ] `Core/MngNotifier.Domain/MngNotifier.Domain.csproj`
  - [ ] `Core/MngNotifier.Application/MngNotifier.Application.csproj`
  - [ ] `Infrastructure/MngNotifier.Infrastructure/MngNotifier.Infrastructure.csproj`
  - [ ] `Infrastructure/MngNotifier.Persistence/MngNotifier.Persistence.csproj`
  - [ ] `Presentation/MngNotifier.Api/MngNotifier.Api.csproj`
- [ ] Proje referanslarını ayarla
  - [ ] Application → Domain
  - [ ] Infrastructure → Application
  - [ ] Persistence → Application, Domain
  - [ ] Api → Application, Infrastructure, Persistence
- [ ] Temel konfigürasyon dosyalarını oluştur
  - [ ] `appsettings.json`
  - [ ] `appsettings.Development.json`
  - [ ] `Program.cs` (temel yapı)
- [ ] NuGet paketlerini ekle
  - [ ] MongoDB.Driver
  - [ ] RabbitMQ.Client
  - [ ] Serilog (Console + Seq)
  - [ ] Asp.Versioning.Mvc
  - [ ] Asp.Versioning.Mvc.ApiExplorer
  - [ ] Swashbuckle.AspNetCore (Swagger)
  - [ ] Scalar.AspNetCore (Scalar API Reference)
  - [ ] FluentValidation
  - [ ] MediatR (opsiyonel)

**Öncelik:** Yüksek (Temel yapı)

**Tahmini Süre:** 1-2 saat

---

### Phase 2: Health Check Servisi - YÜKSEK ÖNCELİK

**Amaç:** Uygulama sağlık durumunu kontrol eden servis implementasyonu

**Gereksinimler:**
- [ ] `IHealthCheckService` interface (Application layer)
- [ ] `HealthCheckService` implementation (Persistence layer)
- [ ] `HealthController` (Api layer)
- [ ] Health check bileşenleri:
  - [ ] MongoDB connection check
  - [ ] RabbitMQ connection check
  - [ ] Disk space check
  - [ ] Mail provider connection check (opsiyonel - Phase 4'te)
- [ ] Health check endpoint'leri:
  - [ ] `GET /api/v1/health` - Comprehensive health check
  - [ ] `GET /api/v1/health/live` - Liveness probe
  - [ ] `GET /api/v1/health/ready` - Readiness probe

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

### Phase 3: Version Servisi - YÜKSEK ÖNCELİK

**Amaç:** API versiyonlama ve uygulama versiyon bilgisi servisi

**Gereksinimler:**
- [ ] API Versioning yapılandırması (Program.cs)
  - [ ] URL-based versioning (`/api/v1/...`)
  - [ ] Query string-based versioning (`?version=1.0`)
  - [ ] Header-based versioning (`Api-Version: 1.0`)
  - [ ] Default version: v1.0
- [ ] `VersionController` (Api layer)
  - [ ] `GET /api/v1/version` - Detailed version information
  - [ ] `GET /api/v1/version/short` - Simple version string
- [ ] Version bilgileri:
  - [ ] Product name
  - [ ] Assembly version
  - [ ] Informational version
  - [ ] Build date
  - [ ] Company/Copyright
  - [ ] Environment
  - [ ] Runtime information (.NET version, OS, vb.)
  - [ ] Dependencies (MongoDB, RabbitMQ, Mail provider, vb.)

**Referans:** MngDataGateway Version Controller implementasyonu

**Version Response Yapısı:**
```json
{
  "product": "MngNotifier API",
  "version": "1.0.0",
  "assemblyVersion": "1.0.0.0",
  "buildDate": "2026-01-11T10:00:00Z",
  "company": "iSIM Platform",
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

### Phase 4: Swagger ve Scalar Desteği - YÜKSEK ÖNCELİK

**Amaç:** API dokümantasyonu için Swagger ve Scalar entegrasyonu

**Gereksinimler:**
- [ ] Swagger yapılandırması (Program.cs)
  - [ ] `AddSwaggerGen` configuration
  - [ ] API versioning desteği (SwaggerConfigureOptions)
  - [ ] Custom schema ID strategy
- [ ] Scalar API Reference yapılandırması
  - [ ] `MapScalarApiReference` configuration
  - [ ] Theme: Purple (diğer servislerle uyumlu)
  - [ ] OpenAPI route pattern: `/api-docs/{documentName}/swagger.json`
  - [ ] Server path configuration
- [ ] Swagger UI endpoint'leri
  - [ ] `/swagger` - Swagger UI
  - [ ] `/api-docs/{version}/swagger.json` - OpenAPI JSON
  - [ ] Scalar UI (otomatik map edilir)

**Referans:** MngDataGateway ve MngLLM Swagger/Scalar implementasyonu

**Öncelik:** Yüksek (API dokümantasyonu için kritik)

**Tahmini Süre:** 1-2 saat

---

### Phase 5: Mail Notification Servisi - YÜKSEK ÖNCELİK

**Amaç:** Mail notification gönderme servisi (iki farklı yöntemle)

**Gereksinimler:**
- [ ] Mail provider interface (`IMailProvider`)
- [ ] SMTP implementation (`SmtpMailProvider`)
- [ ] Mail notification entity (Domain)
- [ ] Notification service (`INotificationService`)
- [ ] Notification controller (`NotificationController`)

**Mail Notification Yöntemleri:**

**1. Direct API Endpoint (Unauthenticated):**
- [ ] `POST /api/v1/notifications/send` - Direct mail gönderme
  - **Authentication:** ❌ No authentication required (AllowAnonymous)
  - Request body: `to`, `cc` (optional), `from` (optional), `subject`, `body`, `isHtml` (optional)
  - `from`: `{ "email": "string", "name": "string (optional)" }` - Default: appsettings'ten alınır
  - Response: Notification ID ve status
  - Async processing (RabbitMQ queue'ya ekle)
  - **Kullanım:** Domain oluşturulduğunda, bootstrap senaryoları, external sistemler
  - **Güvenlik:** Rate limiting önerilir (gelecekte)

**2. RabbitMQ Event Consumer:**
- [ ] RabbitMQ consumer service (`NotificationEventConsumer`)
- [ ] Event model: `MailNotificationEvent`
- [ ] Queue: `mngnotifier.mail.send` (veya configurable)
- [ ] Event structure: `to`, `cc`, `subject`, `body`
- [ ] Auto-acknowledgment veya manual acknowledgment (retry için)

**Referans:** Detaylı tasarım için `docs/MngNotifier/MAIL_NOTIFICATION_DESIGN.md`

**Öncelik:** Yüksek (Temel özellik)

**Tahmini Süre:** 4-6 saat

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

- **HTTP:** `http://localhost:5030`
- **HTTPS:** `https://localhost:5031` (opsiyonel - Gateway'de SSL termination)

### Docker Integration

- **Dockerfile:** `Presentation/MngNotifier.Api/Dockerfile`
- **Docker Compose:** `ApplicationResources/mng_apps/docker-compose.yml` içine eklenecek
- **Network:** `mng_common_mng_network` (external network)

---

**Son Güncelleme:** 11 Ocak 2026
