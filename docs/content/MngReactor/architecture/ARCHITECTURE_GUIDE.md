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
- Login entities (LoginModel, UserInfoModel)
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
- Topic subscription
- Message publishing

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

### Docker
```bash
docker build -t mngreactor -f Dockerfile .
docker run -p 5003:5003 mngreactor
```

## İlgili Dokümantasyon

- [API Documentation](../api/API_DOCUMENTATION.md)
- [Guides](../guides/USAGE_GUIDE.md)
- [MngGateway Integration](../guides/GATEWAY_INTEGRATION.md)

---

**Son Güncelleme:** 16 Ocak 2026
