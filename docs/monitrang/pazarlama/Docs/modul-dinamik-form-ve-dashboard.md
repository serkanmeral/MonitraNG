# Dinamik Form & Widget / Dashboard — Platform veri yüzeyleri

**Kod:** `dynamic-forms` · `widgets` · **Durum:** Canlı (birleşik widget mimarisi genişletme devam)  
**UI:** `/apps/automated-forms` · `/apps/widgets` · `/apps/dashboards` · `/dashboards/{slug}`  
**Omurga:** DataGateway (dataset şeması, sorgular, `@widgets`, `@dashboards`)

**Referanslar (iç):** [Automated Forms rehberi](../../content/Mng.Ui/guides/chatbot/automated-forms/automated-forms.md) · [Widget mimarisi](../../odak/widgets/ARCHITECTURE.md) · [V1 katalog](../../odak/widgets/KATALOG_V1.md) · [Designer UX](../../odak/widgets/DESIGNER_UX.md)

> **Bu dosyanın amacı (şu an):** MonitraNG’nin **schema tabanlı form** ve **widget/dashboard** yeteneklerini müşteri dilinde netleştirmek; modül modül (OC, SIEM, Raporlama…) dokümanlarına köprü kurmak. Broşür metinleri **henüz doldurulmayacak** — bkz. [§Broşür (ertelendi)](#broşür-ertelendi).

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi · 📋 Teklifte tanımlı

---

## 1. Ürün kimliği

### 1.1 Tek cümle

**Dinamik Form** ve **Widget / Dashboard**, MonitraNG’de veriyi **kod yazmadan yüzeye taşıyan** iki tamamlayıcı yetenektir: biri **giriş ve liste** (form), diğeri **özet ve görselleştirme** (panel); ikisi de **DataGateway şeması** ve platform servislerine dayanır.

### 1.2 Ayrı ürün modülü değil — platform yeteneği

| | Dinamik Form | Widget / Dashboard |
|--|--------------|-------------------|
| **Satış dili** | «Veriniz için ekran dakikalar içinde» | «Tek panelde tüm modüllerden özet» |
| **Teknik kök** | Dataset field şeması + form tanımı | Widget manifest + dashboard layout |
| **Kim kurar?** | İş analisti / admin (Manager) | Admin / operasyon lideri |
| **Nerede görünür?** | Yan menü, modül içi, rapor filtresi | Ana panel, modül dashboard’u, embed |

Bu yetenekler **OC, Raporlama, Monitoring, SIEM, DI** ve müşteriye özel vertical uygulamalarda **ortak motor** olarak kullanılır; broşürde çoğu zaman modül altında anlatılır, bu dosya **çapraz referans kaynağıdır**.

### 1.3 Ne değildir?

| Beklenti | Gerçek |
|----------|--------|
| Low-code uygulama geliştirme IDE’si | **Veri odaklı form + panel** — tam uygulama mantığı Workflow / özel modül |
| Power BI / Tableau yerine geçer | **Operasyon özeti + modül KPI’ları** — ağır BI ayrı sınıf |
| Her widget tek tıkla «her veriyi» çeker | **Tanımlı sorgu / servis referansı** — güvenli, sınırlı veri yolu |
| Form = sadece OC | **Üç yüzey:** genel dinamik form, süreç formu, rapor parametresi |

---

## 2. Müşteri perspektifi

### 2.1 Tek paragraf (broşür / sunum)

Kurum veriniz zaten platformda tanımlıdır; **Dinamik Form** ile aynı veri için liste, arama ve kayıt ekranı **kod beklemeden** açılır — envanter, zimmet, eğitim katılımcıları veya özel master tablolar aynı kalıpla yönetilir. **Operasyon Merkezi**’nde formlar süreç kurallarına bağlanır: kim neyi görür, geçişte hangi alan zorunlu, otomatik dolsun mu. **Widget ve dashboard** ile açık iş sayısı, güvenlik olay özeti, belge klasörü veya alarm trendi **tek panelde** toplanır; yönetici sabah girişinde dağınık Excel ve on ekran yerine **canlı özeti** görür.

### 2.2 Günlük deneyim

| Rol | Dinamik Form | Widget / Dashboard |
|-----|--------------|-------------------|
| **İş birimi kullanıcısı** | Menüden «Demirbaşlar»a girer; filtreler, yeni kayıt ekler | Ana panelde açık iş ve SLA özeti |
| **Süreç sahibi (OC)** | WorkItem açarken tipine göre alanlar; kapanışta zorunlu «çözüm» | Workspace dashboard’unda kuyruk tablosu |
| **SOC / güvenlik** | — | SIEM özet paneli: olay sayısı, senaryo kartları, son olaylar |
| **Rapor tüketicisi** | Rapor öncesi tarih / kişi / durum filtresi (form benzeri) | Rapor sonucu tablo *(grafik dashboard plan)* |
| **Admin** | Dataset’e bağlı yeni form; sütun ve menü yapılandırması | Şablondan widget klonlar; dashboard’a sürükler |

### 2.3 Üç dinamik form yüzeyi (müşteri dili)

| Yüzey | Ne işe yarar? | Örnek |
|-------|---------------|-------|
| **Genel Dinamik Form** *(Automated Forms)* | Herhangi bir **veri tablosu** için tam CRUD ekranı | Zimmet demirbaş listesi; eğitim katılımcı kaydı |
| **Süreç Formu** *(OC)* | **WorkItem** yaşam döngüsüne bağlı alanlar, geçiş kuralları | IT ticket kapanış notu; onay tutarı alanı |
| **Rapor parametre paneli** | Rapor çalıştırmadan önce **filtre ve seçim** | Tarih aralığı, personel seçici, durum butonları |

Üçü aynı **field type** ailesini paylaşır (metin, sayı, tarih, ilişki, kişi, grup…); fark **bağlam ve kural derinliğindedir**.

### 2.4 Widget / dashboard — müşteri dili

| Kavram | Müşteri cümlesi |
|--------|-----------------|
| **Widget** | «Tek bir gösterge veya tablo» — örn. «Açık alarm sayısı» |
| **Dashboard** | «Widget’ların yan yana durduğu panel» — örn. «SOC sabah brifingi» |
| **Şablon katalogu** | «Hazır kartlar; siz parametre ve yerleşimi seçersiniz» |
| **Yüzey bağlamı** | «Paneldeki ortak filtre ve zaman aralığı tüm kartlara yansır» |

---

## 3. Platform bağlantıları

| Bağlantı | Dinamik Form | Widget / Dashboard |
|----------|--------------|-------------------|
| **DataGateway** | Dataset şeması, validation, CRUD | `queryRef` sorguları, `@widgets`, `@dashboards` |
| **Keeper** | Person / group picker | Dashboard yetki grupları |
| **Operasyon Merkezi** | OcDynamicForm, geçiş alanları | Workspace dashboard, MO şablonları |
| **Raporlama** | AfListFilters parametre UI | Rapor dashboard layout *(plan R4)* |
| **SIEM / Alarm** | — | Özet panel, senaryo kartları, alarm KPI |
| **Döküman Zekası** | — | Klasör tablosu, arama listesi şablonları |
| **Monitoring** | — | Metrik widget’ları *(ayrı hat; birleşik katalog V1 dışı)* |
| **Notifier** | — | Panel tıklama → olay / kayıt *(drill-down)* |

---

## 4. Dinamik Form — teknik özet (pazarlama dili)

### 4.1 Genel akış (Automated Forms)

```text
Dataset şeması  →  Form tanımı (formCode)  →  Liste + dialog CRUD  →  (opsiyonel) yan menü
```

**Admin rotaları:** `/apps/automated-forms` · `/apps/automated-forms/create` · `/apps/automated-forms/view/{formCode}` · `/apps/automated-forms/edit/{formCode}`

**Temel yetenekler**

- Dataset field’larından **otomatik alan üretimi**
- Liste sütunları: görünürlük, sıra, format, ilişki gösterimi
- Form düzeni: grup, kolon genişliği, textarea / zengin metin
- Yan menüye bağlama (`sideMenuConfig`)
- Liste yüzeyi: varsayılan tablo veya **bağlı rapor** görünümü (`listView`)
- i18n: form ve alan etiketleri

**Desteklenen field türleri (özet)**

| Tür | UI |
|-----|-----|
| text, number, bool | Metin, sayı, onay kutusu |
| datetime | Tarih / saat seçici |
| relation | Başka dataset’ten seçim (dropdown / autocomplete) |
| persons, personGroups | Kullanıcı / grup seçici |
| incremental | Otomatik numara *(formda gizli)* |

### 4.2 OC süreç formu (OcDynamicForm)

WorkItem **oluşturma**, **düzenleme** ve **durum geçişi** ekranlarında kullanılır; alan görünürlüğü **workspace → board → state** katmanlı politikadan gelir.

**Ek widget türleri (süreç):** tip / öncelik / board / state seçicileri, etiketler, dosya, maskeli alan, çoklu kişi (watchers).

**Müşteri farkı:** «Excel’de serbest sütun» değil — **süreç adımında ne sorulacağı** kurallarla bellidir.

### 4.3 Rapor parametre yüzeyi

Raporlama modülü çalıştırma ekranında **AfListFilters** ile parametre toplanır: `dateRange`, `personPicker`, `buttonGroup`, `search` vb. Rapor tanımındaki parametre şeması ile eşleşir.

**Platform bağı:** Raporlama §5’te «Dynamic Form» faz maddesi — parametre UI’si **canlı**, tam form designer entegrasyonu genişletilebilir.

---

## 5. Widget & Dashboard — mimari (müşteri + envanter)

### 5.1 Birleşik hedef — dört katman

MonitraNG widget yeteneği **tek motor** ve **tek manifest şeması** altında birleştirilmektedir:

| Katman | Pazarlama adı | Ne saklar? |
|--------|---------------|------------|
| 1 — **Şablon katalogu** | Hazır kart kütüphanesi | Domain bazlı seed şablonlar (~19 V1) |
| 2 — **Widget tanımı** | «Benim açık alarm kartım» | Şablondan klon + parametre |
| 3 — **Yüzey bağlamı** | Panel filtresi / zaman aralığı | Tüm widget’lara yayılan ortak bağlam |
| 4 — **Yerleşim** | Dashboard grid | Hangi widget nerede, kaç kolon |

**Veri yolları (iç):**

- `queryRef` — DataGateway sorgusu *(Operation Core)*
- `serviceRef` — Alarm, SIEM, DI servis uçları
- Statik banner — deep link, bilgi şeridi

Detay: [ARCHITECTURE.md](../../odak/widgets/ARCHITECTURE.md)

### 5.2 V1 şablon katalogu (domain özeti)

| Domain | Örnek şablonlar | Veri |
|--------|-----------------|------|
| **alarm** | Açık alarm sayısı, severity donut, son alarmlar | MngAlarm |
| **siem** | Olay sayısı, başarısız giriş, senaryo kartları, olay tablosu | MngReactor + Alarm |
| **operation-core** | Duruma göre işler, SLA ihlali, bana atanan, kuyruk tablosu | DG `op_work_items` |
| **document-intelligence** | Klasör içeriği, arama listesi, hızlı link | MngDocument |

Tam liste: [KATALOG_V1.md](../../odak/widgets/KATALOG_V1.md)

### 5.3 Dashboard yüzeyleri (nerede görünür?)

| Yüzey | Rota / konum | Not |
|-------|--------------|-----|
| **Global dashboard** | `/dashboards/{slug}` | Seed + tenant panelleri |
| **Dashboard yönetimi** | `/apps/dashboards` | Liste, oluştur, düzenle |
| **OC workspace dashboard** | `/apps/operation-core/dashboards/{id}` | Süreç odaklı panel |
| **SIEM Güvenlik Merkezi** | `/apps/siem-center` | Modül içi panel + özet dashboard |
| **Monitoring widget’ları** | `/apps/monitoring/widgets` | Metrik izleme — **ayrı form modeli** *(birleşime taşınacak)* |
| **Vertical özel paneller** | örn. `/dashboards/odak-siparis` | Müşteri paketi — referans |

### 5.4 Designer (teknik olmayan kullanıcı)

| Designer | Soru | Rota |
|----------|------|------|
| **Widget Designer** | Ne gösterilecek? | `/apps/widgets/new` · `/apps/widgets/{id}/edit` |
| **Dashboard Designer** | Nerede duracak? | `/apps/dashboards/new` · `/apps/dashboards/{id}/edit` |

Widget wizard: katalog → parametreler → görünüm preset → davranış (yenileme, drill-down, yetki).

---

## 6. Fonksiyon envanteri

### 6.1 Dinamik Form

| Yetenek | Durum | Not |
|---------|-------|-----|
| Automated Forms CRUD (form tanımı) | ✅ | `@automated_forms` dataset |
| Dataset’ten otomatik alan | ✅ | Field type eşlemesi |
| Liste: arama, sıralama, sayfalama | ✅ | |
| Liste sütun formatı (tarih, para, regex…) | ✅ | |
| Form düzeni (grup, span, widget override) | ✅ | |
| Yan menü entegrasyonu | ✅ | Side menu config |
| Liste → rapor görünümü bağlama | 🔶 | `listView` |
| OC WorkItem dinamik form | ✅ | OcDynamicForm |
| Geçiş zorunlu alanları | ✅ | OcTransitionRequiredFields |
| Katmanlı alan politikası | ✅ | workspace / board / state |
| Rapor parametre paneli (AfListFilters) | ✅ | dateRange, personPicker, buttonGroup… |
| Raporlama «tam Dynamic Form» fazı | 📋 | [modul-reporting.md](./modul-reporting.md) §DEVAM |
| Form designer (sürükle-bırak layout) | 🔲 | Schema tabanlı yeterli V1 |

### 6.2 Widget & Dashboard

| Yetenek | Durum | Not |
|---------|-------|-----|
| Global `@widgets` + `@dashboards` dataset | ✅ | DG |
| Widget listesi / oluştur / düzenle | ✅ | `/apps/widgets` |
| Dashboard listesi / builder | ✅ | `/apps/dashboards` |
| WidgetRenderer + manifest adapter | ✅ | stat, chart, table, list, composite |
| V1 şablon seed (`@widget_templates`) | 🔶 | P0 aktif; P1/P2 genişletme |
| Widget Designer wizard | 🔶 | Faz 1 |
| Dashboard Designer | 🔶 | Layout + placement |
| Surface context (zaman, değişken) | 🔶 | Grafana variables benzeri |
| SIEM özet dashboard (`seed-siem-overview`) | 🔶 | Manuel test / seed güncelleme |
| OC workspace dashboard runtime | ✅ | OcDashboardView |
| Monitoring MonitoringWidgetForm | ✅ | **Ayrı hat** — birleşik katalog V1 dışı |
| Eski hardcoded paneller → manifest | 🔲 | Geçiş devam |
| Raporlama çoklu rapor dashboard (R4) | 🔲 | [modul-reporting.md](./modul-reporting.md) |
| Embed / paylaşım URL (dashboard) | 🔲 | Rapor embed modeli ile hizalanacak |

---

## 7. Modül dokümanlarına köprü

| Modül | Form tarafı | Dashboard tarafı |
|-------|-------------|------------------|
| [Operasyon Merkezi](./modul-operation-core.md) | §5.2 dinamik form, geçiş kuralları | §5.4 workspace dashboard |
| [Raporlama](./modul-reporting.md) | Parametre paneli, §5.10 dashboard plan | Tablo odaklı; R4 layout |
| [Monitoring](./modul-monitoring.md) | — | §4 widget’lar, harita, gauge |
| [Güvenlik Merkezi](./modul-siem-center.md) | — | Güvenlik paneli, SIEM şablonları |
| [Döküman Zekası](./modul-document-intelligence.md) | — | DI widget şablonları |
| [Platform omurgası](./modul-platform-omurgasi.md) | DG şema, validation | DG query + dataset depolama |

---

## 8. Gerçek hayat örnekleri

### 8.1 Dinamik Form

| # | Senaryo | Yüzey |
|---|---------|-------|
| 1 | IT envanter kaydı (laptop, seri no) | Automated Form + menü |
| 2 | Zimmet demirbaş atama | Vertical paket formları *(aynı motor)* |
| 3 | Müşteri şikâyeti WorkItem açma | OC süreç formu |
| 4 | Kapanışta «kök neden» zorunlu | OC geçiş alanları |
| 5 | Yıllık eğitim listesi raporu — yıl + durum seç | Rapor parametre paneli |
| 6 | IK personel listesi (Users) gelişmiş filtre | AfListFilters paylaşımı |

### 8.2 Widget / Dashboard

| # | Senaryo | Panel |
|---|---------|-------|
| 1 | SOC sabah brifingi | SIEM özet: olay, senaryo, tablo |
| 2 | IT yöneticisi açık ticket dağılımı | OC donut + kuyruk tablosu |
| 3 | Üretim workspace duvar ekranı | OC dashboard + *(Monitoring metrik şeridi teklif)* |
| 4 | Belge yöneticisi klasör özeti | DI folder table widget |
| 5 | NOC açık operasyon alarmı | Alarm stat + trend *(API genişletme)* |
| 6 | Yönetim kurulu tek sayfa özet | Global dashboard — çok domain widget |

---

## 9. Sektörel örnekler *(opsiyonel)*

| Sektör | Form örneği | Dashboard örneği |
|--------|-------------|------------------|
| **Üretim** | NCR kayıt formu (OC) | Açık emirler + SLA |
| **Bankacılık** | Uyum kontrol listesi | SIEM olay özeti |
| **Lojistik** | Depo lokasyon master (AF) | Sevkiyat istisna KPI *(vertical)* |
| **Kamu** | Vatandaş başvuru kaydı | Günlük başvuru sayacı |
| **Eğitim / IK** | Katılımcı kayıt (AF) | Eğitim doluluk tablosu |

---

## Broşür (ertelendi)

Aşağıdaki metinler modül envanteri oturduktan sonra doldurulacak:

**Kısa (kart):** *(1–2 cümle)*

**Orta (bölüm girişi):** *(1 paragraf)*

**CTA önerisi:** *(ör. «Veriniz için ekranı kodlamadan açın»)*

---

## Teknik notlar *(iç kullanım, broşüre taşınmaz)*

| Konu | Konum |
|------|-------|
| Automated Forms store / API | `Mng.Ui/stores/apps/automatedForms.ts` |
| OC form runtime | `Mng.Ui/components/apps/operation-core/OcDynamicForm.vue` |
| Field widget çözümleme | `Mng.Ui/utils/ocDynamicFormField.ts` |
| Rapor filtre bileşeni | `Mng.Ui/components/apps/automated-forms/AfListFilters.vue` |
| Widget fetch / manifest | `Mng.Ui/services/widgetDataService.ts`, `utils/widgets/` |
| Planlama klasörü | `docs/odak/widgets/` |
| JSON Schema | `docs/odak/widgets/schemas/widget-manifest-v1.schema.json` |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · v0.1*
