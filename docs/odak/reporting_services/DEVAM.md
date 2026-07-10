# Reporting Services — Devam

**Son güncelleme:** 11 Temmuz 2026 (oturum kapanışı)  
**Ortam:** Odak test `192.168.20.20` · lokal `npm run dev`  
**Commit:** `7ed97936` — `feat(mng.ui): reporting viewer share/embed and export xlsx`  
**Deploy:** UI (`mngui`) bu kapanışta · DG zaten test’te

---

## Nerede kaldık

**R2 + R2b + R2c** canlı. Bu oturumda (10–11 Tem): DI belge (D0–D4), browse viewer, **R3a** merkezi katalog, **rapor linklemeleri**, **Viewer 2. dilim**, **Export B1**.

**Sıradaki (önerilen):** parametre iyileştirmesi · Export B2 (PDF/özet) · R3a cilası · otomatik raporlar · Dynamic Form.  
**Ertelendi:** **R3b Named query** (bilinçli).

Kaldığınız yerden devam: bu dosyanın «Sıradaki adaylar» + «Nasıl denerim» bölümleri.

---

## Bu oturumda tamamlananlar (10–11 Tem 2026)

### DI belge entegrasyonu (D0–D4 + kalıplar)

- Rapor ↔ DI şablon bağları (`documentBindings`)
- `reportRun` / `parentRow` / `childRow` üretim
- Belge adı / `generatedAt` token kalıpları
- Odak Eğitim seed şablonları (xlsx/docx)

### Browse / Raporlar (Viewer 1)

- `/apps/reporting/browse` — ağaç + runner; admin araçları yok
- Menü: **Raporlama** → **Raporlar** (+ Rapor kataloğu)

### R3a — merkezi katalog (DG)

- Dataset: `@reporting_categories`, `@reporting_reports`
- LS → DG bir kerelik migrate; seed hydrate sonrası DG’ye yazılır
- Script: `docs/odak/reporting_services/scripts/setup-reporting-catalog-datasets.ps1`
- **R3b named query yok** (ertelendi)

### Rapor linklemeleri

- Sütun `reportLink` → browse deep link (`reportId` + param query)
- Designer: sütun ayarları → **Rapor linki**
- Seed: Katılımcılar `personelId` → `rpt_odak_egitim_person_trainings`

### Viewer 2. dilim

- **Linki kopyala** (auth’lu URL; parametreler dahil)
- Salt-okunur: `showAdminTools=false` → tasarım + DG pipeline kapalı; CSV/belge açık
- `/apps/reporting/embed?reportId=…` — blank layout, gömülü yüzey

### Export B1

- Dialog: Excel (xlsx) / CSV + sütun seçimi
- Filtrelenmiş **tüm** satırlar (sayfa değil); soft cap **5000** + onay
- Bağımlılık: `xlsx` (`Mng.Ui/package.json`)

---

## Sıradaki adaylar

| Öncelik | Konu | Not |
|---------|------|-----|
| **D** | Parametre iyileştirmesi | Bağımlı alanlar, relation→özet arama, UX |
| **B2** | Export devamı | PDF, özet satırı Excel’de, child export |
| **E** | R3a cilası | Migrate UX, yazma yetkisi (manager) |
| **C** | Otomatik raporlar | Scheduler + e-posta — büyük |
| **F** | Dynamic Form | Form benzeri parametre UI |
| **H** | R4 Dashboard | Çoklu rapor layout |
| — | **R3b Named query** | **Ertelendi** |

---

## Nasıl denerim

1. **Lokal:** `Mng.Ui` → `npm run dev`
2. **Test:** http://192.168.20.20:3000/apps/reporting/browse
3. Menü: **Raporlar** / **Rapor kataloğu**

**Smoke (bu kapanış):**

- [ ] Browse → eğitim listesi → expand → Katılımcı adına tık → yeni sekmede personel raporu
- [ ] **Linki kopyala** → yeni sekmede aynı rapor + parametreler
- [ ] `/apps/reporting/embed?reportId=rpt_odak_egitim_trainings` → sade runner
- [ ] **Dışa aktar** → Excel/CSV; sütun seçimi; filtreyle uyumlu satır sayısı
- [ ] Katalog kaydı DG’de kalıcı (başka tarayıcı/oturum)

---

## Önemli dosyalar

| Alan | Dosya |
|------|--------|
| Katalog DG | `reportingCatalogDg.ts`, `reportingCatalogService.ts`, `reportingCategoryService.ts` |
| Linkleme | `reportingColumnLink.ts`, `ReportingListColumnReportLinkDialog.vue` |
| Share / embed | `reportingShareLink.ts`, `pages/apps/reporting/embed/` |
| Export | `reportingExport.ts`, `reportingExportFetch.ts`, `ReportingExportDialog.vue` |
| Browse | `pages/apps/reporting/browse/` |
| Belge | `reportingDocumentGenerate.ts`, `reportingDocumentTokens.ts` |
| Seed | `reportingOdakEgitimSeeds.ts`, `reportingOdakEgitimExpandMigrations.ts` |
| Dataset setup | `docs/odak/reporting_services/scripts/setup-reporting-catalog-datasets.ps1` |
| Plan / devam | `PLAN.md`, `DEVAM.md` |

---

## Kurallar

- Test: `192.168.20.20`
- UI deploy: talep ile (`sync-odak-source.ps1 -Paths Mng.Ui` + `deploy-odak-apps.ps1 -Services mngui -NoCache`)
- Commit/push: yalnızca talep edilince
- SSH: Odak test · şifre oturumda verildiğinde
