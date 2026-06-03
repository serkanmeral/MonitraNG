# Mesajlaşma kanalları (WhatsApp, Slack, Telegram, …) — Mimari ilkeler

**Son güncelleme:** 3 Haziran 2026  
**Durum:** Kararlandı (Odak planlama) — ⏸️ implementasyon e-posta fazından **sonra**

> **Öncelik:** Önce [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) (`send-template`, `@mail_templates`). Mesajlaşma kanalları aynı push-only disiplini izler; ayrı faz.

---

## 1. Kapsam

| Konu | Bu belgede |
|------|------------|
| **Kanallar** | WhatsApp (birincil hedef), ileride Slack, Telegram vb. |
| **Use case** | Alarm oluştu, work item oluştu, work item mention (benzer operasyonel olaylar) |
| **MngNotifier rolü** | Kanal router + credential seçimi + provider gönderimi |
| **Tetikleme** | **Çağıran servis** — ne zaman, kime, hangi template (servis config/policy) |
| **E-posta** | Ayrı endpoint ve render yolu — [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) |
| **In-app / Hub** | Değişmez — MO `op_notifications`, MngHub SignalR |

**Kapsam dışı (bilinçli):**

- Etkileşimli mesajlar (WhatsApp buton, liste, quick reply)
- Notifier’ın RabbitMQ / operasyonel event dinlemesi
- MO’nun provider credential bilmesi

---

## 2. Temel ilke: e-posta ile aynı push modeli

**MngNotifier hiçbir olay dinlemez.** Alarm, work item, mention vb. olayları **ilgili servis** işler; mesaj göndermek istediğinde Notifier HTTP API’sini çağırır.

```text
┌──────────────────┐   olay + servis config       ┌─────────────────────────────┐
│  MngOperations   │ ────────────────────────────► │ Bu olayda kanal açık mı?    │
│  MngAlarm        │                               │ Alıcı listesi resolve       │
│  (ileride DG…)   │                               │ templateKey + context       │
└────────┬─────────┘                               └──────────────┬──────────────┘
         │  POST /api/v1/notifications/send-message               │
         │       { channel, to, templateKey, context, domainId }   │
         └─────────────────────────────────────────────────────────►
                                    ┌──────────────────┐
                                    │   MngNotifier    │
                                    │ credential pick  │
                                    │ template → prov. │
                                    └────────┬─────────┘
                         ┌──────────────────┼──────────────────┐
                         ▼                  ▼                  ▼
                    WhatsApp           Slack (ileride)    Telegram (ileride)
```

**Push model:** Çağıran → Notifier. Notifier → RabbitMQ ❌

---

## 3. Use case haritası

| Olay | Çağıran servis | Config kaynağı | Tipik alıcı |
|------|----------------|----------------|-------------|
| Work item oluştu | MngOperations | `op_notification_policies` | assignee, watchers |
| Work item mention | MngOperations / rule | rule + policy | mention edilen |
| Alarm oluştu | MngAlarm | alarm notification settings | abone / rol |
| (ileride) Chat mention | MngDataGateway | DG notify config | mention edilen |

**Ne zaman mesaj gideceğine** yalnızca çağıran servisin config’i karar verir. Notifier policy bilmez.

---

## 4. Sorumluluk ayrımı

| Sorumluluk | Servis |
|------------|--------|
| Olayı yakalamak | MngOperations, MngAlarm, … |
| Bu olayda hangi kanallar? | **Çağıran servis config** (OC: `channels[]`, alarm: kendi ayarları) |
| Kime? | Çağıran servis: assignee, watchers, mention edilen, … |
| E-posta adresi | Keeper / Keycloak — mevcut akış |
| **Telefon numarası** | **Keycloak / MngKeeper `phoneNumber`** — varsa kullan, yoksa **atla** (hata değil) |
| Template anahtarı seçimi | Çağıran servis policy’si |
| `context` JSON üretimi | Çağıran servis |
| Credential seçimi (domain WABA vs platform BSP) | **MngNotifier** (`domainId` + `channel`) |
| Provider’a gönderim | **MngNotifier** (`IMessageChannelProvider`) |
| In-app kayıt | MngOperations → `op_notifications` |

