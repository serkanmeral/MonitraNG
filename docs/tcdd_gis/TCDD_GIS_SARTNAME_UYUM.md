# TCDD GIS MVP — Şartname Maddeleri Uyum Matrisi

**Doküman Kodu:** SARTNAME-UYUM-001  
**Versiyon:** 1.0  
**Tarih:** 2 Mart 2026  
**Referans:** [TCDD Teknik Şartname](./teknik_sartname.pdf) (Kaynak Planlama ve Büyük Veri Analitiği Platformu)

**Önemli not:** Bu matris, planlarımızın şartname maddelerini ne ölçüde karşıladığını özetler. Şartname maddelerinin **tam metni** `teknik_sartname.pdf` dosyasından alınmalıdır. Madde numaraları ve özetleri, şartname incelendiğinde güncellenebilir.

---

## 1. Amaç

Bu doküman:
- TCDD teknik şartnamesindeki harita ve CBS ile ilgili maddeleri listeler
- MVP planlarımızın hangi maddeleri karşıladığını gösterir
- Karşılanamayan veya kısmen karşılanan maddeleri belirtir

---

## 2. Referans Madde Grupları

SOW'da belirtilen ana referanslar:
- **4.1.33** — Muhtemelen harita tabanlı izleme / CBS fonksiyonları
- **4.5** — Muhtemelen ek teknik gereksinimler veya entegrasyon

*Şartname sayfa numaraları ve tam madde metinleri PDF'ten kontrol edilmelidir.*

---

## 3. Karşılanan Maddeler (In Scope — MVP ile Karşılanıyor)

Aşağıdaki tablo, planlarımız kapsamında karşılanması hedeflenen şartname taleplerini ve MVP karşılıklarını gösterir.

| Madde (Önerilen) | Şartname Talebi (Özet) | Uyum | MVP Karşılığı |
|------------------|------------------------|------|---------------|
| 4.1.33.x | Harita görüntüleme, zoom/pan | ✅ Karşılanıyor | Leaflet/OpenLayers, OSM altlık, karayolu + demiryolu verisi |
| 4.1.33.x | Lokasyon / istasyon gösterimi | ✅ Karşılanıyor | `tcdd_gis_locations` marker, popup, hiyerarşi |
| 4.1.33.x | Güzergâh / hat çizimi | ✅ Karşılanıyor | `tcdd_gis_routes` GeoJSON LineString, polyline |
| 4.1.33.x | Veri giriş ekranları (lokasyon, güzergâh, varlık) | ✅ Karşılanıyor | Automated Forms + MngDataGateway dataset CRUD |
| 4.1.33.x | Harita–veri etkileşimi | ✅ Karşılanıyor | Haritada seçim → widget filtreleme; alarm listeden haritada odaklanma |
| 4.1.33.x | Dashboard üzerinde harita | ✅ Karşılanıyor | Harita widget, KPI widget’ları, alarm listesi widget |
| 4.1.33.x | Raporlama (lokasyon, güzergâh, konum geçmişi, alarm) | ✅ Karşılanıyor | Faz 5: Tablo + harita görünümü, dışa aktarma |
| 4.1.33.x | Anlık konum gösterimi | ✅ Karşılanıyor | Simülatör ile hareketli marker; MngHub SignalR (ileride gerçek veri) |
| 4.1.33.x | Alarm / olay haritada gösterimi | ✅ Karşılanıyor | Hız/güzergâh ihlali alarmları, renkli marker |
| 4.1.33.x | Online harita altlığı | ✅ Karşılanıyor | OSM, opsiyonel OpenRailwayMap overlay |
| 4.1.33.x | Offline harita altyapısı | ✅ Kısmen | Mimari destekler; tile dosyası Müşteri/İdare’den sağlanırsa kullanılır |
| 4.1.33.x | Koordinat sistemi (WGS84) | ✅ Karşılanıyor | EPSG:4326 varsayılan |
| 4.5.x | Platform entegrasyonu / web arayüz | ✅ Karşılanıyor | Mng.Ui (Nuxt/Vue), responsive web |

**Güncelleme talimatı:** Şartname PDF’i incelendiğinde yukarıdaki madde numaraları ve “Şartname Talebi” sütunu, şartnamedeki gerçek metinle değiştirilmelidir.

---

## 4. Karşılanamayan veya Kısmen Karşılanan Maddeler

### 4.1 Tamamen Karşılanamayan (Out of Scope)

