# Güvenlik Merkezi (SIEM) — Modül özellik envanteri

**Kod:** `siem-center` · **Durum:** Canlı (SIEM-hafif MVP)  
**UI:** `/apps/siem-center` · **Alarm operasyonu:** `/apps/alarm-center` · **Backend:** MngReactor (ingest) · MngAlarm (kurallar, lifecycle)

**Referanslar:** [SIEM planlama (iç)](../../odak/monitoring/SIEM_PLANNING.md) · [SIEM DEVAM](../../odak/monitoring/DEVAM.md) · Referans teklif: **Monitoring §4.3 kapsam dışı** — SIEM ayrı ürün katmanı

> **Bu dosyanın amacı (şu an):** Güvenlik Merkezi’nin **müşteri perspektifi**, **Monitoring’den ayrımı**, Alarm Merkezi rolü ve fonksiyon envanteri.

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi · 📋 Vizyon / teklif dışı genişleme

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Güvenlik Merkezi (SIEM)**, kurumun **güvenlik olaylarını** — firewall, sunucu, kimlik, uygulama logları — merkezileştirip arayan; **hedefli kurallarla** alarm üreten; operatörün olay ve alarm **kuyruğunu yönettiği** modüldür.

### 1.2 Konumlandırma: «SIEM-hafif»

MonitraNG SIEM’i **kurumsal Splunk/QRadar ikamesi** iddiasıyla değil; **hedefli senaryolar** (use case paketleri), on-prem dağıtım ve platform bütünleşmesi (OC, Workflow, Notifier) ile konumlanır.

| | **Tam SIEM beklentisi** | **MonitraNG SIEM-hafif** |
|--|-------------------------|---------------------------|
| Kapsam | Sınırsız log kaynağı, SOAR | Seçili kaynak + parser + kural paketleri |
| Tespit | ML/UEBA ağırlıklı | Kural motoru (threshold, correlation, sequence…) |
| AI | Her yerde | ⏸️ Platform kararı — ayrı plan |
| Dağıtım | SaaS / appliance | **On-prem**, tenant içi |

### 1.3 Monitoring vs SIEM vs Alarm Merkezi

| Bileşen | Rol | UI |
|---------|-----|-----|
| **Monitoring** | Operasyonel metrik, uptime, sensör | `/apps/monitoring` |
| **Güvenlik Merkezi** | Güvenlik olayı arama, panel | `/apps/siem-center` |
| **Alarm Merkezi** | Alarm kuyruğu, kural yönetimi, lifecycle | `/apps/alarm-center` |

**Alarm Merkezi** ayrı pazarlama modülü değil; **Güvenlik Yönetimi** menüsü altında SIEM operatör yüzeyidir. Broşürde «Güvenlik Merkezi + Alarm Merkezi» birlikte anlatılabilir.

### 1.4 SIEM ne değildir?

| Beklenti | Gerçek |
|----------|--------|
| Sunucu CPU / disk izleme | **Monitoring** |
| Her log satırında sınırsız ML | Hedefli kurallar + planlı AI |
| Otomatik olay kapatma garantisi | Operatör lifecycle (ack / suppress / resolve) |

---

## 2. Müşteri perspektifi

### 2.1 Tek paragraf (broşür / sunum)

**Güvenlik Merkezi**, dağınık firewall ve sunucu loglarını tek platformda toplar. Güvenlik ekibi olayları arar, panelde özet görür; kurallar eşleştiğinde **alarm** oluşur. Operatör Alarm Merkezi’nde alarmı inceler, onaylar veya çözer; gerekirse **Operasyon Merkezi**’nde takip kaydı açılır. Operasyonel «sunucu down» ile güvenlik «yetkisiz giriş denemesi» aynı üründe karışmaz — biri Monitoring, diğeri SIEM.

### 2.2 Günlük deneyim

| Adım | Müşteri dili |
|------|----------------|
| 1 | Log kaynağı platforma akar (syslog, WEF→WEC, agent…) |
| 2 | Olaylar normalize edilir (`sec_events`) |
| 3 | Kural eşleşir → **alarm** |
| 4 | Operatör **Alarm Merkezi**’nde listeler, filtreler |
| 5 | Detay + timeline; acknowledge / resolve |
| 6 | **Güvenlik paneli**nde trend ve özet |
| 7 | **Olay arama** — tarih, filtre, detay inceleme |

### 2.3 Platform bağlantıları

| Modül | İlişki |
|-------|--------|
| **Operasyon Merkezi** | SOC WorkItem, olay triage *(plan / entegrasyon)* |
| **Workflow** | Olay → otomasyon adımı *(plan)* — [SIEM_WORKFLOW_SEAM.md](../../odak/monitoring/SIEM_WORKFLOW_SEAM.md) |
| **Raporlama** | Güvenlik özet raporu |
| **Notifier** | Alarm bildirimi |
| **Monitoring** | **Karıştırılmaz** — operasyon metrik ayrı |

---

## 3. Temel kavramlar

| Kavram | Tanım |
|--------|--------|
| **sec_events** | Normalize güvenlik olayı kaydı |
| **Parser / normalizer** | Ham log → alanlı olay |
| **Alarm kuralı** | Threshold, correlation, scheduled, sequence… |
| **Alarm** | Kural tetiklenince operatör kuyruğuna düşen kayıt |
| **Lifecycle** | acknowledge · suppress · resolve |
| **Use case (U1–U7)** | Hedefli senaryo paketleri *(iç plan)* |
| **Forwarder** | Syslog, WEF→WEC, agent toplama |

**Veri hattı (özet):**

```text
Log kaynağı → ingest (Reactor) → sec_events
       → kural motoru → alarm → Alarm Merkezi
       → SIEM panel / olay arama
```

