---
title: "MngReactor Architecture Guide"
category: "architecture"
tags: ["reactor", "business-logic", "main-service", "architecture"]
service: "MngReactor"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngReactor Architecture Guide

## Özet
MngReactor, MonitraNG platformunun ana business logic servisidir. Asset yönetimi, data processing, engine entegrasyonu, LDAP işlemleri ve MQTT iletişimi gibi temel iş mantığı operasyonlarını yönetir.

## Genel Bakış

### Amaç
MngReactor, platformun merkezi business logic katmanıdır. Diğer servislerle entegre çalışarak:
- Asset (varlık) yönetimi
- Veri işleme ve dönüşüm
- Engine servisleri ile iletişim
- LDAP/OpenLDAP entegrasyonu
- MQTT mesajlaşma
- Token yönetimi
- Domain işlemleri

### Temel Özellikler
- ✅ Clean Architecture yapısı
- ✅ CQRS pattern (MediatR)
- ✅ Asset yönetimi
- ✅ Data processing
- ✅ Engine entegrasyonu
- ✅ LDAP/OpenLDAP desteği
- ✅ MQTT iletişimi
- ✅ Token generation
- ✅ Domain processing

## Mimari Yapı

### Clean Architecture Katmanları

```
MngReactor/
├── Core/
│   ├── MngReactor.Domain/          # Domain entities, interfaces
│   └── MngReactor.Application/     # CQRS handlers, DTOs, interfaces
├── Infrastructure/
│   ├── MngReactor.Infrastructure/  # External services (MQTT)
│   └── MngReactor.Persistence/     # Repositories, processing services
└── Presentation/
    └── MngReactor.Api/             # REST API controllers
```

### Katman Sorumlulukları

**Domain Layer:**
- Asset entities (AssetType)
- Login entities (UserInfoModel)
- Domain interfaces (IMqttService)

**Application Layer:**
- CQRS handlers (Query/Command handlers)
- DTOs (Request/Response)
- Service interfaces
- Application settings

**Infrastructure Layer:**
- MQTT service implementation
- External service integrations

**Persistence Layer:**
- MongoDB context
- Repositories (DataRepository)
- Processing services:
  - AssetProcessing
  - DataProcessing
  - EngineProcessing
  - DomainProcessing
  - LdapProcessing
  - CryptProcessing
  - TokenProcessing

**Presentation Layer:**
- REST API controllers
- Version endpoint
- Health check

## Ana Bileşenler

### 1. Asset Yönetimi
- Asset tree yapısı
- Asset type yönetimi
- Engine assets entegrasyonu

### 2. Data Processing
- Veri işleme ve dönüşüm
- MongoDB veri operasyonları
- Query ve command işlemleri

### 3. Engine Entegrasyonu
- Engine servisleri ile iletişim
- Asset bilgilerini engine'den alma
- Engine data processing

### 4. LDAP/OpenLDAP
- LDAP arama işlemleri
- OpenLDAP entegrasyonu
- Kullanıcı ve grup yönetimi

### 5. MQTT İletişimi
- MQTT mesajlaşma
- Topic subscription: `MNG/collect/#`
- Message publishing
- Detaylı yapılandırma: [Configuration](../guides/CONFIGURATION.md#mqtt-yapilandirmasi)

### 6. Token Yönetimi
- Token generation
- Token processing
- Authentication token işlemleri

### 7. Domain Processing
- Domain işlemleri
- Multi-tenant yönetim

## API Endpoints

### Asset Endpoints
- `GET /api/v1/asset/tree` - Asset tree yapısı
- `GET /api/v1/engine/assets` - Engine assets

### Data Endpoints
- `GET /api/v1/data` - Veri sorgulama
- `POST /api/v1/data` - Veri işleme

### Engine Endpoints
- `GET /api/v1/engine` - Engine bilgileri

### Auth Endpoints
- `POST /api/v1/auth/token` - Token oluşturma

### MQTT Endpoints
- `POST /api/v1/mqtt/publish` - MQTT mesaj gönderme

### Common Endpoints
- `GET /api/v1/common` - Ortak işlemler

### Version Endpoints
- `GET /api/v1/version` - Versiyon bilgisi

## Teknoloji Stack

- **.NET 9.0** - Framework
- **MediatR** - CQRS pattern
- **MongoDB** - Veritabanı
- **MQTT** - Mesajlaşma
- **Serilog** - Logging
- **Swagger** - API dokümantasyonu

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- MngDataGateway (Data operations)
- MngEngine (Data collection)
- MngHub (Real-time events)

### External Services
- MongoDB
- MQTT Broker
- LDAP/OpenLDAP Server

## Güvenlik

- JWT token authentication
- Domain-based isolation
- Secure token generation
- Encrypted data processing

## Deployment

### Port
- **Default:** 5003

### Docker (tek container)
```bash
cd MngReactor
docker build -t mngreactor -f Dockerfile .
docker run -p 5003:5003 mngreactor
```

### Docker (mng_apps compose)
MngReactor, `ApplicationResources/mng_apps` compose dosyasında tanımlıdır:

```bash
cd ApplicationResources/mng_apps
docker-compose -f docker-compose.yml build mngreactor
docker-compose -f docker-compose.yml up -d mngreactor
```

Ön koşul: `mng_common` (MongoDB, RabbitMQ, Mosquitto vb.) çalışıyor olmalı. Detaylı yapılandırma için [Configuration](../guides/CONFIGURATION.md) rehberine bakınız.

## Testler

- **Entegrasyon testleri (in-process):** `dotnet test` — WebApplicationFactory ile 48 test
- **Docker testleri:** `dotnet test --filter "Category=Docker"` — Container üzerinde HTTP testleri (MngReactor localhost:5003'te çalışıyor olmalı)
- **Smoke test (PowerShell):** `ApplicationResources/mng_apps/test-mngreactor-docker.ps1`

## İlgili Dokümantasyon

- [Technical Specs](../../main/TECHNICAL_SPECS.md)
- [Configuration](../guides/CONFIGURATION.md)
- [Gateway Integration](../guides/GATEWAY_INTEGRATION.md)
- [Usage Guide](../guides/USAGE_GUIDE.md)

---

**Son Güncelleme:** Ocak 2026
