# Raporlama — Modül özellik envanteri

**Kod:** `reporting` · **Durum:** Canlı (genişletme devam ediyor)  
**UI:** `/apps/reporting` · **Veri omurgası:** DataGateway (dataset sorguları) · **Belge:** Döküman Zekası entegrasyonu

**Referanslar:** [Reporting Faz 3 roadmap](../../monitrang/faz3/reporting/Roadmap.md) · [Reporting plan (iç)](../../odak/reporting_services/PLAN.md) · [Referans teklif §4.2 (iç)](../../odak/commercial/Odak_Kompozit_Fiyat_Teklifi.md)

> **Bu dosyanın amacı (şu an):** Raporlama modülünün **müşteri perspektifi**, ürün kimliği, temel kavramları ve **fonksiyon envanteri**. Broşür metinleri **henüz doldurulmayacak** — bkz. [§Broşür (ertelendi)](#broşür-ertelendi).

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi · 📋 Teklifte tanımlı, geliştirilmedi

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Raporlama**, kurum verisini — platform içi kayıtlar ve *(planlanan)* doğrulanmış dış kaynaklar — **parametreli tablo raporlarına** dönüştüren; filtreleme, özet, paylaşım, dışa aktarım ve gerektiğinde **resmi belge üretimi** ile karar vericiye güncel çıktı sunan modüldür.

### 1.2 İsim ve alternatif dil

| Bağlam | Önerilen ifade |
|--------|----------------|
| Platform / modül adı | **Raporlama** |
| Broşür, yönetim | «Veriden karar — **kurumsal rapor merkezi**» |
| Operasyon / IT | «Parametreli operasyon raporları» |
| Uyum / denetim | «Yetkilendirilmiş, izlenebilir rapor çıktıları» |
| Self-service | «Katalogdan rapor seç — filtrele — paylaş veya indir» |

### 1.3 Raporlama ne değildir?

| Beklenti | Raporlama gerçeği |
|----------|-------------------|
| BI suite (Power BI, Tableau tam ikamesi) | **Tablo odaklı** parametreli rapor motoru; ağır görsel analitik ayrı sınıf |
| OC panosu / günlük iş listesi | **Sorgu ve çıktı** — operasyon kaydı OC’de |
| DI wiki / Office editör | Rapor **veri yüzeyi**; resmi belge **DI şablonu** ile üretilir |
| Excel’in tam yerine geçen grid | Export var; **canlı rapor** platformda yetki ile çalışır |
| Erişilemeyen dış sistemden «her zaman veri» | **Erişim tanımlı ve doğrulanmış** kaynak kuralı — teklif zorunluluğu |

---

## 2. Müşteri perspektifi

> **Hedef kitle:** Satış, broşür, landing. Teknik terimler (aggregate pipeline, `documentBindings`…) burada **kullanılmaz**; bkz. §4–§5.

### 2.1 Tek paragraf (broşür / sunum)

**Raporlama**, dağınık Excel dosyaları ve elle çekilen listeler yerine kurumun verisini **tek katalogdan** sunar. Kullanıcı raporu seçer, dönem veya durum gibi **parametreleri** girer, tabloyu görür; satır detayına iner, gerekirse **Excel’e aktarır** veya **resmi belge** oluşturur. Raporlar **yetkiye** göre görünür; link ile paylaşılabilir veya kurum portalına **gömülebilir**. Platformdaki operasyon, eğitim, envanter veya *(planlanan)* dış sistem verisi aynı rapor altyapısından beslenir.

### 2.2 Günlük deneyim — müşteri ne görür?

| Adım | Müşteri dili |
|------|----------------|
| 1 | **Rapor kataloğundan** ihtiyacı olan listeyi bulur (kategori ağacı) |
| 2 | **Parametre girer** — yıl, durum, arama, tarih aralığı… |
| 3 | **Tablo görünür** — sıralama, sayfalama, sütun formatları |
| 4 | **Satıra tıklar** — detay alanları ve ilişkili alt listeler (expand) |
| 5 | **Başka rapora geçer** — sütundaki link ile (ör. eğitim → katılımcı detayı) |
| 6 | **Paylaşır veya gömer** — link kopyala, embed URL |
| 7 | **Dışa aktarır** — CSV / Excel, sütun seçimi |
| 8 | **Belge üretir** *(tanımlı raporlarda)* — satır veya liste bağlamında DI şablonu |

**Özet:** Raporlama = **«Doğru veriyi, doğru filtreye, doğru yetkiyle, doğru formatta»**.

### 2.3 MonitraNG içindeki yeri

| Modül | İlişki | Müşteri cümlesi |
|-------|--------|-----------------|
| **DataGateway** | Platform verisi kaynağı | «MonitraNG’deki kayıtlar rapora gelir» |
| **Operasyon Merkezi** | OC verisi raporlanabilir | «Açık iş emirleri listesi» |
| **Döküman Zekası** | Rapor → resmi belge | «Listeden sertifika / tutanak üret» |
| **Workflow** *(plan)* | Dış HTTP/DB köprüsü | «ERP verisi tek bağlantıdan rapora» |
| **Scheduler** *(plan)* | Otomatik rapor / dağıtım | «Ay sonu raporu e-postayla gitsin» |
| **Notifier (omurga)** | Rapor olayı bildirimi | «Rapor hazır / hata» bildirimi |

**OC ile sınır:** OC = **işi yürütmek**; Raporlama = **veriyi görmek ve dağıtmak**.

**DI ile sınır:** Rapor = **tablo/sorgu yüzeyi**; DI = **antetli Office belgesi** (rapor satırından tetiklenebilir).

### 2.4 Müşteriye net sınırlar

| Beklenti | Gerçek |
|----------|--------|
| «Her dış sistem otomatik bağlanır» | HTTP/DB kaynağı **tanımlı + test edilmiş** olmalı; erişim yoksa rapor yok |
| «Power BI dashboard» | Özet kart/footer var; **çoklu rapor dashboard** plan aşamasında |
| «Her rapor PDF» | Excel/CSV canlı; **PDF export** genişletme |
| «Sadece IT tasarlar» | Tasarım yetkisi rol bazlı; son kullanıcı **browse/runner** |

### 2.5 Pazarlama derinliği — ne anlatılır?

| Konu | Müşteriye | Detay envanteri |
|------|-----------|-----------------|
| Parametreli filtre | ✅ | §6.3 · §5.2 |
| Satır detayı (expand) | ✅ | §6.3 · §5.3 |
| Paylaşım / embed | ✅ | §5.4 |
| Export Excel/CSV | ✅ | §5.5 |
| Rapor → belge | ✅ (örnekli) | §5.6 |
| Dış HTTP/DB kaynak | 🔶 «Yol haritası / teklif kapsamı» | §5.1 |
| Otomatik zamanlanmış rapor | 🔶 «Planlanıyor» | §5.7 |
| Designer ekran adımları | ⏸️ Broşürde hayır | §5 · iç plan |

---

## 3. Amaç ve çözdüğü problem

### 3.1 Sorun

- Her departman **kendi Excel’ini** günceller — versiyon ve doğruluk sorunu
- IT’den «şu listeyi çeker misin?» — gecikme, tekrarlayan iş
- Platformda veri var ama **karar verici göremiyor**
- Dış ERP/DB verisi rapora **güvenli ve tekrarlanabilir** bağlanmıyor
- Tablo çıktısı resmi belgeye **manuel** kopyalanıyor

### 3.2 Amaç

1. **Tek rapor kataloğu** — kurumsal «hangi rapor nerede»
2. **Parametreli self-service** — yetkili kullanıcı kendi filtresiyle çalıştırır
3. **Yetkilendirilmiş görünürlük** — rapor / sütun / alt liste seviyesinde
4. **Paylaşılabilir çıktı** — link, embed, export, *(plan)* zamanlanmış dağıtım
5. **Belge köprüsü** — tablo verisinden DI ile resmi çıktı

---

## 4. Temel kavramlar

| Kavram | Kısa tanım |
|--------|------------|
| **Veri kaynağı** | Raporun beslendiği sistem — DG dataset *(canlı)*; HTTP / DB *(teklif + plan)* |
| **Rapor tanımı** | Dataset, kolonlar, parametreler, expand, özet, yetki, belge bağları |
| **Katalog / kategori** | Raporların ağaç halinde gruplanması (`@reporting_categories`, `@reporting_reports`) |
| **Designer** | Rapor tanımını oluşturan / düzenleyen yüzey |
| **Runner / Browse** | Son kullanıcının raporu çalıştırdığı yüzey |
| **Parametre** | Kullanıcı girdisi → filtre (yıl, durum, arama, tarih, seçim listesi…) |
| **Expand** | Satır detayı — alan paneli + **child sekmeleri** (ilişkili alt listeler) |
| **Özet (summary)** | Üst/alt aggregate kart veya footer (count, sum, …) |
| **Rapor linki** | Sütun değeri → başka rapora deep link (parametre taşır) |
| **Belge bağı (document binding)** | Rapor bağlamından DI şablonu ile belge üretimi |
| **Embed** | Salt-okunur gömülü rapor yüzeyi (`/apps/reporting/embed`) |

**Çalışma akışı (özet):**

```text
Katalog → parametreler → DG sorgu / aggregate
       → tablo + expand + özet
       → export | paylaşım | DI belge
```

---

## 5. Fonksiyon envanteri

### 5.1 Veri kaynakları

| Kaynak tipi | Açıklama | Durum | Not |
|-------------|----------|-------|-----|
| **MonitraNG / DG dataset** | Platform dataset’leri (`GET` / `POST …/query` / `aggregate`) | ✅ | Ana kaynak — OC, eğitim, envanter vb. |
| **HTTP endpoint** | Dış REST/HTTP sistemler | 📋 | Referans teklif §4.2.1; **RPT-1** plan |
| **Veritabanı sorgusu** | Dahili/harici DB | 📋 | Referans teklif §4.2.1; **RPT-1** plan |
| **Kaynak profili + bağlantı testi** | Erişim yoksa rapor yok | 📋 | Zorunlu teklif kuralı |
| **Workflow HTTP/DB köprüsü** | Orkestrasyon üzerinden dış veri | 🔲 | Platform vizyonu; Workflow plan |

### 5.2 Katalog ve rapor tanımı

| Yetenek | Durum | Not |
|---------|-------|-----|
| Kategori ağacı | ✅ | Browse sol panel |
| Merkezi katalog (DG `@reporting_*`) | ✅ | **R3a** — LS migrate |
| Rapor oluşturma / düzenleme (Designer) | ✅ | `/apps/reporting/designer` |
| Dataset seçimi + şema okuma | ✅ | Kolon üretimi |
| Kolon: etiket, format, genişlik, gizle | ✅ | |
| Sıralama, sayfalama | ✅ | |
| Varsayılan filtreler | ✅ | |
| Rapor görünürlük politikası (grup) | ✅ | |
| Named query (özel sorgu tanımı) | ⏸️ | **R3b** ertelendi |

### 5.3 Parametreler ve sorgu

| Yetenek | Durum | Not |
|---------|-------|-----|
| Parametre tipleri (metin, sayı, tarih, yıl, çeyrek, seçim…) | ✅ | |
| `orDateFields` — yıl/tarih alanı eşlemesi | ✅ | |
| Arama parametresi (metin → regex match) | ✅ | |
| Kişi / relation alanları | 🔶 | İyileştirme backlog |
| Bağımlı parametreler (cascade) | 🔲 | Parametre major |
| Dynamic Form parametre UI | 🔲 | Form benzeri yüzey |

### 5.4 Expand, özet ve linkleme

| Yetenek | Durum | Not |
|---------|-------|-----|
| Expand — alan detayı (`sections`) | ✅ | Designer |
| Expand — child liste sekmeleri (`tabs[]`) | ✅ | Runtime; Designer UI kısmen 🔶 |
| Child sekme / sütun yetkisi | ✅ | |
| Özet kart / footer (aggregate) | ✅ | POC — count/sum, metin araması |
| Sütun → rapor **deep link** (`reportLink`) | ✅ | Parametre taşıma |
| Browse ağaç + runner | ✅ | `/apps/reporting/browse` |
| Tek rapor runner | ✅ | `/apps/reporting/run/[id]` |

### 5.5 Görüntüleme, paylaşım, embed

| Yetenek | Durum | Not |
|---------|-------|-----|
| **Linki kopyala** (auth’lu URL + parametreler) | ✅ | Viewer 2 |
| **Embed** salt-okunur yüzey | ✅ | `showAdminTools=false` |
| Gömülü layout (`/apps/reporting/embed`) | ✅ | Blank layout |
| Browse — admin araçları gizli | ✅ | Son kullanıcı modu |

### 5.6 Dışa aktarım

| Yetenek | Durum | Not |
|---------|-------|-----|
| CSV export | ✅ | |
| Excel (xlsx) export | ✅ | **Export B1** |
| Sütun seçimi | ✅ | Dialog |
| Filtrelenmiş tüm satırlar (sayfa değil) | ✅ | Soft cap **5000** + onay |
| PDF export | 🔲 | **Export B2** |
| Özet satırı Excel’de | 🔲 | B2 |
| Child liste export | 🔲 | B2 |

### 5.7 Döküman Zekası entegrasyonu

| Yetenek | Durum | Not |
|---------|-------|-----|
| Rapor ↔ DI şablon bağı (`documentBindings`) | ✅ | **D0–D4** |
| Bağlam: `reportRun` / `parentRow` / `childRow` | ✅ | |
| Belge adı / `generatedAt` token kalıpları | ✅ | |
| Liste / satır / child satırından üretim | ✅ | |
| Klasör hedefi (rapor bazlı convention) | ✅ | |
| Raporlama → DI otomatik dağıtım zinciri | 🔲 | Workflow + Scheduler |

### 5.8 Yetkilendirme

| Seviye | Durum | Not |
|--------|-------|-----|
| Rapor görünürlüğü (grup politikası) | ✅ | |
| Sütun görünürlüğü (koşullu / her zaman) | ✅ | |
| Expand child sekme / sütun | ✅ | |
| Designer / katalog yazma | 🔶 | Manager rolü — cilası backlog |

### 5.9 Bildirimler ve otomasyon

| Yetenek | Durum | Not |
|---------|-------|-----|
| Rapor olayı bildirimi (hazır, hata, paylaşım…) | 📋 | Referans teklif §4.2.6 |
| In-app / e-posta / Telegram kanalları | 📋 | DI ile ortak Notifier modeli |
| Zamanlanmış rapor çalıştırma + e-posta | 🔲 | **Major** — Scheduler |
| Otomatik export eki | 🔲 | |

### 5.10 Dashboard ve ileri

| Yetenek | Durum | Not |
|---------|-------|-----|
| Çoklu rapor dashboard layout | 🔲 | **R4** |
| Rapor içi grafik / chart | 🔲 | Tablo odaklı ürün |
| DG query preview (geliştirici) | ✅ | Designer yardımcı |

---

## 6. Gerçek hayat örnekleri

### 6.1 Günlük senaryolar

| # | Senaryo | Rapor rolü |
|---|---------|--------------|
| 1 | IK yöneticisi yıllık eğitim listesi | Parametre: yıl + durum → tablo → Excel |
| 2 | Eğitim satırından katılımcı detayı | Expand child sekme |
| 3 | Katılımcı adına tık → personel eğitim geçmişi | Sütun rapor linki |
| 4 | Kalite müdürü aylık uygunsuzluk özeti | Özet kart + filtre |
| 5 | Denetçiye salt-okunur link | Paylaşım URL |
| 6 | Kurum intranet’ine gömülü liste | Embed |
| 7 | Katılım belgesi tek tık | DI şablon — satır bağlamı |
| 8 | Operasyon açık iş emirleri | OC dataset raporu *(workspace verisi)* |
| 9 | Ay sonu stok listesi e-postası | Zamanlanmış rapor *(plan)* |
| 10 | ERP sipariş bakiyesi | HTTP/DB kaynak *(teklif / plan)* |

### 6.2 Sektörel tablo

| Sektör | Örnek rapor | Kaynak |
|--------|-------------|--------|
| **Üretim** | İş emri durum listesi, kalite özeti | DG / OC |
| **Eğitim / IK** | Eğitim planı, katılımcı, sertifika listesi | DG |
| **Lojistik** | Sevkiyat istisnaları, envanter | DG · DB *(plan)* |
| **Bankacılık** | Operasyon olay özeti, uyum listesi | DG · SIEM verisi |
| **Kamu** | Başvuru istatistikleri | DG · dış portal *(plan)* |
| **Enerji** | Bakım iş emri, asset özeti | DG · Monitoring |

### 6.3 Müşteri / broşür cümleleri

- **«Excel dağınıklığına son»** — Tek katalog, güncel veri
- **«Parametreyi siz girin, listeyi sistem getirsin»** — Self-service
- **«Detaya bir tık»** — Expand ve raporlar arası link
- **«Yönetime link gönderin»** — Paylaşım ve embed
- **«Tablodan resmi belge»** — DI entegrasyonu
- **«Kim hangi sütunu görür — siz tanımlarsınız»** — Yetkilendirme
- **«Dış sistem verisi — tanımlı bağlantıyla»** — HTTP/DB *(teklif)*

---

## 7. Kimler kullanır?

| Rol | Kullanım |
|-----|----------|
| **Yönetim / karar verici** | Özet rapor, paylaşılan link, export |
| **Operasyon / IK / kalite** | Günlük liste, filtre, detay |
| **IT / veri sorumlusu** | Kaynak tanımı, designer, yetki |
| **Uyum / denetim** | Salt-okunur embed, export arşivi |
| **Son kullanıcı** | Browse — parametre + çalıştır |
| **MonitraNG kurulum** | Katalog, seed raporlar, DI şablon bağı |

---

## 8. Platform bağlantıları

| Modül | İlişki |
|-------|--------|
| **DataGateway** | Birincil veri kaynağı — query / aggregate |
| **Keeper** | Kimlik, grup — rapor yetkisi |
| **Döküman Zekası** | `documentBindings` — belge üretimi |
| **Operasyon Merkezi** | OC dataset’leri raporlanabilir |
| **Workflow** *(plan)* | Dış HTTP/DB orkestrasyon köprüsü |
| **Scheduler** *(plan)* | Periyodik rapor çalıştırma |
| **Notifier** | Rapor bildirimleri *(teklif)* |

**Tetik yönleri:**

```text
Kullanıcı (Browse)     ──► parametre → rapor tablosu
Scheduler (plan)       ──► rapor çalıştır → export / mail
Rapor satırı           ──► DI belge üret
Workflow (plan)        ──► dış veri → dataset → rapor
```

---

## 9. Referans teklif eşlemesi (iç kullanım)

| Referans paket (§4.2) | Bu doküman |
|-----------------------|------------|
| Veri kaynakları MNG / HTTP / DB | §5.1 |
| Erişim tanımı zorunlu | §5.1 · §2.4 |
| Katalog, analiz | §5.2 |
| Parametreli tablo, expand, özet, yetki | §5.2–5.4 · §5.8 |
| Designer / Runner | §5.2 · §4 |
| Browse, embed, paylaşım, rapor linki | §5.4–5.5 |
| CSV / Excel export | §5.6 |
| DI belge üretimi | §5.7 |
| Bildirimler (in-app, mail, Telegram) | §5.9 |
| Teslimat: katalog, tanımlar, yetki matrisi | §5 genel |

---

## 10. Teknik referans (iç kullanım)

| Alan | Konum |
|------|--------|
| UI | `Mng.Ui/pages/apps/reporting/` · `components/apps/reporting/` |
| Katalog DG | `@reporting_categories`, `@reporting_reports` |
| Utils | `Mng.Ui/utils/reporting*.ts` |
| Plan / devam | [PLAN.md](../../odak/reporting_services/PLAN.md) · [DEVAM.md](../../odak/reporting_services/DEVAM.md) |
| Faz 3 | [Roadmap.md](../../monitrang/faz3/reporting/Roadmap.md) |
| Dataset setup | `docs/odak/reporting_services/scripts/setup-reporting-catalog-datasets.ps1` |

---

## Broşür (ertelendi)

Landing / broşür metinleri özellik envanteri genişleyene kadar **doldurulmayacak**. Taslak: [platform-tanitimi.md § Raporlama](./platform-tanitimi.md)

---

## Görseller (bekleyen)

| Dosya | Açıklama |
|-------|----------|
| `../Files/rpt-ekran-browse.png` | Katalog + runner |
| `../Files/rpt-ekran-expand.png` | Satır expand + child sekme |
| `../Files/rpt-ekran-export.png` | Export dialog |
| `../Files/rpt-ekran-belge.png` | DI belge üretimi |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · Ürün kimliği v0.1*
