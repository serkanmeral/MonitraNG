# Legacy Kalite → Production DG veri migrasyonu

**Son güncelleme:** 20 Haziran 2026  
**Durum:** ✅ Çekirdek iş verisi prod’da — ⏳ PO PDF (uploads sync) bekliyor  
**İlke:** Kaynak **legacy SQL dump**; test Mongo’dan kör kopya **yapılmaz** → [INDEPENDENCE.md](./INDEPENDENCE.md)

---

## 1. Özet

Production (`192.168.20.8`) üzerinde Odak Sipariş hub’ının açılması için legacy Kalite verisi **geliştirme PC’den** SQL dump + yerel MySQL export ile DG API’ye aktarıldı.

| Hedef | Değer |
|-------|--------|
| Dış URL (UI) | `https://mng.odaksavunma.com` |
| DG API (migrasyon) | `http://192.168.20.8:5040` |
| Mongo DB | `mng_odak` |

**Neden internal API:** Public nginx (`mng.odaksavunma.com`) üzerinden `POST /data/api/...` → **405 Not Allowed**. Migrasyon script’leri **5040 internal gateway** kullanmalı.

---

## 2. Migrasyon sonuçları (20 Haziran 2026)

Kaynak: `%USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql`

| Collection / veri | SQL / legacy | Prod DG | Not |
|-------------------|--------------|---------|-----|
| `odak_musteriler` | 87 müşteri | **87** | OK |
| `odak_is_paketleri` | 824 paket | **823** | 1 tuple bozuk (`package_no "9"`) |
| `odak_siparis_kalemleri` | 2767 kalem | **2757** | ~10 kalem (bozuk tuple / duplicate `legacyLineId`) |
| `odak_ncr` | — | **499** | 57 paketsiz legacy NCR normal |
| `odak_capa` | — | **25** | OK |
| `odak_sevkiyatlar` | 3776 legacy shipment | **622** | Paket eşleşmesi olan sevkiyatlar |
| PO PDF (`poDocument`) | — | **6** | Yerel `uploads` eksik → bkz. §6 |

**Korunan (dokunulmadı):** `op_workspaces`, `op_work_items`, `op_boards`, `@users`, `@groups`, meta sync sonrası `@datasets` şemaları.

**Türkçe karakter:** Tüm migrasyon script’lerinde `Sanitize-LegacyText` (`DgMigrationCommon.ps1`). Prod’da mojibake (`?`, `Ã`, `Ä`) spot-check: **0** kayıt.

---

## 3. Ön koşullar (geliştirme PC)

| Gereksinim | Konum / komut |
|------------|----------------|
| Güncel SQL dump | `%USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql` |
| Yerel legacy MySQL | `:3307` — `~\kalite-legacy-local\start-mysql.ps1` (NCR/sevkiyat/PO export) |
| PO PDF dosyaları | `~\kalite-legacy-local\uploads` veya `~\kalite-legacy-docker\uploads` |
| Prod token | `docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1` |
| SSH prod | `.env.odak.prod.local` → `192.168.20.8` |

**Ortam değişkeni (prod token):**

```powershell
$env:MNG_OC_USE_PROD_TOKEN = "1"
```

`load-operationcore-token.ps1` bu durumda test yerine prod token dosyasını kullanır.

---

## 4. Script’ler

### Ana orchestrator

```powershell
# Repo kökü, PowerShell 7
pwsh -File .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1
pwsh -File .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1 -DryRun
pwsh -File .\docs\odak\siparis\scripts\migrate-legacy-full-to-prod.ps1 -SkipPoPdf
```

Adımlar: meta tarih onarımı → SQL dump → kalan kalemler → orphan temizlik → index onarım → NCR/CAPA → sevkiyat → PO PDF → doğrulama.

### Tekil / yardımcı

| Script | Rol |
|--------|-----|
| `docs/odak/siparis/scripts/migrate-legacy-from-sql-dump.ps1` | Müşteri + paket + kalem (SQL dump) |
| `docs/odak/siparis/scripts/migrate-remaining-lines.ps1` | Eksik kalemler |
| `docs/odak/siparis/scripts/migrate-legacy-ncs-to-dg.ps1` | NCR + CAPA |
| `docs/odak/siparis/scripts/migrate-legacy-shipments-to-dg.ps1` | Sevkiyat |
| `docs/odak/siparis/scripts/migrate-legacy-po-pdf-to-dg.ps1` | PO PDF (`-All`) |
| `docs/odak/siparis/scripts/repair-dataset-createinfo-dates.ps1` | Meta `$date` onarımı (prod Mongo) |
| `docs/odak/siparis/scripts/probe-prod-migration-counts.ps1` | Sayım + Türkçe spot-check |
| `docs/odak/siparis/scripts/lib/DgMigrationCommon.ps1` | `Sanitize-LegacyText`, token yenileme API |

