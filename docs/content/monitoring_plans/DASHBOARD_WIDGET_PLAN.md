# Dashboard ve Widget Önerileri — Tren / mon_metrics Verisi

Bu doküman, **mon_metrics** koleksiyonundaki tren verisi yapısına göre dashboard ve widget önerilerini, widget oluşturma wizard iyileştirmelerini ve uygulama adımlarını içerir.

---

## 1. Veri yapısı özeti

### Kaynak (tren tarafı)
```json
{
  "trainId": "T1",
  "routeId": "ANK-IST",
  "lat": 40.53, "lon": 30.58,
  "speed": 2191.3, "heading": 290.6,
  "timestamp": "2026-03-08T14:24:38.4953612Z",
  "sensors": {
    "engineTempC": 90.6, "oilPressureBar": 4.83, "coolantTempC": 88.2,
    "batteryVoltageV": 23.77, "brakePipePressureBar": 4.91,
    "cabTempC": 21.1, "vibrationMs2": 0.114, "doorClosed": true
  }
}
```

### MongoDB mon_metrics karşılığı
Her alan ayrı doküman: `meta.assetId`, `meta.collectibleCode`, `value`, `timestamp`.

| collectibleCode | Açıklama | Örnek value |
|-----------------|----------|-------------|
| `trainId` | Tren kodu | "T3" |
| `routeId` | Rota | "ANK-KON" |
| `lat`, `lon` | Konum | 37.86, 32.47 |
| `speed` | Hız | 0 veya km/h |
| `heading` | Yön (derece) | 188 |
| `timestamp` | Zaman damgası | ISO string |
| `sensors.engineTempC` | Motor sıcaklığı (°C) | 74.2 |
| `sensors.oilPressureBar` | Yağ basıncı (bar) | 4.06 |
| `sensors.coolantTempC` | Soğutucu sıcaklığı (°C) | 74 |
| `sensors.batteryVoltageV` | Batarya (V) | 24.35 |
| `sensors.brakePipePressureBar` | Fren borusu basıncı (bar) | 5.03 |
| `sensors.cabTempC` | Kabin sıcaklığı (°C) | 23.3 |
| `sensors.vibrationMs2` | Titreşim (m/s²) | 0.029 |
| `sensors.doorClosed` | Kapı kapalı | true |

---

## 2. Dashboard önerileri

### 2.1 "Tren özet" dashboard (tek tren)
- **Hedef:** Tek bir trenin anlık durumu.
- **Layout önerisi:**
  - **Üst satır:** 4–6 adet **badge/card** (son değer): Hız, Motor °C, Yağ basıncı (bar), Batarya (V), Fren basıncı, Kapı (açık/kapalı).
  - **Orta:** **Harita widget** — sadece bu tren (lat/lon).
  - **Alt satır:** 2–3 **chart**: Hız–zaman, Motor sıcaklığı–zaman, Fren basıncı–zaman.
- **Widget sayısı:** ~6 badge + 1 harita + 2–3 chart.

### 2.2 "Tüm trenler" dashboard
- **Hedef:** Tüm trenlerin konumu + seçilen metrikler.
- **Layout:**
  - **Üst:** İsteğe bağlı özet kartları (toplam tren sayısı, uyarılı tren sayısı vb.).
  - **Ana alan:** **Harita widget** — tüm trenler (asset tipi “Tren” veya manuel çoklu seçim).
  - **Yan/alt:** Chart’lar (çoklu seri: tren bazında hız veya motor sıcaklığı).
- **Widget sayısı:** 1 harita + 2–4 chart/card.

### 2.3 "Bakım / alarm" dashboard
- **Hedef:** Kritik sensörler ve eşik aşımı.
- **Layout:**
  - **Badge’ler:** Motor °C, yağ basıncı, batarya, fren basıncı (renk: normal / uyarı / kritik).
  - **Chart’lar:** Son 1–6 saat trend (motor, yağ, batarya).
- **Widget sayısı:** 4–6 card + 2–3 chart.

---

## 3. Widget tipi önerileri

