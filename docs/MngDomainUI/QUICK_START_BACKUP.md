# Domain Backup Özelliği - Hızlı Başlangıç

## Hızlı Kurulum

### 1. MngAdmin'i Build ve Çalıştır

```bash
cd ApplicationResources/mng_apps

# MngAdmin'i build et
docker-compose build mngadmin

# MngAdmin'i çalıştır
docker-compose up -d mngadmin

# Durumu kontrol et
docker-compose ps mngadmin
```

### 2. MngDomainUI'yi Rebuild ve Çalıştır

```bash
# MngDomainUI'yi rebuild et (yeni environment variable'lar için)
docker-compose build mngdomainui

# MngDomainUI'yi çalıştır
docker-compose up -d mngdomainui

# Logları kontrol et
docker-compose logs -f mngdomainui
```

### 3. Servisleri Kontrol Et

```bash
# Tüm servislerin durumunu kontrol et
docker-compose ps

# MngAdmin API'sini test et
curl http://localhost:5080/api/v1/backup/system

# MngDomainUI'yi test et
curl http://localhost:3001/api/health
```

## Kullanım

1. Tarayıcıda `http://localhost:3001/domains` adresine gidin
2. Bir domain seçin
3. **"Backups"** sekmesine tıklayın
4. Yedek listesini görüntüleyin veya **"Create Backup"** ile yeni yedek oluşturun

## Sorun Giderme

### MngAdmin çalışmıyor

```bash
# Logları kontrol et
docker-compose logs mngadmin

# Yeniden başlat
docker-compose restart mngadmin
```

### Yedek listesi görünmüyor

1. MngAdmin'in çalıştığını kontrol edin: `curl http://localhost:5080/health`
2. Browser console'da hataları kontrol edin
3. Server loglarını kontrol edin: `docker-compose logs mngdomainui`

### Environment Variable'lar

MngDomainUI için gerekli environment variable'lar `docker-compose.yml` dosyasına eklenmiştir:
- `SERVER_ADMIN_URL=http://mngadmin:5080` (server-side)
- `ADMIN_URL=http://localhost:5080` (client-side)

## Detaylı Dokümantasyon

- [Backup Feature Setup](./BACKUP_FEATURE_SETUP.md) - Detaylı kurulum rehberi
- [MngAdmin Docker Setup](../MngAdmin/DOCKER_SETUP.md) - MngAdmin Docker kurulumu