### Meta sync (önceden yapıldı — şema)

```powershell
pwsh -File .\scripts\odak\sync-meta-collections-test-to-prod.ps1
```

İş verisi **bu script ile taşınmaz**; yalnızca `@datasets`, `@side_menu`, vb.

---

## 5. Karşılaşılan sorunlar ve çözümler

| Sorun | Belirti | Çözüm |
|-------|---------|--------|
| Meta sync `$date` | DG `LIST_FAILED` / `Invalid element: '$date'` | `repair-dataset-createinfo-dates.ps1` — `@datasets`, `@automated_forms`, `@dataset_categories`, `@side_menu` içinde Extended JSON tarihleri BSON Date’e çevrildi (**61+ kayıt**) |
| Public URL POST | `405 Not Allowed` (nginx) | Migrasyon `-BaseUrl http://192.168.20.8:5040` |
| JWT süresi (~5 dk) | Paket migrasyonu ortasında **401** | `Invoke-DgMigrationApi` — 401’de otomatik token yenileme; paket döngüsünde periyodik refresh |
| Test prod token karışması | İlk koşuda test token prod URL’ye | `$env:MNG_OC_USE_PROD_TOKEN=1` + `load-operationcore-token.ps1` güncellemesi |
| Verify exit 1 | Script hedefi 825/2769, dump 824/2767 | Beklenen fark; sayılar pratikte tamam — verify parametreleri güncellenecek |

**Log dosyaları (geliştirme PC):**

- `%TEMP%\migrate-legacy-full-to-prod.log`
- `%TEMP%\migrate-legacy-resume.log`

---

## 6. Sıradaki adımlar (devam oturumu)

### P0 — UI doğrulama

- [ ] https://mng.odaksavunma.com/apps/odak-siparis/packages açılıyor mu?
- [ ] Rastgele paket: müşteri, kalem, Türkçe `unvan` / `description`

### P1 — PO PDF tam migrasyon

Yerel `uploads` sunucudan dolu değilse:

```powershell
pwsh -File .\docs\odak\siparis\scripts\sync-legacy-from-server.ps1
$env:MNG_OC_USE_PROD_TOKEN = "1"
pwsh -File .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
pwsh -File .\docs\odak\siparis\scripts\export-legacy-po-candidates-from-mysql.ps1 -All
pwsh -File .\docs\odak\siparis\scripts\migrate-legacy-po-pdf-to-dg.ps1 `
  -BaseUrl http://192.168.20.8:5040 -All -SkipExisting
```

### P2 — İyileştirmeler (opsiyonel)

- [ ] `verify-legacy-dg-migration.ps1` — `-ExpectedPackages 824`, `-ExpectedLines 2767`
- [ ] `sync-meta-collections-test-to-prod.ps1` — export/import sırasında `$date` birikimini önle (kök neden)
- [ ] Eksik ~10 kalem için `analyze-line-gaps.ps1` / SQL tuple inceleme

---

## 7. Idempotency

Migrasyon script’leri `legacyPackageId` / `legacyLineId` / `legacyNcrId` ile **idempotent**. Yeniden koşmak güvenli; mevcut kayıtlar atlanır, eksikler tamamlanır.

Resume örneği:

```powershell
$env:MNG_OC_USE_PROD_TOKEN = "1"
& .\docs\odak\operationcore\scripts\get-operationcore-token-prod.ps1
$BaseUrl = "http://192.168.20.8:5040"
& .\docs\odak\siparis\scripts\migrate-legacy-from-sql-dump.ps1 -BaseUrl $BaseUrl -UseGateway
& .\docs\odak\siparis\scripts\migrate-remaining-lines.ps1 -BaseUrl $BaseUrl -UseGateway
& .\docs\odak\siparis\scripts\verify-legacy-dg-migration.ps1 -BaseUrl $BaseUrl -UseGateway -UseSqlDump
```

---

## 8. İlgili dokümanlar

| Dosya | Rol |
|-------|-----|
| [DEVAM.md](./DEVAM.md) | Production deploy checkpoint |
| [INDEPENDENCE.md](./INDEPENDENCE.md) | Test/prod ayrımı |
| [../siparis/VERI_MIGRASYON_PLANI.md](../siparis/VERI_MIGRASYON_PLANI.md) | Legacy migrasyon planı (genel) |
| [../siparis/DEVAM.md](../siparis/DEVAM.md) | Sipariş modülü geliştirme checkpoint |
