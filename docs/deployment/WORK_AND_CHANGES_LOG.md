# Yapılan İşlemler ve Değişiklikler Logu

**Amaç:** Deployment, sunucu tarafı düzeltmeler ve ilgili değişikliklerin tek yerde takip edilmesi.  
**Son Güncelleme:** 26 Ocak 2026

---

## Kullanım

- Her oturumda yapılan işlemler ve kod/env değişiklikleri bu dosyaya kronolojik olarak eklenir.
- Satır içi "GitLab’a push" veya "sadece sunucuda" notu, değişikliğin nereye uygulandığını belirtir.

---

## 25 Ocak 2026 – Deploy sonrası sunucu düzeltmeleri

### Bağlam

- GitLab pipeline tamamlanmış; `deploy-services` manuel çalıştırılmış.
- Sunucu: `ssh root@monitrang-server` (45.141.151.52), repo: `/root/MonitraNG`, production compose: `ApplicationResources/mng_apps/docker-compose.production.yml`.

### 1. Sunucuda production container durumu

- Tüm container’lar Up; **mngkeeper**, **mngdatagateway**, **mnggateway**, **mngui** “unhealthy” raporlanıyordu.
- Diğerleri: mngadmin, mngdomainui, mnghub, mngllm, mngnotifier, mngscheduler healthy.

### 2. Healthcheck uyumsuzlukları (sadece sunucuda düzeltildi)

Uygulama gerçekte farklı protokol/port/path’te yanıt veriyordu; healthcheck ise başka bir URL’e gidiyordu. Aşağıdaki değişiklikler **yalnızca sunucudaki** `docker-compose.production.yml` üzerinde yapıldı. **GitLab’a push edilmedi.**

| Servis          | Sorun                                                                 | Yapılan değişiklik                                                                                                                                 |
|-----------------|-----------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------|
| **mnggateway**  | Uygulama HTTPS 5000; healthcheck HTTP kullanıyordu                    | `curl -f http://localhost:5000/health` → `curl -k -f https://localhost:5000/health`                                                                 |
| **mngkeeper**   | Uygulama HTTP 5001; healthcheck HTTPS bekliyordu                      | `curl -k -f https://localhost:5001/api/version/short` → `curl -f http://localhost:5001/api/version/short`                                           |
| **mngdatagateway** | Uygulama HTTP 5010; healthcheck HTTPS bekliyordu                    | `curl -k -f https://localhost:5010/api/v1/health` → `curl -f http://localhost:5010/api/v1/health`                                                   |
| **mngui**       | Healthcheck `http://localhost/` kullanıyordu; container’da çalışmıyordu | `http://localhost/` → `http://127.0.0.1/`                                                                                                           |

- Yedek: sunucuda `docker-compose.production.yml.bak-healthcheck` alındı.
- Bu dört servis `docker compose up -d --no-deps --force-recreate ...` ile yeniden oluşturuldu (rebuild yok).
- Sonuç: Tüm 10 servis **Up (healthy)**.

### 3. Repo tarafında yapılmayanlar

- `ApplicationResources/mng_apps/docker-compose.production.yml` içindeki bu healthcheck değişiklikleri repoda **henüz yok**.
- İstenirse aynı metinler repodaki compose dosyasına uygulanıp, **onay sonrası** GitLab’a push edilebilir.

### 4. MngDomainUI Keycloak login 404 (keycloak/keycloak tekrarı)

- **Hata:** `[POST] "http://keycloak:8080/keycloak/keycloak/realms/master/protocol/openid-connect/token": 404 Not Found`
- **Sebep:** `KEYCLOAK_BASE_URL` bazen `http://keycloak:8080/keycloak` olarak veriliyor; `KEYCLOAK_PATH_PREFIX=/keycloak` ile birleşince URL’de `keycloak/keycloak` oluşuyordu.
- **Kod değişikliği:** `MngDomainUI/server/utils/keycloak.ts` içinde `buildKeycloakUrl`: `baseUrl` zaten `pathPrefix` ile bitiyorsa prefix tekrar eklenmiyor.

**Sunucuda uygulama (25 Ocak 2026):**

- Değişiklik **sunucuda** yapıldı; pipeline/repoya push yok.
- Adımlar:
  1. Lokal `MngDomainUI/server/utils/keycloak.ts` (güncel hali) `scp` ile sunucuya kopyalandı:  
     `scp .../keycloak.ts root@monitrang-server:/root/MonitraNG/MngDomainUI/server/utils/keycloak.ts`
  2. Sunucuda: `cd /root/MonitraNG/ApplicationResources/mng_apps`  
     `docker compose -f docker-compose.production.yml build mngdomainui`  
     `docker compose -f docker-compose.production.yml up -d --no-deps mngdomainui`
  3. MngDomainUI yeni image ile yeniden başlatıldı; login token URL’i artık doğru oluşuyor.
- **Uyarı:** Bir sonraki `deploy-services` çalıştığında sunucuda `git reset --hard origin/main` yapılacağı için bu dosya değişikliği **silinir**. Kalıcı olması için aynı değişiklik repoda tutulup (isteğe bağlı) push edilebilir.
- **Dosya (repo):** `MngDomainUI/server/utils/keycloak.ts` — istenirse repoda da aynı mantıkla güncellenip ileride deploy’a dahil edilebilir.

### 5. MngDomainUI [GET] "/domain/api/keeper/domain" 500 — Keeper’a bağlanamıyor

- **Belirti:** `[GET] "/domain/api/keeper/domain": 500`; MngDomainUI log’unda `[Keeper Proxy] Error: [GET] "https://mngkeeper:5001/api/domain": <no response> fetch failed` ve **ERR_SSL_WRONG_VERSION_NUMBER**.
- **Sebep:** MngDomainUI, Keeper’a **HTTPS** (`https://mngkeeper:5001`) ile istek atıyordu; MngKeeper container içinde **HTTP** dinlediği için TLS beklenirken HTTP yanıt geliyor ve Node/OpenSSL bu hatayı veriyordu.
- **Yapılan (sadece sunucuda):** `docker-compose.production.yml` içinde **mngdomainui** env varsayılanları:
  - `SERVER_KEEPER_URL=${SERVER_KEEPER_URL:-https://mngkeeper:5001}` → `http://mngkeeper:5001`
  - `SERVER_DATAGATEWAY_URL=${SERVER_DATAGATEWAY_URL:-https://mngdatagateway:5010}` → `http://mngdatagateway:5010`
- **Adımlar:** Sunucuda `docker-compose.production.yml` düzenlendi, `docker compose up -d --no-deps mngdomainui` ile container yeniden oluşturuldu. Rebuild yok.
- **Doğrulama:** `GET /domain/api/keeper/domain` (MngDomainUI üzerinden) 200 döndü, domain listesi alındı.
- **Yedek:** `docker-compose.production.yml.bak-keeper-http`
- **Uyarı:** Bir sonraki `deploy-services` (git reset --hard) bu compose değişikliğini de siler. Kalıcı olması için repodaki compose’da aynı varsayılanlar kullanılabilir.

### 6. MngDomainUI [GET] "/domain/api/scheduler/v1/system/jobs" 500 — Scheduler’a bağlanamıyor

- **Belirti:** `[GET] "/domain/api/scheduler/v1/system/jobs": 500`, response: `"http://localhost:5090/api/v1/system/jobs": <no response> fetch failed`.
- **Sebep:** Scheduler proxy `SERVER_SCHEDULER_URL` / `SCHEDULER_URL` olmadığı için `http://localhost:5090` kullanıyordu; container içinde localhost scheduler’a çıkmıyor.
- **Yapılan:**
  - **Sunucuda:** `docker-compose.production.yml` içinde **mngdomainui** env’e eklendi:  
    `SERVER_SCHEDULER_URL=${SERVER_SCHEDULER_URL:-http://mngscheduler:5090}`  
    (Satır 433’teki boş/hatalı satır, `scripts/deployment/sched-line.txt` içeriği ile değiştirildi; ardından `docker compose up -d --no-deps mngdomainui`.)
  - **Repoda:** `ApplicationResources/mng_apps/docker-compose.production.yml` içinde mngdomainui bölümüne aynı satır eklendi (ileride deploy’da kullanılmak üzere).
- **Doğrulama:** `GET /domain/api/scheduler/v1/system/jobs` (MngDomainUI üzerinden) 200 döndü, system job listesi alındı.
- **Uyarı:** Sunucudaki compose değişikliği bir sonraki `deploy-services` ile silinir; kalıcı olması için repodaki bu hali push edilebilir.

### 7. MngDomainUI Create Domain — License MasterKey is not configured

- **Belirti:** Create Domain sırasında MngKeeper log’unda:  
  `System.InvalidOperationException: License MasterKey is not configured. Set MngKeeperSettings:License:MasterKey in appsettings.json or environment variable.`
- **Sebep:** Domain oluşturulurken trial lisans şifrelemesi için `LicenseEncryptionService` kullanılıyor; bu da `MngKeeperSettings:License:MasterKey` değerine ihtiyaç duyuyor. Production compose’da bu env tanımlı değildi.
- **Yapılan (repo):** `ApplicationResources/mng_apps/docker-compose.production.yml` içinde **mngkeeper** env’e eklendi:  
  `MngKeeperSettings__License__MasterKey=${MNGKEEPER_LICENSE_MASTER_KEY}`
