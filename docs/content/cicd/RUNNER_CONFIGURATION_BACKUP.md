# GitLab Runner Yapılandırma Backup - Hızlı Referans

**Tarih:** 15 Ocak 2025  
**Durum:** ✅ Başarılı - Pipeline çalışıyor

---

## 🎯 Hızlı Kontrol Komutları

### Runner Durumu
```bash
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify && docker exec gitlab-runner gitlab-runner list"
```

### Network Mode
```bash
ssh root@monitrang-server "docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'"
# Beklenen: "host"
```

### Runner Config
```bash
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E 'url|network_mode|privileged'"
# Beklenen:
# url = "http://45.141.151.52:8090"
# network_mode = "host"
# privileged = true
```

---

## 🔧 Hızlı Düzeltme Komutları

### Config URL Düzeltme
```bash
ssh root@monitrang-server "docker cp gitlab-runner:/etc/gitlab-runner/config.toml /tmp/config.toml && sed -i 's|http://gitlab|http://45.141.151.52:8090|g' /tmp/config.toml && docker cp /tmp/config.toml gitlab-runner:/etc/gitlab-runner/config.toml && cd /root/MonitraNG/ApplicationResources/mng_common && docker compose restart gitlab-runner"
```

### Network Mode Düzeltme
```bash
ssh root@monitrang-server "docker cp gitlab-runner:/etc/gitlab-runner/config.toml /tmp/config.toml && sed -i 's|mng_common_mng_network|host|g' /tmp/config.toml && docker cp /tmp/config.toml gitlab-runner:/etc/gitlab-runner/config.toml && cd /root/MonitraNG/ApplicationResources/mng_common && docker compose restart gitlab-runner"
```

---

## 📋 Kritik Yapılandırma Değerleri

### Docker Compose
- `network_mode: host` ✅
- Docker socket mount ✅
- Config volume ✅

### Runner Config
- `url = "http://45.141.151.52:8090"` ✅
- `network_mode = "host"` ✅
- `privileged = true` ✅

### GitLab
- `external_url 'http://45.141.151.52:8090'` ✅
- Port: `8090:80` ✅
- Pages ports: KALDIRILDI ✅

---

**Detaylı Rehber:** `SUCCESSFUL_RUNNER_CONFIGURATION.md`

