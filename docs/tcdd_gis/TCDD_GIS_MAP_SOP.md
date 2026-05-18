# TCDD GIS — Harita İşlemleri Standart İşlem Prosedürü (SOP)

**Doküman Kodu:** SOP-GIS-MAP-001  
**Versiyon:** 1.0  
**Tarih:** 2 Mart 2026  
**Referans:** TCDD Teknik Şartname, MonitraNG TCDD GIS MVP Planlama

---

## 1. Amaç

Bu prosedür, TCDD GIS MVP kapsamında harita bileşenlerinin kullanımı, veri girişi, simülasyon ve harita fonksiyonlarının standart işleyişini tanımlar.

---

## 2. Kapsam

- Harita bileşenleri (online/offline)
- Veri kaynağı seçimi (manuel giriş, simülatör)
- Harita üzerinde gösterim kuralları
- Dashboard ve raporlama akışları

**Kapsam dışı:** GeoServer, WMS/WFS entegrasyonu, PostGIS (ileride değerlendirilecek)

---

## 3. Tanımlar

| Terim | Açıklama |
|-------|----------|
| **Manuel giriş** | Kullanıcının form ekranlarından veri girdiği işlem |
| **Simülatör** | Sentetik veri üreten MngSim veya seed script |
| **Harita altlığı** | Arka plandaki harita katmanı (OSM, offline tiles) |
| **Katman** | Haritada açılıp kapatılabilen veri grubu (lokasyon, güzergâh, alarm) |
| **CBS editör** | Harita üzerinde nokta, çizgi, alan çizme aracı |

---

## 4. Sorumluluklar

| Rol | Sorumluluk |
|-----|------------|
| **Geliştirici** | Harita bileşenini online/offline destekleyecek şekilde tasarlamak |
| **Kullanıcı** | Lokasyon, güzergâh ve alarm tanımlarını girmek |
| **Sistem** | Simülatör verisini üretmek ve haritada göstermek |

---

## 5. Veri Kaynağı Seçim Prosedürü

### 5.1 Hangi Veri Nereden Gelir?

```
┌─────────────────────────────────────────────────────────────────┐
│                    VERİ KAYNAĞI KARAR AKIŞI                      │
└─────────────────────────────────────────────────────────────────┘

  [Veri ihtiyacı]
        │
        ├── Statik / referans veri mi? (lokasyon, güzergâh, varlık tipi)
        │   └──► MANUEL GİRİŞ (kayıt ekranları)
        │
        ├── Anlık / akış verisi mi? (konum, hız, yakıt, alarm)
        │   └──► SİMÜLATÖR (gerçek sistem yoksa)
        │       └── Gerçek sistem varsa → TİS/ATS/KKY API
        │
        └── Türetilmiş / hesaplanan mı? (yük yoğunluğu, ton-km)
            └──► RAPOR / ANALİZ (veri ambarı, ileride)
```

### 5.2 Prosedür Adımları

| Adım | İşlem | Çıktı |
|------|-------|-------|
| 1 | Veri türünü belirle (statik / akış / türetilmiş) | Karar |
| 2 | Statik ise → Manuel giriş ekranına git | Dataset kaydı |
| 3 | Akış ise ve gerçek sistem yok → Simülatörü çalıştır | Canlı veri |
| 4 | Akış ise ve gerçek sistem var → API entegrasyonu | Canlı veri |
| 5 | Türetilmiş ise → Raporlama modülünü kullan | Rapor |

---

## 6. Manuel Kayıt Giriş Prosedürü (Automated Forms)

**Not:** Tüm kayıt giriş ekranları Mng.Ui **Automated Forms** ile dinamik formlar olarak sunulur. Formlar `/apps/automated-forms/view/{formCode}` adresinde görüntülenir.

### 6.1 Lokasyon Girişi

| Adım | İşlem | Ekran/API |
|------|-------|-----------|
| 1 | `/apps/automated-forms/view/tcdd_gis_locations` sayfasına git | Automated Forms |
| 2 | "Yeni" / "Ekle" butonuna tıkla | Form dialog |
| 3 | Ad, açıklama, tür (istasyon/bölge vb.) gir | Form alanları |
| 4 | Konum: harita üzerinde tıklayarak VEYA lat/lon manuel gir | location alanı (object) |
| 5 | Kaydet | MngDataGateway `POST /data/tcdd_gis_locations` |

