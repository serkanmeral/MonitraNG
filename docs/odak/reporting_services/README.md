# Reporting Services (Odak)

**Son güncelleme:** 9 Temmuz 2026  
**Ortam:** Odak test (`192.168.20.20`)  
**Durum:** R2 katalog + designer + runner + Odak Eğitim seed ✅ (expand child tabs kısmen — designer Faz 2 bekliyor)

Reporting / raporlama modülü planlama, kararlar ve devam notları.

---

## Dokümantasyon

| Dosya | İçerik |
|-------|--------|
| [README.md](./README.md) | Bu indeks |
| [PLAN.md](./PLAN.md) | Amaç, aktörler, fazlar |
| [DEVAM.md](./DEVAM.md) | **Kaldığımız yer**, smoke test, sıradaki işler |

---

## Özet (şu an)

| Özellik | Durum |
|---------|--------|
| Katalog (localStorage) + kategori | ✅ |
| Designer + Runner | ✅ |
| Parametreler (durum, yıl, arama) | ✅ |
| Expand — alan detayı (`sections`) | ✅ Designer |
| Expand — bağlı liste sekmeleri (`tabs[]`) | ✅ Runtime · 🔲 Designer UI |
| Odak Eğitim listesi seed | ✅ |
| CSV export, DG query preview | ✅ |

---

## Ortam

| Öğe | Değer |
|-----|--------|
| Test sunucu | `192.168.20.20` |
| API Gateway | `http://192.168.20.20:5040` |
| UI | `http://192.168.20.20:3000` |
| Domain | `odak` |

---

## İlgili kod (kök)

- UI: `Mng.Ui/pages/apps/reporting/`, `Mng.Ui/components/apps/reporting/`
- Servisler: `Mng.Ui/services/reportingService.ts`, `reportingCatalogService.ts`
- DG: `GET /api/v1/data/{dataset}` + `POST .../query` (match — şu an Odak eğitim raporu GET kullanıyor)
