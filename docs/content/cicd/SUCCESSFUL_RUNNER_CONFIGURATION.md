# GitLab Runner Başarılı Yapılandırma - Backup ve Restore Rehberi

**Tarih:** 15 Ocak 2025  
**Durum:** ✅ Pipeline başarıyla çalışıyor - Tüm job'lar passed  
**Sunucu:** 45.141.151.52 (monitrang-server)

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

**Tam Config İçeriği:**

```toml
concurrent = 1
check_interval = 0
shutdown_timeout = 0

[session_server]
  session_timeout = 1800

[[runners]]
  name = "MonitraNG Runner"
  url = "http://45.141.151.52:8090"
  id = 1
  token = "glrtr-H5lK4yjZsrTj7nx2IQ3qJW86MQpwOjEKdDozCw.01.121jcbg3v"
  token_obtained_at = 2025-12-31T01:37:50Z
  token_expires_at = 0001-01-01T00:00:00Z
  executor = "docker"
  [runners.cache]
    MaxUploadedArchiveSize = 0
    [runners.cache.s3]
    [runners.cache.gcs]
    [runners.cache.azure]
  [runners.docker]
    host = "unix:///var/run/docker.sock"
    tls_verify = false
    image = "mcr.microsoft.com/dotnet/sdk:9.0"
    privileged = true
    disable_entrypoint_overwrite = false
    oom_kill_disable = false
    disable_cache = false
    volumes = ["/cache", "/var/run/docker.sock:/var/run/docker.sock"]
    shm_size = 0
    network_mtu = 0
    network_mode = "host"
```

