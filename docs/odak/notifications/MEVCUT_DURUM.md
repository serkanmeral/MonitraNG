# Bildirimler — Mevcut durum (kod gerçeği)

**Son güncelleme:** 3 Haziran 2026  
**Doğrulama:** MngNotifier local test (Gmail SMTP → Outlook) ✅ (2 kez, 3 Haziran 2026)

**Planlama:** `@mail_templates` / `@mail_layouts` sema + seed → [datasets/](./datasets/) · Duraklatma → [DEVAM.md](./DEVAM.md)

---

## 1. MngNotifier (port 5070)

### Çalışan

| Özellik | Durum | Not |
|---------|-------|-----|
| `POST /api/v1/notifications/mail` | ✅ | Senkron SMTP; `AllowAnonymous` |
| `SmtpMailProvider` | ✅ | `to`, `cc`, `from` override, HTML |
| Health / Version | ✅ | RabbitMQ check placeholder |
| Gateway route | ✅ | `/notifier/api/v1/*` → `:5070` |
| `EmailTemplateService` | ⚠️ Kod var | Yerel `Templates/Email/*.html`; **controller’a bağlı değil** |
| `domain-created.html` | ⚠️ Dosya var | MngKeeper step inline HTML kullanıyor |

### Henüz yok / plan

| Özellik | Durum | Referans |
|---------|-------|----------|
| RabbitMQ consumer (`mngnotifier.mail.send`) | ❌ | ROADMAP Phase 5, MAIL_NOTIFICATION_DESIGN |
| MongoDB notification kaydı | ❌ | GUID geçici ID |
| `POST /notifications/send-template` | ❌ | DG `@mail_templates` planı |
| Rate limiting / auth (mail endpoint) | ❌ | Tasarımda önerilmiş |
| SMS / WhatsApp / Slack / Telegram | ❌ | ROADMAP Phase 7; kararlar → [MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md) |

### Config özeti

| Ortam | SMTP | From |
|-------|------|------|
| Production (`appsettings.json`) | `127.0.0.1:25`, auth yok | `noreply@monitrang.com` |
| Development | `smtp.gmail.com:587`, SSL | `sermeral@gmail.com` (test) |
| Docker compose | Gmail env override | Aynı |

---

## 2. MngOperations (Operation Core bildirim orchestration)

### Çalışan

| Bileşen | Durum |
|---------|-------|
| `IMngNotifiersClient` → `POST notifications/mail` | ✅ |
| `INotificationOrchestratorService` | ✅ |
| Rule: `createNotification` → `op_notifications` | ✅ |
| Rule: `sendEmailViaMngNotifiers` | ✅ |
| `MngNotifiers.Enabled` / health probe | ✅ |
| OC UI: header badge, `/apps/operation-core/notifications` | ✅ (NP-7) |

### Veri modeli (DG dataset’leri)

| Dataset | Rol |
|---------|-----|
| `op_notifications` | Kullanıcı bazlı in-app bildirim |
| `op_notification_policies` | Kanal / template / alıcı politikası |
| `op_rules` | Geçiş ve side-effect kuralları |

**Karar:** `op_*` dataset’lerinde `publish_mode: none` — operasyonel yol MO + `oc.events` ([NOTIFICATIONS_AND_EVENTS.md](../operationcore/mngoperations/NOTIFICATIONS_AND_EVENTS.md)).

---

## 3. MngKeeper

| Senaryo | Durum |
|---------|-------|
| Domain oluşturma → `SendDomainCreatedEmailStep` | ✅ |
| `INotifierService` → MngNotifier HTTP | ✅ |
| Template (`domain-created.html`) | ❌ Kullanılmıyor |

---

## 4. MngDataGateway → MngNotifier (chat)

| Endpoint | Durum |
|----------|-------|
| `POST /api/v1/notifications/chat-mention` | ⚠️ MVP |
| Davranış | Yapılandırılmış **log**; e-posta / push yok |
| Auth | `X-Monitra-Notify-Key` (opsiyonel, `InternalNotifyApiKey`) |

---

## 5. MngHub / gerçek zamanlı (e-posta değil)

| Akış | Kanal | Bildirim türü |
|------|-------|---------------|
| Chat mesajı | DG publish → Hub → SignalR | In-app canlı |
| Monitoring ingest | Reactor → `monitoring.data.updated.{domain}` → Hub | UI yenileme throttle |
| OC operasyonel | MO → `oc.events` | Entegrasyon tüketicileri |

Bunlar MngNotifier ile **karıştırılmamalı**; ayrı kanal.

---

## 6. Bilinen tutarsızlıklar (doküman vs kod)

| Konu | Eski doküman | Gerçek kod |
|------|--------------|------------|
| Mail endpoint adı | `/notifications/send` (MAIL_NOTIFICATION_DESIGN) | `/notifications/mail` |
| Architecture guide endpoint | `/notification/email` | `/notifications/mail` |
| Response status | `"queued"` (tasarım) | `"sent"` (senkron gönderim) |
| ROADMAP “Temel tamamlandı” | RabbitMQ/MongoDB bekliyor olarak da yazıyor | Tutarlı — sadece direct mail canlı |

Bu klasördeki yeni planlama bu tutarsızlıkları gidererek **tek referans** olmayı hedefler.

---

## 7. Test kanıtı (3 Haziran 2026)

```http
POST http://localhost:5070/api/v1/notifications/mail
```

- Gönderen: `sermeral@gmail.com` (Development SMTP)
- Alıcı: `serkan.meral@outlook.com`
- Sonuç: HTTP 200, `status: "sent"` — **iki ayri testte mail alındı** (ilk ve tekrar test ayni gun)
