# Telegram — kullanıcı bağlama (username / chat_id)

**Son güncelleme:** 13 Temmuz 2026  
**Durum:** TG-2/TG-3 uygulandı — deep link + local polling; webhook prod için hazır  
**Üst belge:** [TELEGRAM.md](./TELEGRAM.md) · [MESSAGING_CHANNELS.md](./MESSAGING_CHANNELS.md)

Bu belge Telegram’a aşina olmayan ekip için yazılmıştır.

---

## 1. Üç kavram (karıştırmayın)

| Kavram | Ne | Örnek | Bildirim için yeterli mi? |
|:---|:---|:---|:---:|
| **Telegram @username** | Kullanıcının Telegram’da seçtiği genel ad | `@ayse_yilmaz` | **Hayır** (tek başına göndermek güvenilir değil) |
| **chat_id** | Telegram’ın o sohbet için verdiği sayısal kimlik | `123456789` veya `-10012…` (grup) | **Evet** — Bot API buna mesaj atar |
| **Bot** | Kurumun BotFather ile oluşturduğu hesap | `@OdakMonitraBot` | Token Notifier’da saklanır |

**Kural:** Gönderim hedefi her zaman **`chat_id`**.  
`@username` insanlar için okunur etiket + bağlama yardımcısıdır; Keeper profilinde tutulur.

```text
MonitraNG kullanıcısı  ←→  telegramUsername (@ayse…)   [görünür alan]
                       ←→  telegramChatId (123…)       [gönderim adresi]
```

E-posta ≈ `email` alanı gibi düşünün: username ≈ “görünen adres”, chat_id ≈ “gerçek SMTP’nin anladığı mailbox id”.

---

## 2. Neden telefon / e-posta yetmez?

WhatsApp planında alıcı çoğu zaman `phoneNumber` idi. Telegram Bot API:

- Numaraya doğrudan yazmaz  
- Kullanıcı (veya grup) bot ile **en az bir kez etkileşime** girmelidir (`Start` veya gruba ekleme)  
- Sonra bot o sohbetin `chat_id` değerini öğrenir ve saklarız  

Bu yüzden konuştuğumuz model: **Kullanıcı kartına Telegram alanları eklemek**.

---

## 3. Keeper / kullanıcı profili alanları (hedef)

MngKeeper kullanıcı (veya kişi) kaydına:

| Alan | Tip | Zorunlu | Açıklama |
|:---|:---|:---:|:---|
| `telegramUsername` | string | Hayır | `@` ile veya `@`siz; UI’da gösterilir, arama/admin için |
| `telegramChatId` | string | Hayır* | Bot’un mesaj atacağı id; *kişisel DM için gönderimde şart |
| `telegramLinkedAt` | datetime | Hayır | Bağlama zamanı (audit) |

Boş `telegramChatId` → o kullanıcıya Telegram DM **atlanır** (hata değil; e-posta/in-app devam edebilir) — WhatsApp’taki “telefon yoksa skip” ile aynı disiplin.

**Not:** Grup operasyon kanalı domain ayarındadır (`defaultTelegramChatId`); kullanıcı alanından bağımsız.

---

## 4. İki teslimat modeli (ikisi de desteklenecek)

### 4.1 Operasyon grubu (hızlı MVP)

1. BotFather → bot + token → Notifier credential  
2. Telegram’da grup aç → bot’u ekle  
3. Grup `chat_id` öğren → domain `defaultTelegramChatId`  
4. DI/Rapor/Alarm’da kanal `telegram` açık → mesaj **gruba** gider  

Kişi profili gerekmez. Kurumsal “herkese ortak uyarı” için ideal.

### 4.2 Kişiye özel DM (username + bağlama)

Konuştuğumuz asıl model burası:

1. Kullanıcı Telegram uygulamasında **@username** açar (Settings → Username) — opsiyonel ama önerilir  
2. Admin veya kullanıcı, MonitraNG profiline `telegramUsername` yazar (örn. `ayse_yilmaz`)  
3. Kullanıcı bot’u açıp **Start** eder (tercihen deep link ile, aşağıdaki §5)  
4. Sistem `chat_id` + (varsa) username’i kaydeder → `telegramChatId` dolar  
5. Bildirim policy “assignee / izleyen” seçince Notifier’a `chat_id` listesi gider  

Sadece username yazıp Start etmeden DM **gönderilemez** — bunu kullanıcıya UI’da açık yazın.

---

## 5. Bağlama akışı (önerilen UX)

### 5.1 Deep link (tercih)

```text
https://t.me/<BotUserName>?start=link_<mngPersonId>
```

