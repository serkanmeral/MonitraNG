# Kurumsal sohbet aracı araştırması

**Tarih:** 17 Eylül 2026  
**Kaynak:** planlama sohbeti (implementasyon yok)  
**Durum:** Araştırma; Mattermost kurumsal on-prem messenger **varsayılan adayı**. Kilitlenmiş ürün kararı değil.  
**Devam:** [DEVAM.md](./DEVAM.md)

---

## 1. Neden bakıldı

Müşteriler genellikle belirli güvenlik standartlarına uymak zorunda; teslimat on-prem. Bir standardın **sohbetlerin kurum içinde kalmasını** zorunlu tutması halinde:

- Kendi Chat Room’umuz (ürün içi) bu maddeye hizmet edebilir.
- Aynı ihtiyacı karşılayan **hazır ürünler** de bilinmeli.
- Slack / Teams SaaS, “kurum sunucusunda kalsın” şartında çoğu zaman düşer.

Bu belge sohbet uygulamasını seçmek içindir. MngNotifier e-posta / WhatsApp / Telegram **bildirim** işidir; burada ikame edilmez.

---

## 2. Üç iş karıştırılmamalı

Tek “en iyi sohbet aracı” yok. Aynı ürüne üç iş yıkılmamalı.

| İş | Ne için | Bu araştırmadaki yön |
|----|---------|----------------------|
| **İş kaydına bağlı sohbet** | Sipariş, proje, DI belgesi, görev üzerinde konuşma; tenant, RBAC, denetim izi | **MonitraNG Chat Room** (müşteri on-prem’inde, DG `cht_*`, Keycloak / Keeper). Hazır Slack klonu bunu ikame etmesin. |
| **Kurumun genel messenger’ı** | Günlük kanal, DM, duyuru; isteğe bağlı ses / ekran | Hazır ürün. Müşteride Teams/Slack yoksa veya SaaS yasaksa → **Mattermost** (varsayılan). Yüksek gizlilik / E2EE / air-gap → **Element**. |
| **Ulaşan bildirim** | Alarm, work item, mention, sipariş durumu | **MngNotifier** (e-posta, planlı WhatsApp; Slack/Teams/Telegram sink). Messenger ürünü değil. |

```text
MonitraNG UI
  └─ Chat Room          → iş nesnesine bağlı konuşma (ürün)
  └─ MngNotifier        → e-posta, WhatsApp, ileride webhook sink
Dış / yan paket
  └─ Mattermost         → ofis messenger (on-prem varsayılan aday)
  └─ Element            → istisna: E2EE, air-gap, kamu/savunma dili
  └─ Jitsi (opsiyonel)  → kameralı toplantı Mattermost yanında
  └─ WhatsApp           → saha/müşteri bildirimi (iç sohbet değil)
```

---

## 3. Ürün içi Chat Room — neden duruyor

Mevcut plan: DM, konu odası, Keycloak grup sohbeti, mention, Hub/SignalR, DG kalıcılık. Yeni sohbet-only mikroservis yok (MVP).

On-prem güvenlik cümlesinde **iş sohbeti** için en temiz cevap bu:

- Veri müşterinin Mongo / yedek / erişim modelinde.
- Kimlik zaten Keycloak / Keeper.
- Denetçiye “sohbet ayrı bir ABD SaaS’inde” denmez; operasyon verisinin parçasıdır.
- Ek vendor ve ek lisans yok.

Yetmeyeceği yer: tüm şirketin Slack’ini ikame etmek (mobil istemci olgunluğu, eDiscovery formatı, hukuki tutma, DLP, binlerce kişilik kanal, air-gap federasyon). Bunları Chat Room’a yığmak izleme ürününü messenger şirketine çevirir.

**Karar (araştırma):** Chat Room’u durdurmayın. Ses/görüntü Chat Room’a eklenmesin (WebRTC + TURN + kayıt = ikinci medya platformu).

İlgili: [CHAT_ROOM_ROADMAP.md](../../content/chat_room/CHAT_ROOM_ROADMAP.md).

---

## 4. “Sohbet kurum içinde kalsın” ne demek

ISO 27001 tek başına on-prem sohbet zorunlu kılmaz; bilgiyi kontrol altında tutar. KVKK yurt dışına aktarımı sorar. BDDK, kritik altyapı, bazı kamu şartnameleri SaaS messenger’ı fiilen kapatır.

