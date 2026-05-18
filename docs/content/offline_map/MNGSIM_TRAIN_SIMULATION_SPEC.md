# MngSim — Tren Simülasyonu Spesifikasyonu

> Railway Platform için sentetik veri: (1) tren coğrafi konumları, (2) tren sensör verileri.  
> Bu doküman konum modelini ve REST API’yi tanımlar; sensör verileri coğrafi model netleştikten sonra eklenir.

---

## 1) Üretilecek İki Veri Türü

| Tür | Açıklama | Tüketen |
|-----|----------|--------|
| **Coğrafi konum** | Her tren için anlık koordinat (lat, lon); gerçek ray/istasyon hattı üzerinde | MngEngine → MngReactor → RabbitMQ → MngHub → UI |
| **Sensör verileri** | Tren bazlı sensör metrikleri (detay sonra) | Ayrı kanal / sonra netleşecek |

Bu spec önce **konum** tarafını netleştirir.

---

## 2) Rotalar

- **Tanım:** Sabit hatlar; başlangıç ve bitiş istasyonu (isim + koordinat) ile süre (dakika) tanımlanır.
- **Resmi başlangıç/bitiş noktaları** aşağıda sabitlenmiştir; referans dosya: `docs/content/offline_map/routes-reference.json`.

### 2.1) İstasyon referansları (PostGIS uyumlu)

| İstasyon ID | İsim | Lon | Lat | PostGIS stations.id |
|-------------|------|-----|-----|---------------------|
| ankara-yht | Ankara YHT Garı | 32.843351 | 39.934935 | 4c17d80f-09ab-4373-b7b4-5d1287329cb3 |
| halkali | Halkalı | 28.766529 | 41.018370 | 58a69035-b7f9-4c55-a5e1-d8540cb34731 |
| konya | Konya | 32.475896 | 37.865336 | c836d640-4e1f-4cd2-95b7-7fdc11da9415 |

### 2.2) Rota tanımları (sabit)

| Rota ID | Başlangıç | Bitiş | Süre (dk) | Uçta bekleme |
|---------|-----------|-------|-----------|--------------|
| ANK-IST | Ankara YHT Garı (ankara-yht) | Halkalı (halkali) | 270 | 5 dk |
| IST-ANK | Halkalı (halkali) | Ankara YHT Garı (ankara-yht) | 270 | 5 dk |
| ANK-KON | Ankara YHT Garı (ankara-yht) | Konya (konya) | 90 | 5 dk |
| KON-ANK | Konya (konya) | Ankara YHT Garı (ankara-yht) | 90 | 5 dk |

- Rota listesi konfigüre edilebilir (config veya API ile eklenebilir); başlangıç/bitiş bu tabloya göre sabit kalmalıdır.
- **Coğrafi kısıt:** Konumlar, gerçek demiryolu hattı üzerinde olmalı (PostGIS `railways` / istasyon noktaları ile uyumlu). Rota geometrisi bu istasyonlar arasında `railways` üzerinden çıkarılır veya MngSim dışarıdan alır.

---

## 3) Tren ve Döngü Davranışı

- **Tren:** Bir rotaya atanır; rotada otomatik hareket eder.
- **Döngü:**
  1. Başlangıç istasyonunda (örn. Ankara) **5 dakika bekle**.
  2. Rotayı başlat; **süre (dakika)** içinde bitiş istasyonuna (örn. İstanbul) git.
  3. Bitişte **5 dakika bekle**.
  4. Ters yönü başlat (veya aynı rota tanımı ters yön); süre içinde başlangıca dön.
  5. Başlangıçta tekrar **5 dakika bekle** → 1’e dön.

Yani: **A → (süre) → B → (5 dk) → B→A → (süre) → A → (5 dk) → tekrar.**

- **Aynı anda birden fazla tren:** Farklı rotalarda veya aynı rotada birden fazla tren tanımlanabilir; her biri kendi döngüsünde ilerler.

---

