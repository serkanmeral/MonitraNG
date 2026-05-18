# Bugünün Yol Haritası (Roadmap)

**Tarih:** 8 Mart 2026

Bu doküman, agent/asset tanımları ve ilgili geliştirmeler için bugünkü iş listesidir. Maddeler sırayla güncellenir.

**Sonraki adım:** Madde 3 — Gelen veriler üzerinde örnek dashboard'lar + harita widget.

**Chart widget:** Çalışmalara sonra devam edilecek; mevcut durum [Chart widget — mevcut durum](#chart-widget--mevcut-durum-sonra-devam) bölümünde ve `CHART_OPTIONS_NEXT.md` / `DASHBOARD_WIDGET_PLAN.md` §3.4’te özetlendi.

---

## MngUI — Harita işlemleri kontrolü ✅ Tamamlandı

**Hedef:** Mevcut harita sayfaları ve bileşenlerinin çalışırlığını doğrulamak; veri kaynakları, hata durumları ve kullanıcı akışını kontrol etmek.

### Yapılanlar (8 Mart 2026)

- [x] **Harita alanı:** Harita bileşeni bulunduğu div'i tam dolduruyor (flex layout, 70vh/min-height, ClientOnly kaldırıldı; Leaflet doğru container'a bağlanıyor).
- [x] **Otomatik yenileme:** Harita sayfasında aç/kapa + aralık seçimi (15/30/60 sn); geri sayım metni; sayfa terk edildiğinde timer temizleniyor.
- [x] **GeoServer (çevrimdışı):** Tile proxy TileMatrixSet + col/row ofset (env); 400 TileOutOfRange'de şeffaf tile; MonitoringMapView'da waterways/water_areas katmanları; çevrimiçi/çevrimdışı tek seçim (OSM veya GeoServer); katman listesi sağda, açılır/kapanır panel, katman isimleri (getLayerLabel) görünüyor.
- [x] **Env ve dokümantasyon:** Mng.Ui `.env.example`, mng_apps `env.example`, GEOSERVER_GRIDSET_EPSG3857.md içinde Mng.Ui ortam değişkenleri.
- [x] **Tren detay modal:** MonitoringMapAssetModal — profesyonel başlık (ikon, başlık, alt başlık), varlık bilgisi grid, son metrikler liste; uzun değerler kısaltıldı (ISO tarih → kısa format, metin 22+ karakter → "…"); modal 400px, içerik scroll (max-height 60vh/320px); dark theme renkleri tema değişkenleriyle (--v-theme-on-surface, primary).

### Mevcut yapı (referans)

| Öğe | Dosya / Konum | Açıklama |
|-----|----------------|----------|
| **Harita sayfası** | `Mng.Ui/pages/apps/monitoring/map/index.vue` | İki sekme: "Tren / varlıklar" (MonitoringMapView) ve "Organizasyon" (OrganizationMapView). |
| **Tren konum haritası** | `Mng.Ui/components/apps/monitoring/MonitoringMapView.vue` | Leaflet; OSM / GeoServer tek seçim; katman paneli sağda açılır/kapanır. |
| **Organizasyon haritası** | `Mng.Ui/components/apps/organization/OrganizationMapView.vue` | Leaflet; organizasyon ağacındaki konumlu item'lar; marker tıklanınca detay modal. |
| **Konum verisi (tren)** | `Mng.Ui/composables/useMapPositions.ts` | DataGateway: `mon_metrics` (lat/lon) + `mon_assets`; yenile + otomatik yenileme. |
| **Tren detay modal** | `Mng.Ui/components/apps/monitoring/MonitoringMapAssetModal.vue` | Profesyonel layout; kısa değerler; scroll; dark theme uyumlu. |

### Kontrol listesi (doğrulandı / yapıldı)

- [x] **Sayfa erişimi:** `/apps/monitoring/map`; breadcrumb ve sekmeler doğru.
- [x] **Tren sekmesi:** Konum yoksa mesaj; veri varsa marker'lar; yenile + otomatik yenileme.
- [x] **Tren veri kaynağı:** `useMapPositions` — DataGateway; hata durumunda alert.
- [x] **Organizasyon sekmesi:** Konumlu item'lar; marker → OrganizationItemDetailModal.
- [x] **GeoServer:** Altlık seçimi (Çevrimiçi / Çevrimdışı); katman listesi sağda, açılır/kapanır.
- [x] **Kontrol sayfası linki:** "Kontrol" butonu `/apps/monitoring/control`'a gidiyor.
- [ ] **İsteğe bağlı:** Hub `monitoring.data.updated` ile harita otomatik yenileme (şu an timer ile).

