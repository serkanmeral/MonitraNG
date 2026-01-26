# MngAdmin Docker Kurulum ve Çalıştırma

Bu dokümantasyon, MngAdmin servisini Docker ile build etme ve çalıştırma adımlarını içerir.

## Ön Gereksinimler

- Docker ve Docker Compose yüklü olmalı
- MongoDB, MinIO, RabbitMQ gibi bağımlı servisler çalışıyor olmalı

## Hızlı Başlangıç

### 1. MngAdmin'i Build Etme

MngAdmin servisini build etmek için:

```bash
cd ApplicationResources/mng_apps
docker-compose build mngadmin
```

Veya tüm servisleri build etmek için:

```bash
docker-compose build
```

### 2. MngAdmin'i Çalıştırma

Sadece MngAdmin servisini çalıştırmak için:

```bash
docker-compose up -d mngadmin
```

Veya tüm servisleri çalıştırmak için:

```bash
docker-compose up -d
```

### 3. Logları İzleme

MngAdmin loglarını izlemek için:

```bash
docker-compose logs -f mngadmin
```

### 4. Servis Durumunu Kontrol Etme

MngAdmin'in çalışıp çalışmadığını kontrol etmek için:

```bash
docker-compose ps mngadmin
```

Veya health check endpoint'ini kullanarak:

```bash
curl http://localhost:5080/health
```

## Yapılandırma

MngAdmin yapılandırması `docker-compose.yml` dosyasındaki environment variable'lar ile yapılır:

### Önemli Yapılandırmalar

- **Port**: 5080 (http://localhost:5080)
- **MongoDB**: `mongodb://admin:admin123@mongo:27017`
- **MinIO**: `minio:9000` (backup storage için)
- **Backup Path**: Domain yedekleri `mng-{domainName}/backups/mongodb/` klasöründe saklanır

### Environment Variables

```yaml
MngAdminSettings__Server__Port=5080
MngAdminSettings__MongoDB__ConnectionString=mongodb://admin:admin123@mongo:27017
MngAdminSettings__MinIO__Endpoint=minio:9000
MngAdminSettings__Backup__MaxBackupCount=10
```

## API Endpoints

MngAdmin API'si şu endpoint'ler üzerinden erişilebilir:

- **Base URL**: `http://localhost:5080/api/v1`
- **Backup Endpoints**:
  - `GET /api/v1/backup/domain/{domainName}` - Domain yedek listesi
  - `POST /api/v1/backup/domain/{domainName}` - Domain yedek oluştur
  - `GET /api/v1/backup/system` - Sistem yedek listesi
  - `GET /api/v1/backup/{backupId}` - Yedek durumu

## Sorun Giderme

### Servis Başlamıyor

1. Logları kontrol edin:
   ```bash
   docker-compose logs mngadmin
   ```

2. Bağımlı servislerin çalıştığından emin olun:
   - MongoDB (`mongo`)
   - MinIO (`minio`)
   - RabbitMQ (`rabbitmq`)

### Port Çakışması

Eğer 5080 portu kullanılıyorsa, `docker-compose.yml` dosyasında port mapping'i değiştirin:

```yaml
ports:
  - "5081:5080"  # Host port:Container port
```

### MinIO Bağlantı Hatası

MinIO endpoint'inin doğru olduğundan emin olun:
- Docker network içinde: `minio:9000`
- Local development: `localhost:9090`

## Development

Local development için MngAdmin'i doğrudan çalıştırmak:

```bash
cd MngAdmin/Presentation/MngAdmin.Api
dotnet run
```

Bu durumda `appsettings.Development.json` dosyasındaki yapılandırmalar kullanılır.
