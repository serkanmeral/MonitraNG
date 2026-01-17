---
title: "MngNotifier API Documentation"
category: "api"
tags: ["notifier", "api", "endpoints", "rest"]
service: "MngNotifier"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngNotifier API Documentation

## Base URL

```
http://localhost:5070/api/v1
```

## Endpoints

### Notification Endpoints

#### Send Email
```http
POST /notification/email
Content-Type: application/json

{
  "to": "user@example.com",
  "subject": "Subject",
  "body": "Email body"
}
```

### Health Endpoints

#### Get Health
```http
GET /health
```

### Version Endpoints

#### Get Version
```http
GET /version
```

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)

---

**Son Güncelleme:** 16 Ocak 2026
