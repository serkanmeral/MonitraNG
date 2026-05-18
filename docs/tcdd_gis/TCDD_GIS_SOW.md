# TCDD GIS MVP — Scope of Work (SOW)

**Doküman Kodu:** SOW-GIS-001  
**Versiyon:** 1.0  
**Tarih:** 2 Mart 2026  
**Referans:** TCDD Teknik Şartname (Kaynak Planlama ve Büyük Veri Analitiği Platformu), MonitraNG Platformu

**Dataset prefix kuralı:** Tüm TCDD GIS dataset'leri `tcdd_gis_` prefix'i ile oluşturulacaktır.

**Form yaklaşımı:** Kayıt giriş ekranları Mng.Ui **Automated Forms** ile dinamik formlar olarak oluşturulacaktır.

---

## 1. Proje Bilgileri

| Alan | Değer |
|------|------|
| **Proje adı** | TCDD GIS MVP — Harita Tabanlı İzleme ve Coğrafi Bilgi Sistemi |
| **Kapsam tipi** | MVP (Minimum Viable Product) |
| **Platform** | MonitraNG (MngDataGateway, Mng.Ui, MngSim, MngHub) |
| **Referans doküman** | TCDD Teknik Şartname (4.1.33, 4.5 maddeleri) |

---

## 2. Proje Amacı ve Hedefler

### 2.1 Amaç

TCDD teknik şartnamesinde tanımlanan harita tabanlı izleme ve coğrafi bilgi sistemi ihtiyaçlarının, MonitraNG platformu bileşenleri kullanılarak MVP düzeyinde karşılanması.

### 2.2 Hedefler

1. **Harita bileşenleri:** Online ve offline altlık destekli harita altyapısı
2. **Veri tanımlama:** Lokasyon, güzergâh, varlık ve alarm eşiği giriş ekranları
3. **Dashboard:** Harita widget’ları ve harita–veri etkileşimi
4. **Raporlama:** Lokasyon, güzergâh ve konum geçmişi raporları
5. **Simülasyon:** Gerçek sistem olmadan test için sentetik veri üretimi

---

## 3. Dahil Olanlar (In Scope)

### 3.1 Faz 1 — Harita Altyapısı

| # | Teslimat | Açıklama | Kabul Kriteri |
|---|----------|----------|---------------|
| 1.1 | Harita bileşeni | OpenLayers veya Leaflet tabanlı, online OSM altlık (karayolu + demiryolu verisi) | Harita ekranda görüntülenir, zoom/pan çalışır |
| 1.2 | Online/offline mimari | Tile provider abstraction; offline interface hazır | Runtime’da online/offline mod seçilebilir (offline altlık sağlandığında) |
| 1.3 | Lokasyon marker gösterimi | Point marker’lar, popup detay | Lokasyonlar haritada marker olarak görünür |
| 1.4 | Güzergâh polyline gösterimi | GeoJSON LineString çizimi | Güzergâhlar haritada çizgi olarak görünür |
| 1.5 | Katman yönetimi | Lokasyon, güzergâh, alarm katmanları; opsiyonel OpenRailwayMap overlay | Katman paneli ile katmanlar kontrol edilebilir |
| 1.6 | Koordinat gösterimi | İmlecin bulunduğu nokta (lat, lon) | Harita üzerinde koordinat bilgisi görünür |

### 3.2 Faz 2 — Veri Tanımlama ve Kayıt Ekranları

