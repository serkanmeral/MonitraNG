# Domain oluşturma — MngDomainUI (Odak)

**Ortam:** Odak POC — `192.168.20.20`  
**Arayüz:** MngDomainUI  
**Backend:** MngKeeper (Domain Creation Pipeline)  
**Son güncelleme:** 22 Mayıs 2026

---

## 1. Genel bakış

MonitraNG’de her **domain** ayrı bir müşteri/tenant ortamıdır. Yeni domain açmak için birincil arayüz **MngDomainUI**’dır.

| Bileşen | Görev |
|---------|--------|
| **MngDomainUI** | Login, domain listesi, oluşturma formu |
| **MngKeeper** | `POST /api/domain` ve 13+ adımlı pipeline |
| **Keycloak** | Domain başına **ayrı realm** + ilk admin kullanıcı |
| **MongoDB** | Meta kayıt + `mng_{domainAdı}` veritabanı |
| **MinIO, Redis, RabbitMQ** | Bucket, önbellek, domain-created event |

**Odak erişim adresleri:**

| Servis | URL |
|--------|-----|
| MngDomainUI | http://192.168.20.20:3001/domain/ |
| MngKeeper (API) | http://192.168.20.20:5001 |
| Keycloak Admin | http://192.168.20.20:8080/keycloak/admin/master/console/ |

> UI `baseURL` değeri `/domain/` olduğu için kök adres `http://192.168.20.20:3001/` değil, **`...:3001/domain/`** kullanılır.

---

## 2. Akış diyagramı

```
Tarayıcı → MngDomainUI (login, Keycloak master)
         → Create Domain formu
         → POST /api/keeper/domain (Nuxt server proxy)
         → MngKeeper DomainCreationPipeline
              → MongoDB, Keycloak realm, gruplar, admin user,
                MinIO, Redis, RabbitMQ, lisans, Active
         → Liste güncellenir
```

---

## 3. Ön koşullar

Domain oluşturmadan önce aşağıdakilerin çalışır durumda olması gerekir.

### 3.1 Servisler

| Kontrol | Doğrulama |
|---------|-----------|
| mng_common (mongo, keycloak, redis, rabbitmq, minio, …) | `docker compose ps` — `/home/odak/mng_common` |
| mngkeeper | http://192.168.20.20:5001/health |
| mngdomainui | http://192.168.20.20:3001/domain/api/health |

### 3.2 Ortam değişkenleri (`mng_apps/.env`)

Sunucu: `/home/odak/MonitraNG/ApplicationResources/mng_apps/.env`

| Değişken | Açıklama | Odak örnek |
|----------|----------|------------|
| `MNGKEEPER_LICENSE_MASTER_KEY` | Trial lisans şifrelemesi (**zorunlu**, ≥32 karakter) | `.env.odak.example` içindeki değer veya `openssl rand -base64 32` |
| `KEYCLOAK_BASE_URL` | Container içi origin (**path yok**) | `http://keycloak:8080` |
| `KEYCLOAK_PATH_PREFIX` | Keycloak HTTP path | `/keycloak` |
| `KEYCLOAK_ADMIN_USERNAME` / `PASSWORD` | Master realm admin (UI login ile aynı mantık) | `admin` / altyapı parolası |
| `KEYCLOAK_CLIENT_ID` | Master realm confidential client | `mng-keeper-admin` |
| `KEYCLOAK_CLIENT_SECRET` | Client Credentials sekmesindeki secret | Keycloak’ta oluşturulduktan sonra `.env` — **placeholder bırakmayın** |

**Yanlış örnek:** `KEYCLOAK_BASE_URL=http://keycloak:8080/keycloak` — Keeper token isteğinde 404 riski.

`.env` güncelledikten sonra `docker restart mngkeeper` **yeterli değildir**; ortam değişkenleri için container recreate gerekir:

```bash
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env up -d --no-deps mngkeeper
```

Parolalar: [MNG_COMMON_ODAK_MUSTERI_ERISIM.md](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md)

