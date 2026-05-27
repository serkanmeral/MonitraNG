# El değişimi — Mng.Ui (LDAP / Odak) ✅ tamamlandı

**Son güncelleme:** 25 Mayıs 2026  
**Durum:** LDAP POC UI işleri **bitti** — bu dosya **arşiv / referans**. Yeni geliştirme LDAP dışı: [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md).

---

## Tamamlanan

| Kod | Özet |
|-----|------|
| **K1.6** | Gateway `5040`, AD login, manager JWT |
| **Welcome** | `/` — [../ui/WELCOME_HOME.md](../ui/WELCOME_HOME.md) |
| **K5d** | Kullanıcı rozeti, `fieldPolicies`, edit/profil guard |
| **G1** | Grup rozeti, `capabilities`, domain **Dizin senkronu** kartı |
| **mngui deploy** | Sunucu `http://192.168.20.20:3000` — `deploy-odak-apps.ps1 -Services mngui` |

Keeper sunucu: **v1.3.4** @ `192.168.20.20`. Yerel UI ve sunucu mngui ile doğrulandı.

**Git:** `main` @ `72872d9` (Keeper + UI + Scheduler + Odak doküman/script).

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

## Opsiyonel (LDAP dönüşünde)

| İş | Not |
|----|-----|
| **K5e checklist** | [USER_SOURCES.md](./USER_SOURCES.md) §8 |
| **users/details** rozeti | Liste/edit yeterli; detay sayfası iyileştirme |
| HTTPS / Nginx | POC dışı — [ODAK_HTTP_AND_GATEWAY.md](./ODAK_HTTP_AND_GATEWAY.md) |

---

## Doküman

[DEVAM.md](./DEVAM.md) · [USER_SOURCES.md](./USER_SOURCES.md) · [GROUP_SOURCES.md](./GROUP_SOURCES.md)
