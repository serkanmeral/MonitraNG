# Push / Deploy Sonrası Config Hatası Olmaması — Kontrol Listesi

**Amaç:** GitLab’a push ettiğinizde pipeline (deploy-services) bittikten sonra tüm servislerin config eksikliği olmadan çalışması.  
**Son güncelleme:** 26 Ocak 2026

---

## 1. Repo’da olması gerekenler (push ile gidecek)

Bu dosya/değişiklikler **zaten repoda** olmalı; push öncesi `git status` ile kontrol edin.

| Ne | Nerede | Not |
|----|--------|-----|
| MngKeeper License MasterKey env | `docker-compose.production.yml` mngkeeper | `MngKeeperSettings__License__MasterKey=${MNGKEEPER_LICENSE_MASTER_KEY}` |
| MngKeeper healthcheck HTTP | `docker-compose.production.yml` mngkeeper | `curl -f http://localhost:5001/api/version/short` |
| MngKeeper Keycloak PathPrefix | `docker-compose.production.yml` mngkeeper | `MngKeeperSettings__Keycloak__PathPrefix=${KEYCLOAK_PATH_PREFIX:-/keycloak}` |
| KeycloakService BaseUrl/PathPrefix mantığı | `MngKeeper/.../KeycloakService.cs` | BuildEndpointPath: BaseUrl path ile bitiyorsa tekrar prefix eklemez |
| LicenseEncryptionService MasterKey opsiyonel | `MngKeeper/.../LicenseEncryptionService.cs` | Ctor’da throw yok; Encrypt/Decrypt vb. çağrıda hata |
| LicenseService "no license" → TokenGeneration izin | `MngKeeper/.../LicenseService.cs` | IsOperationAllowedAsync: No license + TokenGeneration ⇒ true |
| MngKeeper License MasterKey dokümantasyonu | `ApplicationResources/mng_apps/env.example` | `# MNGKEEPER_LICENSE_MASTER_KEY=` + açıklama |
| MngDomainUI Keeper/DG http varsayılanları | `docker-compose.production.yml` mngdomainui | `SERVER_KEEPER_URL=…http://mngkeeper:5001`, `SERVER_DATAGATEWAY_URL=…http://mngdatagateway:5010` |
| MngDomainUI Scheduler URL | `docker-compose.production.yml` mngdomainui | `SERVER_SCHEDULER_URL=${SERVER_SCHEDULER_URL:-http://mngscheduler:5090}` |

---

## 2. Sunucuda kalıcı olması gerekenler (.env — repo’da yok)

`git reset --hard` **.env** dosyasına dokunmaz. Deploy öncesi sunucuda `ApplicationResources/mng_apps/.env` içinde aşağıdakilerin tanımlı olduğundan emin olun.

| Değişken | Zorunlu / Kullanım |
|----------|---------------------|
| `MNGKEEPER_LICENSE_MASTER_KEY` | Create Domain / trial lisans için. Yoksa login çalışır, Create Domain hata verir. |
| `KEYCLOAK_BASE_URL` | `http://keycloak:8080` (sadece origin). Create Domain admin token için. |
| `KEYCLOAK_PATH_PREFIX` | `/keycloak` (Keycloak bu path’te çalışıyorsa). |
| `KEYCLOAK_ADMIN_USERNAME`, `KEYCLOAK_ADMIN_PASSWORD`, `KEYCLOAK_CLIENT_ID`, `KEYCLOAK_CLIENT_SECRET` | MngKeeper Create Domain için. |
| `MONGO_CONNECTION_STRING`, `REDIS_CONNECTION_STRING`, `RABBITMQ_*`, `MINIO_*` vb. | Diğer servisler için; `env.example` ile karşılaştırın. |
| `CORS_ALLOWED_ORIGIN_1` | Frontend origin (örn. `https://app.monitrang.com`). MngGateway + MngHub için. |

Yeni sunucu / ilk kurulum: `cp env.example .env` yapıp tüm `CHANGE_ME` ve zorunlu değerleri doldurun; `MNGKEEPER_LICENSE_MASTER_KEY` için `openssl rand -base64 32` ile üretip .env’e ekleyin.

---

## 3. MngKeeper özelinde: “Push sonrası config hatası olmadan çalışır mı?”

- **Repo tarafı:** Yukarıdaki tablodaki MngKeeper satırları repoda ise push yeterli.
- **Sunucu tarafı:** `.env` içinde `MNGKEEPER_LICENSE_MASTER_KEY` tanımlı olmalı (sizin sunucuda tespit edildi).
- **Sonuç:** Push + deploy sonrası MngKeeper, config eksikliği olmadan çalışabilir. MasterKey yoksa bile **login** çalışır (kod değişikliği sayesinde); Create Domain için key şart.

---

## 4. Bu checklist’i güncellemek

Yeni “sadece sunucuda yapıldı, push’ta kayboldu” düzeltmesi yapıldığında:

1. Önce repoya (compose / kod / env.example) yansıtın.
2. Bu dosyada “Repo’da olması gerekenler” veya “Sunucuda kalıcı olması gerekenler” bölümünü güncelleyin.

 Böylece bir sonraki push’ta aynı config hatası tekrarlanmaz.
