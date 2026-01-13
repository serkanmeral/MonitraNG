# MngAdmin - Geliştirme Yol Haritası

**Son Güncelleme:** 13 Ocak 2026  
**Versiyon:** 1.0.0  
**Durum:** 🚀 Aktif Geliştirme

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Tamamlanan Özellikler](#tamamlanan-özellikler)
3. [Devam Eden İşler](#devam-eden-işler)
4. [Gelecek Planlar](#gelecek-planlar)
5. [Teknik Detaylar](#teknik-detaylar)

---

## 🎯 GENEL BAKIŞ

**MngAdmin**, MonitraNG sisteminin genel yönetimsel işlemlerini gerçekleştiren bir mikroservistir. İlk aşamada veritabanı yedekleme işlemlerini yönetir.

### Temel Özellikler:
- ✅ **Clean Architecture** - MngDataGateway benzeri yapı
- ✅ **Backup Management** - System ve Domain veritabanı yedekleme
- ✅ **MinIO Integration** - Backup dosyalarının object storage'da saklanması
- ✅ **Retention Policy** - Otomatik eski backup silme
- ✅ **Docker Support** - Containerized deployment
- ✅ **API Gateway Integration** - Ocelot routing desteği

---

## ✅ TAMAMLANAN ÖZELLİKLER

### 1. Proje Yapısı ve Temel Altyapı - ✅ TAMAMLANDI

**Tarih:** 13 Ocak 2026

**Components:**
- ✅ Clean Architecture yapısı (Domain, Application, Infrastructure, Persistence, Presentation)
- ✅ Version endpoint (`/api/v1/version`)
- ✅ Health check endpoint (`/api/v1/health`)
- ✅ Swagger ve Scalar API documentation
- ✅ Serilog logging entegrasyonu
- ✅ Port: 5080

**API Endpoints:**
- ✅ `GET /api/v1/version` - Uygulama versiyonu
- ✅ `GET /api/v1/health` - Health check (MongoDB, disk space)

---

### 2. Backup Sistemi - ✅ TAMAMLANDI

**Tarih:** 13 Ocak 2026

#### 2.1 System Backup

**Components:**
- ✅ MongoDB system backup (mngkeeper, mngtemplates)
- ✅ PostgreSQL system backup (keycloak)
- ✅ MinIO'ya yükleme (system bucket)
- ✅ Backup status tracking (MongoDB collection)
- ✅ Retention policy (MaxBackupCount)

**API Endpoints:**
- ✅ `POST /api/v1/backup/system` - System backup oluştur
- ✅ `POST /api/v1/backup/system/mongodb` - System MongoDB backup
- ✅ `POST /api/v1/backup/system/postgresql` - System PostgreSQL backup
- ✅ `GET /api/v1/backup/system` - System backup listesi
- ✅ `GET /api/v1/backup/{backupId}` - Backup durumu

**Backup Format:**
- MongoDB: ZIP compressed
- PostgreSQL: GZIP compressed (plain text SQL)

**Storage:**
- System MongoDB: `system/backup/mongodb/{database}_{timestamp}.zip`
- System PostgreSQL: `system/backup/postgresql/{database}_{timestamp}.sql.gz`

#### 2.2 Domain Backup

**Components:**
- ✅ Domain MongoDB backup (mng_{domain_name})
- ✅ Domain discovery (mng_* pattern)
- ✅ MinIO'ya yükleme (domain bucket: mng-{domain})
- ✅ Backup status tracking
- ✅ Retention policy (database bazında)

**API Endpoints:**
- ✅ `POST /api/v1/backup/domain/{domainName}` - Domain backup oluştur
- ✅ `GET /api/v1/backup/domain/{domainName}` - Domain backup listesi

**Storage:**
- Domain MongoDB: `{domain_bucket}/backups/mongodb/{database}_{timestamp}.zip`

#### 2.3 Full Backup

**Components:**
- ✅ System backup orchestration (MongoDB + PostgreSQL)
- ✅ Domain discovery ve backup
- ✅ Sıralı backup işlemi
- ✅ Detaylı raporlama

**API Endpoints:**
- ✅ `POST /api/v1/backup/full` - Full backup (system + all domains)

**Response:**
- System backup listesi
- Domain backup listesi
- Başarılı/başarısız sayıları
- Toplam süre

#### 2.4 Retention Policy

**Components:**
- ✅ MaxBackupCount yapılandırması (default: 10)
- ✅ Database bazında retention
- ✅ Eski backup'ları MinIO'dan silme
- ✅ BackupStatus collection'dan silme
- ✅ Sadece completed backup'lar için retention

**Yapılandırma:**
```json
{
  "Backup": {
    "MaxBackupCount": 10
  }
}
```

---

### 3. MinIO Entegrasyonu - ✅ TAMAMLANDI

**Tarih:** 13 Ocak 2026

**Components:**
- ✅ MinIO client entegrasyonu
- ✅ Bucket oluşturma (domain backup'lar için)
- ✅ Backup dosyası yükleme
- ✅ Backup dosyası silme
- ✅ Backup dosyası bilgisi alma

**Bucket Stratejisi:**
- System backup: `system` bucket
- Domain backup: `mng-{domain}` bucket (otomatik oluşturulur)

---

### 4. Dockerization - ✅ TAMAMLANDI

**Tarih:** 13 Ocak 2026

**Components:**
- ✅ Dockerfile (multi-stage build)
- ✅ MongoDB tools (mongodump) kurulumu
- ✅ PostgreSQL client (pg_dump) kurulumu
- ✅ docker-compose.yml entegrasyonu
- ✅ Environment variable yapılandırması
- ✅ Health check

**Docker Image:**
- Base: `mcr.microsoft.com/dotnet/aspnet:9.0`
- Tools: mongodb-database-tools, postgresql-client
- Port: 5080

---

### 5. API Gateway Entegrasyonu - ✅ TAMAMLANDI

**Tarih:** 13 Ocak 2026

**Components:**
- ✅ Ocelot route tanımları
- ✅ Gateway URL: `/admin/api/v1/*`
- ✅ Rate limiting (100 req/min)
- ✅ BackendServices yapılandırması

**Routes:**
- `/admin/api/v1/*` → `mngadmin:5080/api/v1/*`
- `/admin/api/*` → `mngadmin:5080/api/*`

---

### 6. PostgreSQL Backup Düzeltmeleri - ✅ TAMAMLANDI

**Tarih:** 13 Ocak 2026

**Sorun:**
- PostgreSQL backup dosyaları 0 byte oluyordu
- `pg_dump -F c` (custom format) kullanılıyordu
- Process çıktısı doğru okunmuyordu

**Çözüm:**
- ✅ `pg_dump -F p` (plain text format) kullanılıyor
- ✅ Process çıktısı okuma mantığı iyileştirildi
- ✅ Output stream doğru şekilde işleniyor

**Sonuç:**
- Backup dosyaları artık doğru boyutta (116+ KB)

---

## 🔄 DEVAM EDEN İŞLER

Şu anda devam eden bir iş yok.

---

## 📅 GELECEK PLANLAR

### 1. Backup Restore İşlemleri

**Öncelik:** Yüksek

**Planlanan Özellikler:**
- [ ] System backup restore (MongoDB)
- [ ] System backup restore (PostgreSQL)
- [ ] Domain backup restore
- [ ] Restore validation
- [ ] Restore status tracking

**API Endpoints:**
- `POST /api/v1/backup/restore/system/{backupId}`
- `POST /api/v1/backup/restore/domain/{domainName}/{backupId}`

---

### 2. Backup Scheduling

**Öncelik:** Orta

**Planlanan Özellikler:**
- [ ] Cron-based scheduling
- [ ] Per-database schedule configuration
- [ ] Schedule management API
- [ ] Schedule execution tracking

**Not:** Zamanlama işi başka bir servis (MngScheduler) tarafından yapılabilir.

---

### 3. Backup Verification

**Öncelik:** Orta

**Planlanan Özellikler:**
- [ ] Backup integrity check
- [ ] Backup file validation
- [ ] Automatic verification after backup
- [ ] Verification report

---

### 4. Backup Encryption

**Öncelik:** Düşük

**Planlanan Özellikler:**
- [ ] AES encryption for backup files
- [ ] Encryption key management
- [ ] Encrypted backup restore

---

### 5. Backup Compression Options

**Öncelik:** Düşük

**Planlanan Özellikler:**
- [ ] Compression level configuration
- [ ] Multiple compression formats (zip, gzip, bzip2)
- [ ] Compression performance optimization

---

### 6. Backup Monitoring & Alerts

**Öncelik:** Orta

**Planlanan Özellikler:**
- [ ] Backup failure notifications
- [ ] Backup size monitoring
- [ ] Backup duration tracking
- [ ] Alert configuration

---

### 7. Database Health Monitoring

**Öncelik:** Orta

**Planlanan Özellikler:**
- [ ] MongoDB connection health
- [ ] PostgreSQL connection health
- [ ] Database size monitoring
- [ ] Connection pool monitoring

---

### 8. System Administration Features

**Öncelik:** Düşük

**Planlanan Özellikler:**
- [ ] System configuration management
- [ ] Service status monitoring
- [ ] Log aggregation
- [ ] Performance metrics

---

## 🔧 TEKNİK DETAYLAR

### Mimari Yapı

```
MngAdmin/
├── Core/
│   ├── MngAdmin.Domain/          # Entities, Interfaces, Enums
│   └── MngAdmin.Application/     # DTOs, Services Interfaces, Configuration
├── Infrastructure/
│   ├── MngAdmin.Infrastructure/  # MinIO, MongoDB, PostgreSQL Services
│   └── MngAdmin.Persistence/     # Backup Service Implementation
└── Presentation/
    └── MngAdmin.Api/             # Controllers, Middleware, Startup
```

### Backup Akışı

```
1. Backup Request (API)
   ↓
2. BackupService.CreateBackupAsync()
   ↓
3. Database Backup Service (MongoBackupService / PostgresBackupService)
   ↓
4. Backup Stream Creation
   ↓
5. MinIO Upload (MinioBackupService)
   ↓
6. BackupStatus Update (MongoDB)
   ↓
7. Retention Policy Application
   ↓
8. Response (BackupResponseDto)
```

### Retention Policy Mantığı

```
1. Backup tamamlandıktan sonra
   ↓
2. BackupStatus collection'dan completed backup'ları getir
   ↓
3. Database bazında filtrele (Type + DatabaseName + DomainName)
   ↓
4. StartedAt'a göre sırala (en yeni önce)
   ↓
5. MaxBackupCount'tan fazla olanları sil
   ↓
6. MinIO'dan dosyayı sil
   ↓
7. BackupStatus collection'dan kaydı sil
```

### Network Yapılandırması

**Docker Networks:**
- `mng_common_mng_network` - Ana network (diğer servislerle iletişim)
- `mng_network` - PostgreSQL erişimi için (mng_common'dan)

**Container Names:**
- `mngadmin` - MngAdmin servisi
- `postgres` - PostgreSQL (mng_common)
- `mongo` - MongoDB (mng_common)
- `minio` - MinIO (mng_common)

### Yapılandırma

**appsettings.json:**
```json
{
  "MngAdminSettings": {
    "Server": {
      "Port": 5080
    },
    "Backup": {
      "MaxBackupCount": 10,
      "PostgreSQL": {
        "ConnectionString": "Host=postgres;Port=5432;Database={database};Username=keycloak;Password=keycloak123"
      }
    },
    "MinIO": {
      "Endpoint": "minio:9000"
    }
  }
}
```

---

## 📝 NOTLAR

### Backup Format Kararları

- **MongoDB:** ZIP format (mongodump çıktısı)
- **PostgreSQL:** Plain text SQL + GZIP compression (pg_dump -F p)

### PostgreSQL Backup Sorunu

**Sorun:** İlk implementasyonda backup dosyaları 0 byte oluyordu.

**Neden:** `pg_dump -F c` (custom/binary format) kullanılıyordu ve process çıktısı doğru okunmuyordu.

**Çözüm:** `pg_dump -F p` (plain text format) kullanıldı ve process çıktısı okuma mantığı iyileştirildi.

### Authentication

**Durum:** JWT authentication şu anda devre dışı (BackupController'da `[Authorize]` yok).

**Not:** İleride authentication eklenebilir.

---

## 🚀 DEPLOYMENT

### Docker Compose

```yaml
mngadmin:
  build:
    context: ../../MngAdmin
    dockerfile: Presentation/MngAdmin.Api/Dockerfile
  ports:
    - "5080:5080"
  networks:
    - mng_common_mng_network
    - mng_network
```

### Environment Variables

Tüm yapılandırma environment variable'lar üzerinden yapılabilir:
- `MngAdminSettings__Backup__MaxBackupCount=10`
- `MngAdminSettings__Backup__PostgreSQL__ConnectionString=...`
- `MngAdminSettings__MinIO__Endpoint=minio:9000`

---

**Son Güncelleme:** 13 Ocak 2026  
**Versiyon:** 1.0.0