## 4) Konum Üretimi ve Gerçek Hat Üzerinde Olma

- Simülasyon zamanı (veya gerçek zaman) ile tren, rotadaki **ilerleme oranı** (0..1) hesaplanır.
- Bu ilerleme, **rota geometrisi** (polyline) üzerinde konuma çevrilir.
- **Rota geometrisi:** PostGIS’teki `railways` (ve gerekirse istasyon noktaları) kullanılarak önceden üretilmiş segmentler olabilir; veya MngSim bir “rota servisi”nden (MngReactor veya statik dosya) A→B hattını alır.
- Çıktı: Her tren için **lat, lon** (+ isteğe bağlı hız, yön, timestamp) — **gerçek demiryolu hattı üzerinde**.

### 4.1) Konum hesaplama modeli (zaman → ilerleme → koordinat)

Konum **zaman tabanlı** hesaplanır: her istek anında (örn. her 5 saniyede bir REST çağrısı) mevcut zamandan ilerleme türetilir; **adım adım "her 5 saniyede 5/600 ilerlet" gibi bir birikim yapılmaz.** Böylece çağrı sıklığından bağımsız, birikim hatası olmayan tutarlı konum elde edilir.

**Örnek:** Ankara–İstanbul 10 dakika (600 s) olsun.

1. **Döngü zamanı (cycle):** Bir tam A→B→A döngüsü, **5 dakikalık istasyon bekleme süreleri dahil**: 5 dk A'da bekle + 600 s A→B hareket + 5 dk B'de bekle + 600 s B→A hareket + 5 dk A'da bekle = `T_cycle`. Trenin `t_start` başlangıç anından itibaren `t_now` anında:
   - `t_elapsed = t_now - t_start` (saniye)
   - Döngü içindeki konum: `t_in_cycle = t_elapsed mod T_cycle`
   - **Faz:** `t_in_cycle` hangi aralıktaysa o faza göre ilerleme:
     - **A'da bekle (5 dk):** progress = 0 → konum = A istasyonu (polyline başı).
     - **A→B hareket:** progress = t_move / durationSeconds → polyline üzerinde hareket.
     - **B'de bekle (5 dk):** progress = 1 → konum = B istasyonu (polyline sonu).
     - **B→A hareket:** progress, 1'den 0'a gidecek şekilde (veya ters polyline ile) hesaplanır.

2. **Hareket fazında ilerleme (progress ∈ [0, 1]):** Örneğin A→B hareketi `durationSeconds = 600` ile:
   - Hareket fazının başlangıcından geçen süre: `t_move`
   - **progress = min(1, t_move / 600)**  
   Yani 0. saniyede progress=0 (A), 300. saniyede progress=0.5 (yolun yarısı), 600. saniyede progress=1 (B).  
   İstek 5 saniyede bir gelse bile her seferinde **o anki** `t_move` kullanılır; "bir önceki progress + 5/600" yapılmaz.

3. **Progress → koordinat:** Rota geometrisi bir **polyline** (noktalar dizisi, WGS84 lon/lat). Toplam uzunluk **L (metre)**:
   - **progress=0 veya bekleme fazında A’da:** Konum = A istasyon koordinatı (`routes-reference.json`).
   - **progress=1 veya bekleme fazında B’de:** Konum = B istasyon koordinatı (`routes-reference.json`).
   - **Hareket fazında (0 < progress < 1):** Hedef mesafe `d = progress * L` (A→B) veya `d = (1 − progress) * L` (B→A); polyline üzerinde segmentleri metre cinsinden topla, `d` mesafedeki noktada lineer interpolasyonla **(lon, lat)** üretilir.

Özet: **Her konum isteğinde** → (1) şu anki zaman → (2) döngü fazı ve hareket içindeki süre → (3) progress = süre / rota_süresi → (4) progress × polyline uzunluğu = mesafe → (5) polyline üzerinde o mesafedeki nokta = **(lat, lon)**. Böylece 5 saniyede bir çağırsan da 10 saniyede bir çağırsan da aynı zaman diliminde aynı konum döner; hesaplama deterministik ve poll periyodundan bağımsızdır.

