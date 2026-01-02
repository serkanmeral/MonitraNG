# CI/CD Konfigürasyon Yedekleme Rehberi

**Son Güncelleme:** 1 Ocak 2026  
**Durum:** ✅ Script'ler Hazır

---

## 📋 Genel Bakış

Bu rehber, CI/CD ve deployment konfigürasyonlarınızı yedeklemek ve geri yüklemek için kullanılır. Sistem bozulduğunda veya yanlış değişiklik yapıldığında geri dönüş noktası sağlar.

---

## 🔧 Yedeklenen Dosyalar

### 1. GitLab CI/CD Konfigürasyonu
- `.gitlab-ci.yml` - Pipeline yapılandırması

### 2. Docker Compose Dosyaları
- `docker-compose.production.yml` - Production servisleri
- `docker-compose.common.yml` - GitLab ve infrastructure
- `.env.production` - Environment variables (varsa)

### 3. Script'ler
- `backup-pre-deploy.sh` - Pre-deployment backup
- `restore-backup.sh` - Backup restore
- `monitor-services.sh` - Service monitoring

### 4. GitLab Konfigürasyonları
- `runner-config.toml` - GitLab Runner config
- `gitlab.rb` - GitLab ana konfigürasyon
- `gitlab-secrets.json` - GitLab secrets (varsa)

### 5. Dokümantasyon
- `CICD_DEPLOYMENT_COMPLETE_GUIDE.md` - Kapsamlı rehber
- `DEPLOYMENT_GUIDE.md` - Deployment rehberi
- `SUCCESSFUL_RUNNER_CONFIGURATION.md` - Runner config
- `current_status.md` - Mevcut durum

### 6. Git State
- Commit hash
- Branch bilgisi
- Remote bilgileri

---

## 💾 Yedekleme İşlemi

### Production Sunucusunda

```bash
# Production sunucusuna SSH ile bağlan
ssh root@45.141.151.52

# Repository'ye git
cd /root/MonitraNG

# Script'i çalıştırılabilir yap
chmod +x scripts/backup-cicd-config.sh

# Yedekleme yap
BACKUP_DIR="/root/backups" sh scripts/backup-cicd-config.sh
```

**Çıktı:**
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
...
==========================================
Backup completed successfully!
Backup location: /root/backups/cicd-config-backup_20260101_120000
Manifest: /root/backups/cicd-config-backup_20260101_120000/manifest.txt
==========================================
```

### Yedekleme Konumu

- **Default:** `/root/backups/cicd-config-backup_YYYYMMDD_HHMMSS/`
- **Manifest:** `manifest.txt` dosyasında yedeklenen tüm dosyalar listelenir

---

## 🔄 Geri Yükleme İşlemi

### Production Sunucusunda

```bash
# Production sunucusuna SSH ile bağlan
ssh root@45.141.151.52

# Mevcut yedekleri listele
ls -lt /root/backups | grep cicd-config-backup

# Geri yükleme yap
cd /root/MonitraNG
chmod +x scripts/restore-cicd-config.sh
sh scripts/restore-cicd-config.sh cicd-config-backup_20260101_120000
```

**Onay İstendiğinde:**
- Manuel restore: Enter'a basın
- Otomatik restore: `SKIP_CONFIRM=true sh scripts/restore-cicd-config.sh <backup_name> --skip-confirm`

### GitLab/Runner Konfigürasyonları

GitLab ve Runner container'ları çalışırken config restore edilemez. Manuel adımlar:

**GitLab Runner:**
```bash
# 1. Runner'ı durdur
docker stop gitlab-runner

# 2. Config'i kopyala
docker cp /root/backups/cicd-config-backup_XXX/gitlab-config/runner-config.toml gitlab-runner:/etc/gitlab-runner/config.toml

# 3. Runner'ı başlat
docker start gitlab-runner
```

**GitLab:**
```bash
# 1. GitLab'ı durdur
docker stop gitlab

# 2. Config'i kopyala
docker cp /root/backups/cicd-config-backup_XXX/gitlab-config/gitlab.rb gitlab:/etc/gitlab/gitlab.rb

# 3. Reconfigure et
docker exec gitlab gitlab-ctl reconfigure

# 4. GitLab'ı başlat
docker start gitlab
```

---

## 📅 Periyodik Yedekleme

### Cron Job ile Otomatik Yedekleme

```bash
# Crontab düzenle
crontab -e

# Haftalık yedekleme (Her Pazar 02:00)
0 2 * * 0 cd /root/MonitraNG && BACKUP_DIR="/root/backups" sh scripts/backup-cicd-config.sh > /dev/null 2>&1

# Veya günlük yedekleme (Her gün 02:00)
0 2 * * * cd /root/MonitraNG && BACKUP_DIR="/root/backups" sh scripts/backup-cicd-config.sh > /dev/null 2>&1
```

### Eski Yedekleri Temizleme

```bash
# 30 günden eski yedekleri sil
find /root/backups -name "cicd-config-backup_*" -type d -mtime +30 -exec rm -rf {} \;
```

---

## 🎯 Kullanım Senaryoları

### Senaryo 1: Önemli Değişiklik Öncesi Yedekleme

```bash
# Önemli bir değişiklik yapmadan önce
cd /root/MonitraNG
sh scripts/backup-cicd-config.sh

# Değişiklik yap
# ...

# Sorun olursa geri yükle
sh scripts/restore-cicd-config.sh cicd-config-backup_20260101_120000
```

### Senaryo 2: Pipeline Bozulduğunda Geri Yükleme

```bash
# Son çalışan yedeği bul
ls -lt /root/backups | grep cicd-config-backup | head -1

# Geri yükle
cd /root/MonitraNG
sh scripts/restore-cicd-config.sh <backup_name>

# Pipeline'ı test et
git push origin main
```

### Senaryo 3: GitLab Runner Sorununda Geri Yükleme

```bash
# Runner config'i geri yükle
docker stop gitlab-runner
docker cp /root/backups/cicd-config-backup_XXX/gitlab-config/runner-config.toml gitlab-runner:/etc/gitlab-runner/config.toml
docker start gitlab-runner

# Runner durumunu kontrol et
docker exec gitlab-runner gitlab-runner verify
```

---

## ⚠️ Önemli Notlar

1. **GitLab/Runner Config:** Container'lar çalışırken restore edilemez, manuel adımlar gerekir
2. **Environment Variables:** `.env` dosyaları hassas bilgiler içerir, güvenli saklanmalı
3. **Git State:** Geri yükleme sonrası git commit'ine dönmek için `git checkout <commit_hash>` kullanın
4. **Test:** Geri yükleme sonrası mutlaka pipeline'ı test edin

---

## 📊 Yedekleme Boyutu

- **Tipik Yedekleme:** ~1-5 MB (config dosyaları küçük)
- **Disk Kullanımı:** 10 yedekleme ≈ 50 MB
- **Önerilen Saklama:** Son 10 yedekleme (yaklaşık 1 ay)

---

## 🔍 Yedekleme Kontrolü

```bash
# Yedeklemeleri listele
ls -lh /root/backups | grep cicd-config-backup

# Yedekleme içeriğini kontrol et
cat /root/backups/cicd-config-backup_XXX/manifest.txt

# Yedekleme boyutunu kontrol et
du -sh /root/backups/cicd-config-backup_XXX
```

---

**Son Güncelleme:** 1 Ocak 2026

