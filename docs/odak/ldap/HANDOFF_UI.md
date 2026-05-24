# El değişimi — Mng.Ui (LDAP / Odak)

**Son güncelleme:** 24 Mayıs 2026

---

## Tamamlanan

| Kod | Özet |
|-----|------|
| **K1.6** | Gateway `5040`, AD login, manager JWT |
| **Welcome** | `/` — [../ui/WELCOME_HOME.md](../ui/WELCOME_HOME.md) |
| **K5d** | Kullanıcı rozeti, `fieldPolicies`, edit/profil guard |
| **G1** | Grup rozeti, `capabilities`, domain **Dizin senkronu** kartı |

Keeper sunucu: **v1.3.4** @ `192.168.20.20`. Yerel UI ile doğrulandı.

---

## Domain dizin sync (UI)

| Bileşen | Dosya |
|---------|--------|
| Kart | `Mng.Ui/components/apps/domain/DomainDirectorySyncCard.vue` |
| Sayfa | `Mng.Ui/pages/apps/domain/index.vue` |
| Proxy | `Mng.Ui/server/api/keeper/[...path].ts` → `POST /api/directory/sync` |

**İstek gövdesi (doğru format):**

```json
{
  "domainId": "<opsiyonel; JWT domain yeterli>",
  "triggeredBy": 0
}
```

`triggeredBy` **string değil** (`"Manual"` → 400). Enum: `0` Manual, `1` Scheduled, `2` Login.

---

## İlgili UI dosyaları

| Alan | Dosya |
|------|--------|
| Kullanıcı | `stores/apps/user.ts`, `utils/userFieldPolicy.ts`, `pages/apps/users/*` |
| Grup | `stores/apps/group.ts`, `utils/groupFieldPolicy.ts`, `pages/apps/groups/*` |
| Domain sync | `components/apps/domain/DomainDirectorySyncCard.vue` |

---

## Kalan (opsiyonel)

| İş | Not |
|----|-----|
| **mngui sunucu deploy** | `sync-odak-source.ps1 -Paths Mng.Ui` + `deploy-odak-apps.ps1 -Services mngui` |
| **K5e checklist** | [USER_SOURCES.md](./USER_SOURCES.md) §8 |
| **users/details** rozeti | Liste/edit yeterli; detay sayfası iyileştirme |
| HTTPS / Nginx | POC dışı |

---

## Doküman

[DEVAM.md](./DEVAM.md) · [USER_SOURCES.md](./USER_SOURCES.md) · [GROUP_SOURCES.md](./GROUP_SOURCES.md)