### 6.2 Güzergâh Girişi

| Adım | İşlem | Ekran/API |
|------|-------|-----------|
| 1 | `/apps/automated-forms/view/tcdd_gis_routes` sayfasına git | Automated Forms |
| 2 | "Yeni" / "Ekle" butonuna tıkla | Form dialog |
| 3 | Ad, açıklama gir | Form alanları |
| 4 | Harita üzerinde polyline çiz VEYA koordinat listesi import et | geometry alanı (object) |
| 5 | İstasyon sırasını tanımla (opsiyonel) | relation/liste |
| 6 | Kaydet | MngDataGateway `POST /data/tcdd_gis_routes` |

### 6.3 Alarm Eşiği Tanımı

| Adım | İşlem | Ekran/API |
|------|-------|-----------|
| 1 | `/apps/automated-forms/view/tcdd_gis_alerts_config` sayfasına git | Automated Forms |
| 2 | "Yeni" / "Ekle" butonuna tıkla | Form dialog |
| 3 | Alarm türü seç (hız ihlali, güzergâh ihlali) | Form alanları |
| 4 | Eşik değeri gir (örn. max hız km/saat) | Form alanları |
| 5 | İlgili güzergâh veya bölge seç | relation alanı |
| 6 | Kaydet | MngDataGateway `POST /data/tcdd_gis_alerts_config` |

---

## 7. Kural–Aksiyon (MngWorkflow) Prosedürü (Sonraki Faz)

**Not:** MVP'de simülatör kendi içinde alarm tetikleyebilir. MngWorkflow entegrasyonu sonraki fazda değerlendirilir.

| Adım | İşlem |
|------|-------|
| 1 | `tcdd_gis_alerts_config` içinde kural tanımla (hız eşiği, güzergâh, aksiyon türü) |
| 2 | Trip konum/hız verisi RabbitMQ'ya publish edildiğinde MngWorkflow queue'dan consume eder |
| 3 | Koşul eşleşirse aksiyon çalışır: bildirim, e-posta, UI uyarısı |

---

## 8. Simülatör Çalıştırma Prosedürü

### 8.1 Seed Script ile Statik Veri

| Adım | İşlem | Araç |
|------|-------|------|
| 1 | Örnek lokasyon, güzergâh, varlık verilerini hazırla (JSON/script) | - |
| 2 | MngDataGateway API veya seed script ile bulk insert yap | Script / API |
| 3 | Verilerin haritada göründüğünü doğrula | UI |

### 8.2 Konum Simülasyonu (MngSim veya özel sim)

| Adım | İşlem | Araç |
|------|-------|------|
| 1 | MngSim’i başlat (`dotnet run` veya Docker) | MngSim |
| 2 | Konum simülasyonu modunu etkinleştir | Config |
| 3 | Simülatör periyodik konum güncellemesi gönderir | MQTT/HTTP |
| 4 | MngHub veya MngDataGateway üzerinden UI’a ulaşır | Pipeline |
| 5 | Harita bileşeni anlık konumları gösterir | UI |

---

## 9. Harita Fonksiyonları Prosedürü

### 9.1 Harita Açma ve Altlık Seçimi

| Adım | İşlem |
|------|-------|
| 1 | Harita widget’ı veya harita sayfasını aç |
| 2 | Altlık: Online (varsayılan) veya Offline (yapılandırılmışsa) seç |
| 3 | Online: OSM veya diğer tile servisi yüklenir |
| 4 | Offline: Yerel tile dosyaları veya MBTiles kullanılır (henüz hazır değilse uyarı) |

**Online altlık seçenekleri:**

| Altlık | Açıklama |
|--------|----------|
| **OSM (varsayılan)** | OpenStreetMap — karayolları ve demiryolları (TCDD hatları dahil) birlikte gösterilir |
| **OpenRailwayMap** (opsiyonel overlay) | Demiryoluna özel: ana hat, şube, istasyon, elektriklenme, hız sınırı — katman seçiciden eklenebilir |

