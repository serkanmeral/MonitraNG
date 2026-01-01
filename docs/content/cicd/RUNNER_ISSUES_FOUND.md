# GitLab Runner Sorunları - Tespit Edilen Sorunlar

**Tarih:** 15 Ocak 2025  
**Sunucu:** 45.141.151.52 (monitrang-server)

---

## ✅ Çalışan Kısımlar

1. ✅ Runner container çalışıyor
2. ✅ GitLab IP bulundu: `172.18.0.6`
3. ✅ Privileged mode aktif: `true`
4. ✅ Runner verify başarılı (GitLab'a bağlanabiliyor)

---

## ❌ Tespit Edilen Sorunlar

### Sorun 1: Network Mode Yanlış

**Mevcut Durum:**
- Container Network Mode: `mng_common_mng_network` (bridge network)
- Runner Config Network Mode: `mng_common_mng_network` (bridge network)

**Olması Gereken:**
- Container Network Mode: `host`
- Runner Config Network Mode: `host`

**Neden Sorun:**
- Build container'ları external IP'ye (`45.141.151.52:8090`) erişemiyor
- Git fetch başarısız oluyor

---

### Sorun 2: Runner Config URL Hostname Formatında

**Mevcut Durum:**
```
url = "http://gitlab:80"
```

**Olması Gereken:**
```
url = "http://172.18.0.6"
```
veya
```
url = "http://45.141.151.52:8090"
```

**Neden Sorun:**
- Host network kullanıldığında hostname (`gitlab`) çözümlenemez
- IP kullanmak gerekir

---

## 🔧 Çözüm Adımları

### Adım 1: docker-compose.yml Kontrolü

Runner container'ının `network_mode: host` ile çalıştığından emin olun:

```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:latest
  container_name: gitlab-runner
  network_mode: host  # ← Bu satır olmalı
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
    - gitlab_runner_config:/etc/gitlab-runner
```

**Kontrol:**
```bash
ssh root@monitrang-server "grep -A 5 'gitlab-runner:' /root/MonitraNG/ApplicationResources/mng_common/docker-compose.yml | grep network_mode"
```

---

### Adım 2: Runner Container'ını Host Network'te Çalıştır

Eğer docker-compose.yml'de `network_mode: host` yoksa ekleyin ve restart edin:

```bash
ssh root@monitrang-server "cd /root/MonitraNG/ApplicationResources/mng_common && docker compose stop gitlab-runner && docker compose rm -f gitlab-runner && docker compose up -d gitlab-runner"
```

---

### Adım 3: Runner'ı Yeniden Kaydet (IP ile)

**3.1. GitLab'dan Registration Token Al**

1. GitLab UI: `http://45.141.151.52:8090`
2. Proje: `http://45.141.151.52:8090/root/MonitraNG`
3. **Settings > CI/CD > Runners**
4. **"Set up a specific runner manually"** bölümünden token'ı kopyala

**3.2. Runner'ı Kaydet**

```bash
# GitLab IP'sini al
GITLAB_IP="172.18.0.6"

# Runner'ı kaydet (YOUR_TOKEN_HERE yerine gerçek token'ı yazın)
ssh root@monitrang-server "docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url \"http://${GITLAB_IP}\" \
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

**Not:** Token'ı GitLab UI'dan almanız gerekiyor.

---

### Adım 4: Doğrulama

```bash
# Network mode kontrolü
ssh root@monitrang-server "docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'"
# Beklenen: host

# Runner config URL kontrolü
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E '^[[:space:]]*url[[:space:]]*='"
# Beklenen: url = "http://172.18.0.6"

# Runner config network mode kontrolü
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep network_mode"
# Beklenen: network_mode = "host"

# Runner verify
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify"
# Beklenen: Verifying runner... is alive
```

---

## 📊 Özet

**Tespit Edilen Sorunlar:**
1. ❌ Container network mode: bridge (host olmalı)
2. ❌ Runner config URL: hostname (IP olmalı)
3. ❌ Runner config network mode: bridge (host olmalı)

**Çözüm:**
1. docker-compose.yml'de `network_mode: host` olduğundan emin ol
2. Runner container'ını restart et
3. Runner'ı IP ile yeniden kaydet

---

**Son Güncelleme:** 15 Ocak 2025

