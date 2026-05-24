# Kullanıcı kaynakları — Uygulama vs LDAP (planlama)

**Son güncelleme:** 24 Mayıs 2026  
**Durum:** ✅ K5a–d uygulandı ve Odak’ta doğrulandı — K5e checklist opsiyonel  
**İlişki:** [IMPLEMENTATION_PLAN.md](./IMPLEMENTATION_PLAN.md) (K2/K4 sync, K5), [DEVAM.md](./DEVAM.md)

MonitraNG’de **iki kullanıcı kaynağı** vardır. Alan sahipliği **whitelist** ile yönetilir; UI backend’den gelen `fieldPolicies` / `capabilities` ile kısıtlanır.

---

## 1. Kaynak türleri

| Kaynak | Kod | Nasıl oluşur | Kim yönetir kimliği |
|--------|-----|--------------|---------------------|
| **Uygulama (yerel)** | `Local` | `POST /api/user`, domain pipeline admin | MonitraNG + Keycloak yerel kullanıcı |
| **Dizin (LDAP/AD)** | `Directory` | AD → Keycloak federation → K2/K3/K4 sync | Kurumsal dizin (+ KC sync) |

**Break-glass:** `ProvisioningSource = Local`; directory sync bu kaydı **güncellemez** (IMPLEMENTATION_PLAN stale policy).

---

## 2. Ürün ilkeleri

| Kural | Açıklama |
|-------|----------|
| Tek yazıcı per alan | Bir alan ya dizin ya uygulama; sync ile UI çakışmaz |
| Directory — dizin alanları | Salt okunur; `PUT` ve KC update **reddedilir** |
| Directory — uygulama alanları | Kullanıcı + admin düzenleyebilir; sync **asla ezmez** |
| Directory — şifre | Yok (AD); güvenlik sekmesi gizli |
| Directory — gruplar | Sync ile gelir; UI’da **salt okunur liste** (checkbox yok) |
| Directory — oluşturma | `POST /api/user` → **403** |
| Local | Mevcut tam CRUD + şifre + KC güncelleme |

---

## 3. Onaylanan alan matrisi (Odak v1)

### 3.1 Özet tablo

| Alan (`User`) | Sınıf | K2/K4 sync yazar? | Directory — UI düzenleme | Local — UI |
|---------------|--------|-------------------|---------------------------|------------|
| `username` | Dizin | ✅ | Salt okunur | Düzenlenebilir* |
| `email` | Dizin | ✅ (varsa) | Salt okunur; LDAP’ta yoksa **null** (zorunlu değil) | Düzenlenebilir |
| `firstName`, `lastName` | Dizin | ✅ | Salt okunur | Düzenlenebilir |
| `groups` | Dizin | ✅ (tam replace) | Salt okunur liste | Checkbox |
| `isActive` | Dizin | ✅ | Toggle kapalı | Toggle |
| `keycloakUserId` | Sistem | ✅ (eşleme) | Görünmez | Görünmez |
| `photoUrl` | Uygulama | ❌ | Düzenlenebilir | Düzenlenebilir |
| `gender` | Uygulama | ❌ | Düzenlenebilir | Düzenlenebilir |
| `title` | Uygulama | ❌ | Düzenlenebilir | Düzenlenebilir |
| `department` | Uygulama | ❌ | Düzenlenebilir | Düzenlenebilir |
| `phoneNumber` | Uygulama | ❌ | Düzenlenebilir | Düzenlenebilir |
| `roles` | Sistem | ❌ (sync dışı) | Dokunulmaz | Politikaya göre |
| `customData` | Uygulama | ❌ | DataGateway; Keeper form dışı | Aynı |
| Parola | Dizin / Local | — | Yok | Var |

\* `username` Local’de create sonrası değişim politikası mevcut API ile aynı kalır.

### 3.2 Neden `title` / `department` / `phone` uygulama alanı? (Seçenek A)

- Odak v1: K1/K2 **yalnızca** kimlik + grup + aktiflik sync eder; AD attribute mapper zorunluluğu yok.
- Kullanıcı unvan/telefonu MonitraNG profilinde tutar; LDAP sync **üzerine yazmaz**.
- İleride müşteri “AD’den gelsin” derse: alan `DirectorySyncFields`’e eklenir + UI readonly + `fieldPolicies` güncellenir (domain config veya sürüm notu).

