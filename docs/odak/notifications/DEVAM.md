# DEVAM — MngNotifier e-posta planlama (Kaldığımız yer)

**Son güncelleme:** 3 Haziran 2026  
**Durum:** ⏸️ **Planlama duraklatıldı** — mimari + dataset tasarimi tamam; implementasyon sonraki oturum

> **Kapsam:** Yalnizca **MngNotifier** (e-posta). MngOperations entegrasyonu **ayri calisma**.

---

## 1. Tek cümlede durum

**Direct mail (`POST /notifications/mail`) local'de calisiyor ve dogrulandi.** Push-only mimari, template render Notifier'da, DG `@mail_templates` + `@mail_layouts` sema ve seed'ler dokumante edildi. **Siradaki:** Notifier `send-template` implementasyonu + dataset kurulum scripti.

---

## 2. Bu oturumda kararlasanlar

| Konu | Karar |
|------|--------|
| Notifier kapsami (bu faz) | Yalnizca **e-posta** |
| Event dinleme | **Yok** — Notifier RabbitMQ/olay tuketmez |
| Tetikleme | Cagiran servis → **HTTP push** (`/mail` veya `/send-template`) |
| Template icerigi | DG **`@mail_templates`** + **`@mail_layouts`** |
| Template render | **MngNotifier** (subject, body, layout, placeholder) |
| Cagiran gonderir | `templateKey` + `context` + `to` |
| MO workspace policy | Ayri calisma — bu planda yok |
| RabbitMQ consumer (Notifier) | **Kapsam disi** |
| Odak test logosu | [datasets/odak_test_branding.json](./datasets/odak_test_branding.json) |

Detay: [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) · [MAIL_TEMPLATES.md](./MAIL_TEMPLATES.md)

---

## 3. Bu oturumda uretilen dokumanlar

| Dosya | Icerik |
|-------|--------|
| [README.md](./README.md) | Indeks |
| [MEVCUT_DURUM.md](./MEVCUT_DURUM.md) | Kod gercegi |
| [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) | Push-only HTTP mimarisi |
| [MAIL_TEMPLATES.md](./MAIL_TEMPLATES.md) | Template + render ozeti |
| [datasets/DATASETS.md](./datasets/DATASETS.md) | Alan tanimlari |
| [datasets/notifier_datasets.json](./datasets/notifier_datasets.json) | 2 dataset semasi |
| [datasets/notifier_mail_layouts_seed.json](./datasets/notifier_mail_layouts_seed.json) | `default`, `minimal` |
| [datasets/notifier_mail_templates_seed.json](./datasets/notifier_mail_templates_seed.json) | 6 system sablon |
| [datasets/odak_test_branding.json](./datasets/odak_test_branding.json) | Odak logo URL |

---

## 4. Dogrulama (local)

| Tarih | Test | Sonuc |
|-------|------|-------|
| 3 Haz 2026 | Gmail dev SMTP → `serkan.meral@outlook.com` | ✅ Mail geldi |
| 3 Haz 2026 | Tekrar test (`notificationId` bb3e5961-…) | ✅ Mail geldi |

Endpoint: `POST http://localhost:5070/api/v1/notifications/mail`  
Config: `MngNotifier.Api/appsettings.Development.json` (Gmail SMTP)

---

## 5. Sonraki oturum — oncelikli backlog (Notifier)

1. **Dataset kurulum** — `setup-notifier-datasets.ps1` (kategori + 2 dataset + seed)
2. **`POST /api/v1/notifications/send-template`** — DTO, DG client, render engine, layout birlestirme
3. **Placeholder engine** — `{{path.to.field}}`, `variables[]` validasyonu, HTML encode
4. **Logo** — bos `domain.logoUrl` → `<img>` atlama
5. **`POST /notifications/preview-template`** (opsiyonel)
6. **Faz A** — production Mailu SMTP, endpoint guvenligi (internal key / gateway-only)
7. **Eski dokuman senkronu** — `docs/content/MngNotifier/…` Odak kararlariyla hizalama (ayri PR)

**Kapsam disi (sonra / baska chat):** MO orchestrator → `send-template`, Keeper `domain-created`, admin template UI.

---

## 6. Acik sorular (ertelendi)

| # | Konu |
|---|------|
| N1 | Mail endpoint auth / rate limit |
| N7 | Coklu dil (TR/EN) template |
| N10 | Delivery audit / Mongo kayit |
| N11 | Odak production Mailu dogrulama |

---

## 7. Karar kaydi (tam)

| Tarih | Karar | Not |
|-------|-------|-----|
| 3 Haz 2026 | Direct mail calisiyor | sermeral → serkan.meral@outlook.com |
| 3 Haz 2026 | Push-only; Notifier event dinlemez | MAIL_ARCHITECTURE |
| 3 Haz 2026 | Sadece e-posta (Notifier) | SMS/push yok (mesajlaşma kanallari ayri belge) |
| 3 Haz 2026 | WhatsApp/Slack/Telegram kararlari | [MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md) — e-posta sonrasi faz |
| 3 Haz 2026 | Render Notifier'da | templateKey + context |
| 3 Haz 2026 | DG `@mail_templates` + `@mail_layouts` | datasets/ |
| 3 Haz 2026 | Odak logo URL kayitli | odak_test_branding.json |
| 3 Haz 2026 | Planlama duraklatildi | Implementasyon sonraki oturum |

---

**Yeni chat'te devam:** Once bu dosyayi oku → [MAIL_TEMPLATES.md](./MAIL_TEMPLATES.md) §5 backlog.
