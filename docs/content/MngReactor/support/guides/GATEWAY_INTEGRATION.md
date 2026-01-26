---
title: "MngReactor Gateway Integration"
category: "guides"
tags: ["reactor", "gateway", "integration", "routing"]
service: "MngReactor"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
---

# MngReactor Gateway Integration

## Özet
MngReactor servisinin MngGateway ile entegrasyonu ve routing yapılandırması.

## Gateway Routing

MngGateway üzerinden MngReactor'a erişim:

```
https://api.monitra.local/reactor/*
```

Backend routing:
```
/reactor/* → MngReactor:5003
```

## Ocelot Configuration

MngGateway'in `ocelot.json` dosyasında:

```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/v1/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        {
          "Host": "mngreactor",
          "Port": 5003
        }
      ],
      "UpstreamPathTemplate": "/reactor/api/v1/{everything}",
      "UpstreamHttpMethod": ["GET", "POST", "PUT", "DELETE"],
      "AuthenticationOptions": {
        "AuthenticationProviderKey": "Bearer",
        "AllowedScopes": []
      }
    }
  ]
}
```

## Authentication

Tüm istekler JWT token gerektirir. Token MngKeeper'dan alınır ve Gateway üzerinden iletilir.

## Örnek Kullanım

### Direct Access
```http
GET http://localhost:5003/api/v1/version
Authorization: Bearer {token}
```

### Gateway Access
```http
GET https://api.monitra.local/reactor/api/v1/version
Authorization: Bearer {token}
```

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)
- [Technical Specs](../../main/TECHNICAL_SPECS.md)

---

**Son Güncelleme:** 16 Ocak 2026