Denetçi dili pratikte şunlardan biri (veya birkaçı):

| Denetçi dili | Pratik karşılık |
|--------------|-----------------|
| Veri yurt / kurum dışına çıkmasın | Slack, Teams, Google Chat düşer. On-prem veya müşterinin kendi bulutu. |
| Kimlik kurumsal olsun | Keycloak / AD / LDAP. |
| Kayıt, saklama, hukuki tutma | Mesaj silinse bile yetkili çıkarabilsin. |
| Ağ izolasyonu | Federasyon kapalı, vendor telemetrisi yok, gerekirse air-gap. |
| Uçtan uca şifre (E2EE) | Sunucu yöneticisi bile okuyamasın. |

**E2EE ile arşiv ters yöndedir.** Banka / enerji / tipik izleme müşterisinde sık görülen model E2EE değil; **veri içeride + denetlenebilir arşiv**. Savunma / yüksek gizlilikte E2EE + air-gap öne çıkar. Ürün vaadinde E2EE’yi varsayılan yapmayın.

---

## 5. Hazır ürünler

### 5.1 Mattermost (varsayılan aday)

Self-hosted ekip sohbeti (Slack benzeri his). Go tek binary, React, **PostgreSQL** (mevcut domain Mongo’suna karışmaz). Docker / Nginx ile sizin compose dünyasına yakın.

**Kullanıcı:** kanallar, DM, thread, dosya, mention, arama, webhook/bot, web + masaüstü + mobil.

**Neden öne çıktı (Odak / on-prem müşteri):**

- Veri müşteri tesisinde.
- Denetim modeli E2EE gizemi değil; SSO + saklama + **compliance export** (CSV, Global Relay, Smarsh, Proofpoint — Enterprise).
- MonitraNG’den odaya bot/webhook doğal.
- Ses + ekran paylaşımı var (aşağıda §6).

**Lisans tuzakı:** Open-core. Team Edition (MIT) küçük ekip / hobi; v11 civarında kabaca **250 kullanıcı**, **ticari SSO yok**. Keycloak ile tüm şirketi bağlamak Entry veya **Professional / Enterprise** ister. Ciddi müşteri tesliminde ücretsiz Team Edition varsayılmasın.

**Zayıf:** Slack ekosistemi kadar zengin değil; arama/mobil ücretsiz katmanda her zaman kurumsal beklentiyi karşılamaz; güncelleme/yedek/SSL işlenecek. Kameralı toplantı ürünü değil (§6).

### 5.2 Element + Matrix (ESS Pro)

Avrupa “dijital egemenlik” adayı. On-prem, air-gap, federasyonu kapatma, Signal tipi E2EE. Kamu referansları (ör. Fransız Tchap, Alman kamu/ordu türevleri). Kubernetes / ESS ağır. Denetim için odaya kayıt botu / audit paketi gerekir; yoksa “şifreli, arşiv yok” ters köşe olur.

**Ne zaman:** şartname E2EE, air-gap, kamu/savunma dili konuşuyorsa. “Basit Slack kuralım” değil.

### 5.3 Diğerleri (sıra)

| Ürün | Not |
|------|-----|
| **Wire Enterprise** | Varsayılan E2EE, on-prem kurumsal planda. Güvenli 1:1 / yönetici görüşmesi; ChatOps zayıf. |
| **Rocket.Chat Enterprise** | On-prem + omnichannel güçlü. İşletme (Node + Mongo + eklenti/lisans) ağır; uyumluluk hikâyesi Mattermost kadar net değil. Birinci tercih değil. |
| **Nextcloud Talk** | Müşteride zaten Nextcloud varsa. Saf messenger olarak zayıf. |
| **Jitsi Meet** | Sohbet değil; on-prem görüntülü toplantı. Mattermost veya Rocket.Chat yanına konur. |
| **Zulip** | Thread disiplini iyi; şirket geneli messenger zayıf. |
| **Discord** | Kurumsal / denetim / SSO için uygun değil. |
| **Stream / Sendbird / TalkJS** | Ürün içi kiralık sohbet. Keeper/Keycloak/DG modeli hazırken vendor lock-in; önerilmez. |
| **Slack / Teams / Google Chat** | Gov/ulusal bulut bile **on-prem değil**. Şartname “kurum sunucusunda” diyorsa cevap olmaz. Müşteri zaten M365 yaşıyor **ve** SaaS izinliyse ikinci messenger açılmasın. |

