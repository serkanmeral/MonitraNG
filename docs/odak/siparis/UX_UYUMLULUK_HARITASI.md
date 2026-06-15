# UX uyumluluk haritası — Eski Kalite → MonitraNG

**Durum:** v0.1 · 13 Haziran 2026  
**Amaç:** Müşteri direncini azaltmak için **mental model, terimler ve ekran iskeletini** korumak; altyapı Operation Core + DG dataset olacak.

**İlke:** Pixel-perfect kopya değil; kullanıcı **“aynı program, yeni sürüm”** hissi.

---

## 1. Korunacak UX ilkeleri

| # | İlke | Açıklama |
|---|------|----------|
| U1 | **Aynı terimler** | İş Paketi, Kalem, Müşteri PO, Sevkiyat — OC jargonu (workspace, work item) kullanıcıya gösterilmez |
| U2 | **Liste önce** | Günlük iş tablo + arama; kanban ikincil görünüm |
| U3 | **Üst özet + sekmeler** | Detay: panel başlık + alan tablosu + `[Kalemler] [Sevkiyatlar]` |
| U4 | **Açık / Kapalı / Tümü** | Liste sekmeleri aynı mantık |
| U5 | **Genişletilebilir arama** | Arama kutusu katlanabilir panel (eski `searchToggle`) |
| U6 | **Tek modül girişi** | Yan menüde **Odak Sipariş** veya **Planlama → İş Paketleri** — tree içinde gizli gezinme yok |

---

## 2. Menü eşlemesi

| Eski Kalite menü | MonitraNG (kullanıcıya görünen) | MVP | Not |
|------------------|----------------------------------|-----|-----|
| Planlama → **İş Paketleri** | **Odak Sipariş → İş Paketleri** | ✅ Faz 1 | Ana hub |
| Planlama → Ürünler | Master veri / OC tanım | ⏳ Faz 2 | `odak_urunler` zaten var |
| Sevkiyatlar → Sevkiyat Listesi | **Odak Sipariş → Sevkiyatlar** | ⏳ Faz 2 | Liste benzeri hub |
| Kalite → Uygunsuzluklar / CAPA | Mevcut **Odak Üretim** board | ✅ Var | Profilden veya menüden link |
| Tanımlamalar → Firma / Müşteri | **Odak Sipariş → Müşteriler** veya AF form | ⏳ | `odak_musteriler` |

**Faz 1 MVP menüsü (öneri):**

```
Odak Sipariş
├── İş Paketleri      ← packages/index karşılığı
├── (ileride) Sevkiyatlar
└── (link) Kalite Kuyruğu → OC workspace NCR/CAPA
```

---

## 3. Ekran eşlemesi — İş Paketleri listesi

**Eski:** `Packages/index.ctp` · `/packages` · `/packages/index/closed` · `/packages/index/all`

**Yeni route (taslak):** `/apps/operation-core/odak-siparis/packages` (veya eşdeğer)

### 3.1 Layout bileşenleri

| Bileşen | Eski | Yeni (MonitraNG) | Durum |
|---------|------|------------------|-------|
| Sayfa başlığı | Panel: **İş Paketleri** | `v-card-title`: İş Paketleri | ☐ |
| Arama paneli | Katlanır `panel-info` | `v-expansion-panel` · İş Paketi Arama | ☐ |
| Sekmeler | Açık · Kapalı · Tümü | `v-tabs` aynı etiketler | ☐ |
| Tablo | DataTables server-side | OC list pattern veya DG query + sayfalama | ☐ |
| Ekle butonu | **İş Paketi Ekle** | Primary button · aynı metin | ☐ |
| Dışa aktar | Excel (PhpSpreadsheet) | Export API / xlsx | ☐ Faz 1b |

### 3.2 Arama alanları

| Alan (eski label) | Query param | Yeni alan adı | MVP |
|-------------------|-------------|---------------|-----|
| İş Paketi No | `pno` | `packageNo` | ✅ |
| İş Paketi İsmi | `pn` | `name` | ✅ |
| Durum | `st` | `status` (Açık/Kapalı) | ✅ |
| Müşteri | `fn` | `customerName` | ✅ |
| Müşteri İş Paketi No | `cprno` | kalem filtresi · `customerProjectNo` | ✅ |
| Müşteri PO No | `cpono` | kalem filtresi · `customerPoNo` | ✅ |
| Ürün Hizmet Tanımı | `psd` | kalem filtresi · `description` | ✅ |

