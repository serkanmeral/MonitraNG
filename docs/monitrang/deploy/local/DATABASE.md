# Veritabanı stratejisi (lokal) — Adım 3

## Amaç

`odak` domain’i ve lokal kullanıcılar (Adım 1–2) sonrası **iş / şema verisini** lokal’e almak.

İki ana yol:

| | A — DomainUI varsayılan veri (template) | B — MongoDB dump (`mng_odak`) |
|--|----------------------------------------|-------------------------------|
| Ne | Seçilen koleksiyonların anlık görüntüsü → MinIO/template; domain oluştururken `initialDataTemplateName` | `mongodump` / `mongorestore` |
| Seçicilik | Koleksiyon bazlı (users hariç bırakılabilir) | Genelde tüm DB |
| Kullanıcı kayıtları | Bilinçli hariç → Adım 2 ile uyumlu | Users dahil gelir → Directory bayrakları da gelir |
| `userId` sürekliliği | Yeni kullanıcı ID’leri (Adım 2) | Eski Mongo `_id` korunur |
| Keycloak | Template kullanıcı getirmez | Dump Keycloak’u getirmez; `KeycloakUserId` stale kalır |
| Boyut / taşıma | Daha hafif, taşınabilir | Ağır; ignore path |

**Seçilen:** **B — Mongo dump** (`mng_odak`), ama **`@users` / `@groups` hariç**  
**Tarih:** 2026-07-11 (Adım 2 JSON import sonrası güncellendi)

### Neden users/groups hariç?

Lokal’de CreateUser/CreateGroup ile kimlik kuruldu (yeni `__dataId`, Keycloak Local, `Sm123!?`).  
Tam dump restore bunları ezer → login ve Local bayraklar bozulur.

| Koleksiyon | Dump? | Not |
|------------|-------|-----|
| Dataset / iş verisi / DI meta (Mongo kısmı) | Evet | |
| `@users`, `@groups` | **Hayır** | Lokal Keeper kalsın |
| MinIO dosyaları | Hayır | Adım 4 API pack |

Person/`personGroups` alanları dump’ta **eski Odak id** taşır → expand için sonra **username/group name → yeni id remap** gerekir (export JSON + lokal liste).

Uzak dump prompt: [remote_prompts/RP02_mongo_dump_mng_odak_test.md](./remote_prompts/RP02_mongo_dump_mng_odak_test.md)

---

## DG `persons` / `personGroups` — hangi ID?

| Alan tipi | Mongo’da saklanan değer | Expand lookup |
|-----------|-------------------------|---------------|
| `persons` | **MngKeeper User ID** (string) = tenant DB `@users.__dataId` | `$lookup` `@users` ← `foreignField: __dataId` |
| `personGroups` | **MngKeeper Group ID** = `@groups.__dataId` | `$lookup` `@groups` |

Örnek (persons single): `"author": "690cdb7fae502df7d3330bbb"`.

- Canonical depo **Keeper id**; Keycloak `sub` değil (bazı akışlar alias çözebilir).
- User **ve group** aynı kural: sıfırdan Create* → yeni `__dataId` → ilgili DG alanları + üyelikler kırılır.
- Gruplarda da `provisioningSource` (`Local` / `Directory`) var; dump sonrası **groups da Local’e normalize** edilmeli.
- Birebir için: dump’ta `@users` + `@groups` id’leri korunur → normalize + Keycloak user bağlama — [USERS_AND_AUTH.md](./USERS_AND_AUTH.md)

Dokümantasyon: `persons.md` / `personGroups.md` (Mng.Ui chatbot field-types)

---

## Kullanıcı **ve grup** / Directory sorunu

Keeper’da hem user hem group:

- `provisioningSource`: `Local` (0) veya `Directory` (1)
- Directory sync / AD kaynaklı kayıtlar lokal’de LDAP olmadan bozulur

| Senaryo | Sonuç |
|---------|--------|
| Dump (users + groups dahil) | Directory bayrakları aynen gelir |
| Sadece user normalize, group unutmak | `personGroups` / yetki grupları sorunlu kalır |
| Sadece `provisioningSource` flip (user) | Keycloak native user + `KeycloakUserId` de şart |
| Group | Parola yok; Local flag + id koruma yeterli (Keycloak group sync müşteriye özelse lokal’de gerekmez) |

---

## Öneri

### Varsayılan tercih: **A — DomainUI template, `users` (ve gerekirse groups) hariç**

Adım 1–2 ile uyum:

```text
1. Müşteride DomainUI → Create Template
   - Dataset şemaları, menü, OC/DI seed, kataloglar, …
   - users (ve tercihen groups) SEÇİLMEZ
2. Template içeriğini lokal’e al (MinIO veya content export)
3. Lokal’de odak oluştururken bu template’i initial data olarak ver
4. Adım 2: tüm kullanıcıları Local CreateUser ile üret
```

**Artı:** Directory bayrağı hiç gelmez; Keycloak bağları temiz; Adım 2 basit kalır.  
**Eksi:** İş kayıtlarındaki person/`userId` referansları eski ID’lere işaret ederse boş/yanlış kalır (remap veya tolere).

Bu, “geliştirme ortamını ayağa kaldır / özellik geliştir” hedefi için genelde yeterli.

### Dump ne zaman tercih edilir?

Tam operasyonel klon istiyorsanız (aynı paketler, aynı person bağları, aynı ID’ler):

```text
1. mongodump mng_odak (gitignore path) — **müşteri terminal Cursor**’da; lokal’den doğrudan erişim yok → [REMOTE_CURSOR_WORKFLOW.md](./REMOTE_CURSOR_WORKFLOW.md)
2. Lokal restore
3. Normalize script (zorunlu):
   - provisioningSource → Local
   - directory alanlarını temizle
   - her user için Keycloak’ta lokal user oluştur
   - Mongo KeycloakUserId güncelle
4. Adım 2 = bu normalize; sıfırdan CreateUser değil
```

**Artı:** `userId` sürekliliği.  
**Eksi:** Script + boyut + secret/veri riski; Keycloak realm de uyumlu olmalı.

---

## Hibrit (isteğe bağlı)

| Katman | Yöntem |
|--------|--------|
| Şema / yapılandırma / az seed | DomainUI template (`users` hariç) |
| Kritik iş verisi + person ref | Seçili koleksiyon dump **veya** sonra seed script |
| Kimlik | Her zaman Adım 2 Local (veya dump normalize) |

---

## PostgreSQL (Keycloak)

- Dump ile gelmez; lokal Keycloak kendi volume’unda kalır.
- Realm/client’lar compose ile; kullanıcılar Adım 2 veya normalize ile native.

## MinIO / dosya

- Template meta/content MinIO’da (Keeper template servisi).
- PO/DI binary’leri ayrı karar (çoğu lokal MVP’de boş bucket yeterli).

## Güvenlik

- Dump / template content / user export **git’e girmez**.
- Dokümana yalnızca prosedür yazılır.

## Açık soru (karar için)

Lokal’de person/`userId` bağlı iş verisini birebir mi istiyorsunuz, yoksa şema + seed + yeni Local kullanıcılar yeterli mi?

- **Yeterli** → A (template, users hariç)  
- **Birebir** → B (dump + normalize) veya hibrit
