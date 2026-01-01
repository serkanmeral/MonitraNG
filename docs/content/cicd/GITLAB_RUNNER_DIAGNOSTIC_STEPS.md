# GitLab Runner Durum Kontrolü - Adım Adım Rehber

**Tarih:** 15 Ocak 2025  
**Amaç:** Mevcut runner yapılandırmasını kontrol etmek ve sorunları tespit etmek

---

## 🚀 Hızlı Kontrol (Script ile)

### Adım 1: Script'i Sunucuya Kopyala

```bash
# Windows'tan sunucuya script'i kopyala (PowerShell)
scp scripts/check-gitlab-runner-status.sh root@45.141.151.52:/root/
```

### Adım 2: Script'i Çalıştır

```bash
# Sunucuda SSH ile bağlan
ssh root@45.141.151.52

# Script'i çalıştırılabilir yap
chmod +x /root/check-gitlab-runner-status.sh

# Script'i çalıştır
/root/check-gitlab-runner-status.sh
```

**Çıktı:** Tüm yapılandırma detayları ve sorunlar listelenecek

---

## 🔍 Manuel Kontrol (Adım Adım)

Eğer script çalışmazsa veya daha detaylı kontrol istiyorsanız:

### 1. Runner Container Durumu

```bash
# Sunucuda
docker ps | grep gitlab-runner
```

**Beklenen:** Runner container çalışıyor olmalı

**Sorun varsa:**
```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose up -d gitlab-runner
```

---

### 2. Network Mode Kontrolü

```bash
# Runner container'ının network mode'unu kontrol et
docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'
```

**Beklenen:** `host`

**Sorun varsa:**
- `docker-compose.yml` dosyasında `network_mode: host` olmalı
- Runner container'ını restart et: `docker compose restart gitlab-runner`

---

### 3. Docker Socket Mount Kontrolü

```bash
# Docker socket mount'unu kontrol et
docker inspect gitlab-runner --format '{{range .Mounts}}{{.Source}} -> {{.Destination}}{{println}}{{end}}' | grep docker.sock
```

**Beklenen:** `/var/run/docker.sock -> /var/run/docker.sock`

**Sorun varsa:**
- `docker-compose.yml` dosyasında volumes'a `/var/run/docker.sock:/var/run/docker.sock` ekle
- Runner container'ını restart et

---

### 4. GitLab Container IP

```bash
# GitLab container'ının IP'sini bul
docker inspect gitlab | grep -A 1 IPAddress
# veya daha temiz:
docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab
```

