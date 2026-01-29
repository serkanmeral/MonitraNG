# GitLab Runner ve Pipeline — Notlar, Yapılandırma ve Sorun Giderme

**Tarih:** Ocak 2026 (birleştirilmiş sürüm)  
**Durum:** ✅ Pipeline başarıyla çalışıyor - Tüm job'lar passed  
**Sunucu:** 45.141.151.52 (monitrang-server)

Bu doküman şu eski notların tek dosyada toplanmış halidir: **GITLAB_RUNNER_SUCCESS**, **RUNNER_ISSUES_FOUND**, **RUNNER_FIX_STEP2**, **SUCCESSFUL_RUNNER_CONFIGURATION**.

**En sık tespit edilen sorunlar:** (1) Runner container veya config'de `network_mode` bridge kalmış — host olmalı; (2) Runner config `url` hostname (`http://gitlab:80`) — external IP (`http://45.141.151.52:8090`) olmalı. Çözüm özeti: docker-compose’da `network_mode: host`, runner’ı IP ile yeniden kaydetmek, “Bilinen Sorunlar” bölümüne bakmak.

---

## 🎯 Bu Dokümantasyonun Amacı

Bu dokümantasyon, başarılı çalışan GitLab Runner yapılandırmasını kaydetmek ve gelecekte sorun yaşandığında hızlıca geri yüklemek için hazırlanmıştır.

**Kullanım Senaryoları:**
- Runner yapılandırması bozulduğunda
- Sunucu yeniden kurulduğunda
- Yeni bir ortamda kurulum yapılırken
- Sorun giderme için referans olarak

---

## ✅ Başarılı Yapılandırma Özeti

### Çalışan Özellikler
- ✅ GitLab UI erişilebilir: `http://45.141.151.52:8090`
- ✅ Runner container host network'te çalışıyor
- ✅ Runner GitLab'a bağlanabiliyor
- ✅ Pipeline'lar başarıyla çalışıyor
- ✅ Git fetch başarılı
- ✅ Tüm job'lar passed (test-setup, build, test, build-docker, openapi-extract, validate-docs, deploy-docs, pages)
- ✅ Pages artifacts upload başarılı

### Runner kayıt bilgisi (referans)
- **Runner adı:** MonitraNG Runner / monitrang-runner
- **Executor:** docker
- **Default image:** mcr.microsoft.com/dotnet/sdk:9.0
- **Tags:** docker
- **URL:** `http://45.141.151.52:8090`

---

## 📋 1. Docker Compose Yapılandırması

### Dosya: `ApplicationResources/mng_common/docker-compose.yml`

#### GitLab Runner Servisi

```yaml
  # GitLab Runner
  gitlab-runner:
    image: gitlab/gitlab-runner:latest
    container_name: gitlab-runner
    network_mode: host  # Host network for external IP access
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - gitlab_runner_config:/etc/gitlab-runner
    environment:
      - DOCKER_HOST=unix:///var/run/docker.sock
    restart: unless-stopped
    depends_on:
      - gitlab
```

**Kritik Ayarlar:**
- ✅ `network_mode: host` - Build container'larının external IP'ye erişmesi için
- ✅ Docker socket mount: `/var/run/docker.sock:/var/run/docker.sock`
- ✅ Config volume: `gitlab_runner_config:/etc/gitlab-runner`
- ❌ `networks:` satırı YOK (host network kullanıldığı için)

---

#### GitLab Servisi (Önemli Port Ayarları)

```yaml
  gitlab:
    image: gitlab/gitlab-ce:latest
    container_name: gitlab
    hostname: gitlab.local
    environment:
      GITLAB_OMNIBUS_CONFIG: |
        external_url 'http://45.141.151.52:8090'
        # ... diğer ayarlar
        # GitLab Pages configuration
        pages_external_url 'http://localhost'
        gitlab_pages['enable'] = true
        gitlab_pages['external_http'] = ['0.0.0.0:8090']
        pages_nginx['enable'] = true
    ports:
      - "8090:80"           # HTTP
      - "443:443"         # HTTPS
      - "2222:22"         # SSH (mapped to 2222 to avoid conflict)
      # GitLab Pages ports removed - causing port conflict
      # - "8090:8090"       # GitLab Pages HTTP (KALDIRILDI - port çakışması)
      # - "8091:8091"       # GitLab Pages HTTPS (KALDIRILDI)
```

