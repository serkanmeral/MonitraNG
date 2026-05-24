# LDAP / AD — Oturum özeti ve faz durumu

**Son güncelleme:** 24 Mayıs 2026  
**Odak sunucu:** `192.168.20.20` · Keeper **v1.3.4** + MngScheduler **K3** deploy edildi  
**LDAP POC (K1–K5 + G1):** ✅ işlevsel — kalan: opsiyonel testler, **mngui** sunucu deploy, HTTPS

---

## 1. Nereden başlanır?

| Durum | Doküman |
|--------|---------|
| **Kullanıcı / grup kaynakları** | [USER_SOURCES.md](./USER_SOURCES.md), [GROUP_SOURCES.md](./GROUP_SOURCES.md) |
| **UI el değişimi** | [HANDOFF_UI.md](./HANDOFF_UI.md) |
| K1.6 + yerel UI gateway (tamamlandı) | Aşağı §3.1, [../ui/WELCOME_HOME.md](../ui/WELCOME_HOME.md) |
| K3 Scheduler (tamamlandı) | [HANDOFF_MNGSCHEDULER.md](./HANDOFF_MNGSCHEDULER.md), [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) |
| Geliştir / test / deploy | [DEV_WORKFLOW.md](./DEV_WORKFLOW.md) |
| HTTP + Gateway (Odak POC) | [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md) |
| Faz checklist (bu dosya) | Aşağı §3–4 |
| Teknik plan | [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) |

---

## 2. Ortam (sabitler)

| Bileşen | Adres |
|---------|--------|
| MonitraNG / Odak sunucu | `192.168.20.20` |
| Active Directory | `LDAP://192.168.20.3:389/DC=odak,DC=local` |
| Keycloak Admin | http://192.168.20.20:8080/keycloak/admin/ |
| **API Gateway** | http://192.168.20.20:5040 |
| MngKeeper (doğrudan / Scalar) | http://192.168.20.20:5001 |
| MngScheduler | http://192.168.20.20:5090 |
| Keeper **üretim API yolu** | http://192.168.20.20:5040/keeper/api/... |

**Protokol (Odak POC):** Dışarıdan **HTTP**. HTTPS/Nginx **ertelendi** — [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md).

---

## 3. Faz durumu (K1–K5)

| Kod | İş | Durum |
|-----|-----|--------|
| **K1** | Keycloak AD federation + manuel LDAP sync (odak realm) | ✅ Ops |
| **K2** | `POST /api/directory/sync` — KC→Mongo tam sync; coordinator **409** | ✅ Kod + deploy |
| **P0** | `directoryPrivileges` + `IPrivilegeGroupResolver` + JWT claim | ✅ Yerel + sunucu token |
| **K4** | Login tek kullanıcı sync (`SyncUserOnLoginAsync`) | ✅ Kod + deploy — [K4_LOGIN_SYNC.md](./K4_LOGIN_SYNC.md) |
| **Deploy** | Keeper **v1.3.4** → `192.168.20.20` | ✅ [DEPLOY_KEEPER_LDAP.md](./DEPLOY_KEEPER_LDAP.md) |
| **K3** | MngScheduler → periyodik `POST /api/directory/sync` | ✅ Kod + deploy + sunucu sync doğrulandı — [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) |
| **K1.6** | UI pilot AD login (+ manager JWT) | ✅ Yerel UI → sunucu gateway; AD kullanıcı girişi doğrulandı |
| **K5** | Local / Directory — kullanıcı API + UI | ✅ K5a–d; sunucu/liste doğrulandı |
| **G1** | Grup Local / Directory + domain manuel sync | ✅ Keeper 1.3.4 + UI; sync butonu (`triggeredBy: 0`) |
| **HTTPS** | Nginx + TLS | ⬜ En son işlerden |

**Ayrım:** `/api/sync/users|groups|all` = Keeper Mongo → **DataGateway** (LDAP değil). LDAP = `/api/directory/sync`.

### 3.1 UI (24 Mayıs 2026)

| Konu | Durum |
|------|--------|
| Yerel Mng.Ui → sunucu Keeper | ✅ `GATEWAY_URL=http://192.168.20.20:5040` |
| K1.6 AD giriş + manager JWT | ✅ |
| K5d kullanıcı listesi / edit / profil | ✅ `fieldPolicies` + Yerel/Kurumsal rozet |
| G1 grup listesi / edit / üye yönetimi | ✅ Kurumsal gruplar salt okunur |
| Domain → dizin sync butonu | ✅ `POST /api/directory/sync` — body `triggeredBy: 0` (Manual) |
| Sunucu **mngui** (K5d/G1 kartları) | ✅ Deploy — `http://192.168.20.20:3000` |

---