- **Sunucuda yapmanız gerekenler:**
  1. **Key üretin** (bir kez, güvenli saklayın):  
     `openssl rand -base64 32`  
     veya benzeri en az 32 karakterlik rastgele bir dize.
  2. **Sunucuda** `ApplicationResources/mng_apps` içinde `.env` varsa oraya, yoksa compose’u çalıştırdığınız yerde bu değişkeni verin:  
     `MNGKEEPER_LICENSE_MASTER_KEY=<ürettiğiniz-key>`
  3. **Compose’da satır yoksa** mngkeeper env bölümüne (Certificate Settings satırından hemen sonra) ekleyin:  
     `- MngKeeperSettings__License__MasterKey=${MNGKEEPER_LICENSE_MASTER_KEY}`
  4. MngKeeper’ı yeniden başlatın:  
     `docker compose -f docker-compose.production.yml build mngkeeper` ardından `up -d --no-deps mngkeeper`
- **Not:** Bu key mevcut domain’lerin lisans dosyalarını çözmek için kullanıldığından, değiştirirseniz eski trial/real lisanslar okunamaz. Bir kez belirleyip aynı key’i kalıcı kullanın.
- **Durum:** Key üretildi, sunucuda `MNGKEEPER_LICENSE_MASTER_KEY` olarak tanımlandı ve mngkeeper yeniden başlatıldı. Create Domain çalışır durumda.

### 8. Create Domain — Keycloak admin token 404 (Failed to get admin token, Resource not found)

- **Belirti:** Create Domain sırasında "Create Keycloak realm" adımında MngKeeper log’unda:  
  `Failed to get admin token. Status: NotFound`  
  body: `<html><body><h1>Resource not found</h1></body></html>`
- **Sebep:** MngKeeper, Keycloak admin token için `realms/master/protocol/openid-connect/token` isteğini atıyordu; Keycloak sunucuda `/keycloak` path prefix ile çalıştığı için doğru URL `http://keycloak:8080/keycloak/realms/master/...` olmalıydı. Repoda **PathPrefix** mngkeeper env’inde tanımlı değildi; BaseUrl ile path birleşiminde keycloak/keycloak tekrarını önleyen mantık da yoktu.
- **Yapılan (repo):**
  1. **KeycloakService.cs:**  
     - `BuildEndpointPath` içinde BaseUrl okunuyor; BaseUrl zaten PathPrefix ile bitiyorsa prefix tekrar eklenmiyor (MngDomainUI’daki `buildKeycloakUrl` ile aynı mantık).  
     - Constructor’da `_baseUrl` ve `_pathPrefix` ayarlanıyor; path prefix boşsa varsayılan davranış aynı.
  2. **docker-compose.production.yml:** mngkeeper Keycloak env’ine eklendi:  
     `MngKeeperSettings__Keycloak__PathPrefix=${KEYCLOAK_PATH_PREFIX:-/keycloak}`  
     Böylece `KEYCLOAK_BASE_URL=http://keycloak:8080` ve `KEYCLOAK_PATH_PREFIX=/keycloak` (veya .env’de tanımlı değer) ile token URL’i  
     `http://keycloak:8080/keycloak/realms/master/protocol/openid-connect/token` olur.
- **Sunucuda yapmanız gerekenler:**
  1. Repodaki değişiklikleri alıp mngkeeper’ı yeniden build/up edin (veya deploy pipeline ile gelsin).  
  2. `KEYCLOAK_BASE_URL` origin olmalı (örn. `http://keycloak:8080`). Zaten `http://keycloak:8080/keycloak` ise kod path’i tekrar eklemez.  
  3. İsteğe bağlı: `.env` veya compose ortamında `KEYCLOAK_PATH_PREFIX=/keycloak` tanımlı olsun (compose’daki varsayılan da `/keycloak`).  
  4. MngKeeper’ı yeniden başlatın:  
     `docker compose -f docker-compose.production.yml build mngkeeper`  
     `docker compose -f docker-compose.production.yml build mngkeeper` ardından `up -d --no-deps mngkeeper`
- **Dosyalar:** `MngKeeper.Infrastructure/Services/KeycloakService.cs`, `ApplicationResources/mng_apps/docker-compose.production.yml`

**Sunucuda uygulama (GitLab push olmadan):**

- §8 düzeltmesi **repoya push edilmeden** sunucuda yapıldı.
- Yapılan: Lokalden `KeycloakService.cs` ve `docker-compose.production.yml` sunucuya `scp` ile kopyalandı; sunucuda `docker compose -f docker-compose.production.yml build mngkeeper` ve `up -d --no-deps mngkeeper` çalıştırıldı.
- Create Domain (Keycloak admin token) bu sayede çalışır hale geldi. Kalıcı olması için ileride aynı değişiklikler repoda tutulup GitLab’a push edilebilir.

### 9. Sunucuda MngKeeper healthcheck HTTP (Portainer'da healthy görünsün)

- **Amaç:** Portainer’da MngKeeper’ın “starting” yerine “healthy” görünmesi. Container içinde uygulama HTTP (5001) dinlediği için healthcheck da HTTP kullanmalı.
- **Değişiklik:** `docker-compose.production.yml` içinde **mngkeeper** healthcheck:
  - Eski: `curl -k -f https://localhost:5001/api/version/short`
  - Yeni: `curl -f http://localhost:5001/api/version/short`
- **Repoda yapılan:** Aynı satır `ApplicationResources/mng_apps/docker-compose.production.yml` içinde güncellendi. İleride deploy/push ile sunucuya bu hali gelebilir.
- **Sunucuda yapılacak (bu işlemin kaydı):**
  1. Sunucuda `docker-compose.production.yml` içinde mngkeeper `healthcheck.test` satırını şu şekilde yapın:
     `test: ["CMD-SHELL", "curl -f http://localhost:5001/api/version/short || exit 1"]`
  2. MngKeeper’ı healthcheck’i yeniden okuyacak şekilde yeniden oluşturun (rebuild gerekmez):
     `cd /root/MonitraNG/ApplicationResources/mng_apps`
     `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngkeeper`
  3. Birkaç dakika içinde Portainer’da mngkeeper “healthy” görünmeli.
- **Alternatif (repodan dosyayı sunucuya taşıyorsanız):** Güncel `docker-compose.production.yml` dosyasını lokalden sunucuya `scp` ile kopyalayıp ardından yukarıdaki `up -d --no-deps --force-recreate mngkeeper` komutunu çalıştırın.
- **Sunucuda uygulama:** Seçenek A ile yapıldı — mngkeeper healthcheck satırı `sed` ile HTTP’ye çevrildi, ardından `docker compose up -d --no-deps --force-recreate mngkeeper` çalıştırıldı. Portainer’da healthy görünmesi beklenir.

### 10. Create Domain 404 devam ediyorsa — .env ve Keycloak ayarları

Create Domain sırasında "Failed to get admin token. Status: NotFound" alınıyorsa, MngKeeper Keycloak’a yanlış URL ile gidiyor olabilir. **Sunucudaki** `ApplicationResources/mng_apps/.env` dosyasında aşağıdakileri kontrol edin.

- **KEYCLOAK_BASE_URL**  
  - **Doğru:** `KEYCLOAK_BASE_URL=http://keycloak:8080` (sadece origin, path yok).  
  - **Yanlış:** `KEYCLOAK_BASE_URL=http://keycloak:8080/keycloak` veya dış erişim adresi (örn. `https://auth.monitrang.com/keycloak`). BaseUrl’e path veya dış adres yazılırsa token isteği 404 verebilir. Container içinden Keycloak’a erişim `http://keycloak:8080` olmalı.

- **KEYCLOAK_PATH_PREFIX**  
  - Keycloak sunucuda `/keycloak` altında çalışıyorsa: `KEYCLOAK_PATH_PREFIX=/keycloak` yazın veya en azından tanımsız bırakın (compose varsayılanı `/keycloak`).  
  - Tanımlı değilse compose’daki `${KEYCLOAK_PATH_PREFIX:-/keycloak}` varsayılanı kullanılır; bu genelde doğrudur.

- **Diğer zorunlu Keycloak değişkenleri (MngKeeper admin token için)**  
  - `KEYCLOAK_ADMIN_USERNAME=admin`  
  - `KEYCLOAK_ADMIN_PASSWORD=<Keycloak admin şifresi>`  
  - `KEYCLOAK_CLIENT_ID=mng-keeper-admin`  
  - `KEYCLOAK_CLIENT_SECRET=<Keycloak’ta bu client için tanımlı secret>`  
  - `KEYCLOAK_DEFAULT_ADMIN_PASSWORD=...` (yeni domain admin kullanıcı şifresi)

**Yapılacaklar (sunucuda):**

