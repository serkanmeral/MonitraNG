# Odak — Tam kurulum ve çalışma rehberi

**Amaç:** Bu doküman, Odak POC sunucusu (`192.168.20.20`) ve yerel geliştirme ortamının kurulum oturumunda yapılanların **tek başlangıç noktası**dır. Yeni bir chat’te ürün geliştirmesine geçmeden önce burayı okuyun; ayrıntılar alt dokümanlarda.

**Son güncelleme:** 25 Mayıs 2026  
**Durum:** Sunucu kurulumu tamamlandı · LDAP K1–K5 + G1 POC tamamlandı (**duraklatıldı**) · **mngui** deploy ✅ · HTTPS opsiyonel

---

## 1. Ortam özeti

| Rol | Adres / konum | Not |
|-----|----------------|-----|
| **Odak sunucu** | `192.168.20.20` (hostname: `monitrang`) | Debian 13, 8 CPU, 15 GiB RAM, Docker 29.x |
| **SSH** | `odak@192.168.20.20:22` | Root SSH kapalı; `sudo` / `docker` grubu |
| **Altyapı dizini** | `/home/odak/mng_common` | mng_common compose |
| **Uygulama kaynağı** | `/home/odak/MonitraNG` | sync ile; sunucuda git zorunlu değil |
| **Geliştirme PC** | MonitraNG workspace | UI: `Mng.Ui` → `npm run dev` (localhost:3000) |

**Parolalar ve portlar:** Repoya yazılmaz; [setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) ve [setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md](./setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md).

---

## 2. Kurulum fazları (tamamlanan)

| # | Faz | Durum | Detay doküman |
|---|-----|--------|----------------|
| 1 | Sunucu analizi + Docker | ✅ | [setup/KURULUM.md](./setup/KURULUM.md) |
| 2 | DNS düzeltmesi (`/etc/resolv.conf.tail`) | ✅ | KURULUM.md |
| 3 | mng_common (12 servis, Odak override) | ✅ | [setup/MNG_COMMON_ODAK.md](./setup/MNG_COMMON_ODAK.md) |
| 4 | Keycloak Admin UI (port 8080, `KC_PROXY`) | ✅ | MNG_COMMON_ODAK.md |
| 5 | mng_apps deploy (build + compose up) | ✅ | [setup/MNG_APPS_ODAK.md](./setup/MNG_APPS_ODAK.md), [setup/MNG_APPS_ODAK_DEPLOY.md](./setup/MNG_APPS_ODAK_DEPLOY.md) |
| 6 | Keycloak `mng-keeper-admin` + `.env` secret | ✅ | [domain/DOMAIN_OLUSTURMA_KAYIT.md](./domain/DOMAIN_OLUSTURMA_KAYIT.md) |
| 7 | Domain oluşturma (UI: `odak`) | ✅ | [domain/DOMAIN_OLUSTURMA.md](./domain/DOMAIN_OLUSTURMA.md) |
| 8 | Initial data şablonu `initial_data` import | ✅ | Bu doküman §6 |
| 9 | Mng.Ui cookie/token düzeltmesi (HTTP Odak) | ✅ | Kod: `Mng.Ui/utils/tokenUtils.ts`, `stores/auth.ts` |
| 10 | Yerel geliştirme (`npm run dev`) | ✅ | Bu doküman §7 |

---

## 3. Hızlı erişim (sunucu)

| Servis | URL |
|--------|-----|
| **MngUI** | http://192.168.20.20:3000 |
| **MngDomainUI** | http://192.168.20.20:3001/domain/ |
| **API Gateway** | http://192.168.20.20:5040 |
| **MngKeeper** | http://192.168.20.20:5001 |
| **MngScheduler** | http://192.168.20.20:5090 |
| **Keycloak Admin** | http://192.168.20.20:8080/keycloak/admin/master/console/ |
| MinIO Console | http://192.168.20.20:9091 |
| Mongo Express | http://192.168.20.20:8081 |

