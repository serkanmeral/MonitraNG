# Monitoring — Modül özellik envanteri

**Kod:** `monitoring` · **Durum:** Canlı (altyapı + UI); teklif kapsamı genişletme devam ediyor  
**UI:** `/apps/monitoring` · **Backend:** MngEngine (edge) · MngReactor (sunucu) · DG `mon_*` dataset’leri

**Referanslar:** [Monitoring Faz 3 roadmap](../../monitrang/faz3/monitoring/Roadmap.md) · [Monitoring planlama (mimari)](../../content/monitoring_plans/monitrang_monitoring_planlama.md) · [Referans teklif §4.3 (iç)](../../odak/commercial/Odak_Kompozit_Fiyat_Teklifi.md)

> **Bu dosyanın amacı (şu an):** Monitoring modülünün **müşteri perspektifi**, **SIEM’den ayrımı** ve fonksiyon envanteri. Broşür metinleri **henüz doldurulmayacak** — bkz. [§Broşür (ertelendi)](#broşür-ertelendi).

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi · 📋 Teklifte tanımlı, geliştirilmedi

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Monitoring**, kurumun **operasyonel sağlığını** — sunucu, servis, veritabanı, ağ, saha sensörü ve benzeri **varlıkları** — merkezi envanterden izleyen; metrik toplayan, dashboard ve **eşik alarm** ile erken uyarı veren; üretim ve operasyon süreçlerine veri sağlayan modüldür.

### 1.2 Monitoring vs Güvenlik Merkezi (SIEM)

| | **Monitoring** | **Güvenlik Merkezi (SIEM)** |
|--|----------------|------------------------------|
| **Odak** | Operasyonel metrik, uptime, sensör, kapasite | Güvenlik logu, tehdit, uyumluluk olayı |
| **Veri** | Metrik, durum, saha sinyali | `sec_events`, syslog/WEF, parser |
| **Alarm** | Eşik / anomaly *(plan)* operasyon alarmı | Güvenlik kuralı, korelasyon, sequence |
| **UI** | `/apps/monitoring` | `/apps/siem-center` + **Alarm Merkezi** |
| **Referans teklif** | §4.3 İzleme | **Kapsam dışı** (ayrı ürün katmanı) |

**Alarm Merkezi** (`/apps/alarm-center`): Operatörün **alarm kuyruğunu** yönettiği yüzey; güvenlik alarmları ağırlıklı (**SIEM** hattından). Monitoring operasyon alarmları aynı bildirim omurgasını paylaşabilir — pazarlama dilinde **«operasyon alarmı»** vs **«güvenlik alarmı»** ayrımı net tutulur. Ayrıntı: [modul-siem-center.md](./modul-siem-center.md).

### 1.3 Monitoring ne değildir?

| Beklenti | Gerçek |
|----------|--------|
| Tam SIEM / log korelasyon suite | **Operasyonel izleme** — SIEM ayrı modül |
| Tam MES / fabrika çizelgeleme | Metrik + alarm + OC köprüsü; ağır MES değil |
| APM (uygulama trace) odaklı observability | Metrik + asset modeli; derin APM ayrı sınıf |
| Erişilemeyen kaynaktan «her zaman veri» | **Erişim tanımlı ve doğrulanmış** kaynak kuralı |

---

## 2. Müşteri perspektifi

### 2.1 Tek paragraf (broşür / sunum)

**Monitoring**, altyapınızın ve saha cihazlarınızın **canlı nabzını** tek platformda toplar. Hangi sunucu, sensör veya servisin izlendiği envanterde bellidir; metrikler toplanır, panellerde görünür; eşik aşıldığında **alarm** oluşur ve doğru kişiye bildirim gider. Üretim hattındaki sıcaklık, veritabanının yanıt süresi veya web servisinin erişilebilirliği — operasyon ekibi sorunu **kullanıcı şikâyet etmeden** fark edebilir. Gerektiğinde aynı veri **Operasyon Merkezi** iş emrine bağlanır.

### 2.2 Günlük deneyim

| Rol | Müşteri dili |
|-----|----------------|
| **NOC / operasyon** | Dashboard’da kırmızı alarm; metrik grafiğine in; onayla / not düş |
| **Altyapı / tesis** | Engine ve agent durumu; hangi varlık offline |
| **Üretim** | Hat sensörü canlı; emir kartında metrik *(OC köprüsü)* |
| **Yönetim** | Özet panel; trend ve kapasite uyarısı *(AI plan)* |

### 2.3 Platform içindeki yeri

| Bağlantı | Müşteri cümlesi |
|----------|-----------------|
| **Operasyon Merkezi** | Alarm üretim emrine not düşer; emirde sensör şeridi |
| **Raporlama** | Metrik / alarm geçmişi raporlanabilir |
| **Notifier** | E-posta, Telegram, uygulama içi |
| **Simulator** *(plan)* | Sensör yokken demo senaryosu |
| **Workflow** *(plan)* | Alarm → otomatik adım |

### 2.4 Müşteriye net sınırlar

| Beklenti | Gerçek |
|----------|--------|
| «Firewall log analizi burada» | **SIEM** modülü |
| «Her protokol kutudan hazır» | MQTT, OPC UA, kamera vb. **keşif + bağlayıcı** |
| «Simulator gerçek sensör» | Demo / eğitim — production yerine geçmez |

---

## 3. Amaç ve temel kavramlar

### 3.1 Sorun

- İzleme araçları parçalı; envanter Excel’de
- Alarm e-posta flood; kök neden geç anlaşılır
- OT (saha) ile IT (sunucu) verisi aynı ekranda değil
- Üretim operatörü sensörü ayrı sistemden izler

### 3.2 Kavramlar

| Kavram | Tanım |
|--------|--------|
| **Asset (varlık)** | İzlenen somut kaynak — sunucu, DB, sensör, HTTP uç… |
| **Asset type / family** | Toplama yöntemi ve metrik şablonu |
| **Item / organizasyon** | Varlıkların lokasyon hiyerarşisi (bölge → oda → kabin…) |
| **Engine** | Edge’de çalışan toplama cihazı (MngEngine) |
| **Agent** | Engine’e bağlı toplama görevi grubu |
| **Collectible** | Toplanan metrik / sinyal tanımı |
| **Widget / dashboard** | Görselleştirme (harita, gauge…) |
| **Eşik alarm** | Metrik kuralı → alarm → bildirim |
| **Anomaly** *(plan)* | Eşik dışı olağandışı davranış — Monitoring AI |

**Mimari özet:**

```text
Asset envanteri (DG mon_*)
  → Engine (edge) / Agent toplama
  → MngReactor ingest
  → Metrik depolama · widget · alarm
  → Bildirim · OC · Raporlama
```

---

## 4. Fonksiyon envanteri

### 4.1 Envanter ve yapılandırma (UI — canlı)

| Yetenek | Durum | Not |
|---------|-------|-----|
| Organizasyon ağacı (`mon_items`) | ✅ | `/apps/monitoring/organization` |
| Engine tanımı, durum, config string | ✅ | |
| Agent tanımı, engine ataması | ✅ | |
| Toplama periyotları (cron) | ✅ | |
| İzleme aralıkları (schedule) | ✅ | |
| İzleme yapılandırması | ✅ | `/apps/monitoring/config` |
| Kontrol merkezi (engine/agent sağlık) | ✅ | `/apps/monitoring/control` |
| Asset type family / type / asset CRUD | 🔶 | Model + UI kısmen; olgunluk artıyor |

### 4.2 Görselleştirme

| Yetenek | Durum | Not |
|---------|-------|-----|
| Monitoring widget’ları (liste, düzenle) | ✅ | `/apps/monitoring/widgets` |
| Harita widget | ✅ | `/apps/monitoring/map` |
| Gauge widget tipi | ✅ | |
| Operasyon dashboard seti (teklif) | 📋 | Widget + teklif dashboard paketi |

### 4.3 Toplama — referans teklif kapsamı

| Kaynak | Durum | Not |
|--------|-------|-----|
| Windows sunucu metrikleri | 📋 | §4.3.2 — MON-1 |
| Linux sunucu metrikleri | 📋 | |
| MongoDB, Oracle, SQL Server, PostgreSQL | 📋 | §4.3.3 — MON-2 |
| Windows Service / systemd / process | 📋 | §4.3.4 |
| HTTP health / ping | 📋 | §4.3.5 |
| MQTT, SNMP, TCP, OPC UA | 📋 | §4.3.6 — MON-3 |
| Güvenlik kamerası alarm yakalama | 📋 | §4.3.7 — protokol keşifte |
| Engine → Reactor ingest hattı | 🔶 | Mimari plan; olgunluk artıyor |
| Erişilemeyen kaynak kuralı | 📋 | Teklif zorunluluğu |

### 4.4 Alarm ve bildirim (operasyon)

| Yetenek | Durum | Not |
|---------|-------|-----|
| Eşik tabanlı operasyon alarmı | 🔶–📋 | Teklif §4.3.8; güvenlik alarmları SIEM’de ✅ |
| In-app / e-posta / Telegram | 📋 | Notifier omurgası ✅ |
| Alarm → OC WorkItem notu | 📋 | Üretim referans senaryosu |
| Alarm Merkezi UI | ✅ | Ağırlıklı SIEM — bkz. SIEM dokümanı |

### 4.5 Monitoring AI (referans teklif §4.3.9)

| # | Yetenek | Durum |
|---|---------|-------|
| 1 | Anomaly detection | 📋 |
| 2 | Alarm açıklaması | 📋 |
| 3 | Kök neden önerisi | 📋 |
| 4 | Alarm gürültü azaltma | 📋 |
| 5 | Doğal dil sorgu | 📋 |
| 6 | Kapasite / trend notu | 📋 |
| 7 | Sensör / kamera alarm özeti | 📋 |
| 8 | Eşik önerisi | 📋 |

> AI implementasyonu platform kararına bağlı; teklifte **standart** olarak tanımlı.

### 4.6 Üretim ve OC köprüsü

| Yetenek | Durum | Not |
|---------|-------|-----|
| Emir kartında canlı sensör | 📋 | Referans paket — OC §8 |
| Alarm → emre otomatik not | 📋 | |
| Emirden metrik deep link | 📋 | |
| Simulator (sensör yoksa demo) | 📋 | [MONITORING_SIMULATOR.md](../../content/monitoring_plans/MONITORING_SIMULATOR.md) |

### 4.7 Bilinçli sınırlar

- Tam **MES** yok
- **SIEM** / güvenlik log analizi ayrı modül
- Simulator production sensörün yerine geçmez

---

## 5. Gerçek hayat örnekleri

| # | Senaryo | Monitoring rolü |
|---|---------|-----------------|
| 1 | Disk doluluk %90 | Eşik alarm → mail |
| 2 | Web API yanıt vermiyor | HTTP check kırmızı |
| 3 | Fırın sıcaklığı limit üstü | MQTT sensör → alarm → OC emir notu |
| 4 | Şube sunucusu offline | Engine/agent sağlık paneli |
| 5 | Soğuk zincir sıcaklık sapması | Saha sensör + anomaly *(plan)* |
| 6 | Veritabanı yavaşladı | DB metrik *(plan)* → NL sorgu *(plan)* |
| 7 | Haritada bölgesel asset durumu | Map widget |
| 8 | Vardiya özeti Telegram | Bildirim + özet *(plan)* |

### Sektörel özet

| Sektör | Örnek |
|--------|--------|
| Üretim | Hat sensörü, OPC UA, kamera alarmı |
| Enerji / tesis | SCADA benzeri metrik, bakım tetik |
| Lojistik | Soğuk depo, ping |
| Bankacılık | Sunucu/DB operasyonel sağlık *(SIEM ayrı)* |
| Savunma | OT–IT sınırında operasyonel asset izleme |

---

## 6. Platform bağlantıları

| Modül | İlişki |
|-------|--------|
| **MngEngine / MngReactor** | Toplama ve ingest |
| **DataGateway** | `mon_*` envanter ve metadata |
| **Operasyon Merkezi** | Alarm → WorkItem; emirde metrik |
| **Raporlama** | Metrik/alarm raporu |
| **Notifier** | Bildirim kanalları |
| **Güvenlik Merkezi** | **Ayrı** — log/olay; karıştırılmaz |
| **Scheduler** *(plan)* | Periyodik kontrol / özet |

---

## 7. Referans teklif eşlemesi (iç kullanım)

| Referans paket (§4.3) | Bu doküman |
|-----------------------|------------|
| Envanter + toplama yöntemi | §4.1 · §4.3 |
| Host / DB / servis / ağ / saha / kamera | §4.3 |
| Dashboard, eşik, bildirim | §4.2 · §4.4 |
| Monitoring AI | §4.5 |
| Üretim köprüsü | §4.6 · OC dokümanı |
| SIEM hariç | §1.2 |
| Simulator | §4.6 |

---

## 8. Teknik referans (iç kullanım)

| Alan | Konum |
|------|--------|
| UI | `Mng.Ui/pages/apps/monitoring/` |
| Mimari planlar | `docs/content/monitoring_plans/` |
| Faz 3 | `docs/monitrang/faz3/monitoring/` |
| Engine | `MngEngine/` |
| Reactor | `MngReactor/` |

---

## Broşür (ertelendi)

Taslak: [platform-tanitimi.md § Monitoring](./platform-tanitimi.md)

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · Ürün kimliği v0.1*