### Referans

- Monitoring map sayfası: `Mng.Ui/pages/apps/monitoring/map/index.vue`
- Konum verisi: `Mng.Ui/composables/useMapPositions.ts`
- GeoServer proxy: `Mng.Ui/server/api/tiles/geoserver.get.ts`; env: `GEOSERVER_BASE_URL`, `GEOSERVER_TILE_MATRIX_SET`, `GEOSERVER_TILE_COL_OFFSET`, `GEOSERVER_TILE_ROW_OFFSET`
- ROADMAP_TODAY Madde 3 (harita widget) ile ilişki: Mevcut harita bileşenleri dashboard widget olarak da kullanılabilir.

---

## 1. Cron time wizard'larını baştan ele almak ✅ Tamamlandı

**Hedef:** Agent ve asset tanımlarken kullanılan cron üretici wizard'ları genişletmek; **saniye**, dakika ve **saat** seçenekleri sunmak; format Quartz 6 alanlı.

**Mevcut durum:**
- **Konum:** `Mng.Ui/pages/apps/monitoring/index.vue` — Cron builder modal (Toplama periyotları ve Engine "Veri gönderim cron" formlarında "Cron oluştur" butonu ile açılıyor).
- **Format:** Şu an 5 alanlı; Engine/Quartz 6 alanlı kabul ediyor (ör. `0 * * * * ?` → saniye dakika saat gün ay haftanın_günü).

---

### Kararlar (tartışma çıktısı)

- **Birim ve format:** Saniye her zaman kullanılabilir; çıktı **her zaman 6 alanlı** Quartz cron. **Birim girişi: tek satır** → [ Sayı (input) ] [ Birim (dropdown: Saniye / Dakika / Saat) ].
- **Gün / hafta:** **Farklı bir blok** olacak (basit “Her N birim” bloğundan ayrı; örn. “Her gün”, “Haftanın belirli günleri” vb.).
- **Karma ifadeler:** **Ayrı bir tab** içinde; belirli günlerin belirli saatleri (örn. “Pazartesi–Cuma 08:00”, “Her Pazartesi 09:00”) gibi ifadeler bu tab’da tanımlanabilir.
- **Genel hedef:** **Dinamik** bir wizard; cron time seçeneklerini (saniye, dakika, saat, gün, hafta, belirli saat/gün kombinasyonları) kullanabilmek.

**Wizard yapısı (özet):**
- **Tab 1 – Basit periyot:** Tek satır [ N ] [ Saniye / Dakika / Saat ]; isteğe bağlı ayrı blok: gün/hafta (her gün, haftanın belirli günü vb.).
- **Tab 2 – Karma ifadeler:** Belirli günler, belirli saatler, gün+saat kombinasyonları; tam cron seçeneklerine yakın dinamik form.

**Yapılacaklar:**
- [x] Cron wizard: Tab yapısı (Basit periyot | Karma ifadeler).
- [x] Tab 1: Tek satır [ sayı input ] [ birim dropdown: Saniye, Dakika, Saat ]; ayrı blok: gün/hafta seçenekleri.
- [x] Tab 2: Karma ifadeler UI (belirli günler, belirli saatler, gün+saat); üretilen 6 alanlı cron.
- [x] Her iki tab’da da üretilen ifade 6 alanlı Quartz formatında.
- [x] Var olan cron açıldığında doğru tab ve alanlara parse ederek göstermek.
- [x] Backend/Reactor/DataGateway’de 6 alanlı cron kullanımını doğrulamak.

**Referans:** `Mng.Ui/pages/apps/monitoring/index.vue` — `cronBuilder*`, `cronPresets`, `parseCronIntoBuilder`, `cronBuilderGenerated`; locale anahtarları `monitoring.cronBuilder.*`.

---

