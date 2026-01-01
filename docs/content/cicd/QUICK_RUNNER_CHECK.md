# GitLab Runner Hızlı Kontrol - Adım Adım

**Tarih:** 15 Ocak 2025  
**Sunucu:** 45.141.151.52

---

## 🚀 Hızlı Kontrol Komutları

Sunucuya SSH ile bağlandıktan sonra şu komutları sırayla çalıştırın:

### 1. Runner Container Durumu

```bash
docker ps | grep gitlab-runner
```

**Beklenen:** Runner container çalışıyor olmalı

---

### 2. Network Mode Kontrolü

```bash
docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'
```

**Beklenen:** `host`

**Sorun varsa:** `docker-compose.yml`'de `network_mode: host` olmalı

---

### 3. GitLab Container IP

```bash
docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab
```

**Beklenen:** `172.18.0.6` gibi bir IP

**Not:** Bu IP'yi runner config'de kullanacağız

---

### 4. Runner Config URL (EN ÖNEMLİSİ)

```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E "^[[:space:]]*url[[:space:]]*="
```

**Beklenen:**
- ✅ `url = "http://172.18.0.6"` (GitLab container IP)
- ✅ `url = "http://45.141.151.52:8090"` (external IP)
- ❌ `url = "http://gitlab"` (host network'te çalışmaz!)

**Sorun varsa:** Runner'ı yeniden kaydetmek gerekir

---

### 5. Runner Config Network Mode

```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep network_mode
```

**Beklenen:** `network_mode = "host"`

**Sorun varsa:** Runner'ı yeniden kaydetmek gerekir

---

### 6. Runner Config Privileged

```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep privileged
```

**Beklenen:** `privileged = true`

**Sorun varsa:** Runner'ı yeniden kaydetmek gerekir

---

### 7. Runner Verify

```bash
docker exec gitlab-runner gitlab-runner verify
```

**Beklenen:**
```
Verifying runner... is alive                        runner=...
```

**Sorun varsa:**
- Runner config URL'sini kontrol et
- GitLab container'ının çalıştığını kontrol et

---

### 8. Runner List

```bash
docker exec gitlab-runner gitlab-runner list
```

**Beklenen:** Runner listelenmeli ve "active" olmalı

---

### 9. Docker Erişim Testi

```bash
docker exec gitlab-runner docker ps
```

**Beklenen:** Container listesi görünmeli

**Sorun varsa:** Docker socket mount'unu kontrol et

---

### 10. GitLab Erişim Testi

```bash
# Önce GitLab IP'sini al
GITLAB_IP=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab)
echo "GitLab IP: $GITLAB_IP"

# Erişim testi
docker exec gitlab-runner curl -I "http://${GITLAB_IP}"
```

**Beklenen:** HTTP 200 veya 302 (redirect)

---

## 📊 Sonuçları Paylaşma

Bu komutların çıktılarını paylaşın, özellikle:

1. **Network Mode:** `host` olmalı
2. **Runner Config URL:** IP formatında olmalı
3. **Runner Verify:** Başarılı olmalı

---

## 🔧 Sorun Tespiti

### Sorun 1: Network Mode "host" Değil

**Çözüm:**
```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose restart gitlab-runner
```

---

### Sorun 2: Runner Config URL Hostname İçeriyor

**Çözüm:** Runner'ı yeniden kaydet (aşağıdaki bölüme bakın)

---

### Sorun 3: Runner Verify Başarısız

**Çözüm:** Runner config URL'sini kontrol et ve gerekirse yeniden kaydet

---

## 🔄 Runner'ı Yeniden Kaydetme

Eğer runner config'de sorun varsa, runner'ı yeniden kaydedin:

### Adım 1: GitLab'dan Registration Token Al

1. GitLab UI'ya git: `http://45.141.151.52:8090`
2. Proje: `http://45.141.151.52:8090/root/MonitraNG`
3. **Settings > CI/CD > Runners**
4. **"Set up a specific runner manually"** bölümünden token'ı kopyala

### Adım 2: GitLab IP'sini Al

```bash
GITLAB_IP=$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab)
echo "GitLab IP: $GITLAB_IP"
```

### Adım 3: Runner'ı Kaydet

```bash
# Token'ı değişkene al (YOUR_TOKEN_HERE yerine gerçek token'ı yazın)
REGISTRATION_TOKEN="YOUR_TOKEN_HERE"

# Runner'ı kaydet
docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url "http://${GITLAB_IP}" \
  --registration-token "${REGISTRATION_TOKEN}" \
  --executor "docker" \
  --docker-image "mcr.microsoft.com/dotnet/sdk:9.0" \
  --description "monitrang-runner" \
  --tag-list "docker" \
  --run-untagged="true" \
  --locked="false" \
  --docker-privileged="true" \
  --docker-network-mode="host"
```

### Adım 4: Doğrula

```bash
docker exec gitlab-runner gitlab-runner verify
docker exec gitlab-runner gitlab-runner list
```

---

**Son Güncelleme:** 15 Ocak 2025

