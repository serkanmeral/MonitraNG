# LDAP / AD entegrasyonu — Uygulama planı (Odak)

**Active Directory:** `LDAP://192.168.20.3:389/DC=odak,DC=local`  
**Keycloak (Odak):** `http://192.168.20.20:8080/keycloak/`  
**MonitraNG sunucu:** `192.168.20.20`  
**Son güncelleme:** 22 Mayıs 2026  
**Yarın devam:** [DEVAM.md](./DEVAM.md) (oturum özeti + checklist)

Bu doküman, onaylanan çalışma sırasını ve **MngKeeper** tarafında yapılacakları netleştirir. Genel faz planı için [ROADMAP.md](./ROADMAP.md).

---

## 1. Hedef mimari

**Kaynak sırası (tek doğruluk kaynağı zinciri):**

```
Active Directory (192.168.20.3)
        ↓  User Federation + Sync (Keycloak)
Keycloak realm (domain adı, örn. odak)
        ↓  MngKeeper senkron (K2 manuel / K4 login; K3 → MngScheduler tetikler)
MongoDB mng_{domain} → @users, @groups (+ üyelikler)
        ↓  (mevcut) IDataGatewaySyncService
DataGateway persons / groups (gerektiğinde)

Periyodik: MngScheduler → GET domain list → POST Keeper sync / domain
```

| Katman | Rol |
|--------|-----|
| **AD** | Kurumsal kullanıcı, grup, parola |
| **Keycloak** | LDAP’tan kullanıcı/grup import; login doğrulama |
| **Mongo (Keeper)** | Uygulama verisi, JWT claim kaynağı (`user_groups`, profil), menü izinleri |
| **Login anı** | Keycloak başarılı → Mongo ile **tutarlılık kontrolü** → gerekirse **tek kullanıcı** senkronu |

> **Önemli:** Mevcut `POST /api/sync/*` endpoint’leri yalnızca **MngKeeper Mongo → DataGateway** senkronudur (`SyncController`). LDAP planı için **yeni** Keycloak → Mongo pipeline’ı gerekir.

---

## 2. Active Directory (bilinen)

| Alan | Değer |
|------|--------|
| URL | `ldap://192.168.20.3:389` |
| Base DN | `DC=odak,DC=local` |
| Tam URI (referans) | `LDAP://192.168.20.3:389/DC=odak,DC=local` |

**Ön koşullar:**

- `192.168.20.20` (Keycloak container/host) → `192.168.20.3:389` ağ erişimi
- Keycloak realm’de LDAP User Federation + grup mapper (manuel kurulum — Faz K1)
- Bind hesabı ve arama DN’leri müşteri IT ile netleştirilecek (`REQUIREMENTS.md` — ileride)

---

## 3. Çalışma sırası (özet)

| Sıra | Ne | Nerede | Durum |
|------|-----|--------|--------|
| **K1** | LDAP federation + **manuel** “Sync all users / groups” (bir kerelik + ihtiyaçta tekrar) | Keycloak Admin | ⬜ Operasyon |
| **K2** | MngKeeper: Keycloak → Mongo tam senkron (endpoint) | MngKeeper API | ⬜ Geliştirme |
| **K3** | **MngScheduler:** periyodik job → tüm Active domain’ler → Keeper sync (K2) | MngScheduler + Keeper API | ✅ Deploy — [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) |
| **K4** | MngKeeper: Login sonrası kullanıcı bazlı tutarlılık + gerekirse sync | `GetTokenCommandHandler` | ⬜ Geliştirme |
| **K5** | Kullanıcı kaynağı (Local / Directory): API + profil/kullanıcı UI kısıtları | MngKeeper + Mng.Ui | ⬜ Geliştirme |
| **K5 (DG)** | (Opsiyonel) DataGateway senkronu mevcut `IDataGatewaySyncService` ile | Mevcut `/api/sync` | ✅ Var |

> **K5 detay:** [USER_SOURCES.md](./USER_SOURCES.md) — LDAP kullanıcı düzenleme, şifre, fotoğraf.

---

## 4. Faz K1 — Keycloak (manuel sync)

**Amaç:** AD verisinin Keycloak realm’e alınması; MngKeeper geliştirmesinden önce doğrulama.

