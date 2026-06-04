# Test ↔ Production — tam bağımsızlık

**İlke:** `192.168.20.20` (test) ile `192.168.20.8` (production) **iki ayrı fiziksel sunucu**, **iki ayrı çalışan sistem**. Aralarında paylaşılan veritabanı, Keycloak, ağ, volume veya “ortak altyapı” **yoktur**.

Production, tüm bileşenleri **kendi makinesi üzerinde** çalıştırır — tıpkı test sunucusunun kendi içinde yaptığı gibi, ancak **test ile hiçbir bağlantısı olmadan**.

---

## Her sunucuda kendi tam yığını

Aşağıdakiler **her ortamda ayrı VM / ayrı Docker daemon** üzerinde, **ayrı kalıcı veri** ile çalışır:

| Katman | Bileşenler (örnek) | Test (`20.20`) | Production (`20.8`) |
|--------|-------------------|----------------|---------------------|
| **mng_common** | MongoDB, PostgreSQL (Keycloak), Redis, RabbitMQ, MinIO, Mosquitto, Keycloak, Seq, … | Kendi `/home/odak/mng_common` | Kendi `/home/odak/mng_common` |
| **mng_apps** | MngGateway, MngKeeper, MngUI, MngOperations, … | Kendi `MonitraNG/.../mng_apps` | Kendi `MonitraNG/.../mng_apps` |
| **Docker ağı** | `mng_common_mng_network` | Yalnızca bu host’taki konteynerler | Yalnızca bu host’taki konteynerler |
| **Kimlik** | Keycloak realm / client / kullanıcılar | Test Keycloak DB | Production Keycloak DB |
| **Uygulama verisi** | Mongo `mngkeeper`, MinIO bucket’lar, … | Test volume’ları | Production volume’ları |

Uygulama konteynerleri **yalnızca aynı sunucudaki** docker servis adlarına bağlanır (`mongo`, `keycloak`, `redis` — **asla** `192.168.20.20` veya karşı ortamın IP’si).

```
┌─────────────────────────────┐     ┌─────────────────────────────┐
│  192.168.20.20  (TEST)      │     │  192.168.20.8  (PRODUCTION) │
│  mng_common + mng_apps      │     │  mng_common + mng_apps      │
│  Mongo / KC / Redis / …     │     │  Mongo / KC / Redis / …     │
│  (tamamen yerel)            │     │  (tamamen yerel)            │
└─────────────────────────────┘     └─────────────────────────────┘
         ▲                                       ▲
         │                                       │
    Geliştirme PC                          Bilinçli prod deploy
    (varsayılan test)                      (sync-odak-prod / deploy-odak-prod)
         ╳───────────────── bağlantı yok ─────────────────╳
```

---

## Yasak / yapılmaması gerekenler

| ❌ Yapılmaz | ✅ Bunun yerine |
|------------|----------------|
| Test sunucudaki `.env` dosyasını production’a kopyalamak | `cp .env.odak.prod.example .env` (production sunucuda) |
| Test `KEYCLOAK_CLIENT_SECRET` / lisans anahtarını production’da kullanmak | Production Keycloak’ta yeni client + yeni secret |
| Production `.env` içinde `192.168.20.20` (test IP) | Yalnızca `192.168.20.8` veya docker internal host adları |
| Production uygulamasının test Mongo/Keycloak’a bağlanması | `mongo`, `keycloak` (aynı host compose ağı) |
| Test sunucudan `docker volume` / DB dump’ı production’a “sessizce” aktarmak | Ayrı **migration/cutover** planı ve onay |
| Tek `mng_common`’ı iki IP’den paylaşmak | Her sunucuda **ayrı** `docker compose up` |
| Karışık SSH parolası / `.env.odak.local` ile prod deploy | Yalnızca `.env.odak.prod.local` + `*-prod.ps1` |

---

## İzin verilen “ortaklık”

Yalnızca **kaynak kod** ve **repo şablonları** ortaktır (git workspace → `sync` ile her sunucuya ayrı kopya). Çalışan sistem ve veri ortak değildir.

| Ortak | Ortak değil |
|-------|-------------|
| MonitraNG kaynak kodu (build için) | Çalışan konteynerler |
| `.env.odak.prod.example` (şablon) | Sunucudaki dolu `.env` |
| `docker-compose.production.yml` (genel) | Mongo/Postgres verisi |

---

## Production compose ve şablonlar

Production sunucuda **yalnızca** production override kullanılır:

```bash
# mng_common (192.168.20.8 üzerinde)
cd /home/odak/mng_common
docker compose -f docker-compose.yml -f docker-compose.odak.prod.yml --env-file .env up -d

# mng_apps (aynı sunucuda)
cd /home/odak/MonitraNG/ApplicationResources/mng_apps
docker compose -f docker-compose.production.yml -f docker-compose.odak.prod.yml --env-file .env up -d
```

Test sunucuda `docker-compose.odak.yml` kalır; production’a **taşınmaz**.

Scriptler: `deploy-odak-prod.ps1` / `sync-odak-prod.ps1` otomatik olarak `docker-compose.odak.prod.yml` seçer.

---

## İlk kurulum = sıfırdan production yığını

Production ilk kez ayağa kalkarken test sunucunun “kopyası” değil, **aynı prosedürün production IP ve prod şablonlarıyla** uygulanmasıdır:

1. Production’da Docker + `mng_common` (kendi volume’ları)
2. Production’da Keycloak kurulumu (kendi PostgreSQL verisi)
3. Production’da `mng_apps` + **yeni** secret’lar
4. Domain / initial data (bilinçli karar; test verisi otomatik aktarılmaz)

Adımlar: [INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md).

---

## Geliştirme PC

| Bağlantı | Hedef |
|----------|--------|
| `appsettings.Development.json`, `npm run dev`, varsayılan scriptler | **Test** `192.168.20.20` |
| Production deploy | **Yalnızca** `*-prod.ps1` veya `-Server 192.168.20.8` |

Test sunucuya deploy, production’ı **etkilemez**. Production deploy, test sunucuyu **etkilemez**.

---

## İlgili dokümanlar

- [ENVIRONMENTS.md](./ENVIRONMENTS.md) — IP ve URL matrisi
- [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md) — production sunucu hazırlık durumu
- [../deploy/README.md](../deploy/README.md) — yalnızca test deploy
