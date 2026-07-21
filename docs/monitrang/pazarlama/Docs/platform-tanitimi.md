# MonitraNG Platform Tanıtımı

MonitraNG, kurumların operasyon, içerik, güvenlik ve karar süreçlerini tek çatı altında birleştiren modüler bir kurumsal platformdur. Savunma sanayisinde doğrulanmış altyapımız; bankacılık, lojistik, üretim ve hizmet sektörlerinde aynı omurgayla kullanılabilir.

Dağınık dosyalar, e-posta ile yürüyen süreçler, geç fark edilen alarmlar ve birbirinden kopuk raporlar — MonitraNG bu parçaları birbirine bağlayan ortak bir katman sunar. Modüller birlikte çalıştığında veri, süreç ve güvenlik aynı platformda buluşur; ihtiyaca göre yalnızca ilgili modüller de devreye alınabilir.

Bu döküman, platform modüllerinin kısa tanıtımını içerir. Her bölüm ileride ekran görüntüleri ve sektörel örneklerle genişletilecektir.

**Modül kaynak kitaplığı:** Detaylı modül dosyaları ve yazım şablonu için [moduller-index.md](./moduller-index.md) ve [modul-sablon.md](./modul-sablon.md) kullanılır.

### Modül bağlantı haritası

![MonitraNG modül bağlantı haritası](../Files/monitrang-modul-baglanti-haritasi.svg)

**Görsel dosya:** `Pazarlama/Files/monitrang-modul-baglanti-haritasi.svg`

- **Merkez — Platform omurgası:** Kimlik (Keeper), veri (DataGateway), bildirim (Notifier) + **Zamanlayıcı (Scheduler)** rozeti
- **Sol panel — Tetik kaynakları:** Aksiyonun nasıl başladığı (zamanlama / modül olayı / dış HTTP flow)
- **Kehribar kesik oklar:** Zamanlama → modül aksiyonu (belge, WorkItem, izleme/WF adımı…)
- **Mavi kesik oklar:** Modül olayı → **Döküman Zekası** (belge)
- **Mor kesik oklar:** Modül olayı → **Workflow** (orkestrasyon adımı)
- **Yeşil ok:** Dış tüketici → Workflow **HTTP flow endpoint** (SDK yok)
- **Gri düz oklar:** Omurga bağlantısı
- **Mor kesik çerçeve:** Workflow *(yol haritası)*

**SVG’de yönetim ilkesi:** Diyagram *hub’ları* (DI, WF), *tetik kaynaklarını* (sol panel + çizgi renkleri) ve modül *hedeflerini* (kart altı pill) ayırır. Modül detayları ayrı bölümlerde anlatılır; harita mimariyi özetler.

**İki hub + zamanlama**

| Katman | Rol |
|--------|-----|
| **Döküman Zekası** | Belge üretim hedefi (tetik: olay veya zamanlama) |
| **Workflow** | Orkestrasyon + dış HTTP flow kapısı |
| **Scheduler** | Platform omurgasında; periyot/saat ile tüm modüllerde aksiyon tetikler |

**Bağlantı ilkesi (Workflow / dış):** SDK yok; dış taraf tanımlı **HTTP flow endpoint**’lerine başvurur.

**Bağlantı ilkesi (Zamanlama):** Belirli saatte/periyotta DI belge, OC WorkItem, Monitoring veya WF adımı çalıştırılabilir — tek scheduler, çok modül hedefi.

| Tetik | Örnek sonuç |
|-------|-------------|
| Zamanlama | Ay sonu DI raporu; sabah OC WorkItem; gece monitoring taraması |
| Modül olayı | Alarm → WF adımı; süreç kapanışı → DI belge |
| Dış HTTP flow | Partner sistemi → WF → platform verisi/işlemi |

---

## Modül haritası

| Modül | Kısa tanım |
|-------|------------|
| Döküman Zekası | Kurumsal içerik ve belge üretimi |
| Operasyon Merkezi (OC) | Süreç, görev, operasyon kayıtları ve denetim izi |
| Raporlama | Veriden karar — rapor ve analiz |
| Monitoring | Operasyonel metrik ve varlık izleme |
| Güvenlik Merkezi (SIEM) | Güvenlik olayı ve alarm yönetimi |
| Workflow | Orkestrasyon ve dış HTTP flow |

**Platform veri yüzeyleri** *(ayrı modül kartı değil):* [Dinamik Form & Widget / Dashboard](./modul-dinamik-form-ve-dashboard.md) — schema tabanlı form ve panel motoru; OC, Raporlama ve modül dashboard’larında ortak kullanılır.

