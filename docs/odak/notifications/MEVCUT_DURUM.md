# Bildirimler — Mevcut durum (kod gerçeği)

**Son güncelleme:** 7 Haziran 2026  
**Planlama:** [DEVAM.md](./DEVAM.md) · Policy: [MO_MAIL_POLICIES.md](./MO_MAIL_POLICIES.md)

---

## 1. MngNotifier (port 5070)

### Çalışan

| Özellik | Durum | Not |
|---------|-------|-----|
| `POST /api/v1/notifications/mail` | ✅ | Senkron SMTP; `AllowAnonymous` |
| `SmtpMailProvider` | ✅ | `to`, `cc`, `from`, HTML |
| Health / Version | ✅ | |
| Gateway route | ✅ | `/notifier/api/v1/*` → `:5070` |
| `POST /notifications/chat-mention` | ⚠️ MVP | Log-only |

### Faz 0 (tamamlandı — 7 Haziran 2026)

| Özellik | Durum |
|---------|-------|
| MailKit SMTP (port 465 SslOnConnect) | ✅ Odak SMTP ile doğrulandı |
| `POST /notifications/send-template` | ✅ |
| `POST /notifications/preview-template` | ✅ |
| DG `@mail_templates` / `@mail_layouts` | ✅ Odak DG'de kurulu |
| `setup-notifier-datasets.ps1` | ✅ OC token ile çalışıyor |
| Uçtan uca | ✅ `work-item-transitioned` → Outlook |

### Henüz yok

| Özellik | Not |
|---------|-----|
| RabbitMQ consumer | Kapsam dışı (push-only) |
| MongoDB delivery audit | Ertelendi |
| Endpoint auth (mail) | Ertelendi |

### SMTP doğrulama

| Ortam | Sunucu | Durum |
|-------|--------|-------|
| Development | Gmail `:587` | ✅ (3 Haz) |
| Odak müşteri | `mail.kurumsaleposta.com:465` | ✅ (7 Haz) |
| Production config | Henüz Notifier'a bağlanmadı | Env ile yapılacak |

---

## 2. MngOperations

| Bileşen | Durum | Not |
|---------|-------|-----|
| `IMngNotifiersClient` → `/mail` | ✅ | `emailTemplateKey` yoksa legacy fallback |
| `IMngNotifiersClient` → `send-template` | ✅ | Bearer forward; policy `emailTemplateKey` doluysa |
| `MailNotificationContextBuilder` | ✅ | actor, workItem, transition, domain, event |
| `op_notification_policies` model | ✅ | `transitionKey`, `fromStateId`, `toStateId`, `emailSubject` |
| Geçiş filtresi policy'de | ✅ | `PolicyMatches` / `PolicyScore` |
| `field:` alıcı çözümü | ✅ | `GetPersonRefId` / `GetPersonRefIdList` |
| `PersonDisplayDto.Email` | ✅ | Keeper `GetUsersAsync` → email yoksa atla |
| DG şema patch (Odak) | ⏳ | Policy kayıtları + UI henüz yok |

---

## 3. DG mail dataset'leri

| Dataset | Şema | Seed | Kurulum |
|---------|------|------|---------|
| `@mail_templates` | ✅ | ✅ | Script 🔄 |
| `@mail_layouts` | ✅ | ✅ | Script 🔄 |

---

## 4. Diğer tüketiciler

| Servis | Durum |
|--------|-------|
| MngKeeper domain mail | ✅ inline HTML → `/mail` |
| MngDataGateway chat-mention | ⚠️ log-only |
| MngHub / SignalR | Ayrı kanal (e-posta değil) |

---

## 5. Bilinen tutarsızlıklar

| Konu | Eski doküman | Gerçek |
|------|--------------|--------|
| Endpoint | `/notifications/send` | `/notifications/mail` |
| Response | `"queued"` | `"sent"` |
| RabbitMQ consumer | ROADMAP'te | Kapsam dışı |

Odak klasörü (`docs/odak/notifications/`) güncel referanstır.
