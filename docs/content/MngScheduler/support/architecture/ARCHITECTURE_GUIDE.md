---
title: "MngScheduler Architecture Guide"
category: "architecture"
tags: ["scheduler", "job", "quartz", "cron", "architecture"]
service: "MngScheduler"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngScheduler Architecture Guide

## Özet
MngScheduler, MonitraNG platformunun zamanlanmış görev yönetim servisidir. Quartz.NET kullanarak veritabanından job tanımlarını okuyarak dinamik olarak HTTP endpoint çağrıları yapar.

## Genel Bakış

### Amaç
MngScheduler, zamanlanmış görevleri (scheduled tasks) yöneten bir infrastructure servisidir. Veritabanından job tanımlarını okuyarak dinamik olarak Quartz.NET job'ları oluşturur ve yönetir.

**ÖNEMLİ:** MngScheduler'ın amacı **işi yapmak değil, işi yapacak endpoint'i trigger etmektir**.

### Temel Özellikler
- ✅ Clean Architecture yapısı
- ✅ Quartz.NET job scheduling
- ✅ MongoDB job storage
- ✅ System Job ve User Job ayrımı
- ✅ Runtime job management
- ✅ Cron expression desteği
- ✅ HTTP GET/POST endpoint çağrıları
- ✅ RabbitMQ event publishing
- ✅ Multi-tenant/domain izolasyonu

## Mimari Yapı

### Clean Architecture Katmanları

```
MngScheduler/
├── Core/
│   ├── MngScheduler.Domain/          # Domain entities, exceptions
│   └── MngScheduler.Application/    # Interfaces, configurations, DTOs
├── Infrastructure/
│   ├── MngScheduler.Infrastructure/ # MongoDB, RabbitMQ, Quartz services
│   └── MngScheduler.Persistence/    # Repositories
└── Presentation/
    └── MngScheduler.Api/            # API controllers, middleware
```

### Katman Sorumlulukları

**Domain Layer:**
- ScheduledJob entity
- JobExecution entity
- JobType enum (System, User)
- Domain exceptions

**Application Layer:**
- Service interfaces
- DTOs (Request/Response)
- Application settings
- Configuration

**Infrastructure Layer:**
- MongoDB integration
- RabbitMQ integration
- Quartz.NET scheduler
- HTTP client services

**Persistence Layer:**
- Repositories (JobExecutionRepository, SystemJobRepository, UserJobRepository)
- Domain lookup service

**Presentation Layer:**
- REST API controllers
- SystemJobController
- UserJobController
- HealthController
- VersionController

## Ana Bileşenler

### 1. Job Scheduling (Quartz.NET)
- Zamanlanmış görev yönetimi
- Cron expression desteği
- Dynamic job creation
- Job synchronization

### 2. Job Types
- **System Job:** Sistem tarafından oluşturulan job'lar
- **User Job:** Kullanıcılar tarafından oluşturulan job'lar

### 3. HTTP Endpoint Triggering
- GET request support
- POST request support
- Custom headers
- Request body support

### 4. Job Execution Tracking
- Execution history
- Success/failure tracking
- Retry mechanism
- Error logging

### 5. Event Publishing
- RabbitMQ integration
- Job execution events
- Status updates

## API Endpoints

### System Job Endpoints
- `GET /api/v1/system-job` - System job listesi
- `POST /api/v1/system-job` - System job oluşturma
- `PUT /api/v1/system-job/{id}` - System job güncelleme
- `DELETE /api/v1/system-job/{id}` - System job silme

### User Job Endpoints
- `GET /api/v1/user-job` - User job listesi
- `POST /api/v1/user-job` - User job oluşturma
- `PUT /api/v1/user-job/{id}` - User job güncelleme
- `DELETE /api/v1/user-job/{id}` - User job silme

### Job Execution Endpoints
- `GET /api/v1/job-execution` - Execution history
- `GET /api/v1/job-execution/{id}` - Execution detayı

### Health Endpoints
- `GET /api/v1/health` - Health check

### Version Endpoints
- `GET /api/v1/version` - Versiyon bilgisi

## Teknoloji Stack

- **.NET 9.0** - Framework
- **Quartz.NET** - Job scheduling
- **MongoDB** - Veritabanı
- **RabbitMQ** - Message queue
- **Serilog** - Logging
- **Swagger/Scalar** - API dokümantasyonu

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- MngGateway (API Gateway)
- Target services (HTTP endpoints)

### External Services
- MongoDB
- RabbitMQ

## Güvenlik

- JWT token authentication
- Domain-based isolation
- Secure HTTP requests
- Rate limiting

## Deployment

### Port
- **Default:** 5060

### Docker
```bash
docker build -t mngscheduler -f Dockerfile .
docker run -p 5060:5060 mngscheduler
```

## İlgili Dokümantasyon

- [Technical Specs](../../main/TECHNICAL_SPECS.md)
- [Gateway Integration](../guides/GATEWAY_INTEGRATION.md)
- [ROADMAP](../../../MngScheduler/ROADMAP.md)

---

**Son Güncelleme:** 16 Ocak 2026
