# Veri migrasyon planı — Kalite → MonitraNG

**Durum:** v0.2 · 15 Haziran 2026 (DG-only migrasyon tamamlandı)  
**Kaynak DB:** MySQL `kalite` · dump: `01-kalite.sql` (`kalite-legacy-docker/db/init/`)

---

## 1. Hedef envanter

| Hedef | Teknoloji | Açıklama |
|-------|-----------|----------|
| İş paketi üst kayıt | **`odak_is_paketleri`** (DG dataset) | ✅ Birincil model (MO yok) |
| İş paketi (süreç) | `op_work_items` | Sonraki faz · `workItemId` alanı hazır |
| Sipariş kalemleri | `odak_siparis_kalemleri` | ✅ `parentPackageId` + `legacyLineId` |
| Müşteri | `odak_musteriler` | ✅ `legacyFirmId` |
| Ürün | `odak_urunler` | Kısmen mevcut · `products` zenginleştirme |
| NCR / CAPA | `op_work_items` | Mevcut tipler · `parentItemId` |
| Sevkiyat | `odak_sevkiyatlar` + kalemler (Faz 2) | |
| PO PDF | Object storage / DI | Dosya kopyası + metadata |
| Kullanıcı / rol | Keycloak + OC | Ayrı migrasyon |

---

## 2. Tablo eşleme — Faz 1 (sipariş)

### 2.1 `packages` → `odak_is_paketleri` (✅ birincil)

| Kaynak (`packages`) | Hedef | Dönüşüm notu |
|---------------------|-------|--------------|
| `id` | `legacyPackageId` | text, unique |
| `package_no` | `packageNo` | |
| `name` | `name` | |
| `customer_id` | `customerId` | relation → `odak_musteriler` |
| `status` | `status` | `0`→open · `1`→closed |
| `begin_date` | `beginDate` | ISO datetime |
| `delivery_date` | `deliveryDate` | |
| `part_count` … | `partCount` … | bkz. dataset JSON |

**Alternatif (sonraki faz):** aynı kayıt için `op_work_items` + `workItemId` relation alanı.

### 2.1b `packages` → `op_work_items` (MO — ertelendi)

| Kaynak (`packages`) | Hedef | Dönüşüm notu |
|---------------------|-------|--------------|
| `id` | — | Yeni UUID · `legacyPackageId` extraField veya mapping tablosu |
| `package_no` | `workItemKey` veya `extraFields.packageNo` | Prefix: mevcut ODF veya `IP` |
| `name` | `title` | |
| `customer_id` | `extraFields.customerId` | `firms` → `odak_musteriler` id map |
| `status` | `stateId` | `0`→açık state · `1`→kapalı/terminal |
| `begin_date` | `extraFields.beginDate` | |
| `delivery_date` | `extraFields.plannedDate` | OC taslak ile uyumlu |
| `responsible` | `assignee` | `employees` → MngPersonId map |
| `design_responsible` | `extraFields.designResponsible` | |
| `manufacture_responsible` | `extraFields.manufactureResponsible` | |
| `contact_id` | `extraFields.customerContactId` | |
| `part_count` | `extraFields.partCount` | |
| `stock_count` | `extraFields.stockCount` | |
| `address` | `extraFields.deliveryAddress` | |
| `payment_detail` | `extraFields.paymentDetail` | |
| `notes` | `description` veya extraField | |
| `polink`, `po_version` | `extraFields.poDocumentPath` | Dosya migrasyonu ile |
| `created`, `created_by` | audit alanları | MO create API veya bulk import |

**Mapping tablosu (öneri):** Migrasyon script’inde `legacy_package_id → op_work_item_id` JSON/SQLite; idempotent re-run için.

### 2.2 `packageitems` → `odak_siparis_kalemleri`

| Kaynak | Hedef alan | Not |
|--------|---------------------|-----|
| `id` | `legacyLineId` | unique |
| `package_id` | `parentPackageId` | relation → `odak_is_paketleri` |
| `number` | `lineNo` | dump: `[2]=customer_project_no`, `[3]=number` |
| `customer_project_no` | `customerProjectNo` | |
| `customer_po_no` | `customerPoNo` | |
| `customer_po_item_no` | `customerPoItemNo` | |
| `description` | `description` | |
| `po_item_rev_no` | `poItemRevNo` | |
| `customer_job_no` | `customerJobNo` | |
| `count` | `quantity` | |
| `unit` | `unit` | |
| `unit_cost` | `unitCost` | |
| `total_cost` | `totalCost` | |
| `currency` | `currency` | |
| `quality_reqs` | `qualityReqs` | |
| `isfai` | `isFai` | bool |
| `shipment_date` | `shipmentDate` | |
| `shipment_address` | `shipmentAddress` | |

### 2.3 `firms` (müşteri) → `odak_musteriler`

| Kaynak | Hedef | Not |
|--------|-------|-----|
| `id` | mapping | |
| `name` | `unvan` | |
| `is_customer=1` | `aktif=true` | Sadece müşteriler Faz 1 |
| — | `kod` | `MUS-{legacyId}` veya iş kuralı |

Tedarikçiler ayrı faz (`is_supplier`).

---

## 3. Tablo eşleme — Faz 2+