| # | Teslimat | Açıklama | Kabul Kriteri |
|---|----------|----------|---------------|
| 2.1 | `tcdd_gis_locations` dataset | İstasyon, bölge, lokasyon tanımları | MngDataGateway üzerinden CRUD API |
| 2.2 | Lokasyon Automated Form | Automated Forms + harita/koordinat alanı | `tcdd_gis_locations` formu ile CRUD |
| 2.3 | `tcdd_gis_routes` dataset | Güzergâh/hat tanımları (GeoJSON LineString) | CRUD API |
| 2.4 | Güzergâh Automated Form | Automated Forms + CBS editör veya koordinat import | `tcdd_gis_routes` formu ile CRUD |
| 2.5 | `tcdd_gis_alerts_config` dataset | Alarm eşikleri (hız, güzergâh ihlali) | CRUD API |
| 2.6 | Alarm eşiği Automated Form | Automated Forms, eşik değeri, güzergâh relation | `tcdd_gis_alerts_config` formu ile CRUD |
| 2.7 | Varlık/asset tanımları | Mevcut mon_assets genişletmesi veya tcdd_gis_assets | Varlıklar lokasyon ile ilişkilendirilebilir |

### 3.3 Faz 3 — Dashboard

| # | Teslimat | Açıklama | Kabul Kriteri |
|---|----------|----------|---------------|
| 3.1 | Harita widget | Dashboard’a harita bileşeni eklenebilir | Harita widget seçilip panele eklenebilir |
| 3.2 | Harita–widget etkileşimi | Haritada seçim → diğer widget’lar güncellenir | Seçim filtreleme çalışır |
| 3.3 | KPI widget’ları | Toplam lokasyon, varlık, alarm sayısı (basit) | KPI değerleri görüntülenir |
| 3.4 | Alarm listesi widget | Son alarmlar listesi, haritada tıklanınca odaklanma | Alarm listeden haritada gösterilebilir |

### 3.4 Faz 4 — Simülasyon

| # | Teslimat | Açıklama | Kabul Kriteri |
|---|----------|----------|---------------|
| 4.1 | Seed script veya toplu veri | Örnek lokasyon, güzergâh, varlık verileri | Örnek veriler sisteme yüklenebilir |
| 4.2 | Konum simülasyonu | MngSim genişletmesi veya ayrı sim — anlık konum üretimi | Haritada hareketli konum gösterilebilir |
| 4.3 | Alarm simülasyonu | Hız/güzergâh ihlali tetikleme | Simülasyon ile alarm üretilip haritada gösterilebilir |

### 3.5 Faz 5 — Raporlama

| # | Teslimat | Açıklama | Kabul Kriteri |
|---|----------|----------|---------------|
| 5.1 | Lokasyon listesi raporu | Tablo, filtreleme | Lokasyonlar listelenebilir |
| 5.2 | Güzergâh raporu | Harita + tablo | Güzergâhlar harita ve liste olarak görüntülenebilir |
| 5.3 | Konum geçmişi raporu | Zaman serisi, harita üzerinde iz | Konum geçmişi görüntülenebilir |
| 5.4 | Alarm özeti raporu | Tablo, dışa aktarma (Excel/PDF — destekleniyorsa) | Alarmlar listelenebilir, dışa aktarılabilir |

---

## 4. Dahil Olmayanlar (Out of Scope)

| # | Öğe | Açıklama |
|---|-----|----------|
| 1 | TİS/ATS/KKY gerçek entegrasyonu | Gerçek tren işletme ve araç takip sistemlerine bağlantı |
| 2 | GeoServer / WMS / WFS | OGC standart harita servisleri |
| 3 | PostGIS | Ayrı coğrafi veritabanı; MongoDB kullanılacak |
| 4 | Offline harita altlık dosyası | Mimari destekler; tile dosyası sağlama Müşteri/İdare sorumluluğunda |
| 5 | Antetli harita çıktısı (A0–A4 PDF) | Sonraki faz |
| 6 | Heatmap / yük yoğunluğu haritası | Sonraki faz |
| 7 | Mobil native uygulama | Web tabanlı (responsive) kapsam dahilinde |
| 8 | Çoklu dil desteği (GIS modülü) | Platform genelinde varsa kullanılır; GIS’e özel geliştirme yok |
| 9 | Eğitim ve kullanıcı dokümantasyonu | Ayrı kapsam; teknik dokümanlar dahil |
| 10 | MngWorkflow entegrasyonu | Kural–aksiyon (tcdd_gis_alerts_config → tetikleme) sonraki fazda değerlendirilir |