### 4.2) Coğrafi hesaplama — kararlar ve önerilen yaklaşım

Aşağıdaki seçimler implementasyon için referans alınacaktır:

| Konu | Karar |
|------|--------|
| **Rota polyline kaynağı** | **Önceden export edilmiş statik polyline.** Her rota (ANK-IST, ANK-KON vb.) için A→B yönünde tek bir koordinat dizisi (GeoJSON veya `[[lon,lat], ...]`) dosyada tutulur. Polyline, PostGIS’ten bir kerelik (script veya manuel) çıkarılır; MngSim sadece bu dosyaları okur. Örn. `docs/content/offline_map/route-geometries/ANK-IST.json`. |
| **Polyline ↔ istasyon uyumu** | **progress=0 ve progress=1 (ve bekleme fazları) için konum doğrudan istasyon koordinatı.** `routes-reference.json` içindeki ilgili istasyonun `lon`/`lat` değeri döner; polyline’ın uç noktasının tam istasyona snap olması zorunlu değildir. |
| **Uzunluk birimi (L)** | **Metre.** Polyline toplam uzunluğu metre cinsinden hesaplanır (segment başına Haversine veya benzeri; veya PostGIS `ST_Length(geography)` ile export sırasında). |
| **CRS** | **WGS84 (EPSG:4326).** Tüm koordinatlar ve polyline (lon, lat) bu referansta. |
| **B→A yönü** | **Tek polyline (A→B) kullanılır.** B→A hareketinde hedef mesafe **d = (1 − progress) × L**; aynı polyline üzerinde sondan başa doğru bu mesafedeki nokta alınır (ekstra ters polyline tutulmaz). |

**Rota geometrisi export’u:** `docs/content/offline_map/scripts/export_route_geometries.py` — PostGIS koridorundaki `railways` segmentlerini alıp açgözlü sıralama ile A→B polyline üretir. Çıktı: `docs/content/offline_map/route-geometries/ANK-IST.json`, `ANK-KON.json` (format: `coordinates`, `length_m`). MngSim bu dosyaları okur.

---

## 5) REST API — Konum Sorgulama

Tüketen taraf (MngEngine veya test) **belirli periyotlarla** konum isteyecek.

- **Tüm trenlerin anlık konumları:**  
  `GET /api/trains/positions`  
  Cevap: `{ "updatedAt": "ISO8601", "positions": [ { "trainId", "routeId", "lat", "lon", "speed", "heading", "timestamp" }, ... ] }`

- **Tek tren:**  
  `GET /api/trains/{trainId}/position`  
  Cevap: `{ "trainId", "routeId", "lat", "lon", "speed", "heading", "timestamp" }` veya 404.

- **İsteğe bağlı:** Query ile `?trainIds=T1,T2` ile sadece belirtilen trenler dönülebilir.

Böylece tüketen taraf istediği periyotla (örn. 5 saniyede bir) bu endpoint’leri çağırır; push (MQTT/WebSocket) sonra eklenebilir.

---

## 6) Konfigürasyon Örneği (Tren + Rota)

- **Rotalar:** Id, başlangıç, bitiş, süre (dakika), geometri referansı veya dosya yolu.
- **Trenler:** Id, ad, bağlı olduğu rota, başlama zamanı (veya “hemen”).

Örnek (`routes-reference.json` ile uyumlu):

```json
{
  "routes": [
    { "id": "ANK-IST", "fromStationId": "ankara-yht", "toStationId": "halkali", "durationMinutes": 270, "waitAtEndMinutes": 5 },
    { "id": "IST-ANK", "fromStationId": "halkali", "toStationId": "ankara-yht", "durationMinutes": 270, "waitAtEndMinutes": 5 },
    { "id": "ANK-KON", "fromStationId": "ankara-yht", "toStationId": "konya", "durationMinutes": 90, "waitAtEndMinutes": 5 },
    { "id": "KON-ANK", "fromStationId": "konya", "toStationId": "ankara-yht", "durationMinutes": 90, "waitAtEndMinutes": 5 }
  ],
  "trains": [
    { "id": "T001", "name": "YHT-1", "routeId": "ANK-IST", "startImmediately": true },
    { "id": "T002", "name": "YHT-2", "routeId": "IST-ANK", "startImmediately": true }
  ]
}
```

