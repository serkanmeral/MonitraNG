---
title: "MngEngine API Documentation"
category: "api"
tags: ["engine", "api", "endpoints", "rest"]
service: "MngEngine"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngEngine API Documentation

## Özet
MngEngine servisinin REST API endpoint'leri ve kullanım örnekleri.

## Base URL

```
http://localhost:5004/api/v1
```

API Gateway üzerinden:
```
https://api.monitra.local/engine/api/v1
```

## Authentication

Tüm endpoint'ler JWT token authentication gerektirir.

## Endpoints

### Config Endpoints

#### Get Config
```http
GET /config
```

#### Apply Config
```http
POST /config
Content-Type: application/json

{
  "config": {}
}
```

### Job Endpoints

#### Get Jobs
```http
GET /job
```

#### Create Job
```http
POST /job
Content-Type: application/json

{
  "name": "job-name",
  "cronExpression": "0 0 * * *",
  "type": "LinuxHost"
}
```

#### Update Job
```http
PUT /job/{id}
Content-Type: application/json

{
  "cronExpression": "0 0 * * *"
}
```

#### Delete Job
```http
DELETE /job/{id}
```

### Data Endpoints

#### Get Data
```http
GET /data
```

## İlgili Linkler

- [Architecture Guide](../support/architecture/ARCHITECTURE_GUIDE.md)

---

**Son Güncelleme:** 16 Ocak 2026