### 3.3 Liste sütunları

| Sütun (eski) | Veri kaynağı | MVP |
|--------------|--------------|-----|
| Eylemler | gör · düzenle · sil | ✅ |
| İş Paketi No | `package_no` → detay link | ✅ |
| İş Paketi İsmi | `name` | ✅ |
| Müşteri | `customer.name` → müşteri link | ✅ |
| Müşteri Proje No | kalemlerden birleşik | ✅ |
| Müşteri PO No | kalemlerden birleşik | ✅ |
| Parça Sayısı | `part_count` | ✅ |
| Stok Sayısı | `stock_count` | ⏳ |
| Durum | Açık / Kapalı | ✅ |
| Başlangıç Tarihi | `begin_date` | ✅ |
| Termin Tarihi | `delivery_date` | ✅ |

**Arka plan:** Liste satırı OC `op_work_items` + join `odak_siparis_kalemleri` aggregate veya MO read API.

---

## 4. Ekran eşlemesi — İş Paketi detay

**Eski:** `Packages/view.ctp` · `/packages/view/{id}`

### 4.1 Üst panel (özet tablo)

| Eski label | OC / dataset alanı | MVP |
|------------|----------------------|-----|
| ODAK İş Paketi No | `workItemKey` veya `packageNo` | ✅ |
| İş Paketi İsmi | `title` | ✅ |
| Müşteri | `customerId` → lookup | ✅ |
| Müşteri İş Paketi Sorumlusu | `contactId` | ⏳ |
| **Müşteri Sipariş Emri** | PO PDF link | ⏳ Faz 1b (dosya) |
| ODAK İş Paketi Sorumlusu | atanan / custom field | ✅ |
| Tasarım Sorumlusu | `designResponsible` | ⏳ |
| Üretim Sorumlusu | `manufactureResponsible` | ⏳ |
| Parça Sayısı | aggregate / field | ✅ |
| Ödeme Bilgisi | `paymentDetail` (ERP rol) | ⏳ |
| Stok Sayısı | computed | ⏳ |
| Durum | state görünen ad · Açık/Kapalı | ✅ |
| Başlangıç / Termin | `beginDate`, `deliveryDate` | ✅ |
| Teslimat Adresi | `address` | ✅ |
| Sisteme giriş / Son düzenleme | audit (OC native) | ✅ |

**Aksiyonlar:** Düzenle · Sil — eski panel heading ile aynı konum (sağ üst).

### 4.2 Sekmeler

| Sekme (eski) | İçerik | Yeni implementasyon | MVP |
|--------------|--------|---------------------|-----|
| **Kalemler** | `packageitems` tablosu | Dataset grid + ekle/düzenle | ✅ |
| **Sevkiyatlar** | `shipments` listesi | Dataset veya alt liste | ⏳ Faz 2 |

**Kalemler grid sütunları (eski view):**

| Sütun | MVP |
|-------|-----|
| Eylemler (düzenle/sil) | ✅ |
| İş Paketi Kalem No | ✅ |
| Müşteri Proje No | ✅ |
| Müşteri PO No | ✅ |
| Müşteri PO Kalem No | ✅ |
| Ürün ve Hizmet Tanımı | ✅ |
| Miktar | ✅ |
| Sevk Miktarı | ⏳ (shipmentitems aggregate) |
| Birim Fiyat / Toplam (ERP) | ⏳ |
| Sevkiyat Tarihi | ✅ |
| Sevkiyat Adresi | ✅ |
| **Kalem Ekle** butonu | ✅ |

### 4.3 Gizli / arka plan (kullanıcı görmez)

| OC özellik | Kullanım |
|------------|----------|
| Durum geçişleri | Üretim → kalite → sevk (profil yan panel veya durum dropdown) |
| `parentItemId` | NCR/CAPA bağlantısı |
| Board | Opsiyonel · “Kanban görünümü” linki |
| Otomasyon | `hold_quality` → NCR (planlı SW-A*) |

