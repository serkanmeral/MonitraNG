# Telegram bildirim kanalı — karar ve plan (Odak / Faz 3)

**Son güncelleme:** 13 Temmuz 2026  
**Durum:** Kararlar kilitli · **TG-1/TG-2/TG-3 kodlandı** (Notifier send + Keeper alanları + deep link bağlama; default Telegram kapalı)  
**İlgili sohbet:** Odak teklif oturumu (12 Tem 2026) — bildirimlere Telegram eklenmesi  
**Üst mimari:** [MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md)

> **Kullanıcı @username / chat_id bağlama** için ayrıntılı belge:  
> **[TELEGRAM_USER_BINDING.md](./TELEGRAM_USER_BINDING.md)**

---

## 1. Bu belge ne için?

Teklifte (DI / Raporlama / İzleme) **Telegram standart bildirim kanalı** olarak geçiyor. Bu belge ürün kararlarını ve uygulama sırasını kilitler.

---

## 2. Geçmiş konuşmadan gelenler (teklif)

- Mevcut kanallar: **uygulama içi** + **e-posta**
- İstek: bunlara **Telegram** eklensin
- Ortak kanal modeli: DI · Raporlama · Monitoring (`in-app` / `email` / `telegram`)

---

## 3. Platformda bugün ne var?

| Katman | Durum |
|:---|:---|
| **In-app** | Operation Core `op_notifications` (+ Hub) — DI için henüz D-N bağlı değil |
| **E-posta** | MngNotifier `POST …/notifications/mail` — çalışır; D-N1 omurgası DI’de (default kapalı) |
| **Telegram** | **TG-1…4:** send-message, Keeper alanları, deep link, DI `document.generated` → telegram |
| **WhatsApp / Slack** | Planlı; Faz 3’te yok |

Ürün modeli: **tek yön bildirim** (chatbot değil). Push → grup veya kişi DM; deep link ile bağlama.

---

## 4. Mimari ilke (değişmez)

```text
DI / Reporting / Alarm  →  MngNotifier
  POST /notifications/mail
  POST /notifications/send-message  { channel: "telegram", to: [chat_id], text }
        → Telegram Bot API sendMessage

UI “Telegram’ı bağla”
  → t.me/<Bot>?start=link_{domainId}_{userId}
  → Notifier getUpdates (local) veya webhook (prod)
  → Keeper POST /api/internal/telegram-link
```

Bot token yalnızca MngNotifier’da (env / secret).

---

## 5. Telegram ≠ WhatsApp

Adres = **`chat_id`** (telefon değil). Keeper `phoneNumber` yeterli değil. Kişi bağlama: [TELEGRAM_USER_BINDING.md](./TELEGRAM_USER_BINDING.md). MVP grup: `DefaultChatId`.

---

## 6. Fazlar

| Dilim | İçerik | Durum |
|:---|:---|:---|
| **TG-1** | Notifier provider + `send-message` + env | **Kodlandı + canlı smoke** |
| **TG-2** | Keeper `telegramUsername` / `telegramChatId` / `telegramLinkedAt` + UI | **Kodlandı** |
| **TG-3** | Deep link `?start=link_…` + polling (local) / webhook (prod) | **Kodlandı** |
| **TG-4** | DI `document.generated` → `channels: telegram` + chatId resolve | **Kodlandı** |
| **TG-5** | `@message_templates` + sekmeli UI + yönetici test bildirimi | **Kodlandı** |

Smoke: `pwsh scripts/tests/MngNotifier/telegram/test-send-message.ps1`

Compose env: `TELEGRAM_ENABLED`, `TELEGRAM_BOT_TOKEN`, `TELEGRAM_DEFAULT_CHAT_ID`, `TELEGRAM_BOT_USERNAME`, `TELEGRAM_USE_POLLING`.

---

## 7. Kilitli kararlar

| # | Karar |
|:--:|:---|
| K1 | MVP önce **grup chat** (`DefaultChatId`) |
| K2 | Bot token müşteri/domain secret (Notifier); demo platform bot opsiyon |
| K3 | Telegram kapalıysa diğer kanallar devam |
| K4 | DI ilk olay: `document.generated` (TG-4) |
| K5 | TR kısa düz metin (mail HTML’den ayrı) |
| K6 | Faz 3’te WhatsApp **yok** |
| U1 | Ürün = **one-way notify**, interactive chatbot değil |
| U2 | Grup demo → kişi DM bağlama |
| U3 | Deep link (TG-3); local = polling |
| U4 | Kullanıcıya BotFather / Start rehberi binding dokümanında |

---

## 8. Karar kaydı

| Tarih | Madde |
|:---|:---|
| 12 Tem 2026 | Teklif: Telegram kanalı |
| 3 Haz 2026 | Push-only; Notifier event dinlemez |
| 13 Tem 2026 | USER_BINDING + one-way ürün kararı |
| 13 Tem 2026 | **TG-1:** send-message + TelegramBotMessageSender |
| 13 Tem 2026 | **TG-2:** Keeper telegram alanları + UI |
| 13 Tem 2026 | **TG-3:** deep link + polling/webhook + internal telegram-link |
| 13 Tem 2026 | **TG-4:** DI document.generated → Telegram (Channels + DefaultTelegramChatIds + Keeper resolve) |

---

## 9. İlgili yollar

| Konu | Path |
|:---|:---|
| Kullanıcı bağlama | [TELEGRAM_USER_BINDING.md](./TELEGRAM_USER_BINDING.md) |
| Mesajlaşma mimarisi | [MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md) |
| Smoke script | `scripts/tests/MngNotifier/telegram/test-send-message.ps1` |
| Notifier settings | `MngNotifierSettings:Telegram` |
| Keeper internal | `POST /api/internal/telegram-link` |
| Faz 3 DI | `docs/monitrang/faz3/document_intelligence/Roadmap.md` |
