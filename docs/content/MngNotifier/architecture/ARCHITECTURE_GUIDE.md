---
title: "MngNotifier Architecture Guide"
category: "architecture"
tags: ["notifier", "notification", "email", "messaging", "architecture"]
service: "MngNotifier"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngNotifier Architecture Guide

## Özet
MngNotifier, MonitraNG platformunun merkezi bildirim servisidir. E-posta, SMS, WhatsApp ve diğer bildirim kanallarını yönetir.

## Genel Bakış

### Amaç
MngNotifier, platformun tüm bildirim işlemlerini merkezi olarak yönetir. İlk aşamada e-posta bildirimleri ile başlamış, gelecekte SMS, WhatsApp, Slack ve diğer kanalları destekleyecektir.

### Temel Özellikler
- ✅ Clean Architecture yapısı
- ✅ E-posta bildirimleri
- ✅ Template yönetimi (planlanan)
- ✅ RabbitMQ event consumer (planlanan)
- ✅ Multi-channel support (planlanan)
- ✅ MongoDB storage
- ✅ RabbitMQ integration

## Mimari Yapı

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
- Notification entities
- Template entities (planlanan)
- Domain exceptions

**Application Layer:**
- Service interfaces
- DTOs (Request/Response)
- Application settings
- Configuration

**Infrastructure Layer:**
- MongoDB integration
- RabbitMQ integration
- Mail service (SMTP)
- Template engine (planlanan)

**Persistence Layer:**
- Repositories
- MongoDB context

**Presentation Layer:**
- REST API controllers
- Health check
- Version endpoint
- Notification endpoints

## Ana Bileşenler

### 1. E-posta Bildirimleri
- SMTP entegrasyonu
- Direct API endpoint
- Template-based sending (planlanan)

### 2. Template Yönetimi (Planlanan)
- Template CRUD operations
- Placeholder replacement
- Multi-language templates

### 3. RabbitMQ Integration (Planlanan)
- Event consumer
- Async notification processing
- Queue management

### 4. Multi-Channel Support (Planlanan)
- SMS notifications
- WhatsApp notifications
- Slack notifications
- Push notifications

## API Endpoints

### Notification Endpoints
- `POST /api/v1/notification/email` - E-posta gönderme
- `POST /api/v1/notification/sms` - SMS gönderme (planlanan)
- `POST /api/v1/notification/whatsapp` - WhatsApp gönderme (planlanan)

### Template Endpoints (Planlanan)
- `GET /api/v1/template` - Template listesi
- `POST /api/v1/template` - Template oluşturma
- `PUT /api/v1/template/{id}` - Template güncelleme
- `DELETE /api/v1/template/{id}` - Template silme

### Health Endpoints
- `GET /api/v1/health` - Health check

### Version Endpoints
- `GET /api/v1/version` - Versiyon bilgisi

## Teknoloji Stack

- **.NET 9.0** - Framework
- **MongoDB** - Veritabanı
- **RabbitMQ** - Message queue
- **SMTP** - E-posta gönderimi
- **Serilog** - Logging
- **Swagger/Scalar** - API dokümantasyonu

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- MngGateway (API Gateway)

### External Services
- MongoDB
- RabbitMQ
- SMTP Server
- SMS Gateway (planlanan)
- WhatsApp API (planlanan)

## Güvenlik

- JWT token authentication
- Secure SMTP connection
- Encrypted credentials
- Rate limiting

## Deployment

### Port
- **Default:** 5070

### Docker
```bash
docker build -t mngnotifier -f Dockerfile .
docker run -p 5070:5070 mngnotifier
```

## İlgili Dokümantasyon

- [API Documentation](../api/API_DOCUMENTATION.md)
- [Guides](../guides/USAGE_GUIDE.md)
- [MngGateway Integration](../guides/GATEWAY_INTEGRATION.md)

---

**Son Güncelleme:** 16 Ocak 2026