1. MonitraNG “Telegram’ı bağla” → bu linki gösterir / QR  
2. Kullanıcı tıklar → Telegram açılır → Start  
3. Bot webhook / long-poll: `message.from.id` (= chat_id), `message.from.username`, `start` payload’taki personId  
4. Keeper kullanıcı güncellenir: `telegramChatId`, `telegramUsername`, `telegramLinkedAt`  
5. UI: “Bağlı ✓ @ayse_yilmaz”

### 5.2 Admin manuel

- Admin `telegramUsername` girer (bilgi amaçlı)  
- `telegramChatId` genelde manuel girilmez (hata riski); bağlama linki tercih edilir  
- İstisna: bilinen grup `chat_id` domain ayarına yazılır  

### 5.3 chat_id nasıl öğrenilir? (operasyon / debug)

| Yöntem | Not |
|:---|:---|
| Bot’a `/start` + webhook log | Kişi DM |
| Gruba bot ekle + bir mesaj; `getUpdates` | Grup id (genelde negatif) |
| `@userinfobot` vb. yardımcı botlar | Sadece keşif; prod bağlama deep link olmalı |

---

## 6. Gönderim sırası (çağıran servis)

```text
Policy: channels includes "telegram"
  → alıcı kişiler resolve
  → her kişi için telegramChatId var mı?
       evet → to[] listesine ekle
       hayır → skip + log
  → isteğe bağlı: domain defaultTelegramChatId (grup) da ekle
  → POST Notifier send-message { channel: "telegram", to: [chatIds...], text/context }
```

Notifier bot token ile `sendMessage` çağırır. Credential çağıran serviste **olmaz**.

---

## 7. UI’da kullanıcıya gösterilecek metin (özet)

**Profil / bildirim ayarı:**

> Telegram bildirimi için:  
> 1) Telegram’da bir kullanıcı adı (@username) oluşturun (önerilir).  
> 2) Aşağıdaki “Telegram’ı bağla” ile bot’u başlatın.  
> 3) Bağlantı tamamlanınca kişisel bildirimler bu hesaba gider.  
> Bağlamadan önce yalnızca e-posta / uygulama içi çalışır.

**Admin:**

> Ortak operasyon grubu domain ayarından yönetilir.  
> Kişi alanındaki @username bilgilendirme içindir; gerçek gönderim chat_id ile yapılır.

---

## 8. Güvenlik / KVKK notları

- Bot token yalnızca Notifier secret store  
- `chat_id` kişisel veri sayılır; erişim yetkili admin + ilgili kullanıcı  
- Kullanıcı “bağlantıyı kaldır” → `telegramChatId` / username temizlenir  
- Bot’a yazmayan / engelleyen kullanıcıya gönderim başarısız → log; diğer kanallar etkilenmez  

---

## 9. Fazlama (Odak / Faz 3)

| Dilim | Kapsam | Durum |
|:---|:---|:---|
| **TG-0** | Bu belge + [TELEGRAM.md](./TELEGRAM.md) karar kilidi | Tamam |
| **TG-1** | Notifier TelegramProvider + domain bot token + **grup** `defaultTelegramChatId` | Kodlandı |
| **TG-2** | Keeper alanları: `telegramUsername`, `telegramChatId` (+ UI profil) | Kodlandı |
| **TG-3** | Deep link bağlama (`?start=link_…`) + webhook/polling | Kodlandı (local: polling) |
| **TG-4** | DI / Rapor / Alarm policy `channels: telegram` + kişi resolve | DI document.generated kodlandı |
| **TG-5** | `@message_templates` + sekmeli Bildirim şablonları UI + yönetici test bildirimi | Kodlandı (dataset seed + Notifier templateKey + UI) |

Öneri: TG-1 (grup) erken demo; TG-2/3 kişi modeli teklifteki “kullanıcıya username” vaadini karşılar.

---

## 10. Karar kaydı

| Tarih | Madde |
|:---|:---|
| 12 Tem 2026 | Teklife Telegram kanalı eklendi |
| 13 Tem 2026 | Ayrıntılı username/bağlama belgesi yoktu → **bu dosya yeniden yazıldı** |
| 13 Tem 2026 | Model: profilde **telegramUsername + telegramChatId**; gönderim chat_id; grup kanalı domain default |
| 13 Tem 2026 | **TG-5:** `@message_templates`, send-message `templateKey`, Bildirim şablonları sekmeleri, kullanıcı test bildirimi |

---

## 11. Onay bekleyenler

| # | Soru | Öneri |
|:--:|:---|:---|
| U1 | Profil alan adları yukarıdaki gibi mi? | Evet |
| U2 | MVP önce grup (TG-1) mi, yoksa doğrudan kişi bağlama (TG-2/3)? | Önce **TG-1 grup**, hemen ardından TG-2/3 |
| U3 | Username zorunlu mu? | Hayır — önerilir; chat_id asıl zorunlu |
| U4 | Deep link mi, sadece manuel chat_id mi? | **Deep link** (TG-3) |
