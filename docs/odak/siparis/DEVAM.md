# Odak Sipariş — Devam noktası (checkpoint)

**Son güncelleme:** 15 Haziran 2026 (döküman senkronu: DEVAM · README · FAZ_PLANI · VERI_MIGRASYON_PLANI · MIMARI_KARAR)  
**Durum:** ✅ DG-only toplu migrasyon (~%99,7 kalem) · ⏸ Hub UI deploy bekliyor · MO entegrasyonu sonraki faz

> **⭐ KALDIĞIMIZ YER:** Odak test (`192.168.20.20:5040`) üzerinde **dataset kurulumu + SQL dump migrasyonu** tamamlandı. Veri modeli **MO'suz DG-only**: `odak_musteriler` + `odak_is_paketleri` + `odak_siparis_kalemleri`. Hub UI refactor kodda hazır ama **deploy edilmedi** (bilinçli erteleme). **Sıradaki:** UI deploy + walkthrough · git commit · kalan 8 kalem (manuel) · MO `workItemId` bağlantısı (ileride).

---

## Mimari karar (güncel)

```
Legacy SQL dump (01-kalite.sql)
    → DG POST
        odak_musteriler       (legacyFirmId)
        odak_is_paketleri     (legacyPackageId)
        odak_siparis_kalemleri (parentPackageId, legacyLineId)

MO (op_work_items): ileride — dataset alanları hazır tutulabilir
Hub UI: odak_is_paketleri listesi (MO board değil)
```

Detay: [MIMARI_KARAR.md](./MIMARI_KARAR.md) · [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md)

---

## Migrasyon sonuçları (Odak test — 15 Haziran 2026)

| Kaynak (dump parse) | DG (`192.168.20.20:5040`) | Oran | Not |
|---------------------|----------------------------|------|-----|
| Paketler | 824 | **824** | ✅ 1 paket (`package_no "9"`) SQL tuple bozuk |
| Kalemler | 2767 | **2759** | ~**%99,7** · `legacyLineId` eşleşen: **2757** / 2763 geçerli |
| Müşteriler (`is_customer=1`) | 87 | **89** | +2 seed kaydı (beklenen) |

**Migrasyon bekleyen otomatik kalem:** 0 (`analyze-line-gaps.ps1` → `Ready to POST: 0`)

### Otomatik taşınamayan 8 kalem (2767 − 2759)

| Neden | Adet |
|-------|------|
| SQL parse edilemeyen satır | 4 |
| Parent paket DG'de yok | 2 |
| `lineNo` slot çakışması (doğru kayıt başka yerde) | 4 |

*(4+2+4=10; 2 kayıt çakışan kategorilerde örtüşür — net eksik 8.)*

---

## Oturumda tamamlanan işler (14–15 Haziran 2026)

### Dataset kurulumu
- [x] `setup-odak-siparis-datasets.ps1` — `odak_musteriler` (legacyFirmId) + `odak_is_paketleri` + `odak_siparis_kalemleri`
- [x] AF: `odak-is-paketleri-form`

### Migrasyon scriptleri
- [x] `migrate-legacy-from-sql-dump.ps1` — tam akış (MySQL/Docker gerekmez)
- [x] `migrate-remaining-lines.ps1` — kalan kalemler (doğrudan POST)
- [x] `verify-legacy-dg-migration.ps1` · `analyze-line-gaps.ps1`
- [x] `lib/LegacySqlDumpCommon.ps1` — alan sayısına göre SQL tuple birleştirme
- [x] `lib/DgMigrationCommon.ps1` — legacy id map · `Get-DgTotalCount` fix

### Temizlik / onarım
- [x] `remove-orphan-siparis-kalemleri.ps1` — test/bozuk kayıtlar (~22 silindi)
- [x] `remove-conflicting-siparis-lines.ps1` — yanlış lineNo slotları (4 silindi)
- [x] `repair-odak-siparis-kalemleri-indexes.ps1` — Mongo `idx_parent_line` → `parentPackageId+lineNo`
- [x] Türkçe karakter: `Sanitize-JsonText` (mojibake → UTF-8)

### Bilinçli yapılmadı
- [ ] Hub UI deploy (kullanıcı: erken)
- [ ] Git commit/push

---

## Hub UI (kod hazır — deploy yok)

