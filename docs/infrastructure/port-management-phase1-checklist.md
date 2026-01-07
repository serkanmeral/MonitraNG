# Port Yönetimi Phase 1: Hazırlık Checklist

**Tarih:** 4 Ocak 2026  
**Durum:** 📋 Hazırlık Aşamasında

---

## 📋 Phase 1: Hazırlık ve Planlama Checklist

### 1. Mevcut Durum Analizi

- [ ] Mevcut port kullanımı kontrol edildi
- [ ] Docker container port mapping'leri listelendi
- [ ] Nginx durumu kontrol edildi
- [ ] Docker network'ler kontrol edildi
- [ ] Mevcut durum dokümante edildi

**Komutlar:**
```bash
# Port kullanımı
sudo netstat -tlnp | grep LISTEN

# Docker container portları
docker ps --format "table {{.Names}}\t{{.Ports}}"

# Nginx durumu
systemctl status nginx

# Docker network'ler
docker network ls | grep mng
```

### 2. Backup Stratejisi

- [ ] Backup dizini oluşturuldu
- [ ] Nginx yapılandırması yedeklendi
- [ ] Docker Compose dosyaları yedeklendi
- [ ] Mevcut port kullanımı dokümante edildi
- [ ] Docker container durumu dokümante edildi

**Backup Script:**
```bash
# Script'i sunucuya kopyala
scp scripts/infrastructure/port-management-phase1-prepare.sh root@monitrang-server:/root/MonitraNG/scripts/infrastructure/

# Sunucuda çalıştır
ssh root@monitrang-server "cd /root/MonitraNG && chmod +x scripts/infrastructure/port-management-phase1-prepare.sh && ./scripts/infrastructure/port-management-phase1-prepare.sh"
```

**Manuel Backup:**
```bash
# Backup dizini oluştur
mkdir -p ~/backups/port-management-$(date +%Y%m%d-%H%M%S)
BACKUP_DIR=~/backups/port-management-$(date +%Y%m%d-%H%M%S)

# Nginx yapılandırması
sudo cp /etc/nginx/sites-available/monitrang $BACKUP_DIR/nginx/

# Docker Compose dosyaları
cp ApplicationResources/mng_common/docker-compose.yml $BACKUP_DIR/
cp ApplicationResources/mng_apps/docker-compose.yml $BACKUP_DIR/
cp ApplicationResources/mng_apps/docker-compose.production.yml $BACKUP_DIR/
```

### 3. Rollback Planı

- [ ] Rollback script'i hazırlandı
- [ ] Rollback prosedürü dokümante edildi
- [ ] Rollback test edildi (opsiyonel)

**Rollback Script:**
```bash
# Script'i sunucuya kopyala
scp scripts/infrastructure/port-management-rollback.sh root@monitrang-server:/root/MonitraNG/scripts/infrastructure/

# Sunucuda çalıştır (backup dizini ile)
ssh root@monitrang-server "cd /root/MonitraNG && chmod +x scripts/infrastructure/port-management-rollback.sh && ./scripts/infrastructure/port-management-rollback.sh ~/backups/port-management-YYYYMMDD-HHMMSS"
```

**Manuel Rollback:**
```bash
# 1. Nginx container'ını durdur
docker stop nginx
docker rm nginx

# 2. Host Nginx'i başlat
sudo systemctl start nginx

# 3. Yedeklenen yapılandırmayı geri yükle
sudo cp ~/backups/port-management-YYYYMMDD/nginx/monitrang /etc/nginx/sites-available/monitrang
sudo nginx -t
sudo systemctl reload nginx

# 4. Docker Compose dosyalarını geri yükle
cp ~/backups/port-management-YYYYMMDD/docker-compose.yml ApplicationResources/mng_common/
docker compose up -d
```

### 4. Test Ortamı Hazırlığı (Opsiyonel)

- [ ] Test ortamı kuruldu (opsiyonel)
- [ ] Test ortamında Phase 2 test edildi (opsiyonel)

---

## ✅ Phase 1 Tamamlandı Kontrolü

Phase 1'in tamamlandığından emin olmak için:

- [ ] Tüm backup'lar alındı
- [ ] Rollback planı hazır
- [ ] Mevcut durum dokümante edildi
- [ ] Script'ler hazır ve test edildi

**Sonraki Adım:** Phase 2 - Nginx Containerization

---

## 📝 Notlar

- Backup dizini: `~/backups/port-management-YYYYMMDD-HHMMSS`
- Rollback için backup dizini path'ini saklayın
- Tüm değişikliklerden önce backup alın
- Production'da değişiklik yapmadan önce test ortamında deneyin (opsiyonel)

---

**Son Güncelleme:** 4 Ocak 2026