1. **Kod güncel olmalı:** `KeycloakService.cs` içinde BaseUrl path ile bitiyorsa relative path döndürülüyor; böylece .env’de `KEYCLOAK_BASE_URL=http://keycloak:8080/keycloak` aynen kalabilir. Bu değişiklik sunucuya alınıp MngKeeper yeniden build/up edilmeli. .env’i değiştirmeniz gerekmez.
2. Değişiklikten sonra MngKeeper’ı yeniden başlatın (env’i okuyabilmesi için):  
   `cd /root/MonitraNG/ApplicationResources/mng_apps`  
   `docker compose -f docker-compose.production.yml build mngkeeper` ardından `up -d --no-deps mngkeeper`
3. Create Domain’i tekrar deneyin.

**Repo tarafı:** `ApplicationResources/mng_apps/env.example` içine `KEYCLOAK_PATH_PREFIX=/keycloak` ve açıklama eklendi; yeni ortamlar için rehber olarak kullanılabilir.

### 11. MngUI → DataGateway 401 (Side Menu: `/api/data/v1/data/@side_menu`)

- **Belirti:** `GET https://app.monitrang.com/api/data/v1/data/@side_menu?skip=0&limit=10000&sort=order:asc,level:asc` isteği **401 Unauthorized** dönüyordu; side menu DataGateway’den alınamıyordu.
- **Sebep:** Production’da MngUI statik build olarak Nginx ile sunuluyor. `/api/data/` istekleri Nuxt server route’a değil, doğrudan Nginx proxy ile `mngdatagateway:5010`’a gidiyor. Nginx `proxy_set_header Authorization $http_authorization` ile header’ı iletiyor; ancak **client tarafında** `fetchFromDataGateway` içinde `$fetch` çağrısına `Authorization: Bearer <token>` hiç eklenmiyordu. Token sadece cookie’de tutuluyor, istek header’ında gönderilmediği için DataGateway 401 döndü.
- **Yapılan (repo):** `Mng.Ui/services/apiService.ts` içinde `fetchFromDataGateway` güncellendi:
  1. Cookie’den token alınıp (`getAccessToken()`), token yoksa "Access token bulunamadı. Lütfen tekrar giriş yapın." hatası fırlatılıyor.
  2. İlk istekte ve 401 sonrası retry’da `Authorization: Bearer ${token}` header’ı ekleniyor.
- **Sonuç:** Tarayıcıdan giden `/api/data/...` istekleri artık Nginx’e Authorization ile gidiyor; Nginx DataGateway’e iletiyor, side menu ve diğer Data istekleri 401 almadan çalışır.
- **Dosya:** `Mng.Ui/services/apiService.ts`.

**Development ortamında etkisi:** Bu değişiklik **lokal development’ta sorun çıkarmaz**. Development’ta `/api/data/...` istekleri **Nuxt server route** (`server/api/data/[...path].ts`) üzerinden gider; server route token’ı **cookie**’den (`getCookie(event, 'access_token')`) okur ve DataGateway’e `Authorization: Bearer …` ekleyerek iletir. Client tarafında artık header’da da token göndermemiz, server route’un cookie ile çalışmasını değiştirmez; sadece production’daki Nginx proxy akışı için gerekli. Özetle: dev’de davranış aynı kalır.

**Sunucuya alma — iki yol:**

1. **GitLab pipeline ile (önerilen)**  
   - Değişiklikleri GitLab’a push edin (örn. `main` veya hedef branch).  
   - GitLab CI’da **deploy** stage’inde **deploy-services** job’ını çalıştırın (Play).  
   - Sunucuda script şunları yapar: `git fetch origin` → `git reset --hard origin/main` → `ApplicationResources/mng_apps` altında `docker compose -f docker-compose.production.yml build mngui` → `up -d --no-deps --force-recreate mngui`.  
   - Bu akışta güncel `apiService.ts` repodan gelir, MngUI image’ı yeniden build edilir ve container güncellenir.

2. **Push etmeden sadece sunucuda (geçici)**  
   - Lokalden değişen dosyayı sunucuya kopyalayın:  
     `scp Mng.Ui/services/apiService.ts root@monitrang-server:/root/MonitraNG/Mng.Ui/services/apiService.ts`  
     (Windows’ta tam path kullanın, örn. `c:\Serkan\iSIM\MonitraNG\Mng.Ui\services\apiService.ts`.)  
   - Sunucuda:  
     `cd /root/MonitraNG/ApplicationResources/mng_apps`  
     `docker compose -f docker-compose.production.yml build mngui`  
     `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`  
   - **Uyarı:** Sonraki `deploy-services` çalıştığında `git reset --hard origin/main` bu dosyayı eski hâline döndürür. Kalıcı olması için değişikliği repoya push edip pipeline ile deploy etmek gerekir.

### 12. MngUI → MngHub CORS hatası (SignalR / Hub bağlantısı)

- **Belirti:** MngUI, MngHub’a (SignalR) bağlanırken tarayıcıda **CORS** hatası alınıyordu.
- **Sebep:** MngGateway’de production CORS, env ile ayarlanıyor (`CORS_ALLOWED_ORIGIN_1`, `CORS_ALLOWED_ORIGIN_2`). MngHub’da ise production ortamında **CORS env’i yoktu**; sadece `appsettings.json` içindeki `http(s)://localhost:3000` kullanılıyordu. Tarayıcı doğrudan Hub’a veya Gateway’in Hub’a proxy ettiği uç noktaya gidiyorsa, Hub’ın CORS’u da frontend origin’ini (örn. `https://app.monitrang.com`) kabul etmeli. Gateway ve Hub CORS ayarlarının uyumlu olmaması bu hataya yol açıyordu.
- **Yapılan (repo):**
  1. **docker-compose.production.yml** — **mnghub** servisine MngGateway ile aynı env isimleriyle CORS eklendi:  
     `MngHubSettings__Cors__AllowedOrigins__0=${CORS_ALLOWED_ORIGIN_1:-https://app.monitra.local}`  
     `MngHubSettings__Cors__AllowedOrigins__1=${CORS_ALLOWED_ORIGIN_2:-}`  
     `MngHubSettings__Cors__AllowCredentials=true`
  2. **env.example** — `CORS_ALLOWED_ORIGIN_1` ve `CORS_ALLOWED_ORIGIN_2` açıklamaları eklendi; hem MngGateway hem MngHub’ın bu değişkenleri kullandığı belirtildi.
- **Sunucuda:** `.env` içinde `CORS_ALLOWED_ORIGIN_1=https://app.monitrang.com` (ve gerekiyorsa `CORS_ALLOWED_ORIGIN_2=…`) tanımlı olmalı. Hem mnggateway hem mnghub bu değişkenleri okur. Değişiklikten sonra mnghub’ı yeniden başlatın:  
  `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mnghub`
- **Dosyalar:** `ApplicationResources/mng_apps/docker-compose.production.yml`, `ApplicationResources/mng_apps/env.example`

**CORS hâlâ devam ediyorsa — same-origin /hub/ proxy:**

Tarayıcı **api.monitrang.com/hub/ws** yerine **app.monitrang.com/hub/ws** kullanırsa istek same-origin olur ve CORS devre dışı kalır. Uygulama ayrıntıları aşağıdaki **§12.1**’de.

**§12.1 — app.monitrang.com same-origin /hub/ uygulaması (25 Ocak 2026)**

Production’da **app.monitrang.com** trafiği host Nginx değil, **Docker**’daki **nginx** container’ı (proje: **mng_common**) tarafından serve edilir. 80/443 `docker-proxy` ile bu container’a gider; `systemctl nginx` “inactive (dead)” olduğu için host Nginx config’i kullanılmaz.

**Yapılanlar:**

1. **Nginx config — `/hub/` proxy**
   - **Kullanılan dosya:** `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf` (repo ve sunucu aynı path: `…/mng_common/nginx/conf.d/`).
   - app.monitrang.com HTTPS server bloğunda, “# Frontend (MngUI)” / `location /` öncesine **`location /hub/`** eklendi:
     - `proxy_pass http://mnggateway:5000/hub/` (container ağında Gateway)
     - WebSocket: `Upgrade`, `Connection "upgrade"`, `proxy_read_timeout 86400`.
   - **Config reload:** `docker exec nginx nginx -s reload` (host’ta `systemctl reload nginx` kullanılmaz).
   - Repo’daki `monitrang.conf` bu haliyle güncellendi; sunucuya `scp` ile kopyalanıp reload yapıldı.

2. **MngUI same-origin build**
   - **Sunucuda .env:** `GATEWAY_URL=https://app.monitrang.com`, `HUB_URL=` (boş). Yedek: `.env.bak-mngui-hub`.
   - **Sunucuda docker-compose:** mngui servisine build args eklendi (repoda zaten vardı):
     - `args:` → `GATEWAY_URL: ${GATEWAY_URL:-https://app.monitrang.com}`, `HUB_URL: ${HUB_URL:-}`.
   - **Build/up:**  
     `docker compose -f docker-compose.production.yml build --no-cache mngui`  
     `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`
   - Sonuç: MngUI client tarafında Hub adresi `https://app.monitrang.com/hub` (same-origin).

