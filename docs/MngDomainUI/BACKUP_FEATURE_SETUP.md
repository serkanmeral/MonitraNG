# Domain Backup Özelliği Kurulum Rehberi

Bu dokümantasyon, Domain Yönetimi sayfasına eklenen yedek yönetimi özelliğinin kurulum ve kullanımını açıklar.

## Ön Gereksinimler

1. **MngAdmin Servisi**: Yedek alma işlemlerini gerçekleştiren servis
2. **MinIO**: Yedek dosyalarının saklandığı object storage
3. **MongoDB**: Domain veritabanları ve backup status tracking için

## Kurulum Adımları

### 1. MngAdmin Servisini Build ve Çalıştırma

```bash
cd ApplicationResources/mng_apps

# MngAdmin'i build et
docker-compose build mngadmin

# MngAdmin'i çalıştır
docker-compose up -d mngadmin

# Logları kontrol et
docker-compose logs -f mngadmin
```

### 2. MngDomainUI Environment Variables

MngDomainUI için gerekli environment variable'ları ayarlayın:

#### Docker Compose ile (Önerilen)

`docker-compose.yml` dosyasına MngDomainUI servisi için şu environment variable'ları ekleyin:

```yaml
mngdomainui:
  environment:
    # MngAdmin API URL (server-side için)
    - SERVER_ADMIN_URL=http://mngadmin:5080
    # Client-side için (eğer gerekirse)
    - ADMIN_URL=http://localhost:5080
```

#### Local Development için

`.env` dosyası oluşturun veya mevcut environment variable'ları ayarlayın:

```bash
# MngAdmin API URL
SERVER_ADMIN_URL=http://mngadmin:5080  # Docker içinde
# veya
SERVER_ADMIN_URL=http://localhost:5080  # Local development

ADMIN_URL=http://localhost:5080  # Client-side (browser)
```

### 3. MngDomainUI'yi Çalıştırma

#### Docker ile

```bash
cd ApplicationResources/mng_apps
docker-compose up -d mngdomainui
```

#### Local Development ile

```bash
cd MngDomainUI
npm install
npm run dev
```

## Kullanım

### 1. Domain Yönetimi Sayfasına Erişim

1. Tarayıcıda `http://localhost:3010/domains` adresine gidin
2. Bir domain'e tıklayın
3. "Backups" sekmesine gidin

### 2. Yedek Listesi Görüntüleme

- Backups sekmesinde domain'e ait tüm yedekler listelenir
- Her yedek için şu bilgiler gösterilir:
  - **Status**: completed, in_progress, veya failed
  - **Database**: Yedeklenen veritabanı adı
  - **Created**: Yedek oluşturulma tarihi
  - **Size**: Yedek dosyası boyutu
  - **Duration**: Yedek alma süresi
  - **Path**: MinIO'daki dosya yolu

### 3. Yeni Yedek Oluşturma

1. Backups sekmesinde "Create Backup" butonuna tıklayın
2. Yedek alma işlemi başlar (status: in_progress)
3. İşlem tamamlandığında liste otomatik güncellenir
4. Başarı/hata mesajı gösterilir

## API Endpoints

MngDomainUI, MngAdmin API'sine şu endpoint'ler üzerinden erişir:

### Server-side Proxy Routes

- `GET /api/admin/backup/domain/{domainName}` - Domain yedek listesi
- `POST /api/admin/backup/domain/{domainName}` - Domain yedek oluştur
- `GET /api/admin/backup/{backupId}` - Yedek durumu

### Backend API (MngAdmin)

- `GET /api/v1/backup/domain/{domainName}` - Domain yedek listesi
- `POST /api/v1/backup/domain/{domainName}` - Domain yedek oluştur
- `GET /api/v1/backup/{backupId}` - Yedek durumu

## Yapılandırma

### MngAdmin Backup Ayarları

`docker-compose.yml` içinde MngAdmin için backup ayarları:

```yaml
MngAdminSettings__Backup__MaxBackupCount=10  # Maksimum yedek sayısı
MngAdminSettings__Backup__MinIO__DomainBackupPath=backups  # Domain yedek yolu
MngAdminSettings__MinIO__Endpoint=minio:9000  # MinIO endpoint
```

### Yedek Depolama Yapısı

Domain yedekleri MinIO'da şu yapıda saklanır:

```
mng-{domainName}/
  └── backups/
      └── mongodb/
          └── mng_{domainName}_{timestamp}.zip
```

Örnek:
```
mng-meral/
  └── backups/
      └── mongodb/
          └── mng_meral_20250124_143022.zip
```

## Sorun Giderme

### Yedek Listesi Görünmüyor

1. **MngAdmin servisinin çalıştığını kontrol edin:**
   ```bash
   docker-compose ps mngadmin
   curl http://localhost:5080/health
   ```

2. **Server-side proxy route'un çalıştığını kontrol edin:**
   - Browser console'da network isteklerini kontrol edin
   - Server loglarını kontrol edin: `docker-compose logs mngdomainui`

3. **Environment variable'ları kontrol edin:**
   ```bash
   docker-compose exec mngdomainui env | grep ADMIN
   ```

### Yedek Oluşturma Başarısız

1. **MongoDB bağlantısını kontrol edin:**
   - Domain veritabanının var olduğundan emin olun
   - MongoDB servisinin çalıştığını kontrol edin

2. **MinIO bağlantısını kontrol edin:**
   - MinIO servisinin çalıştığını kontrol edin
   - Bucket'ın oluşturulduğunu kontrol edin

3. **Logları kontrol edin:**
   ```bash
   docker-compose logs mngadmin | grep -i backup
   ```

### Port Çakışması

Eğer 5080 portu kullanılıyorsa:

1. `docker-compose.yml` dosyasında port mapping'i değiştirin
2. MngDomainUI'deki `SERVER_ADMIN_URL` environment variable'ını güncelleyin

## Test

### Manuel Test

1. Bir domain oluşturun
2. Domain detay sayfasına gidin
3. Backups sekmesine tıklayın
4. "Create Backup" butonuna tıklayın
5. Yedek listesinin güncellendiğini kontrol edin

### API Test

```bash
# Yedek listesi
curl http://localhost:5080/api/v1/backup/domain/meral

# Yedek oluştur
curl -X POST http://localhost:5080/api/v1/backup/domain/meral \
  -H "Content-Type: application/json" \
  -d '{"databaseType": "mongodb"}'
```

## Notlar

- Yedekler otomatik olarak retention policy'ye göre yönetilir
- Maksimum yedek sayısı (MaxBackupCount) aşıldığında eski yedekler otomatik silinir
- Yedek alma işlemi asenkron olarak çalışır (in_progress → completed/failed)
- MinIO'da bucket'lar otomatik oluşturulur (mng-{domainName} formatında)
