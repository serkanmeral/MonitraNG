# Odak Sipariş — Oturum durumu

**Son güncelleme:** 2 Temmuz 2026 (gece oturumu)  
**Konu:** Legacy Kalite → prod MngDataGateway migrasyonu · UTF-8 onarım · go-live doğrulama · sevkiyat hub UI

> **Kaldığımız yer:** Prod migrasyon büyük ölçüde tamamlandı. **NCR 556/556 OK.** Sevkiyat migrasyonu idempotent hale getirildi (FAIL 199 → 3). Go-live hâlâ **5 BLOCKER + 1 WARN**. Yarın: sevkiyat kalemi gap analizi, 3 bozuk sevkiyat, paket/kalem farkı, CAPA/PO PDF.

**Ana referans:** [CANLI_GECIS_KAPSAMI.md](./CANLI_GECIS_KAPSAMI.md)  
**Go-live raporu:** `datasets/odak-go-live-readiness-report.json` (son koşu: 2026-07-02T00:47Z)

---

## Prod veri durumu (son ölçüm)

| Dataset | Legacy | Prod DG | Durum |
|---------|--------|---------|-------|
| Müşteri | 87 | 87 | OK |
| NCR | 556 | **556** | OK |
| Sevkiyat | 3776 | 3775 | -1 |
| Sevkiyat kalemi | 6981 | 4254 | büyük fark (veri eşleşme) |
| Paket | 824 | 823 | -1 |
| Sipariş kalemi | 2767 | 2757 | -10 |
| CAPA | 110 | 25 | 85 legacy NCR bağlantısız |
| PO PDF | 664 aday | 7 (%1.1) | WARN |

Türkçe metin (120 örnek): mojibake **0**.

---

## Bu oturumda tamamlanan işler

### Migrasyon altyapısı
- [x] `Invoke-DgRestMethod` (UTF-8 HttpClient) — `DgMigrationCommon.ps1`; tüm migrasyon scriptlerinde kullanım
- [x] `UpdateOdakLineShippedQuantities.ps1` + `update-odak-line-shipped-quantities.ps1` — filter yerine list API; 401 döngüsü çözüldü
- [x] `migrate-legacy-shipments-to-dg.ps1` — sevkiyat kalemi **Upsert-ShipmentItem** (legacyShipmentItemId map); FAIL 199 → **3**
- [x] Prod şema: `setup-odak-siparis-sevkiyat-datasets.ps1` (recordScope, lineMode)
- [x] Prod şema: `setup-odak-siparis-ncr-capa-datasets.ps1` (recordScope, supplierRef vb.)
- [x] `backfill-odak-record-scope.ps1` — 499 NCR recordScope backfill
- [x] `verify-odak-go-live-readiness.ps1` + `migrate-legacy-full-to-prod.ps1` adım 7–10

### Prod migrasyon koşuları
- [x] RepairText sevkiyat (622 kayıt) + NCR metin onarımı
- [x] Adım 6 sevkiyat: OK=3772, SKIP=1, **FAIL=3** (~78 dk/koşu — normal)
- [x] Kalem sevk miktarı backfill: 1210 güncelleme (ilk koşu), son koşuda 0 değişiklik
- [x] Adım 5 NCR: **57 genel NCR** eklendi → toplam 556
- [x] Kalan kalemler: 2767 zaten DG’de; orphan **0**

### UI (paralel geliştirme)
- [x] Global sevkiyat listesi sayfası + expand panel + genel sevkiyat dialog
- [x] NCR dialog / servis güncellemeleri (recordScope, genel NCR)
- [x] Keeper directory picker iyileştirmeleri (profil, gruplar)

---

## Açık BLOCKER’lar (go-live)