---

## 5. Alıcı çözümleme — telefon

- Kaynak: MngKeeper kullanıcı profili / Keycloak attribute **`phoneNumber`** (JWT claim olarak da mevcut).
- MO person çözümlemesi (`IKeeperDirectoryClient`) ile orchestrator e-posta alıcılarından sonra messaging alıcılarını türetir.
- `phoneNumber` boş veya tanımsız → o kullanıcı **sessizce atlanır**; debug/info log yeterli.
- Format doğrulama (**E.164**) tercihen **Notifier**’da (tüm kanallar için tek yer).

---

## 6. Credential modeli

İki katmanlı fallback:

```text
domainId + channel (ör. whatsapp)
  → DomainNotificationChannels (veya eşdeğer domain ayarı)
       → tanımlıysa: domain’in kendi WhatsApp Business (WABA) credentials
       → tanımlı değilse: platform varsayılan BSP credentials
```

| Senaryo | Kim yönetir / öder | Notifier config |
|---------|-------------------|-----------------|
| Kurumsal müşteri kendi WABA | Müşteri (domain başına) | Domain-scoped encrypted secret |
| Küçük tenant / demo / ortak hizmet | Platform | `MngNotifierSettings.DefaultChannels.WhatsApp` (BSP) |
| Slack / Telegram (ileride) | Genelde domain bot token | Aynı pattern — domain override + platform default |

**Güvenlik:** Token/secret yalnızca Notifier’da tutulur. Çağıran servis yalnızca `domainId` gönderir; credential taşımaz.

---

## 7. WhatsApp kısıtları (basit kapsam)

- **Etkileşimli mesaj yok** — yalnızca onaylı **düz metin template** mesajları.
- Soğuk iletişim: Meta onaylı template zorunlu (alarm, WI oluştu, mention senaryoları buna uygun).
- 24 saatlik session penceresi / serbest metin — bu fazda **hedeflenmiyor**.
- E-posta `@mail_templates` HTML’i ile **paylaşılmaz**; Meta’da ayrı onaylı template adları gerekir.
- **Ortak `context` JSON** hem e-posta hem WhatsApp’a verilebilir; Notifier kanala göre map eder.

Örnek policy genişlemesi (MngOperations):

```json
{
  "eventType": "WorkItemCreated",
  "channels": ["inApp", "email", "whatsapp"],
  "recipients": ["assignee", "watchers"],
  "emailTemplateKey": "work-item-created",
  "whatsappTemplateKey": "work_item_created_tr"
}
```

Rule effect (hedef): `sendMessageViaMngNotifiers` — kanal parametreli; mevcut `sendEmailViaMngNotifiers` genelleştirmesi.

---

## 8. Notifier API (hedef)

Messaging kanalları için **tek ortak endpoint** (Slack/Telegram eklemeyi kolaylaştırır). E-posta ayrı kalır (`send-template`, `mail`).

### 8.1 Birincil yol — template + context

```http
POST /api/v1/notifications/send-message

{
  "channel": "whatsapp",
  "to": ["+905551234567"],
  "templateKey": "work-item-created",
  "language": "tr",
  "context": {
    "workItem": { "key": "WI-42", "title": "Pompa arızası" },
    "actor": { "displayName": "Ahmet" }
  },
  "domainId": "abc123"
}
```

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| `channel` | Evet | `whatsapp`, `slack`, `telegram`, … |
| `to` | Evet | Kanala özgü adres (E.164 telefon, Slack channel id, …) — **çağıran resolve etmiş** |
| `templateKey` | Evet | Notifier/provider template eşlemesi |
| `context` | Evet | Placeholder kaynağı — e-posta ile paylaşılabilir |
| `domainId` | Evet (multi-tenant) | Credential seçimi |
| `language` | Hayır | WhatsApp template dili (vars. `tr`) |

