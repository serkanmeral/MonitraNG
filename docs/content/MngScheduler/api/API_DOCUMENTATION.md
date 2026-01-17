---
title: "MngScheduler API Documentation"
category: "api"
tags: ["scheduler", "api", "endpoints", "rest"]
service: "MngScheduler"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngScheduler API Documentation

## Base URL

```
http://localhost:5060/api/v1
```

## Endpoints

### System Job Endpoints

#### Get System Jobs
```http
GET /system-job
```

#### Create System Job
```http
POST /system-job
Content-Type: application/json

{
  "name": "job-name",
  "cronExpression": "0 0 * * *",
  "endpoint": "http://service:port/api/endpoint",
  "method": "GET"
}
```

### User Job Endpoints

#### Get User Jobs
```http
GET /user-job
```

#### Create User Job
```http
POST /user-job
Content-Type: application/json

{
  "name": "user-job",
  "cronExpression": "0 0 * * *",
  "endpoint": "http://service:port/api/endpoint",
  "method": "POST"
}
```

### Job Execution Endpoints

#### Get Execution History
```http
GET /job-execution
```

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)
- [ROADMAP](../../../../MngScheduler/ROADMAP.md)

---

**Son Güncelleme:** 16 Ocak 2026
