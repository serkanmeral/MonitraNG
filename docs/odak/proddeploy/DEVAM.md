# DEVAM — Production deploy (Kaldığımız yer)

**Son güncelleme:** 4 Haziran 2026  
**Durum:** ⏸️ **mng_common compose up bekliyor** — sunucu dosyaları hazır; **Docker + sudo** IT’de

> **İlke:** Test (`192.168.20.20`) ile production (`192.168.20.8`) tamamen bağımsız → [INDEPENDENCE.md](./INDEPENDENCE.md)  
> **Yeni chat / IT sonrası:** Bu dosyadan devam edin.

---

## 1. Tek cümlede durum

Production VM’de **`mng_common` dosyaları ve `.env` hazır**; altyapı konteynerleri (Mongo, Keycloak, Redis, …) **başlatılamadı** çünkü `odak` kullanıcısında **Docker yok** ve **sudo yetkisi yok**. IT tamamlayınca sıradaki komut: `setup-mng-common-odak-prod.ps1`.

---

## 2. Faz özeti

| Faz | Konu | Durum |
|-----|------|--------|
| P0 | Dokümantasyon + prod şablonları (`docker-compose.odak.prod.yml`, `.env.odak.prod.example`) | ✅ |
| P0 | Yerel SSH `.env.odak.prod.local` (gitignore) | ✅ |
| P0 | Scriptler (`sync-mng-common-prod`, `deploy-odak-prod`, `bootstrap-odak-prod`, …) | ✅ |
| **P1** | **Production `mng_common` dosya senkronu** | ✅ |
| **P1** | **Production `mng_common/.env` (prod şablon)** | ✅ |
| **P1** | **`mng_common compose up` (Mongo, Keycloak, …)** | ⏸️ Docker engeli |
| P2 | Keycloak realm / `mng-keeper-admin` client + production secret’lar | ⏳ P1 sonrası |
| P3 | `mng_apps` tam senkron + ilk build/deploy | ⏳ P1 sonrası |
| P4 | Domain oluşturma + initial data (bilinçli karar) | ⏳ P3 sonrası |

---

## 3. Sunucu anlık görüntü (`192.168.20.8`)

| Öğe | Durum |
|-----|--------|
| Hostname | `monitrang-prod` |
| OS | Debian 13 (trixie) |
| SSH `odak@192.168.20.8` | ✅ |
| Docker | ❌ `command not found` |
| `odak` → sudo | ❌ `not in sudoers file` |
| `/home/odak/mng_common` | ✅ `docker-compose.yml`, `docker-compose.odak.prod.yml`, `.env`, `mongo-init`, `mosquitto`, … |
| `mng_common_mng_network` | ❌ (compose up yapılmadı) |
| `/home/odak/MonitraNG` | ⚠️ Kısmi (önceki sync; tam liste için `sync-odak-prod.ps1 -Full` P3’te) |
| `mng_apps/.env` | ✅ (prod şablonundan; secret’lar `CHANGE_ME` olabilir) |

Canlı kontrol: `pwsh -File scripts/odak/probe-mng-common-prod.ps1`

---

## 4. Bu oturumda yapılanlar (4 Haziran 2026)

1. Production deploy planı ve **tam bağımsızlık** dokümantasyonu (`INDEPENDENCE.md`, …).
2. Production IP şablonları ve `docker-compose.odak.prod.yml` (mng_common + mng_apps).
3. `sync-mng-common-prod.ps1` ile **yalnızca** production `mng_common` senkronu.
4. `bootstrap-odak-prod.ps1` — `mng_common/.env` ← `.env.odak.prod.example`.
5. `setup-mng-common-odak-prod.ps1` denendi → **Docker yok** hatası.
6. `setup-docker-odak-prod.ps1` denendi → **sudo yok** hatası.
7. `sync-odak-source.ps1` CRLF/bash düzeltmesi; `-MngCommonOnly` eklendi.

**Test sunucuya (`192.168.20.20`) dokunulmadı.**

---

## 5. IT’den istenen (deploy öncesi zorunlu)

`192.168.20.8` üzerinde kullanıcı **`odak`** için:

