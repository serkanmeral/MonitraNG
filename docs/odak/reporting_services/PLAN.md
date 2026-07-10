# Reporting Services — Plan

**Son güncelleme:** 11 Temmuz 2026  
**Ortam:** Odak test `192.168.20.20` · DG + UI  
**Durum:** R2–R2c · R3a (DG katalog) · belge D0–D4 · linkleme · Viewer 1–2 · Export B1 · Odak Eğitim POC

## Amaç

Kullanıcıların DG dataset’lerinden parametreli **tablo raporları** tasarlayıp çalıştırabildiği bir Raporlama modülü.

## Ana aktörler (ürün)

| Aktör | Kısa tanım | Şu an |
|-------|------------|--------|
| **Data Source** | DG dataset | ✅ Seçim + şema |
| **Tablo** | Kolon + sıralama + sayfalama | ✅ |
| **Parametreler** | Durum, yıl (`orDateFields`), arama, kişi | ✅ · 🔲 iyileştirme major |
| **Expand** | Satır detayı + child sekmeler + yetki | ✅ |
| **Özet** | count/sum · cards/footer · aggregate (+ text search) | ✅ POC |
| **Katalog** | Rapor tanımı | ✅ **DG** (`@reporting_*`) · LS migrate |
| **Yetki** | Sütun + rapor + child sekme/sütun | ✅ |
| **Belge (DI)** | Şablon bağ + üret (reportRun/parent/child) | ✅ D0–D4 |
| **Viewer** | Browse + embed + link kopyala | ✅ 1–2. dilim |
| **Linkleme** | Sütun → rapor deep link | ✅ |
| **Export** | CSV + Excel + sütun seçimi + soft cap | ✅ B1 · 🔲 B2 (PDF…) |
| **Otomatik raporlar** | Zamanlama + dağıtım | 🔲 Major |
| **Dynamic Form** | Form benzeri parametre UI | 🔲 Major |
| **Dokümantasyon** | Yardım, alan sözlüğü | 🔲 (DI belge ayrı kanal) |
| **Dashboard** | Çoklu rapor / layout | 🔲 R4 |

## Çalışma prensibi (güncel)

```
Report definition (DG @reporting_reports)
  → parameters → AfListFilter[] (+ orDateFields → POST /query match)
  → expand.tabs[] → child dataset
  → summary → POST /aggregate
  → optional: reportLink / share URL / export (csv|xlsx)
        ↓
Browse | Embed | Runner (+ belgeler DI)
```

## Fazlar

| Faz | Hedef | Durum |
|-----|--------|--------|
| **R0** | Starter designer | ✅ |
| **R1** | Parametre UX, CSV | ✅ |
| **R2** | Katalog + designer/runner | ✅ |
| **R2b** | Expand child list tabs | ✅ |
| **R2c** | Summary aggregate | ✅ POC |
| **R3a** | Merkezi kayıt (DG) | ✅ |
| **R3b** | Named query | ⏸ Ertelendi |
| **R4** | Dashboard köprüsü | 🔲 |

## Major backlog (güncel)

1. Parametre iyileştirmesi  
2. Export B2 (PDF, özet satırı, …)  
3. R3a cilası (migrate UX, yazma yetkisi)  
4. Otomatik raporlar  
5. Dynamic Form  
6. R4 Dashboard  
7. R3b Named query *(ertelendi)*  

Oturum notları: [DEVAM.md](DEVAM.md)

## Odak Eğitim POC

- `rpt_odak_egitim_trainings` / `rpt_odak_egitim_person_trainings`
- Expand Katılımcılar → `personelId` rapor linki (yeni sekme)
- Menü: **Raporlar** (browse) · **Rapor kataloğu**

## İlgili kod

- UI: `Mng.Ui/pages/apps/reporting/` · `components/apps/reporting/`
- Katalog DG: `reportingCatalogDg.ts`
- Link / share / export: `reportingColumnLink.ts`, `reportingShareLink.ts`, `reportingExport*.ts`
- Seed: `reportingOdakEgitimSeeds.ts` · `reportingOdakEgitimExpandMigrations.ts`
- DG: `POST …/query` · `POST …/aggregate` · `@reporting_*` dataset’ler
