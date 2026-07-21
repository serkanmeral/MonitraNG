# Domain ve URL yapılandırması (lokal)

## Amaç

Lokal Docker Desktop’ta tarayıcı ve servislerin birbirini doğru host adları / portlarla bulması; Keeper’da tek tenant domain’in kurulması.

## Tenant / Keeper domain — karar

| Konu | Karar |
|------|--------|
| Domain sayısı | **Tek domain** |
| Domain adı | **`odak`** |
| LDAP / AD | **Yok** |
| Müşteri erişimi | Adım 1 için **gerekmez** (lokal stack yeterli) |

---

## Adım 1 — Lokal: temizle + `odak` oluştur

### Önkoşul (bu makinede doğrulandı)

Docker Desktop ayakta; `mng_common` + `mngkeeper` + `mngdomainui` + Keycloak + Mongo çalışıyor.

| Servis | Lokal URL |
|--------|-----------|
| MngDomainUI | http://localhost:3001/domain/ | master `admin` / `admin123` |
| MngKeeper | http://localhost:5001 | |
| Keycloak | http://localhost:8080/keycloak/... | `admin` / `admin123` |
| Mongo Express | http://localhost:8081 | auth yok |

Tam liste: [../../localdocker/CREDENTIALS.md](../../localdocker/CREDENTIALS.md)

### Mevcut durum

| Domain | Status | DB | Realm | Bucket | Not |
|--------|--------|-----|-------|--------|-----|
| `odak` | **Active** (1) | `mng_odak` | `odak` | `mng-odak` | Adım 1 tamam — 2026-07-11 |

### 1a — DomainUI ile kısmi temizlik

http://localhost:3001/domain/ → **Clear All Domains**

- Siler: Keycloak realm’leri (`master` hariç), MinIO `mng-*` bucket’ları
- **Silmez:** MongoDB `mng_*` DB’leri ve Keeper `domains` meta kayıtları → **manuel gerekir**

### 1b — Mongo manuel temizlik (zorunlu)

Örnek (mongo container; parola compose’a göre):

```powershell
docker exec -it mongo mongosh -u admin -p admin123 --authenticationDatabase admin --eval "
  db.getSiblingDB('mngkeeper').domains.deleteMany({});
  db.adminCommand({ listDatabases: 1 }).databases
    .map(d => d.name)
    .filter(n => n.startsWith('mng_') && n !== 'mngkeeper' && n !== 'mng_templates')
    .forEach(n => { print('Dropping ' + n); db.getSiblingDB(n).dropDatabase(); });
"
```

`mngkeeper` meta DB’sini **drop etmeyin**; yalnızca `domains` koleksiyonunu ve tenant `mng_*` DB’lerini temizleyin. `mng_templates` varsa şablon için bırakılabilir.

### 1c — `odak` oluştur (DomainUI)

1. http://localhost:3001/domain/ — Keycloak **master** admin ile giriş
2. **Create Domain**
3. Örnek alanlar:

| Alan | Öneri |
|------|--------|
| Domain Name | `odak` |
| Display Name | `Odak` |
| Admin Email | örn. `admin@odak.local` |
| Admin Password | lokal test parolası (gitignore not; dokümana yazma) |
| Initial Data Template | Şimdilik boş (veri Adım 3 dump ile) |

4. Pipeline bitince doğrula: liste Active · Keycloak realm `odak` · Mongo `mng_odak`

Ayrıntılı form kuralları: [docs/odak/domain/DOMAIN_OLUSTURMA.md](../../../odak/domain/DOMAIN_OLUSTURMA.md)

### 1d — Checklist

- [x] Clear All Domains (Keycloak + MinIO)
- [x] Mongo `domains` + eski `mng_*` tenant DB drop
- [x] `odak` oluşturuldu, Active (realm + bucket doğrulandı)
- [ ] Domain admin ile login smoke (Adım 2 öncesi / sırasında)

---

## URL / hosts — karar (TBD)

| Seçenek | Artı | Eksi |
|---------|------|------|
| A — `*.monitra.local` + Windows hosts | Prod’a yakın | hosts bakımı |
| B — Yalnızca `localhost` + portlar | Basit | CORS farkları |

**Seçilen:** _TBD_ (Adım 1 için localhost yeterli)

## URL matrisi (lokal — mevcut)

| Servis | Lokal URL |
|--------|-----------|
| UI | http://localhost:4000 |
| Domain UI | http://localhost:3001/domain/ |
| Gateway | http://localhost:5040 |
| Keycloak | http://localhost:8080 |
| Keeper | http://localhost:5001 |
