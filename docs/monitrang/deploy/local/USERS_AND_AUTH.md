# Kullanıcılar, gruplar ve kimlik (lokal)

## Adım 2 durumu (2026-07-11)

| | Sonuç |
|--|--------|
| Export | `docs/odak/exports/odak-keeper-20260711/` |
| Import script | `scripts/tests/MngKeeper/users/import-odak-export-local.ps1` |
| Ortak şifre (import edilen kullanıcılar) | `Sm123!?` |
| Domain admin (oluşturma) | `odak_admin` / `Admin123!` |
| Gruplar | 69 oluşturuldu + 6 mevcut (admins/managers/users/guests/…) atlandı = 75 hedef |
| Kullanıcılar | 177 oluşturuldu; 1 atlandı (`odak_admin`); 2 hata |
| Hatalar | `serkan meral` (username boşluk / beklenmeyen hata); `serkan.meral` (email `sermeral@gmail.com` zaten var — muhtemelen domain admin) |

Import sonrası tüm yeni kayıtlar API Create ile **Local**. Directory sync kapalı kalmalı.

Smoke: Keeper doğrudan `http://localhost:5001` (gateway 5040 bu makinede unhealthy olabilir).

---

## Amaç

Lokal’de login, yetki ve person/personGroups referanslarının çalışması. Müşterideki **kullanıcılar ve gruplar** (Local + Directory/AD kaynaklı) lokal’de **Local** olarak ele alınır; birebir veri için Mongo `__dataId` korunur.

## Kararlar

| Konu | Karar |
|------|--------|
| Tenant domain | `odak` — [DOMAIN.md](./DOMAIN.md) |
| LDAP / AD federation | **Kurulmaz** |
| Kullanıcı tipi | Hepsi `Local` (Directory değil) |
| Grup tipi | Hepsi `Local` (Directory değil) — **aynı kural** |
| Veri / ID | Birebir lazım → dump + normalize; sıfırdan Create* yok — [DATABASE.md](./DATABASE.md) |

## Bileşenler

| Bileşen | Rol |
|---------|-----|
| Keycloak | Realm, client, **lokal** user (LDAP yok); gruplar çoğunlukla Keeper tarafında |
| MngKeeper | Domain `odak`, `@users` / `@groups`, üyelikler |
| Gateway | JWT doğrulama |

---

## Adım 1 — Domain (önkoşul)

→ [DOMAIN.md](./DOMAIN.md): mevcut domainleri temizle, `odak` oluştur.

---

## Adım 2 — Kullanıcılar **ve gruplar** (kimlik katmanı)

### Neden (user + group simetrisi)

Müşteride hem kullanıcılar hem gruplar:

| Kaynak | `provisioningSource` | Lokal’de |
|--------|----------------------|----------|
| Keeper’da elle açılmış | `Local` | Olduğu gibi Local kalabilir |
| AD / Keycloak federation + directory sync | `Directory` | Local’e çevrilmeli (LDAP yok) |

Grup entity’sinde de `ProvisioningSource` / `DirectorySyncedAt` var (user ile aynı enum).

DG tarafı:

| Alan | Saklanan ID |
|------|-------------|
| `persons` | Keeper user id = `@users.__dataId` |
| `personGroups` | Keeper group id = `@groups.__dataId` |

Sıfırdan CreateUser / CreateGroup → **yeni id** → hem person hem personGroups (ve user↔group üyelikleri) kırılır.

### Birebir yol (tercih — veri dump ile)

```text
1. mongodump mng_odak (users + groups + iş verisi; gitignore path)
2. Lokal restore
3. Normalize (zorunlu):
   - users:  provisioningSource → Local; directory alanları temizle
   - groups: provisioningSource → Local; directory alanları temizle
   - Keycloak’ta her user için native hesap; Mongo KeycloakUserId güncelle
   - Grup üyelikleri Mongo’da zaten __dataId ile duruyorsa dokunma (id korunur)
4. Login / expand smoke (persons + personGroups)
```

Bu modelde Adım 2 = “yeniden Create*” değil **normalize + Keycloak bağlama**.

### Alternatif (birebir değil)

Template’den `users` **ve** `groups` hariç + sonra CreateUser/CreateGroup → yeni id’ler; person/personGroups remap gerekir. Birebir hedefte kullanılmaz.

### Dikkat

| Risk | Not |
|------|-----|
| Yalnız user normalize, group unutmak | `personGroups` expand boş; Directory grup kısıtları |
| Üyelik (user.groupIds) | User/group `__dataId` korunursa üyelik dizileri geçerli kalır |
| LDAP sync job | Lokal’de kapalı |
| Ortak varsayılan şifre | Yalnız users; groups’ta parola yok |

### API referansı

- User: `POST /api/user` — [TECHNICAL_SPECS](../../../content/MngKeeper/main/TECHNICAL_SPECS.md)
- Group: Keeper group API’leri (oluşturma / liste)
- Enum: `UserProvisioningSource.Local = 0`, `Directory = 1` (group’ta da aynı)

---

## Önerilen genel sıra (birebir)

1. Keycloak ayakta (LDAP **yok**)
2. Domain temizliği + `odak` iskeleti (veya dump sonrası meta uyumu)
3. Mongo dump restore (`mng_odak`)
4. User + group normalize → Local; Keycloak user bağları
5. Smoke: login, `persons` / `personGroups` expand

## Secret’lar

| Ne | Nerede tutulur |
|----|----------------|
| Keycloak admin / client secret | lokal `.env` (gitignore) |
| Kullanıcı varsayılan şifresi | local-credentials (gitignore) |
| Dump / export | ignore path; git’e yok |

Dokümana parola **yazılmaz**.

## Referans

- Compose Keycloak: `ApplicationResources/mng_apps/docker-compose.yml`
- Müşteri LDAP (lokal’de uygulanmaz): `docs/odak/ldap/`
- Person / personGroups ID: [DATABASE.md](./DATABASE.md)
