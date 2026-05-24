# LDAP / directory sync — Geliştirme ve deploy akışı

**Son güncelleme:** 23 Mayıs 2026  
**Durum:** K2 + P0 + K4 **deploy edildi** (Keeper v1.3.0); sıradaki **K3** — [HANDOFF_MNGSCHEDULER.md](./HANDOFF_MNGSCHEDULER.md)  
**İlişki:** [DEVAM.md](./DEVAM.md), [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md), [ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)

---

## 1. Özet akış

```
Kod (PC / workspace)
    → Yerel run (dotnet / IDE)
    → Swagger / Scalar ile API test
    → Kabul kriterleri tamam
    → sync-odak-source + deploy-odak-apps (sunucu)
    → Odak’ta smoke test (isteğe bağlı)
```

| Aşama | Nerede | Çıktı |
|-------|--------|--------|
| **Geliştirme** | `MngKeeper` (+ sonra `MngScheduler`) | K2, P0, K4, K5… |
| **Yerel run** | Geliştirme makinesi | API ayakta |
| **Test** | Swagger / Scalar, `POST /api/auth/token` | T1–T2, 409, Mongo doğrulama |
| **Deploy** | `192.168.20.20` | Production compose |

**İlke:** Sunucuya **erken deploy yok**; yerel doğrulama bitmeden `deploy-odak-apps` çalıştırılmaz.

---

## 2. Ön koşullar (yerel test)

### 2.1 K1 tamam (Odak sunucu)

- Keycloak **odak** realm: LDAP kullanıcı + gruplar sync ([POC_KEYCLOAK_LDAP.md](./POC_KEYCLOAK_LDAP.md)).
- Domain **odak** Mongo’da mevcut (`mng_odak`).

### 2.2 Altyapı (yerel veya uzak)

Geliştirme PC’sinden erişilebilir olmalı:

| Servis | Yerel (varsayılan appsettings) | Odak sunucu (geliştirme için override) |
|--------|--------------------------------|----------------------------------------|
| MongoDB | `localhost:27017` | `192.168.20.20:27017` |
| Keycloak | `localhost:8080` | `http://192.168.20.20:8080` + `PathPrefix=/keycloak` |
| Seq (opsiyonel) | `localhost:5341` | `192.168.20.20:5341` |

**Odak Keycloak ile yerel Keeper testi** için `appsettings.Development.json` veya User Secrets örneği:

**Yapılandırma dosyası:** `MngKeeper/Presentation/MngKeeper.Api/appsettings.Development.json`  
(`ASPNETCORE_ENVIRONMENT=Development` → `appsettings.json` üzerine yazar.)

Şablon: `appsettings.Development.example.json` (yeni makine için kopyala).

| Alan | Odak yerel değer | Not |
|------|------------------|-----|
| `Keycloak:BaseUrl` | `http://192.168.20.20:8080` | |
| `Keycloak:PathPrefix` | `/keycloak` | |
| `Keycloak:AdminUsername` | `admin` | Admin konsolu |
| `Keycloak:AdminPassword` | [MNG_COMMON §5](../setup/MNG_COMMON_ODAK_MUSTERI_ERISIM.md) | `appsettings.Development.json` |
| `Keycloak:ClientId` | `admin-cli` | K2 / admin API; secret gerekmez |
| `Keycloak:ClientSecret` | `""` | Boş bırakın |
| `MongoDB:ConnectionString` | `mongodb://admin:admin123@192.168.20.20:27017` | |

**`unauthorized_client`:** `appsettings.json` (varsayılan) `mng-keeper-admin` + yerel secret içindir; Development dosyası bunu **override** etmeli. Sunucu Docker’da `mng-keeper-admin` + `.env`: [DOMAIN_OLUSTURMA_KAYIT.md](../domain/DOMAIN_OLUSTURMA_KAYIT.md).

---

## 3. Yerel çalıştırma (MngKeeper)

```powershell
cd MngKeeper\Presentation\MngKeeper.Api
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

| | |
|--|--|
| Varsayılan URL | `http://localhost:5001` (`appsettings.json` → `MngKeeperSettings:Server:Port`) |
| `launchSettings.json` | `5280` tanımlı olabilir; Kestrel config **5001**’i override eder |

**Swagger / API dokümantasyonu (Development):**

| UI | URL (yerel) |
|----|-------------|
| **Scalar** (önerilen) | http://localhost:5001/scalar/v1 |
| Swagger UI | http://localhost:5001/api-docs (`/swagger` → yönlendirme) |

Sunucu (Odak, `EnableSwagger=true`): aynı yollar — **http://**192.168.20.20:5001/... (`https` değil).
| OpenAPI JSON | http://localhost:5001/api-docs/v1/swagger.json |

---

## 4. Swagger ile K2 test checklist

Geliştirme tamamlandıkça işaretleyin:

| # | Test | Beklenen |
|---|------|----------|
| T-API1 | `POST /api/directory/sync` — geçerli admin token, domain `odak` | 200 + özet (usersCreated/Updated …) |
| T-API2 | Aynı istek sync sürerken tekrar POST | **409** `SYNC_ALREADY_RUNNING` |
| T-API3 | `POST /api/auth/token` — pilot AD kullanıcı | 200 + JWT `user_groups` |
| T-API4 | Mongo Express / compass — `mng_odak.@users` | Federated kayıtlar, `Groups` dolu; e-postasız kullanıcıda `email` alanı yok/null |
| T-API4b | Eski `email_1` index hatası (E11000 `email: ""`) | Aşağıdaki Mongo düzeltmesi + sync yeniden |

**Mongo (mevcut `mng_odak` — bir kez):** Eski tam unique `email_1` kaldırıp boş e-postaları temizleyin, ardından sync:

```javascript
use mng_odak
db.getCollection("@users").updateMany({ email: "" }, { $unset: { email: "" } })
db.getCollection("@users").dropIndex("email_1")
// Yeni index: bir sonraki domain oluşturmada otomatik; veya elle partial unique (kod: InitializeDataGatewayCollectionsStep)
```

| T-API5 | Mevcut `POST /api/sync/users` | Hâlâ DataGateway; LDAP ile karışmıyor |

**Token almak:** Önce `POST /api/auth/token` (body: username, password, domain: `odak`) — dönen `accessToken` → Swagger **Authorize** Bearer.

---

## 5. Geliştirme fazları (kod sırası)

| Sıra | Faz | Yerel test |
|------|-----|------------|
| 1 | **K2** — directory sync endpoint + coordinator + KC list | ✅ deploy |
| 2 | **P0** — `directoryPrivileges` + login JWT | ✅ deploy |
| 3 | **K4** — login tek kullanıcı sync | ✅ deploy |
| 4 | **K3** — MngScheduler orchestration | ⬜ **sonraki chat** — Scheduler API + Keeper 409 skip |
| 5 | **K5** — `provisioningSource` + UpdateUser guard | ⬜ |
| 6 | **Mng.Ui** K1.6 / K5 | ⬜ |
| 7 | **HTTPS** / Nginx | ⬜ en son |

Detay: [DEVAM.md §4](./DEVAM.md#4-onaylı-sıra-güncel).

---

## 6. Sunucuya deploy (doğrulama sonrası)

### 6.1 Kaynak senkronu

```powershell
# Repo kökünden — örnek: sadece Keeper
.\scripts\odak\sync-odak-source.ps1 -Paths MngKeeper
```

### 6.2 Build + up

```powershell
.\scripts\odak\deploy-odak-apps.ps1 -Services mngkeeper
# İlk kez veya büyük değişiklik:
.\scripts\odak\deploy-odak-apps.ps1 -Services mngkeeper -FullBuild
```

Scheduler dahil:

```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths MngKeeper,MngScheduler
.\scripts\odak\deploy-odak-apps.ps1 -Services mngkeeper,mngscheduler
```

Ayrıntı: [../setup/MNG_APPS_ODAK_DEPLOY.md](../setup/MNG_APPS_ODAK_DEPLOY.md), [ODAK_FULL_SETUP.md §5](../ODAK_FULL_SETUP.md#5-deploy-stratejisi-pc--sunucu).

### 6.3 Sunucuda smoke test

| | |
|--|--|
| MngKeeper | http://192.168.20.20:5001 — health / swagger (`EnableSwagger=true` ise) |
| Directory sync | `POST` gateway veya doğrudan Keeper (ortam politikasına göre) |
| UI | http://192.168.20.20:3000 — pilot AD login |

**Not:** Sunucuda Swagger kapalı olabilir; smoke için `curl` / Postman veya geçici `EnableSwagger=true` kullanılabilir.

---

## 7. Deploy öncesi “bitti” tanımı (K2–K4 — tamamlandı)

- [x] Yerel `dotnet run` hatasız
- [x] `POST /api/auth/token` + P0 JWT (yerel + sunucu)
- [x] K4 login sync doğrulandı
- [x] Sunucu deploy — v1.3.0
- [ ] İsteğe bağlı: sunucuda tam `POST /api/directory/sync` 200 + 409
- [ ] Break-glass **Local** kullanıcıya sync dokunmuyor (regresyon)

---

## 8. Doküman ağacı (güncel)

```
ldap/
├── HANDOFF_MNGSCHEDULER.md  ← sonraki chat (K3)
├── DEV_WORKFLOW.md          ← bu dosya
├── DEVAM.md
├── ODAK_HTTP_AND_GATEWAY.md
├── DEPLOY_KEEPER_LDAP.md
└── SCHEDULER_DIRECTORY_SYNC.md
```