---

## Döküman Zekası

Kurumların Word, Excel ve sunum dosyalarını merkezi platformda barındırmasını, tarayıcıdan düzenlemesini ve gerektiğinde şablon + veri ile otomatik belge üretmesini sağlar. **Platformun belge üretim hub’ıdır:** diğer modüller (OC, Raporlama, Monitoring, SIEM, Workflow) ihtiyaç halinde DI üzerinden resmi çıktı oluşturabilir.

**Detaylı modül kaynağı:** [modul-document-intelligence.md](./modul-document-intelligence.md)

**Öne çıkan yetenekler**

- Merkezi içerik ağacı (sayfalar, dökümanlar, klasör yapısı)
- Tarayıcı tabanlı düzenleme (Collabora / Managed Office)
- Belge tasarımcısı — şablon, antet, otomatik üretim
- Yetki ve sürüm geçmişi

*[Ekran: Döküman Zekası — kaynak ağacı ve içerik editörü]*

---

## Operasyon Merkezi (OC)

Operasyonel ve **kurumsal süreçleri** workspace ve WorkItem modeli üzerinden yönetir. Modül adı «Operasyon Merkezi»dir; broşür ve departman içi iletişimde **süreç yönetimi**, **iş akışı** veya **görev merkezi** ifadeleriyle de anlatılabilir — IT helpdesk’ten üretim emrine, onay kuyruğundan bakım iş emrine aynı motor farklı workspace’lerle kurulur.

**Detaylı modül kaynağı:** [modul-operation-core.md](./modul-operation-core.md)

**Öne çıkan yetenekler**

- Workspace tabanlı süreç organizasyonu (çoklu domain aynı tenant’ta)
- WorkItem — görev, olay, emir, onay kayıtları
- Durum akışı, dinamik form, kurallar
- Timeline / denetim izi
- DI, Monitoring, Scheduler ve Workflow entegrasyon potansiyeli

*[Ekran: Operasyon Merkezi — dashboard ve WorkItem detay]*

---

## Raporlama

Platform verisinden ve *(planlanan)* doğrulanmış dış kaynaklardan **parametreli tablo raporları** üretir. Katalog, filtre, satır detayı, paylaşım, dışa aktarım ve gerektiğinde **Döküman Zekası ile resmi belge** — karar vericiye güncel, yetkilendirilmiş çıktılar sunar.

**Detaylı modül kaynağı:** [modul-reporting.md](./modul-reporting.md)

**Öne çıkan yetenekler**

- Rapor kataloğu ve kategori ağacı
- Parametreli tablo, expand (detay + alt listeler), özet kartları
- Yetki: rapor, sütun ve alt görünüm seviyesinde
- Paylaşım linki, embed, raporlar arası deep link
- CSV / Excel export; DI şablonundan belge üretimi
- MonitraNG dataset kaynağı *(canlı)*; HTTP/DB kaynak profili *(teklif / yol haritası)*

*[Ekran: Raporlama — katalog ve runner]*

---

## Monitoring

Sunucu, servis, veritabanı, ağ ve saha cihazlarından **operasyonel metrik** toplar. Asset envanteri, engine/agent modeli, dashboard widget’ları, eşik alarm ve bildirimlerle altyapı ve üretim sağlığı izlenir. **Güvenlik log analizi bu modülde değildir** — bkz. Güvenlik Merkezi.

**Detaylı modül kaynağı:** [modul-monitoring.md](./modul-monitoring.md)

**Öne çıkan yetenekler**

- Asset / organizasyon / engine / agent envanteri
- Kontrol merkezi, harita ve widget dashboard’ları
- Eşik alarm ve bildirim *(teklif kapsamı genişliyor)*
- Operasyon Merkezi üretim köprüsü *(referans senaryo)*
- Monitoring AI: anomaly, açıklama, trend *(teklif / plan)*

*[Ekran: Monitoring — kontrol merkezi ve harita]*

> **Not:** Operasyonel sağlık = Monitoring. Güvenlik olayları = [Güvenlik Merkezi (SIEM)](./modul-siem-center.md).

---

## Güvenlik Merkezi (SIEM)

Kurumsal **güvenlik olaylarını** merkezileştirir (SIEM-hafif). Log ingest, parser, hedefli alarm kuralları; **Alarm Merkezi** ile operatör kuyruğu; güvenlik paneli ve olay arama.

**Detaylı modül kaynağı:** [modul-siem-center.md](./modul-siem-center.md)

**Öne çıkan yetenekler**