## 2. Reactor → MngHub: Engine verisi geldiğinde UI için throttle’lu event

**Hedef:** Engine’den veri (ingest) geldiğinde MngReactor’un MngHub’ın kullanımı için bir event üretmesi; UI’da gereksiz hareket olmaması için **domain bazlı throttle** (örn. 5 saniyede en fazla 1 bildirim).

**Mevcut akış:**
- **Reactor** (`IngestProcessing`): Her metrik için `_metricPublisher.PublishAsync(...)` → RabbitMQ `mng.topics`, routing key `monitoring.metric.inserted.{domain}` (**metrik başına bir mesaj**).
- **MngHub:** `mng.topics` exchange’ine bağlı; bu mesajlar UI’a iletilirse saniyede çok güncelleme olabilir.

---

### Karar: Reactor’da throttle + tek “data.updated” event

**Seçilen yöntem:** Reactor’da ingest başarılı olduktan sonra, **domain bazlı throttle** ile **tek** bir bildirim event’i üretmek. Throttle süresi Reactor config’ten okunacak.

**Tasarım:**

| Öğe | Değer |
|-----|--------|
| **Exchange** | `mng.topics` (mevcut) |
| **Routing key** | `monitoring.data.updated.{domain}` |
| **Payload** | `{ domain, lastIngestAtUtc, engineIds[] }` — UI’ın “yenile” veya veri çekmesi için yeterli |
| **Throttle** | Domain bazlı: bu domain için son **N** saniyede zaten publish yapıldıysa tekrar gönderme. |
| **Konfig** | Reactor `appsettings`: `MngReactorSettings:IngestNotifyThrottleSeconds` (varsayılan örn. 5). |

**Mevcut metrik publish:** Metrik başına `monitoring.metric.inserted.{domain}` publish’e **şimdilik dokunulmayacak** (başka tüketici varsa bozulmasın). MngHub, UI için **sadece** `monitoring.data.updated.{domain}` event’ine subscribe olacak; böylece RabbitMQ’dan gelen “UI’a ilet” mesaj sayısı throttle’a göre azalır. İleride metrik başına publish kaldırılıp sadece data.updated’e geçilebilir.

**Reactor tarafı:**
- Yeni interface: `IIngestNotifyPublisher` (veya mevcut publisher’a ek method): `TryPublishDataUpdatedAsync(domain, lastIngestAtUtc, engineIds, cancellationToken)`.
- Implementasyon: Domain + son publish zamanını in-memory tutar; `IngestNotifyThrottleSeconds` dolmamışsa publish etmez, doluysa RabbitMQ’ya tek mesaj gönderir.
- `IngestProcessing.ProcessAsync` sonunda (MongoDB yazımı ve lastSeenAt güncellemesi tamamlandıktan sonra) bu servisi çağırır: başarılı ingest’te toplanan `engineIds` ve `DateTime.UtcNow` ile.

**MngHub tarafı:**
- `monitoring.data.updated.{domainName}` (veya `monitoring.data.updated.{domain}`) routing key’e subscribe olur.
- Bu event geldiğinde ilgili domain’e bağlı UI bağlantılarına (SignalR) “monitoring data updated” mesajı iletir; UI gerekirse listeyi yeniler veya veri çeker.

---

**Yapılacaklar:**

- [x] **Reactor:** `MngReactorSettings`’e `IngestNotifyThrottleSeconds` ekle (varsayılan 5); opsiyonel `IngestNotifyEnabled` (true/false).
- [x] **Reactor:** `IIngestNotifyPublisher` interface + throttle’lu implementasyon (domain → son publish zamanı; aynı exchange `mng.topics`, routing key `monitoring.data.updated.{domain}`).
- [x] **Reactor:** `IngestProcessing.ProcessAsync` sonunda, başarılı ingest’te `IIngestNotifyPublisher.TryPublishDataUpdatedAsync(domain, UtcNow, engineIds)` çağrısı.
- [x] **MngHub:** RabbitMQ binding’e `monitoring.data.updated.{domainName}` ekle; consumer’da bu event’i işleyip SignalR ile ilgili domain’e “monitoring.data.updated” ilet.
- [ ] **UI:** (İsteğe bağlı; sonra yapılabilir.) Gerekirse Hub’tan gelen “monitoring data updated” event’ine abone olup harita/dashboard/listeyi yenileme veya veri çekme.

