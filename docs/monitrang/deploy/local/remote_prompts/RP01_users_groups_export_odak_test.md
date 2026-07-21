# RP01 — Odak test: kullanıcı ve grup JSON export

**Kullanım:** Bu dosyanın **PROMPT** bölümünü müşteri terminalindeki Cursor’a yapıştırın.  
**Ortam:** Yalnızca **test** `192.168.20.20` — production’a dokunmayın.  
**Çıktı:** İki (veya üç) JSON dosyası; lokal PC’ye taşımak için.  
**Sonraki adım (lokal):** JSON’da Local dönüşümü + Keeper’da Create group/user.

İş akışı: [../REMOTE_CURSOR_WORKFLOW.md](../REMOTE_CURSOR_WORKFLOW.md)

---

## PROMPT (aşağıyı kopyala)

```
MonitraNG repo kökündesin (müşteri terminal PC). Görev: Odak TEST ortamından tüm Keeper kullanıcılarını ve gruplarını JSON dosyalarına aktarmak. Import / CreateUser YAPMA — sadece export.

## Ortam (zorunlu)
- Gateway: http://192.168.20.20:5040
- Domain: odak
- Production (192.168.20.8) KULLANMA
- Parola / token’ı chat’e yazma; mevcut scriptleri kullan

## Token
Repo kökünden:
  pwsh -File .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
veya load-operationcore-token.ps1
Token genelde: $env:TEMP\operationcore_dg_token.txt
Authorization: Bearer <token>
Gerekirse header: X-Domain-Name: odak

## API (Gateway üzerinden)
Kullanıcı listesi (sayfalı; pageSize yüksek tut, tüm sayfaları birleştir):
  GET http://192.168.20.20:5040/keeper/api/user?page=1&pageSize=100
Grup listesi:
  GET http://192.168.20.20:5040/keeper/api/group?page=1&pageSize=100

NOT: /keeper/api/user/export?format=json ve group/export YETERSİZ — userId, groupId, provisioningSource yok. Mutlaka sayfalı GET kullan.

GetUsers / GetGroups yanıtındaki users/groups (veya items) dizisini topla; totalPages bitene kadar page++.

## Çıktı klasörü
Oluştur (yoksa):
  C:\Users\monitra\Dev\exports\odak-keeper-YYYYMMDD\
veya kullanıcı home altında benzeri bir exports yolu (repo içine KOYMA / commit etme).

Dosyalar:
1) groups.json
2) users.json
3) manifest.json (opsiyonel ama tercih edilir)

### groups.json şeması
{
  "exportedAt": "<ISO8601 UTC>",
  "source": { "host": "192.168.20.20", "domain": "odak", "gateway": "http://192.168.20.20:5040" },
  "count": <n>,
  "groups": [ /* API’den gelen her grup objesi olduğu gibi; en az: groupId, name, description, isActive, provisioningSource, memberCount varsa */ ]
}

### users.json şeması
{
  "exportedAt": "<ISO8601 UTC>",
  "source": { "host": "192.168.20.20", "domain": "odak", "gateway": "http://192.168.20.20:5040" },
  "count": <n>,
  "users": [ /* API user objeleri; en az: userId, keycloakUserId, username, email, firstName, lastName, title, department, phoneNumber, gender, isActive, includeInApplication, provisioningSource, directorySyncedAt, groups (isim listesi) */ ]
}

### manifest.json
{
  "exportedAt": "...",
  "source": { ... },
  "groupsFile": "groups.json",
  "usersFile": "users.json",
  "groupCount": <n>,
  "userCount": <n>,
  "provisioningSourceBreakdown": { "Local": <n>, "Directory": <n>, "other": <n> },
  "notes": "Passwords not included. For local import: set all provisioningSource to Local; assign new local default password; remap group membership by group name after creating groups first."
}

## Kurallar
- Şifre alanı yok / ekleme
- Fotoğraf binary indirme — sadece metadata (photoUrl varsa string kalsın)
- Hata olursa HTTP status + body özetini göster; kısmi dosya bırakma
- İş bitince: dosya yollarını, count’ları, Local vs Directory sayılarını chat’te özetle
- PowerShell 7 (pwsh) tercih et; Invoke-RestMethod ile sayfalama script’i yazıp çalıştırabilirsin

## Başarı kriteri
- groups.json ve users.json oluştu
- count > 0 (veya gerçekten boşsa manifest’te açıkça belirt)
- Her user’da userId + username + provisioningSource var
- Her group’ta groupId + name (veya eşdeğeri) var
```

---

## Lokal’e taşıma (siz)

1. Terminalden `exports\odak-keeper-...` klasörünü bu PC’ye kopyalayın.  
2. Repoya commit **etmeyin** (gitignore path / kişisel klasör).  
3. Burada import + Local normalize prosedürüne geçin.
