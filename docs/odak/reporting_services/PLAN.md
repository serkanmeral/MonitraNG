# Reporting Services — Plan

**Son güncelleme:** 10 Temmuz 2026  
**Ortam:** Odak test `192.168.20.20` · UI deploy edildi (`494635f1`)  
**Durum:** R2 + R2b (expand designer) + R2c (summary aggregate) · Odak Eğitim POC canlı

## Amaç

Kullanıcıların DG dataset’lerinden parametreli **tablo raporları** tasarlayıp çalıştırabildiği bir Raporlama modülü.

## Ana aktörler (ürün)

| Aktör | Kısa tanım | Şu an |
|-------|------------|--------|
| **Data Source** | DG dataset | ✅ Seçim + şema |
| **Tablo** | Kolon + sıralama + sayfalama | ✅ |
| **Parametreler** | Durum sekmesi, yıl, arama, kişi | ✅ Bağımsız AND · 🔲 iyileştirme major |
| **Expand** | Satır detayı + bağlı dataset sekmeleri | ✅ Runtime + designer (Bağlantı/Sütunlar/Özet) |
| **Özet** | count/sum · cards/footer · DG aggregate | ✅ POC |
| **Katalog** | Rapor tanımı (localStorage) | ✅ |
| **Yetki** | Sütun + rapor görünürlüğü | ✅ · 🔲 child tab yetkisi |
| **Viewer** | Salt okunur / paylaşımlı görüntüleyici | 🔲 Major |
| **Otomatik raporlar** | Zamanlama + dağıtım | 🔲 Major |
| **Export** | CSV ötesi | 🔲 Major (CSV var) |
| **Linkleme** | Raporlar arası / deep link | 🔲 Major |
| **Dynamic Form** | Form benzeri parametre UI | 🔲 Major |
| **Dokümantasyon** | Yardım, alan sözlüğü | 🔲 Major |
| **Dashboard** | Çoklu rapor / layout | 🔲 Sonra (R4) |

## Çalışma prensibi (güncel)

```
Report definition (local catalog)
  → parameters → AfListFilter[] (GET ?filter=)
  → expand.tabs[] → child dataset (linkField = parent __dataId)
  → summary → POST /aggregate ($match + $group)
        ↓
Table + expand panel + cards/footer
```

## Fazlar

| Faz | Hedef | Durum |
|-----|--------|--------|
| **R0** | Starter designer | ✅ |
| **R1** | Parametre UX, CSV | ✅ büyük ölçüde |
| **R2** | Katalog + designer/runner | ✅ |
| **R2b** | Expand child list tabs + designer | ✅ |
| **R2c** | Summary aggregate (count/sum) | ✅ POC |
| **R3** | Named query + merkezi kayıt (DG) | 🔲 |
| **R4** | Dashboard köprüsü | 🔲 |

## Major backlog (10 Tem 2026)

1. Dokümantasyon entegrasyonu  
2. Otomatik raporlar  
3. Rapor parametreleri iyileştirmesi  
4. Export geliştirmeleri  
5. Rapor linklemeleri  
6. Dynamic Form seçenekleri  
7. Viewer sayfası  

Detay, oturum notları ve kısa vadeli eksikler: [DEVAM.md](DEVAM.md)

## Odak Eğitim POC

- Rapor: `rpt_odak_egitim_trainings` — dataset `odak_egitimler`
- Parametreler: durum, yıl (`gerceklesenTarih`), arama
- Expand: Genel + **Katılımcılar** (persons label düzeltildi)
- Özet: kartlar (kayıt + süre) · katılımcı footer count
- Designer: sol dikey sekmeler · Tasarım ilk sekme

## İlgili kod

- UI: `Mng.Ui/pages/apps/reporting/` · `components/apps/reporting/`
- Seed: `reportingOdakEgitimSeeds.ts` · `reportingOdakEgitimExpandMigrations.ts`
- Summary: `reportingSummary.ts`
- DG: `FilterParser`, `DatetimeMatchFilterExpander`, `POST …/aggregate`
