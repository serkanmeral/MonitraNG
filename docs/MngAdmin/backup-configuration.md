# MngAdmin Backup Konfigürasyonu

## MongoDB Backup

MongoDB backup işlemleri için connection string `appsettings.json` içinde tanımlanmıştır:

```json
{
  "MngAdminSettings": {
    "MongoDB": {
      "ConnectionString": "mongodb://admin:admin123@localhost:27017"
    }
  }
}
```

### Connection String Formatı

MongoDB connection string formatı:
```
mongodb://[username]:[password]@[host]:[port]
```

Örnekler:
- `mongodb://admin:admin123@localhost:27017` - Local MongoDB with authentication
- `mongodb://localhost:27017` - Local MongoDB without authentication
- `mongodb://user:pass@mongodb.example.com:27017` - Remote MongoDB

### Backup İşlemi

`MongoBackupService` connection string'i kullanarak `mongodump` komutunu çalıştırır:

```bash
mongodump --uri="mongodb://admin:admin123@localhost:27017" --db="database_name" --out="dump_path"
```

Connection string içindeki kullanıcı adı ve şifre otomatik olarak `mongodump` komutuna geçirilir.

## MngKeeper URL Konfigürasyonu

MngKeeper için iki farklı URL kullanılabilir:

1. **HTTP**: `http://localhost:5001`
2. **HTTPS**: `https://localhost:5040/keeper`

### appsettings.json

```json
{
  "MngAdminSettings": {
    "Actors": {
      "MngKeeper": "http://localhost:5001"
    }
  }
}
```

### JWT Authentication

JWT authentication için MngKeeper URL'i `Authority` olarak kullanılır:

```csharp
options.Authority = settings.Actors.MngKeeper;
```

## PostgreSQL Backup

PostgreSQL backup için connection string formatı:

```
Host=localhost;Port=5432;Database=keycloak;Username=keycloak;Password=keycloak123
```

Bu format `pg_dump` komutuna dönüştürülür:

```bash
pg_dump -h localhost -p 5432 -U keycloak -d keycloak -F c
```

## MinIO Backup Storage

Backup dosyaları MinIO'da saklanır:

```json
{
  "MngAdminSettings": {
    "MinIO": {
      "Endpoint": "localhost:9000",
      "AccessKey": "admin",
      "SecretKey": "admin123",
      "UseSSL": false
    },
    "Backup": {
      "MinIO": {
        "SystemBucket": "system",
        "SystemBackupPath": "backup",
        "DomainBackupPath": "system/backup"
      }
    }
  }
}
```

### Backup Dosya Yolları

- **System Backups**: `{SystemBucket}/{SystemBackupPath}/{databaseType}/{database}_{timestamp}.{ext}`
  - Örnek: `system/backup/mongodb/mngkeeper_20240113_120000.zip`
  
- **Domain Backups**: `{DomainBucket}/{DomainBackupPath}/{databaseType}/{database}_{timestamp}.{ext}`
  - Örnek: `mng-meral/system/backup/mongodb/mng_meral_20240113_120000.zip`

## Retention Policy

Her veritabanı için maksimum backup sayısı:

```json
{
  "MngAdminSettings": {
    "Backup": {
      "MaxBackupCount": 10
    }
  }
}
```

En eski backup'lar otomatik olarak silinir.