**Response (taslak):** `notificationId`, `status` (`sent` / `failed`), `channel`, `queuedAt` — e-posta yanıtı ile hizalı.

### 8.2 Provider soyutlaması (Notifier içi)

```csharp
interface IMessageChannelProvider
{
    string ChannelId { get; }  // whatsapp, slack, telegram
    Task SendAsync(MessageSendRequest request, ChannelCredentials creds, CancellationToken ct);
}
```

Ek bileşenler:

- `IChannelCredentialResolver` → `domainId + channel` → credentials
- `IMessageTemplateMapper` → `templateKey + channel + language` → provider payload

E-posta (`IMailProvider` / SMTP) ayrı kalır; messaging kanalları bu arayüz ailesinde birleşir.

---

## 9. Önerilen uygulama sırası

| Sıra | İş | Not |
|------|-----|-----|
| 1 | E-posta `send-template` + `@mail_templates` | [DEVAM.md](./DEVAM.md) — mevcut odak |
| 2 | Notifier: credential store + `send-message` iskeleti | Domain/platform fallback |
| 3 | WhatsApp MVP | Platform BSP, 1–2 onaylı Meta template |
| 4 | MO: `channels: ["whatsapp"]` + telefon resolve | `op_notification_policies` genişlemesi |
| 5 | Domain WABA override | Domain admin / encrypted config |
| 6 | MngAlarm entegrasyonu | Alarm servisi kendi config → Notifier |
| 7 | Slack / Telegram | Yeni `IMessageChannelProvider` implementasyonu |

---

## 10. Bilinçli olarak yapılmayanlar

- Notifier RabbitMQ consumer veya operasyonel event subscription
- Etkileşimli WhatsApp (buton, liste, carousel)
- Telefon zorunluluğu — yoksa skip, exception değil
- MO’da provider secret veya WABA token yönetimi
- Notifier’ın “hangi olayda mesaj atılır” policy’si

---

## 11. Karar kaydı

| Tarih | Karar |
|-------|--------|
| 3 Haz 2026 | Mesajlaşma kanalları e-posta ile **aynı push-only** disiplin; Notifier event dinlemez |
| 3 Haz 2026 | **Ne zaman / kime** çağıran servis config’i; Notifier yalnızca gönderir |
| 3 Haz 2026 | Telefon: Keycloak **`phoneNumber`**; tanımsızsa kanal atlanır |
| 3 Haz 2026 | WhatsApp: domain **kendi WABA** veya **platform BSP** (tüm domainler ortak) |
| 3 Haz 2026 | Etkileşimli mesaj yok; onaylı düz template yeterli |
| 3 Haz 2026 | **Önce e-posta**; WhatsApp/messaging sonraki faz |
| 3 Haz 2026 | Sadece WhatsApp değil — **Slack, Telegram** vb. aynı `send-message` + provider modeli |
| 3 Haz 2026 | Use case: alarm oluştu, WI oluştu, WI mention (+ benzer operasyonel olaylar) |

---

## 12. İlgili belgeler

| Belge | Konum |
|-------|--------|
| E-posta mimarisi | [MAIL_ARCHITECTURE.md](./MAIL_ARCHITECTURE.md) |
| Mevcut kod durumu | [MEVCUT_DURUM.md](./MEVCUT_DURUM.md) |
| MO entegrasyonları | [INTEGRATIONS.md](../operationcore/mngoperations/INTEGRATIONS.md) |
| OC notification policies | [operationcore_phase1.md §16](../operationcore/operationcore_phase1.md) |
| MngNotifier ROADMAP Phase 7 | [ROADMAP.md](../../content/MngNotifier/main/ROADMAP.md) |
| Keeper `phoneNumber` | MngKeeper user profile / JWT claim |
