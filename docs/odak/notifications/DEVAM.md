# DEVAM — Mail Notifications (Kaldığımız yer)

**Son güncelleme:** 7 Haziran 2026  
**Durum:** ✅ **Mail Faz 0–2 + in-app toaster (T1–T3, T5) Odak canlı** — smoke `OCD-0102`, toaster doğrulandı

---

## 1. Tek cümlede durum

**MO bildirimleri uçtan uca:** e-posta + in-app inbox + Hub toaster + **zil deep link** (`NotificationDto.DeepLink` → `ocNotificationNavigation`). **Alarm bildirimleri (AN-1→AN-5) Odak canlı.** Manuel test: [CONTROL_CHECKLIST.md](../CONTROL_CHECKLIST.md) A+B. **RMQ-DIAG** ayrı oturum.

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

### Faz 2 — Policy matris UI (devam)

- [x] Workspace tanımları → **Bildirim politikaları** sekmesi (`tab=mail`)
- [x] `op_notification_policies` CRUD (DG) — geçiş, alıcı, kanal, template, subject
- [x] Odak `mngui` deploy (7 Haz) + `smoke-mail-policies-ui.ps1`
- [ ] Tarayıcı doğrulama (odak_admin — liste + modal)
- [x] Template anahtarı dropdown (`@mail_templates` listesi)

### Faz 3 — Template UI (devam)

- [x] `@mail_templates` CRUD sayfası (`/apps/operation-core/admin/mail-templates`)
- [x] HTML fragment editör + Notifier `preview-template` (kayıtlı şablon)
- [x] Bildirim politikası modalında şablon combobox
- [x] `mngui` nginx → `/api/notifier/` proxy
- [ ] UI deploy (kullanıcı onayı ile)
- [ ] Tarayıcı doğrulama

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
| 7 Haz 2026 | Faz 2 `mngui` deploy + `smoke-mail-policies-ui.ps1` | ✅ |

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
| 7 Haz 2026 | UI sekme adı: **Bildirim politikaları** (e-posta + in-app + gelecek kanallar) |
| 7 Haz 2026 | Person = Keeper user; email yoksa atla |
| 7 Haz 2026 | Subject override policy'de |
| 7 Haz 2026 | Body fragment + layout |
| 7 Haz 2026 | İlk senaryo: WorkItemTransitioned + work-item-transitioned |
| 7 Haz 2026 | Odak SMTP doğrulandı |

---

### In-app toaster (T1–T5) ✅

- [x] MngHub `user:{personId}` + `POST /internal/user-notify`
- [x] Mng.Ui `useAppToast` + hub plugin + `pushToast` policy UI
- [x] MO orchestrator + `InAppNotificationComposer` + `@notification_templates` seed
- [x] Odak deploy (`mnghub`, `mngoperations`, `mngui`) + `smoke-inapp-toast.ps1`
- [ ] Kalan smoke: iki kullanıcı izolasyonu, `pushToast: false`, hub kapalı poll

**Zil deep link (✅):** `mngoperations` API `deepLink` + `mngui` `NotificationDD` / `/apps/operation-core/notifications` navigasyon.

---

## 8. Sıradaki (yeni oturum)

1. [CONTROL_CHECKLIST.md](../CONTROL_CHECKLIST.md) — A6–A8 (toaster edge), B (alarm policy UI gözle), D (deep link tıklama).
2. MO toaster kalan smoke maddeleri (yukarıdaki checkbox'lar).
3. Alarm tarafı handoff: [../alarm/DEVAM.md](../alarm/DEVAM.md) §7.

Toast planı: [IN_APP_TOAST_PLAN.md](./IN_APP_TOAST_PLAN.md).
