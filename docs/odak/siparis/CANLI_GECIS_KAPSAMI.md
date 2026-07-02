# Odak Sipariş — Canlı Geçiş Kapsamı

**Durum:** v1.0 · 1 Temmuz 2026  
**Amaç:** Eski **Kalite** uygulamasından MonitraNG **Odak Sipariş Hub**’a geçişte **zorunlu**, **yüksek öncelikli** ve **kapsam dışı** maddeleri tek yerde toplamak.  
**İlgili:** [FAZ_PLANI.md](./FAZ_PLANI.md) Faz 3 · [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md) · [FONKSIYONEL_HARITA.md](./FONKSIYONEL_HARITA.md)

---

## 1. Geçiş hedefi

| Eski sistem | Yeni sistem |
|-------------|-------------|
| Kalite (CakePHP) · Planlama → İş Paketleri | **Odak Sipariş Hub** (`Mng.Ui`) |
| MySQL `kalite` | **MngDataGateway** dataset’leri (Odak domain) |
| Monolitik modüller | Hub sekmeleri + DG CRUD; NCR/CAPA mevcut workspace bağlantısı |

**Kullanıcı deneyimi ilkesi:** Aynı iş, tanıdık terimler (“İş Paketi”, “Kalem”, “Sevkiyat”, “NCR/CAPA”). “Workspace” jargonu kullanıcıya gösterilmez.

---

## 2. Kavramsal model (onaylanmış)

| Kavram | Hub yaklaşımı |
|--------|----------------|
| **Paketli kayıt** | `recordScope: Paketli` · `parentPackageId` dolu · İş paketi detayından yönetim; global listede salt okunur (sevkiyat/NCR) |
| **Genel kayıt** | `recordScope: Genel` · paket bağlantısı yok · Hub global sayfalarında CRUD (genel sevkiyat, genel NCR) |
| **Kalite geçmişi** | NCR/CAPA dataset’leri; paket detayında sekme + OC workspace deep link |

---

## 3. Canlı geçiş — BLOCKER (zorunlu)

Bunlar tamamlanmadan production cutover yapılmaz.

| # | Konu | Hedef / kriter | Mevcut durum (Odak test ~Haz 2026) | Doğrulama |
|---|------|----------------|-------------------------------------|-----------|
| 1 | **İş paketi migrasyonu** | Legacy `packages` = DG `odak_is_paketleri` | ~824 / 825 (1 bozuk tuple: `package_no "9"`) | `verify-legacy-dg-migration.ps1` |
| 2 | **Kalem migrasyonu** | Legacy `packageitems` = DG `odak_siparis_kalemleri` | ~2759 / 2767 (~%99,7); **8 kalem** + 1 paket manuel | Aynı + `analyze-line-gaps.ps1` |
| 3 | **Müşteri master** | Legacy müşteri firmalar = `odak_musteriler` | 87 legacy + 2 seed ≈ 89 | Sayım eşleşmesi |
| 4 | **Sevkiyat migrasyonu** | Legacy `shipments` + `shipmentitems` → `odak_sevkiyatlar` + kalemler | Kısmi koşular (~1895 hedef); token expire riski | `verify-odak-go-live-readiness.ps1` |
| 5 | **NCR migrasyonu** | Legacy `ncs` → `odak_ncr` | ~501 (57 paketsiz/genel) | Go-live verify |
| 6 | **CAPA migrasyonu** | Legacy `cpas` → `odak_capa` | ~25 | Go-live verify |
| 7 | **Türkçe metin kalitesi** | Mojibake (`Ã§`, `Ä±` vb.) kabul edilebilir düzeyde | Kısmen onarıldı (NCR `-RepairText`); sevkiyat onarımı eklendi | Go-live verify WARN + `-RepairText` |
| 8 | **Hub UI prod deploy** | İş paketleri listesi/detay, kalemler, NCR/CAPA, sevkiyat listesi | Kod hazır; deploy süreci | Kullanıcı UAT |
| 9 | **UAT walkthrough** | En az 10 rastgele paket alan karşılaştırması | Bekliyor | [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md) §9 |
| 10 | **Cutover kararı** | Tarih, rollback, eski sistem salt okunur dönem | Operasyonel karar | Yönetim onayı |

---

## 4. Yüksek öncelik (WARN — go-live öncesi çözülmeli)

Canlı geçişi teknik olarak bloklamaz; operasyon riski taşır.

| Konu | Not | Hedef |
|------|-----|-------|
| **PO PDF migrasyonu** | `odak_is_paketleri.poDocument` · legacy `MUSTERI_PO` | POC 7 paket; tam batch `%85+` kapsam |
| **Mongo index onarımı** | Prod cutover öncesi `parentPackageId+lineNo` vb. | `repair-odak-siparis-kalemleri-indexes.ps1` |
| **createInfo tarih onarımı** | Prod Mongo dataset metadata | `repair-dataset-createinfo-dates.ps1` |
| **Açık/kapalı paket listesi** | Eski uygulama ile filtre karşılaştırması | UAT maddesi |
| **Paket `"9"`** | SQL dump tuple bozuk | Manuel veya dump düzeltme |

