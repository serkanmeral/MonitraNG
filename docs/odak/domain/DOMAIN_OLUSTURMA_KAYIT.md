# Domain oluşturma — canlı oturum kaydı

**Amaç:** MngDomainUI ile domain oluşturma; her adımı (başarı / hata / çözüm) kaydederek [DOMAIN_OLUSTURMA.md](./DOMAIN_OLUSTURMA.md) rehberini doğrulamak ve güncellemek.  
**Ortam:** Odak — `192.168.20.20`  
**Başlangıç:** 22 Mayıs 2026

---

## Test domain (API ile oluşturuldu)

| Alan | Değer |
|------|--------|
| Domain Name | `odak-demo` |
| Display Name | Odak Demo Domain |
| Admin Email | `admin@odak-demo.local` |
| Admin Password | `Admin123!@#` |
| domainId | `6a0f887f3d6ba5d774ee37b3` |
| databaseName | `mng_odak-demo` |

---

## Oturum günlüğü

| # | Adım | Sonuç | Not / çözüm |
|---|------|--------|-------------|
| 0 | Ön kontrol (servisler, .env, Keycloak admin-cli token) | ✅ | keeper/domainui/keycloak 200; LICENSE ve CLIENT_ID .env’de var |
| 0b | API ile ilk deneme (`odak-demo`) | ❌ | `CreateKeycloakRealm` — `invalid_client` |
| 0c | Keycloak’ta `mng-keeper-admin` client oluştur | ✅ | Master realm, confidential, direct access grants |
| 0d | `.env` → `KEYCLOAK_CLIENT_SECRET` güncelle | ✅ | Sunucu: `/home/odak/MonitraNG/ApplicationResources/mng_apps/.env` |
| 0e | `docker restart mngkeeper` | ❌ | Container env hâlâ `CHANGE_ME_...` |
| 0f | Compose ile recreate | ✅ | `docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d --no-deps mngkeeper` |
| 0g | API ile ikinci deneme | ✅ | HTTP 201, 16 adım, `isSuccess: true` |
| 1 | MngDomainUI login | Bekliyor | Sizin tarayıcı adımı |
| 2 | UI ile ikinci domain (isteğe bağlı) | Bekliyor | Örn. `odak-ui-test` |

---

## Detaylı adımlar

### Adım 0 — Ön kontrol (22 May 2026)

**Servisler:** mngkeeper, mngdomainui, keycloak healthy; keeper/domainui/keycloak HTTP 200.

**`.env` (sunucu):** `KEYCLOAK_BASE_URL=http://keycloak:8080`, `KEYCLOAK_PATH_PREFIX=/keycloak`, `MNGKEEPER_LICENSE_MASTER_KEY` dolu.

**Eksik:** `KEYCLOAK_CLIENT_SECRET` dosyada güncellenmiş olsa bile container eski değeri taşıyordu → compose recreate şart.

---

### Adım 1 — MngDomainUI login

| Alan | Değer |
|------|--------|
| URL | http://192.168.20.20:3001/domain/ |
| Kullanıcı | `admin` (Keycloak **master** realm) |
| Parola | Altyapı parolası — [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) |
| Sonuç | Bekliyor (sizin tarayıcı) |
| Not | Keeper tarafı hazır; login UI ile doğrulanmalı |

---

### Adım 2 — Create Domain formu (UI)

| Alan | Değer |
|------|--------|
| Öneri | Yeni ad: `odak-ui-test` (`odak-demo` API ile zaten var) |
| Sonuç | Bekliyor |
| Beklenen | Modal kapanır, listede **Active** |
| Keeper log / failedStep | Hata olursa: `docker logs mngkeeper --tail 80` |

---

### Adım 3 — Doğrulama (API sonrası)

| Kontrol | Sonuç |
|---------|--------|
| Keeper API | `odak-demo` — `isSuccess: true`, message: 16 steps |
| Keycloak realm | `odak-demo` (master dışında yeni realm) |
| MongoDB | `mng_odak-demo` |

---

## Hata 1 — `invalid_client` / `unauthorized_client` (CreateKeycloakRealm)

**Belirti:**

```text
Failed at step 'CreateKeycloakRealm': Failed to get admin token: Unauthorized
{"error":"invalid_client","error_description":"Invalid client or Invalid client credentials"}
```

veya secret güncellenip sadece `docker restart` yapıldıysa:

```text
{"error":"unauthorized_client", ...}
```

**Kök neden:** Master realm’de `mng-keeper-admin` client yok veya `.env` içindeki `KEYCLOAK_CLIENT_SECRET` Keycloak ile uyuşmuyor; ayrıca **restart env dosyasını yeniden okumaz**.

**Çözüm (sırayla):**

1. Keycloak Admin → http://192.168.20.20:8080/keycloak/admin/master/console/
2. **Clients** → Create → Client ID: `mng-keeper-admin`
   - Client authentication: **ON** (confidential)
   - **Direct access grants**: **ON** (Keeper admin token için)
3. **Credentials** sekmesinden Client secret kopyala.
4. Sunucuda `.env`: `KEYCLOAK_CLIENT_SECRET=<secret>`
5. Container’ı **recreate** et (restart yetmez):

```bash
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d --no-deps mngkeeper
```

6. Doğrula: `docker inspect mngkeeper` → `MngKeeperSettings__Keycloak__ClientSecret` artık `CHANGE_ME_...` olmamalı.

**Otomasyon (SSH, admin parolası biliniyorsa):** Admin token (`admin-cli`) ile REST `POST /admin/realms/master/clients` — bu oturumda client yoktu, API ile oluşturuldu.

---

## UI token / cookie (Odak HTTP)

**Belirti:** `Access token bulunamadı` — `getDomainByName` / `fetchFromMngKeeper`  
**Neden:** Production build’de cookie `Secure=true`; Odak UI `http://192.168.20.20:3000` → tarayıcı cookie yazmaz.  
**Çözüm:** `Mng.Ui` — `shouldUseSecureCookie()` (yalnızca HTTPS); `getAccessToken()` Pinia fallback. `mngui` image yeniden build.

---

## Öğrenilenler (rehbere yansıyacak)

1. Domain oluşturmadan **önce** `mng-keeper-admin` + secret + compose recreate zorunlu.
2. `.env` güncellemesi sonrası mutlaka `docker compose ... up -d --no-deps mngkeeper`, `docker restart` yeterli değil.
3. Pipeline Mongo adımlarını Keycloak’tan önce çalıştırır; Keycloak hatasında rollback temizler (`mng_odak-demo` silinir).
4. UI adımları aynı Keeper endpoint’ine gider; API başarısı UI’nın da çalışacağını gösterir.

---

## İlgili

- [DOMAIN_OLUSTURMA.md](./DOMAIN_OLUSTURMA.md)
- [DOMAIN_OLUSTURMA_API.md](./DOMAIN_OLUSTURMA_API.md)