1. **packages** — 823 vs 824 (1 paket eksik)
2. **lines** — 2757 vs 2767 (-10 kalem)
3. **shipments** — 3775 vs 3776 (-1; ayrıca 3 fail kayıt)
4. **shipment_items** — 4254 vs 6981 (~2667 warn: `kalem yok legacyLineId=` veya boş legacy ID)
5. **capa** — 25 vs 110 (85 CAPA’nın legacy `cpas_ncs` bağlantısı yok — veri sınırı)
6. **WARN po_pdf** — yerelde yalnızca ~10 PDF; tam arşiv gerekli

### 3 fail sevkiyat (legacy veri)
| legacyShipmentId | Neden |
|------------------|-------|
| 106 | Boş `legacyShipmentItemId` (`''`) unique ihlali |
| 179 | Aynı |
| 3159 | Aynı |

---

## Devam eden / bekleyen işler

| Öncelik | İş |
|---------|-----|
| P0 | Sevkiyat kalemi gap: warn analizi (`legacy-shipments-migration-report.json`) — kaç satır gerçekten taşınabilir? |
| P0 | 3 bozuk sevkiyat (106, 179, 3159) — MySQL `shipmentitems` inceleme / manuel düzeltme |
| P1 | Eksik 1 paket + 10 kalem — SQL dump vs DG diff |
| P1 | PO PDF — tam `uploads` arşivi sağlanınca adım 8 |
| P2 | CAPA 85 — legacy bağlantısız kayıtlar için iş kuralı kararı |
| P2 | Hub prod deploy + UAT §9 (10 paket alan karşılaştırması) |

---

## Sonraki adımlar (yarın)

1. `docs/odak/siparis/current_status.md` oku (bu dosya)
2. `verify-odak-go-live-readiness.ps1 -UseSqlDump -BaseUrl http://192.168.20.8:5040` — güncel BLOCKER listesi
3. Sevkiyat kalemi warn analizi + 3 fail sevkiyat MySQL sorgusu
4. Paket/kalem diff (eksik 1+10)
5. PO PDF arşiv durumu kullanıcıyla netleştir

### Prod token
```powershell
$env:MNG_OC_USE_PROD_TOKEN = "1"
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
```

### Tipik komutlar
```powershell
# Go-live doğrulama
.\docs\odak\siparis\scripts\verify-odak-go-live-readiness.ps1 -UseSqlDump -BaseUrl http://192.168.20.8:5040

# Sevkiyat retry (idempotent, ~1–1.5 saat)
.\docs\odak\siparis\scripts\export-legacy-shipments-from-mysql.ps1
.\docs\odak\siparis\scripts\migrate-legacy-shipments-to-dg.ps1 -BaseUrl http://192.168.20.8:5040

# Kalem sevk miktarı (ayrı, ~20 dk)
.\docs\odak\siparis\scripts\update-odak-line-shipped-quantities.ps1 -BaseUrl http://192.168.20.8:5040
```

---

## Önemli notlar

- Sevkiyat migrasyonu **1–1.5 saat** sürer (3776 kayıt × binlerce API çağrısı) — bu normaldir.
- Idempotent upsert sayesinde yeniden koşmak güvenlidir; FAIL dramatik düştü.
- CAPA 85 kayıt: legacy `cpas` tablosunda NCR linki yok; script bilinçli atlıyor.
- Yerel MySQL `:3307` export için gerekli; servisleri AI kendiliğinden başlatmamalı.

---

## İlgili dosyalar

| Dosya | Amaç |
|-------|------|
| `scripts/lib/DgMigrationCommon.ps1` | UTF-8 REST, Load-LegacyIdMap |
| `scripts/lib/UpdateOdakLineShippedQuantities.ps1` | shippedQuantity backfill |
| `scripts/migrate-legacy-shipments-to-dg.ps1` | Sevkiyat + idempotent kalem upsert |
| `scripts/migrate-legacy-full-to-prod.ps1` | Tam prod pipeline (0–10) |
| `scripts/verify-odak-go-live-readiness.ps1` | BLOCKER/WARN |
| `datasets/legacy-shipments-migration-report.json` | Son sevkiyat raporu (FAIL=3) |
| `datasets/legacy-ncs-migration-report.json` | NCR/CAPA raporu |