---

## 4. Fonksiyon envanteri

### 4.1 Toplama ve ingest

| Yetenek | Durum | Not |
|---------|-------|-----|
| Syslog collector / listener | ✅ | Engine/Reactor — tam syslog sunucusu değil |
| WEF → WEC → HTTP batch ingest | ✅ | Windows forwarder şablonları |
| Linux rsyslog forwarder | ✅ | Dokümante |
| Agent tabanlı toplama | 🔶 | Hibrit model |
| Parser pipeline (`firewall.vendor.v1` vb.) | ✅ | FortiGate örneği |
| Throughput / kuyruk planı | 📋 | Yoğun veri |

### 4.2 Olay arama ve panel (UI)

| Yetenek | Durum | Not |
|---------|-------|-----|
| SIEM Güvenlik Paneli (dashboard) | ✅ | `/apps/siem-center` — prod perf iyileştirmesi ✅ |
| Olay arama (sec_events) | ✅ | `/apps/siem-center/events` — pagination, filtre, auto-refresh |
| Olay detay paneli | ✅ | |
| Referans / yardım sayfası | ✅ | `/apps/siem-center/reference` |

### 4.3 Alarm Merkezi (UI)

| Yetenek | Durum | Not |
|---------|-------|-----|
| Açık alarmlar listesi | ✅ | Server pagination, filtreler |
| Alarm detay + context timeline | ✅ | |
| Lifecycle: acknowledge / suppress / resolve | ✅ | API + UI |
| Alarm geçmişi | ✅ | Tarih aralığı, durum filtresi |
| Kurallar sekmesi / ayrı rota | ✅ | `/apps/alarm-center/rules` |
| Kural tipleri: threshold, correlation, scheduled, sequence | ✅ | U2 sequence preset |
| Bildirim politikaları | 🔶 | `/apps/alarm-center/notification-policies` |
| Smoke / regresyon scriptleri | ✅ | `scripts/odak/test-siem-*.ps1` |

### 4.4 Kural motoru ve tespit

| Yetenek | Durum | Not |
|---------|-------|-----|
| Threshold kural | ✅ | |
| Correlation | ✅ | |
| Scheduled kural | ✅ | |
| Sequence kural | ✅ | Adım düzenleme UI kısıtlı 🔶 |
| Observation map (Faz 2) | ✅ | U1–U7 eşlemesi |
| LogAlarm parite yol haritası | 📋 | Referans kıyas — hedef değil |

### 4.5 Entegrasyon ve genişleme

| Yetenek | Durum | Not |
|---------|-------|-----|
| OC WorkItem köprüsü | 🔲 | SOC triage senaryosu |
| Workflow tetik | 🔲 | Seam dokümante |
| WORM / 5651 uyumluluk spike | 📋 | Finans / kamu |
| Dikey: finans / savunma notları | 📋 | İç plan |
| SIEM AI | ⏸️ | Platform AI kararı |

### 4.6 Bilinçli sınırlar

- **Monitoring metrikleri** SIEM’de değil
- Sequence kural **düzenleme** backend sınırı (yeni kural oluşturma)
- Eski alarmlarda lifecycle timeline backfill yok
- Tam enterprise SIEM + SOAR iddiası yok

---

## 5. Gerçek hayat örnekleri

| # | Senaryo | SIEM rolü |
|---|---------|-----------|
| 1 | Firewall’ta brute force denemesi | Kural → alarm → operatör ack |
| 2 | Windows admin grubu değişikliği | WEF olayı → sequence kural |
| 3 | Gece yoğun alarm dönemi özeti | Panel + *(plan)* AI özet |
| 4 | Denetçiye «şu IP ne zaman görüldü?» | Olay arama |
| 5 | SOC analisti triage | Alarm Merkezi → *(plan)* OC kaydı |
| 6 | FortiGate trafik anomalisi | Parser + correlation |

### Sektörel

| Sektör | Örnek use case |
|--------|----------------|
| Bankacılık / finans | Yetkisiz erişim, 5651 log bütünlüğü *(plan)* |
| Savunma | OT–IT sınırı, firewall olayları |
| Üretim | **Operasyon sensörü Monitoring’de**; güvenlik olayı SIEM’de |
| Kamu | Merkezi log toplama, hedefli kural paketi |

---

## 6. Kimler kullanır?

| Rol | Kullanım |
|-----|----------|
| **SOC / güvenlik operasyon** | Alarm kuyruğu, olay arama |
| **Güvenlik mühendisi** | Kural tanımı, parser |
| **Uyum / denetim** | Olay arşivi, export *(Raporlama)* |
| **IT yönetim** | Özet panel |
| **Platform admin** | Forwarder, ingest sağlığı |

---

## 7. Referans teklif notu

Referans **İzleme (Monitoring)** paketinde **SIEM açıkça kapsam dışıdır**. SIEM-hafif MVP **platform ürünü** olarak canlıdır; ayrı ticari paketleme müşteri bazında tanımlanır.

---

## 8. Teknik referans (iç kullanım)

| Alan | Konum |
|------|--------|
| UI SIEM | `Mng.Ui/pages/apps/siem-center/` |
| UI Alarm | `Mng.Ui/pages/apps/alarm-center/` · `components/apps/alarm-center/` |
| Alarm API | `MngAlarm/` |
| Ingest | `MngReactor/` |
| Dokümantasyon | `docs/odak/monitoring/SIEM_*.md` |

---

## Broşür (ertelendi)

Taslak: [platform-tanitimi.md § Güvenlik Merkezi](./platform-tanitimi.md)

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · Ürün kimliği v0.1*