---

## 5. Ekran eşlemesi — Kalem ekle/düzenle

**Eski:** `Packageitems/add.ctp`, `edit.ctp`

| Form alanı (eski label) | Dataset alanı (taslak) | MVP |
|--------------------------|--------------------------|-----|
| İş Paketi Kalem No | `lineNo` | ✅ |
| Müşteri Proje No | `customerProjectNo` | ✅ |
| Müşteri PO No | `customerPoNo` | ✅ |
| Müşteri PO Kalem No | `customerPoItemNo` | ✅ |
| Ürün ve Hizmet Tanımı / Kodu | `description` | ✅ |
| Ürün Revizyonu | `poItemRevNo` | ✅ |
| Müşteri İş Emri | `customerJobNo` | ✅ |
| Miktar | `quantity` | ✅ |
| Birim | `unit` | ✅ |
| Birim Fiyatı | `unitCost` | ⏳ |
| Toplam Maliyet | `totalCost` (computed) | ⏳ |
| Para Birimi | `currency` | ⏳ |
| Kalite İsterleri | `qualityReqs` | ⏳ |
| FAI Yapılacak mı? | `isFai` | ⏳ |
| Sevkiyat Tarihi | `shipmentDate` | ✅ |
| Sevkiyat Adresi | `shipmentAddress` | ✅ |

**İş kuralı (eski):** Belirli müşteri + kalite isteri → FAI otomatik Evet — migrasyon sonrası OC kural veya form script.

---

## 6. Ekran eşlemesi — Sevkiyat listesi (Faz 2)

**Eski:** `Shipments/index.ctp`

| Bileşen | Korunacak |
|---------|-----------|
| Sekmeler | Planlanan · Tümü |
| Arama | İrsaliye no, tarih aralığı, müşteri, iş paketi, denetim tipi |
| **Sevkiyat Ekle** | ✅ |

---

## 7. Rol / görünürlük

| Alan / aksiyon | Eski rol | MonitraNG |
|----------------|----------|-----------|
| Fiyat sütunları | erp, management, admin | OC policy / Keycloak grup |
| Tam PO PDF | qualityman, management, admin… | redacted vs full |
| İş paketi ekle | project, projectman | workspace izin |
| Sil | yetkili roller | OC delete guard + `force` |

---

## 8. MVP checklist (Faz 1 — müşteri demo)

### Liste ekranı

- [ ] Menü: Odak Sipariş → İş Paketleri
- [ ] Sekmeler: Açık / Kapalı / Tümü
- [ ] Katlanır arama (7 alan)
- [ ] Tablo sütunları (§3.3 — stok hariç)
- [ ] İş Paketi Ekle → create form
- [ ] Satır tıklama → detay

### Detay ekranı

- [ ] Üst özet paneli (§4.1 — MVP alanlar)
- [ ] Sekme: Kalemler (grid)
- [ ] Kalem Ekle / Düzenle / Sil
- [ ] Düzenle → header form
- [ ] Durum: Açık/Kapalı veya OC state (kullanıcıya sade etiket)

### Bilinçli ertelenen (Faz 1 dışı)

- [ ] PO PDF inline görüntüleme
- [ ] Excel export
- [ ] Sevkiyatlar sekmesi
- [ ] DataTables satır genişletme (kalem önizleme)
- [ ] FAI otomatik kuralı

---

## 9. Müşteri ile paylaşılabilir özet cümle

> “Ekranlarınız aynı kalacak: İş Paketi listesi, arama, Açık/Kapalı sekmeleri, detayda Kalemler tablosu. Altyapı güçlenecek; günlük iş akışınız değişmeyecek.”

---

## 10. Referans URL’ler (eski sistem)

| Ekran | Eski URL kalıbı |
|-------|-----------------|
| Açık paketler | `/kalite/packages` |
| Kapalı | `/kalite/packages/index/closed` |
| Detay | `/kalite/packages/view/{id}` |
| Kalem ekle | `/kalite/packageitems/add/{package_id}` |

Lokal Docker kurulumundan sonra bu URL’ler referans ortamında doğrulanacak → [DOCKER_LOCAL_PLAN.md](./DOCKER_LOCAL_PLAN.md)