**Domain UI girişi:** Keycloak **master** realm, admin kullanıcısı (altyapı parolası).  
**Ana UI girişi:** Domain realm kullanıcısı (ör. `odak` domain admin).

---

## 4. Sunucu — önemli dizinler ve compose

### 4.1 mng_common

```bash
cd /home/odak/mng_common
docker compose -f docker-compose.yml -f docker-compose.odak.yml --env-file .env up -d
```

- Override: `ApplicationResources/mng_common/docker-compose.odak.yml` (GitLab, Nginx, MkDocs **kapalı**).
- Ağ: `mng_common_mng_network` (mng_apps bu ağa bağlanır).

### 4.2 mng_apps

```bash
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d
```

**Zorunlu `.env` alanları (ilk kurulum):**

- `KEYCLOAK_CLIENT_SECRET` — master realm `mng-keeper-admin` client secret
- `MNGKEEPER_LICENSE_MASTER_KEY` — en az 32 karakter

**Keycloak URL (Keeper):**

- `KEYCLOAK_BASE_URL=http://keycloak:8080` (path **yok**)
- `KEYCLOAK_PATH_PREFIX=/keycloak`

`.env` değişince `docker restart` **yeterli değil**; ilgili servisi compose ile **recreate** edin:

```bash
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d --no-deps mngkeeper
```

### 4.3 Odak’ta kapalı / stub servisler

| Servis | Neden |
|--------|--------|
| mngreactor | Repoda Dockerfile/kaynak yok |
| mngllm / Ollama | RAM; mngui için nginx stub |
| Nginx (mng_common) | Host portları doğrudan açık |

---

## 5. Deploy stratejisi (PC → sunucu)

**Git push sunucu deploy’una bağlı değildir.**

| Script | Ne yapar |
|--------|----------|
| `scripts/odak/sync-odak-source.ps1` | Workspace → `/home/odak/MonitraNG` (tar/scp) |
| `scripts/odak/deploy-odak-apps.ps1` | Sunucuda `docker compose build` + `up` |
| `scripts/odak/import-template-to-odak.ps1` | `initial_data.json` → MinIO + Mongo `templates` |

**İlk tam kurulum (PC, repo kökü):**

```powershell
.\scripts\odak\sync-odak-source.ps1 -Full
# Sunucuda bir kez: cp .env.odak.example .env && secret'ları doldur
.\scripts\odak\deploy-odak-apps.ps1 -FullBuild
```

**Tek servis güncelleme:**

```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui,MngKeeper
.\scripts\odak\deploy-odak-apps.ps1 -Services mngui,mngkeeper
```

Ayrıntı: [setup/MNG_APPS_ODAK_DEPLOY.md](./setup/MNG_APPS_ODAK_DEPLOY.md).

---

## 6. Domain ve initial data

### 6.1 Domain oluşturma

- **UI:** [domain/DOMAIN_OLUSTURMA.md](./domain/DOMAIN_OLUSTURMA.md) — MngDomainUI adımları.
- **API:** [domain/DOMAIN_OLUSTURMA_API.md](./domain/DOMAIN_OLUSTURMA_API.md).
- **Oturum kaydı / hatalar:** [domain/DOMAIN_OLUSTURMA_KAYIT.md](./domain/DOMAIN_OLUSTURMA_KAYIT.md).

**Ön koşul:** `mng-keeper-admin` client + `KEYCLOAK_CLIENT_SECRET` + `mngkeeper` recreate.

**Doğrulanan domain:** `odak` (UI ile oluşturuldu; initial data şablonu ile test edildi).

### 6.2 Initial data (şablon)

Şablon sistemi: meta → Mongo `mngkeeper.templates`; içerik → MinIO `system/System/templates/{ad}.json`.

