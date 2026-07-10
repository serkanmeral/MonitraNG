# Reporting Services — Devam

**Son güncelleme:** 10 Temmuz 2026 (oturum kapanışı)  
**Ortam:** Odak test `192.168.20.20` · lokal `npm run dev`  
**Commit:** `f096ae36` — `feat(mng.ui,mngdatagateway): reporting year OR, summary search, child tab auth`  
**Deploy:** DG (`mngdatagateway`) test’te · UI (`mngui`) bu kapanışta deploy

---

## Nerede kaldık

**Generic raporlama (R2+ / R2b / R2c)** + kısa vadeli teknik borçlar tamam; Odak test’te DG güncel.

Bu oturumda: Planlanan+yıl (`orDateFields`), DG tarih match deploy, summary+search, child tab yetkisi, side menu «Rapor kataloğu», debounce + `$contains`→`$regex` düzeltmeleri.

**Sıradaki (önerilen):** major backlog’dan seçim (dokümantasyon entegrasyonu, otomatik raporlar, parametreler, export, linkleme, Dynamic Form, Viewer).

---

## Bu oturumda tamamlananlar (10 Tem 2026 — kısa vadeli)

### Planlanan + yıl (`orDateFields`)

- Yıl parametresi: `(gerceklesenTarih ∈ yıl) OR (planlananTarih ∈ yıl)` — parametre coupling yok
- Runtime: `yearOrDateRange` → `mongoMatch` (`POST /query`) + özet aynı match
- Seed/migration: `orDateFields` artık silinmiyor; Eğitim listesine yazılıyor

### DG tarih match

- `DatetimeMatchFilterExpander` test sunucuya deploy (`--no-cache`)
- `$gte`/`$lte` gün sonu düzeltmesi (`$lte` → gün sonu)
- Liste `POST /query`: string tarih (expander); aggregate: Extended JSON `$date` (UI coerce)

### Summary + search

- Metin arama özet aggregate’ine yansır (şema `text` alanlarında `$regex`)
- Relation araması özette yok (liste DG `?search=` ile relation da tarar — küçük sayı farkı olabilir)

### Child tab yetkisi

- `tabs[].visibilityPolicies` — sekme görünürlüğü
- `tabs[].fieldPolicies` — child sütun yetkisi
- Designer: Expand → Sekmeler → **Yetki**
- Runtime: gizli sekme filtresi + child listede sütun gizleme

### Side menu

- `@side_menu`: **Raporlama** → **Rapor kataloğu** (`/apps/reporting`)
- Script: `docs/odak/reporting_services/scripts/patch-reporting-side-menu.ps1`

### Düzeltmeler

- Gelişmiş filtre debounce (~450 ms)
- `contains` / `startsWith` / `endsWith` → `$regex` (yıl aktifken POST match yolu)

### Smoke (doğrulandı)

- Tamamlanan + 2017: liste + kartlar uyumlu
- Başlık içerir + yıl: 500 yok; debounce çalışıyor
- Personel raporu / child yetki / menü: kullanıcı onayı

---

## Mevcut eksikler + major backlog

### Kısa vadeli / teknik borç

*(Bu turda kapatıldı.)*

Kalan ince fark: özet araması relation alanlarını kapsamaz (major / parametre iyileştirmesi kapsamında ele alınabilir).

### Major başlıklar (yol haritası)

| # | Başlık | Not |
|---|--------|-----|
| 1 | **Dokümantasyon entegrasyonu** | Rapor / alan yardımı, sözlük, DI veya yardım paneli köprüsü |
| 2 | **Otomatik raporlar** | Zamanlanmış çalıştırma, e-posta / bildirim, abonelik |
| 3 | **Rapor parametreleri iyileştirmesi** | UX, bağımlı alanlar, relation search’ün özete yansıması, daha zengin binding’ler |
| 4 | **Export geliştirmeleri** | CSV ötesi (Excel, PDF, seçili sütun, özet satırları) |
| 5 | **Rapor linklemeleri** | Raporlar arası / satır → başka rapor / deep link parametreleri |
| 6 | **Dynamic Form seçenekleri** | Form benzeri parametre / filtre UI; OC form desenleri |
| 7 | **Viewer sayfası** | Salt okunur / gömülü / paylaşımlı görüntüleyici (designer’dan ayrı) |

*(PLAN fazları R3 named query / R4 dashboard ile birlikte bu major’lar sıraya alınacak.)*

---

## Nasıl denerim

1. **Lokal:** `Mng.Ui` → `npm run dev` → `/apps/reporting`
2. **Test sunucu:** http://192.168.20.20:3000/apps/reporting (UI deploy sonrası)
3. Menü: **Raporlama** → **Rapor kataloğu**

**Smoke:**

- [ ] Planlanan + yıl → `planlananTarih` ile kayıtlar
- [ ] Tamamlanan + yıl → liste + özet kartları uyumlu
- [ ] Gelişmiş filtre «içerir» + yıl → hata yok; yazarken debounce
- [ ] Arama → özet kartları da daralsın
- [ ] Expand → Katılımcılar Yetki (sekme/sütun)
- [ ] Sol menü: Rapor kataloğu

---

## Önemli dosyalar

| Alan | Dosya |
|------|--------|
| Tipler | `Mng.Ui/types/apps/reporting.ts` |
| Yıl OR / match | `reportingParameterModel.ts`, `reportingMongoMatch.ts`, `reportingParameters.ts` |
| Summary + search | `reportingSummary.ts` |
| Child tab yetki | `ReportingExpandPanel.vue`, `ReportingChildListPanel.vue`, `ReportingExpandChildTabsPanel.vue`, `reportingReportAccess.ts` |
| Seed / migration | `reportingOdakEgitimSeeds.ts`, `reportingOdakEgitimExpandMigrations.ts` |
| DG tarih | `DatetimeMatchFilterExpander.cs` |
| Side menu | `docs/odak/reporting_services/scripts/patch-reporting-side-menu.ps1` |
| Plan / devam | `PLAN.md`, `DEVAM.md` |

---

## Kurallar

- Test: `192.168.20.20`
- UI deploy: kullanıcı talebi ile (`deploy-odak-apps.ps1 -Services mngui`)
- Backend deploy: DG ayrı (bu oturumda yapıldı)
- Commit/push: yalnızca talep edilince
- SSH: `docs/odak/operationcore` · şifre oturumda verildiğinde kullanılır
