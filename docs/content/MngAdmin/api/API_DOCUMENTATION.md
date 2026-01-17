---
title: "MngAdmin API Documentation"
category: "api"
tags: ["admin", "api", "endpoints", "rest", "backup"]
service: "MngAdmin"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngAdmin API Documentation

## Base URL

```
http://localhost:5080/api/v1
```

## Endpoints

### System Backup Endpoints

#### Create System Backup
```http
POST /backup/system
```

#### Create System MongoDB Backup
```http
POST /backup/system/mongodb
```

#### Create System PostgreSQL Backup
```http
POST /backup/system/postgresql
```

#### Get System Backups
```http
GET /backup/system
```

### Domain Backup Endpoints

#### Create Domain Backup
```http
POST /backup/domain/{domainName}
```

#### Get Domain Backups
```http
GET /backup/domain/{domainName}
```

### Full Backup Endpoints

#### Create Full Backup
```http
POST /backup/full
```

### Backup Management Endpoints

#### Get Backup Status
```http
GET /backup/{backupId}
```

#### Delete Backup
```http
DELETE /backup/{backupId}
```

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)
- [ROADMAP](../../../../MngAdmin/ROADMAP.md)

---

**Son Güncelleme:** 16 Ocak 2026
