# Reporting Services — Devam

**Son güncelleme:** 9 Temmuz 2026 (gece)  
**Ortam:** Odak test `192.168.20.20` · lokal `npm run dev`  
**Deploy:** UI (`mngui`) bu oturum sonunda test sunucuya alındı

---

## Nerede kaldık

**Generic raporlama modülü (R2+)** UI’da çalışır durumda. Odak Eğitim **Eğitim listesi** raporu (`rpt_odak_egitim_trainings`) katalog seed ile geliyor; çalıştırıcı + designer + expand panel (Genel + **Katılımcılar** sekmesi) test edildi.

**Sorgu düzeltildi:** Yıl filtresi tekrar tek alan (`gerceklesenTarih`) + GET `?filter=`; gereksiz `$or` (planlananTarih | gerceklesenTarih) ve POST `/query` yolu kaldırıldı.

**Expand Katılımcılar sekmesi** çalışıyor; designer’da **düzenleme UI yok** (Faz 2) — yalnızca salt okunur özet + seed/migration (`expand.tabs[]`).

---

## Bu oturumda tamamlananlar

### Raporlama çekirdeği (Mng.Ui)

- Katalog: localStorage + `ReportingCatalogService` / kategori ağacı
- Designer: `/apps/reporting/designer/{id}` — kolonlar, expand düzeni, parametreler, varsayılan filtreler, sütun/rapor yetkisi
- Runner: `/apps/reporting/run/{id}` — parametre paneli, tablo, CSV, DG sorgu önizleme (`showQuery`)
- Parametre modeli: bağımsız AND filtreleri (`choiceFilters`, `datePartRange`, `search`, `personPicker`)
- Yıl combobox: statik `yearRange` (2017–güncel yıl), DG’den çekilmiyor
- «Tümü» durum sekmesi: `durum in Planlandi,Tamamlandi` (boş filtre değil)

### Expand panel — bağlı liste sekmeleri

- `expand.tabs[]` + `ReportingChildListPanel` + `fetchReportingChildList`
- Odak seed: `ODAK_EGITIM_PARTICIPANTS_EXPAND_TAB` → `odak_egitim_katilimlari` / `parentTrainingId` ← `__dataId`
- Runtime migration: `ensureOdakEgitimParticipantsExpandTab`
- Plugin: `reporting-catalog-seeds.client.ts` (uygulama açılışında bootstrap)

### Düzeltmeler

- `orDateFields` + `$or` yıl filtresi geri alındı → GET filtreleri
- Vite duplicate import uyarıları (`reportingParameterModel` re-export) temizlendi
- Expand `tabs` parse: `reportingCatalogStorage.parseExpandConfig`
- Designer `resetExpandDefaults` → `tabs` korunur

### MngDataGateway (tarih filtreleri)

- `DatetimeMatchFilterExpander` — POST `/query` match içinde string → BSON Date (`$and`/`$or` içinde)
- `FilterParser` iyileştirmeleri + test scriptleri (`scripts/tests/MngDataGateway/filter/`)
- **Not:** Test sunucuda DG yeniden deploy edilmediyse POST match fix canlıda olmayabilir; bu rapor artık GET kullanıyor

### Diğer

- i18n (tr/en), welcome registry, sidebar fallback
- Side menu script: `docs/odak/reporting_services/scripts/patch-reporting-side-menu.ps1`

---

## Nasıl denerim

1. **Lokal:** `Mng.Ui` → `npm run dev` → `/apps/reporting` veya runner `/apps/reporting/run/rpt_odak_egitim_trainings`
2. **Test sunucu:** `http://192.168.20.20:3000` (mngui deploy sonrası)
3. Menü: Raporlama (side menu patch gerekirse script çalıştır)

**Kontrol listesi (smoke):**

- [ ] Tamamlanan + yıl 2017 → `durum=Tamamlandi` AND `gerceklesenTarih` aralığı, **$or yok**
- [ ] Expand → Genel alanlar + **Katılımcılar** sekmesi
- [ ] Designer → Expand sekmesinde «Bağlı liste sekmeleri» bilgi kutusu (salt okunur)

---

## Sıradaki (yarın)

1. **Expand designer Faz 2** — `tabs[]` UI: dataset seçimi, link alanı, sütun editörü (`ReportingExpandLayoutPanel`)
2. **Planlanan + yıl** — yıl filtresi şu an her zaman `gerceklesenTarih`; «Planlanan» sekmesinde `planlananTarih` istenirse status’a göre alan seçimi (coupling olmadan) tartışılacak
3. **DG deploy** — `DatetimeMatchFilterExpander` fix’i test sunucuya (ileride POST match kullanılırsa)
4. **Side menu / katalog** — raporlama menü kayıtları production hazırlığı
5. **Personel eğitim geçmişi** raporu (`rpt_odak_egitim_person`) smoke test

---

## Önemli dosyalar

| Alan | Dosya |
|------|--------|
| Tipler | `Mng.Ui/types/apps/reporting.ts` |
| Katalog seed | `Mng.Ui/utils/reportingOdakEgitimSeeds.ts` |
| Katılımcı expand tab | `Mng.Ui/utils/reportingOdakEgitimExpandMigrations.ts` |
| Parametre → filtre | `Mng.Ui/utils/reportingParameterModel.ts`, `reportingParameters.ts` |
| Child liste | `Mng.Ui/utils/reportingChildList.ts`, `ReportingChildListPanel.vue` |
| Expand UI | `ReportingExpandPanel.vue`, `ReportingExpandLayoutPanel.vue` |
| DG datetime | `MngDataGateway/.../DatetimeMatchFilterExpander.cs` |

---

## Kurallar

- Test: `192.168.20.20`
- UI deploy: kullanıcı talebi ile (`deploy-odak-apps.ps1 -Services mngui`)
- Backend deploy: DG değişiklikleri ayrı (`mngdatagateway` veya tam apps)
