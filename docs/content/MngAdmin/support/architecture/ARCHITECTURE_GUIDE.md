---
title: "MngAdmin Architecture Guide"
category: "architecture"
tags: ["admin", "backup", "management", "minio", "architecture"]
service: "MngAdmin"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngAdmin Architecture Guide

## Özet
MngAdmin, MonitraNG sisteminin genel yönetimsel işlemlerini gerçekleştiren bir mikroservistir. İlk aşamada veritabanı yedekleme işlemlerini yönetir.

## Genel Bakış

### Amaç
MngAdmin, MonitraNG sisteminin genel yönetimsel işlemlerini gerçekleştiren bir mikroservistir. İlk aşamada veritabanı yedekleme işlemlerini yönetir.

### Temel Özellikler
- ✅ Clean Architecture yapısı
- ✅ Backup Management (System ve Domain)
- ✅ MinIO Integration
- ✅ Retention Policy
- ✅ Docker Support
- ✅ API Gateway Integration

## Mimari Yapı

### Clean Architecture Katmanları

```
MngAdmin/
├── Core/
│   ├── MngAdmin.Domain/          # Domain entities, exceptions
│   └── MngAdmin.Application/     # Interfaces, configurations, DTOs
├── Infrastructure/
│   ├── MngAdmin.Infrastructure/ # MongoDB, MinIO, PostgreSQL services
│   └── MngAdmin.Persistence/     # Repositories
└── Presentation/
    └── MngAdmin.Api/             # API controllers, middleware
```

### Katman Sorumlulukları

**Domain Layer:**
- Backup entities
- Backup status entities
- Domain exceptions

**Application Layer:**
- Service interfaces
- DTOs (Request/Response)
- Application settings
- Configuration

**Infrastructure Layer:**
- MongoDB integration
- PostgreSQL integration
- MinIO integration
- Backup services

**Persistence Layer:**
- Repositories
- MongoDB context

**Presentation Layer:**
- REST API controllers
- BackupController
- HealthController
- VersionController

## Ana Bileşenler

### 1. System Backup
- MongoDB system backup (mngkeeper, mngtemplates)
- PostgreSQL system backup (keycloak)
- MinIO'ya yükleme (system bucket)
- Backup status tracking
- Retention policy

### 2. Domain Backup
- Domain MongoDB backup (mng_{domain_name})
- Domain discovery (mng_* pattern)
- MinIO'ya yükleme (domain bucket)
- Backup status tracking
- Retention policy (database bazında)

### 3. Full Backup
- System backup orchestration
- Domain backup orchestration
- Comprehensive backup management

### 4. Backup Management
- Backup listesi
- Backup durumu
- Backup silme
- Backup restore (planlanan)

## API Endpoints

### System Backup Endpoints
- `POST /api/v1/backup/system` - System backup oluştur
- `POST /api/v1/backup/system/mongodb` - System MongoDB backup
- `POST /api/v1/backup/system/postgresql` - System PostgreSQL backup
- `GET /api/v1/backup/system` - System backup listesi

### Domain Backup Endpoints
- `POST /api/v1/backup/domain/{domainName}` - Domain backup oluştur
- `GET /api/v1/backup/domain/{domainName}` - Domain backup listesi

### Full Backup Endpoints
- `POST /api/v1/backup/full` - Full backup oluştur

### Backup Management Endpoints
- `GET /api/v1/backup/{backupId}` - Backup durumu
- `DELETE /api/v1/backup/{backupId}` - Backup silme

### Health Endpoints
- `GET /api/v1/health` - Health check (MongoDB, disk space)

### Version Endpoints
- `GET /api/v1/version` - Versiyon bilgisi

## Backup Format

### MongoDB Backup
- Format: ZIP compressed
- Storage: `system/backup/mongodb/{database}_{timestamp}.zip`
- Domain: `{domain_bucket}/backups/mongodb/{database}_{timestamp}.zip`

### PostgreSQL Backup
- Format: GZIP compressed (plain text SQL)
- Storage: `system/backup/postgresql/{database}_{timestamp}.sql.gz`

## Teknoloji Stack

- **.NET 9.0** - Framework
- **MongoDB** - Veritabanı
- **PostgreSQL** - Keycloak database
- **MinIO** - Object storage
- **Serilog** - Logging
- **Swagger/Scalar** - API dokümantasyonu

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- MngGateway (API Gateway)

### External Services
- MongoDB
- PostgreSQL
- MinIO

## Güvenlik

- JWT token authentication
- Secure backup storage
- Encrypted backups (planlanan)
- Access control

## Deployment

### Port
- **Default:** 5080

### Docker
```bash
docker build -t mngadmin -f Dockerfile .
docker run -p 5080:5080 mngadmin
```

## İlgili Dokümantasyon

- [Technical Specs (API)](../../main/TECHNICAL_SPECS.md)
- [Gateway Integration](../guides/GATEWAY_INTEGRATION.md)
- [ROADMAP](../../main/ROADMAP.md)

---

**Son Güncelleme:** 16 Ocak 2026