### 3.3 LDAP / AD eşlemesi (referans — sync edilenler)

| Mongo | Keycloak / AD (tipik) |
|-------|------------------------|
| `username` | `username` / `sAMAccountName` |
| `email` | `email` / `mail` |
| `firstName` | `firstName` / `givenName` |
| `lastName` | `lastName` / `sn` |
| `groups` | KC group membership |
| `isActive` | `enabled` |

---

## 4. Alan yönetimi — nasıl uygulanır?

### 4.1 İki whitelist (Keeper)

```csharp
/// <summary>Directory kullanıcı — K2/K3/K4 yalnızca bunları günceller.</summary>
public static class DirectoryUserFieldSets
{
    public static readonly IReadOnlySet<string> SyncFromKeycloak = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "username", "email", "firstName", "lastName",
        "groups", "isActive", "keycloakUserId"
    };

    /// <summary>Directory kullanıcı — PUT /api/user ve profil güncellemesi.</summary>
    public static readonly IReadOnlySet<string> EditableByApplication = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "photoUrl", "gender", "title", "department", "phoneNumber"
    };
}
```

| İşlem | Local | Directory |
|--------|-------|-----------|
| `IKeycloakToMongoSyncService` | N/A | Yalnızca `SyncFromKeycloak` |
| `UpdateUserCommandHandler` | Tüm alanlar + KC | Merge yalnızca `EditableByApplication`; diğer alan değiştiyse **400** |
| `CreateUserCommandHandler` | `ProvisioningSource = Local` | — |
| KC `UpdateUserAsync` / şifre | ✅ | ❌ |

**Kural:** `SyncFromKeycloak` ∩ `EditableByApplication` = **boş** (çakışma yok).

### 4.2 `IUserFieldPolicyService` (öneri)

```csharp
UserFieldPolicyDto GetPolicies(User user, Domain domain);
// → provisioningSource, capabilities, fieldPolicies per property
```

- `fieldPolicies["email"]` → `{ "editable": false, "source": "directory" }`
- UI hardcode `disabled` listesi **kullanmaz**; API cevabına güvenir.
- İleride `domain.settings.directorySync.extraSyncFields` ile müşteri bazlı genişletme (v2).

### 4.3 API yanıtı (örnek)

```json
{
  "userId": "...",
  "provisioningSource": "Directory",
  "capabilities": {
    "canCreateViaApi": false,
    "canChangePassword": false,
    "canManageGroups": false,
    "canDeactivate": false,
    "canDelete": false
  },
  "fieldPolicies": {
    "username": { "editable": false, "source": "directory" },
    "email": { "editable": false, "source": "directory" },
    "firstName": { "editable": false, "source": "directory" },
    "lastName": { "editable": false, "source": "directory" },
    "groups": { "editable": false, "source": "directory" },
    "isActive": { "editable": false, "source": "directory" },
    "photoUrl": { "editable": true, "source": "app" },
    "gender": { "editable": true, "source": "app" },
    "title": { "editable": true, "source": "app" },
    "department": { "editable": true, "source": "app" },
    "phoneNumber": { "editable": true, "source": "app" }
  }
}
```

**400 örneği (directory alan gönderildi):**

```json
{
  "code": "DIRECTORY_FIELD_NOT_EDITABLE",
  "message": "Kurumsal hesap alanları uygulama üzerinden güncellenemez.",
  "rejectedFields": ["email", "firstName"]
}
```

### 4.4 Sync merge davranışı (Directory)

```
Mevcut Mongo user (Directory)
    → KC’den okunan değerler
    → Yalnızca SyncFromKeycloak alanları assign
    → photoUrl, gender, title, department, phoneNumber: DOKUNMA
```

---

## 5. Veri modeli

```csharp
public enum UserProvisioningSource { Local = 0, Directory = 1 }

[BsonElement("provisioningSource")]
public UserProvisioningSource ProvisioningSource { get; set; } = UserProvisioningSource.Local;

[BsonElement("directorySyncedAt")]
public DateTime? DirectorySyncedAt { get; set; }
```

