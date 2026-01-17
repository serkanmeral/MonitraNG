---
title: "MngGateway Architecture Guide"
category: "architecture"
tags: ["gateway", "api-gateway", "ocelot", "routing", "architecture"]
service: "MngGateway"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngGateway Architecture Guide

## Özet
MngGateway, MonitraNG mikroservis ekosisteminin merkezi giriş noktasıdır. Ocelot kullanarak tüm mikroservislerin tek giriş noktasından yönetilmesini sağlar.

## Genel Bakış

### Amaç
MngGateway, tüm mikroservislerin tek giriş noktasından yönetilmesini sağlayan bir API Gateway servisidir. Ocelot kullanılarak geliştirilmiştir.

### Temel Özellikler
- ✅ Tek giriş noktası
- ✅ Merkezi authentication (JWT validation)
- ✅ Rate limiting
- ✅ CORS policy
- ✅ Request/Response logging
- ✅ SSL/TLS termination
- ✅ Backend izolasyonu

## Mimari Yapı

### Clean Architecture Katmanları

```
MngGateway/
├── Core/
│   ├── MngGateway.Domain/          # Entities, Exceptions
│   └── MngGateway.Application/    # Settings, Interfaces
├── Infrastructure/
│   └── MngGateway.Infrastructure/ # Ocelot, JWT, Logging
└── Presentation/
    └── MngGateway.Api/            # Program.cs, Ocelot config
```

### Katman Sorumlulukları

**Domain Layer:**
- Gateway entities
- Domain exceptions

**Application Layer:**
- Gateway settings
- Service interfaces
- Configuration

**Infrastructure Layer:**
- Ocelot configuration
- JWT validation
- Rate limiting
- CORS policy
- Serilog logging

**Presentation Layer:**
- Ocelot middleware
- Gateway API

## Routing Yapısı

```
/keeper/*     → MngKeeper:5001
/data/*       → MngDataGateway:5010
/hub/*        → MngHub:5020
/reactor/*    → MngReactor:5003
/engine/*     → MngEngine:5004
/notifier/*   → MngNotifier:5070
/scheduler/*  → MngScheduler:5060
/llm/*        → MngLLM:5050
/admin/*      → MngAdmin:5080
/auth/*       → Keycloak:8080
```

## Ana Bileşenler

### 1. Request Routing
- Ocelot routing configuration
- Load balancing
- Service discovery

### 2. Authentication & Authorization
- JWT token validation
- Keycloak integration
- Token forwarding

### 3. Rate Limiting
- Client/IP bazlı throttling
- Authenticated limit: 100 req/min
- Unauthenticated limit: 30 req/min

### 4. CORS Policy
- Frontend origin whitelist
- Configurable origins

### 5. Request/Response Logging
- Serilog integration
- Request/response logging
- Error logging

### 6. SSL/TLS Termination
- Tek sertifika yönetimi
- HTTPS support

## Konfigürasyon

### appsettings.json
```json
{
  "MngGatewaySettings": {
    "Server": {
      "Port": 5000,
      "Scheme": "https"
    },
    "Jwt": {
      "Authority": "http://keycloak:8080/realms/monitra",
      "Audience": "account"
    },
    "Cors": {
      "AllowedOrigins": ["https://app.monitra.local"]
    },
    "RateLimit": {
      "EnableRateLimiting": true,
      "AuthenticatedLimit": 100
    }
  }
}
```

### ocelot.json
Ocelot routing yapılandırması `Presentation/MngGateway.Api/ocelot.json` dosyasında tanımlanmıştır.

## Teknoloji Stack

- **.NET 9.0** - Framework
- **Ocelot** - API Gateway framework
- **JWT Bearer** - Authentication
- **Serilog** - Logging

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- Tüm backend servisler (routing)

### External Services
- Keycloak (Authentication)

## Güvenlik

- JWT token validation (Keycloak)
- Rate limiting (30-500 req/min)
- CORS policy
- SSL/TLS termination
- Backend servisler internal network'te

## Deployment

### Port
- **Default:** 5000

### Docker
```bash
docker build -t mnggateway -f Dockerfile .
docker run -p 5000:5000 mnggateway
```

## Notlar

- Gateway, backend servislerin port/host bilgilerini gizler
- Tüm servisler internal network üzerinde çalışır
- Sertifika yönetimi sadece gateway'de yapılır

## İlgili Dokümantasyon

- [API Documentation](../api/API_DOCUMENTATION.md)
- [Guides](../guides/USAGE_GUIDE.md)
- [ROADMAP](../../../MngGateway/ROADMAP.md)

---

**Son Güncelleme:** 16 Ocak 2026
