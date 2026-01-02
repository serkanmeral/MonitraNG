# İlk CI/CD Konfigürasyon Yedeklemesi - Adım Adım Rehber

**Tarih:** 1 Ocak 2026  
**Durum:** İlk Yedekleme

---

## 🎯 Amaç

Mevcut CI/CD ve deployment konfigürasyonlarını yedeklemek ve geri dönüş noktası oluşturmak.

---

## 📋 Yedekleme Adımları

### 1. Production Sunucusuna Bağlan

```bash
ssh root@monitra-server
```

**Not:** SSH key yapılandırması tamamlandı, password gerektirmez.

### 2. Repository'ye Git

```bash
cd /root/MonitraNG
```

### 3. Script'i Çalıştırılabilir Yap

```bash
chmod +x scripts/backup-cicd-config.sh
```

### 4. Yedekleme Yap

```bash
BACKUP_DIR="/root/backups" sh scripts/backup-cicd-config.sh
```

### 5. Yedekleme Sonucunu Kontrol Et

```bash
# Yedekleme klasörünü kontrol et
ls -lh /root/backups | grep cicd-config-backup

# Manifest dosyasını oku
cat /root/backups/cicd-config-backup_*/manifest.txt
```

---

## ✅ Beklenen Çıktı

```
==========================================
MonitraNG CI/CD Configuration Backup
Date: 20260101_120000
Backup Name: cicd-config-backup_20260101_120000
==========================================
Backing up GitLab CI/CD configuration...
✓ .gitlab-ci.yml backed up
Backing up Docker Compose files...
✓ docker-compose.production.yml backed up
✓ docker-compose.common.yml backed up
Backing up scripts...
✓ backup-pre-deploy.sh backed up
✓ restore-backup.sh backed up
✓ monitor-services.sh backed up
Backing up GitLab Runner configuration...
✓ GitLab Runner config.toml backed up
Backing up GitLab configuration...
✓ GitLab gitlab.rb backed up
Backing up CI/CD documentation...
✓ CICD_DEPLOYMENT_COMPLETE_GUIDE.md backed up
✓ DEPLOYMENT_GUIDE.md backed up
...
Backing up Git state...
✓ Git state backed up

==========================================
Backup completed successfully!
Backup location: /root/backups/cicd-config-backup_20260101_120000
Manifest: /root/backups/cicd-config-backup_20260101_120000/manifest.txt
==========================================
```

---

## 📊 Yedeklenen Dosyalar

- ✅ `.gitlab-ci.yml` - Pipeline yapılandırması
- ✅ `docker-compose.production.yml` - Production servisleri
- ✅ `docker-compose.common.yml` - GitLab ve infrastructure
- ✅ Deployment script'leri (backup-pre-deploy.sh, restore-backup.sh, monitor-services.sh)
- ✅ GitLab Runner config (`config.toml`)
- ✅ GitLab config (`gitlab.rb`, `gitlab-secrets.json`)
- ✅ CI/CD dokümantasyonları
- ✅ Git state (commit hash, branch, remotes)

---

## 🔍 Yedekleme Kontrolü

```bash
# Yedekleme boyutunu kontrol et
du -sh /root/backups/cicd-config-backup_*

# Yedekleme içeriğini listele
ls -R /root/backups/cicd-config-backup_*/

# Manifest'i oku
cat /root/backups/cicd-config-backup_*/manifest.txt
```

---

## 📝 Notlar

- Yedekleme konumu: `/root/backups/cicd-config-backup_YYYYMMDD_HHMMSS/`
- Her yedekleme kendi klasöründe saklanır
- Manifest dosyası yedeklenen tüm dosyaları listeler
- Yedekleme boyutu: ~1-5 MB (config dosyaları küçük)

---

**Son Güncelleme:** 1 Ocak 2026

