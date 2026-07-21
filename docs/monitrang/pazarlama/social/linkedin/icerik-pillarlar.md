# LinkedIn — İçerik Sütunları ve Paylaşım Fikirleri

---

## İçerik sütunları

| # | Sütun | Amaç | Kaynak |
|---|-------|------|--------|
| 1 | Platform & omurga | Tek kimlik, veri, bildirim, scheduler | `Docs/modul-platform-omurgasi.md` |
| 2 | Modül spotu | Her modül 1 post (rotasyon) | `brosur/moduller/*.md` |
| 3 | On-prem & AI | Veri egemenliği, kapalı ağ, MngLLM | Landing `#yapay-zeka`, `#on-prem` |
| 4 | Sorun → çözüm | Müşteri dili, kısa hikâye | Broşür “tanıdık sorunlar” |
| 5 | Duyuru | Landing, özellik, etkinlik | Deploy / release notları |

---

## Paylaşım formatları

| Format | Ne zaman | Örnek |
|--------|----------|--------|
| Tek görsel + metin | Modül tanıtımı | Modül haritası kırpımı |
| Carousel (PDF/çoklu görsel) | Broşür özeti | 5 slayt: omurga + 4 modül |
| Kısa metin (link) | Duyuru | “www.monitrang.com yayında” |
| Alıntı / soru | Etkileşim | “On-prem AI sizin için öncelik mi?” |

---

## İlk 8 post takvimi (taslak)

| Hafta | Post | Başlık / hook | Görsel |
|-------|------|---------------|--------|
| 0 | Açılış | “MonitraNG LinkedIn'de” | Logo + banner |
| 1 | Platform | “Tek omurga, çok modül” | Modül bağlantı haritası |
| 1 | On-prem | “Veriniz kurumda kalır” | Landing on-prem bölümü özeti |
| 2 | DI | “Belge üretimi hub'ı” | DI broşür alıntısı |
| 2 | OC | “Operasyon tek ekranda” | OC kart metni |
| 3 | Monitoring + SIEM | “İzleme vs güvenlik” | Karşılaştırma tablosu (kısa) |
| 3 | AI | “Yerel model, buluta veri yok” | AI pillar metni |
| 4 | CTA | “Demo / görüşme” | İletişim + www linki |

---

## Modül post şablonu

```
[Modül adı] — [tek cümle değer önerisi]

Kimin için?
• [persona 1]
• [persona 2]

Ne sağlar?
• [yetenek 1]
• [yetenek 2]
• [yetenek 3]

MonitraNG modülleri bağımsız devreye alınabilir; aynı platform omurgasını paylaşır.

Detay: https://www.monitrang.com#modul-[kısa-ad]

#MonitraNG #[modülEtiketi]
```

**Modül anchor örnekleri (landing ile uyumlu):**
- `#modul-di`, `#modul-oc`, `#modul-reporting`, `#modul-monitoring`, `#modul-siem`, `#modul-workflow`

---

## Hashtag önerileri

**Marka:** `#MonitraNG`

**Genel (TR):** `#KurumsalYazılım` `#OnPrem` `#YapayZeka` `#SiberGüvenlik` `#DijitalDönüşüm` `#OperasyonelMükemmellik`

**Genel (EN):** `#EnterpriseSoftware` `#OnPremises` `#LocalAI` `#SIEM` `#BPM`

*Post başına 3–5 hashtag yeterli.*

---

## Yapılmaması gerekenler

- Müşteri adı / logo (yazılı onay olmadan)
- “%100 güvenli” gibi kanıtsız süperlatifler
- Henüz canlı olmayan modülü “şimdi kullanın” diye sunmak (Workflow yol haritası notu)
- `docs.monitrang.com` — site hazır değilse link verme (landing ile aynı politika)

---

## Görsel üretim notları

| Görsel | Boyut | Kaynak |
|--------|-------|--------|
| LinkedIn banner | 1584×396 | `Files/monitrang-modul-baglanti-haritasi.svg` + slogan |
| Post görseli | 1200×627 | Broşür tablosu veya modül kartı |
| Kare | 1080×1080 | `monitrang-logo-icon.png` + kısa metin |

Üretilen dosyalar: `social/assets/`
