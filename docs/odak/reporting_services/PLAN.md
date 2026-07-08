# Reporting Services — Plan

**Son güncelleme:** 9 Temmuz 2026  
**Ortam:** Odak test · UI test sunucuda deploy edilebilir  
**Durum:** R2 katalog + Odak Eğitim POC raporu

## Amaç

Kullanıcıların DG dataset’lerinden parametreli **tablo raporları** tasarlayıp çalıştırabildiği bir Raporlama modülü.

## Ana aktörler (ürün)

| Aktör | Kısa tanım | Şu an |
|-------|------------|--------|
| **Data Source** | DG dataset | ✅ Seçim + şema |
| **Tablo** | Kolon + sıralama + sayfalama | ✅ |
| **Parametreler** | Durum sekmesi, yıl, arama, kişi | ✅ Bağımsız AND filtreleri |
| **Expand** | Satır detayı + bağlı dataset sekmeleri | ✅ Runtime · Designer Faz 2 |
| **Katalog** | Kayıtlı rapor tanımı (localStorage) | ✅ |
| **Yetki** | Sütun + rapor görünürlüğü | ✅ |
| **Dashboard** | Çoklu rapor / layout | 🔲 Sonra |
| **Dokümantasyon** | Yardım, alan sözlüğü | 🔲 Sonra |

## Çalışma prensibi (güncel)

```
Report definition (local catalog)
  → parameters → AfListFilter[] (GET ?filter=)
  → expand.tabs[] → child dataset (linkField = parent __dataId)
        ↓
GET /api/v1/data/{dataset}?fields=&filter=&sort=&skip=&limit=&expand=true
        ↓
Table + expand panel (Genel + child tabs)
```

İsteğe bağlı: `POST /query` + `match` (ileride karmaşık match; DG `DatetimeMatchFilterExpander` hazır).

## Fazlar

| Faz | Hedef | Durum |
|-----|--------|--------|
| **R0** | Starter designer | ✅ |
| **R1** | Parametre UX, CSV | ✅ büyük ölçüde |
| **R2** | Katalog + designer/runner | ✅ |
| **R2b** | Expand child list tabs | ✅ seed · 🔲 designer UI |
| **R3** | Named query + merkezi kayıt (DG) | 🔲 |
| **R4** | Dashboard köprüsü | 🔲 |

## Odak Eğitim POC

- Rapor: `rpt_odak_egitim_trainings` — dataset `odak_egitimler`
- Parametreler: durum (Planlanan / Tamamlanan / Tümü), yıl (`gerceklesenTarih`), arama
- Expand: Genel (`konu`, `konum`, …) + **Katılımcılar** (`odak_egitim_katilimlari`)

## İlgili kod

- UI: `Mng.Ui/pages/apps/reporting/`
- Seed: `Mng.Ui/utils/reportingOdakEgitimSeeds.ts`
- Expand tab: `Mng.Ui/utils/reportingOdakEgitimExpandMigrations.ts`
- DG: `FilterParser`, `DatetimeMatchFilterExpander`