## 4. Onaylı sıra (güncel)

```
✅ K1 → ✅ K2 + P0 + K4 → ✅ K3 Scheduler → ✅ Keeper 1.3.4
    → ✅ K1.6 + K5 + G1 (kullanıcı + grup + domain sync UI)
    → ⬜ Opsiyonel: K5e checklist, mngui sunucu deploy, HTTPS
```

**Opsiyonel:** [USER_SOURCES.md](./USER_SOURCES.md) §8 (T13–T20) resmi senaryo checklist.

---

## 5. Mimari kararlar (özet)

### Veri akışı

```
AD → Keycloak (federation; müşteri LDAP’a dokunulmuyoruz)
  → MngKeeper sync (K2 manuel / K3 periyodik / K4 login)
  → JWT (P0 directoryPrivileges + Mongo Groups)
```

### Admin / manager

- `mngkeeper.domains.settings.directoryPrivileges` (`adminGroupNames`, `managerGroupNames`)
- Kod: `admins` / `managers` + domain listesi birleşimi
- Test: [P0_DIRECTORY_PRIVILEGES_TEST.md](./P0_DIRECTORY_PRIVILEGES_TEST.md)
- Mongo’da alanlar **yalnızca** `settings.directoryPrivileges` altında (kökte `adminGroupNames` hata verir)

### Eşzamanlılık

- `IDirectorySyncCoordinator` — domain başına tek aktif tam sync
- Manuel sync sürerken ikinci istek → **409**
- Scheduler aynı domain → 409 ise **skip**

---

## 6. Deploy ve scriptler

| Script | Görev |
|--------|--------|
| [sync-odak-source.ps1](../../../scripts/odak/sync-odak-source.ps1) | PC → `/home/odak/MonitraNG` |
| [deploy-odak-apps.ps1](../../../scripts/odak/deploy-odak-apps.ps1) | Sunucuda compose build/up |
| [deploy-keeper-odak.ps1](../../../scripts/odak/deploy-keeper-odak.ps1) | Keeper + mng_apps sync kısayolu |

SSH (agent): `.env.odak.local` veya `scripts/odak/local-credentials.ps1` (gitignore) — [deploy-odak README](../../../scripts/odak/).

---

## 7. İlgili kod (güncel)

| Alan | Dosyalar |
|------|----------|
| K2/K4 sync | `KeycloakToMongoSyncService.cs`, `DirectorySyncController.cs`, `DirectoryUserFieldSets.cs` |
| K3 Scheduler | `DirectorySyncOrchestrationJob.cs`, `DomainLookupService.cs`, `MngKeeperDirectorySyncClient.cs` |
| P0 | `DirectoryPrivilegeSettings.cs`, `PrivilegeGroupResolver.cs`, `AuthPrivilegeHelper.cs` |
| Keycloak API | `GetRealmUserByUsernameAsync`, `ListRealmUsersAsync` |
| Scalar/Swagger | `Config/Extensions.cs` — `EnableSwagger=true` + Scalar Production |
| Versiyon | **1.3.4** — `MngKeeper/version.ps1` |
| K5 kullanıcı | `UserFieldPolicyService`, `DirectoryUserFieldSets`, `pages/apps/users/*`, `Profile*Tab.vue` |
| G1 grup | `GroupFieldPolicyService`, `DirectoryGroupPolicy`, `pages/apps/groups/*`, `DomainDirectorySyncCard.vue` |
| Directory sync API | `DirectorySyncController.cs` — `triggeredBy`: `0`=Manual, `1`=Scheduled |

---

## 8. Açık teknik sorular (K3 sonrası opsiyonel)

1. K2 her çağrıda Keycloak LDAP full sync tetiklesin mi, yalnızca KC→Mongo?
2. KC’de olmayan Mongo directory kullanıcısı: disable / silme?
3. Tam sync sonrası otomatik DataGateway `SyncAllAsync`?

---

## 9. Doküman ağacı

```
ldap/
├── HANDOFF_UI.md             ← UI özeti (K5 + G1 ✅)
├── GROUP_SOURCES.md          ← G1 grup + domain sync
├── HANDOFF_MNGSCHEDULER.md   ← K3 tamamlandı
├── DEVAM.md                  ← bu dosya
├── DEV_WORKFLOW.md
├── DEPLOY_KEEPER_LDAP.md
├── ODAK_HTTP_AND_GATEWAY.md
├── K4_LOGIN_SYNC.md
├── P0_DIRECTORY_PRIVILEGES_TEST.md
├── SCHEDULER_DIRECTORY_SYNC.md
├── IMPLEMENTATION_PLAN.md
├── USER_SOURCES.md
├── POC_KEYCLOAK_LDAP.md
└── ROADMAP.md
```
