# GitLab Runner - Temel Gereksinimler ve Başarılı Kurulum Rehberi

**Tarih:** 15 Ocak 2025  
**Amaç:** GitLab Runner'ın başarılı çalışması için temel gereksinimleri ve doğru yapılandırmayı açıklamak

---

## 🎯 GitLab Runner Nedir ve Ne İşe Yarar?

GitLab Runner, GitLab CI/CD pipeline'larını çalıştıran servistir. GitLab'dan gelen job'ları alır ve belirtilen executor'da (Docker, shell, vb.) çalıştırır.

**Temel İşlevler:**
1. GitLab'dan job'ları alır
2. Job'ları belirtilen executor'da çalıştırır
3. Sonuçları GitLab'a geri gönderir

---

## ✅ Başarılı GitLab Runner Kurulumu İçin TEMEL GEREKSİNİMLER

### 1. Network Bağlantısı (EN ÖNEMLİSİ)

**Gereksinim:**
- Runner, GitLab'a **erişebilmeli**
- Build container'ları, GitLab repository'ye **erişebilmeli**

**Sorun:**
- GitLab repository URL'leri external IP döndürüyor (`http://45.141.151.52:8090/root/monitrang.git`)
- Build container'ları bu external IP'ye erişemiyor

**Çözüm Seçenekleri:**

#### Seçenek A: Runner'ı Host Network'te Çalıştırmak (ÖNERİLEN)
- Runner container'ı host network'te çalışır
- Build container'ları da host network'te olur
- External IP'ye erişebilirler
- **Gereksinim:** Runner config'de GitLab URL'yi IP'ye çevirmek gerekir

#### Seçenek B: GitLab'ı Internal URL ile Yapılandırmak
- GitLab'ın repository URL'lerini internal network ismi ile döndürmesi
- Build container'ları `http://gitlab/root/monitrang.git` kullanır
- **Gereksinim:** GitLab ve Runner aynı Docker network'te olmalı

### 2. Docker Erişimi

**Gereksinim:**
- Runner, Docker daemon'a erişebilmeli (Docker executor kullanıyorsak)
- Docker socket mount edilmeli: `/var/run/docker.sock:/var/run/docker.sock`
- Privileged mode aktif olmalı (Docker-in-Docker için)

**Kontrol:**
```bash
docker exec gitlab-runner docker ps
# Çıktı: Container listesi görünmeli
```

### 3. Runner Kaydı

