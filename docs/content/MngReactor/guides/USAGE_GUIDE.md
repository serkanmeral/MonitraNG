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
- MongoDB bağlantısı yapılandırılmış olmalı
- JWT token alınmış olmalı

## Temel Kullanım

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

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)
- [API Documentation](../api/API_DOCUMENTATION.md)
- [Gateway Integration](./GATEWAY_INTEGRATION.md)

---

**Son Güncelleme:** 16 Ocak 2026