İstasyon koordinatları ve PostGIS id'leri için `docs/content/offline_map/routes-reference.json` kullanılır. Gerçek konumun hattın üzerinde olması için `routes` içinde `geometrySource` (örn. PostGIS layer adı veya önceden hesaplanmış polyline) tanımlanabilir.

---

## 7) Lokomotif / Tren Sensör Verileri: İki Kanallı Model

Tren sensörleri **iki şekilde** sunulur:

| Kanallar | Taşıma | Kullanım | Örnek veriler |
|----------|--------|----------|----------------|
| **Polling (periyodik)** | **HTTP** — MngEngine, MngSim’e REST ile istek atar (konum endpoint’i gibi). | Sürekli izlenen metrikler; periyodik okuma. | Enerji, motor sıcaklığı, batarya, fren basıncı, hız, kabin sıcaklığı. |
| **Event (anlık olay)** | **MQTT** — MngSim olay olduğunda mesaj yayınlar; taraflar subscribe eder. | Seyrek, anlık olaylar; tetiklenince iletilir. | Yangın alarmı, hareket halinde kapı açıldı, acil fren, anormal titreşim uyarısı. |

---

### 7.1) Polling sensörleri (HTTP)

MngEngine’in aynı REST çağrısında hem konum hem bu metrikleri alması için **konum yanıtına isteğe bağlı `sensors` alanı** eklenir.

**Taşıma:** `GET /api/trains/positions` ve `GET /api/trains/{trainId}/position` (mevcut endpoint’ler). Query: `?includeSensors=true` (varsayılan true).

**Örnek veri türleri (sürekli / periyodik):**

| Kategori | Veri | Birim | Açıklama |
|----------|------|-------|----------|
| Hareket | Hız, yön (heading) | km/h, ° | Konum modelinden türetilebilir. |
| Motor / güç | Motor sıcaklığı, yağ basıncı/sıcaklığı, soğutucu sıcaklığı | °C, bar | Soğutma ve yağlama izleme. |
| Enerji | Batarya gerilimi, yakıt seviyesi | V, % | Yardımcı sistemler. |
| Fren | Fren borusu / silindir basıncı | bar | Hava freni. |
| Kapı (durum) | Kapı kapalı mı (anlık durum) | boolean | Polling’de “şu an kapalı”; olay “açıldı” MQTT’de. |
| Titreşim (sürekli) | Aks/bogie ölçümü | m/s² | Anomali eşiği aşılırsa ayrıca event. |
| Ortam | Kabin sıcaklığı | °C | Sürücü kabini. |

**Örnek yanıt (konum + sensors):**
```json
{
  "trainId": "T001",
  "routeId": "ANK-IST",
  "lat": 39.5,
  "lon": 32.8,
  "speed": 245.0,
  "heading": 270.5,
  "timestamp": "2025-03-03T12:00:00Z",
  "sensors": {
    "engineTempC": 78.5,
    "oilPressureBar": 4.2,
    "coolantTempC": 82.0,
    "batteryVoltageV": 24.1,
    "brakePipePressureBar": 5.0,
    "cabTempC": 22.0,
    "vibrationMs2": 0.05,
    "doorClosed": true
  }
}
```

---

### 7.2) Event sensörleri (MQTT)

Olay **olduğu anda** tek seferlik mesaj yayınlanır; tüketen taraflar (MngEngine, Workflow, UI) MQTT topic’ine **subscribe** eder.

**Taşıma:** MQTT broker (örn. Mosquitto). MngSim, simülasyon kurallarına göre olay üretir ve ilgili topic’e **publish** eder.