| Adım | İş |
|------|-----|
| K1.1 | Realm (ör. `odak`) → User federation → Add LDAP provider (Active Directory) |
| K1.2 | Connection URL: `ldap://192.168.20.3:389`, Users DN / Bind DN / credentials |
| K1.3 | Group LDAP mapper (realm groups veya group mapper) — AD security group → Keycloak group |
| K1.4 | **Sync settings:** “Import users”, “Sync registrations”; ilk kurulumda **Sync all users** ve **Sync LDAP groups to Keycloak** (manuel) |
| K1.5 | Test kullanıcı ile Admin Console’da kullanıcı + grup üyeliği görünür mü kontrol |
| K1.6 | `POST /api/auth/token` (MngKeeper) ile aynı kullanıcı login testi |

**Not:** İleride MngKeeper endpoint’i, Keycloak Admin REST API üzerinden `POST .../user-storage/{id}/sync?action=triggerFullSync` (veya eşdeğeri) çağırarak bu adımı otomatikleştirebilir. İlk entegrasyonda **manuel** yeterlidir.

**Uygulama rehberi:** [POC_KEYCLOAK_LDAP.md](./POC_KEYCLOAK_LDAP.md) — ekran ayarları, checklist, kayıt defteri.

---

## 5. Faz K2 — MngKeeper endpoint (Keycloak → Mongo)

### 5.1 Yeni API (önerilen)

| Method | Path | Açıklama | Yetki |
|--------|------|----------|--------|
| `POST` | `/api/sync/keycloak` veya `/api/directory/sync` | Tam directory senkron pipeline’ını başlatır | Admin (domain) |

**Pipeline adımları (sıralı):**