### 3.1 Badge (son değer kartı)
- **Veri:** `mon_metrics`, filter: `meta.assetId` + `meta.collectibleCode`, sort `-timestamp`, limit 1.
- **Görünüm:** Küçük kart; büyük sayı + birim + ikon (örn. mdi-speedometer, mdi-thermometer).
- **Uygulama:** Mevcut **card** tipi aynı veriyle kullanılabilir; `config.format`, `config.icon`, `config.color` ile badge görünümü verilebilir. İsteğe bağlı: ayrı **badge** tipi veya card’a `variant: 'badge'` eklenebilir.

| Metrik | collectibleCode | İkon önerisi | Birim |
|--------|-----------------|--------------|-------|
| Hız | speed | mdi-speedometer | km/h |
| Motor sıcaklığı | sensors.engineTempC | mdi-thermometer | °C |
| Yağ basıncı | sensors.oilPressureBar | mdi-gauge | bar |
| Batarya | sensors.batteryVoltageV | mdi-battery-charging | V |
| Fren basıncı | sensors.brakePipePressureBar | mdi-car-brake-parking | bar |
| Kapı | sensors.doorClosed | mdi-door-closed | Açık/Kapalı |
| Rota | routeId | mdi-map-marker-path | — |

### 3.2 Chart (zaman serisi)
- **Veri:** Aynı asset + collectibleCode, zaman aralığı (örn. son 1 saat), limit 500.
- **Görünüm:** Line/area/bar (mevcut ChartWidget).
- **Önerilen metrikler:** speed, sensors.engineTempC, sensors.oilPressureBar, sensors.batteryVoltageV, sensors.brakePipePressureBar.

### 3.3 Harita widget
- **Veri:** `lat` ve `lon` collectible’ları; assetId bazlı son konum.
- **Görünüm:** Mevcut MonitoringMapView (OSM / GeoServer); dashboard’da sabit yükseklik (örn. 320px).
- **Seçenekler:** Tek tren veya çoklu tren (tipe göre / manuel).

### 3.4 Anlamlı widget ve chart seçimi (rehber)

Elimizdeki **widget tipleri:** Chart, Card, Gauge, Harita.  
**Chart tipleri:** Çizgi (line), Çubuk (bar), Alan (area), Pasta (pie), Halka (donut).  
**Veri türleri (mon_metrics):** Zaman serisi (sayı), anlık sayı, anlık boolean, konum (lat/lon), metin (örn. trainId, routeId).

| Ne göstermek istiyorsunuz? | Veri türü / Örnek collectible | Önerilen widget | Chart tipi (sadece Chart için) |
|----------------------------|--------------------------------|-----------------|--------------------------------|
| **Zaman içinde trend** (örn. son 1 saatte motor sıcaklığı) | Zaman serisi sayısal: `speed`, `sensors.engineTempC`, `sensors.oilPressureBar`, `sensors.batteryVoltageV` | **Chart** | **Line** veya **Area** — sürekli değişen metrikler için en okunaklı. **Bar** da uygun (özellikle az nokta varsa). |
| **Birden fazla asset’i aynı grafikte karşılaştırma** (örn. T1, T2, T3 hızı) | Aynı collectible, çoklu asset; zaman serisi | **Chart** (çoklu asset seçin) | **Line** veya **Area** — legend’da asset isimleri çıkar. |
| **Anlık tek değer** (şu anki hız, motor °C, batarya V) | Son kayıt, sayısal: `speed`, `sensors.engineTempC`, `sensors.batteryVoltageV` | **Card** | — |
| **Açık/Kapalı, Evet/Hayır** (kapı kapalı mı?) | Son kayıt, boolean: `sensors.doorClosed` | **Card** (format: boolean) | — |
| **Tek sayıyı min–max aralığında gösterme** (gauge) | Son veya ortalama değer; bir min/max aralığı anlamlı: basınç, sıcaklık, voltaj | **Gauge** | — |
| **Konum / rota** (nerede?) | `lat`, `lon` (son konum) | **Harita** | — |
| **Oran veya kategori dağılımı** (örn. toplam değerin yüzde payları) | Zaman serisi veya anlık; **gruplanmış/özet** veri (şu an mon_metrics’te doğrudan yok; ileride aggregation ile) | **Chart** | **Pie** veya **Donut** — kategorik/oransal veri için. Ham zaman serisi yerine “son N kayıt özeti” gibi veri hazırlanırsa anlamlı olur. |