**Örnek olay türleri:**

| Olay | Açıklama | Tetikleyici (simülasyonda) |
|------|----------|----------------------------|
| Yangın alarmı | Lokomotif veya vagon yangın sensörü | Sentetik: rastgele zamanlı veya test amaçlı manuel. |
| Hareket halinde kapı açıldı | Kapı güvenlik sensörü + hız > 0 | Sentetik: rastgele veya belirli tren/rotada simüle. |
| Acil fren | Fren sistemi acil fren tetiklendi | Sentetik: olay senaryosu veya test. |
| Anormal titreşim | Titreşim eşiği aşıldı | Polling’deki titreşim değeri eşiği geçince event. |
| Aşırı ısınma uyarısı | Motor/soğutucu sıcaklık eşiği | Polling’deki sıcaklık eşiği geçince event. |
| Düşük basınç uyarısı | Yağ/fren basıncı düşük | Polling’deki basınç eşiği altına inince event. |

**Topic ve mesaj formatı (öneri):**

- **Topic:** `mngsim/trains/events` veya `mngsim/trains/{trainId}/events` (tren bazlı subscribe için).
- **Payload (JSON):** En azından `trainId`, `eventType`, `timestamp`; olay türüne göre ek alanlar (konum, değer, şiddet vb.).

Örnek:
```json
{
  "trainId": "T001",
  "eventType": "door_opened_while_moving",
  "timestamp": "2025-03-03T12:05:00Z",
  "speedKmh": 120.0,
  "doorId": "rear-left"
}
```

```json
{
  "trainId": "T002",
  "eventType": "fire_alarm",
  "timestamp": "2025-03-03T12:10:00Z",
  "zone": "engine",
  "severity": "high"
}
```

**Tüketen taraf:** MngEngine (veya ayrı bir servis) bu topic’e subscribe olur; gelen mesajları işler, Reactor/Workflow’a veya alarm sistemine iletebilir.

---

### 7.3) Özet

- **Polling (HTTP):** Konum endpoint’i + `sensors` ile enerji, sıcaklık, basınç, kapı durumu vb. tek istekte alınır.
- **Event (MQTT):** Yangın, hareket halinde kapı açılması, acil fren, eşik aşım uyarıları vb. olay anında MQTT ile yayınlanır; tek bir taşıma katmanı (MQTT) kullanılır.

---

## 8) Özet ve Sıradaki Adımlar

| Madde | Durum |
|-------|--------|
| Rotalar (ANK-IST, IST-ANK, ANK-KON, KON-ANK) + süre (dakika) | Spec’te tanımlandı |
| Tren → rota atama, döngü (5 dk bekleme uçlarda) | Spec’te tanımlandı |
| Konumun gerçek hat üzerinde olması | §4.2: statik polyline + progress→koordinat kuralları belirlendi |
| REST: tüm / tek tren konumları | Endpoint’ler spec’te |
| Çoklu tren aynı anda | Spec’te kabul edildi |
| Sensör verileri (polling HTTP + event MQTT) | §7’de iki kanallı model tanımlandı |

**Sıradaki teknik adımlar:**  
1) ~~Rota polyline’larını üretme~~ ✅  
2) ~~MngSim’de rota + tren config ve konum motoru~~ ✅  
3) ~~REST API `/api/trains/positions` ve `/api/trains/{id}/position`~~ ✅  
4) ~~Polling sensörleri: konum yanıtına `sensors` objesi ve `includeSensors` query~~ ✅  
5) ~~Event sensörleri: MQTT client, topic (`mngsim/trains/events` vb.), manuel tetikleme, UI event logu ve haritada uyarı~~ ✅  
(Eşik bazlı otomatik event üretimi kapsam dışı bırakıldı; yalnızca UI’dan tetikleme kullanılıyor.)

---

*Referans: [railway-platform.md](./railway-platform.md) Bölüm 1.1, 9.1.1; Faz 2.*
