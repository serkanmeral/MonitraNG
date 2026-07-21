# MonitraNG — Modül dokümantasyon indeksi

Broşür, landing page, DI (Pazarlama/Docs) ve sektörel sayfalar için **modül kaynak kitaplığı**.

**Ana özet:** [platform-tanitimi.md](./platform-tanitimi.md)  
**Bağlantı haritası:** [../Files/monitrang-modul-baglanti-haritasi.svg](../Files/monitrang-modul-baglanti-haritasi.svg)  
**Modül şablonu:** [modul-sablon.md](./modul-sablon.md)

---

## Katmanlar

| Katman | Bileşen | Pazarlama rolü | Doküman |
|--------|---------|----------------|---------|
| Omurga | Keeper, DataGateway, Notifier | Kimlik, veri, bildirim | [modul-platform-omurgasi.md](./modul-platform-omurgasi.md) |
| Zamanlama | Scheduler | Çapraz tetikleyici | [modul-scheduler.md](./modul-scheduler.md) |
| Veri yüzeyleri | Dinamik Form, Widget / Dashboard | Schema tabanlı form + panel | [modul-dinamik-form-ve-dashboard.md](./modul-dinamik-form-ve-dashboard.md) |
| Hub — Belge | Döküman Zekası | Merkezi belge üretimi | [modul-document-intelligence.md](./modul-document-intelligence.md) · **ürün perspektifi tamam** |
| Hub — Orkestrasyon | Workflow | İç akış + dış HTTP flow + **Kanal Akışları** *(alt)* | [modul-workflow.md](./modul-workflow.md) §7 |

---

## Platform modülleri (broşür / landing)

| # | Modül | Kod | Durum | UI (Mng.Ui) | Detay dosyası |
|---|-------|-----|-------|-------------|---------------|
| 1 | Döküman Zekası | `document-intelligence` | Canlı | `/apps/document-intelligence` | `modul-document-intelligence.md` |
| 2 | Operasyon Merkezi (OC) | `operation-core` | Canlı | `/apps/operation-core` | [modul-operation-core.md](./modul-operation-core.md) |
| 3 | Raporlama | `reporting` | Canlı | `/apps/reporting` | [modul-reporting.md](./modul-reporting.md) |
| 4 | Monitoring | `monitoring` | Canlı | `/apps/monitoring` | [modul-monitoring.md](./modul-monitoring.md) |
| 5 | Güvenlik Merkezi (SIEM) | `siem-center` | Canlı | `/apps/siem-center` | [modul-siem-center.md](./modul-siem-center.md) |
| 6 | Workflow | `workflow` | Canlı *(backend + W1 UI)* | `/apps/automation-center/workflows` | [modul-workflow.md](./modul-workflow.md) |

---

## UI’da olan, pazarlama modülü olmayan bileşenler

Bunlar ayrı ürün modülü değil; OC / Monitoring / SIEM altında veya müşteriye özel paketlerde konumlanır.

| UI adı | Rota | Not |
|--------|------|-----|
| Dinamik Formlar (Automated Forms) | `/apps/automated-forms` | Dataset tabanlı CRUD — bkz. [modul-dinamik-form-ve-dashboard.md](./modul-dinamik-form-ve-dashboard.md) |
| Widget / Dashboard yönetimi | `/apps/widgets` · `/apps/dashboards` | Birleşik panel motoru — aynı dosya |
| Alarm Merkezi | `/apps/alarm-center` | SIEM alarm operasyonu; Monitoring operasyon alarmları ile kanal paylaşımı — bkz. [modul-siem-center.md](./modul-siem-center.md) |
| Otomasyon Merkezi | `/apps/automation-center/workflows` | Workflow yol haritasının erken yüzü; pazarlama dilinde **Workflow** ile hizalanacak |
| Zamanlanmış işler (OC admin) | `/apps/operation-core/admin/scheduled-jobs` | Scheduler + modül hedefleri |
| Task Manager | `/apps/task-manager` | Ayrı Jira-benzeri modül; genel broşürde opsiyonel |
| Vertical müşteri uygulamaları (sipariş, eğitim vb.) | `/apps/*-vertical` *(ör. müşteriye özel rota)* | Müşteri/vertical çözüm; horizontal platform broşüründe ayrı |