### 3.3 Keycloak

- **Master realm** admin ile MngDomainUI’ya giriş yapılır.
- Pipeline, formdaki **domain adıyla yeni bir realm** oluşturur (örn. domain `acme-corp` → realm `acme-corp`).
- Master admin: http://192.168.20.20:8080/keycloak/admin/master/console/
- **MngKeeper için** master realm’de client `mng-keeper-admin` (confidential, **Direct access grants** açık) ve `.env` içinde eşleşen `KEYCLOAK_CLIENT_SECRET` zorunludur. Ayrıntılı kurulum: [../setup/MNG_APPS_ODAK.md](../setup/MNG_APPS_ODAK.md) — “Kurulum öncesi: Keycloak”.

---

## 4. Adım adım — UI ile domain oluşturma

### Adım 1 — MngDomainUI’ya giriş

1. Tarayıcıda açın: **http://192.168.20.20:3001/domain/**
2. **Sign In** ekranında Keycloak **master** admin bilgilerini girin.
3. Başarılı girişte token `master` realm üzerinden alınır (`admin-cli` client).

### Adım 2 — Domain Management

1. **Domain Management** sayfası açılır (`/domain/` → domains).
2. **Domains** sekmesinde mevcut domain listesi görünür.
3. **Create Domain** butonuna tıklayın.

### Adım 3 — Form alanları

#### Zorunlu

| Alan | Kurallar | Örnek |
|------|----------|--------|
| **Domain Name** | Benzersiz; küçük harf; harf, rakam, tire | `acme-corp` |
| **Display Name** | Görünen ad | `Acme Corporation` |
| **Admin Email** | Domain yöneticisi e-posta | `admin@acme.local` |
| **Admin Password** | Min. 8 karakter; büyük, küçük, rakam, özel karakter | `Admin123!@#` |

#### İsteğe bağlı

| Alan | Açıklama |
|------|----------|
| Related Person Phone | İleride SMS vb. |
| Related Person Email | Domain oluşturma bildirimi (SMTP varsa) |
| Logo | Dosya yükleme (base64) |
| Logo URL | Harici logo URL |
| **Initial Data Template** | `mng_templates` şablonundan koleksiyon/veri kopyası |
| **Advanced Settings** | Max users, max assets, Enable MQTT |

### Adım 4 — Gönder ve bekle

1. **Create** / gönder.
2. UI `POST /api/keeper/domain` çağırır → sunucu proxy → MngKeeper pipeline.
3. Pipeline **30–90 saniye** sürebilir; sayfayı kapatmayın.
4. Başarılıysa modal kapanır, liste yenilenir.
5. Hata mesajı varsa metni not alın; detay için Adım 6.

### Adım 5 — Doğrulama

| Kontrol | Beklenen |
|---------|----------|
| Domain listesi | Yeni domain **Active** (veya kısa süre Pending → Active) |
| Keycloak | Realms listesinde domain adıyla yeni realm |
| MongoDB | `mng_{domainName}` veritabanı (mongo-express veya CLI) |
| MinIO | Domain bucket |

### Adım 6 — Hata durumunda log

Sunucuda:

```bash
docker logs mngkeeper --tail 150 2>&1 | less
```

Yanıtta `FailedStep` / pipeline adı ve exception metni hangi adımda kaldığını gösterir.

---

## 5. MngKeeper pipeline (arka plan)

Tek `POST /api/domain` çağrısı aşağıdaki adımları sırayla çalıştırır. Bir adım başarısız olursa pipeline durur.

