# Grup kaynakları — Yerel vs Kurumsal (Directory)

**Son güncelleme:** 24 Mayıs 2026 · **Durum:** ✅ Odak’ta doğrulandı (Keeper 1.3.4)  
**İlişki:** [USER_SOURCES.md](./USER_SOURCES.md), [DEVAM.md](./DEVAM.md)

Kullanıcılarla aynı model: **Local** (uygulama) / **Directory** (AD → Keycloak → K2/K3/K4 sync).

## Kurallar

| İşlem | Yerel grup | Kurumsal grup |
|--------|------------|----------------|
| Oluştur (`POST /api/group`) | ✅ | — (sync ile gelir) |
| Düzenle / sil | ✅ | ❌ `DIRECTORY_GROUP_NOT_MUTABLE` |
| Üye ekle / çıkar | ✅ | ❌ `DIRECTORY_GROUP_MEMBERSHIP_NOT_MUTABLE` |
| Yerel kullanıcıya kurumsal grup atama | — | ❌ (CreateUser / UpdateUser `groupIds`) |

**Varsayılan gruplar** (`admins`, `managers`, `users`, `guests`): her zaman **Local** — sync yalnızca eksik `keycloakGroupId` bağlar.

**Diğer KC grupları** (AD/LDAP federation): ilk sync veya sonraki tam sync ile **Directory** işaretlenir (eskiden Local kayıtlı olsa bile).

**Önemli:** G1 deploy sonrası bir kez `POST /api/directory/sync` veya Domain sayfasındaki buton çalıştırılmalı; aksi halde liste hep Yerel görünür.

## API

- GET `/api/group`, GET `/api/group/{id}` → `provisioningSource`, `capabilities` (`canEdit`, `canDelete`, `canManageMembers`)
- POST `/api/directory/sync` — manuel tam sync (kullanıcı + grup KC→Mongo)

**Sync isteği:**

```json
POST /api/directory/sync
{ "triggeredBy": 0 }
```

| `triggeredBy` | Anlam |
|---------------|--------|
| `0` | Manual (UI butonu, Scalar) |
| `1` | Scheduled (MngScheduler) |
| `2` | Login (dahili) |

## UI

- Grup listesi / düzenle / detay — rozet + `capabilities`
- Domain yönetimi — **Dizin senkronunu çalıştır** (`DomainDirectorySyncCard.vue`)

## Deploy / migrasyon

| Bileşen | Durum |
|---------|--------|
| Keeper **1.3.4** | Sunucu deploy ✅ — KC grupları sync ile Directory’ye yükseltilir (`DirectoryGroupPolicy`) |
| Mng.Ui G1 + sync kartı | Yerel dev ✅; sunucu `mngui` deploy isteğe bağlı |

İlk kullanımda veya G1 sonrası **bir kez** tam dizin sync çalıştırın; aksi halde tüm gruplar Yerel görünebilir.