| Kaynak | Hedef | Faz |
|--------|-------|-----|
| `shipments` | `odak_sevkiyatlar` | 2 |
| `shipmentitems` | `odak_sevkiyat_kalemleri` | 2 |
| `ncs` | NCR work items | 2 (kısmen var) |
| `cpas` | CAPA work items | 2 |
| `products` | `odak_urunler` | 2 |
| `items` | `odak_malzemeler` | 4 |
| `invoices` | ERP / ayrı modül | 5 |
| `pos` / `podetails` | Tedarik modülü | 5 |

---

## 4. Sevkiyat miktarı (kalem `shippedQty`)

Migrasyon sonrası hesaplanabilir:

```sql
-- Eski mantık özeti
SELECT pi.id, SUM(si.shipment_count) AS shipped
FROM packageitems pi
LEFT JOIN shipmentitems si ON si.packageitem_id = pi.id
LEFT JOIN shipments s ON s.id = si.shipment_id AND s.status = 'Tamamlandi'
GROUP BY pi.id;
```

Hedef dataset alanı: `shippedQuantity` (Faz 2 import veya batch job).

---

## 5. Dosya migrasyonu

| Kaynak path | Hedef |
|-------------|--------|
| `{polink}{package_no}_{po_version}.pdf` | Object storage key · WI extraField |
| `{porlink}..._redacted.pdf` | Aynı · rol bazlı erişim |
| `html/Urunler/` | Ürün ekleri (Faz 2) |

**Adımlar:**

1. rsync/scp `html/Yonetim`, `file_storage` vb.
2. Path rewrite (eski root → yeni base URL)
3. WI metadata güncelle

---

## 6. Migrasyon stratejisi

| Aşama | Kapsam | Doğrulama |
|-------|--------|-----------|
| **POC** | 1 açık paket + kalemleri | ✅ MO POC (2018-004) |
| **Full (DG-only)** | Tüm dump | ✅ 824 paket · 2759 kalem (Odak test) |
| **Pilot UAT** | Kullanıcı walkthrough | ⏳ UI deploy sonrası |
| **Delta** | Cutover sonrası eski sistem read-only | — |

**Idempotency:** `legacyPackageId` unique index; re-run günceller, duplicate oluşturmaz.

**Sıra (DG-only — uygulandı):**

1. `odak_musteriler` (`legacyFirmId`)
2. `odak_is_paketleri` (`legacyPackageId`)
3. `odak_siparis_kalemleri` (`parentPackageId`, `legacyLineId`)
4. Orphan/slot temizliği + Mongo index onarımı
5. `migrate-remaining-lines.ps1` (encoding fix)

**MO sırası (ileride):** `op_work_items` + `workItemId` link

---

## 7. Script konumu (güncel)

```
docs/odak/siparis/
├── datasets/
│   ├── odak_is_paketleri_dataset.json           ✅
│   ├── odak_siparis_kalemleri_dataset.json      ✅
│   └── migration-mapping-dg.json              ✅ runtime
└── scripts/
    ├── setup-odak-siparis-datasets.ps1        ✅
    ├── migrate-legacy-from-sql-dump.ps1       ✅ ana script
    ├── migrate-remaining-lines.ps1              ✅ kalan kalemler
    ├── verify-legacy-dg-migration.ps1           ✅
    ├── analyze-line-gaps.ps1                   ✅
    ├── remove-orphan-siparis-kalemleri.ps1      ✅
    ├── remove-conflicting-siparis-lines.ps1     ✅
    ├── repair-odak-siparis-kalemleri-indexes.ps1 ✅
    ├── lib/LegacySqlDumpCommon.ps1              ✅
    ├── lib/DgMigrationCommon.ps1                ✅
    └── migrate-packages-poc.ps1                 📦 MO POC (legacy)
```

Test script kuralı: `scripts/tests/` altında smoke testler (servis adına göre).

---

## 8. Riskler

| Risk | Azaltma |
|------|---------|
| `employees` → MngPersonId eşleşmez | Mapping CSV · default boş |
| Duplicate `package_no` | Unique WI key stratejisi |
| Türkçe charset | `Sanitize-JsonText` + ISO-8859-1 round-trip · Mongo index `parentPackageId+lineNo` |
| Açık/kapalı state yanlış | Pilot UAT · eski ekran yan yana |
| PO PDF path kırık | Dosya migrasyonu POC önce |

---

## 9. Doğrulama checklist

- [x] Paket sayısı: 824 / 825 (1 tuple bozuk — `package_no "9"`)
- [x] Kalem sayısı: 2759 / 2767 (~%99,7)
- [ ] Rastgele 10 paket: alan birebir (UAT)
- [ ] Toplam `quantity` per paket eşleşmesi
- [x] Müşteri sayısı: 87 legacy + 2 seed
- [ ] Açık/kapalı paket listesi eski uygulama ile karşılaştırma

---

## 10. İlgili dokümanlar

- Kaynak şema: sunucu `kalite_schema.sql`
- Mimari: [MIMARI_KARAR.md](./MIMARI_KARAR.md)
- UX alan adları: [UX_UYUMLULUK_HARITASI.md](./UX_UYUMLULUK_HARITASI.md)