| Dosya | Amaç |
|-------|------|
| `Mng.Ui/utils/odakSiparisConfig.ts` | `packagesDataset`, `packagesFormCode` |
| `Mng.Ui/utils/odakSiparisService.ts` | Liste/detay/kalem istatistikleri (DG) |
| `Mng.Ui/pages/apps/odak-siparis/packages/index.vue` | `odak_is_paketleri` listesi |
| `Mng.Ui/pages/apps/odak-siparis/packages/[id]/index.vue` | DG detay |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisLinesPanel.vue` | `parentPackageId` |
| `Mng.Ui/pages/apps/automated-forms/view/[formCode].vue` | `parentPackageId` query |

**Eski POC (MO tabanlı — referans):** `packages/index.vue` önceki sürüm MO board kullanıyordu; yeni sürüm DG-only.

---

## Script envanteri (güncel)

| Script | Amaç |
|--------|--------|
| `setup-odak-siparis-datasets.ps1` | Toplu dataset + AF kurulum |
| `setup-odak-is-paketleri-dataset.ps1` | İş paketi dataset |
| `setup-odak-siparis-kalemleri-dataset.ps1` | Kalem dataset |
| `migrate-legacy-from-sql-dump.ps1` | Tam migrasyon (firm + paket + kalem) |
| `migrate-remaining-lines.ps1` | Eksik kalemler (encoding fix dahil) |
| `migrate-legacy-firms-to-dg.ps1` | Yalnız müşteriler |
| `migrate-legacy-package-to-dg.ps1` | Tek paket + kalemler |
| `remove-orphan-siparis-kalemleri.ps1` | Test/bozuk kalemler |
| `remove-conflicting-siparis-lines.ps1` | Yanlış slot doldurucular |
| `repair-odak-siparis-kalemleri-indexes.ps1` | Mongo index (SSH + Posh-SSH) |
| `verify-legacy-dg-migration.ps1` | Sayım doğrulama (`-UseSqlDump`) |
| `analyze-line-gaps.ps1` | Eksik kalem analizi |
| `clear-odak-siparis-kalemleri.ps1` | Tüm kalemleri sil (full re-run) |
| `patch-odak-siparis-side-menu.ps1` | Yan menü |
| `patch-odak-siparis-board-list.ps1` | Board listColumns (MO POC) |

**Kaynak dump:** `%USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql`

**Token:** `docs/odak/operationcore/scripts/get-operationcore-token.ps1` → `$env:TEMP\operationcore_dg_token.txt`

**Typical re-run (kalemler):**
```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\siparis\scripts\migrate-remaining-lines.ps1
.\docs\odak\siparis\scripts\verify-legacy-dg-migration.ps1 -UseSqlDump
```

---

## Faz ilerleme özeti

| Adım | Durum | Not |
|------|--------|-----|
| Dataset + AF (3 dataset) | ✅ | Odak test deploy |
| DG-only migrasyon (824 paket) | ✅ | MO yok |
| Kalem migrasyonu | ✅ ~%99,7 | 2759/2767 |
| Müşteri migrasyonu | ✅ | 87 legacy + 2 seed |
| Mongo index onarımı | ✅ | `parentPackageId+lineNo` |
| Hub UI (DG refactor) | 🟡 kod | Deploy bekliyor |
| MO POC (3 paket ODF) | ✅ eski | Artık birincil model değil |
| Kullanıcı walkthrough | ⏳ | UI deploy sonrası |
| MO `workItemId` entegrasyonu | ⏳ | Sonraki faz |
| Git commit | ⏳ | Bekliyor |

---

## Sonraki adımlar (önerilen sıra)

1. **Git commit** — script + dataset JSON + UI refactor (deploy olmadan)
2. **Hub UI deploy** — `mngui` Odak test · DG-only paket listesi/detay/kalemler
3. **Walkthrough** — kullanıcı checklist · eski Kalite ile karşılaştırma
4. **Hub iyileştirmeleri** — bulgulara göre (müşteri lookup, arama, create akışı)
5. **Kalan 8 kalem** — manuel inceleme (parse/slot/paket eksikleri)
6. **Eksik paket `"9"`** — SQL tuple onarımı veya manuel POST
7. **`parentWorkItemId` temizliği** — index düzeltildi; dataset'ten kaldırılabilir (opsiyonel)
8. **MO entegrasyonu** — `workItemId` alanı · üretim emri bağlantısı (Faz 1b+)
9. **Faz 2** — sevkiyat dataset'leri · PO PDF

---

## Referans ortamlar

| Ortam | Adres | Not |
|-------|--------|-----|
| Odak test (MonitraNG) | http://192.168.20.20:5040 (API) · :3000 (UI) | Migrasyon hedefi |
| SQL dump | `%USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql` | MySQL gerekmez |
| Lokal native | http://localhost:8080 | [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md) |
| Sunucu legacy | http://192.168.20.30/kalite/ | Canlı kaynak · 825 paket |

---

## Önemli teknik notlar

- DG **filter API güvenilmez** → migrasyon scriptleri client-side `legacyId` map kullanır.
- `Get-DgTotalCount`: string header `[int]$header[0]` hatası düzeltildi (ASCII code point bug).
- SQL parse: `),(` split yerine **alan sayısı birleştirme** (packages=27, packageitems=23, firms=19).
- Mongo eski index `parentWorkItemId+lineNo` duplicate 500 veriyordu → SSH ile drop edildi.
- Encoding: dump UTF-8; bazı alanlar mojibake → `Sanitize-JsonText` (ISO-8859-1 round-trip).
- `-LinesOnly` paket döngüsünde gruplama sorunu → `migrate-remaining-lines.ps1` kullanın.

---

## Mimari dokümanlar

- [README.md](./README.md) · [FAZ_PLANI.md](./FAZ_PLANI.md) · [MIMARI_KARAR.md](./MIMARI_KARAR.md)
- [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md) · [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md)
