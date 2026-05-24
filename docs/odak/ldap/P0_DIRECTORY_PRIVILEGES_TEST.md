# P0 — directoryPrivileges test (Odak)

**Yapılandırma:** `mngkeeper.domains` dokümanında `settings.directoryPrivileges` — **manuel Mongo** (ayrı CRUD API yok).  
Login sırasında kod `admins` / `managers` varsayılanlarını domain listesiyle birleştirir.

---

## 1. Mongo — admin testi (`MonitraNG Users`)

**Önemli:** Alanlar yalnızca `settings.directoryPrivileges` altında olmalı.  
`adminGroupNames` / `managerGroupNames` kökte veya yalnızca `settings.*` altında yazılırsa Keeper domain okuyamaz (`Domain with realm 'odak' not found`).

Yanlış alanları temizlemek için:

```javascript
use mngkeeper

db.domains.updateOne(
  { name: "odak" },
  {
    $unset: { adminGroupNames: "", managerGroupNames: "" },
    $set: {
      "settings.directoryPrivileges": {
        adminGroupNames: ["MonitraNG Users"],
        managerGroupNames: []
      },
      updatedAt: new Date()
    }
  }
)
```

Doğru güncelleme (temiz doküman):

```javascript
use mngkeeper

db.domains.updateOne(
  { name: "odak" },
  {
    $set: {
      "settings.directoryPrivileges": {
        adminGroupNames: ["MonitraNG Users"],
        managerGroupNames: []
      },
      updatedAt: new Date()
    }
  }
)

// Doğrulama
db.domains.findOne(
  { name: "odak" },
  { "settings.directoryPrivileges": 1, name: 1 }
)
```

Etkili admin grupları (kod): `admins`, `MonitraNG Users`  
Etkili manager grupları (kod): `managers`

---

## 2. Keeper çalıştır + login

```powershell
cd MngKeeper\Presentation\MngKeeper.Api
dotnet run
```

1. Pilot kullanıcının `mng_odak.@users.groups` içinde `MonitraNG Users` olduğundan emin olun (K2 sync).
2. `POST /api/auth/token` — `domain: "odak"`, pilot kullanıcı/parola.
3. JWT → admin claim **true** olmalı.

---

## 3. Manager testi (aynı LDAP grubu)

```javascript
db.domains.updateOne(
  { name: "odak" },
  {
    $set: {
      "settings.directoryPrivileges": {
        adminGroupNames: [],
        managerGroupNames: ["MonitraNG Users"]
      }
    }
  }
)
```

Yeniden login → admin **false**, manager **true** (grup üyeliği varsa).

---

## 4. İsteğe bağlı okuma

`GET /api/domain/name/odak` yanıtında `settings.directoryPrivileges` görünür (genel domain GET; ayrı endpoint yok).