| # | Adım | Özet |
|---|------|------|
| 1 | ValidateDomain | Format ve benzersizlik |
| 2 | CreateDomainEntity | Mongo meta kayıt |
| 3 | CreateDatabase | `mng_{domainName}` |
| 4 | InitializeDatabaseCollections | `@datasets`, `@dataset_categories`, … |
| 5 | InitializeInitialData | Şablon seçildiyse template kopyası |
| 6 | InitializeDataGatewayCollections | `@users`, `@groups` |
| 7 | CreateIndexes | İndeksler |
| 8 | CreateKeycloakRealm | Domain adıyla realm |
| 9 | CreateDefaultGroups | Admins, Managers, Users, Guests |
| 10 | CreateAdminUser | Formdaki admin |
| 11 | PublishDomainCreatedEvent | RabbitMQ |
| 12 | InitializeDomainCache | Redis |
| 13 | CreateMinIOBucket | Object storage |
| 14 | CreateLicense | Trial lisans |
| 15 | ActivateDomain | Status → Active |
| 16 | SendDomainCreatedEmail | E-posta (kritik değil) |

Kaynak kod: `MngKeeper/Application/Pipelines/DomainCreation/`  
Ayrıntı: [MngKeeper Architecture Guide](../../content/MngKeeper/support/architecture/ARCHITECTURE_GUIDE.md)

---

## 6. Domain oluşturduktan sonra (opsiyonel)

UI dışında veya test script’lerinde yapılan ek işlemler (gerekirse):

| İşlem | API / not |
|--------|-----------|
| Realm mapper yapılandırma | `POST /api/admin/realms/{domainName}/configure-mappers` |
| Domain admin token | Domain realm’inde kullanıcı + parola ile token |
| Test kullanıcı / grup / dataset | Domain detay sayfası veya script |

Günlük iş **yalnızca domain açmak** ise Bölüm 4 yeterlidir.

---

## 7. Sık karşılaşılan hatalar

| Belirti | Olası neden | Çözüm |
|--------|-------------|--------|
| License MasterKey is not configured | `MNGKEEPER_LICENSE_MASTER_KEY` yok | `.env` doldur → `docker compose ... up -d --no-deps mngkeeper` |
| Failed to get admin token, `invalid_client` / `unauthorized_client` | `mng-keeper-admin` yok veya secret yanlış; veya sadece `docker restart` | Client oluştur → secret `.env` → **compose recreate** mngkeeper ([kayıt](./DOMAIN_OLUSTURMA_KAYIT.md#hata-1--invalid_client--unauthorized_client-createkeycloakrealm)) |
| Failed to get admin token, 404 | Keycloak URL/path yanlış | `KEYCLOAK_BASE_URL=http://keycloak:8080`, `KEYCLOAK_PATH_PREFIX=/keycloak` |
| Domain already exists | Aynı `domainName` | Farklı ad veya eski domain silme |
| Login başarısız | Keycloak master admin / port 8080 | [Keycloak erişim](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) |
| UI açılmıyor | Yanlış URL | `:3001/domain/` kullanın |
| E-posta gitmiyor | Odak’ta SMTP yok | Beklenen; domain yine oluşabilir |

---

## 8. Kod referansları (repo)

| Dosya | Açıklama |
|-------|----------|
| `MngDomainUI/pages/domains/index.vue` | Liste + Create Domain modal |
| `MngDomainUI/components/domain/DomainForm.vue` | Form ve validasyon |
| `MngDomainUI/composables/useDomain.ts` | `createDomain()` → `/api/keeper/domain` |
| `MngDomainUI/server/api/keeper/[...path].ts` | Keeper proxy |
| `MngDomainUI/server/api/auth/login.post.ts` | Keycloak master login |
| `MngKeeper/.../DomainCreationPipeline.cs` | Pipeline sırası |

---

## 9. İlgili dokümanlar

- [DOMAIN_OLUSTURMA_KAYIT.md](./DOMAIN_OLUSTURMA_KAYIT.md) — canlı oturum günlüğü (başarı/hata/çözüm)
- [DOMAIN_OLUSTURMA_API.md](./DOMAIN_OLUSTURMA_API.md) — curl / script ile oluşturma
- [../setup/MNG_APPS_ODAK.md](../setup/MNG_APPS_ODAK.md)
- [../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md](../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md)