*Not: Hub veya UI’da throttle alternatifleri değerlendirildi; RabbitMQ ve Hub’a giden mesaj sayısını azaltmak için throttle’ın Reactor’da ve tek “data.updated” event’i ile yapılması kararlaştırıldı.*

**Referans:** `MngReactor/.../IngestProcessing.cs`, `MetricPublisher.cs`; MngHub RabbitMQ consumer, SignalR.

**Detaylı uygulama planı:** [MADDE_2_INGEST_NOTIFY_PLAN.md](MADDE_2_INGEST_NOTIFY_PLAN.md) — adım adım dosya yolları, interface/payload, throttle mantığı, Reactor + MngHub + UI sırası.

**Durum:** ✅ Tamamlandı (Reactor: config, IIngestNotifyPublisher, IngestProcessing; MngHub: RoutingKeyHelper, MessageRouter. UI isteğe bağlı.)

---

## 3. Gelen veriler üzerinde örnek dashboard’lar + harita widget

**Hedef:** Gelen (ingest) verilere dayalı **örnek dashboard’lar** hazırlamak; ayrıca widget türlerine **harita widget’ı** ekleyerek dinamik dashboard ekranına harita bileşeni koyabilmek.

**Örnek dashboard’lar:**
- Gelen veriler (metrikler, tren/asset konumları vb.) üzerinden hazır şablon veya örnek dashboard’lar tanımlanacak.
- Kullanıcılar bu örnekleri referans alıp kendi dashboard’larını oluşturabilecek.

**Yeni widget türü: Harita widget’ı**
- Mevcut widget türleri (chart, card vb.) yanına **harita (map) widget** eklenecek.
- **Seçim:** Widget’ta **bir veya birden fazla tren** (veya konum verisi olan asset) seçilebilecek.
- **Yerleşim:** Bu widget, mevcut **dinamik dashboard** ekranına (dashboard layout’a) diğer widget’larla birlikte eklenebilecek; yani dashboard sayfasında bir “kutucuk” olarak harita görünecek.

**Yapılacaklar (özet):**
- [ ] Örnek dashboard’lar: Gelen veri yapısına uygun örnek dashboard tanımları (ve gerekirse seed/import).
- [ ] Widget türü: “Harita” tipinin monitoring widget tip listesine eklenmesi (backend/veri modeli + UI).
- [ ] Harita widget formu: Tren/asset seçimi (tek veya çoklu); seçilen varlıkların haritada gösterilmesi.
- [ ] Dashboard layout’a harita widget’ının eklenebilmesi ve render edilmesi (WidgetRenderer / dashboard bileşenleri).

**Referans:** Mevcut monitoring widget’lar (`MonitoringWidgetForm.vue`, widget tipi: chart/card); dashboard sayfası ve layout renderer; harita bileşenleri (MonitoringMapView, konum verisi kaynağı).

---

## 4. Alarm yönetimi: MQTT event’leri → Engine → Reactor → Hub → UI (global alarm modal)

**Hedef:** MngSim’in MQTT üzerinden ürettiği event’leri sisteme almak; bir **asset türü** ile MQTT topic vb. tanımlanabilmesi; MngEngine’in MQTT event’ini yakalayıp anlık olarak MngReactor’a göndermesi; MngReactor’un alarm durumunu MngHub üzerinden UI’a iletmesi; UI’da **hangi sayfada olursa olsun** kullanıcıya bir **modal** ile alarm/hatanın gösterilmesi. Bu madde “Alarm yönetimi” başlığı altında sırası geldiğinde detaylandırılacak.

**Akış (özet):**
1. **MngSim:** MQTT üzerinden event üretir (mevcut veya eklenecek).
2. **Asset türü tanımı:** MQTT topic (veya benzeri) içeriğinin tanımlanabildiği bir asset türü / konfigürasyonu.
3. **MngEngine:** MQTT türündeki event’i dinleyebilir (subscribe), yakalar ve **anlık** olarak MngReactor’a iletir.
4. **MngReactor:** Gelen alarm/event’i işler ve MngHub üzerinden UI’a iletir (örn. mevcut SignalR / event kanalı).
5. **MngHub:** Alarm bildirimini ilgili domain’e bağlı UI istemcilerine push eder.
6. **UI:** Kullanıcı **hangi sayfada olursa olsun** bir **modal** (veya benzeri global bileşen) ile alarm/hatayı gösterir.

