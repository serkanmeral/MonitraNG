# Odak Sipariş — Devam noktası (checkpoint)

**Son güncelleme:** 16 Haziran 2026  
**Durum:** ✅ DG-only migrasyon (~%99,7 kalem) · ✅ Hub UI Odak deploy · ⏳ Walkthrough · MO sonraki faz

> **⭐ KALDIĞIMIZ YER (16 Haz 2026 — oturum kapanışı):** Hub UI paket listesi/detay **Odak test'e deploy edildi** (`mngui`). **DG** tarafında `FilterParser` düzeltmeleri (`mngdatagateway`) canlı: çoklu müşteri `in` filtresi + ISO string tarih filtreleri. Liste UI: sayfalama, gelişmiş filtre (AfListFilters), sunucu sıralama, sabit İşlemler sütunu, performans (line stats lazy). **Sıradaki:** kullanıcı walkthrough (Odak `:3000`) · legacy Kalite karşılaştırma · kalan 8 kalem · MO `workItemId` (ileride).

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

## Oturumda tamamlanan işler (15–16 Haziran 2026)

### Hub UI (liste + detay)

- [x] **i18n** — `odakSiparis.*` (tr/en)
- [x] **Sayfalama** — `v-data-table-server` + DG `X-Total-Count` (`operationCoreService.parseListResponseWithTotal`)
- [x] **Gelişmiş filtre** — `AfListFilters` (paket alanları; kalem araması ayrı panel, client-side max 500)
- [x] **Sunucu sıralama** — `packageNo`, `name`, `customerId.unvan`, tarihler, sayılar; varsayılan `-packageNo`
- [x] **Sabit İşlemler sütunu** — OcBoardPanel ile aynı sticky CSS
- [x] **Performans** — normal listede line stats yok; detay özet sekmesinde kalemler lazy; müşteri label cache
- [x] **CRUD** — liste/detay gör/düzenle/sil; AF form `returnTo`

### DG backend (Odak deploy ✅)

- [x] **FilterParser** — `in`/`nin` çoklu değer: JSON dizi + köşeli parantez içi virgül koruması
- [x] **FilterParser** — tarih filtreleri ISO **string** alanlarıyla uyumlu (`YYYY-MM-DD`, lte/gt gün sonu genişletme, eq regex)
- [x] **afListFilters.ts** — çoklu seçim `JSON.stringify` ile DG'ye iletim
- [x] **AfListFilters.vue** — tarih girişi `type="date"` (takvim günü)

### Deploy

- [x] `mngdatagateway` Odak (`--no-cache`) — 16 Haz
- [x] `mngui` Odak — 16 Haz (oturum kapanış)
- [x] Git commit + push — 16 Haz

### Diagnostic

- [x] `docs/odak/diagnostic/scripts/diagnostic-odak-siparis-pages.ps1`
- [x] Rapor: `reports/oc_pages_odak_siparis_20260615_234610.json` (liste darboğaz: line stats — P1 ile giderildi)

### Önceki oturum (14–15 Haz) — özet

- [x] Dataset + migrasyon scriptleri + Mongo index onarımı (bkz. git `8c3d749`)

---

## Hub UI dosyaları

| Dosya | Amaç |
|-------|------|
| `Mng.Ui/utils/odakSiparisConfig.ts` | Dataset / form kodları |
| `Mng.Ui/utils/odakSiparisService.ts` | Liste, filtre, sıralama, müşteri cache |
| `Mng.Ui/pages/apps/odak-siparis/packages/index.vue` | Paket listesi |
| `Mng.Ui/pages/apps/odak-siparis/packages/[id]/index.vue` | Detay |
| `Mng.Ui/components/apps/odak-siparis/OdakSiparisLinesPanel.vue` | Kalemler paneli |
| `Mng.Ui/utils/afListFilters.ts` · `AfListFilters.vue` | Gelişmiş filtre (paylaşımlı) |
| `Mng.Ui/services/operationCoreService.ts` | `_totalCount` sayfalama |

**Odak UI:** http://192.168.20.20:3000/apps/odak-siparis/packages

---

## Faz ilerleme özeti