---

## 6. Ses ve görüntü

Mattermost yalnızca yazı değil; **Zoom / Teams toplantı ikamesi de değil**.

### Mattermost Calls

Yerleşik eklenti, WebRTC; medya self-host edilebilir (gerekirse ayrı **RTCD**).

| Özellik | Durum |
|---------|--------|
| 1:1 ses | Var (ücretsiz katmanda da) |
| Kanal / grup sesi | Professional / Enterprise (kabaca 50 kişi; ötesi RTCD) |
| Ekran paylaşımı | Web / masaüstü; mobilde yok |
| Kamera | **Deneysel.** Sadece 1:1 DM, web/masaüstü. Kanal toplantısında yok. Mobilde yok. Kayıtta kamera yok. |
| Kayıt / transkript / altyazı | Enterprise |

Team Edition’da grup araması kısıtlı / süreli.

### Karşılaştırma

| Ürün | Yazı | Ses | Görüntü / toplantı |
|------|------|-----|---------------------|
| Mattermost | Çekirdek | Calls (ses + ekran) | Kamera deneysel, DM-only |
| Element | Çekirdek | Var | **Element Call** (MatrixRTC + LiveKit, E2EE) ve/veya odaya Jitsi. Kameralı on-prem paket burada en dolu. |
| Rocket.Chat | Çekirdek | Jitsi / BBB ile | Yerleşik değil; yanına Jitsi |
| Wire | Var | Çekirdek, E2EE | 1:1 / küçük grup; toplantı canavarı değil |
| Nextcloud Talk | Var | Var | Aynı stack’te toplantı |
| Jitsi (tek başına) | Yok | Var | On-prem konferans |

Ses/görüntü on-prem’de yazıdan ağırdır: UDP/WebRTC, STUN/TURN (Coturn), ayrı medya sunucusu. Air-gap ve sıkı firewall’da yazı kolay, medya zahmetli. Şartnamedeki “sohbet içeride kalsın” çoğu zaman **metin arşivi**dir; medyayı aynı cümleye eklemek kayıt politikasını (kişisel veri) büyütür.

**Yön:** yazı + kısa ses + ekran → Mattermost yeterli. Kameralı ekip toplantısı → Element Call veya Mattermost + Jitsi.

---

## 7. Önerilen iş bölümü (araştırma sonucu)

```text
Varsayılan hazır messenger     → Mattermost Enterprise (SSO + Calls + export)
İstisna (yüksek gizlilik)      → Element ESS Pro
Ürün içi iş sohbeti            → kendi Chat Room (on-prem, bağlama bağlı)
Toplantı şartnamesi ayrıca     → Jitsi (Mattermost’un yanına)
Saha / müşteri haberi          → e-posta / WhatsApp (Notifier)
```

Satış/şartname dilinde genel amaçlı messenger **çekirdek ürün değil**, tercihen **opsiyonel entegrasyon**: aynı Keycloak, aynı VLAN, veri müşteri VM’inde. MonitraNG içine gömülmez.

---

## 8. Bilinçli olarak kilitlenmeyen

1. Mattermost müşteriye **opsiyonel yan paket** mi, yoksa MonitraNG kurulumunun parçası mı? (lisans, yedek, kim işletir)
2. POC / demo ortamında Team Edition ile deneme vs. baştan Enterprise varsayımı
3. Keycloak SSO’nun hangi Mattermost sürümünde zorunlu olduğu (Entry / ücretli) — teslim öncesi lisans teyidi
4. Chat Room MVP kapsamı bu araştırmayla değişmedi; durdurulmadı

---

## 9. İlgili belgeler

| Belge | İlişki |
|-------|--------|
| [docs/content/chat_room/CHAT_ROOM_ROADMAP.md](../../content/chat_room/CHAT_ROOM_ROADMAP.md) | Ürün içi sohbet (DM / konu / grup) |
| [../notifications/MESSAGING_CHANNELS.md](../notifications/MESSAGING_CHANNELS.md) | WhatsApp / Slack / Telegram — push bildirim, sohbet ürünü değil |
| [../notifications/README.md](../notifications/README.md) | MngNotifier planlama indeksi |
| [../compliance/README.md](../compliance/README.md) | ISO 27001 / AS9100 ürün-uyum planı |
