# Kurumsal sohbet — kaldığımız yer

**Son güncelleme:** 17 Eylül 2026  
**Durum:** ⏸️ Araştırma duraklatıldı; kod yok. Mattermost **varsayılan hazır messenger adayı**.  
**Ana not:** [RESEARCH.md](./RESEARCH.md) · indeks: [README.md](./README.md)

---

## Bu oturumda netleşen

- Üç iş ayrı: (1) ürün içi / iş kaydına bağlı sohbet, (2) kurumun genel messenger’ı, (3) Notifier bildirimi.
- (1) → **Chat Room** duruyor; hazır ürüne yıkılmayacak; ses/görüntü Chat Room’a eklenmeyecek.
- (2) on-prem + denetlenebilir arşiv + SSO → hazır ürünlerde **Mattermost** önde. E2EE / air-gap / kamu-savunma → **Element**.
- (3) değişmedi: MngNotifier; WhatsApp saha kanalı; Slack/Telegram sink.
- Mattermost yazı + ses + ekran; kameralı toplantı değil. Gerekirse yanına **Jitsi**.
- Slack / Teams “kurum sunucusunda kalsın” şartında yok. Müşteri zaten M365 ve SaaS izinliyse ikinci messenger açılmasın.
- Rocket.Chat, Wire, Nextcloud Talk, gömülebilir SaaS (Stream/Sendbird) birinci tercih değil.

## Sıradaki açık soru (bir sonraki sohbet)

Mattermost’u müşteriye **opsiyonel yan paket** olarak mı sunacağız, yoksa on-prem MonitraNG kurulumunun parçası mı?

Bu karar lisans (Team Edition yetmez; SSO/export ücretli), yedek, güncelleme ve “kimin yedeğini kim alır” sorusunu belirler. Henüz seçilmedi.

İsteğe bağlı devam başlıkları:

- Şartname diline göre (KVKK / ISO 27001 / BDDK tarzı) Mattermost vs Element: denetçiye hangi cümle, hangi özellik şart
- Küçük mimari: Docker + Keycloak + Notifier webhook — henüz kurmadan
- Chat Room fazı ile bu araştırmanın çakışmaması (iki iş paralel, aynı ürün değil)

## Okuma sırası (yeni chat)

1. [README.md](./README.md)
2. Bu dosya
3. [RESEARCH.md](./RESEARCH.md) — özellikle §2, §5.1, §7, §8
4. Gerekirse [CHAT_ROOM_ROADMAP.md](../../content/chat_room/CHAT_ROOM_ROADMAP.md) ve [MESSAGING_CHANNELS.md](../notifications/MESSAGING_CHANNELS.md)

## Yeni chat prompt’u

```text
docs/odak/team_chat/DEVAM.md ve RESEARCH.md oku.

Konu: on-prem müşteriler için kurumsal messenger araştırması (17 Eyl 2026).
Ürün içi Chat Room duruyor; bildirim Notifier’da.
Varsayılan hazır aday Mattermost Enterprise; istisna Element.
Kaldığımız yer: Mattermost opsiyonel yan paket mi, kurulumun parçası mı — henüz karar yok.
Kod yazma; önce bu kararı ve gerekirse şartname/lisans netleştirmesini konuş.
```
