---
title: "MngEngine Architecture Guide"
category: "architecture"
tags: ["engine", "data-collection", "scheduler", "quartz", "architecture"]
service: "MngEngine"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngEngine Architecture Guide

## Özet
MngEngine, MonitraNG platformunun veri toplama motorudur. Quartz.NET kullanarak zamanlanmış görevler çalıştırır ve Linux/Windows host'lardan veri toplar.

## Genel Bakış

### Amaç
MngEngine, platformun veri toplama katmanıdır. Zamanlanmış görevler ile:
- Linux host'lardan veri toplama
- Windows host'lardan veri toplama
- Asset bilgilerini yönetme
- Config yönetimi
- Quartz.NET job scheduling

### Temel Özellikler
- ✅ Clean Architecture yapısı
- ✅ Quartz.NET job scheduling
- ✅ Linux/Windows host collectors
- ✅ Asset yönetimi
- ✅ Config yönetimi
- ✅ Data processing
- ✅ Crypt processing

## Mimari Yapı

### Clean Architecture Katmanları

```
MngEngine/
├── Core/
│   ├── MngEngine.Domain/          # Domain entities
│   └── MngEngine.Application/     # Interfaces, DTOs, handlers
├── Infrastructure/
│   ├── MngEngine.Infrastructure/  # REST context
│   └── MngEngine.Persistence/     # Repositories, services, jobs
└── Presentation/
    └── MngEngine.Api/             # REST API controllers
```

### Katman Sorumlulukları

**Domain Layer:**
- Asset entities (AssetInfo)
- Config entities (ConfigApply)
- Job entities (JobSchedule)

**Application Layer:**
- CQRS handlers (Query/Command handlers)
- DTOs (Request/Response)
- Service interfaces
- Collector requests/responses

**Infrastructure Layer:**
- REST context
- External service integrations

**Persistence Layer:**
- Repositories (DataRepository)
- Collector handlers (LinuxHost, WindowsHost)
- Quartz.NET jobs (CollectorJob)
- Services:
  - AssetService
  - ConfigService
  - DataProcessing
  - CryptProcessing
  - JobService
  - QuartzHostedService

**Presentation Layer:**
- REST API controllers
- Config endpoints
- Job endpoints

## Ana Bileşenler

### 1. Job Scheduling (Quartz.NET)
- Zamanlanmış görev yönetimi
- Cron expression desteği
- Job factory (Scoped/Singleton)
- Hosted service

### 2. Data Collectors
- **Linux Host Collector:** Linux sistemlerden veri toplama
- **Windows Host Collector:** Windows sistemlerden veri toplama
- Collector handler pattern

### 3. Asset Management
- Asset bilgilerini yönetme
- Asset service

### 4. Config Management
- Config uygulama
- Config service

### 5. Data Processing
- Veri işleme ve dönüşüm
- MongoDB veri operasyonları

### 6. Crypt Processing
- Şifreleme işlemleri
- Güvenli veri işleme

## API Endpoints

### Config Endpoints
- `GET /api/v1/config` - Config bilgileri
- `POST /api/v1/config` - Config uygulama

### Job Endpoints
- `GET /api/v1/job` - Job listesi
- `POST /api/v1/job` - Job oluşturma
- `PUT /api/v1/job/{id}` - Job güncelleme
- `DELETE /api/v1/job/{id}` - Job silme

### Data Endpoints
- `GET /api/v1/data` - Veri sorgulama

## Teknoloji Stack

- **.NET 9.0** - Framework
- **Quartz.NET** - Job scheduling
- **MongoDB** - Veritabanı
- **Serilog** - Logging
- **Swagger** - API dokümantasyonu

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- MngDataGateway (Data storage)
- MngReactor (Business logic)

### External Services
- MongoDB
- Target hosts (Linux/Windows)

## Güvenlik

- JWT token authentication
- Secure data collection
- Encrypted data processing

## Deployment

### Port
- **Default:** 5004

### Docker
```bash
docker build -t mngengine -f Dockerfile .
docker run -p 5004:5004 mngengine
```

## İlgili Dokümantasyon

- [Technical Specs](../../main/TECHNICAL_SPECS.md)
- [Gateway Integration](../guides/GATEWAY_INTEGRATION.md)

---

**Son Güncelleme:** 16 Ocak 2026