---

## 5. Ertelenebilir (nice-to-have)

| Konu | Gerekçe |
|------|---------|
| **Gelişmiş sevkiyat filtresi** (`AfListFilters`) | Bilinen UI sorunları; kullanıcı erteledi |
| **Global CAPA listesi** | Paket detayından erişim yeterli (ilk dalga) |
| **Fiyat alanları** (birim/toplam) | Faz 1b; operasyon toleransı |
| **Kalem sevk miktarı özeti UI** | Backend kısmen güncelleniyor; görsel özet sonra |
| **Tam QCF form modülü** | Yalnız header alanları migrate; tam form ayrı proje |
| **MO `workItemId` köprüsü** | Sonraki faz |

---

## 6. Bilinçli kapsam dışı (Faz 3’te yok)

Detay: [FAZ_PLANI.md](./FAZ_PLANI.md) “Yapılmayacaklar”.

- Stok / envanter, satın alma, muhasebe / faturalama  
- Tam KYS (eğitim, denetim, doküman yönetimi)  
- Cihaz kalibrasyon modülü  
- GKK / MCF / FAI **tam form** üretimi (NCR/CAPA hariç)  
- Eski uygulamanın birebir UI klonu  

---

## 7. Hub UI — geçiş kapsamındaki yüzeyler

| Yüzey | Route / menü | Durum |
|-------|--------------|-------|
| İş Paketleri listesi | `/apps/odak-siparis/packages` | ✅ kod |
| İş paketi detay (expand) | Kalemler, Kalite, PO PDF, özet | ✅ kod |
| Müşteriler + kalite isterleri | `/apps/odak-siparis/customers` | ✅ deploy edildi (Haz) |
| **Sevkiyat Listesi (global)** | `/apps/odak-siparis/shipments` | ✅ kod · expand panel · filtre sekmeleri |
| NCR/CAPA | Paket detay sekmesi + OC | ✅ |
| Gelişmiş liste filtreleri (sevkiyat) | `AfListFilters` | ⏳ ertelendi |

### Sevkiyat hub — önemli dosyalar

| Dosya | Amaç |
|-------|------|
| `Mng.Ui/pages/apps/odak-siparis/shipments/index.vue` | Global sevkiyat listesi |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisShipmentExpandPanel.vue` | Expand detay |
| `Mng.Ui/utils/odakSiparisShipmentService.ts` | Sorgu, müşteri→paket filtresi, chunk birleştirme |
| `Mng.Ui/components/apps/automated-forms/AfListFilters.vue` | Gelişmiş filtre (sorunlu) |

---

## 8. Veri migrasyon — dataset eşlemesi

| Legacy (MySQL) | DG dataset | Script |
|----------------|------------|--------|
| `firms` (müşteri) | `odak_musteriler` | `migrate-legacy-from-sql-dump.ps1` |
| `packages` | `odak_is_paketleri` | aynı |
| `packageitems` | `odak_siparis_kalemleri` | + `migrate-remaining-lines.ps1` |
| `ncs` | `odak_ncr` | `migrate-legacy-ncs-to-dg.ps1` |
| `cpas` | `odak_capa` | aynı |
| `shipments` / `shipmentitems` | `odak_sevkiyatlar` / `odak_sevkiyat_kalemleri` | `migrate-legacy-shipments-to-dg.ps1` |
| MUSTERI_PO PDF | `odak_is_paketleri.poDocument` | `migrate-legacy-po-pdf-to-dg.ps1` |

**Idempotency:** `legacyPackageId`, `legacyLineId`, `legacyShipmentId`, `legacyNcrId`, `legacyCapaId` — re-run güvenli.

**Metin onarımı:**

```powershell
.\migrate-legacy-ncs-to-dg.ps1 -RepairText
.\migrate-legacy-shipments-to-dg.ps1 -RepairText
```

---

## 9. Doğrulama ve raporlama

### 9.1 Temel sayım

```powershell
.\docs\odak\siparis\scripts\verify-legacy-dg-migration.ps1 -UseSqlDump
```

Paket, kalem, müşteri — MySQL/dump vs DG eşitlik.

### 9.2 Canlı geçiş hazırlık (BLOCKER / WARN)

```powershell
# Test DG (192.168.20.20:5040)
.\docs\odak\siparis\scripts\verify-odak-go-live-readiness.ps1 -UseSqlDump

# Prod DG
$env:MNG_OC_USE_PROD_TOKEN = "1"
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
.\docs\odak\siparis\scripts\verify-odak-go-live-readiness.ps1 -UseSqlDump -BaseUrl "http://192.168.20.8:5040"