| Adım | Durum | Not |
|------|--------|-----|
| Dataset + AF (3 dataset) | ✅ | Odak test |
| DG-only migrasyon (824 paket) | ✅ | MO yok |
| Kalem migrasyonu | ✅ ~%99,7 | 2759/2767 |
| Hub UI (DG refactor) | ✅ | Odak `mngui` deploy |
| DG filtre düzeltmeleri | ✅ | `mngdatagateway` deploy |
| Git commit/push | ✅ | 16 Haz |
| Kullanıcı walkthrough | ⏳ | **Sıradaki** |
| Yan menü patch | ⏳ | `patch-odak-siparis-side-menu.ps1` |
| Kalan 8 kalem + paket `"9"` | ⏳ | Veri |
| MO `workItemId` entegrasyonu | ⏳ | Sonraki faz |

---

## Sonraki adımlar (yarın — önerilen sıra)

1. **Walkthrough** — Odak `:3000` paket listesi/detay/kalemler checklist (aşağı)
2. **Legacy Kalite karşılaştırma** — [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md) `localhost:8080`
3. **Walkthrough bulguları** — küçük UI düzeltmeleri
4. **`patch-odak-siparis-side-menu.ps1`** — yan menü (henüz çalıştırılmadıysa)
5. **Kalan 8 kalem** — manuel inceleme
6. **Eksik paket `"9"`** — SQL tuple veya manuel POST
7. **P5 (opsiyonel)** — kalem araması için backend aggregate (client 500 paket limiti kaldırma)
8. **MO entegrasyonu** — `workItemId` · Faz 1b+
9. **Faz 2** — sevkiyat · PO PDF

### Walkthrough checklist (kısa)

**Liste** `/apps/odak-siparis/packages`

- [ ] Sekmeler + sayfalama + toplam kayıt
- [ ] Sütun sıralama (müşteri dahil)
- [ ] Gelişmiş filtre: metin, müşteri (çoklu `in`), tarih
- [ ] Yatay scroll → İşlemler sabit
- [ ] Gör / düzenle / sil / yeni paket
- [ ] Kalem araması paneli (yavaş — bilinçli)

**Detay**

- [ ] Özet vs kalemler sekmesi (lazy load)
- [ ] Müşteri linki · audit alanları

---

## Script envanteri

| Script | Amaç |
|--------|--------|
| `setup-odak-siparis-datasets.ps1` | Toplu dataset + AF |
| `migrate-legacy-from-sql-dump.ps1` | Tam migrasyon |
| `migrate-remaining-lines.ps1` | Eksik kalemler |
| `verify-legacy-dg-migration.ps1` | Sayım doğrulama |
| `patch-odak-siparis-side-menu.ps1` | Yan menü |
| `docs/odak/diagnostic/scripts/diagnostic-odak-siparis-pages.ps1` | Sayfa performans |

**Token:** `docs/odak/operationcore/scripts/get-operationcore-token.ps1`

**UI deploy (tekrar):**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui
.\scripts\odak\deploy-odak-apps.ps1 -Services mngui
```

**DG deploy (tekrar):**
```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths MngDataGateway
.\scripts\odak\deploy-odak-apps.ps1 -Services mngdatagateway -NoCache
```

---

## Referans ortamlar

| Ortam | Adres | Not |
|-------|--------|-----|
| Odak test | http://192.168.20.20:5040 · :3000 | Migrasyon + UI |
| SQL dump | `%USERPROFILE%\kalite-legacy-docker\db\init\01-kalite.sql` | |
| Lokal legacy | http://localhost:8080 | [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md) |
| Sunucu legacy | http://192.168.20.30/kalite/ | 825 paket |

---

## Önemli teknik notlar

- Migrasyon scriptleri **client-side legacyId map** kullanır (DG filter API liste sorgularında güvenilir).
- Paket tarih alanları Mongo'da **ISO string** (`2019-05-05T21:00:00.0000000Z`) — FilterParser artık string karşılaştırma / gün regex kullanır; BSON Date'e çevirmez.
- Çoklu `in` filtresi: `customerId:in:["id1","id2"]` (virgülle birleştirilmiş eski format **bozuk**).
- Liste performans: normal modda `fetchPackageLineStatsMap` yok; kalem araması max 500 paket + client filtre.
- `Get-DgTotalCount` / `_totalCount`: sayfalama toplamı için UI fix.

---

## Mimari dokümanlar

- [README.md](./README.md) · [FAZ_PLANI.md](./FAZ_PLANI.md) · [MIMARI_KARAR.md](./MIMARI_KARAR.md)
- [VERI_MIGRASYON_PLANI.md](./VERI_MIGRASYON_PLANI.md) · [NATIVE_LOCAL_PLAN.md](./NATIVE_LOCAL_PLAN.md)