**Kritik Ayarlar:**
- ✅ `url = "http://45.141.151.52:8090"` - External IP formatında (host network'te hostname çalışmaz)
- ✅ `network_mode = "host"` - Build container'ları host network'te çalışacak
- ✅ `privileged = true` - Docker-in-Docker için gerekli
- ✅ Docker socket volume: `/var/run/docker.sock:/var/run/docker.sock`
- ❌ `extra_hosts` YOK (host network'te gerekmez)

---

## 📋 3. GitLab CI/CD Pipeline Yapılandırması

### Dosya: `.gitlab-ci.yml`

#### Önemli Ayarlar

**Retry Yapılandırması (GitLab CE Limit'leri):**
```yaml
retry:
  max: 2  # GitLab CE'de maksimum 2 (3 değil!)
  when:
    - runner_system_failure
    - stuck_or_timeout_failure
    - api_failure
    # network_failure KALDIRILDI - GitLab CE'de desteklenmiyor
```

**Pages Job Artifacts Optimizasyonu:**
```yaml
pages:
  artifacts:
    paths:
      - public
    exclude:
      # Gereksiz dosyaları hariç tut (artifacts size'ı küçültmek için)
      - public/**/*.map
      - public/**/*.log
      - public/.cache/
    expire_in: 30 days
```

**Script İçinde Temizlik:**
```bash
# Gereksiz dosyaları temizle (artifacts size'ı küçültmek için)
find public -name "*.map" -type f -delete 2>/dev/null || true
find public -name "*.log" -type f -delete 2>/dev/null || true
find public -type d -name ".cache" -exec rm -rf {} + 2>/dev/null || true
```

---

## 📋 4. GitLab Yapılandırması

### External URL
```
external_url 'http://45.141.151.52:8090'
```

### GitLab Pages
```
pages_external_url 'http://localhost'
gitlab_pages['enable'] = true
gitlab_pages['external_http'] = ['0.0.0.0:8090']
pages_nginx['enable'] = true
```

### Database (PostgreSQL - Ayrı Container)
```
gitlab_rails['db_host'] = 'gitlab-postgres'
gitlab_rails['db_port'] = 5432
gitlab_rails['db_username'] = 'gitlab'
gitlab_rails['db_password'] = 'gitlab123'
```

### Redis (Ayrı Container)
```
gitlab_rails['redis_host'] = 'gitlab-redis'
gitlab_rails['redis_port'] = 6379
gitlab_rails['redis_password'] = 'gitlab123'
```

---

## 🔄 Restore/Recovery Adımları

### Senaryo 1: Runner Config Bozuldu

**Adım 1: Mevcut Config'i Yedekle**
```bash
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > /tmp/runner-config-backup.toml"
```

**Adım 2: Config'i Düzelt**
```bash
# Config dosyasını container'dan çıkar
ssh root@monitrang-server "docker cp gitlab-runner:/etc/gitlab-runner/config.toml /tmp/config.toml"

# URL'yi düzelt (eğer hostname formatındaysa)
ssh root@monitrang-server "sed -i 's|http://gitlab|http://45.141.151.52:8090|g' /tmp/config.toml"

# network_mode'u düzelt
ssh root@monitrang-server "sed -i 's|mng_common_mng_network|host|g' /tmp/config.toml"

# extra_hosts satırını kaldır
ssh root@monitrang-server "sed -i '/extra_hosts/d' /tmp/config.toml"

# Config'i geri kopyala
ssh root@monitrang-server "docker cp /tmp/config.toml gitlab-runner:/etc/gitlab-runner/config.toml"

# Runner'ı restart et
ssh root@monitrang-server "cd /root/MonitraNG/ApplicationResources/mng_common && docker compose restart gitlab-runner"
```

**Adım 3: Doğrula**
```bash
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify"
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner list"
```

---

### Senaryo 2: Runner'ı Sıfırdan Kaydetme

**Adım 1: GitLab'dan Registration Token Al**
1. GitLab UI: `http://45.141.151.52:8090`
2. Proje: `http://45.141.151.52:8090/root/MonitraNG`
3. **Settings > CI/CD > Runners**
4. **"Set up a specific runner manually"** bölümünden token'ı kopyala

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

**Önemli Parametreler:**
- `--url "http://45.141.151.52:8090"` - External IP (host network'te hostname çalışmaz)
- `--docker-network-mode="host"` - Build container'ları host network'te
- `--docker-privileged="true"` - Docker-in-Docker için

**Adım 3: Doğrula**
```bash
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify"
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner list"
```

---

### Senaryo 3: Docker Compose Yapılandırması Bozuldu

**Adım 1: docker-compose.yml'i Kontrol Et**
```bash
ssh root@monitrang-server "cd /root/MonitraNG/ApplicationResources/mng_common && grep -A 15 'gitlab-runner:' docker-compose.yml"
```

**Adım 2: Doğru Yapılandırmayı Uygula**
```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:latest
  container_name: gitlab-runner
  network_mode: host  # ← Bu satır olmalı
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
    - gitlab_runner_config:/etc/gitlab-runner
  environment:
    - DOCKER_HOST=unix:///var/run/docker.sock
  restart: unless-stopped
  depends_on:
    - gitlab
```

**Adım 3: Container'ı Restart Et**
```bash
ssh root@monitrang-server "cd /root/MonitraNG/ApplicationResources/mng_common && docker compose stop gitlab-runner && docker compose rm -f gitlab-runner && docker compose up -d gitlab-runner"
```

**Adım 4: Network Mode'u Doğrula**
```bash
ssh root@monitrang-server "docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'"
# Beklenen: "host"
```

---

## 🔍 Doğrulama Komutları

### Runner Durumu
```bash
# Runner container durumu
ssh root@monitrang-server "docker ps | grep gitlab-runner"

# Network mode
ssh root@monitrang-server "docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'"
# Beklenen: "host"

# Runner verify
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify"
# Beklenen: "Verifying runner... is alive"

# Runner list
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner list"
# Beklenen: Runner listelenmeli ve "active" olmalı
```

### Runner Config
```bash
# Config URL kontrolü
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E '^[[:space:]]*url[[:space:]]*='"
# Beklenen: url = "http://45.141.151.52:8090"

# Config network mode kontrolü
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep network_mode"
# Beklenen: network_mode = "host"
```

### GitLab Erişimi
```bash
# GitLab container durumu
ssh root@monitrang-server "docker ps | grep gitlab"

# GitLab UI erişimi
curl -I http://45.141.151.52:8090
# Beklenen: HTTP 200 veya 302
```

---

## 📊 Başarılı Pipeline Yapılandırması

### Pipeline Stages
```
1. test-setup      ✅ Başarılı
2. build           ✅ Başarılı (MngKeeper, MngDataGateway, MngHub, MngGateway, Frontend)
3. test            ✅ Başarılı (allow_failure: true)
4. build-docker    ✅ Başarılı (Mng.Ui, MngGateway)
5. openapi-extract ✅ Başarılı
6. validate-docs   ✅ Başarılı (allow_failure: true)
7. deploy-docs     ✅ Başarılı
8. pages           ✅ Başarılı (artifacts upload başarılı)
```

### Önemli Pipeline Ayarları

**Retry:**
- `max: 2` (GitLab CE limit)
- `when: runner_system_failure, stuck_or_timeout_failure, api_failure`
- ❌ `network_failure` yok (desteklenmiyor)

**Artifacts:**
- Pages job'unda exclude: `*.map`, `*.log`, `.cache/`
- Script içinde temizlik: gereksiz dosyalar siliniyor

**Network Resilience:**
- GIT_HTTP_LOW_SPEED_LIMIT: "1000"
- GIT_HTTP_LOW_SPEED_TIME: "300"
- NPM_CONFIG_FETCH_RETRIES: "5"
- PIP_RETRIES: "5"

---

## 🚨 Bilinen Sorunlar ve Çözümleri

### Sorun 1: Port 8090 Çakışması

**Belirtiler:**
- GitLab container başlatılamıyor
- "Bind for 0.0.0.0:8090 failed: port is already allocated"

**Çözüm:**
- GitLab Pages port mapping'lerini kaldır: `8090:8090` ve `8091:8091`
- Sadece `8090:80` mapping'i kalsın

---

### Sorun 2: Git Fetch Başarısız

**Belirtiler:**
- Pipeline başlamıyor
- "fatal: unable to access 'http://45.141.151.52:8090/...'"

**Çözüm:**
1. Runner container'ının `network_mode: host` olduğunu kontrol et
2. Runner config URL'sinin IP formatında olduğunu kontrol et
3. Runner'ı restart et

---

### Sorun 3: Artifacts Upload 413 Hatası

**Belirtiler:**
- "413 Request Entity Too Large"
- Pages job fail oluyor

**Çözüm:**
1. Artifacts exclude ekle: `*.map`, `*.log`, `.cache/`
2. Script içinde gereksiz dosyaları temizle
3. Artifacts boyutunu kontrol et: `du -sh public`

---

### Sorun 4: Retry Max Hatası

**Belirtiler:**
- "retry max must be less than or equal to 2"
- Pipeline başlamıyor

**Çözüm:**
- Tüm `retry: max: 3` değerlerini `max: 2` yap

---

### Sorun 5: Retry When Hatası

**Belirtiler:**
- "retry when contains unknown values: network_failure"
- Pipeline başlamıyor

**Çözüm:**
- Tüm `network_failure` değerlerini retry when'den kaldır

---

## 📝 Önemli Notlar

### Network Yapısı
- **Runner:** Host network (`network_mode: host`)
- **GitLab:** Bridge network (`mng_network`)
- **Build Container'ları:** Host network (runner config'de `network_mode = "host"`)

### URL Yapılandırması
- **Runner Config URL:** External IP formatında olmalı (`http://45.141.151.52:8090`)
- **GitLab External URL:** External IP formatında (`http://45.141.151.52:8090`)
- **Hostname kullanılamaz:** Host network'te hostname çözümleme çalışmaz

### GitLab CE Limit'leri
- **Retry max:** 2 (3 değil!)
- **Retry when:** `network_failure` desteklenmiyor
- **Artifacts size:** Limit var (413 hatası alınırsa optimize et)

### Docker Yapılandırması
- **Privileged mode:** Aktif (Docker-in-Docker için)
- **Docker socket:** Mount edilmiş
- **Network mode:** Host (build container'ları için)

---

## 🔗 İlgili Dosyalar

### Yapılandırma Dosyaları
- `ApplicationResources/mng_common/docker-compose.yml` - Docker Compose yapılandırması
- `.gitlab-ci.yml` - CI/CD pipeline yapılandırması
- `/etc/gitlab-runner/config.toml` - Runner config (container içinde)

### Dokümantasyon
- `docs/content/cicd/GITLAB_RUNNER_FUNDAMENTALS.md` - Temel gereksinimler
- `docs/content/cicd/RUNNER_ISSUES_FOUND.md` - Tespit edilen sorunlar
- `docs/content/cicd/PAGES_ARTIFACTS_FIX.md` - Pages artifacts sorunu
- `docs/content/cicd/SUCCESSFUL_RUNNER_CONFIGURATION.md` - Bu dosya

### Script'ler
- `scripts/fix-runner-config.sh` - Runner config düzeltme script'i
- `scripts/run-runner-check.sh` - Runner durum kontrol script'i

---

## 🎯 Hızlı Kontrol Listesi

Başarılı yapılandırma için kontrol edilmesi gerekenler:

- [ ] Runner container `network_mode: host` ile çalışıyor
- [ ] Runner config URL IP formatında (`http://45.141.151.52:8090`)
- [ ] Runner config `network_mode = "host"`
- [ ] Runner config `privileged = true`
- [ ] Docker socket mount edilmiş
- [ ] Runner verify başarılı
- [ ] GitLab UI erişilebilir
- [ ] Pipeline'lar çalışıyor
- [ ] Git fetch başarılı
- [ ] Pages artifacts upload başarılı

---

## 📋 Backup Komutları

### Runner Config Backup
```bash
# Config'i yedekle
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > runner-config-backup-$(date +%Y%m%d).toml"
```

### Docker Compose Backup
```bash
# docker-compose.yml'i yedekle
ssh root@monitrang-server "cp /root/MonitraNG/ApplicationResources/mng_common/docker-compose.yml docker-compose-backup-$(date +%Y%m%d).yml"
```

### GitLab Config Backup
```bash
# GitLab config'i yedekle (volume'dan)
ssh root@monitrang-server "docker run --rm -v mng_common_gitlab_config:/data -v $(pwd):/backup alpine tar czf /backup/gitlab-config-backup-$(date +%Y%m%d).tar.gz -C /data ."
```

---

## 🔄 Tam Restore Senaryosu

### Senaryo: Sunucu Yeniden Kuruldu veya Tüm Yapılandırma Kayboldu

**Adım 1: Docker Compose Yapılandırması**
```bash
# docker-compose.yml'i kontrol et
cd /root/MonitraNG/ApplicationResources/mng_common
# Runner servisinin network_mode: host olduğundan emin ol
```

**Adım 2: Runner Container'ını Başlat**
```bash
docker compose up -d gitlab-runner
```

**Adım 3: Runner Config'i Kontrol Et**
```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml
# URL ve network_mode doğru mu kontrol et
```

**Adım 4: Runner'ı Kaydet (Gerekirse)**
```bash
# GitLab'dan token al, sonra:
docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url "http://45.141.151.52:8090" \
  --registration-token "YOUR_TOKEN" \
  --executor "docker" \
  --docker-image "mcr.microsoft.com/dotnet/sdk:9.0" \
  --description "monitrang-runner" \
  --tag-list "docker" \
  --run-untagged="true" \
  --locked="false" \
  --docker-privileged="true" \
  --docker-network-mode="host"
```

**Adım 5: Doğrula**
```bash
docker exec gitlab-runner gitlab-runner verify
docker exec gitlab-runner gitlab-runner list
```

**Adım 6: Pipeline Test**
```bash
# GitLab'da küçük bir commit yap veya pipeline'ı manuel tetikle
```

---

## 📊 Başarı Metrikleri

### Pipeline Başarı Oranı
- **Test Setup:** ✅ %100
- **Build Jobs:** ✅ %100
- **Test Jobs:** ✅ %100 (allow_failure: true)
- **Docker Build:** ✅ %100
- **OpenAPI Extract:** ✅ %100
- **Validate Docs:** ✅ %100 (allow_failure: true)
- **Deploy Docs:** ✅ %100
- **Pages:** ✅ %100

### Ortalama Pipeline Süresi
- **Test Setup:** ~30 saniye
- **Build Stage:** ~5-10 dakika (paralel)
- **Test Stage:** ~2-5 dakika (paralel)
- **Docker Build:** ~2-3 dakika
- **OpenAPI Extract:** ~3-5 dakika
- **Deploy Docs:** ~2-3 dakika
- **Pages:** ~1-2 dakika
- **Toplam:** ~15-25 dakika

---

## 🎉 Başarılı Yapılandırma Özeti

### Temel Gereksinimler (Tümü Karşılandı)

1. ✅ **Network Yapılandırması:**
   - Runner host network'te
   - Runner config URL IP formatında
   - Build container'ları host network'te

2. ✅ **Docker Erişimi:**
   - Docker socket mount edilmiş
   - Privileged mode aktif
   - Docker-in-Docker çalışıyor

3. ✅ **Runner Kaydı:**
   - Runner GitLab'a kayıtlı
   - Runner verify başarılı
   - Runner active ve online

4. ✅ **Pipeline Yapılandırması:**
   - Retry max: 2 (GitLab CE limit)
   - Retry when: desteklenen değerler
   - Artifacts optimize edilmiş
   - Tüm job'lar çalışıyor

---

**Son Güncelleme:** 15 Ocak 2025  
**Durum:** ✅ Başarılı - Pipeline çalışıyor, tüm job'lar passed