# WARN'leri de BLOCKER say (-Strict)
.\docs\odak\siparis\scripts\verify-odak-go-live-readiness.ps1 -UseSqlDump -Strict
```

**Rapor:** `docs/odak/siparis/datasets/odak-go-live-readiness-report.json`

| Kontrol | Seviye | Kural |
|---------|--------|-------|
| Paket, kalem, müşteri | BLOCKER | DG = Legacy |
| Sevkiyat, NCR, CAPA (+ kalemler) | BLOCKER | DG ≥ Legacy |
| PO PDF kapsamı | WARN | Varsayılan ≥ %85 |
| Türkçe mojibake örnekleme | WARN | Varsayılan oran ≤ %5 |

### 9.3 Tam prod pipeline

```powershell
$env:MNG_OC_USE_PROD_TOKEN = "1"
.\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
.\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1 -RepairText
```

Adımlar `[0/9]` … `[9/9]`: dump migrasyon → NCR/CAPA → sevkiyat → PO PDF → temel verify → **go-live verify**.

Parametreler: `-DryRun`, `-SkipPoPdf`, `-SkipGoLiveVerify`, `-RepairText`.

---

## 10. UAT checklist (kullanıcı)

[VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md) §9 ile uyumlu:

- [ ] **10 rastgele paket:** paket no, müşteri, kalem sayısı, termin, durum — eski ekran vs hub  
- [ ] **3 paket NCR/CAPA:** kayıt sayısı, metin okunabilirliği, deep link  
- [ ] **Sevkiyat listesi:** Tümü / İş paketli / Genel sekmeleri; irsaliye arama; expand detay  
- [ ] **PO PDF:** en az 3 migrate edilmiş pakette önizleme/indirme  
- [ ] **Açık/kapalı filtre:** eski “açık paketler” listesi ile karşılaştırma  
- [ ] **Türkçe spot-check:** `unvan`, kalem `description`, NCR `explanation`, sevkiyat `notes`

---

## 11. Önerilen sprint sırası

### Sprint A — Veri (teknik)

1. Legacy MySQL `:3307` + güncel SQL dump (`%USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql`)  
2. Test’te `verify-odak-go-live-readiness.ps1` → BLOCKER listesi  
3. Eksik migrasyon koşuları + `-RepairText` (NCR + sevkiyat)  
4. Kalan 8 kalem + paket `"9"`  
5. Prod `migrate-legacy-full-to-prod.ps1 -RepairText`  
6. Prod verify → `ready: true`

### Sprint B — UAT (kullanıcı + PO)

1. Hub prod/staging deploy (`sync-odak-source` + `deploy-odak-apps`)  
2. §10 checklist  
3. PO PDF tam batch (uploads sync gerekir)

### Sprint C — Cutover

1. Cutover tarihi + eski Kalite salt okunur  
2. İlk hafta delta kontrol (yeni kayıt yok varsayımı)  
3. Eğitim: “aynı iş, yeni sürüm”

---

## 12. Ortamlar

| Ortam | DG | UI | Not |
|-------|-----|-----|-----|
| Odak test | `http://192.168.20.20:5040` | `http://192.168.20.20:3000` | Migrasyon doğrulama |
| Prod | `http://192.168.20.8:5040` | `https://mng.odaksavunma.com` | Cutover hedefi |
| Legacy referans | MySQL `kalite` · `192.168.20.30` | `http://192.168.20.30/kalite/` | UAT karşılaştırma |

---

## 13. Bilinen açık konular

| Konu | Durum |
|------|--------|
| Gelişmiş sevkiyat filtresi (müşteri vb.) | Kullanıcı erteledi |
| ROKETSAN gibi çok paketli müşteride liste filtresi | `fetchOdakShipmentsPageByCustomer` chunk çözümü uygulandı |
| Tablo layout / sağ boşluk | Kısmen düzeltildi; gelişmiş filtre ayrı |
| Legacy sevkiyat sayısı README’de ~3776 | Dump tablosu `shipments` — verify script gerçek sayımı kullanır |

---

## 14. Script envanteri (canlı geçiş)

| Script | Amaç |
|--------|------|
| `migrate-legacy-full-to-prod.ps1` | Tam prod pipeline (9 adım) |
| `verify-odak-go-live-readiness.ps1` | **BLOCKER/WARN** canlı geçiş hazırlık |
| `verify-legacy-dg-migration.ps1` | Paket/kalem/müşteri sayımı |
| `migrate-legacy-shipments-to-dg.ps1` | Sevkiyat (+ `-RepairText`) |
| `migrate-legacy-ncs-to-dg.ps1` | NCR/CAPA (+ `-RepairText`) |
| `migrate-legacy-po-pdf-to-dg.ps1` | PO PDF |
| `migrate-remaining-lines.ps1` | Kalan kalemler |
| `analyze-line-gaps.ps1` | Eksik kalem analizi |
| `repair-odak-siparis-kalemleri-indexes.ps1` | Prod Mongo index |
| `repair-dataset-createinfo-dates.ps1` | Prod createInfo |

Konum: `docs/odak/siparis/scripts/`

---

## 15. Devam noktası

Güncel oturum özeti: [DEVAM.md](./DEVAM.md)