**Özet:**
- **Zaman serisi sayı** → Chart, tip: **line** veya **area** (tercih), gerekirse bar.
- **Anlık sayı** → Card veya Gauge (aralık varsa).
- **Anlık boolean** → Card (boolean format).
- **Konum** → Harita.
- **Oran/dağılım** (özet veri) → Chart, tip: **pie** veya **donut**.

---

## 4. Widget oluşturma wizard iyileştirmeleri

### 4.1 Yapılacaklar (uygulama)

1. **Harita widget tipi**
   - Widget tipi listesine **Harita** eklenir.
   - Harita seçildiğinde Adım 2 (Collectible) **zorunlu olmaz**; konum için lat/lon otomatik kullanılır.
   - Adım 3’te harita için sadece başlık, name, yenileme aralığı alınır.

2. **Adım 2 kuralları**
   - **Chart veya Card** seçiliyse: collectible zorunlu (mevcut davranış).
   - **Harita** seçiliyse: collectible atlanır; Adım 2’de bilgi metni: “Harita widget’ında seçilen asset’lerin konum verisi (lat/lon) kullanılır.”
   - Geçiş: Adım 2’de “İleri” her zaman tıklanabilir; Adım 3’te Chart/Card ise collectible boşsa “Kaydet” devre dışı.

3. **Collectible etiketleme (okunabilir isimler)**
   - Tren/asset tipinden gelen collectible listesi, bilinen `collectibleCode` değerleri için **görünen ad** ile zenginleştirilir.
   - Örnek eşleme (code → label):
     - speed → "Hız (km/h)"
     - lat, lon → "Enlem", "Boylam"
     - heading → "Yön (°)"
     - sensors.engineTempC → "Motor sıcaklığı (°C)"
     - sensors.oilPressureBar → "Yağ basıncı (bar)"
     - sensors.coolantTempC → "Soğutucu sıcaklığı (°C)"
     - sensors.batteryVoltageV → "Batarya (V)"
     - sensors.brakePipePressureBar → "Fren borusu basıncı (bar)"
     - sensors.cabTempC → "Kabin sıcaklığı (°C)"
     - sensors.vibrationMs2 → "Titreşim (m/s²)"
     - sensors.doorClosed → "Kapı kapalı"
     - trainId, routeId, timestamp → "Tren kodu", "Rota", "Zaman"
   - Bu eşleme formda collectible dropdown’ında `item-title` olarak kullanılır; code aynen saklanır.

4. **Badge görünümü (opsiyonel)**
   - Card tipi için `config.variant: 'badge'` veya ayrı “Badge” tipi eklenebilir; veri yapısı card ile aynı (son değer).

### 4.2 Wizard akış özeti (güncel)

| Adım | İçerik | Zorunluluk |
|------|--------|------------|
| 1 | Asset kapsamı (tipe göre / manuel), asset tipi veya manuel seçim | Tipe göre: tip seçili; Manuel: en az 1 asset |
| 2 | Collectible seçimi + bilgi (harita için: “lat/lon kullanılacak”) | Chart/Card: collectible zorunlu; Harita: zorunlu değil |
| 3 | Başlık, name, widget tipi (Chart / Card / Harita), chart tipi, zaman aralığı, limit, yenileme | Başlık + name; Chart/Card ise collectible dolu olmalı |

---

## 5. Teknik referanslar

- **Widget listesi:** `Mng.Ui/pages/apps/monitoring/widgets/index.vue`
- **Widget form (wizard):** `Mng.Ui/components/apps/monitoring/MonitoringWidgetForm.vue`
- **Widget renderer:** `Mng.Ui/components/widgets/WidgetRenderer.vue`
- **Veri çekme:** `Mng.Ui/services/widgetDataService.ts` (mon_metrics, timeRangeMinutes, multiSeries)
- **Harita konumları:** `Mng.Ui/composables/useMapPositions.ts` (lat/lon)
- **Dashboard layout:** `Mng.Ui/stores/apps/dashboard.ts`, `DashboardLayoutRenderer.vue`

---