---

## Modül ↔ UI ↔ Servis eşlemesi

| Pazarlama modülü | Backend / servis | UI sidebar grubu |
|------------------|------------------|------------------|
| Döküman Zekası | MngDocument | Otomasyon Merkezi altında DI |
| Operasyon Merkezi | MngDataGateway (OC dataset’leri) + ilgili API | Operasyon |
| Raporlama | Raporlama servisi + DG | Otomasyon Merkezi |
| Monitoring | MngEngine, asset/agent | *(menü yapılandırmasına bağlı)* |
| SIEM | MngEngine (sec_events) | Güvenlik Yönetimi |
| Workflow | MngWorkflow *(plan)* | Otomasyon Merkezi / OC admin |

---

## Dokümantasyon durumu

| Dosya | Durum | Not |
|-------|-------|-----|
| `platform-tanitimi.md` | Taslak | Kısa modül özetleri mevcut |
| `modul-sablon.md` | Hazır | Yeni modül dosyaları için |
| `modul-document-intelligence.md` | **Ürün perspektifi tamam** | ~1000 satır envanter; broşür ertelendi; implementasyon detayları ileride |
| `modul-operation-core.md` | **Ürün kimliği v0.2** | §2 müşteri perspektifi; yapılandırma derinliği §2.5 · §5.6 |
| `modul-reporting.md` | **Ürün kimliği v0.1** | §2 müşteri perspektifi + §5 fonksiyon envanteri |
| `modul-monitoring.md` | **Ürün kimliği v0.1** | §2 müşteri · SIEM ayrımı · §4 envanter |
| `modul-siem-center.md` | **Ürün kimliği v0.1** | SIEM-hafif · Alarm Merkezi · §4 envanter |
| `modul-workflow.md` | **Ürün kimliği v0.2** | Orkestrasyon hub · OC/Alarm/DI sınırı · **§7 Kanal Akışları** *(plan)* |
| `modul-scheduler.md` | **v0.1** | Omurga tetikleyici — modül hedefleri |
| `modul-platform-omurgasi.md` | **v0.1** | Keeper · DG · Notifier |
| `modul-dinamik-form-ve-dashboard.md` | **v0.1** | Üç form yüzeyi · widget 4 katman · V1 katalog |

---

## Önerilen çalışma sırası

1. ~~**Omurga + Scheduler**~~ ✅  
2. ~~**Döküman Zekası**~~ ✅  
3. ~~**Operasyon Merkezi**~~ ✅  
4. ~~**Raporlama · Monitoring · SIEM**~~ ✅  
5. ~~**Workflow**~~ ✅ *(v0.1)*  
6. **Broşür / landing metinleri** — modül dosyalarından türetme *(DI seed: brosur/)*  
7. Sektörel varyant sayfaları — modül dosyalarından türetilir

---

## Broşür (Döküman Zekası)

| Konum | Açıklama |
|-------|----------|
| Repo | `docs/monitrang/pazarlama/brosur/` |
| DI ağacı | Sayfalar → MonitraNG → Pazarlama → **Broşür** → Modüller |
| Antet | **MNG-STD** — `scripts/seed-letterheads-monitrang.ps1` |
| Seed | `docs/odak/document_intelligence/scripts/seed-monitrang-pazarlama-brosur.ps1` |

---

## Landing page / broşür türetme

| Kaynak bölüm | Kullanım yeri |
|--------------|---------------|
| Tek cümle + Kısa kart | Modül kartları, SVG alt metinleri |
| Sorun + Çözüm | Landing hero altı, feature bölümleri |
| Platform bağlantıları | Mimari diyagram açıklaması |
| Görseller | `Pazarlama/Files/` → DI sync |
| Sektörel örnekler | Vertical landing / müşteri teklifleri |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama*
