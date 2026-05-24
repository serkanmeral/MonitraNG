# K5 — Yerel Keeper testi (dotnet run)

**Sürüm:** 1.3.1+ (K5a/K5b — `fieldPolicies`, Directory `UpdateUser` guard)  
**Son güncelleme:** 23 Mayıs 2026

Deploy öncesi bu PC’de **Odak sunucu altyapısına** (Mongo, Keycloak @ `192.168.20.20`) bağlanarak test edin.

---

## 1. Hazırlık

```powershell
cd MngKeeper\Presentation\MngKeeper.Api
copy appsettings.Development.example.json appsettings.Development.json
# Gerekirse Keycloak/Mongo parolalarını düzenleyin
```

`.env` gerekmez; ayarlar `appsettings.Development.json` içinde.

---

## 2. Çalıştırma

```powershell
cd MngKeeper\Presentation\MngKeeper.Api
dotnet run
```

- API: http://localhost:5001  
- Scalar (dev): http://localhost:5001/scalar/v1  
- Swagger: http://localhost:5001/swagger

---

## 3. Unit testler

```powershell
cd MngKeeper
dotnet test tests/MngKeeper.Application.Tests/MngKeeper.Application.Tests.csproj
```

Kapsam: `UserFieldPolicyService`, `DirectoryUserUpdateValidator`.

---

## 4. Manuel API testi (K5)

### 4.1 Token

```http
POST http://localhost:5001/api/auth/token
Content-Type: application/json

{ "username": "odak_admin", "password": "...", "domain": "odak" }
```

### 4.2 Kullanıcı listesi — yeni alanlar

```http
GET http://localhost:5001/api/user?page=1&pageSize=20
Authorization: Bearer {token}
```

Her kullanıcıda beklenen:

- `provisioningSource`: `"Local"` veya `"Directory"`
- `capabilities`: `{ canChangePassword, canManageGroups, canDeactivate, canDelete }`
- `fieldPolicies`: alan bazlı `{ editable, source }`

### 4.3 Directory kullanıcı — GET tek

AD sync sonrası bir Directory kullanıcı `userId` ile:

```http
GET http://localhost:5001/api/user/{userId}
```

`fieldPolicies.email.editable` → `false`  
`fieldPolicies.title.editable` → `true`

### 4.4 Directory — email değiştirme reddi (T13)

```http
PUT http://localhost:5001/api/user/{directoryUserId}
Authorization: Bearer {token}
Content-Type: application/json

{
  "username": "...",
  "email": "farkli@ornek.com",
  "firstName": "...",
  "lastName": "...",
  "isActive": true
}
```

Beklenen: `isSuccess: false`, mesajda `DIRECTORY_FIELD_NOT_EDITABLE`.

### 4.5 Directory — uygulama alanı güncelleme (T15)

Aynı kullanıcı; email/username değiştirmeden yalnızca `title` / `photoUrl` güncelle → **200**.

### 4.6 Local kullanıcı — mevcut davranış (T17)

Yerel kullanıcıda tam güncelleme + Keycloak sync eskisi gibi çalışmalı.

---

## 5. Deploy (testler tamam)

```powershell
# Repo kökünden
.\scripts\odak\sync-odak-source.ps1
.\scripts\odak\deploy-keeper-odak.ps1
```

Detay: [DEPLOY_KEEPER_LDAP.md](./DEPLOY_KEEPER_LDAP.md), [DEV_WORKFLOW.md](./DEV_WORKFLOW.md).

---

## 6. Sonraki adım (UI)

Keeper deploy sonrası **K5d** — [HANDOFF_UI.md](./HANDOFF_UI.md): `users/index.vue` rozeti, `fieldPolicies` tüketimi.