**Beklenen:** `172.18.0.6` gibi bir IP (bridge network'te)

**Not:** Bu IP'yi runner config URL'sinde kullanacağız

---

### 5. GitLab External URL

```bash
# GitLab external URL'ini kontrol et
docker exec gitlab env | grep GITLAB_OMNIBUS_CONFIG | grep external_url
```

**Beklenen:** `external_url 'http://45.141.151.52:8090'`

---

### 6. Runner Config Kontrolü

```bash
# Runner config dosyasını oku
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml
```

**Kontrol Edilecekler:**

#### a) URL Kontrolü

```bash
# URL'yi çıkar
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E "^[[:space:]]*url[[:space:]]*="
```

**Beklenen:**
- ✅ `url = "http://172.18.0.6"` (GitLab container IP)
- ✅ `url = "http://45.141.151.52:8090"` (external IP)
- ❌ `url = "http://gitlab"` (host network'te çalışmaz!)

**Sorun varsa:** Runner'ı yeniden kaydet (IP ile)

#### b) Network Mode Kontrolü

```bash
# Network mode'u çıkar
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep network_mode
```

**Beklenen:** `network_mode = "host"`

**Sorun varsa:** Runner'ı yeniden kaydet (`--docker-network-mode "host"` ile)

#### c) Privileged Mode Kontrolü

```bash
# Privileged mode'u çıkar
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep privileged
```

**Beklenen:** `privileged = true`

**Sorun varsa:** Runner'ı yeniden kaydet (`--docker-privileged` ile)

---

### 7. Runner Verify

```bash
# Runner'ın GitLab'a bağlanabildiğini kontrol et
docker exec gitlab-runner gitlab-runner verify
```

**Beklenen:**
```
Verifying runner... is alive                        runner=...
```

**Sorun varsa:**
- Runner config URL'sini kontrol et (IP formatında olmalı)
- GitLab container'ının çalıştığını kontrol et
- Runner loglarını kontrol et: `docker logs gitlab-runner --tail 50`

---

### 8. Runner List

```bash
# Kayıtlı runner'ları listele
docker exec gitlab-runner gitlab-runner list
```

**Beklenen:** Runner listelenmeli ve "active" olmalı

---

### 9. Docker Erişim Testi

```bash
# Runner'ın Docker daemon'a erişebildiğini test et
docker exec gitlab-runner docker ps
```

**Beklenen:** Container listesi görünmeli

**Sorun varsa:**
- Docker socket mount'unu kontrol et
- Runner container'ını restart et

---

### 10. GitLab Erişim Testi

```bash
# Runner'ın GitLab'a erişebildiğini test et
# Önce GitLab IP'sini al
GITLAB_IP=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab)

# Erişim testi
docker exec gitlab-runner curl -I "http://${GITLAB_IP}"
```

**Beklenen:** HTTP 200 veya 302 (redirect)

**Not:** Host network kullanıyorsak, external IP'yi de test edebiliriz:
```bash
docker exec gitlab-runner curl -I "http://45.141.151.52:8090"
```

---

## 📊 Sorun Tespiti ve Çözüm

### Sorun 1: Runner Container Çalışmıyor

**Belirtiler:**
- `docker ps | grep gitlab-runner` boş döner

**Çözüm:**
```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose up -d gitlab-runner
docker ps | grep gitlab-runner
```

---

### Sorun 2: Network Mode Host Değil

**Belirtiler:**
- `docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'` → `bridge` veya başka bir değer

**Çözüm:**
1. `docker-compose.yml` dosyasını kontrol et:
   ```yaml
   gitlab-runner:
     network_mode: host  # Bu satır olmalı
   ```
2. Runner container'ını restart et:
   ```bash
   docker compose restart gitlab-runner
   ```

---

### Sorun 3: Runner Config URL Hostname İçeriyor

**Belirtiler:**
- Config'de `url = "http://gitlab"` var
- Host network kullanılıyor

**Çözüm:**
Runner'ı yeniden kaydet (IP ile):
```bash
# GitLab IP'sini al
GITLAB_IP=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab)

# GitLab'dan registration token al (UI'dan)

# Runner'ı kaydet
docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url "http://${GITLAB_IP}" \
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

---

### Sorun 4: Runner Verify Başarısız

**Belirtiler:**
- `gitlab-runner verify` hata veriyor
- Runner "offline" görünüyor

**Çözüm:**
1. Runner config URL'sini kontrol et (IP formatında olmalı)
2. GitLab container'ının çalıştığını kontrol et
3. Runner loglarını kontrol et: `docker logs gitlab-runner --tail 50`
4. Gerekirse runner'ı yeniden kaydet

---

### Sorun 5: Docker Erişim Sorunu

**Belirtiler:**
- `docker exec gitlab-runner docker ps` hata veriyor

**Çözüm:**
1. Docker socket mount'unu kontrol et
2. Runner container'ını restart et
3. Runner config'de `privileged = true` olmalı

---

## 📋 Kontrol Listesi

Kontrol edilmesi gerekenler:

- [ ] Runner container çalışıyor
- [ ] Network mode: `host`
- [ ] Docker socket mount edilmiş
- [ ] GitLab container IP bulundu
- [ ] Runner config mevcut
- [ ] Runner config URL IP formatında
- [ ] Runner config network_mode: `host`
- [ ] Runner config privileged: `true`
- [ ] Runner verify başarılı
- [ ] Docker erişim testi başarılı
- [ ] GitLab erişim testi başarılı

---

## 🎯 Sonraki Adımlar

Kontrol tamamlandıktan sonra:

1. **Sorunlar varsa:** Yukarıdaki çözümleri uygula
2. **Sorun yoksa:** Pipeline test et
3. **Detaylı rehber:** `docs/content/cicd/GITLAB_RUNNER_FUNDAMENTALS.md`

---

**Son Güncelleme:** 15 Ocak 2025