| Olay | `provisioningSource` |
|------|----------------------|
| `CreateUser` başarılı | `Local` |
| K2/K3/K4 insert veya federation kullanıcı sync | `Directory` |
| Break-glass | `Local` (sync atlar) |

---

## 6. UI (Mng.Ui)

| Sayfa | Directory davranışı |
|-------|---------------------|
| `pages/apps/users/index.vue` | Rozet + isteğe bağlı filtre Yerel / Kurumsal (v1: rozet yeterli) |
| `pages/apps/users/create/index.vue` | Değişmez (yalnızca Local) |
| `pages/apps/users/edit/[id].vue` | `fieldPolicies`: dizin alanları disabled; gruplar **chip/liste**; app alanları + foto açık |
| `components/apps/profile/ProfileGeneralTab.vue` | Dizin alanları readonly + hint |
| `components/apps/profile/ProfileSecurityTab.vue` | Gizli veya bilgi metni |
| `stores/apps/user.ts` | `fieldPolicies` / `capabilities` sakla |

**Layout (admin edit):** Üst blok — salt okunur kimlik özeti; alt blok — “MonitraNG profil bilgileri” (foto, unvan, telefon, cinsiyet).

**i18n anahtarları:** `users.source.directory`, `users.directory.fieldReadOnly`, `profile.security.directoryPasswordManaged`, `users.directory.groupsManagedExternally`.

---

## 7. K5 iş paketi

| Kod | İş |
|-----|-----|
| **K5a** | `ProvisioningSource` + `DirectoryUserFieldSets` + `IUserFieldPolicyService` + DTO |
| **K5b** | `UpdateUser` guard; şifre/KC update Directory’de kapalı |
| **K5c** | K2/K4 sync — whitelist merge |
| **K5d** | Mng.Ui `fieldPolicies` tüketimi |
| **K5e** | Test T13–T20 |

**Sıra:** K5a → K5c (K2/K4 ile) → K5b → K5d.

---

## 8. Test senaryoları

| # | Senaryo | Beklenen |
|---|---------|----------|
| T13 | Directory — admin `email` değiştirme | UI disabled; API 400 |
| T14 | Directory — `photoUrl` güncelle | 200; sonraki sync foto korur |
| T15 | Directory — `title` güncelle | 200; sync unvanı değiştirmez |
| T16 | Directory — profil güvenlik | Şifre formu yok |
| T17 | Local kullanıcı | Mevcut davranış |
| T18 | `POST /api/user` (Directory senaryosu) | 403 |
| T19 | Directory — admin grup checkbox | Yok; salt okunur liste |
| T20 | K2 sync sonrası Directory — app alanları | Önceki `title`/`photoUrl` aynı |

---

## 9. İleride (v2 — dokunmadan plan)

| Konu | Tetik |
|------|--------|
| `title`/`department`/`phone` AD’den sync | Müşteri talebi + K1 attribute mapper |
| `domain.settings.directorySync.syncProfileFields` | Kiracı bazlı whitelist |
| Directory kullanıcı silme UI | Ayrı politika kararı |

---

## 10. İlgili kod

| Dosya | Durum |
|-------|--------|
| `User.cs`, `UserFieldPolicyService`, `UserDtoMapper` | ✅ K5 |
| `Group.cs`, `GroupFieldPolicyService`, `DirectoryGroupPolicy` | ✅ G1 |
| `pages/apps/users/*`, `pages/apps/groups/*`, `DomainDirectorySyncCard.vue` | ✅ UI |

---

## 11. Onaylanan kararlar özeti

| # | Karar |
|---|--------|
| 1 | İki kaynak: **Local** / **Directory** |
| 2 | Alan yönetimi: **`DirectoryUserFieldSets`** sync + edit whitelist |
| 3 | Sync, uygulama alanlarına **asla yazmaz** |
| 4 | Odak v1: `title`, `department`, `phoneNumber` → **uygulama** (AD sync yok) |
| 5 | `gender`, `photoUrl` → **uygulama** |
| 6 | Kimlik + grup + aktiflik → **dizin**; UI salt okunur; gruplar liste |
| 7 | UI: backend **`fieldPolicies` + `capabilities`** (hardcode alan listesi yok) |
| 8 | Directory: silme UI **gizli**; devre dışı AD’de |
| 9 | Liste: kaynak **rozeti** (filtre opsiyonel v1) |