**Gereksinim:**
- Runner, GitLab'a kayıtlı olmalı
- Registration token doğru olmalı
- Runner URL doğru olmalı (GitLab'a erişebilmeli)

**Kontrol:**
```bash
docker exec gitlab-runner gitlab-runner verify
# Çıktı: Runner "online" ve "active" olmalı
```

### 4. Executor Yapılandırması

**Gereksinim:**
- Executor tipi belirlenmeli (docker, shell, vb.)
- Docker executor için default image belirlenmeli
- Network yapılandırması doğru olmalı

---

## 🔍 Mevcut Durum Analizi

### Yapılandırma Dosyası (docker-compose.yml)

**Mevcut Yapılandırma:**
```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:latest
  container_name: gitlab-runner
  network_mode: host  # ✅ Host network kullanılıyor
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock  # ✅ Docker socket mount edilmiş
    - gitlab_runner_config:/etc/gitlab-runner  # ✅ Config volume
  environment:
    - DOCKER_HOST=unix:///var/run/docker.sock  # ✅ Docker host ayarlanmış
```

**Durum:** ✅ Docker-compose yapılandırması doğru görünüyor

### Olası Sorunlar

1. **Runner Config URL Sorunu:**
   - Runner config'de URL `http://gitlab` olabilir
   - Host network'te hostname çözümleme çalışmaz
   - **Çözüm:** URL'yi IP'ye çevirmek gerekir

2. **GitLab External URL Sorunu:**
   - GitLab `external_url` external IP'ye ayarlanmış
   - Repository URL'leri external IP döndürüyor
   - Build container'ları external IP'ye erişemiyor
   - **Çözüm:** Runner host network'te olmalı (zaten var) + Runner config URL doğru olmalı

3. **Runner Kayıt Sorunu:**
   - Runner kayıtlı değil veya yanlış token ile kayıtlı
   - **Çözüm:** Runner'ı yeniden kaydetmek

---

## 🚀 Sıfırdan Başlama Planı

### Faz 1: Temizlik ve Hazırlık

#### Adım 1.1: Mevcut Runner'ı Durdur ve Temizle

```bash
# Sunucuda
cd /root/MonitraNG/ApplicationResources/mng_common

# Runner container'ını durdur
docker compose stop gitlab-runner

# Runner container'ını kaldır (config volume korunur)
docker compose rm -f gitlab-runner

# Runner config volume'u kontrol et (gerekirse temizle)
docker volume inspect mng_common_gitlab_runner_config
# Eğer temiz başlamak istiyorsanız:
# docker volume rm mng_common_gitlab_runner_config
```

**Neden:** Eski yapılandırmaları temizlemek, karışıklığı önlemek

#### Adım 1.2: GitLab Durumunu Kontrol Et

```bash
# GitLab container'ının çalıştığını kontrol et
docker ps | grep gitlab

# GitLab'a erişilebilirliği test et
curl -I http://localhost:8090
# veya external IP'den
curl -I http://45.141.151.52:8090
```

**Neden:** Runner'ın GitLab'a bağlanabilmesi için GitLab çalışıyor olmalı

#### Adım 1.3: GitLab Container IP'sini Bul

```bash
# GitLab container'ının IP'sini bul
docker inspect gitlab | grep IPAddress
# veya
docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab
```

**Beklenen Sonuç:** `172.18.0.6` gibi bir IP (bridge network'te)

**Neden:** Runner config'de URL olarak kullanacağız

---

### Faz 2: Runner Yapılandırması

#### Adım 2.1: docker-compose.yml Kontrolü

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**Doğru Yapılandırma:**
```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:latest
  container_name: gitlab-runner
  network_mode: host  # ✅ Host network (external IP erişimi için)
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock  # ✅ Docker socket
    - gitlab_runner_config:/etc/gitlab-runner  # ✅ Config volume
  environment:
    - DOCKER_HOST=unix:///var/run/docker.sock  # ✅ Docker host
  restart: unless-stopped
  depends_on:
    - gitlab
```

**Kontrol Listesi:**
- ✅ `network_mode: host` var mı?
- ✅ Docker socket mount edilmiş mi?
- ✅ Config volume tanımlı mı?
- ✅ `networks:` satırı YOK mu? (host network kullanıyorsak)

**Neden:** Host network, build container'larının external IP'ye erişmesini sağlar

#### Adım 2.2: Runner Container'ını Başlat

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose up -d gitlab-runner

# Container'ın çalıştığını kontrol et
docker ps | grep gitlab-runner

# Network mode'u kontrol et
docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'
# Beklenen: "host"
```

**Neden:** Runner'ın host network'te çalıştığını doğrulamak

---

### Faz 3: Runner Kaydı

#### Adım 3.1: GitLab'dan Registration Token Al

1. GitLab'a giriş yap: `http://45.141.151.52:8090`
2. Proje sayfasına git: `http://45.141.151.52:8090/root/MonitraNG`
3. **Settings > CI/CD > Runners** sekmesine git
4. **"Set up a specific runner manually"** bölümünden **Registration token**'ı kopyala

**Neden:** Runner'ı kaydetmek için token gerekli

#### Adım 3.2: Runner'ı Kaydet (IP ile)

**ÖNEMLİ:** Host network kullandığımız için URL olarak IP kullanmalıyız.

```bash
# GitLab IP'sini değişkene al (örnek: 172.18.0.6)
GITLAB_IP="172.18.0.6"  # veya external IP: "45.141.151.52:8090"

# Runner'ı kaydet
docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url "http://${GITLAB_IP}" \
  --registration-token "YOUR_TOKEN_HERE" \
  --executor "docker" \
  --docker-image "mcr.microsoft.com/dotnet/sdk:9.0" \
  --description "monitrang-runner" \
  --tag-list "docker" \
  --run-untagged="true" \
  --locked="false" \
  --docker-privileged="true" \
  --docker-network-mode="host"
```

**Parametreler:**
- `--url`: GitLab IP'si (host network'te hostname çözümleme çalışmaz)
- `--executor`: Docker executor
- `--docker-image`: Default image (.NET SDK)
- `--docker-privileged`: Docker-in-Docker için
- `--docker-network-mode`: Build container'ları için host network

**Neden:** Host network'te hostname (`gitlab`) çözümlenemez, IP kullanmalıyız

#### Adım 3.3: Runner Config'i Kontrol Et

```bash
# Runner config'i oku
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml
```

**Beklenen Yapılandırma:**
```toml
[[runners]]
  name = "monitrang-runner"
  url = "http://172.18.0.6"  # veya "http://45.141.151.52:8090"
  token = "..."
  executor = "docker"
  [runners.docker]
    image = "mcr.microsoft.com/dotnet/sdk:9.0"
    privileged = true
    network_mode = "host"
```

**Kontrol Listesi:**
- ✅ URL IP formatında mı? (`http://172.18.0.6` veya `http://45.141.151.52:8090`)
- ✅ `privileged = true` var mı?
- ✅ `network_mode = "host"` var mı?

**Neden:** Config doğru olmalı ki runner çalışsın

---

### Faz 4: Doğrulama

#### Adım 4.1: Runner Durumunu Kontrol Et

```bash
# Runner listesi
docker exec gitlab-runner gitlab-runner list

# Runner verify
docker exec gitlab-runner gitlab-runner verify
```

**Beklenen Sonuç:**
```
Verifying runner... is alive                        runner=...
```

**Neden:** Runner'ın GitLab'a bağlanabildiğini doğrulamak

#### Adım 4.2: GitLab UI'da Kontrol Et

1. GitLab'da **Settings > CI/CD > Runners** sekmesine git
2. **"Available specific runners"** bölümünde runner'ı görmelisiniz
3. Status: **"Online"** ve **"Active"** olmalı

**Neden:** Runner'ın GitLab tarafından tanındığını doğrulamak

#### Adım 4.3: Docker Erişimini Test Et

```bash
# Runner container'ından Docker komutunu test et
docker exec gitlab-runner docker ps
```

**Beklenen Sonuç:** Container listesi görünmeli

**Neden:** Runner'ın Docker daemon'a erişebildiğini doğrulamak

#### Adım 4.4: Pipeline Test

1. GitLab'da küçük bir değişiklik yap (örn: README'ye satır ekle)
2. Commit ve push yap
3. **CI/CD > Pipelines** sekmesinde pipeline'ı kontrol et
4. `test-setup` job'unun başarılı olduğunu kontrol et