**Yapılacaklar (özet; detay sırası geldiğinde netleştirilecek):**
- [ ] Asset türü / konfig: MQTT topic (veya topic pattern, payload şeması vb.) tanımlanabilir alanlar.
- [ ] MngEngine: MQTT client / subscriber; event geldiğinde Reactor’a anlık iletim (REST veya mevcut kanal).
- [ ] MngReactor: MQTT kaynaklı alarm/event’i kabul etme, işleme, Hub’a iletme (routing/event yapısı).
- [ ] MngHub: Alarm event’ini UI’a push etme (SignalR veya mevcut bildirim kanalı).
- [ ] UI: Global alarm modal (layout/root seviyesinde); Hub’dan gelen alarmı hangi sayfada olunursa olunsun gösterme.

**Not:** Bu madde karışık ve çok bileşenli; “Alarm yönetimi” sırası geldiğinde birlikte adım adım detaylandırılacak.

**Referans:** MngSim MQTT event üretimi; mevcut asset type / connection_info yapıları; Engine ingest/Reactor/Hub/UI event akışı.

---

## 5. Grafana kurulumu ve mon_metrics için dashboard

**Hedef:** **Grafana** kurulumu yapılması ve ürettiğimiz verilerin tutulduğu **mon_metrics** collection’ı kullanarak Grafana’da dashboard oluşturabilme. Sırası geldiğinde birlikte netleştirilecek.

**Yapılacaklar (özet; detay sırası geldiğinde netleştirilecek):**
- [ ] Grafana kurulumu (deploy, konfig, erişim).
- [ ] mon_metrics veri kaynağı: Grafana’nın veriyi okuyacağı kaynak (MongoDB / DataGateway / başka bir arayüz) ve bağlantı.
- [ ] mon_metrics collection’ına uygun dashboard’lar oluşturulabilmesi (sorgu, panel, örnek dashboard’lar).

**Not:** Detaylar (veri kaynağı seçimi, örnek sorgular, panel tipleri vb.) sırası geldiğinde netleştirilecek.

**Referans:** mon_metrics collection yapısı; mevcut DataGateway/MongoDB erişim.

---

## 6. Workflow: Veri koşullarına göre aksiyonlar (MngSim dışı veriler)

**Hedef:** **Workflow** konusunu yeniden gündeme almak. MngSim üzerinden gelen event’ların dışında, **alınan verilerin** belirli bir değere **eşit** veya **dışında** (örn. eşik üstü/altı, aralık dışı) olduğunda **neler yapılacağının** tanımlanabilmesi. Sırası geldiğinde birlikte netleştirilecek.

**Kapsam (özet):**
- Workflow altyapısı / mevcut MngWorkflow ile ilişki.
- Koşul: Gelen veri (metrik, collectible vb.) belirli bir değerin eşiti veya dışında (eşik, aralık).
- Aksiyon: Koşul sağlandığında yapılacak işlemler (bildirim, alarm, başka servis tetikleme vb.).

**Not:** Detaylar (hangi veri kaynağı, koşul ifadesi, aksiyon türleri, MngSim event’larından farkı vb.) sırası geldiğinde netleştirilecek.

**Referans:** MngWorkflow; mon_metrics / ingest veri yapısı; mevcut alarm/event akışı.

---

## 7. ReportingManager

**Hedef:** **ReportingManager** konusunda çalışmalara başlamak. Raporlama altyapısı, rapor tanımları, zamanlamalar ve çıktılar (PDF, e-posta vb.) sırası geldiğinde netleştirilecek.

**Yapılacaklar (özet; detaylar çalışma sırasında eklenecek):**
- [ ] ReportingManager kapsamı ve gereksinimler.
- [ ] Rapor şablonları / veri kaynakları (örn. mon_metrics, dashboard verileri).
- [ ] Zamanlama ve dağıtım (periyodik rapor, e-posta, export).
- [ ] UI ve entegrasyon (mevcut monitoring / dashboard ile ilişki).