**Özet:** app.monitrang.com’u dinleyen Nginx, **mng_common**’daki Docker nginx’tir; `/hub/` config’i **monitrang.conf** içine eklendi, reload bu container’a yapılır. MngUI aynı origin üzerinden Hub’a bağlanacak şekilde `.env` + build args + yeniden build/up ile güncellendi.

**Eski not (host Nginx):** Daha önce `/etc/nginx/sites-available/monitrang` ve `docs/deployment/nginx-hub-snippet.conf` ile yapılan tarif, bu sunucuda geçerli değildir; app.monitrang.com bu dosyayı kullanmıyor. Snippet referansı yalnızca aynı blok için örnek olarak bırakılabilir.

**§12.2 — MngUI Hub URL build-time varsayılanı (25 Ocak 2026)**

§12.1 uygulandıktan sonra tarayıcı hâlâ **`http://localhost:5020/ws/negotiate`** adresine istek atıyor ve CORS hatası alınıyordu. Origin `https://app.monitrang.com` idi; yani client Hub adresi olarak same-origin değil, doğrudan Hub portunu kullanıyordu.

- **Sebep:** `Mng.Ui/nuxt.config.ts` içinde `runtimeConfig.public.hubUrl` şöyle tanımlıydı:  
  `hubUrl: process.env.HUB_URL || 'http://localhost:5020'`  
  Production’da `HUB_URL=` (boş) verildiğinde `process.env.HUB_URL` falsy sayıldığı için **her zaman** varsayılan `'http://localhost:5020'` kullanılıyordu. `stores/hub.ts` tarafında “hubUrl yoksa gatewayUrl + '/hub' kullan” mantığı doğruydu; ancak build sırasında hubUrl hiç boş kalmadığı için client hep localhost:5020’e gidiyordu.
- **Yapılan (repo):** `nuxt.config.ts` içinde `hubUrl` şu şekilde güncellendi:  
  `hubUrl: (process.env.HUB_URL && process.env.HUB_URL.trim()) ? process.env.HUB_URL : ''`  
  Böylece `HUB_URL` boş veya sadece boşluk olduğunda `hubUrl` gerçekten `''` olur; `hub.ts` de `config.public.hubUrl` falsy gördüğü için `config.public.gatewayUrl + '/hub'` yani `https://app.monitrang.com/hub` kullanır.
- **Development:** Lokal geliştirmede Hub’a doğrudan bağlanmak için `.env` içinde `HUB_URL=http://localhost:5020` tanımlı olmalı.
- **Dosya:** `Mng.Ui/nuxt.config.ts`
- **Sunucuda uygulama (25 Ocak 2026):** Güncel `nuxt.config.ts` sunucuya `scp` ile kopyalandı; ardından  
  `docker compose -f docker-compose.production.yml build --no-cache mngui`  
  `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`  
  çalıştırıldı. Sonrasında client istekleri `https://app.monitrang.com/hub/ws/...` adresine gitmeli.

**§12.3 — MngUI Gateway URL production varsayılanı (25 Ocak 2026)**

§12.2 uygulandıktan sonra tarayıcı bu kez **`https://localhost:5040/hub/ws/negotiate`** adresine istek atıyor ve **net::ERR_CERT_AUTHORITY_INVALID** alınıyordu. Yani hubUrl boştu (gateway + '/hub' kullanılıyordu) ancak **gatewayUrl** nuxt.config varsayılanı olan **`'https://localhost:5040'`** ile kalıyordu.

