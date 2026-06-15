# Eski Kalite uygulaması — genel bakış

**Durum:** Referans analizi (13 Haziran 2026)  
**Kaynak:** `192.168.20.30` · `/home/odak/html/kalite/`

---

## 1. Teknoloji

| Öğe | Değer |
|-----|--------|
| Framework | CakePHP **3.10** |
| PHP (sunucu) | 8.3.6 (uyumluluk riski — Docker’da 7.4 önerilir) |
| Web sunucu | Apache `:80` |
| Veritabanı | MySQL 8 · schema `kalite` |
| URL | `/kalite/` (DocumentRoot altında `webroot`) |

**Bağımlılıklar (özet):** PhpSpreadsheet, CakePDF/dompdf, Queue, CSV view, Upload plugin.

---

## 2. Menü yapısı (sipariş/planlama açısından)

| Menü | Alt öğe | Controller | Sipariş ilişkisi |
|------|---------|------------|------------------|
| **Planlama** | İş Paketleri | `Packages` | **Ana sipariş modülü** |
| **Planlama** | Ürünler | `Products` | Parça katalogu / revizyon |
| **Sevkiyatlar** | Sevkiyat Listesi | `Shipments` | Kalem bazlı sevk |
| **Sevkiyatlar** | Kalite Kontrol Formları | `Qcfs` | Sevkiyat QC |
| **Kalite** | Uygunsuzluklar, CAPA, FAI, GKK, MCF… | çeşitli | Üretim kalitesi |
| **Muhasebe** | Kesilen / Alım faturaları | `Invoices`, `Bills` | Finans |
| **DBA** | Malzemeler, Alım emirleri | `Items`, `Pos` | Stok / tedarik |
| **Tanımlamalar** | Firma listesi | `Firms` | Müşteri + tedarikçi |

**Not:** Müşteri siparişi **“Sipariş” menüsünde değil**; **İş Paketleri** ekranında yönetiliyor.

---

## 3. Veri modeli — sipariş omurgası

### 3.1 `packages` (iş paketi üst kayıt)

| Alan | Açıklama |
|------|----------|
| `package_no` | ODAK iş paketi no (örn. `2026-022`) |
| `customer_id` | Müşteri (`firms`, `is_customer`) |
| `name` | İş paketi adı |
| `polink` / `porlink` | Müşteri PO PDF yolu (tam / redacted) |
| `po_version` | PO revizyon |
| `responsible`, `design_responsible`, `manufacture_responsible` | Sorumlular |
| `contact_id` | Müşteri tarafı sorumlu |
| `part_count`, `stock_count`, `shipped_count` | Özet sayılar |
| `status` | `0` = Açık, `1` = Kapalı |
| `begin_date`, `delivery_date` | Başlangıç / termin |
| `address`, `payment_detail`, `notes` | Teslimat, ödeme, not |

### 3.2 `packageitems` (sipariş kalemleri)

| Alan | Açıklama |
|------|----------|
| `package_id` | Üst iş paketi |
| `number` | Kalem no |
| `customer_project_no` | Müşteri proje no |
| `customer_po_no` | Müşteri PO no |
| `customer_po_item_no` | PO kalem no |
| `description` | Ürün/hizmet tanımı |
| `po_item_rev_no` | Revizyon |
| `customer_job_no` | Müşteri iş emri |
| `count`, `unit` | Miktar |
| `unit_cost`, `total_cost`, `currency` | Fiyat (ERP rolleri) |
| `quality_reqs` | Kalite isterleri |
| `isfai`, `faicomp` | FAI |
| `shipment_date`, `shipment_address` | Planlanan sevkiyat |

### 3.3 İlişkili tablolar

| Tablo | Rol |
|-------|-----|
| `shipments` / `shipmentitems` | Gerçekleşen sevkiyat, kalem miktarı |
| `invoices` | Kesilen fatura · `po_no`, `package_id` |
| `firms` | Müşteri / tedarikçi |
| `products` / `prodrevs` | Ürün tanımı |
| `ncs`, `cpas`, `fais`, `mcfs`, `aqiforms` | Kalite |
| `items`, `pos` | Malzeme stok · tedarik alım emri |

---

## 4. Veri hacmi (Nisan 2026 dump)

| Tablo | Kayıt (yaklaşık) |
|-------|------------------|
| `packages` | 825 |
| `packageitems` | 2769 |
| `shipments` | 3776 |
| `firms` | 801 |

---

## 5. Dosya depolama (migrate ederken gerekli)

Sunucuda uygulama kökü `/home/odak/html/`:

| Klasör | İçerik |
|--------|--------|
| `Yonetim/MUSTERI_PO/` | Müşteri PO PDF (yıl alt klasörleri) |
| `Urunler/` | Ürün dosyaları |
| `file_storage/` | Genel upload |
| `Satin_Alma/` | Satın alma dokümanları |
| `Kalite_Arsiv/` | Kalite arşivi |

PO görüntüleme: `PackagesController::po()` → `CAKEPHP_UPLOAD_ROOT` + `polink` + PDF.

---

## 6. Roller (özet)

Eski sistemde çok rollü kullanıcı (`roles_users`). Sipariş/planlama ile ilgili örnekler:

- Proje Sorumlusu / Proje Yöneticisi — iş paketi ekle/düzenle
- Kalite / Yönetim — tam PO PDF
- ERP / Yönetim — fiyat alanları, faturalar

MonitraNG tarafında Keycloak grupları + OC workspace izinleri ile eşlenecek.

---

## 7. MonitraNG ile kavramsal eşleme

| Eski Kalite | MonitraNG hedef |
|-------------|-----------------|
| İş Paketi | OC work item (İş Paketi / ODF tipi) |
| Kalem | DG dataset `odak_siparis_kalemleri` |
| Açık/Kapalı | WI durumu veya `status` alanı |
| Sevkiyat | Dataset veya Sevkiyat WI (Faz 2) |
| NCR/CAPA | Mevcut Odak Üretim tipleri |
| Firma (müşteri) | `odak_musteriler` |

Detay: [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md)