---

## 5. Teslimat Özeti

| Faz | Teslimat Sayısı | Özet |
|-----|-----------------|------|
| Faz 1 | 6 | Harita altyapısı, marker, polyline, katman, koordinat |
| Faz 2 | 7 | Dataset’ler, lokasyon/güzergâh/alarm ekranları |
| Faz 3 | 4 | Dashboard, harita widget, etkileşim |
| Faz 4 | 3 | Simülasyon, seed veri, konum/alarm sim |
| Faz 5 | 4 | Raporlar (lokasyon, güzergâh, konum geçmişi, alarm) |
| **Toplam** | **24** | |

---

## 6. Varsayımlar

| # | Varsayım |
|---|----------|
| 1 | MonitraNG platformu (MngDataGateway, Mng.Ui, MngHub, MngSim) çalışır durumda ve erişilebilir olacaktır |
| 2 | Müşteri/İdare, gerekli lokasyon ve güzergâh referans verilerini (isteğe bağlı) sağlayacak veya seed veri kullanılacaktır |
| 3 | Harita altlıkları için online OSM kullanılır; karayolu ve demiryolu verisi OSM'de mevcuttur. OpenRailwayMap opsiyonel overlay olarak eklenebilir. Lisans maliyeti Yüklenici tarafından karşılanacaktır |
| 4 | Gerçek TİS/ATS/KKY entegrasyonu bu MVP kapsamı dışındadır; simülasyon ile demonstrasyon yapılacaktır |
| 5 | Offline harita tile’ları müşteri tarafından sağlanmazsa, yalnızca online mod kullanılacaktır |
| 6 | Mevcut MngKeeper yetkilendirme yapısı kullanılacak; GIS’e özel ek rol tanımları analiz aşamasında belirlenebilir |

---

## 7. Müşteri/İdare Bağımlılıkları

| # | Bağımlılık | Açıklama |
|---|------------|----------|
| 1 | Platform erişimi | Test ve geliştirme ortamına erişim |
| 2 | Onaylar | Faz bazlı teknik onay ve kabul süreçleri |
| 3 | Referans veri (opsiyonel) | Örnek istasyon, hat, güzergâh verileri |
| 4 | Offline tile (opsiyonel) | Offline kullanım istenirse tile dosyası temini |

---

## 8. Kabul Prosedürü

| Adım | İşlem |
|------|-------|
| 1 | Yüklenici, her faz tamamlandığında teslimatları sunar |
| 2 | İdare/Müşteri, bu SOW’daki kabul kriterlerine göre test eder |
| 3 | Eksiklik varsa yazılı geri bildirim verilir; düzeltme süresi mutabık kalınır |
| 4 | Kabul kriterleri karşılandığında faz onaylanır |
| 5 | Tüm fazlar kabul edildiğinde MVP geçici kabul edilir |

---

## 9. Değişiklik Yönetimi

- Kapsam değişikliği talepleri yazılı olarak iletilir
- SOW değişikliği, tarafların mutabakatı ile yapılır
- Ek teslimatlar veya çıkarılan maddeler revize SOW’a yansıtılır

---

## 10. İlgili Belgeler

| Belge | Açıklama |
|-------|----------|
| [TCDD GIS MVP Planlama](./TCDD_GIS_MVP_PLANNING.md) | Teknik planlama, veri matrisi, mimari |
| [TCDD GIS SOP](./TCDD_GIS_MAP_SOP.md) | Standart işlem prosedürleri |
| [TCDD GIS Şartname Uyum Matrisi](./TCDD_GIS_SARTNAME_UYUM.md) | Hangi şartname maddelerinin karşılandığı / karşılanamadığı |
| [TCDD Teknik Şartname](./teknik_sartname.pdf) | Referans şartname |

---

## 11. Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik | Onay |
|----------|-------|------------|------|
| 1.0 | 2 Mart 2026 | İlk yayın | - |
