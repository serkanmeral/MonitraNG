# MngReactor — Odak Deploy Checklist (stub → gerçek servis)

**Durum:** ✅ C6 tamam (3 Haz 2026)  
**Son güncelleme:** 3 Haziran 2026  
**Sunucu:** `192.168.20.20` (odak)  
**Deploy akışı:** [../deploy/README.md](../deploy/README.md)

Odak'ta `mngreactor` **gerçek image** (`mngreactor:latest`, port 5003). Native observation publish açık; alarm bridge kapalı.

---

## 1. Ön koşullar

| # | Kontrol | Komut / beklenen |
|---|---------|------------------|
| P1 | `mng_common` ayakta | `docker network inspect mng_common_mng_network` |
| P2 | Mongo, RabbitMQ, Mosquitto erişilebilir | compose içi host adları |
| P3 | Gateway Ocelot Reactor route | `MngGateway/.../ocelot.json` → `/reactor/api/v1/{everything}` |
| P4 | Lokal repo güncel | `git pull` · `MngReactor/` dolu |
| P5 | SSH kimlik bilgisi | `.env.odak.local` veya `local-credentials.ps1` |

---

## 2. R0 — Compose stub kaldırma (tek seferlik)

**Dosya:** `ApplicationResources/mng_apps/docker-compose.odak.yml`

**Mevcut (kaldırılacak):**

```yaml
  mngreactor:
    build: !reset null
    image: alpine:3.19
    container_name: mngreactor
    command: ["sleep", "infinity"]
    ...
```

**Yerine (production.yml'den inherit + Odak port/env):**

```yaml
  mngreactor:
    ports:
      - "5003:5003"
    environment:
      - MngReactorSettings__OpenApiServerPath=http://192.168.20.20:5003
      - MngReactorSettings__MongoDB__Host=mongo
      - MngReactorSettings__MongoDB__Password=${MONGO_PASSWORD:-admin123}
      - MngReactorSettings__RabbitMQ__Host=rabbitmq
      - MngReactorSettings__RabbitMQ__Password=${RABBITMQ_PASSWORD:-admin123}
      - MngReactorSettings__Mqtt__Host=mosquitto
      # Observation publish (C6 — PR-O2 sonrası):
      - MngReactorSettings__ObservationPublish__Enabled=true
```

`build` / `image` / `healthcheck` satırları **production.yml'den gelir** — Odak override yalnızca port + env.

> **Not:** `ObservationPublish__Enabled` kodu merge edilmeden env etkisizdir; PR-O2 öncesi `false` bırakılabilir.

---

## 3. Deploy komutları (Windows geliştirme PC)

Repo kökünden, **`pwsh` zorunlu**:

### 3.1 Kaynak senkronu

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG

pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 `
  -Paths MngReactor,ApplicationResources/mng_apps
```

### 3.2 Build + konteyner

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 `
  -Services mngreactor
```

İlk deploy veya Dockerfile değiştiyse:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 `
  -Services mngreactor -NoCache
```

Gateway Reactor route kullanıyorsa (genelde gerekmez):

```powershell
# pwsh ... -Services mnggateway,mngreactor
```

---

## 4. Doğrulama checklist

| # | Test | Beklenen |
|---|------|----------|
| D1 | Container image | `docker inspect mngreactor --format '{{.Config.Image}}'` → `mngreactor:*` (alpine **değil**) |
| D2 | Health direct | `curl http://192.168.20.20:5003/api/v1/health` → 200 |
| D3 | Health gateway | `curl http://192.168.20.20:5040/reactor/api/v1/health` → 200 |
| D4 | Swagger | `http://192.168.20.20:5003/swagger` açılır |
| D5 | Metrik ingest smoke | `ApplicationResources/mng_apps/test-mngreactor-docker.ps1` veya mevcut ingest test |
| D6 | C6 diagnostic | `pwsh scripts/odak/diagnose-c6-reactor.ps1` — stub uyarısı **yok** |

### Alarm observation E2E (PR-O2 sonrası)

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\test-observation-native-e2e.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\test-metric-bridge-e2e.ps1
```

Bridge kapatma (MngAlarm Worker env):

```yaml
# docker-compose.odak.yml → mngalarm-worker veya mngalarm
- MngAlarmSettings__Engine__ReactorBridge__Enabled=false
- MngAlarmSettings__Engine__ConsumeObservations=true
```

### SIEM Faz 1 E2E (PR-5 sonrası)

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\test-siem-faz1-e2e.ps1
```

*(Script: `scripts/odak/test-siem-faz1-e2e.ps1` — S4.2–S4.6.)*

---

## 5. Rollback

Stub'a geri dönmek (acil):

```yaml
# docker-compose.odak.yml — geçici
  mngreactor:
    build: !reset null
    image: alpine:3.19
    command: ["sleep", "infinity"]
```

```powershell
pwsh ... deploy-odak-apps.ps1 -Services mngreactor
```

Gateway `/reactor/*` çağrıları 502/504 verir — bilinçli trade-off.

---

## 6. Sık hatalar

| Belirti | Olası neden | Çözüm |
|---------|-------------|--------|
| `alpine` image hâlâ | Odak override uygulanmadı | `docker-compose.odak.yml` §2 · `-NoCache` rebuild |
| Health 503/connection refused | Port 5003 publish yok | Odak `ports: 5003:5003` ekle |
| Mongo auth fail | `.env` parola uyumsuz | `MONGO_PASSWORD` Odak `.env` ile hizala |
| MQTT connect fail | Mosquitto cred | `MngReactorSettings__Mqtt__*` kontrol |
| Gateway 404 | Ocelot route | `ocelot.json` Reactor downstream `mngreactor:5003` |
| Observation E2E skip | Stub veya flag kapalı | D1 + `ObservationPublish__Enabled=true` |

---

## 7. R0 sertleştirme backlog (deploy sonrası)

| # | Madde | PR |
|---|-------|-----|
| R0.1 | JWT imza doğrulama (Keeper JWKS) | reactor hardening |
| R0.2 | `tempkey.jwk` git'ten çıkar + `.gitignore` | repo hygiene |
| R0.3 | Health: Mongo + RabbitMQ probe | ops |
| R0.4 | ARCHITECTURE_GUIDE hayalet LDAP bölümü | docs |

---

## 8. İlgili dokümanlar

- [MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md](./MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md)
- [REACTOR_NATIVE_PUBLISH_HANDOFF.md](../alarm/REACTOR_NATIVE_PUBLISH_HANDOFF.md)
- [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md)
- [../deploy/README.md](../deploy/README.md)