### 9.2 Katman Yönetimi

| Adım | İşlem |
|------|-------|
| 1 | Katman panelini aç (katman listesi) |
| 2 | Gösterilmek istenen katmanları işaretle: Lokasyon, Güzergâh, Alarm, Anlık konum, OpenRailwayMap (opsiyonel) |
| 3 | Katman sırasını sürükle-bırak ile değiştir (opsiyonel) |
| 4 | Zoom seviyesine göre görünürlük ayarla (ileride) |

### 9.3 Harita Üzerinde Etkileşim

| İşlem | Kullanıcı aksiyonu | Sistem tepkisi |
|-------|-------------------|----------------|
| Marker tıklama | Lokasyon/alarm marker’ına tıkla | Popup açılır, detay gösterilir |
| Güzergâh seçimi | Polyline’a tıkla | Güzergâh bilgisi, ilişkili istasyonlar |
| Koordinat öğrenme | Haritada noktaya tıkla | Koordinat (lat, lon) gösterilir |
| CBS editör | "Çizim" modunu aç, nokta/çizgi çiz | Yeni lokasyon veya güzergâh taslağı oluşturulur |

### 9.4 Online / Offline Geçiş

| Adım | İşlem |
|------|-------|
| 1 | Ayarlar veya harita araç çubuğundan "Altlık" seç |
| 2 | "Online" veya "Offline" seç |
| 3 | Online: Tile URL’leri kullanılır |
| 4 | Offline: Yerel tile kaynağı kullanılır (hazırsa) |
| 5 | Offline kaynak yoksa kullanıcıya bilgi verilir |

---

## 10. Dashboard ve Raporlama Prosedürü

### 10.1 Dashboard Oluşturma

| Adım | İşlem |
|------|-------|
| 1 | Dashboard sayfasına git |
| 2 | "Widget ekle" → "Harita" seç |
| 3 | Harita widget’ına veri kaynağı bağla (lokasyon, güzergâh, anlık konum) |
| 4 | Diğer widget’ları ekle (KPI, alarm listesi) |
| 5 | Harita–widget etkileşimini ayarla: haritada seçim → filtre |

### 10.2 Harita Tabanlı Rapor Alma

| Adım | İşlem |
|------|-------|
| 1 | Raporlama modülüne git |
| 2 | Rapor türü seç (lokasyon listesi, güzergâh raporu, konum geçmişi) |
| 3 | Tarih, filtre parametrelerini gir |
| 4 | "Oluştur" veya "Görüntüle" tıkla |
| 5 | Harita + tablo çıktısı al; PDF/Excel’e aktar (destekleniyorsa) |

---

## 11. Hata ve İstisna Durumları

| Durum | Tepki |
|-------|-------|
| Offline altlık yok | "Offline harita henüz yüklenmemiş" mesajı; Online kullan |
| Simülatör bağlantı hatası | "Simülatör erişilemiyor" uyarısı; manuel veri ile devam |
| Geçersiz koordinat | Form validasyonu; kullanıcıyı düzeltmeye yönlendir |
| Harita yüklenmiyor | Ağ hatası kontrolü; tile URL’lerini doğrula |

---

## 12. Referanslar

- [TCDD GIS SOW](./TCDD_GIS_SOW.md) — Scope of Work
- [TCDD GIS MVP Planlama](./TCDD_GIS_MVP_PLANNING.md)
- [TCDD GIS Şartname Uyum Matrisi](./TCDD_GIS_SARTNAME_UYUM.md) — Şartname maddeleri karşılama durumu
- [TCDD Teknik Şartname](./teknik_sartname.pdf)
- [MngDataGateway API](../../MngDataGateway/README.md)
- [MngSim README](../../MngSim/README.md)
- [MngWorkflow Planı](../../content/monitoring_plans/MONITORING_WORKFLOW.md)

---

## 13. Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik | Hazırlayan |
|----------|-------|------------|------------|
| 1.0 | 2 Mart 2026 | İlk yayın | - |