- Güvenlik olayı toplama ve arama (`sec_events`)
- Alarm kuralları (threshold, correlation, sequence…)
- Alarm Merkezi — lifecycle (onayla, bastır, çöz)
- Güvenlik paneli (dashboard)
- Operasyon Merkezi / Workflow entegrasyon potansiyeli

*[Ekran: Güvenlik Merkezi — panel ve olay arama]*

---

## Workflow

Platformun **orkestrasyon katmanı** ve **dışa açılan kapılardır**: partner sistemler **HTTP flow** endpoint’lerine başvurur; müşteri ve saha personeli **WhatsApp / Telegram** üzerinden **Kanal Akışları** ile self-servis diyalog başlatabilir *(plan)*. Çok adımlı onay, gecikme, HTTP, operasyon kaydı ve belge üretimini tek zincirde birleştirir. **SDK yok.**

**Detaylı modül kaynağı:** [modul-workflow.md](./modul-workflow.md) *(§7 Kanal Akışları)* · **Omurga:** [modul-platform-omurgasi.md](./modul-platform-omurgasi.md) · **Zamanlama:** [modul-scheduler.md](./modul-scheduler.md)

**Öne çıkan yetenekler**

- Versiyonlu workflow tanımı ve çalıştırma geçmişi
- Event trigger (OC, alarm…) · schedule trigger (Scheduler)
- Onay bekleme, gecikme, HTTP, WorkItem adımları
- **Kanal Akışları** — mesajla diyalog, veri/kimlik flow’da *(WhatsApp, Telegram — plan)*
- Otomasyon Merkezi admin UI *(form editör canlı; canvas genişletme devam)*
- OC / Alarm / DI ile net sınır — tek adım OC, çok adım Workflow

**Dış entegrasyon modeli**

```text
Dış sistem / ortak  ──HTTP──►  Workflow flow endpoint  ──►  Platform verisi / işlem
WhatsApp / Telegram ──►  Kanal kapısı  ──►  Workflow (channel adımları)  ──►  aynı zincir
                                    │
                                    └──► Raporlama, DI, OC, …
```

*[Ekran: Otomasyon Merkezi — workflow listesi]*

---

## Dinamik Form & Widget / Dashboard

Veriyi **kod yazmadan ekrana taşıyan** platform yetenekleri: **Dinamik Form** giriş ve listeleri, **Widget / Dashboard** özet panelleri sağlar. Ayrı satılabilir modül değil; OC, Raporlama, SIEM ve müşteri vertical uygulamalarında **ortak motor**dur.

**Detaylı kaynak:** [modul-dinamik-form-ve-dashboard.md](./modul-dinamik-form-ve-dashboard.md)

**Öne çıkan yetenekler**

- Dataset şemasından **otomatik form** (Automated Forms) — menüye bağlanabilir CRUD ekranı
- **Süreç formu** (OC) — geçiş kuralları, katmanlı alan politikası, kişi/grup seçiciler
- **Rapor parametre paneli** — tarih, kişi, durum filtreleri
- **Widget katalogu** — Alarm, SIEM, OC, DI domain şablonları
- **Dashboard designer** — widget’ları panelde birleştirme; drill-down ve yenileme

*[Ekran: Dinamik form listesi · widget dashboard builder]*

---

## Neden MonitraNG?

- **Modüler:** İhtiyaç duyulan modüller devreye alınır; tam platform birlikte de kullanılabilir.
- **Tek omurga:** Kimlik, yetki, veri ve bildirim altyapısı tüm modüllerde ortaktır — bkz. [modul-platform-omurgasi.md](./modul-platform-omurgasi.md) · [modul-scheduler.md](./modul-scheduler.md)
- **Veri yüzeyleri:** Form ve panel **schema tabanlı** — yeni ekran için uzun geliştirme döngüsü gerekmez — bkz. [modul-dinamik-form-ve-dashboard.md](./modul-dinamik-form-ve-dashboard.md)
- **Kanıtlanmış:** Savunma sanayisinde üretim ortamında doğrulanmış referanslar.
- **Genişletilebilir:** Bankacılık, lojistik, üretim ve hizmet sektörlerine uyarlanabilir.

---

## Sonraki adımlar

- [x] Broşür markdown + DI seed v1.0 — `brosur/` · MNG-STD antet
- [ ] Modül bölümlerine ekran görüntüleri ekleme
- [ ] Sektörel kullanım örnekleri (banka, lojistik, üretim)
- [ ] Broşür PDF birleşik çıktı (MNG-STD antet ile native DOCX)

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama*