**Referans:** Mevcut MngWorkflow, MngScheduler, MngNotifier; mon_metrics ve dashboard veri yapısı.

---

## 8. Notification (bildirim) — derinlemesine tartışma ve implementasyon

**Hedef:** **Notification** konusunu derinlemesine tartışmak; kararlar alındıktan sonra implementasyona başlamak.

**Planlanan adımlar:**
1. **Tartışma:** Bildirim kanalları (e-posta, push, in-app, SMS vb.), tetikleyiciler (alarm, workflow, eşik), hedef kitle/gruplar, mevcut MngNotifier ve diğer servislerle ilişki.
2. **Kararlar:** Mimari, veri modeli, entegrasyon noktaları ve önceliklerin netleştirilmesi.
3. **Implementasyon:** Alınan kararlara göre geliştirme adımlarının uygulanması.

**Not:** Önce tartışma ve karar aşaması; implementasyon sonrasında başlayacak.

**Referans:** MngNotifier; MngHub (SignalR / in-app); alarm ve workflow maddeleri.

---

## Chart widget — Mevcut durum (sonra devam)

**Tarih:** 8 Mart 2026  
Chart ile ilgili çalışmalara sonra devam edilecek; bulunulan nokta aşağıda ve ilgili dosyalarda kayıtlı.

### Tamamlananlar

- **Chart tipleri:** Line, bar, area, **pie**, **donut** — Monitoring widget formunda seçilebilir; ChartWidget ApexCharts ile destekliyor.
- **Çoklu seri (multi-series):** Aynı grafikte birden fazla asset; formda çoklu asset seçildiğinde bilgi metni (`chartHintMultiSeries`); veri `widgetDataService.pivotMonMetricsForMultiSeries` ile pivotlanıyor; legend’da seri isimleri gösteriliyor.
- **Zaman aralığı:** Widget üzerindeki **çark (⚙) menüsü** ile zaman aralığı (son 20 dk, 1s, 6s, 24s, 7g, tümü) ve limit/yenileme seçilebiliyor; ayrı preset butonları yok (gereksiz bulundu).
- **Anlamlı widget seçimi rehberi:** `DASHBOARD_WIDGET_PLAN.md` §3.4 — Veri türüne göre widget/chart tipi önerisi (zaman serisi → Chart line/area; anlık sayı → Card; boolean → Card; konum → Harita; gauge → Gauge; oran/dağılım → Chart pie/donut).

### Referans dosyalar

| Konu | Dosya |
|------|--------|
| Plan / chart sonraki adımlar | `docs/content/monitoring_plans/CHART_OPTIONS_NEXT.md` |
| Anlamlı widget seçimi tablosu | `docs/content/monitoring_plans/DASHBOARD_WIDGET_PLAN.md` §3.4 |
| Chart bileşeni | `Mng.Ui/components/widgets/chart/ChartWidget.vue` |
| Monitoring widget formu | `Mng.Ui/components/apps/monitoring/MonitoringWidgetForm.vue` |
| Widget ayarları (çark menüsü) | `Mng.Ui/components/dashboards/WidgetWithSettings.vue` |
| Veri / multi-series pivot | `Mng.Ui/services/widgetDataService.ts` |

### Sonra devam edilecekler (CHART_OPTIONS_NEXT.md’den)

- Chart tipi: radar, scatter (ApexCharts imkânına göre).
- Eksen/etiket: X/Y etiketleri, birim, tarih formatı iyileştirmesi.
- Veri yoğunluğu: Limit, örnekleme/aggregation ile performans.
- Dışa aktarma: Grafik PNG veya veri CSV indirme.

---

## 9. (Sonraki işler buraya eklenecek)

*Kullanıcı sıradaki işleri anlattıkça madde madde eklenecek.*

---

## Notlar

- Cron wizard aynı modal hem **Toplama periyotları** (collection period expression) hem **Engine → Veri gönderim cron** (sendSchedule) için kullanılıyor.
- İsteğe bağlı: Wizard’ı ayrı bir bileşen (örn. `CronBuilderModal.vue`) olarak çıkarıp monitoring dışında da kullanılabilir hale getirmek.
