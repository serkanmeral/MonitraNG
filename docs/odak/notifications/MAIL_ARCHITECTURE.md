# E-posta bildirimi — Mimari ilkeler

**Son güncelleme:** 7 Haziran 2026  
**Durum:** Kararlandı — Faz 0 Notifier implementasyon devam ediyor ([DEVAM.md](./DEVAM.md))

---

## 1. Kapsam

| Konu | Bu aşamada |
|------|------------|
| **MngNotifier** | **E-posta:** SMTP + **template render** + domain layout |
| SMS, push, Slack, WhatsApp, Telegram | ❌ Gelecek — [MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md) |
| In-app bildirim | **MngOperations** → DG `op_notifications` |
| Gerçek zamanlı (Hub) | **MngHub** / SignalR |

---

## 2. Temel ilke: event tüketicisi değil, HTTP hedefi

**MngNotifier hiçbir domain/operasyonel event dinlemez.**

- RabbitMQ consumer yok.
- WorkItem, domain vb. olayları **ilgili servis** kendi içinde işler.
- E-posta için çağıran servis **doğrudan** Notifier HTTP API’sini çağırır.

```text
┌──────────────────┐   olay (pipeline / komut)    ┌─────────────────────────────┐
│  MngOperations   │ ───────────────────────────► │ policy: event → templateKey │
│  MngKeeper       │                              │ alıcı resolve               │
└────────┬─────────┘                              │ context JSON üret           │
         │                                        └──────────────┬──────────────┘
         │  POST /api/v1/notifications/send-template             │
         │       { templateKey, context, to }                     │
         └──────────────────────────────────────────────────────►
                                    ┌──────────────────┐
                                    │   MngNotifier    │
                                    │   template render│
                                    │   layout + SMTP  │
                                    └──────────────────┘
```

**Push model:** Çağıran → Notifier. Notifier → RabbitMQ ❌

---

## 3. Sorumluluk ayrımı

| Sorumluluk | Servis |
|------------|--------|
| Olayı yakalamak | **MngOperations**, **MngKeeper**, … |
| Event → templateKey eşlemesi (workspace tanımları) | **MngOperations** — `op_notification_policies`, rules |
| Alıcı resolve, in-app kayıt | **MngOperations** |
| Template içeriği CRUD | **DG** `@mail_templates` (+ admin UI) |
| **Template render** (subject, body, layout, placeholder) | **MngNotifier** |
| SMTP | **MngNotifier** |
| `oc.events` RabbitMQ | **MngOperations** (mail değil) |

Detaylı template modeli: [MAIL_TEMPLATES.md](./MAIL_TEMPLATES.md)

---

## 4. Notifier API

### 4.1 Birincil yol — template render

```http
POST /api/v1/notifications/send-template
Authorization: Bearer <token>

{
  "to": ["user@example.com"],
  "templateKey": "work-item-transitioned",
  "subject": null,
  "context": {
    "workItem": { "key": "WI-1", "title": "..." },
    "transition": { "key": "resolve", "fromState": "Open", "toState": "Done" },
    "actor": { "displayName": "..." },
    "domain": { "displayName": "...", "logoUrl": "https://..." }
  }
}
```

**Subject önceliği:** policy `emailSubject` veya request `subject` → yoksa template `subject` (hepsi Notifier'da placeholder render).

MO/Keeper **templateKey + context** gönderir; HTML üretmez.

### 4.2 Legacy — ham mail

```http
POST /api/v1/notifications/mail
```

Bootstrap ve geçiş dönemi; yeni entegrasyonlar `send-template` kullanmalı.

---

## 5. Mevcut kod — yapılacaklar

| Bugün | Hedef |
|-------|--------|
| MO → `/mail` + `NotificationMessageBuilder` | MO → `/send-template` + `emailTemplateKey` + context |
| `emailTemplateKey` policy’de var, kullanılmıyor | Orchestrator templateKey ile Notifier’a delegasyon |
| Notifier sadece SMTP | Notifier + DG template okuma + render engine |
| Keeper inline HTML | Keeper → `templateKey: domain-created` |

---

## 6. Bilinçli olarak yapılmayanlar

- MngNotifier RabbitMQ consumer
- Notifier’ın operasyonel event dinlemesi
- MO’da template HTML render

---

## 7. Karar kaydı

| Tarih | Karar |
|-------|--------|
| 3 Haz 2026 | Push-only HTTP; Notifier event dinlemez |
| 3 Haz 2026 | Notifier kapsamı: sadece e-posta (bu faz) |
| 3 Haz 2026 | **Template render Notifier’da**; MO event→templateKey + context |

İlgili: [MAIL_TEMPLATES.md](./MAIL_TEMPLATES.md) · [DEVAM.md](./DEVAM.md)