1. **sudo** (sudoers) veya root ile eşdeğer kurulum yetkisi  
2. **Docker Engine** + **Compose plugin** ([../setup/KURULUM.md](../setup/KURULUM.md) Faz 3)  
3. `usermod -aG docker odak` + SSH oturumu yenileme  
4. Geliştirme ağından **22** ve uygulama portları (8080, 27017, 5040, 3000, …)

IT’ye iletilecek kısa not: *“MonitraNG production kurulumu için VM’de Docker gerekli; `odak` sudo + docker grubunda olmalı.”*

---

## 6. IT hazır olunca — sıradaki komutlar (sırayla)

**Geliştirme PC** (repo kökü, PowerShell 7):

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG

# 0) Durum kontrolü
pwsh -File .\scripts\odak\probe-mng-common-prod.ps1

# 1) Docker yoksa ve odak sudo'daysa (IT kurmadıysa)
pwsh -File .\scripts\odak\setup-docker-odak-prod.ps1

# 2) mng_common dosyaları (genelde atlanır — zaten senkron)
# pwsh -File .\scripts\odak\sync-mng-common-prod.ps1

# 3) Altyapıyı ayağa kaldır (Mongo, Keycloak, Redis, RabbitMQ, MinIO, …)
pwsh -File .\scripts\odak\setup-mng-common-odak-prod.ps1
```

**Beklenen:** `mng_common_mng_network` oluşur; `docker compose ps` servisleri `running`; Keycloak `http://192.168.20.8:8080/keycloak/` → HTTP 200.

### P1 sonrası (aynı oturum veya yeni)

```powershell
# 4) Keycloak: monitra realm + mng-keeper-admin client
#    Production sunucuda YENİ secret — test secret KOPYALANMAZ
#    Bkz. ../domain/DOMAIN_OLUSTURMA_KAYIT.md (URL: 192.168.20.8)

# 5) Sunucuda mng_apps/.env secret'ları doldur
#    KEYCLOAK_CLIENT_SECRET, MNGKEEPER_LICENSE_MASTER_KEY

# 6) Uygulama kaynağı + ilk deploy (uzun)
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -Full
pwsh -File .\scripts\odak\deploy-odak-prod.ps1

# 7) Doğrulama
# http://192.168.20.8:5040/health
# http://192.168.20.8:3000/
```

---

## 7. Agent / yeni chat talimatı

Kullanıcı *“production deploy’a devam”* dediğinde:

1. Bu dosyayı ve [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md) oku.  
2. `probe-mng-common-prod.ps1` çalıştır.  
3. Docker ✅ ise → `setup-mng-common-odak-prod.ps1` (P1 bitmemişse).  
4. `mng_common` ✅ ise → P2–P7 ([AGENT_PRODUCTION_DEPLOY.md](./AGENT_PRODUCTION_DEPLOY.md)).  
5. Test sunucu (`20.20`) scriptlerini **varsayılan olarak kullanma**.

---

## 8. İlgili dosyalar

| Dosya | Rol |
|-------|-----|
| [README.md](./README.md) | proddeploy indeks |
| [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md) | Sunucu kontrol tablosu |
| [INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md) | İlk kurulum checklist |
| [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md) | Günlük prod deploy komutları |
| [AGENT_PRODUCTION_DEPLOY.md](./AGENT_PRODUCTION_DEPLOY.md) | Agent kuralları |
| `scripts/odak/sync-mng-common-prod.ps1` | mng_common senkron |
| `scripts/odak/setup-mng-common-odak-prod.ps1` | **Sıradaki adım** |
| `scripts/odak/deploy-odak-prod.ps1` | mng_apps deploy |

---

## 9. Bilinen riskler / notlar

- Production `.env` içinde `KEYCLOAK_CLIENT_SECRET=CHANGE_ME_...` olabilir — P2’de doldurulmalı.  
- `MonitraNG` tam senkron P3 öncesi `sync-odak-prod.ps1 -Full` ile tazelenmeli.  
- Test ortamından veri/secret kopyası **yasak** ([INDEPENDENCE.md](./INDEPENDENCE.md)).
