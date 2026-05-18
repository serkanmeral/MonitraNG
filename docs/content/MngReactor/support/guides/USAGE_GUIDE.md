---
title: "MngReactor Usage Guide"
category: "guides"
tags: ["reactor", "usage", "tutorial", "examples"]
service: "MngReactor"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngReactor Usage Guide

## Özet
MngReactor servisinin kullanım rehberi ve örnek senaryolar.

## Önkoşullar

- MngKeeper servisi çalışıyor olmalı
- MngDataGateway servisi çalışıyor olmalı
- JWT token alınmış olmalı (MngKeeper üzerinden)

## Temel Kullanım

### Health Kontrolü (Auth gerekmez)

```http
GET /api/v1/health
GET /api/v1/health/live
GET /api/v1/health/ready
```

### 1. Asset Tree Alma

```http
GET /api/v1/asset/tree
Authorization: Bearer {token}
```

### 2. Engine Assets Alma

```http
GET /api/v1/engine/assets
Authorization: Bearer {token}
```

### 3. Veri Sorgulama

```http
GET /api/v1/data?filter={"name":"value"}
Authorization: Bearer {token}
```

### 4. MQTT Mesaj Gönderme

```http
POST /api/v1/mqtt/publish
Authorization: Bearer {token}
Content-Type: application/json

{
  "topic": "monitra/events",
  "message": "Event data"
}
```

## Testler

- **Entegrasyon:** `dotnet test` (MngReactor.Tests)
- **Docker:** `dotnet test --filter "Category=Docker"` (container localhost:5003'te çalışıyor olmalı)

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)
- [Technical Specs](../../main/TECHNICAL_SPECS.md)
- [Configuration](./CONFIGURATION.md)
- [Gateway Integration](./GATEWAY_INTEGRATION.md)

---

**Son Güncelleme:** Ocak 2026
