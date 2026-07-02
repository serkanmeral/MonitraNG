# Odak Sipariş — Oturum durumu

**Son güncelleme:** 3 Temmuz 2026 (gece oturumu)  
**Konu:** Sevkiyat miktar UX · UTF-8 paket metin onarımı · hub UI · bildirim/mail şablonları

> **Kaldığımız yer:** Prod UI deploy edildi (bu oturum sonunda). Paket `name`/`deliveryAddress`/`notes` Türkçe karakter onarımı prod’da **504 paket / 614 alan** PATCH edildi (ARG1704 doğrulandı). Sevkiyat listesinde miktar sütun etiketleri netleştirildi; satır **Kalan = Toplam − Sevk** düzeltmesi canlı. Go-live BLOCKER’ları (sevkiyat kalemi gap, 3 fail sevkiyat, paket/kalem sayı farkı, CAPA, PO PDF) **hâlâ açık** — bkz. aşağı.

**Ana referans:** [CANLI_GECIS_KAPSAMI.md](./CANLI_GECIS_KAPSAMI.md)  
**Go-live raporu:** `datasets/odak-go-live-readiness-report.json`

---

## Bu oturumda tamamlanan işler

### Sevkiyat miktar UX (Mng.Ui)
- [x] **Kalan miktar (B paketi):** `aggregateLineQuantities`, paket özeti/dashboard refresh, kalem dialog readonly Kalan
- [x] **Sevkiyat listesi:** Toplam / Sevk / Kalan sütunları, expand panel, paket footer toplamları
- [x] **Satır Kalan bug fix:** paket kalanı yerine `Toplam − Sevk` (irsaliye satırı)
- [x] **Sütun etiketleri + ipuçları:** Sipariş miktarı (kalem) · Bu sevkte · Bu sevkte kalan; footer “Paket geneli” uyarısı
- [x] **Sayfalama + scroll:** sevkiyat tablosu yatay scroll ve sayfa boyutu (10/25/50)
- [x] **Para cinsi:** birim fiyat + `currency` format; kalem dialog aynı satır
- [x] **Alan politikası:** `resolveOdakFieldAccess` — liste sütunları form politikasına map

### Bildirim / mail (Mng.Ui)
- [x] Mail şablon sayfalama, i18n, legacyShipmentId, global sevkiyat bildirim test scripti

### UTF-8 / migrasyon onarım (prod veri)
- [x] `Test-SuspiciousLegacyText` — `DgMigrationCommon.ps1`
- [x] `repair-odak-package-text.ps1` — `name`, `deliveryAddress`, `notes` (SQL dump → DG PATCH)
- [x] `repair-odak-musteri-unvan.ps1` (önceki oturum; script repo’da)
- [x] **Prod koşu (3 Tem):** 504 paket satırı, 614 alan onarıldı — rapor: `datasets/repair-odak-package-text-report.json`
- [x] `verify-odak-go-live-readiness.ps1` — mojibake taramasına `odak_is_paketleri` + `odak_musteriler.unvan` eklendi

### Deploy
- [x] Commit + push (`main`)
- [x] Prod `mngui` deploy (`192.168.20.8`)

---

## Prod veri durumu (önceki ölçüm + bu oturum)

| Konu | Durum |
|------|--------|
| Paket adı/adres/not UTF-8 | **504 paket onarıldı** (prod PATCH) |
| Müşteri unvan UTF-8 | Script hazır; gerekirse `repair-odak-musteri-unvan.ps1` |
| NCR | 556/556 OK |
| Sevkiyat kalemi | ~4254 vs 6981 legacy (gap devam) |
| Go-live BLOCKER | packages/lines/shipments/shipment_items/capa — önceki oturumdan açık |

---

## Açık işler (öncelik)

| Öncelik | İş |
|---------|-----|
| P0 | Sevkiyat kalemi gap analizi + 3 fail sevkiyat (106, 179, 3159) |
| P1 | Eksik paket/kalem sayı diff (824/823, 2767/2757) |
| P1 | PO PDF arşivi / kapsam |
| P2 | CAPA 85 (legacy NCR bağlantısız) |
| P2 | `odak_musteri_kisileri.ad` UTF-8 onarım scripti (gerekirse) |
| P2 | Global sevkiyat listesine aynı miktar sütun ipuçları |

---

## Sonraki adımlar (yeni chat)

1. Bu dosyayı oku: `docs/odak/siparis/current_status.md`
2. `verify-odak-go-live-readiness.ps1 -UseSqlDump -BaseUrl http://192.168.20.8:5040`
3. Sevkiyat kalemi warn / 3 fail sevkiyat
4. IX04 veya başka pakette sevkiyat miktar UX smoke test

### Prod token
```powershell
$env:MNG_OC_USE_PROD_TOKEN = "1"
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
```

### UTF-8 onarım
```powershell
# Paket metin (dry-run önce)
.\docs\odak\siparis\scripts\repair-odak-package-text.ps1 -DryRun
.\docs\odak\siparis\scripts\repair-odak-package-text.ps1

# Müşteri unvan
.\docs\odak\siparis\scripts\repair-odak-musteri-unvan.ps1 -DryRun
.\docs\odak\siparis\scripts\repair-odak-musteri-unvan.ps1
```

---

## İlgili dosyalar

| Dosya | Amaç |
|-------|------|
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisShipmentsPanel.vue` | Sevkiyat listesi + footer |
| `Mng.Ui/utils/odakSiparisShipmentService.ts` | Miktar aggregate / satır görünümü |
| `scripts/repair-odak-package-text.ps1` | Paket UTF-8 onarım |
| `scripts/repair-odak-musteri-unvan.ps1` | Müşteri unvan onarım |
| `scripts/verify-odak-go-live-readiness.ps1` | Go-live BLOCKER/WARN |
| `datasets/repair-odak-package-text-report.json` | Son prod onarım raporu |
