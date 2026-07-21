# Bildirimler (Notifications) — Odak planlama

**Son güncelleme:** 7 Haziran 2026  
**Durum:** 🔄 Faz 0 implementasyon — [DEVAM.md](./DEVAM.md). MO policy: [MO_MAIL_POLICIES.md](./MO_MAIL_POLICIES.md)

---

## Amaç

MonitraNG **MngNotifier e-posta sablonlari** icin Odak planlama cercevesi (DG dataset + render + SMTP).

**Kapsam disi (ayri calisma):** MngOperations policy, orchestrator, workspace UI; chat/Hub.

---

## Bu klasördeki belgeler

| Belge | İçerik |
|-------|--------|
| **[MEVCUT_DURUM.md](./MEVCUT_DURUM.md)** | Kod ve config gerçeği — ne çalışıyor, ne eksik |
| **[DEVAM.md](./DEVAM.md)** | Planlama oturumu — açık sorular, faz önerisi, karar kaydı |
| **[MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md)** | **Kararlandı:** push-only HTTP; Notifier event dinlemez; sadece mail |
| **[MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md)** | **Kararlandı:** WhatsApp / Slack / Telegram — push-only; e-posta sonrasi faz |
| **[TELEGRAM.md](./TELEGRAM.md)** | **Faz 3 / Odak:** teklif + mimari; kanal kararları |
| **[TELEGRAM_USER_BINDING.md](./TELEGRAM_USER_BINDING.md)** | **Yeniden yazıldı:** @username + chat_id; profil alanları; bağlama UX |
| **[MAIL_TEMPLATES.md](./MAIL_TEMPLATES.md)** | Template render Notifier'da; DG dataset ozeti |
| **[MO_MAIL_POLICIES.md](./MO_MAIL_POLICIES.md)** | Workspace mail matrisi; `op_notification_policies` genişletmesi |
| **[IN_APP_TOAST_PLAN.md](./IN_APP_TOAST_PLAN.md)** | Inbox + Hub user push + global toaster (MO + Alarm) |
| **[../alarm/ALARM_NOTIFICATION_POLICIES.md](../alarm/ALARM_NOTIFICATION_POLICIES.md)** | Alarm bildirim politikaları (çoklu kullanıcı seçimi) |
| **[datasets/](./datasets/)** | `@mail_templates` + `@mail_layouts` sema ve seed ornekleri |
| **[scripts/setup-notifier-datasets.ps1](./scripts/setup-notifier-datasets.ps1)** | DG kategori + dataset + seed kurulumu |

---

## Mevcut doküman haritası (okunması önerilen)

### MngNotifier (merkezi e-posta servisi)

| Doküman | Konum | Not |
|---------|-------|-----|
| Technical Specs (API) | [docs/content/MngNotifier/main/TECHNICAL_SPECS.md](../../content/MngNotifier/main/TECHNICAL_SPECS.md) | `POST /notifications/mail` referansı |
| Roadmap | [docs/content/MngNotifier/main/ROADMAP.md](../../content/MngNotifier/main/ROADMAP.md) | Phase 5 kısmen; template/RabbitMQ bekliyor |
| Mail tasarım (hedef mimari) | [docs/content/MngNotifier/support/guides/MAIL_NOTIFICATION_DESIGN.md](../../content/MngNotifier/support/guides/MAIL_NOTIFICATION_DESIGN.md) | Direct API + RabbitMQ + template — çoğu henüz kodda yok |
| Configuration | [docs/content/MngNotifier/support/guides/CONFIGURATION.md](../../content/MngNotifier/support/guides/CONFIGURATION.md) | SMTP, DefaultFrom, env |
| Dev vs Prod SMTP | [docs/content/MngNotifier/support/guides/DEVELOPMENT_VS_PRODUCTION.md](../../content/MngNotifier/support/guides/DEVELOPMENT_VS_PRODUCTION.md) | Gmail / Mailu ayrımı |
| Architecture | [docs/content/MngNotifier/support/architecture/ARCHITECTURE_GUIDE.md](../../content/MngNotifier/support/architecture/ARCHITECTURE_GUIDE.md) | Genel mimari (bazı endpoint adları güncel değil) |

### Operation Core / MngOperations (in-app + e-posta orchestration)

| Doküman | Konum | Not |
|---------|-------|-----|
| DG publish_mode vs MO bildirim | [docs/odak/operationcore/mngoperations/NOTIFICATIONS_AND_EVENTS.md](../operationcore/mngoperations/NOTIFICATIONS_AND_EVENTS.md) | **Kararlandı:** `op_*` → `publish_mode: none` + `oc.events` |
| Entegrasyonlar (MngNotifiers) | [docs/odak/operationcore/mngoperations/INTEGRATIONS.md](../operationcore/mngoperations/INTEGRATIONS.md) | `IMngNotifiersClient`, orchestrator |
| Rule engine | [docs/odak/operationcore/mngoperations/RULE_ENGINE.md](../operationcore/mngoperations/RULE_ENGINE.md) | `createNotification`, `sendEmailViaMngNotifiers` |
| Phase 1 spec §15–16 | [docs/odak/operationcore/operationcore_phase1.md](../operationcore/operationcore_phase1.md) | `op_notifications`, `op_notification_policies` |

### Diğer tüketiciler

| Doküman | Konum | Not |
|---------|-------|-----|
| Domain oluşturma e-postası | [docs/content/MngKeeper/support/guides/DOMAIN_CREATION_EMAIL_NOTIFICATION.md](../../content/MngKeeper/support/guides/DOMAIN_CREATION_EMAIL_NOTIFICATION.md) | MngKeeper → MngNotifier; template henüz kullanılmıyor |
| Chat mention (MVP) | [docs/content/chat_room/CHAT_ROOM_ROADMAP.md](../../content/chat_room/CHAT_ROOM_ROADMAP.md) §6, §8.2 | DG → `chat-mention`; şu an log-only |
| Monitoring UI throttle | [docs/content/monitoring_plans/MADDE_2_INGEST_NOTIFY_PLAN.md](../../content/monitoring_plans/MADDE_2_INGEST_NOTIFY_PLAN.md) | Reactor → Hub — **e-posta değil**, SignalR |

---

## Kavram ayrımı (özet)

```text
┌─────────────────────────────────────────────────────────────────┐
│ Kullanıcı bildirimi (ürün anlamı)                                │
│  • op_notifications (in-app)                                     │
│  • MngNotifier (e-posta; ileride WhatsApp/Slack/Telegram)        │
│  • MngHub (anlık push — chat, monitoring)                        │
└─────────────────────────────────────────────────────────────────┘
                              ≠
┌─────────────────────────────────────────────────────────────────┐
│ Entegrasyon / veri katmanı event’i                               │
│  • DG publish_mode (ham CRUD → RabbitMQ)                         │
│  • oc.events (anlamlı operasyonel olay — MO)                     │
└─────────────────────────────────────────────────────────────────┘
```

Detay: [NOTIFICATIONS_AND_EVENTS.md](../operationcore/mngoperations/NOTIFICATIONS_AND_EVENTS.md).

---

## Sonraki adim

**Faz 0** devam ediyor: Notifier `send-template` + dataset script. Sonra MO entegrasyonu (Faz 1). Bkz. **[DEVAM.md](./DEVAM.md)**.