| Madde (Önerilen) | Şartname Talebi (Özet) | Durum | Açıklama |
|------------------|------------------------|-------|----------|
| — | TİS / ATS / KKY gerçek entegrasyonu | ❌ Karşılanmıyor | MVP dışı; simülasyon ile demonstrasyon yapılacak. Gerçek sistem entegrasyonu ayrı proje/faz |
| — | GeoServer / WMS / WFS (OGC standart harita servisleri) | ❌ Karşılanmıyor | MVP dışı; ileride ayrı servis olarak değerlendirilebilir |
| — | PostGIS (ayrı coğrafi veritabanı) | ❌ Karşılanmıyor | MongoDB + object/GeoJSON kullanılacak. PostGIS yoğun coğrafi analiz için ileride değerlendirilebilir |
| — | Offline harita altlık dosyası | ⚠️ Bağımlı | Mimari destekler; tile dosyasının temini Müşteri/İdare sorumluluğundadır. Sağlanmazsa yalnızca online mod |
| — | Antetli harita çıktısı (A0–A4 PDF) | ❌ Karşılanmıyor | Sonraki faz |
| — | Heatmap / yük yoğunluğu haritası | ❌ Karşılanmıyor | Sonraki faz |
| — | Mobil native uygulama | ⚠️ Alternatif | Web tabanlı responsive kapsamda; native mobil MVP dışı |
| — | Çoklu dil (GIS modülüne özel) | ⚠️ Platform | Platform genelinde varsa kullanılır; GIS’e özel geliştirme yok |
| — | Eğitim ve kullanıcı dokümantasyonu | ❌ Karşılanmıyor | Ayrı kapsam; teknik dokümanlar dahil |
| — | MngWorkflow kural–aksiyon entegrasyonu | ❌ Sonraki faz | `tcdd_gis_alerts_config` → otomatik tetikleme sonraki fazda |

### 4.2 Kısmen Karşılanan (Koşullu / Sınırlı)

| Madde | Şartname Talebi | Sınırlama | MVP Yaklaşımı |
|-------|-----------------|-----------|---------------|
| — | CBS editör (harita üzerinde nokta/çizgi çizimi) | Tam CBS editör yok | MVP’de koordinat manuel giriş veya JSON import. CBS editör sonraki fazda değerlendirilebilir |
| — | Gerçek zamanlı tren konumu | Gerçek veri yok | Simülatör ile sentetik veri; TİS/ATS entegrasyonu sonraki faz |
| — | Rapor dışa aktarma (Excel/PDF) | Platform yeteneğine bağlı | Mevcut export destekleniyorsa kullanılır; tam raporlama servisi ileride |

---

## 5. Özet Tablo

| Kategori | Sayı | Açıklama |
|----------|------|----------|
| ✅ Tam karşılanan | 12+ | Harita, lokasyon, güzergâh, formlar, dashboard, raporlama, simülasyon |
| ⚠️ Kısmen / koşullu | 4 | Offline tile, CBS editör, gerçek zamanlı veri, dışa aktarma |
| ❌ Karşılanmayan | 6 | TİS/ATS/KKY, OGC, PostGIS, antetli PDF, heatmap, eğitim, MngWorkflow |

---

## 6. Şartname Güncelleme Prosedürü

1. `teknik_sartname.pdf` açılarak harita/CBS ile ilgili tüm maddeler listelenir.
2. Her madde için: madde no, tam metin veya özet, uyum durumu bu tablolara işlenir.
3. Yeni maddeler bulunursa ilgili tabloya (Karşılanan / Karşılanamayan) eklenir.
4. Revizyon geçmişi aşağıya işlenir.

---

## 7. İlgili Belgeler

| Belge | Açıklama |
|-------|----------|
| [TCDD Teknik Şartname](./teknik_sartname.pdf) | Ana referans şartname |
| [TCDD GIS SOW](./TCDD_GIS_SOW.md) | Scope of Work — teslimat ve kabul kriterleri |
| [TCDD GIS MVP Planlama](./TCDD_GIS_MVP_PLANNING.md) | Teknik planlama |
| [TCDD GIS SOP](./TCDD_GIS_MAP_SOP.md) | Standart işlem prosedürleri |

---

## 8. Revizyon Geçmişi

| Versiyon | Tarih | Değişiklik | Hazırlayan |
|----------|-------|------------|------------|
| 1.0 | 2 Mart 2026 | İlk yayın; SOW ve planlama dokümanlarından türetildi | - |
