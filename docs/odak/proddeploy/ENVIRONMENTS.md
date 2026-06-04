# Odak — Test ve production ortamları

**Tam bağımsızlık ilkesi:** [INDEPENDENCE.md](./INDEPENDENCE.md) — iki sunucu, iki ayrı mng_common + mng_apps yığını; paylaşılan DB/Keycloak yok.

## Karar özeti

| | Test (geliştirme / POC) | Production |
|---|-------------------------|------------|
| **IP** | `192.168.20.20` | `192.168.20.8` |
| **Rol** | Günlük kod, deploy, smoke, LDAP POC, diagnostic | Müşteri canlı / üretim benzeri |
| **Deploy dokümanı** | [../deploy/README.md](../deploy/README.md) | [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md) |
| **Script varsayılanı** | `-Server` belirtilmezse `192.168.20.20` | Her zaman `-Server 192.168.20.8` |

Geliştirme PC’deki workspace, `appsettings.Development.json` ve yerel `npm run dev` bağlantıları **test sunucuya** (`20.20`) yönelmeye devam eder. Production’a geçiş yalnızca bilinçli deploy komutuyla yapılır.

---

## URL matrisi (dış erişim)

Aynı portlar; yalnızca host IP değişir.

| Servis | Test | Production |
|--------|------|------------|
| MngUI | http://192.168.20.20:3000 | http://192.168.20.8:3000 |
| MngDomainUI | http://192.168.20.20:3001/domain/ | http://192.168.20.8:3001/domain/ |
| API Gateway | http://192.168.20.20:5040 | http://192.168.20.8:5040 |
| Keycloak Admin | http://192.168.20.20:8080/keycloak/admin/... | http://192.168.20.8:8080/keycloak/admin/... |
| MngKeeper (doğrudan) | http://192.168.20.20:5001 | http://192.168.20.8:5001 |

Tam port listesi (IT): [../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md](../setup/MNG_APPS_ODAK_MUSTERI_ERISIM.md) — tablodaki IP’yi ortama göre değiştirin.

---

## Sunucu dizin yapısı (yapı benzer, veri ve süreç ayrı)

Her sunucu **kendi** altyapı ve uygulama dizinine sahiptir; içerik test’ten kopyalanmaz.

| | Test (`20.20`) | Production (`20.8`) |
|---|----------------|---------------------|
| mng_common override | `docker-compose.odak.yml` | `docker-compose.odak.prod.yml` |
| mng_apps override | `docker-compose.odak.yml` | `docker-compose.odak.prod.yml` |
| `.env` şablonu | `.env.odak.example` | `.env.odak.prod.example` |

```
/home/odak/   (her sunucuda ayrı VM — volume’lar bu host’ta kalır)
├── mng_common/     → Mongo, Keycloak, Redis, RabbitMQ, MinIO, … (YALNIZCA bu makine)
└── MonitraNG/.../mng_apps/   → Gateway, Keeper, UI, … (YALNIZCA bu makine)
```

Şablon: [env.prod.server.example](./env.prod.server.example) · İlke: [INDEPENDENCE.md](./INDEPENDENCE.md).

---

## Veri ve kimlik (sıfır paylaşım)

| Konu | Test | Production |
|------|------|------------|
| MongoDB verisi | `20.20` disk/volume | `20.8` disk/volume — **farklı dosya sistemi** |
| Keycloak / Postgres | Test sunucu PostgreSQL | Production sunucu PostgreSQL |
| Redis / RabbitMQ / MinIO | Test konteyner volume’ları | Production konteyner volume’ları |
| Domain adı | Genelde `odak` | Aynı isim olabilir; **farklı Mongo/Keycloak kaydı** |
| Secret’lar | Test `.env` | Production `.env` — **test’ten kopyalanmaz** |

İki ortam arası veri aktarımı yalnızca ayrı, onaylı **migration** süreciyle yapılır ([INDEPENDENCE.md](./INDEPENDENCE.md)).

---

## Compose (ortama özel override)

**Test** (`20.20`):

```bash
docker compose -f docker-compose.production.yml -f docker-compose.odak.yml --env-file .env
```

**Production** (`20.8` — kendi mng_common’ı ayakta olmalı):

```bash
docker compose -f docker-compose.production.yml -f docker-compose.odak.prod.yml --env-file .env
```

`docker-compose.odak.prod.yml` yalnızca production IP varsayılanlarını taşır; test compose dosyası production sunucuya **deploy edilmez**.

---

## Geliştirme PC — hangi kimlik dosyası?

| Deploy hedefi | Yerel dosya | Script’e aktarım |
|---------------|-------------|------------------|
| Test | `.env.odak.local` → `ODAK_SSH_PASSWORD` | `OdakSshCommon.ps1` otomatik okur |
| Production | `.env.odak.prod.local` → `ODAK_PROD_SSH_PASSWORD` | Deploy öncesi `$env:ODAK_SSH_PASSWORD = $env:ODAK_PROD_SSH_PASSWORD` veya agent talimatı — bkz. [AGENT_PRODUCTION_DEPLOY.md](./AGENT_PRODUCTION_DEPLOY.md) |

Test parolası ile production parolası **farklıdır** (müşteri bildirimi).