1. **(Opsiyonel / yapılandırılabilir)** Keycloak LDAP provider üzerinde full sync tetikle (Admin API).
2. Keycloak’tan **tüm kullanıcıları** oku (realm).
3. Keycloak’tan **tüm grupları** oku.
4. Her kullanıcı için **grup üyeliklerini** oku.
5. Mongo `@users` upsert: `keycloakUserId`, `username`, email, ad/soyad, `IsActive` — **yalnızca** [USER_SOURCES §4](./USER_SOURCES.md#4-alan-yönetimi--nasıl-uygulanır) `SyncFromKeycloak` alanları; `photoUrl`, `title`, `department`, `phoneNumber`, `gender` **dokunulmaz**.
6. Mongo `@groups` upsert: grup adı, Keycloak group id (saklanacaksa).
7. Kullanıcı–grup ilişkisi: Mongo `User.Groups` listesi Keycloak ile hizala.
8. Keycloak’ta olmayan / devre dışı kullanıcılar için politika uygula (aşağıda).
9. Senkron özeti döndür: `{ usersCreated, usersUpdated, groupsCreated, usersDisabled, errors[] }`.

### 5.2 Geliştirme — yeni / genişletilecek bileşenler

| Bileşen | Görev |
|---------|--------|
| `IKeycloakService` | `ListUsersAsync`, `ListGroupsAsync`, `GetUserGroupMembershipsAsync`, `TriggerLdapSyncAsync` (realm + federation id) |
| `IKeycloakToMongoSyncService` (yeni) | Yukarıdaki pipeline orchestration |
| `IPrivilegeGroupResolver` | **§5.5** — `AdminGroupNames` / `ManagerGroupNames` ile `isAdmin` / `isManager` |
| `IGroupRepository` | Grup upsert / Keycloak id eşlemesi (yoksa tamamlanır) |
| `UserRepository` | Mevcut `GetByKeycloakUserIdAsync`, `GetByUsernameAsync` kullanımı |
| `DirectorySyncController` veya `SyncController` genişletmesi | HTTP endpoint; 409 when busy |
| `IDirectorySyncCoordinator` | Domain kilidi; manuel / scheduled / (K4) koordinasyonu |
| DTO | `DirectorySyncResult` (+ `code`, `triggeredBy` alanları) |

### 5.3 Senkron kuralları (taslak)

| Konu | Önerilen politika |
|------|-------------------|
| Yeni KC kullanıcı | Mongo’da insert |
| Mevcut (aynı `keycloakUserId` veya `username`) | Alanları güncelle |
| KC’de disabled | Mongo `IsActive = false` (login zaten engellenir) |
| Mongo’da var, KC’de yok | **Yapılandırılabilir:** disable veya silme yok (break-glass yerel kullanıcılar) |
| Grup üyeliği | Keycloak üyeliği = Mongo `Groups` (tam replace veya merge — karar: **tam replace** önerilir) |
| `admins` / `managers` | Bkz. **§5.5** — müşteri LDAP/KC grubu adları **değiştirilmez**; Keeper içinde alias listesi |

### 5.5 Ayrıcalık grupları — Keeper içi eşleme (karar: Yol 2)

**İlke:** Müşterinin AD/LDAP veya Keycloak federation yapılandırmasına **müdahale etmeyiz**. Keycloak’tan gelen **grup adları olduğu gibi** domain DB’de `User.Groups` ve JWT `user_groups` içinde kalır; **admin / manager** kararı **MonitraNG yapılandırmasıyla** verilir.

**Yapılandırma kaynağı (karar):** `AdminGroupNames` / `ManagerGroupNames` listeleri **`mng_keeper` MongoDB** içindeki **`domains`** koleksiyonunda, ilgili domain dokümanında tutulur — `appsettings` değil (çok kiracılı ortam, deploy gerektirmeden güncelleme).

#### Neden `domains` koleksiyonu?

| Gerekçe | Açıklama |
|---------|----------|
| Zaten yükleniyor | `GetTokenCommandHandler` login’de `GetByRealmNameAsync` ile domain çekiyor; ekstra sorgu gerekmez |
| Kiracı bazlı | Her müşteri/domain farklı AD grup adları (Odak: `Odak-Managers`, başka tenant: farklı CN) |
| Mevcut API | `GET /api/domain/{id}`, `GetDomainByName` — operatör / Domain UI ile güncellenebilir |
| Ayrım net | `mng_keeper.domains` = meta + politika; `mng_{domain}` = kullanıcı/grup **verisi** |

#### Önerilen şema (`Domain.Settings` genişletmesi)

Mevcut `DomainSettings` altına typed alt nesne ( `CustomSettings` sözlüğüne gömülmemesi tercih edilir — tip güvenliği ve validasyon):

```csharp
// Domain.cs — DomainSettings içinde
public DirectoryPrivilegeSettings DirectoryPrivileges { get; set; } = new();

public class DirectoryPrivilegeSettings
{
    public List<string> AdminGroupNames { get; set; } = new() { "admins" };
    public List<string> ManagerGroupNames { get; set; } = new() { "managers" };
    // İleride: LdapEnabled, SyncCron, KeycloakLdapComponentId ...
}
```

**Mongo örneği (`domains` dokümanı):**

```json
{
  "name": "odak",
  "realmName": "odak",
  "settings": {
    "maxUsers": 100,
    "directoryPrivileges": {
      "adminGroupNames": [ "admins", "Odak-Admins" ],
      "managerGroupNames": [ "managers", "Odak-Managers", "IT-Operations" ]
    }
  }
}
```

- Alan yok veya liste boş → kod tarafında **`SystemGroups.Admins` / `SystemGroups.Managers`** varsayılanı (geriye uyumluluk).
- `CreateDomain` pipeline / DTO: opsiyonel başlangıç listesi; yoksa varsayılan.

#### `IPrivilegeGroupResolver`

```csharp
bool IsAdmin(Domain domain, IReadOnlyList<string> userGroups);
bool IsManager(Domain domain, IReadOnlyList<string> userGroups); // IsAdmin → true
```

- `GetTokenCommandHandler`, `RefreshTokenCommandHandler`: zaten eldeki `domain` nesnesi ile çağrı.
- Keycloak `IsUserInGroupAsync(..., "managers")` fallback: aynı domain listesindeki **herhangi bir** isim için kontrol (veya sync sonrası yalnızca Mongo).

**Global `appsettings`:** Yalnızca **fallback** (domain dokümanında alan yoksa) veya geliştirme ortamı varsayılanı — üretimde asıl kaynak domain dokümanı.

**LDAP sync ile ilişki:**

1. K1: Müşteri AD → Keycloak.
2. K2: KC grup adları → `mng_{domain}` `@users` / `@groups` (**rename yok**).
3. Login / JWT: `domain.settings.directoryPrivileges` + `User.Groups` → claim’ler.

**Bilinçli olarak yapılmayan:** Müşteri LDAP/KC mapper’ında grup birleştirme veya rename.

| Bileşen | Görev |
|---------|--------|
| `DirectoryPrivilegeSettings` + `DomainSettings` | Mongo şema |
| `IPrivilegeGroupResolver` | Domain + `user.Groups` → `isAdmin` / `isManager` |
| `CreateDomain` / `UpdateDomain` | İsteğe bağlı; **üretimde manuel Mongo** (`domains.settings.directoryPrivileges`) |
| `GET domain` | Mevcut endpoint yanıtta `directoryPrivileges` döner (okuma) |
| `SystemGroups` sabitleri | Kod varsayılanı |

### 5.4 Eşzamanlılık — tek aktif tam sync (zorunlu)

**Kural:** Aynı domain için **tam directory sync** pipeline’ı (K2 / K3) aynı anda yalnızca **bir** kez çalışabilir. Manuel tetik ile **MngScheduler** periyodik job **birbirini dışlar** (kilitleme Keeper’da).

| Durum | Davranış |
|--------|----------|
| Sync **devam ederken** yeni **manuel** `POST` | **409** + mesaj; `triggeredBy`: `Manual` / `Scheduled`. |
| **Manuel** sync sürerken **Scheduler** aynı domain’e POST | Keeper **409** → Scheduler **skip**, log; sonraki domain’e devam ([SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md)). |
| İki manuel istek üst üste | İkincisi aynı şekilde **409**. |
| Sync bitti | Kilit serbest; sonraki manuel veya job normal başlar. |

**Uygulama notları:**

| Bileşen | Görev |
|---------|--------|
| `IDirectorySyncCoordinator` (veya eşdeğeri) | Domain bazlı `SemaphoreSlim` / distributed lock; `TryBeginSync(domainId, trigger)` / `EndSync` |
| Tam sync servisi | Pipeline başında `TryBegin`, `finally` içinde `EndSync` |
| Manuel endpoint | `TryBegin` başarısız → 409 + `DirectorySyncResult` / problem details |
| MngScheduler orchestration | Domain başına POST; 409 → skip, **continue** |
| `[DisallowConcurrentExecution]` | Scheduler orchestration job’da **ek** koruma (aynı cron çift tetiklenmesin) — asıl garanti Keeper **coordinator** |

**Login tek kullanıcı sync (K4):** Tam sync ile **aynı domain kilidini** paylaşmak önerilir: tam sync sürerken K4 ya kısa süre bekler ya da *«full sync in progress»* ile yalnızca okuma/fallback (netleştirilecek). K4, tam sync ile aynı `@users` kaydına eşzamanlı yazmamalı.

**Örnek API yanıtı (409):**

```json
{
  "isSuccess": false,
  "code": "SYNC_ALREADY_RUNNING",
  "message": "Directory sync zaten çalışıyor. Lütfen mevcut işlem bitene kadar bekleyin.",
  "startedAt": "2026-05-22T10:15:00Z",
  "triggeredBy": "Scheduled"
}
```

---

## 6. Faz K3 — Periyodik sync (MngScheduler, çok kiracı)

**Amaç:** Yeni eklenen domain’ler dahil, **çalışma anındaki** tüm uygun tenant’lar için K2 pipeline’ını cron ile tetiklemek.

**Karar:** Zamanlama **MngKeeper Quartz değil** — çok domain / dinamik liste. **MngScheduler** system job (MngAdmin backup ile aynı desen). Ayrıntı: **[SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md)**.

| Konu | Karar |
|------|--------|
| Zamanlayıcı | **MngScheduler** (Quartz zaten var; `JobSyncService` + `@scheduled_jobs`) |
| Domain listesi | Runtime `GET MngKeeper /api/domain` — Active (+ ileride `directorySync.enabled`) |
| Tetik | Domain başına `POST` K2 endpoint, `triggeredBy: Scheduled` |
| Tek URL HttpJob | **Yeterli değil** — orchestration handler (N domain → N POST) |
| Çakışma | Keeper `IDirectorySyncCoordinator` §5.4; Scheduler 409’u **skip** |
| MngKeeper | Sync kodu + endpoint; **periyodik job yok** |

**Geliştirme:** K3a–K3e — bkz. SCHEDULER_DIRECTORY_SYNC §4.4.

---

## 7. Faz K4 — Login anında kullanıcı senkronu

**Konum:** `GetTokenCommandHandler` — Keycloak `GetTokenAsync` **başarılı** olduktan sonra, JWT üretilmeden önce.

```
Login başarılı (Keycloak)
    → Keycloak’tan kullanıcı id + güncel grup üyelikleri al
    → Mongo user var mı? keycloakUserId / username ile
         → Yok veya stale (ör. LastSyncedAt / hash karşılaştırması)
              → SyncSingleUserFromKeycloakAsync(domainId, keycloakUserId)
    → isAdmin / isManager: **§5.5** `IPrivilegeGroupResolver` (Mongo `Groups` + yapılandırılmış alias listesi)
    → Token claim’leri oluşturulur
```

| Kontrol | Açıklama |
|---------|----------|
| Kullanıcı yok | Insert + gruplar |
| `keycloakUserId` farklı | Username çakışması politikası (log + hata veya merge) |
| Grup listesi farklı | Mongo `Groups` güncelle |
| Profil (dizin) | KC → Mongo: email, firstName, lastName (app alanları sync dışı — [USER_SOURCES](./USER_SOURCES.md)) |
| Performans | Her login’de tam realm sync **yapılmaz**; yalnızca **oturum açan kullanıcı** |
| DataGateway | İsteğe bağlı: mevcut `SyncUserToDataGatewayAsync` login sonrası tek kullanıcı için |

**Mevcut kod notu:** Handler zaten Mongo yoksa Keycloak attribute fallback kullanıyor; hedef, federated kullanıcıda **Mongo’nun her zaman güncel** olması.

---

## 8. Senkron kapsamı checklist

| Veri | Keycloak ← AD (K1) | Mongo ← Keycloak (K2/K3/K4) |
|------|----------------------|-----------------------------|
| Kullanıcılar | ✅ Federation sync | ✅ |
| Gruplar | ✅ LDAP group mapper | ✅ |
| Kullanıcı–grup atamaları | ✅ KC group membership | ✅ `User.Groups` |
| Parola | AD (KC doğrular) | Mongo’da tutulmaz |
| Profil (ad, email) | KC user attributes | ✅ (unvan/telefon uygulama alanı — sync etmez) |
| DataGateway persons | — | Mevcut sync servisi (ayrı tetik) |

---

## 9. Yapılandırma (appsettings taslağı)

**MngKeeper** (`DirectorySync`):

```json
{
  "DirectorySync": {
    "TriggerKeycloakLdapSync": false,
    "StaleUserPolicy": "DisableInMongo",
    "LoginSyncEnabled": true
  }
}
```

**MngScheduler** — cron ve orchestration: [SCHEDULER_DIRECTORY_SYNC.md §5](./SCHEDULER_DIRECTORY_SYNC.md#5-appsettings-taslak). `Domains: [ "odak" ]` **kullanılmaz** (liste runtime).

**Admin / manager grup listeleri** Keeper’da değil; domain dokümanında `settings.directoryPrivileges` (bkz. **§5.5**).

---

## 10. Test planı

| # | Senaryo |
|---|---------|
| T1 | K1 manuel sync sonrası KC’de N kullanıcı, M grup |
| T2 | `POST /api/sync/keycloak` → Mongo’da N kullanıcı, üyelikler doğru |
| T3 | AD’de yeni kullanıcı → KC sync → Keeper endpoint → Mongo’da görünür |
| T4 | AD’de gruba ekleme → sync → JWT `user_groups` / menü izni güncellenir |
| T5 | İlk login (Mongo boş kullanıcı) → K4 tek kullanıcı sync → token doğru claim |
| T6 | MngScheduler job log’da periyodik başarı (çok domain) |
| T21–T24 | Scheduler orchestration — [SCHEDULER_DIRECTORY_SYNC.md §6](./SCHEDULER_DIRECTORY_SYNC.md#6-test-senaryoları-ek) |
| T7 | Yerel break-glass admin (KC’de yok) → silinmez / login çalışır |
| T8 | Tam sync sürerken ikinci manuel `POST` → **409**, mesaj Türkçe/İngilizce |
| T9 | Manuel sync sürerken Scheduler aynı domain → 409 skip |
| T10 | Sync bittikten sonra manuel + job sırayla sorunsuz tamamlanır |
| T11 | Kullanıcı yalnızca `Odak-Managers` (listedeki alias) → JWT `is_manager=true`, `managers` üyeliği şart değil |
| T12 | `ManagerGroupNames` yalnızca varsayılan → mevcut `managers` grubu davranışı değişmez |

---

## 11. Açık teknik kararlar

1. Keycloak LDAP sync API’sini endpoint’ten **her zaman** tetiklemek mi, yalnızca operatör manuel + job sadece KC→Mongo mu?
2. Mongo’da KC’de olmayan kullanıcı: disable mı, silme mi, dokunma mı?
3. ~~Grup eşlemesi: AD → KC rename~~ → **Kapandı:** müşteri LDAP’a dokunmuyoruz; **§5.5** alias listesi.
4. ~~`AdminGroupNames` nerede~~ → **Kapandı:** `mng_keeper.domains.settings.directoryPrivileges`. Odak ilk değerleri: migration script veya `PUT /api/domain/{id}`.
5. ~~Quartz tek instance~~ → **MngScheduler** tek instance yeterli (Odak); Keeper’da Quartz directory sync **yok**
6. Sync sonrası otomatik `IDataGatewaySyncService.SyncAllAsync` çağrılsın mı?

---

## 12. İlgili kod (mevcut)

| Dosya | Not |
|-------|-----|
| `MngKeeper.Api/Controllers/SyncController.cs` | Sadece → DataGateway |
| `GetTokenCommandHandler.cs` | Login; Mongo/KC fallback |
| `IKeycloakService` | List/sync metotları **eklenecek** |
| `IDataGatewaySyncService` | User/group → DG Mongo |
| `LicenseValidationBackgroundService` | Örnek hosted background pattern |

---

## 13. Onaylanan kararlar özeti

| # | Karar |
|---|--------|
| 1 | AD → Keycloak (müşteri federation); Keeper KC → Mongo |
| 2 | Müşteri LDAP/KC mapper’ına **müdahale yok** |
| 3 | Admin/manager: **`mng_keeper.domains.settings.directoryPrivileges`** (§5.5) |
| 4 | Tam sync: tek aktif iş; manuel ↔ Scheduler §5.4 |
| 7 | Periyodik sync: **MngScheduler** domain listesi + Keeper POST; Keeper’da Quartz yok — [SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md) |
| 5 | Mevcut `SyncController` ≠ LDAP; yeni directory sync endpoint |
| 6 | Alan whitelist: sync 7 alan; UI 5 app alanı; `fieldPolicies` API — [USER_SOURCES.md](./USER_SOURCES.md) |

## 14. Faz K5 — Kullanıcı kaynakları ve UI (Local vs Directory)

**Amaç:** İki kaynaklı kullanıcı modeli; alan sahipliği whitelist + `fieldPolicies` API.

| Kaynak | Oluşturma | Sync (K2/K4) yazar | UI düzenler | Şifre |
|--------|-----------|-------------------|-------------|--------|
| **Local** | `POST /api/user` | — (manuel/KC) | Tüm alanlar | Evet |
| **Directory** | AD + sync | `username`, `email`, ad/soyad, `groups`, `isActive` | `photoUrl`, `gender`, `title`, `department`, `phoneNumber` | Hayır |

**Tam doküman:** [USER_SOURCES.md](./USER_SOURCES.md) — `DirectoryUserFieldSets`, `IUserFieldPolicyService`, UI, T13–T20.

**Sync ile bağ:** K2/K4 merge yalnızca `SyncFromKeycloak`; uygulama alanları **asla** overwrite edilmez.

---

## 15. Sonraki adım

Öncelik sırası — ayrıntılı checklist: **[DEVAM.md](./DEVAM.md)**.

1. **P0:** `DirectoryPrivilegeSettings` + `IPrivilegeGroupResolver`  
2. **K1:** Keycloak AD federation + manuel sync (`192.168.20.3`)  
3. **K2:** `IKeycloakToMongoSyncService` + endpoint + coordinator  
4. **K4:** Login tek kullanıcı sync  
5. **K3:** MngScheduler orchestration job ([SCHEDULER_DIRECTORY_SYNC.md](./SCHEDULER_DIRECTORY_SYNC.md))  
6. **K5:** `provisioningSource` + UpdateUser guard + Mng.Ui ([USER_SOURCES.md](./USER_SOURCES.md))  
7. `REQUIREMENTS.md` (bind DN) — K1 ile paralel