- **Sebep:** `nuxt.config.ts` içinde `gatewayUrl: process.env.GATEWAY_URL || 'https://localhost:5040'` tanımlıydı. Build sırasında `GATEWAY_URL` ortam değişkeni bir şekilde geçmezse (farklı build ortamı, cache, compose env yüklenmemesi vb.) her zaman bu varsayılan kullanılıyordu.
- **Yapılan (repo):** `gatewayUrl` için production’da güvenli varsayılan eklendi:
  - `GATEWAY_URL` dolu/geçerliyse → aynen kullanılır.
  - **Production** (`NODE_ENV === 'production'`) ve `GATEWAY_URL` boş/verilmediyse → **`''`** (boş string). Hub store bu durumda `hubBaseUrl = '' + '/hub'` → **`/hub`** (relative) üretir; bağlantı adresi **`/hub/ws?...`** olur, sayfa `https://app.monitrang.com` üzerindeyken otomatik same-origin (**https://app.monitrang.com/hub/ws**) olur.
  - **Development**’ta `GATEWAY_URL` yoksa → `'https://localhost:5040'` (önceki davranış korunur).
- **Dosya:** `Mng.Ui/nuxt.config.ts`
- **Sunucuda uygulama (25 Ocak 2026):** Güncel `nuxt.config.ts` sunucuya `scp` ile kopyalandı; ardından  
  `docker compose -f docker-compose.production.yml build --no-cache mngui`  
  `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`  
  çalıştırıldı. Production build’de `GATEWAY_URL` geçmese bile client artık relative `/hub` kullanacağı için same-origin çalışır.

**§12.4 — Nginx → Gateway: 502, 401 ve WebSocket (25 Ocak 2026)**

§12.1–§12.3 sonrası client `https://app.monitrang.com/hub/...` kullanıyordu; önce **502 Bad Gateway**, ardından **401 Unauthorized**, son aşamada WebSocket “connection could not be found” benzeri hata alındı. Aşağıdaki Nginx değişiklikleri uygulanınca Hub bağlantısı tamamlandı.

- **502 Bad Gateway**
  - **Sebep:** MngGateway port 5000’de **HTTPS** dinliyor (“Kestrel configured for HTTPS on port 5000”). Nginx ise **http://mnggateway:5000** kullandığı için upstream ile anlaşamıyordu.
  - **Yapılan:** `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf` içinde MngGateway’e giden tüm proxy’ler **HTTPS** olacak şekilde güncellendi ve `proxy_ssl_verify off` eklendi:
    - **app.monitrang.com** → `location /hub/`: `proxy_pass https://mnggateway:5000/hub/`
    - **api.monitrang.com** → `location /`: `proxy_pass https://mnggateway:5000`
    - **api.monitrang.com** → `location /health`: `proxy_pass https://mnggateway:5000/health`

- **401 Unauthorized**
  - **Sebep:** Gateway’de Ocelot `/hub/ws/...` rotası `AuthenticationOptions: "Bearer"` ile korunuyor; JWT’nin **Authorization: Bearer &lt;token&gt;** header’ında gelmesi gerekiyor. MngUI ise token’ı yalnızca **query string** ile gönderiyor (`/hub/ws?access_token=...`).
  - **Yapılan:** app.monitrang.com için `location /hub/` bloğunda query’deki `access_token` varsa **Authorization** header’ına taşındı:
    - `set $hub_auth $http_authorization;`
    - `if ($arg_access_token != "") { set $hub_auth "Bearer $arg_access_token"; }`
    - `proxy_set_header Authorization $hub_auth;`
  - Böylece client token’ı sadece query’de gönderse bile Gateway’e `Authorization: Bearer …` iletiliyor.

- **WebSocket**
  - Aynı `location /hub/` blokta `proxy_set_header Upgrade $http_upgrade`, `proxy_set_header Connection "upgrade"` ve `proxy_read_timeout 86400` zaten vardı. 502 ve 401 giderilince negotiate + WebSocket akışı tamamlandı.

- **Dosya:** `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf`
- **Sunucuda:** Güncel `monitrang.conf` kopyalanıp `docker exec nginx nginx -t` ve `docker exec nginx nginx -s reload` çalıştırıldı.

**§12.5 — Profil fotoğrafı 401 (GET /api/keeper/user/…/photo) (25 Ocak 2026)**

Profil fotoğrafı isteği **401 Unauthorized** alıyordu:

- **Belirti:** Tarayıcı konsolunda  
  `GET https://app.monitrang.com/api/keeper/user/{userId}/photo?t=…` ve  
  `GET https://app.monitrang.com/api/keeper/user/{userId}/photo`  
  için 401.
- **Sebep:** Profil fotoğrafı `<img :src="photoUrl">` ile gösteriliyordu. `<img src>` isteklerinde tarayıcı **Authorization** header’ı gönderilmez. MngUI production’da statik SPA olarak serve edildiği için istekler doğrudan mngui container’ındaki Nginx’e gider; `Mng.Ui/nginx.conf` içinde `/api/keeper/` → `proxy_pass http://mngkeeper:5001/api/` ve **sadece** `proxy_set_header Authorization $http_authorization` iletilir. Token gelmediği için Keeper 401 döner. (Development’ta Nuxt server route `/api/keeper/[...path]` cookie’den `access_token` alıp Authorization eklediği için sorun yoktu.)
- **Yapılan (repo):**
  - **AvatarDisplay.vue:** Profil fotoğrafı artık `<img src="url">` yerine **token’lı fetch + blob URL** ile gösteriliyor. `useAuthStore().accessToken` veya `getAccessToken()` (apiService) ile token alınır; `fetch(photoUrl, { headers: { Authorization: 'Bearer ' + token }, credentials: 'include' })` ile binary alınıp `URL.createObjectURL(blob)` ile img `src`’ye veriliyor. Böylece hem development hem production’da Authorization header gider, 401 oluşmaz. `onUnmounted`’ta `URL.revokeObjectURL` ile sızıntı önlenir.
  - **users/details/[id].vue:** Kullanıcı detay sayfasındaki büyük profil fotoğrafı alanı, aynı 401’e maruz kalan `<img :src="getPhotoUrl()">` yerine **AvatarDisplay** kullanacak şekilde güncellendi; böylece bu sayfada da token’lı blob ile fotoğraf yüklenir.
- **Dosyalar:** `Mng.Ui/components/apps/profile/AvatarDisplay.vue`, `Mng.Ui/pages/apps/users/details/[id].vue`
- **Sunucuda:** Değişiklikler MngUI build’ine dahil; yeni image ile `docker compose -f docker-compose.production.yml build --no-cache mngui` ve `up -d --no-deps --force-recreate mngui` yeterli.

**§12.6 — Profil foto 401 ek önlemler + Event Mesajları / Hub log temizliği (25 Ocak 2026)**

§12.5 sonrası 401 devam ediyorsa veya konsolda gereksiz loglar görünüyorsa uygulanan ek düzeltmeler:

- **Profil foto 401:**
  - **AvatarDisplay.vue:** (1) `photoUrl` tam URL (`https://api...` vb.) gelirse sadece path alınıp **aynı-origin** `/api/keeper/...` path’ine çevriliyor; fetch her zaman uygulama origin’ine gidiyor. (2) Foto yüklemeden önce `authStore.ensureValidToken()` çağrılıyor; token süresi dolmuşsa yenilenip sonra fetch atılıyor. (3) Fetch artık `async/await` ile yapılıyor, hata durumunda sessizce initials gösteriliyor.
- **Event Mesajları / Hub logları:** MngHub bağlantısı çalışsa bile tarayıcı konsolunda `[Hub] …`, `[Events Page] …` vb. mesajlar görünüyordu. Tüm bu **console.log / console.warn** çağrıları yalnızca **development** ortamında çalışacak şekilde `import.meta.dev` ile sarmalandı; production build’de konsol temiz kalır. Events sayfasındaki connect/handler/subscription log’ları kaldırıldı.
- **Dosyalar:** `Mng.Ui/components/apps/profile/AvatarDisplay.vue`, `Mng.Ui/stores/hub.ts`, `Mng.Ui/pages/apps/events/index.vue`

**§12.8 — "Authorization header missing" / DG'de var Keeper'da yok:** DG istekleri fetchFromDataGateway() ile gidiyor (token ekleniyor); Keeper'a bazı yerler doğrudan $fetch kullanıyordu. Lisans Yönetimi fetchFromMngKeeper ve token'lı fetch ile güncellendi. Dosya: `Mng.Ui/pages/apps/license-management/index.vue`

**§12.9 — MngUI build hatası: "The symbol url has already been declared" (25 Ocak 2026)**

`docker compose build --no-cache mngui` sırasında esbuild "The symbol 'url' has already been declared" veriyordu. **Sebep:** Aynı kapsamda iki kez `const url`. **Düzeltmeler:** (1) **AvatarDisplay.vue:** computed içinde `url` → `raw`, loadPhoto içinde `url` → `photoPath`. (2) **license-management/index.vue:** `downloadLicense` içinde iki `const url` vardı; biri `requestUrl`, diğeri `blobUrl` yapıldı. **Dosyalar:** `Mng.Ui/components/apps/profile/AvatarDisplay.vue`, `Mng.Ui/pages/apps/license-management/index.vue`

**§12.7 — Profil foto: Token header’da gitmediği durumda query fallback (25 Ocak 2026)**

Network’te `GET .../api/keeper/user/.../photo` isteğinde **Authorization** header ve **access_token** query’si görünmüyorsa (token hiç gitmiyorsa) için yapılanlar:

- **AvatarDisplay.vue:** Token artık hem **header** hem **query** ile gidiyor. Fetch URL’i `url + '?access_token=' + encodeURIComponent(token)` (veya mevcut query varsa `&access_token=...`) olacak şekilde genişletildi. Böylece bazı ortamlarda header gitmese bile token query’de olur.
- **Mng.Ui/nginx.conf:** `/api/keeper/` location’ında `$http_authorization` boşsa ve `$arg_access_token` doluysa **Authorization: Bearer $arg_access_token** header’ı ekleniyor (`set $keeper_auth ...`, `if ($arg_access_token != "") { set $keeper_auth "Bearer $arg_access_token"; }`, `proxy_set_header Authorization $keeper_auth`). Keeper’a giden istekte token ya gelen header’dan ya da query’den taşınmış olur.
- **Dosyalar:** `Mng.Ui/components/apps/profile/AvatarDisplay.vue`, `Mng.Ui/nginx.conf`
- **Sunucuda:** MngUI image yeniden build edilmeli (nginx.conf image’a gömülü); ardından `docker compose -f docker-compose.production.yml build --no-cache mngui` ve `up -d --no-deps --force-recreate mngui`.

### 13. MngUI Domain Yönetimi — Yedek alma 405 (Method Not Allowed) (25 Ocak 2026)

- **Belirti:** Domain Yönetimi sayfasında "Yedek alma" butonuna basıldığında  
  `POST https://app.monitrang.com/api/admin/backup/domain/{domainName}` → **405 Method Not Allowed**  
  Konsol: `Failed to create backup: FetchError: [POST] "/api/admin/backup/domain/…": 405`
- **Sebep:** Production’da MngUI statik build olarak mngui container’ındaki Nginx ile sunuluyor; Nuxt server çalışmıyor. İstek doğrudan bu Nginx’e gidiyor. `Mng.Ui/nginx.conf` içinde `/api/admin/` için tanımlı location yoktu; sadece `/api/auth/`, `/api/keeper/`, `/api/data/`, `/api/v1/` vardı. Bu yüzden `POST /api/admin/backup/domain/…` hiçbir API location’ına düşmeyip `location /` (try_files) ile karşılanıyordu; POST bu blokta işlenemediği için 405 dönüyordu. İstek MngAdmin’e hiç ulaşmıyordu.
- **Yapılan (repo):** `Mng.Ui/nginx.conf` içine **`/api/admin/`** için location eklendi:
  - `location /api/admin/` → `proxy_pass http://mngadmin:5080/api/v1/;`
  - Böylece `POST /api/admin/backup/domain/meral2` → `POST http://mngadmin:5080/api/v1/backup/domain/meral2` olur (MngAdmin BackupController bu path’i kabul ediyor).
  - Diğer API location’ları ile aynı header’lar: Host, X-Real-IP, X-Forwarded-For, X-Forwarded-Proto, **Authorization** ($http_authorization), CORS pass-through.
- **Dosya:** `Mng.Ui/nginx.conf`
- **Sunucuda:** MngUI image yeniden build edilmeli (nginx.conf image’a gömülü):  
  `docker compose -f docker-compose.production.yml build --no-cache mngui`  
  `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`

### 14. Domain Yönetimi — Yedek alma 400 (Failed to upload backup to MinIO) (25 Ocak 2026)

§13 uygulandıktan sonra istek MngAdmin’e ulaşıyordu ancak **400 Bad Request** dönüyordu; response body:  
`{"error": "Failed to upload backup to MinIO: mng-{domain}/backups/mongodb/…"}`

- **Sebep:** MngAdmin, MinIO bağlantı ayarlarını **MngAdminSettings:MinIO** (yani `MngAdminSettings__MinIO__*` env’leri) üzerinden okuyor. Production compose’da mngadmin için sadece `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY` tanımlıydı; bunlar ASP.NET Core’da `MngAdminSettings:MinIO` altına bind olmuyor. Uygulama appsettings varsayılanlarıyla (örn. localhost) MinIO’ya gitmeye çalışıyor, yükleme başarısız oluyordu.
- **Yapılan (repo):** `ApplicationResources/mng_apps/docker-compose.production.yml` içinde **mngadmin** servisi için MinIO env’leri doğru config anahtarına bağlanacak şekilde güncellendi:
  - `MngAdminSettings__MinIO__Endpoint=${MINIO_ENDPOINT:-minio:9000}`
  - `MngAdminSettings__MinIO__AccessKey=${MINIO_ACCESS_KEY}`
  - `MngAdminSettings__MinIO__SecretKey=${MINIO_SECRET_KEY}`
  - `MngAdminSettings__MinIO__UseSSL=${MINIO_USE_SSL:-false}`
  - Değerler yine `.env`’deki `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY` kullanılıyor; sadece env **isimleri** MngAdmin’in okuduğu bölüme denk getirildi.
- **Doğrulamalar:** (1) Sunucuda `.env` içinde `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY` tanımlı olmalı. (2) MinIO container’ı **mng_common_mng_network** üzerinde olmalı (mngadmin ile aynı ağ); böylece mngadmin `minio:9000` adresine erişebilir.
- **Sunucuda uygulama (25 Ocak 2026):** Compose’da mngadmin env satırları `MngAdminSettings__MinIO__*` olacak şekilde güncellendi; `docker compose -f docker-compose.production.yml up -d --no-deps mngadmin` ile mngadmin yeniden oluşturuldu. Yedek: `docker-compose.production.yml.bak-minio`.
- **Dosya:** `ApplicationResources/mng_apps/docker-compose.production.yml`

### 15. Chatbot — POST /api/llm/v1/chatbot/chat 405 (Method Not Allowed) (25 Ocak 2026)

- **Belirti:** Chatbot’a (https://app.monitrang.com) “Selam” yazıldığında  
  `POST https://app.monitrang.com/api/llm/v1/chatbot/chat` → **405 Method Not Allowed**
- **Sebep:** Production’da MngUI statik build; Nuxt server çalışmıyor. İstek mngui nginx’ine gidiyor, `Mng.Ui/nginx.conf` içinde `/api/llm/` location’ı yoktu; `location /` (try_files) POST’u kabul etmediği için 405 dönüyordu.
- **Yapılan (repo):**
  1. **Mng.Ui/nginx.conf:** `location /api/llm/` → `proxy_pass http://mngllm:5030/api/;` eklendi. Böylece `POST /api/llm/v1/chatbot/chat` → `POST http://mngllm:5030/api/v1/chatbot/chat` (MngLLM ChatbotController). Authorization ve standart proxy header’ları iletiliyor.
  2. **Mng.Ui/services/apiService.ts:** `fetchFromMngLLM` artık production’da token’ı header’da gönderiyor: `getAccessToken()` + `Authorization: Bearer ${token}` (ilk istek ve 401 retry’da). Nginx `proxy_set_header Authorization $http_authorization` ile MngLLM’e iletiyor.
- **Dosyalar:** `Mng.Ui/nginx.conf`, `Mng.Ui/services/apiService.ts`
- **Sunucuda:** Değişiklikler mngui imajında; build + up gerekir:  
  `docker compose -f docker-compose.production.yml build --no-cache mngui`  
  `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`  
  Sonrasında chatbot’ta “Selam” vb. ile test edin.

---

## 26 Ocak 2026 – Chatbot tamamlama (500, Ollama, 401)

### Bağlam

- §15 (Chatbot 405) repoda vardı; production’da chatbot’a “Selam” denendiğinde sırasıyla **500**, “yanıt üretemiyorum” ve **401** alındı. Aşağıdaki adımlarla chatbot çalışır hale getirildi; sorulara mantıklı cevaplar veriyor.

### §16. MngLLM 500 — AllowAnonymousInDevelopment policy not found

- **Belirti:** Chatbot isteği MngLLM’e ulaşıyordu; backend log:  
  `System.InvalidOperationException: The AuthorizationPolicy named: 'AllowAnonymousInDevelopment' was not found.`
- **Sebep:** Bu politika yalnızca `if (builder.Environment.IsDevelopment())` bloğunda tanımlanıyordu. Production’da politika hiç eklenmediği için `[Authorize(Policy = "AllowAnonymousInDevelopment")]` kullanan ChatbotController 500 veriyordu.
- **Yapılan (repo):** `MngLLM/Presentation/MngLLM.Api/Program.cs` içinde politikayı **her ortamda** kaydettik:
  - Development: `RequireAssertion(_ => true)` (anonim)
  - Production: `RequireAuthenticatedUser()` (JWT zorunlu).
- **Sunucuda:** `Program.cs` scp ile alınıp `docker compose -f docker-compose.production.yml build mngllm` ve `up -d --no-deps mngllm` çalıştırıldı.

### §17. Chatbot “Üzgünüm, şu anda yanıt üretemiyorum”

- **Belirti:** İstek 200 dönüyordu; cevap metni “Üzgünüm, şu anda yanıt üretemiyorum. Lütfen daha sonra tekrar deneyin veya dokümantasyonu kontrol edin.”
- **Sebep:** MngLLM yanıt için **Ollama**’ya (`http://ollama:11434`) gidiyordu; production compose’da **ollama** servisi tanımlı değildi. LLM çağrısı başarısız olunca bu fallback mesajı dönüyordu.
- **Kaynak kontrolü:** Sunucuda `free -h`, `df -h`, `nproc`, `uptime` ile kaynaklara bakıldı; ~8.4 GB available RAM, 92 GB boş disk, 8 çekirdek — Ollama için yeterli.
- **Yapılan (repo):** `ApplicationResources/mng_apps/docker-compose.production.yml` içine **ollama** servisi eklendi:
  - Image: `ollama/ollama:latest`, volume: `ollama_data`, ağ: `mng_common_mng_network`
  - Limit: 4 GB RAM, 4 CPU; port dışarı açılmadı (sadece mngllm erişir).
  - `mngllm` `depends_on: ollama` olacak şekilde güncellendi.
  - `volumes:` bölümüne `ollama_data` eklendi.
- **Sunucuda adımlar:**
  1. Güncel `docker-compose.production.yml` scp ile alındı.
  2. `docker compose -f docker-compose.production.yml up -d ollama`
  3. `docker exec ollama ollama pull qwen2.5:3b`
  4. `docker compose -f docker-compose.production.yml up -d --no-deps mngllm`

### §18. Chatbot 401 — MngLLM JWT; domain değişken, realm sabit değil

- **Belirti:** “Selam” yazıldığında **401 Unauthorized**.
- **Sebep:** MngLLM JWT doğrulamasında Authority olarak MngKeeper kullanılıyordu; token ise Keycloak’tan geliyor ve MngKeeper payload’a `domain_name` ekleyip imzayı güncellemiyor (imza geçersiz). Ayrıca **realm/domain değişkendir**; token’daki `domain_name` alanından gelir (DataGateway ile aynı model). Sabit “meral” realm varsaymak yanlıştı.
- **Yapılan (repo):**
  1. **MngLLMSettings:** `Jwt.Authority` alanı eklendi (isteğe bağlı).
  2. **AuthConfig (MngLLM):** Authority **sadece** `Jwt.Authority` doluysa set ediliyor; boşsa set edilmiyor. Böylece metadata isteği yapılmaz; token sadece parse edilir, domain_name token içinden okunur (DG gibi). `ValidateIssuerSigningKey = false` ve `SignatureValidator` ile mevcut davranış korunur.
  3. **docker-compose.production.yml:**  
     `MngLLMSettings__Jwt__Authority=${MNGLLM_JWT_AUTHORITY:-}` (varsayılan boş).
  4. **env.example:** Açıklama eklendi; çoğu kurulumda boş bırakılır, realm değişkendir.
- **Sunucuda adımlar (scp + docker):**
  - `scp` ile sunucuya: `AuthConfig.cs`, `MngLLMSettings.cs`, `docker-compose.production.yml`
  - `cd /root/MonitraNG/ApplicationResources/mng_apps`
  - `docker compose -f docker-compose.production.yml build mngllm`
  - `docker compose -f docker-compose.production.yml up -d --no-deps mngllm`
- **Dosyalar:** `MngLLM/Presentation/MngLLM.Api/Config/AuthConfig.cs`, `MngLLM/Core/MngLLM.Application/Configuration/MngLLMSettings.cs`, `ApplicationResources/mng_apps/docker-compose.production.yml`, `ApplicationResources/mng_apps/env.example`

### §19. Chatbot §15 (405) production deploy

- **Bağlam:** §15 (nginx `/api/llm/` + apiService token) repoda vardı; production’a alınması gerekiyordu.
- **Sunucuda adımlar:**
  1. `scp` ile `Mng.Ui/nginx.conf` ve `Mng.Ui/services/apiService.ts` sunucuya kopyalandı.
  2. `docker compose -f docker-compose.production.yml build --no-cache mngui`
  3. `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`
- Böylece `/api/llm/` istekleri mngui nginx’ten MngLLM’e gidiyor ve token header’da iletilmiş oluyor.

### Sonuç (26 Ocak 2026)

- **Chatbot çalışıyor:** https://app.monitrang.com üzerinde giriş yapıldıktan sonra chatbot’a “Selam” vb. yazıldığında mantıklı cevaplar alınıyor.
- **Özet adımlar (yeni ortam / tekrar deploy için):**
  - MngLLM: `Program.cs` (AllowAnonymousInDevelopment her ortamda), `AuthConfig.cs` + `MngLLMSettings.cs` (JWT Authority boş, domain token’dan), compose’da ollama servisi ve `MngLLMSettings__Jwt__Authority=${MNGLLM_JWT_AUTHORITY:-}`.
  - Sunucuda: ollama up → `ollama pull qwen2.5:3b` → mngllm build/up; gerekirse mngui build/up (§15 dosyaları ile).

---

## 26 Ocak 2026 – MngScheduler (zamanlanmış job'lar)

### Bağlam

- Belirli bir saatte (örn. 16:07) çalışması gereken system job tetiklenmiyordu. Log'da `Retrieved 0 active system jobs (filtered from 0)` görülüyordu; MongoDB `mngkeeper.@scheduled_jobs` içinde ilgili job dökümanı mevcuttu. Ayrıca kullanıcı job için `isActive: true` yaptığında kısa süre sonra tekrar `false` oluyordu.

### §20. Job'ların "aktif" listesine alınmaması (jobType / isActive filtresi)

- **Sebep:** `GetActiveJobsAsync` filtresi yalnızca C# enum değeri (`JobType.System = 0`) ve `bool true` ile eşleşiyordu. MongoDB'de bazı dökümanlarda `jobType: "System"` (string) veya `isActive: 1` (int) olabiliyordu; bu yüzden mevcut job "aktif" sayılmıyordu.
- **Yapılan (repo):** `SystemJobRepository.GetActiveJobsAsync` içinde filtre gevşetildi:
  - `jobType` için hem `0` (enum) hem `"System"` (string) kabul ediliyor.
  - `isActive` için hem `true` hem `1` kabul ediliyor.
- **Dosya:** `MngScheduler/Infrastructure/MngScheduler.Persistence/Repositories/SystemJobRepository.cs`

### §21. isActive manuel true yapılsa bile tekrar false'a dönmesi

- **Belirti:** Kullanıcı MongoDB'de ilgili job için `isActive: true` yapıyordu; bir süre sonra yine `false` oluyordu.
- **Sebep:** `GetActiveJobsAsync` her sync'te (yaklaşık 30 sn) job'ları okuyup `ShouldExecute(now)` ile kontrol ediyordu. `ShouldExecute`, **ExpireDate** geçmişse veya **TotalExecutionCount >= MaxExecutionCount** ise nesnede `IsActive = false` set ediyordu. Bu job'lar "jobsToDeactivate" listesine alınıp `UpdateJobAsync` ile MongoDB'ye tam döküman (içinde `isActive: false`) yazılıyordu. Yani Scheduler her sync'te "süresi dolmuş / limiti dolmuş" gördüğü job'ın `isActive` değerini DB'de kalıcı olarak false yapıyordu.
- **Yapılan (repo):** Sync sırasında DB'ye `isActive` yazan "auto-deactivate + UpdateJobAsync" akışı kaldırıldı. ExpireDate veya MaxExecutionCount nedeniyle bu turda çalıştırılmayacak job'lar yalnızca **o turda atlanıyor** (validJobs'a eklenmiyor); MongoDB'deki `isActive` **değiştirilmiyor**. Böylece kullanıcı `expireDate` veya `maxExecutionCount`'u düzelttikten sonra `isActive: true` yaparsa değer kalıcı kalır.
- **Not:** Job **gerçekten çalıştıktan sonra** execution limit'e ulaşırsa, HttpJob içinde `CheckExecutionLimit()` ve `UpdateJobAsync` ile `isActive: false` yazılmaya devam eder (tasarlanan "limit dolunca kendini kapat" davranışı).
- **Dosya:** `MngScheduler/Infrastructure/MngScheduler.Persistence/Repositories/SystemJobRepository.cs`

### Sunucuda uygulama (MngScheduler)

- Değişen kod yalnızca `SystemJobRepository.cs`; MngScheduler image'ı yeniden build edilip container güncellenmeli:
  1. `cd /root/MonitraNG/ApplicationResources/mng_apps`
  2. `docker compose -f docker-compose.production.yml build mngscheduler`
  3. `docker compose -f docker-compose.production.yml up -d --no-deps mngscheduler`
- İsterseniz değişen dosyayı `scp` ile sunucuya kopyalayıp aynı build/up komutları çalıştırılabilir.

### Sonuç (MngScheduler)

- Job'lar cron zamanlarında tetiklenir. Kullanıcı tarafında `expireDate` / `maxExecutionCount` düzeltilip `isActive: true` yapıldığında Scheduler bir daha bunu false'a çevirmez. Yeni ortam veya `deploy-services` sonrası MngScheduler için bu log'taki §20–§21 ve "Sunucuda uygulama (MngScheduler)" adımları uygulanabilir.

---

## 26 Ocak 2026 – DataGateway MinIO + MngUI dosya önizleme/indirme

### §22. DataGateway – file field upload (Connection refused localhost:9090, bucket ensure failed)

- **Belirti:** Dataset’te alan tipi file olan bir alana resim eklenirken hata: `Failed to upload file for field '...': Failed to ensure bucket exists: mng-meral`. DG log: `Connection refused (localhost:9090)`.
- **Sebep:** MngDataGateway MinIO’ya **localhost:9090** ile bağlanmaya çalışıyordu. Production compose’da mngdatagateway için MinIO env tanımlı değildi; uygulama `appsettings.json` içindeki varsayılanı (localhost:9090, üstelik Console portu; S3/API portu 9000) kullanıyordu. Konteyner içinde localhost MinIO konteynerine erişmiyor.
- **Yapılan (repo):**
  1. **docker-compose.production.yml** — mngdatagateway `environment` bölümüne MinIO env’leri eklendi (MngAdmin/MngKeeper ile aynı değişkenler):
     - `MngDataGatewaySettings__FileStorage__Minio__Endpoint=${MINIO_ENDPOINT:-minio:9000}`
     - `MngDataGatewaySettings__FileStorage__Minio__AccessKey=${MINIO_ACCESS_KEY}`
     - `MngDataGatewaySettings__FileStorage__Minio__SecretKey=${MINIO_SECRET_KEY}`
     - `MngDataGatewaySettings__FileStorage__Minio__UseSSL=${MINIO_USE_SSL:-false}`
  2. **MngDataGateway.Api/appsettings.json** — `FileStorage:Minio:Endpoint` varsayılanı `localhost:9090` → `minio:9000` yapıldı.
- **Dosyalar:** `ApplicationResources/mng_apps/docker-compose.production.yml`, `MngDataGateway/Presentation/MngDataGateway.Api/appsettings.json`
- **Sunucuda:** `.env` içinde `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY` tanımlı olmalı (zaten MngAdmin/MngKeeper için kullanılıyor). Ardından:
  - `docker compose -f docker-compose.production.yml up -d --no-deps mngdatagateway`

### §23. MngUI – file preview/download 401 (Authorization header yok)

- **Belirti:** Dosya alanında önizleme veya indirme isteğinde `GET https://app.monitrang.com/api/data/v1/files/download?filePath=...` → **401 Unauthorized**. Network sekmesinde ilgili istekte **Authorization** header’ı yok.
- **Sebep:** (1) `fetchBlobFromDataGateway` blob isteğinde token göndermiyordu. (2) Thumbnail’lar `<img :src="preview.url">` ile yükleniyordu; tarayıcı `img` isteğinde hiçbir özel header (Authorization dahil) göndermiyor. URL’e token eklesek bile, ilk 401 alan istek büyük ihtimalle bu thumbnail isteğiydi.
- **Yapılan (repo):**
  1. **apiService.ts** — `fetchBlobFromDataGateway`: DG’nin diğer istekleriyle aynı mantık; `fetchFromDataGateway` ile aynı URL üretimi, token `getAccessToken()` ile alınıp **native `fetch(fullUrl, { headers: { Authorization: Bearer … } })`** ile gönderiliyor. Blob yanıtı `res.blob()` ile dönülüyor.
  2. **FileUploadField.vue** — Resim thumbnail’ları artık doğrudan sunucu URL’i ile değil, **fetchBlobFromDataGateway** ile (token’lı istek) indirilip `URL.createObjectURL(blob)` ile img src’ye veriliyor. Böylece sunucuya giden tek istek Authorization’lı fetch. Blob URL’ler `revokeBlobUrlsInPreviews` ve `onUnmounted` ile serbest bırakılıyor.
  3. **nginx.conf** — `/api/data/` location’ında `$arg_access_token` doluysa `Authorization: Bearer $arg_access_token` (query → header) uygulandı; img/link gibi header gönderilemeyen yerlere yönelik fallback.
- **Dosyalar:** `Mng.Ui/services/apiService.ts`, `Mng.Ui/components/apps/automated-forms/FileUploadField.vue`, `Mng.Ui/nginx.conf`
- **Sunucuda uygulama:** Bu üç dosya MngUI image’ına gömülü; build + up gerekir:
  - İsterseniz scp ile:  
    `apiService.ts` → `…/Mng.Ui/services/`  
    `FileUploadField.vue` → `…/Mng.Ui/components/apps/automated-forms/`  
    `nginx.conf` → `…/Mng.Ui/`
  - Ardından:  
    `cd /root/MonitraNG/ApplicationResources/mng_apps`  
    `docker compose -f docker-compose.production.yml build --no-cache mngui`  
    `docker compose -f docker-compose.production.yml up -d --no-deps --force-recreate mngui`

### Sonuç (DG MinIO + dosya önizleme)

- File field ile yükleme çalışır (DG doğru MinIO endpoint’i kullanır).
- Önizleme ve indirme 401 almaz; tüm dosya istekleri Authorization header veya (fallback) query’deki access_token ile gider.

---

## Yapılacak / Devam Eden

### Kaldığımız yer (26 Ocak 2026)

- **Chatbot:** Tamamlandı. §15–§19 ile production’da çalışıyor; sorulara mantıklı cevaplar veriyor.
- **MngScheduler:** Tamamlandı. §20–§21 ile job'lar cron zamanlarında tetikleniyor; `isActive` manuel true yapıldığında Scheduler tarafından tekrar false'a çekilmiyor.
- **DataGateway MinIO + dosya önizleme/indirme:** Tamamlandı. §22 ile file upload (MinIO endpoint); §23 ile MngUI’da dosya önizleme/indirme 401’i (Authorization + thumbnail blob URL) çözüldü.
- **İleride:** Yeni ortamda veya `deploy-services` sonrası: Ollama + MngLLM §16–§19; MngScheduler §20–§21; DG MinIO (§22) ve MngUI dosya önizleme (§23) için compose env + MngUI build/up.

---

## Notlar

- Sunucudaki `docker-compose.production.yml` farkı: `git status` çıktısında `M ApplicationResources/mng_apps/docker-compose.production.yml` görünebilir; bu, yukarıdaki **healthcheck**, **mngdomainui SERVER_* / SERVER_SCHEDULER_URL**, **MngKeeperSettings__License__MasterKey**, **§8 Keycloak PathPrefix**, **§14 MngAdmin MinIO (MngAdminSettings__MinIO__*)**, **§17 ollama servisi + volumes**, **§18 MngLLMSettings__Jwt__Authority**, **§22 MngDataGateway FileStorage MinIO (MngDataGatewaySettings__FileStorage__Minio__*)** ve (sunucuda elle eklenmişse) **mngui build args (GATEWAY_URL, HUB_URL)** düzenlemelerinden kaynaklanıyor.
- GitLab’a push yalnızca kullanıcı onayı ile yapılacak.

---

**Portainer'da MngKeeper "starting" görünmesi:** Portainer'da MngKeeper "starting" durumunda kalıp, container loglarında uygulamanın başladığı görülebilir. Bu genelde **healthcheck** ile ilgilidir: Docker, healthcheck başarılı olana kadar container'ı "starting" sayar. Uygulama logda düzgün başladıysa ve diğer servisler MngKeeper'a erişebiliyorsa pratikte çalışıyor demektir; Nginx healthcheck sonucuna bakmıyorsa trafik akmaya devam eder. Kalıcı "starting" veya "unhealthy" ise sunucudaki mngkeeper healthcheck'inin **HTTP** kullandığından emin olun: `curl -f http://localhost:5001/api/version/short` (HTTPS değil). Ayrıntılar **§2**'de.

---

## Yapılması gerekenler (yeni ortam / deploy sonrası)

Yeni bir sunucu veya `deploy-services` (git reset --hard) sonrası sunucuda sadece compose gelir; aşağıdakileri **elle** yapmanız gerekir.

### License MasterKey (Create Domain için)

1. Key üret: `openssl rand -base64 32` — çıktıyı güvenli sakla (repo/ dokümana yazma).
2. Sunucuda `ApplicationResources/mng_apps/.env` içine ekle:  
   `MNGKEEPER_LICENSE_MASTER_KEY=<ürettiğin-key>`
3. Compose’da mngkeeper env’de şu satır olmalı (yoksa ekle):  
   `- MngKeeperSettings__License__MasterKey=${MNGKEEPER_LICENSE_MASTER_KEY}`
4. MngKeeper’ı yeniden başlat:  
   `docker compose -f docker-compose.production.yml build mngkeeper` ardından `up -d --no-deps mngkeeper`

### MngDomainUI backend URL’leri (Keeper / DataGateway / Scheduler)

Compose’da **mngdomainui** env’de şunlar **http** ile olmalı (container içi iletişim için):

- `SERVER_KEEPER_URL=${SERVER_KEEPER_URL:-http://mngkeeper:5001}`
- `SERVER_DATAGATEWAY_URL=${SERVER_DATAGATEWAY_URL:-http://mngdatagateway:5010}`
- `SERVER_SCHEDULER_URL=${SERVER_SCHEDULER_URL:-http://mngscheduler:5090}`

Yoksa ekleyip `docker compose up -d --no-deps mngdomainui` çalıştır.

### Keycloak path prefix ve .env (Create Domain – MngKeeper)

Keycloak `/keycloak` altında çalışıyorsa:

- Compose’da mngkeeper env’de: `MngKeeperSettings__Keycloak__PathPrefix=${KEYCLOAK_PATH_PREFIX:-/keycloak}` (varsayılan yeterli).
- **.env’de:** `KEYCLOAK_BASE_URL=http://keycloak:8080` (sadece origin; sonunda `/keycloak` veya başka path olmasın). İsteğe bağlı: `KEYCLOAK_PATH_PREFIX=/keycloak`.

Yanlış BaseUrl (ör. `http://keycloak:8080/keycloak`) veya eksik/yanlış path prefix, Create Domain’de "Failed to get admin token" 404’üne yol açar. Ayrıntılar **§8** ve **§10**’da.

### MinIO (Domain yedek alma – MngAdmin; file field upload – MngDataGateway)

Domain Yönetimi’nde "Yedek alma" ve dataset **file** alanlarında dosya yükleme çalışsın istiyorsanız:

- **.env’de** tanımlı olsun: `MINIO_ENDPOINT`, `MINIO_ACCESS_KEY`, `MINIO_SECRET_KEY` (ve isteğe bağlı `MINIO_USE_SSL=false`).
- **Compose’da** mngadmin env’de `MngAdminSettings__MinIO__*`; mngdatagateway env’de `MngDataGatewaySettings__FileStorage__Minio__Endpoint`, `__AccessKey`, `__SecretKey`, `__UseSSL` ile bu değerler bağlanmalı (repo’daki production compose’da §14 ve **§22** ile mevcut).
- MinIO container’ı mngadmin ve mngdatagateway ile aynı Docker network’te (`mng_common_mng_network`) olmalı ki `minio:9000` erişilebilsin.

### Chatbot (MngLLM + Ollama)

Chatbot’un sorulara yanıt verebilmesi için:

1. **Compose’da** `ollama` servisi ve `mngllm depends_on: ollama` tanımlı olmalı (repo’daki production compose’da §17 ile eklendi). `volumes: ollama_data` tanımlı olmalı.
2. **Sunucuda:**  
   `docker compose -f docker-compose.production.yml up -d ollama`  
   `docker exec ollama ollama pull qwen2.5:3b`  
   `docker compose -f docker-compose.production.yml up -d --no-deps mngllm`
3. **MngLLM JWT:** Çoğu kurulumda `MNGLLM_JWT_AUTHORITY` boş bırakılır (domain token içinden okunur, DG ile aynı model). Compose’da `MngLLMSettings__Jwt__Authority=${MNGLLM_JWT_AUTHORITY:-}` yeterli. Ayrıntılar **§16–§19**’da.

### CORS (MngGateway + MngHub)

MngUI’nin Hub (SignalR) veya Gateway üzerinden eriştiği tüm isteklerde tarayıcı origin’i CORS’ta tanımlı olmalı. `.env` içinde:

- `CORS_ALLOWED_ORIGIN_1=https://app.monitrang.com` (veya frontend’in gerçek origin’i)
- İkinci origin gerekiyorsa: `CORS_ALLOWED_ORIGIN_2=…`

Hem **mnggateway** hem **mnghub** bu değişkenleri kullanır. Değiştirdikten sonra ilgili container’ları yeniden başlatın. Ayrıntılar **§12**’de.

### Same-origin Hub (app.monitrang.com/hub)

CORS’u bypass etmek için Hub trafiği app.monitrang.com üzerinden same-origin gitsin isteniyorsa:

- **mng_common Nginx:** `ApplicationResources/mng_common/nginx/conf.d/monitrang.conf` içinde app.monitrang.com HTTPS server bloğuna `location /hub/` ekleyin. Gateway port 5000’de **HTTPS** dinlediği için `proxy_pass https://mnggateway:5000/hub/` ve `proxy_ssl_verify off` kullanın; client token’ı query’de gönderdiği için `$arg_access_token` varsa `Authorization: Bearer $arg_access_token` header’ına taşıyın (§12.4). WebSocket için `Upgrade`, `Connection "upgrade"`, `proxy_read_timeout 86400` ekleyin. Sonra `docker exec nginx nginx -s reload`.
- **MngUI:** `.env`’de `GATEWAY_URL=https://app.monitrang.com`, `HUB_URL=` (boş). Compose’da mngui `build.args` ile `GATEWAY_URL` ve `HUB_URL` verilmeli. **Kod tarafında** `Mng.Ui/nuxt.config.ts` içinde `hubUrl` için boş/whitespace (§12.2) ve production'da `gatewayUrl` için boş varsayılan (§12.3) olmalı; aksi halde build’de `HUB_URL` boş olsa bile client `http://localhost:5020` kullanmaya devam eder. Ardından `docker compose -f docker-compose.production.yml build --no-cache mngui` ve `up -d --no-deps --force-recreate mngui`.

Ayrıntılar **§12.1**, **§12.2**, **§12.3**, **§12.4** ve **§12.5**’te.

### Healthcheck’ler (opsiyonel)

Compose’da sağlık kontrolleri yanlış protokol kullanıyorsa aynı sunucu tarafı düzeltmeler uygulanabilir; ayrıntılar bu log’un **§2** bölümünde.