| Yol | Açıklama |
|-----|----------|
| **Önerilen** | Kaynak domain’den DomainUI → Templates veya `POST /api/templates` |
| **Taşıma** | Kaynak ortamdan export JSON → `import-template-to-odak.ps1` |
| **Legacy** | `scripts/tests/MngKeeper/template/prepare-template.ps1` → `mng_templates` DB |

**Odak’ta import edilen şablon:** `initial_data` (43 koleksiyon; kaynak: `docs/odak/domain/initial_data.json` — büyük dosya, commit dikkatli).

Yeni domain oluştururken formda **Initial Data Template** = `initial_data`.

Domain indeksi: [domain/README.md](./domain/README.md).

---

## 7. Yerel geliştirme (Mng.Ui)

**Prensip:** Günlük UI geliştirmesi **Docker deploy değil**, `npm run dev` ile yapılır. Sunucuya deploy siz uygun gördüğünüzde.

### 7.1 Kurulum (bir kez)

```powershell
cd Mng.Ui
npm install          # Nuxt 3.13.x projeye gelir; global Nuxt kurulumu gerekmez
copy .env.example .env
```

**Node:** 18+ (geliştirme makinede 22 kullanıldı).

### 7.2 `.env` — Odak backend’e bağlanma

```env
GATEWAY_URL=http://192.168.20.20:5040
HUB_URL=http://192.168.20.20:5020
```

İstekler Nuxt dev server proxy (`server/api/*`) üzerinden gider.

### 7.3 Çalıştırma

```powershell
npm run dev
```

Tarayıcı: http://localhost:3000

**Production build (sunucu):** `npm run generate` + nginx image (`Mng.Ui/Dockerfile`) — deploy script ile.

### 7.4 UI token / cookie (HTTP)

Odak sunucu UI **HTTP** kullanır. Eski production build’de `Secure` cookie yüzünden “Access token bulunamadı” oluşuyordu.

**Düzeltme (repo’da):** `shouldUseSecureCookie()` — yalnızca HTTPS; `getAccessToken()` Pinia fallback.

Sunucuda eski `mngui` image varsa `Mng.Ui` sync + `deploy-odak-apps.ps1 -Services mngui` gerekir.

---

## 8. Bilinen sorunlar ve çözümler (özet)

| Belirti | Çözüm | Detay |
|--------|--------|--------|
| Domain pipeline `invalid_client` | `mng-keeper-admin` + secret + **compose recreate** mngkeeper | [DOMAIN_OLUSTURMA_KAYIT.md](./domain/DOMAIN_OLUSTURMA_KAYIT.md) |
| Keycloak admin UI yanlış port | `KC_HOSTNAME_PORT=8080`, `KC_PROXY=passthrough` | MNG_COMMON_ODAK.md |
| UI “Access token bulunamadı” | Cookie Secure + `getAccessToken` fallback; mngui rebuild | §7.4 |
| `.env` güncellendi ama servis eski secret | `docker restart` yetmez → `compose up -d --no-deps <servis>` | §4.2 |
| apt/curl DNS hatası | `/etc/resolv.conf.tail` (8.8.8.8, 1.1.1.1) | KURULUM.md |
| E-posta gitmiyor | Odak’ta SMTP yok | Beklenen |

---

## 9. Yeni chat’te geliştirme için kontrol listesi