## 6. Sıra özeti

1. **Plan (bu doküman)** — Tamamlandı.
2. **Wizard:** Harita tipi, Adım 2 opsiyonel (harita için), collectible etiketleme.
3. **Backend/UI:** Widget tipi `map`, MapWidget bileşeni, WidgetRenderer’da map case.
4. **İsteğe bağlı:** Badge variant veya badge tipi; örnek dashboard şablonları (seed/import).

Bu sırayla ilerlenebilir; önce wizard + harita widget tamamlanır, ardından badge ve örnek dashboard'lar eklenebilir.

---

## 7. Bilinen durumlar

### 7.1 Gauge widget — çift parça görünümü

- **Belirti:** Bazı tarayıcı/ortamlarda gauge değer göstergesi iki ayrı turuncu parça gibi görünüyor: ana yay + sağ uçta küçük bir bar.
- **Denenenler:** (1) Değer yayı stroke yerine **dolu bant (filled band)** ile çizildi (iç/dış yay arası `fill`, stroke yok); (2) Daha önce stroke kullanılırken `stroke-linecap: butt` denendi. Görünüm bazı ortamlarda aynı kaldı.
- **Durum:** Bilinen bir görüntüleme farkı olarak not edildi; işlevsel (min/max, eşik renkleri, tema) çalışıyor. İleride tarayıcı/GPU veya SVG render farkları araştırılabilir.
- **Dosya:** `Mng.Ui/components/widgets/gauge/GaugeWidget.vue`

---

## 8. Dashboard ve widget seed

**Amaç:** Kurulum veya demo ortamında hemen kullanılabilecek **örnek dashboard** ve **monitoring widget’ları** oluşturmak. Kullanıcı bu örnekleri açar, widget’lara asset/collectible seçerek veri bağlar.

### 8.1 Ön koşullar

- DataGateway erişimi (token: `load-token.ps1` / `get-token.ps1`).
- Dataset’ler: `@widget_categories`, `@widgets`, `@dashboards`. (Seed script bu dataset’leri yoksa oluşturabilir.)
- İsteğe bağlı: `mon_assets` içinde en az bir tren (örn. T1) ve `mon_metrics` verisi; yoksa widget’lar “veri yok” gösterir, kullanıcı widget’ı düzenleyip asset seçer.

### 8.2 Seed içeriği

- **Widget kategorisi:** "Monitoring" (yoksa oluşturulur).
- **Örnek widget’lar (mon_metrics tabanlı):**
  - Card: Hız (speed), Motor sıcaklığı (sensors.engineTempC), Yağ basıncı (sensors.oilPressureBar).
  - Chart: Hız–zaman (line).
  - Harita: Tek/çoklu asset konumu (lat/lon).
  - Gauge: Motor sıcaklığı (min/max/eşik).
- **Örnek dashboard:** "Tren özet" — üst satırda 3 card, orta satırda harita, alt satırda chart + gauge. Layout’taki `widgetId` değerleri seed sırasında oluşturulan widget `__dataId`’leri ile doldurulur.

### 8.3 Script ve dosyalar

- **Script:** `scripts/tests/MngDataGateway/dataset/setup-dashboard-widget-seed.ps1`  
  - Dataset’leri oluşturur (yoksa); Monitoring kategorisini ekler; seed widget’ları POST eder; örnek dashboard’ı bu widget ID’leri ile oluşturur.
- **Seed veri:** Script içinde inline veya `monitoring-widgets-seed.json` / `monitoring-dashboard-seed.json` (opsiyonel) kullanılabilir.

### 8.4 Kullanım

```powershell
cd scripts/tests/MngDataGateway/dataset
.\setup-dashboard-widget-seed.ps1 -BaseUrl "https://localhost:5040" -UseGateway
```

**Chart seçenekleri (sonraki adım):** Bkz. [CHART_OPTIONS_NEXT.md](CHART_OPTIONS_NEXT.md).

- Mevcut dashboard/widget’ları silmez; **yeni** kayıtlar ekler. Aynı `name` ile widget/dashboard varsa 409 alınabilir (script idempotent davranacak şekilde “name ile kontrol” veya “zaten varsa atla” ile genişletilebilir).