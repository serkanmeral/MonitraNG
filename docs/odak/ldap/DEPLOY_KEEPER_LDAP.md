# Deploy — MngKeeper LDAP (K2 + P0 + K4)

**Durum:** ✅ **Tamamlandı** (23 Mayıs 2026) — sunucu **v1.3.0**, smoke token + gateway doğrulandı.  
**Sıra (onaylı):** ~~Deploy~~ → **K3** MngScheduler ([HANDOFF_MNGSCHEDULER.md](./HANDOFF_MNGSCHEDULER.md)) → **UI** (K1.6 / K5) → **HTTPS** (en son).

**Ortam:** Odak POC **HTTP**; API yolu `http://192.168.20.20:5040/keeper/api/...` — [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md).

---

## 1. Deploy öncesi (PC — tamamlandı)

- [x] Yerel `dotnet run` + `POST /api/auth/token` (odak pilot)
- [x] P0 admin/manager JWT doğrulandı
- [x] K4 login sync (Mongo gruplar / log)
- [x] Sunucu deploy + `GET /api/version/short` → **1.3.0**
- [ ] İsteğe bağlı: sunucuda tam `POST /api/directory/sync` + 409 smoke (T8–T12)

**Mongo (sunucu = `192.168.20.20`):** `mngkeeper.domains` → `settings.directoryPrivileges` doğru yerde (kökte `adminGroupNames` **olmamalı**). Yerel testte düzelttiyseniz aynı DB kullanılıyor.

---

## 2. Kaynak senkronu (geliştirme PC)

Repo kökünden PowerShell. SSH: **`ApplicationResources/mng_apps/.env.odak.local`** veya **`scripts/odak/local-credentials.ps1`** (gitignore); alternatif `$env:ODAK_SSH_PASSWORD`. Parola dokümanda tutulmaz.

**Tek komut (önerilen — v1.3.0 LDAP):**

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG
$env:ODAK_SSH_PASSWORD = '<odak-kullanici-ssh-parolasi>'
.\scripts\odak\deploy-keeper-odak.ps1 -FullBuild
```

Adım adım:

```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths MngKeeper,ApplicationResources/mng_apps
.\scripts\odak\deploy-odak-apps.ps1 -Services mngkeeper -FullBuild
```

İlk kez veya tam repo yoksa: `.\scripts\odak\sync-odak-source.ps1 -Full`

**Ön koşul:** Sunucuda `/home/odak/mng_common` ayakta, `mng_common_mng_network` var.

---

## 3. Build + up (sunucu)

İlk build veya büyük değişiklik (~10–30 dk): `-FullBuild` kullanın.

**Not:** Script sunucuda `docker compose -f docker-compose.production.yml -f docker-compose.odak.yml` kullanır. `.env` dosyası **üzerine yazılmaz** — sunucuda bir kez `cp .env.odak.example .env` ve secret’lar dolu olmalı.

### Sunucu `.env` kritik alanlar (Keeper)

| Değişken | Beklenen |
|----------|----------|
| `KEYCLOAK_PATH_PREFIX` | `/keycloak` |
| `KEYCLOAK_CLIENT_ID` / `SECRET` | Admin API (realm kullanıcı listesi) — `mng-keeper-admin` veya çalışan client |
| `MONGO_CONNECTION_STRING` | `mongo:27017` (Docker ağı) |
| `REDIS_CONNECTION_STRING` | `redis:6379,password=...` |
| `MINIO_ENDPOINT` | `minio:9000` |

Yerelde `admin-cli` kullandıysanız; sunucu `.env` farklı client içerebilir — directory sync hata verirse Keycloak client / secret kontrol edin.

---

## 4. Smoke test (deploy sonrası)

| # | Test | URL / komut |
|---|------|-------------|
| D1 | Keeper ayakta + sürüm | http://192.168.20.20:5001/api/version/short → **1.3.0** |
| D2 | Scalar (`EnableSwagger=true`; v1.3.0+ deploy sonrası) | http://192.168.20.20:5001/scalar/v1 |
| D2b | Swagger UI (her zaman `EnableSwagger=true` ile) | http://192.168.20.20:5001/api-docs |
| D2c | OpenAPI JSON | http://192.168.20.20:5001/api-docs/v1/swagger.json |

**Not:** `https://` değil **`http://`** (Odak compose 5001 HTTP). `/scalar/v1` eski image’da 404 — Scalar Production’da v1.3.0+ deploy ile açılır.

| D3 | Token | `POST /api/auth/token` — `domain: odak`, pilot kullanıcı |
| D4 | Directory sync | `POST /api/directory/sync` — body `{ "domainId": "odak" }` + Bearer |
| D5 | 409 | Sync sürerken ikinci POST → 409 |
| D6 | Mongo | `mng_odak.@users` — gruplar, `directorySyncedAt` |
| D7 | JWT | `is_admin` / `is_manager` / `user_groups` |

Gateway üzerinden test (UI ile aynı yol — **doğrulanmış**):

```http
GET  http://192.168.20.20:5040/keeper/api/version/short
POST http://192.168.20.20:5040/keeper/api/auth/token
POST http://192.168.20.20:5040/keeper/api/directory/sync
```

Detay: [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md). Scalar Try it out için sunucu **`http://192.168.20.20:5001`** (5040 kökü → 404).

---

## 5. Sorun giderme

| Belirti | Olası neden |
|---------|-------------|
| `Domain with realm 'odak' not found` | `domains` dokümanı / `directoryPrivileges` BSON hatası |
| Directory sync 401/403 | `KEYCLOAK_CLIENT_ID` / secret sunucu `.env` |
| Login OK, eski gruplar | K4 kapalı veya tam sync lock — log `Login sync` / `sync_in_progress` |
| Build uzun / RAM | Sunucu 15 GiB — diğer build’leri erteleyin |

Log:

```bash
ssh odak@192.168.20.20
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml logs -f mngkeeper --tail 100
```

---

## 6. Deploy sonrası yol haritası

1. **K3** — [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md)  
2. **UI** — K1.6 pilot login, sonra K5 ([USER_SOURCES.md](./USER_SOURCES.md))

İlgili: [DEV_WORKFLOW.md](./DEV_WORKFLOW.md), [MNG_APPS_ODAK_DEPLOY.md](../setup/MNG_APPS_ODAK_DEPLOY.md)