- [ ] Bu dosyayı ve [README.md](./README.md) okudum.
- [ ] Sunucu erişimi: `ssh odak@192.168.20.20`.
- [ ] UI geliştirme: `Mng.Ui` → `npm run dev` + `.env` → `GATEWAY_URL=http://192.168.20.20:5040`. Ana sayfa: `/` — bkz. [ui/WELCOME_HOME.md](./ui/WELCOME_HOME.md).
- [ ] Giriş: domain `odak` kullanıcısı; Domain UI için master admin.
- [ ] Deploy gerektiğinde: `sync-odak-source.ps1` + `deploy-odak-apps.ps1` (tam build zaman alır).
- [ ] Domain/şablon işleri: `docs/odak/domain/` alt dokümanlar.
- [x] LDAP Keeper (K2 + P0 + K4 + K5 + G1): sunucu **v1.3.4** — [ldap/DEVAM.md](./ldap/DEVAM.md)
- [x] LDAP **K3** MngScheduler: periyodik directory sync — [ldap/SCHEDULER_DIRECTORY_SYNC.md](./ldap/SCHEDULER_DIRECTORY_SYNC.md)
- [x] LDAP **UI** (K1.6 / K5 / G1): sunucu **mngui** — [ldap/HANDOFF_UI.md](./ldap/HANDOFF_UI.md)
- [x] GitHub `main`: `72872d9` (LDAP + Odak script/doküman)
- [ ] HTTPS / Nginx: bilinçli **ertelendi** (opsiyonel) — [ldap/ODAK_HTTP_AND_GATEWAY.md](./ldap/ODAK_HTTP_AND_GATEWAY.md)
- [ ] LDAP **opsiyonel** test checklist (K5e): [ldap/USER_SOURCES.md](./ldap/USER_SOURCES.md) §8

**LDAP geliştirme duraklatıldı (25 Mayıs 2026).** Yeni chat: ürün özelliği, iş kuralları, yeni modüller — önce bu dosya + [README.md](./README.md).

---

## 10. Doküman ve dosya indeksi

| Dosya | İçerik |
|-------|--------|
| [README.md](./README.md) | Odak klasör girişi |
| [setup/README.md](./setup/README.md) | Kurulum alt indeks |
| [setup/KURULUM.md](./setup/KURULUM.md) | SSH, Docker, sunucu analizi |
| [setup/MNG_COMMON_ODAK.md](./setup/MNG_COMMON_ODAK.md) | Altyapı compose |
| [setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md](./setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) | IT — altyapı erişim |
| [setup/MNG_APPS_ODAK.md](./setup/MNG_APPS_ODAK.md) | Uygulama servisleri |
| [setup/MNG_APPS_ODAK_DEPLOY.md](./setup/MNG_APPS_ODAK_DEPLOY.md) | Deploy stratejisi |
| [setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md](./setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md) | IT — uygulama URL/port |
| [domain/DOMAIN_OLUSTURMA.md](./domain/DOMAIN_OLUSTURMA.md) | Domain UI rehberi |
| [domain/DOMAIN_OLUSTURMA_KAYIT.md](./domain/DOMAIN_OLUSTURMA_KAYIT.md) | Oturum + hata çözümleri |
| [ldap/DEVAM.md](./ldap/DEVAM.md) | LDAP durum + duraklatma |
| [ldap/HANDOFF_UI.md](./ldap/HANDOFF_UI.md) | UI tamamlandı (arşiv) |
| [ldap/HANDOFF_MNGSCHEDULER.md](./ldap/HANDOFF_MNGSCHEDULER.md) | K3 tamamlandı |
| [ldap/SCHEDULER_DIRECTORY_SYNC.md](./ldap/SCHEDULER_DIRECTORY_SYNC.md) | Periyodik sync |
| [ldap/ODAK_HTTP_AND_GATEWAY.md](./ldap/ODAK_HTTP_AND_GATEWAY.md) | HTTP POC, `/keeper` yolu |
| `ApplicationResources/mng_common/docker-compose.odak.yml` | Altyapı override |
| `ApplicationResources/mng_apps/docker-compose.odak.yml` | Uygulama override |
| `ApplicationResources/mng_apps/.env.odak.example` | Sunucu .env şablonu |
| `Mng.Ui/README.md` | UI geliştirme detayı |

---

## 11. Oturum notu (Cursor kurulum chat’i)

Bu rehber, **Odak sunucu + mng_common + mng_apps + domain + initial data + yerel dev** kurulum oturumunun birleşik özetidir. Kurulum amacına ulaşıldı; sonraki çalışmalar ürün geliştirmesine ayrılmalıdır.
