# DEVAM — Mail Notifications (Kaldığımız yer)

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ **Faz 1 Odak doğrulandı** — MO + Notifier deploy; geçiş smoke `OCD-0090`

---

## 1. Tek cümlede durum

**Uçtan uca template mail yolu Odak'ta canlı:** MO geçiş → policy → `send-template` → Notifier SMTP. Faz 0–1 backend tamam; Faz 2 policy matris UI sırada.

---

## 2. Ürün hedefi (kararlandı — 7 Haziran 2026)

| Konu | Karar |
|------|--------|
| Template UI | DG `@mail_templates` + HTML body + placeholder |
| Workspace mail ayarları | **`op_notification_policies` genişletmesi** + matris UI |
| Tetikleme | MO geçiş anında → `send-template` HTTP push |
| Render | **MngNotifier** (subject, body fragment, layout) |
| Alıcılar | MngKeeper kullanıcıları; person/persons alanları |
| E-posta yoksa | **Atla** — yalnızca tanımlı adreslere gönder |
| Subject | Policy `emailSubject` override (placeholder'lı) veya template |
| Body | **Fragment + layout** (`@mail_layouts`) |
| Logo | MO `context.domain.logoUrl` (Keeper domain tanımı) |
| İlk canlı senaryo | `WorkItemTransitioned` + `work-item-transitioned` |
| Notifier event dinleme | **Yok** (push-only) |

Detay: [MO_MAIL_POLICIES.md](./MO_MAIL_POLICIES.md) · [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md)

---

## 3. Mimari özet

```text
UI (template CRUD + policy matris)
        │
MO (geçiş → policy → alıcı resolve → context)
        │  POST /send-template { templateKey, subject?, context, to }
        ▼
MngNotifier (DG template oku → render → SMTP)
```

---

## 4. Uygulama fazları

### Faz 0 — Notifier temeli ✅

- [x] MailKit SMTP (`SecureSocketMode`: port 465 → SslOnConnect)
- [x] `setup-notifier-datasets.ps1`
- [x] `POST /api/v1/notifications/send-template`
- [x] `POST /api/v1/notifications/preview-template`
- [x] `TemplateRenderService` (placeholder, layout, logo skip)
- [x] DG client (`@mail_templates`, `@mail_layouts`)
- [x] Uçtan uca test (7 Haz 2026 — aşağıda)
- [x] Odak SMTP kalıcı config (`configure-odak-notifier-smtp.ps1` + `.env`; şifre repoda yok)

**Token (Odak):** `docs/odak/operationcore/scripts/get-operationcore-token.ps1` → `$env:TEMP\operationcore_dg_token.txt`

### Faz 0 doğrulama (7 Haziran 2026)

| Adım | Sonuç |
|------|-------|
| `get-operationcore-token.ps1` | ✅ |
| `setup-notifier-datasets.ps1` | ✅ kategori + 2 dataset + seed |
| `preview-template` (`work-item-transitioned`) | ✅ subject + 2079 char HTML |
| `send-template` → Odak SMTP | ✅ `serkan.meral@outlook.com` |

Local Notifier env: `MngNotifierSettings__DataGateway__BaseUrl=http://192.168.20.20:5040/data`

### Faz 1 — MO entegrasyonu ✅

- [x] `PersonDisplayDto.Email` + Keeper map
- [x] `NotificationRecipientResolver` genişletmesi (`field:…`, `GetPersonRefId`)
- [x] Policy `transitionKey` / `fromStateId` / `toStateId` eşleştirme + `emailSubject`
- [x] `MailNotificationContextBuilder` + orchestrator → `send-template` (Bearer forward)
- [x] `IKeeperDirectoryClient.GetDomainByNameAsync` (logoUrl)
- [x] `op_notification_policies` DG şema patch (Odak — 4 alan)
- [x] Odak deploy: `mngoperations` + `mngnotifier` (`--no-cache`)
- [x] Policy seed: `OC Demo Mail - WorkItem Transitioned`
- [x] Smoke: `smoke-mail-transition.ps1` → assignee `serkan.meral@outlook.com` (`datasets/odak_mail_test_assignee.json`, personId `odak_admin`)

**Scriptler:** `docs/odak/notifications/scripts/` (`patch-*`, `seed-op-mail-*`, `smoke-mail-transition.ps1`)

### Faz 2 — Policy matris UI

- [ ] Workspace Mail Policies sekmesi
- [ ] Geçiş + alıcı + template + subject override

### Faz 3 — Template UI

- [ ] `@mail_templates` CRUD + HTML editör + önizleme

---

## 5. Doğrulama kayıtları

| Tarih | Test | Sonuç |
|-------|------|-------|
| 3 Haz 2026 | Gmail dev SMTP → Outlook | ✅ |
| 7 Haz 2026 | Odak SMTP `mail.kurumsaleposta.com:465` → `serkan.meral@outlook.com` | ✅ |
| 7 Haz 2026 | MO Faz 1 deploy + geçiş smoke (ilk deneme alıcı/SMTP sorunlu) | ⚠️ |
| 7 Haz 2026 | SMTP + policy fix → `OCD-FIX` + `OCD-0091` mail gönderildi | ✅ |
| 7 Haz 2026 | LDAP assignee notu + smoke `OCD-0092` → `serkan.meral@outlook.com` | ✅ |
| 7 Haz 2026 | Inbox doğrulama: `[OCD-0092] Durum degisti: OC Demo In Progress` | ✅ |

---

## 6. Açık sorular

| # | Konu | Durum |
|---|------|-------|
| N1 | Mail endpoint auth / rate limit | Ertelendi |
| N7 | Çoklu dil template | Ertelendi |
| N10 | Delivery audit / Mongo | Ertelendi |
| N11 | Odak production SMTP | ✅ Doğrulandı (7 Haz) |

---

## 7. Karar kaydı

| Tarih | Karar |
|-------|--------|
| 3 Haz 2026 | Push-only HTTP; Notifier event dinlemez |
| 3 Haz 2026 | Render Notifier'da; içerik DG'de |
| 7 Haz 2026 | Policy matrisi `op_notification_policies` genişletmesi |
| 7 Haz 2026 | Person = Keeper user; email yoksa atla |
| 7 Haz 2026 | Subject override policy'de |
| 7 Haz 2026 | Body fragment + layout |
| 7 Haz 2026 | İlk senaryo: WorkItemTransitioned + work-item-transitioned |
| 7 Haz 2026 | Odak SMTP doğrulandı |

---

**Devam:** Faz 2 — Workspace Mail Policies matris UI. Faz 0–1 uçtan uca doğrulandı (7 Haz, `OCD-0092` inbox ✅).
