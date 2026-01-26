---
title: "MngReactor API Documentation"
category: "api"
tags: ["reactor", "api", "endpoints", "rest"]
service: "MngReactor"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngReactor API Documentation

## Özet
MngReactor servisinin REST API endpoint'leri ve kullanım örnekleri.

## Base URL

```
http://localhost:5003/api/v1
```

API Gateway üzerinden:
```
https://api.monitra.local/reactor/api/v1
```

## Authentication

Tüm endpoint'ler JWT token authentication gerektirir:

```http
Authorization: Bearer {access_token}
```

## Endpoints

### Asset Endpoints

#### Get Asset Tree
```http
GET /asset/tree
```

**Response:**
```json
{
  "assets": [
    {
      "id": "asset-001",
      "name": "Asset 1",
      "type": "Server",
      "children": []
    }
  ]
}
```

#### Get Engine Assets
```http
GET /engine/assets
```

**Response:**
```json
{
  "assets": [
    {
      "id": "engine-asset-001",
      "name": "Engine Asset 1"
    }
  ]
}
```

### Data Endpoints

#### Get Data
```http
GET /data?filter={filter}
```

**Query Parameters:**
- `filter` - MongoDB filter JSON

**Response:**
```json
{
  "data": [],
  "total": 0
}
```

#### Process Data
```http
POST /data
Content-Type: application/json

{
  "operation": "process",
  "data": {}
}
```

### Engine Endpoints

#### Get Engine Information
```http
GET /engine
```

**Response:**
```json
{
  "engines": []
}
```

### Auth Endpoints

#### Generate Token
```http
POST /auth/token
Content-Type: application/json

{
  "username": "user",
  "password": "pass"
}
```

**Response:**
```json
{
  "token": "jwt_token_here"
}
```

### MQTT Endpoints

#### Publish Message
```http
POST /mqtt/publish
Content-Type: application/json

{
  "topic": "topic/name",
  "message": "message content"
}
```

### Version Endpoints

#### Get Version
```http
GET /version
```

**Response:**
```json
{
  "version": "1.0.0",
  "buildDate": "2026-01-16T00:00:00Z"
}
```

## Error Responses

### 400 Bad Request
```json
{
  "error": "Invalid request",
  "message": "Error details"
}
```

### 401 Unauthorized
```json
{
  "error": "Unauthorized",
  "message": "Invalid or missing token"
}
```

### 500 Internal Server Error
```json
{
  "error": "Internal server error",
  "message": "Error details"
}
```

## İlgili Linkler

- [Architecture Guide](../support/architecture/ARCHITECTURE_GUIDE.md)
- [Usage Guide](../support/guides/USAGE_GUIDE.md)

---

**Son Güncelleme:** 16 Ocak 2026