**Kritik Ayarlar:**
- ✅ `external_url 'http://45.141.151.52:8090'` - External IP kullanılıyor
- ❌ GitLab Pages port mapping'leri KALDIRILDI (port çakışması nedeniyle)

---

## 📋 2. Runner Config Yapılandırması

### Dosya: `/etc/gitlab-runner/config.toml` (container içinde)

**Kritik Ayarlar:**
- ✅ `url = "http://45.141.151.52:8090"` - External IP formatında (host network'te hostname çalışmaz)
- ✅ `network_mode = "host"` - Build container'ları host network'te çalışacak
- ✅ `privileged = true` - Docker-in-Docker için gerekli
- ✅ Docker socket volume: `/var/run/docker.sock:/var/run/docker.sock`
- ❌ `extra_hosts` YOK (host network'te gerekmez)

---

## 📋 3. GitLab CI/CD Pipeline Yapılandırması

### Dosya: `.gitlab-ci.yml`

**Retry (GitLab CE limit):** `max: 2`; `when`: `runner_system_failure`, `stuck_or_timeout_failure`, `api_failure` — `network_failure` desteklenmiyor.

**Pages artifacts:** `exclude`: `*.map`, `*.log`, `.cache/`; script içinde gereksiz dosyalar temizlenmeli.

---

## 🔄 Restore/Recovery Adımları

### Senaryo 1: Runner Config Bozuldu

**Adım 1: Mevcut Config'i Yedekle**
```bash
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > /tmp/runner-config-backup.toml"
```

**Adım 2: Config'i Düzelt**
```bash
ssh root@monitrang-server "docker cp gitlab-runner:/etc/gitlab-runner/config.toml /tmp/config.toml"
ssh root@monitrang-server "sed -i 's|http://gitlab|http://45.141.151.52:8090|g' /tmp/config.toml"
ssh root@monitrang-server "sed -i 's|mng_common_mng_network|host|g' /tmp/config.toml"
ssh root@monitrang-server "sed -i '/extra_hosts/d' /tmp/config.toml"
ssh root@monitrang-server "docker cp /tmp/config.toml gitlab-runner:/etc/gitlab-runner/config.toml"
ssh root@monitrang-server "cd /root/MonitraNG/ApplicationResources/mng_common && docker compose restart gitlab-runner"
```

**Adım 3: Doğrula**
```bash
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify"
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner list"
```

### Senaryo 2: Runner'ı Sıfırdan Kaydetme

**Adım 1:** GitLab UI → `http://45.141.151.52:8090` → Proje → **Settings > CI/CD > Runners** → "Set up a specific runner manually" → token'ı kopyala.

**Adım 2: Runner'ı Kaydet**
```bash
ssh root@monitrang-server "docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url \"http://45.141.151.52:8090\" \
  --registration-token \"YOUR_TOKEN_HERE\" \
  --executor \"docker\" \
  --docker-image \"mcr.microsoft.com/dotnet/sdk:9.0\" \
  --description \"monitrang-runner\" \
  --tag-list \"docker\" \
  --run-untagged=\"true\" \
  --locked=\"false\" \
  --docker-privileged=\"true\" \
  --docker-network-mode=\"host\""
```

**Adım 3:** `gitlab-runner verify` ve `gitlab-runner list` ile doğrula.

### Senaryo 3: Docker Compose Yapılandırması Bozuldu

Runner servisinde `network_mode: host` olduğundan emin olun. Sonra:
```bash
ssh root@monitrang-server "cd /root/MonitraNG/ApplicationResources/mng_common && docker compose stop gitlab-runner && docker compose rm -f gitlab-runner && docker compose up -d gitlab-runner"
ssh root@monitrang-server "docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'"
# Beklenen: "host"
```

---

## 🔍 Doğrulama Komutları

```bash
# Runner container ve network
docker ps | grep gitlab-runner
docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'   # "host"

# Runner verify ve list
docker exec gitlab-runner gitlab-runner verify
docker exec gitlab-runner gitlab-runner list

# Config URL ve network_mode
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E 'url|network_mode'
```

---

## 📊 Başarılı Pipeline Yapılandırması

**Stages:** test-setup → build → test → build-docker → openapi-extract → validate-docs → deploy-docs → pages. Retry max: 2; artifacts optimize (exclude .map, .log, .cache).

---

## 🚨 Bilinen Sorunlar ve Çözümleri

### Sorun 1: Port 8090 Çakışması
**Belirtiler:** GitLab container başlatılamıyor; "Bind for 0.0.0.0:8090 failed: port is already allocated".  
**Çözüm:** GitLab Pages port mapping'lerini kaldır (`8090:8090`, `8091:8091`); sadece `8090:80` kalsın.

### Sorun 2: Git Fetch Başarısız
**Belirtiler:** Pipeline başlamıyor; "fatal: unable to access 'http://...'".  
**Çözüm:** (1) Runner container `network_mode: host` olsun, (2) Runner config URL IP formatında olsun (`http://45.141.151.52:8090`), (3) Runner'ı restart edin.

### Sorun 3: Artifacts Upload 413
**Belirtiler:** "413 Request Entity Too Large"; Pages job fail.  
**Çözüm:** Artifacts exclude ekleyin (`*.map`, `*.log`, `.cache/`); script içinde gereksiz dosyaları temizleyin; `du -sh public` ile boyutu kontrol edin.

### Sorun 4: Retry Max Hatası
**Belirtiler:** "retry max must be less than or equal to 2".  
**Çözüm:** Tüm `retry: max: 3` değerlerini `max: 2` yapın (GitLab CE limiti).

### Sorun 5: Retry When Hatası
**Belirtiler:** "retry when contains unknown values: network_failure".  
**Çözüm:** Tüm `network_failure` değerlerini retry when'den kaldırın (GitLab CE desteklemiyor).

---

## 📝 Önemli Notlar

- **Network:** Runner ve build container’ları host; GitLab bridge. Runner config URL mutlaka external IP.
- **GitLab CE:** Retry max 2; `network_failure` retry’da kullanılamaz.
- **Docker:** Privileged ve socket mount gerekli.

---

## 🔗 İlgili Dosyalar

- `ApplicationResources/mng_common/docker-compose.yml` — Docker Compose
- `.gitlab-ci.yml` — CI/CD pipeline
- [GITLAB_RUNNER_FUNDAMENTALS](GITLAB_RUNNER_FUNDAMENTALS.md) — Temel gereksinimler
- [PAGES_ARTIFACTS_FIX](PAGES_ARTIFACTS_FIX.md) — Pages artifacts sorunu
- [INDEX](INDEX.md) — Tüm CI/CD rehberleri listesi

---

## 🎯 Hızlı Kontrol Listesi

- [ ] Runner container `network_mode: host`
- [ ] Runner config URL: `http://45.141.151.52:8090` (veya ortamınızdaki IP)
- [ ] Runner config `network_mode = "host"`, `privileged = true`
- [ ] Docker socket mount edilmiş
- [ ] `gitlab-runner verify` başarılı
- [ ] GitLab UI erişilebilir, pipeline’lar çalışıyor

---

## 📋 Backup Komutları

```bash
# Runner config
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > runner-config-backup-$(date +%Y%m%d).toml"

# docker-compose
ssh root@monitrang-server "cp /root/MonitraNG/ApplicationResources/mng_common/docker-compose.yml docker-compose-backup-$(date +%Y%m%d).yml"
```

---

**Son Güncelleme:** Ocak 2026  
**Durum:** ✅ Başarılı - Pipeline çalışıyor, tüm job'lar passed