**Beklenen Sonuç:**
- ✅ Pipeline başlıyor
- ✅ Git fetch başarılı
- ✅ Job'lar çalışıyor

**Neden:** Tüm yapılandırmanın çalıştığını doğrulamak

---

## 🔧 Sorun Giderme

### Sorun 1: Runner GitLab'a Bağlanamıyor

**Belirtiler:**
- Runner "offline" görünüyor
- `gitlab-runner verify` başarısız

**Çözüm:**
1. GitLab container'ının çalıştığını kontrol et: `docker ps | grep gitlab`
2. Runner config URL'yi kontrol et: `docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep url`
3. URL IP formatında mı? (`http://172.18.0.6` veya `http://45.141.151.52:8090`)
4. GitLab IP'sini doğrula: `docker inspect gitlab | grep IPAddress`
5. Runner loglarını kontrol et: `docker logs gitlab-runner --tail 50`

### Sorun 2: Git Fetch Başarısız

**Belirtiler:**
- Pipeline başlıyor ama Git fetch hatası
- `fatal: unable to access 'http://45.141.151.52:8090/...'`

**Çözüm:**
1. Runner container'ının network mode'unu kontrol et: `docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'`
   - Beklenen: `"host"`
2. Runner config'de `network_mode = "host"` var mı?
3. Firewall kurallarını kontrol et
4. GitLab external URL'ini kontrol et: `docker exec gitlab env | grep GITLAB_OMNIBUS_CONFIG`

### Sorun 3: Docker Build Job'ları Başarısız

**Belirtiler:**
- Docker build job'ları "Cannot connect to Docker daemon" hatası veriyor

**Çözüm:**
1. Docker socket mount'unu kontrol et: `docker inspect gitlab-runner | grep docker.sock`
2. Runner config'de `privileged = true` var mı?
3. Runner container'ını restart et: `docker compose restart gitlab-runner`

---

## 📋 Özet: Başarılı Kurulum İçin Gereksinimler

### ✅ Zorunlu Gereksinimler

1. **Network Yapılandırması:**
   - Runner `network_mode: host` ile çalışmalı
   - Runner config URL IP formatında olmalı (`http://172.18.0.6` veya `http://45.141.151.52:8090`)

2. **Docker Erişimi:**
   - Docker socket mount edilmiş olmalı
   - Runner config'de `privileged = true` olmalı

3. **Runner Kaydı:**
   - Runner GitLab'a kayıtlı olmalı
   - Registration token doğru olmalı
   - Runner URL doğru olmalı (IP formatında)

4. **GitLab Erişilebilirliği:**
   - GitLab container'ı çalışıyor olmalı
   - GitLab'a erişilebilir olmalı (localhost veya external IP)

### ⚠️ Yaygın Hatalar

1. **Runner config URL'de hostname kullanmak:**
   - ❌ `url = "http://gitlab"` (host network'te çalışmaz)
   - ✅ `url = "http://172.18.0.6"` (IP kullan)

2. **Network mode uyumsuzluğu:**
   - ❌ Runner bridge network'te, build container'lar host network'te
   - ✅ Runner host network'te, build container'lar da host network'te

3. **Docker socket erişimi:**
   - ❌ Docker socket mount edilmemiş
   - ✅ Docker socket mount edilmiş: `/var/run/docker.sock:/var/run/docker.sock`

---

## 🎯 Sonraki Adımlar

1. ✅ Temizlik ve hazırlık (Faz 1)
2. ✅ Runner yapılandırması (Faz 2)
3. ✅ Runner kaydı (Faz 3)
4. ✅ Doğrulama (Faz 4)
5. ⏳ Pipeline test ve optimizasyon

---

**Son Güncelleme:** 15 Ocak 2025

