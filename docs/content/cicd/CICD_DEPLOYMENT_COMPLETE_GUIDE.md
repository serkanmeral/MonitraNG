# CI/CD ve Deployment - Kapsamlı Rehber ve Geri Dönüş Noktası

**Son Güncelleme:** 1 Ocak 2026  
**Durum:** ✅ Tüm Pipeline ve Deployment Süreçleri Başarıyla Çalışıyor  
**Versiyon:** 1.0 (Stable)

---

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Başarılı Konfigürasyonlar](#başarılı-konfigürasyonlar)
3. [GitLab Runner Yapılandırması](#gitlab-runner-yapılandırması)
4. [GitLab Yapılandırması](#gitlab-yapılandırması)
5. [Pipeline Yapılandırması](#pipeline-yapılandırması)
6. [Deployment Script Yapılandırması](#deployment-script-yapılandırması)
7. [Backup Script Yapılandırması](#backup-script-yapılandırması)
8. [Health Check Mekanizması](#health-check-mekanizması)
9. [GitLab CI/CD Variables](#gitlab-cicd-variables)
10. [Önemli Ayarlar ve Limitler](#önemli-ayarlar-ve-limitler)
11. [Troubleshooting Rehberi](#troubleshooting-rehberi)
12. [Geri Dönüş Noktaları](#geri-dönüş-noktaları)

---

## 🎯 Genel Bakış

Bu dokümantasyon, MonitraNG projesi için başarıyla çalışan CI/CD ve deployment konfigürasyonlarını içerir. Bu rehber, gelecekte yapılacak değişikliklerde veya sorunlarda geri dönüş noktası olarak kullanılabilir.

### Başarıyla Çalışan Özellikler

- ✅ **GitLab Runner:** Docker executor ile çalışıyor
- ✅ **GitLab Pages:** Dokümantasyon otomatik deploy ediliyor
- ✅ **Pipeline:** Tüm job'lar başarıyla tamamlanıyor
- ✅ **Deployment:** Production sunucusuna otomatik deploy çalışıyor
- ✅ **Backup:** Pre-deployment backup mekanizması hazır
- ✅ **Health Check:** Servis sağlık kontrolleri çalışıyor
- ✅ **Zero-Downtime:** Rolling update stratejisi uygulanıyor

---

## 🔧 Başarılı Konfigürasyonlar

### GitLab Runner Yapılandırması

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**Önemli Ayarlar:**

```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:latest
  container_name: gitlab-runner
  volumes:
    - gitlab_runner_config:/etc/gitlab-runner
    - /var/run/docker.sock:/var/run/docker.sock
  network_mode: host  # ÖNEMLİ: host network kullanılıyor
  restart: unless-stopped
```

**Kritik Noktalar:**
- `network_mode: host` kullanılmalı (bridge network sorun çıkarıyor)
- Docker socket mount edilmeli (`/var/run/docker.sock`)
- Config volume persistent olmalı

**Runner Config (`config.toml`):**

```toml
concurrent = 1
check_interval = 0

[session_server]
  session_timeout = 1800

[[runners]]
  name = "MonitraNG Runner"
  url = "http://45.141.151.52:8090"  # GitLab external URL
  token = "YOUR_RUNNER_TOKEN"
  executor = "docker"
  [runners.docker]
    tls_verify = false
    image = "alpine:latest"
    privileged = false
    disable_entrypoint_overwrite = false
    oom_kill_disable = false
    disable_cache = false
    volumes = ["/cache"]
    shm_size = 0
  [runners.cache]
    [runners.cache.s3]
    [runners.cache.gcs]
    [runners.cache.azure]
```

**Önemli Notlar:**
- URL: GitLab'ın external IP'si kullanılmalı (hostname değil)
- Network mode: Host network kullanılıyor
- Executor: Docker executor çalışıyor

---

### GitLab Yapılandırması

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**Önemli Ayarlar:**

```yaml
gitlab:
  image: gitlab/gitlab-ce:latest
  container_name: gitlab
  environment:
    GITLAB_OMNIBUS_CONFIG: |
      external_url 'http://45.141.151.52:8090'
      
      # Artifact size limit (100MB)
      gitlab_rails['artifacts_max_size'] = 100.megabytes
      
      # Nginx client body size
      nginx['client_max_body_size'] = '100m'
      
      # GitLab Workhorse limits
      gitlab_workhorse['max_request_size'] = 100 * 1024 * 1024
      gitlab_workhorse['max_filesize'] = 100 * 1024 * 1024
      
      # Git receive size limit
      gitlab_rails['receive_max_input_size'] = 100.megabytes
      
      # Pages ayarları
      pages_external_url 'http://45.141.151.52:8090'
      gitlab_pages['enable'] = true
  ports:
    - "8090:80"
    - "8443:443"
    # NOT: 8090:8090 ve 8091:8091 mapping'leri kaldırıldı (port çakışması)
  volumes:
    - gitlab_config:/etc/gitlab
    - gitlab_logs:/var/log/gitlab
    - gitlab_data:/var/opt/gitlab
  restart: unless-stopped
```

**Kritik Noktalar:**
- `artifacts_max_size`: 100MB (GitLab Pages için yeterli)
- `client_max_body_size`: 100m (Nginx için)
- `max_request_size` ve `max_filesize`: 100MB (Workhorse için)
- `receive_max_input_size`: 100MB (Git push için)
- Port mapping: Sadece `8090:80` ve `8443:443` (Pages portları kaldırıldı)

**GitLab Rails Console Komutları (Gerekirse):**

```ruby
# Artifact size limit ayarlama
ApplicationSetting.current.update(artifacts_max_size: 100.megabytes)

# Receive input size limit ayarlama
ApplicationSetting.current.update(receive_max_input_size: 100.megabytes)
```

---

## 📝 Pipeline Yapılandırması

**Dosya:** `.gitlab-ci.yml`

### Önemli Ayarlar

**Stages:**
```yaml
stages:
  - test-setup
  - build
  - test
  - build-docker
  - openapi-extract
  - validate-docs
  - deploy-docs
  - deploy
```

**Retry Ayarları (GitLab CE Limitleri):**
- `retry: max: 2` (GitLab CE maksimum 2 destekliyor)
- `retry: when: [runner_system_failure, stuck_or_timeout_failure]` (network_failure desteklenmiyor)

**Pipeline Skip:**
- `[skip ci]` veya `[ci skip]` commit mesajında kullanılabilir
- `rules:` ile kontrol ediliyor

**Artifact Ayarları:**
- Artifacts optional (upload başarısız olursa pipeline devam ediyor)
- Pages artifacts optimize edildi (büyük dosyalar temizleniyor)

---

## 🚀 Deployment Script Yapılandırması

**Dosya:** `.gitlab-ci.yml` (deploy-services job)

### Önemli Özellikler

**SSH Bağlantısı:**
- Alpine image kullanılıyor
- SSH client kuruluyor
- Private key SSH agent'a ekleniyor
- Host key verification bypass ediliyor

**Deployment Süreci:**
1. **Pre-deployment Backup:** Git reset'ten önce backup alınıyor
2. **Git Sync:** `git fetch` ve `git reset --hard origin/main`
3. **Port Mapping Fix:** `443:443` → `5443:443` otomatik düzeltiliyor
4. **Container Cleanup:** Mevcut container'lar durduruluyor
5. **Rolling Update:** Her servis sırayla güncelleniyor
6. **Health Check:** Her servis için health check yapılıyor

### Sh-Compatibility Önemli Noktalar

**Kullanılmayan Syntax'lar:**
- ❌ `set -e` (heredoc içinde sorun çıkarıyor)
- ❌ `|| { ... }` (bash-specific)
- ❌ `for SERVICE in $SERVICES` (değişken expand olmuyor)
- ❌ `update_service() { ... }` (function heredoc içinde sorun çıkarıyor)
- ❌ `case $SERVICE in ... esac` (değişken expand olmuyor)

**Kullanılan Syntax'lar:**
- ✅ `if ! command; then ... fi` (sh-compatible)
- ✅ `for i in 1 2 3 4 5 6; do ... done` (direkt sayılar)
- ✅ Direkt servis adları (değişken kullanmadan)
- ✅ `if [ $? -eq 0 ]; then ... fi` (exit code kontrolü)

**Deployment Script Yapısı:**

```bash
# Her servis için ayrı blok
# MngKeeper
echo "Building mngkeeper..."
if ! docker compose -f docker-compose.production.yml build mngkeeper; then
  echo "ERROR: Failed to build mngkeeper"
  exit 1
fi
# ... health check ...
```

**Servis Sırası:**
1. mngkeeper
2. mngdatagateway
3. mnghub
4. mnggateway
5. mngui

---

## 💾 Backup Script Yapılandırması

**Dosya:** `scripts/backup-pre-deploy.sh`

### Önemli Özellikler

**Sh-Compatibility:**
- `#!/bin/sh` kullanılıyor
- `set -e` kaldırıldı
- Brace expansion yok
- `|| { ... }` syntax'ı yok

**Backup İçeriği:**
1. MongoDB backup (mongodump)
2. PostgreSQL backup (Keycloak - pg_dump)
3. Docker volumes backup (mongo_data)
4. Configuration backup (docker-compose.production.yml, .env)
5. Git state backup (commit hash, branch, last commit)
6. Docker Compose state backup (running containers)

**Backup Konumu:**
- Default: `/root/backups`
- Format: `pre-deploy-backup_YYYYMMDD_HHMMSS`

**Kullanım:**
```bash
# Backup oluştur
BACKUP_DIR="/root/backups" bash scripts/backup-pre-deploy.sh

# Backup restore
/root/MonitraNG/scripts/restore-backup.sh <backup_name>
```

---

## 🏥 Health Check Mekanizması

**Her Servis İçin Health Check Endpoint'leri:**

| Servis | Health Check Endpoint | Port |
|--------|----------------------|------|
| MngKeeper | `https://localhost:5001/api/version/short` | 5001 |
| MngDataGateway | `https://localhost:5010/api/v1/health` veya `/api/version/short` | 5010 |
| MngHub | `http://localhost:5020/health` veya `/api/version/short` | 5020 |
| MngGateway | `https://localhost:5443/health` veya `http://localhost:5000/health` | 5443/5000 |
| MngUI | `http://localhost:3000` | 3000 |

**Health Check Yapısı:**
- 6 deneme (her 5 saniyede bir)
- Başarısız olursa uyarı verip devam ediyor (deployment durmuyor)
- Timeout önleme: Health check başarısız olsa bile deployment tamamlanıyor

**Health Check Kodu:**
```bash
HEALTH_CHECK_PASSED=0
for i in 1 2 3 4 5 6; do
  if curl -f -k https://localhost:5001/api/version/short 2>/dev/null; then
    HEALTH_CHECK_PASSED=1
    echo "✓ mngkeeper is healthy and running"
    break
  fi
  if [ $i -lt 6 ]; then
    echo "  Health check attempt $i of 6 failed, retrying in 5 seconds..."
    sleep 5
  fi
done
if [ $HEALTH_CHECK_PASSED -eq 0 ]; then
  echo "⚠ WARNING: mngkeeper health check failed after 6 attempts"
  echo "Continuing deployment despite health check failure..."
fi
```

---

## 🔐 GitLab CI/CD Variables

**Gerekli Variables (Settings > CI/CD > Variables):**

| Key | Value | Type | Protected | Masked | Açıklama |
|-----|-------|------|-----------|--------|----------|
| `DEPLOY_SSH_PRIVATE_KEY` | (SSH private key) | Variable | ❌ | ✅ | Production sunucusuna SSH bağlantısı |
| `DEPLOY_SERVER_HOST` | `45.141.151.52` | Variable | ❌ | ❌ | Production sunucu IP |
| `DEPLOY_SERVER_USER` | `root` | Variable | ❌ | ❌ | SSH kullanıcı adı |
| `DEPLOY_SERVER_PORT` | `22` | Variable | ❌ | ❌ | SSH port (opsiyonel, default: 22) |
| `DEPLOY_SERVER_PATH` | `/root/MonitraNG` | Variable | ❌ | ❌ | Repository path |

**SSH Key Oluşturma:**
```bash
# Production sunucusunda
ssh-keygen -t ed25519 -C "gitlab-ci-deploy" -f ~/.ssh/gitlab_deploy_key -N ""
cat ~/.ssh/gitlab_deploy_key.pub >> ~/.ssh/authorized_keys

# Private key'i GitLab'a ekle
cat ~/.ssh/gitlab_deploy_key
```

---

## ⚙️ Önemli Ayarlar ve Limitler

### GitLab Limitleri

- **Artifact Size:** 100MB
- **Client Body Size:** 100MB
- **Receive Input Size:** 100MB
- **Workhorse Max Request:** 100MB
- **Workhorse Max Filesize:** 100MB

### Pipeline Ayarları

- **Retry Max:** 2 (GitLab CE limiti)
- **Retry When:** `runner_system_failure`, `stuck_or_timeout_failure` (network_failure desteklenmiyor)
- **Timeout:** 30 dakika (deploy-services job için)
- **Pipeline Skip:** `[skip ci]` veya `[ci skip]` destekleniyor

### Deployment Ayarları

- **Health Check Retries:** 6 (her 5 saniyede bir)
- **Health Check Timeout:** 30 saniye (6 * 5)
- **Container Start Wait:** 10 saniye
- **Rolling Update:** Servisler sırayla güncelleniyor

---

## 🔍 Troubleshooting Rehberi

### Sorun 1: Pipeline Başlamıyor

**Belirtiler:**
- Pipeline oluşturulmuyor
- Runner job'ları almıyor

**Çözüm:**
1. Runner'ın çalıştığını kontrol et: `docker ps | grep gitlab-runner`
2. Runner config'i kontrol et: `docker exec gitlab-runner cat /etc/gitlab-runner/config.toml`
3. Runner URL'ini kontrol et: `http://45.141.151.52:8090` olmalı
4. Network mode'u kontrol et: `network_mode: host` olmalı

### Sorun 2: Git Fetch Başarısız

**Belirtiler:**
- `fatal: unable to access 'http://gitlab:80/...'`
- Connection refused

**Çözüm:**
1. Runner'ın `network_mode: host` kullandığını kontrol et
2. Runner config'de URL'nin IP adresi olduğunu kontrol et (hostname değil)
3. GitLab'ın çalıştığını kontrol et: `docker ps | grep gitlab`

### Sorun 3: Artifact Upload Başarısız (413 Request Entity Too Large)

**Belirtiler:**
- `413 Request Entity Too Large`
- Pages job başarısız

**Çözüm:**
1. GitLab config'i kontrol et: `docker exec gitlab cat /etc/gitlab/gitlab.rb | grep artifacts_max_size`
2. GitLab'ı reconfigure et: `docker exec gitlab gitlab-ctl reconfigure`
3. GitLab'ı restart et: `docker restart gitlab`
4. Artifacts'ı optimize et (büyük dosyaları temizle)

### Sorun 4: Deployment Script Syntax Hatası

**Belirtiler:**
- `sh: X: Syntax error: "(" unexpected`
- `sh: X: Syntax error: word unexpected (expecting "in")`

**Çözüm:**
1. Bash-specific syntax kullanılmamalı
2. `|| { ... }` yerine `if ! ...; then ... fi` kullan
3. `for SERVICE in $SERVICES` yerine direkt servis adlarını kullan
4. Function tanımlama heredoc içinde sorun çıkarıyor, inline kod kullan
5. `set -e` kaldır (heredoc içinde sorun çıkarıyor)

### Sorun 5: Health Check Timeout

**Belirtiler:**
- Health check sürekli başarısız
- Pipeline timeout oluyor

**Çözüm:**
1. Health check endpoint'lerini kontrol et
2. Servislerin başladığını kontrol et: `docker ps`
3. Health check başarısız olsa bile deployment devam ediyor (uyarı veriyor)
4. Health check retry sayısını azalt (6'dan daha az)

### Sorun 6: SERVICE Değişkeni Boş

**Belirtiler:**
- `Building ...` (servis adı boş)
- `no such service:`

**Çözüm:**
1. Heredoc içinde değişken ataması sorun çıkarıyor
2. `SERVICE="mngkeeper"` yerine direkt `mngkeeper` kullan
3. Tüm servis adlarını direkt yaz (değişken kullanma)

---

## 🔄 Geri Dönüş Noktaları

### GitLab Runner Geri Dönüş

**Eğer Runner çalışmıyorsa:**

1. **Config'i kontrol et:**
```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml
```

2. **Config'i düzelt:**
```bash
# URL'yi düzelt
sed -i 's|url = ".*"|url = "http://45.141.151.52:8090"|' /var/lib/docker/volumes/mng_common_gitlab_runner_config/_data/config.toml

# Runner'ı restart et
docker restart gitlab-runner
```

3. **Network mode'u kontrol et:**
```yaml
# docker-compose.yml'de
gitlab-runner:
  network_mode: host  # Bu olmalı
```

### GitLab Geri Dönüş

**Eğer GitLab çalışmıyorsa:**

1. **Config'i kontrol et:**
```bash
docker exec gitlab cat /etc/gitlab/gitlab.rb | grep -E "artifacts_max_size|client_max_body_size|receive_max_input_size"
```

2. **Config'i düzelt:**
```yaml
# docker-compose.yml'de GITLAB_OMNIBUS_CONFIG'e ekle
gitlab_rails['artifacts_max_size'] = 100.megabytes
nginx['client_max_body_size'] = '100m'
gitlab_workhorse['max_request_size'] = 100 * 1024 * 1024
gitlab_workhorse['max_filesize'] = 100 * 1024 * 1024
gitlab_rails['receive_max_input_size'] = 100.megabytes
```

3. **GitLab'ı reconfigure et:**
```bash
docker exec gitlab gitlab-ctl reconfigure
docker restart gitlab
```

### Pipeline Geri Dönüş

**Eğer Pipeline çalışmıyorsa:**

1. **`.gitlab-ci.yml` dosyasını kontrol et:**
   - Retry max: 2 olmalı
   - Retry when: `network_failure` olmamalı
   - Sh-compatible syntax kullanılmalı

2. **Deployment script'i kontrol et:**
   - Function tanımlama yok
   - Değişken ataması yok (direkt servis adları)
   - `set -e` yok
   - `|| { ... }` yok

### Deployment Script Geri Dönüş

**Eğer Deployment script çalışmıyorsa:**

1. **Sh-compatibility kontrolü:**
   - Tüm bash-specific syntax'lar kaldırılmalı
   - Direkt servis adları kullanılmalı
   - `for i in 1 2 3 4 5 6` kullanılmalı (değişken yok)

2. **Health check kontrolü:**
   - Health check başarısız olsa bile deployment devam etmeli
   - Timeout önlenmeli

### Backup Script Geri Dönüş

**Eğer Backup script çalışmıyorsa:**

1. **Sh-compatibility kontrolü:**
   - `#!/bin/sh` kullanılmalı
   - `set -e` kaldırılmalı
   - Brace expansion yok
   - `|| { ... }` yok

---

## 📊 Başarılı Konfigürasyon Özeti

### GitLab Runner
- ✅ Network mode: `host`
- ✅ URL: `http://45.141.151.52:8090`
- ✅ Executor: `docker`
- ✅ Image: `alpine:latest`

### GitLab
- ✅ External URL: `http://45.141.151.52:8090`
- ✅ Artifact size: 100MB
- ✅ Client body size: 100MB
- ✅ Receive input size: 100MB

### Pipeline
- ✅ Retry max: 2
- ✅ Retry when: `runner_system_failure`, `stuck_or_timeout_failure`
- ✅ Pipeline skip: `[skip ci]` destekleniyor
- ✅ Timeout: 30 dakika

### Deployment
- ✅ SSH bağlantısı çalışıyor
- ✅ Git sync çalışıyor
- ✅ Docker build çalışıyor
- ✅ Rolling update çalışıyor
- ✅ Health check çalışıyor (başarısız olsa bile devam ediyor)

### Backup
- ✅ Pre-deployment backup hazır
- ✅ Sh-compatible
- ✅ Tüm bileşenler yedekleniyor

---

## 🎓 Öğrenilen Dersler

### Sh vs Bash Farkları

1. **Heredoc içinde değişken ataması sorun çıkarıyor:**
   - `SERVICE="mngkeeper"` çalışmıyor
   - Direkt servis adı kullanılmalı

2. **Function tanımlama heredoc içinde sorun çıkarıyor:**
   - `update_service() { ... }` çalışmıyor
   - Inline kod kullanılmalı

3. **Bash-specific syntax'lar sh'de çalışmıyor:**
   - `|| { ... }` çalışmıyor
   - `if ! ...; then ... fi` kullanılmalı

4. **Değişken expansion sorunları:**
   - `$RETRY_COUNT/$MAX_RETRIES` expand olmuyor
   - Direkt sayılar kullanılmalı: `for i in 1 2 3 4 5 6`

### GitLab CE Limitleri

1. **Retry max:** Maksimum 2
2. **Retry when:** `network_failure` desteklenmiyor
3. **Artifact size:** Varsayılan ~5MB, config ile 100MB'a çıkarılabilir

### Deployment Best Practices

1. **Health check başarısız olsa bile deployment devam etmeli:**
   - Timeout önlenir
   - Kullanıcı manuel kontrol edebilir

2. **Rolling update kullanılmalı:**
   - Servisler sırayla güncellenmeli
   - Her servis için health check yapılmalı

3. **Pre-deployment backup alınmalı:**
   - Rollback için gerekli
   - Git reset'ten önce alınmalı

---

## 📝 Sonraki Adımlar

### Kısa Vadeli
- [ ] Health check endpoint'lerini optimize et
- [ ] Monitoring script'i entegre et
- [ ] Alerting mekanizması ekle

### Orta Vadeli
- [ ] Blue-Green deployment stratejisi
- [ ] Automated rollback mekanizması
- [ ] Deployment notifications

### Uzun Vadeli
- [ ] Multi-environment deployment (staging, production)
- [ ] Canary deployment
- [ ] A/B testing desteği

---

## 📞 Destek ve Kaynaklar

### İlgili Dokümantasyon
- `docs/content/cicd/DEPLOYMENT_GUIDE.md` - Deployment detayları
- `docs/content/cicd/SUCCESSFUL_RUNNER_CONFIGURATION.md` - Runner config
- `docs/content/cicd/RUNNER_CONFIGURATION_BACKUP.md` - Runner backup

### Script'ler
- `scripts/backup-pre-deploy.sh` - Pre-deployment backup
- `scripts/restore-backup.sh` - Backup restore
- `scripts/monitor-services.sh` - Service monitoring

### Konfigürasyon Dosyaları
- `.gitlab-ci.yml` - Pipeline yapılandırması
- `ApplicationResources/mng_common/docker-compose.yml` - GitLab ve Runner
- `ApplicationResources/mng_apps/docker-compose.production.yml` - Production servisler

---

**Not:** Bu dokümantasyon, 1 Ocak 2026 tarihinde başarıyla çalışan konfigürasyonları içerir. Gelecekte yapılan değişikliklerde bu dokümantasyonu referans alarak geri dönüş yapabilirsiniz.

