# Reporting Services — Devam

**Son güncelleme:** 10 Temmuz 2026 (oturum kapanışı)  
**Ortam:** Odak test `192.168.20.20` · lokal `npm run dev`  
**Commit:** `494635f1` — `feat(mng.ui): reporting expand designer, summary aggregate and designer UX`  
**Deploy:** UI (`mngui`) Odak test’e alındı (`sync Mng.Ui` + `--no-cache`) · smoke `gateway=200 ui=200 oc_live=200`

---

## Nerede kaldık

**Generic raporlama (R2+ / R2b / R2c)** çalışır durumda ve test sunucuda canlı.

Bu oturumda: Expand designer Faz 2, özet metrikler (count/sum + DG aggregate), designer UX (sol dikey sekmeler), Katılımcılar persons hücre düzeltmesi, major backlog kaydı. Commit + push + UI deploy tamam.

**Sıradaki (önerilen):** kısa vadeli teknik borçtan biri (Planlanan+yıl / personel raporu smoke / child tab yetkisi) veya major backlog’dan seçim.

---

## Bu oturumda tamamlananlar (10 Tem 2026)

### Expand designer Faz 2

- `ReportingExpandLayoutPanel`: iç sekmeler **Ayarlar** | **Sekmeler**
- `ReportingExpandChildTabsPanel`: sekme listesi; seçili sekmede **Bağlantı** | **Sütunlar** | **Özet**
- Bağlantı: title, id, child dataset, linkField, parentField, emptyMessage, limit
- Sütunlar: `ReportingListColumnsPanel` reuse (child şema)
- Dataset değişince sütunlar yeni şemadan üretilir

### Katılımcılar hücre düzeltmesi

- Kök neden: `personelId` **persons** expand → `firstName`/`lastName`/`username` (displayName yok)
- `reportingListConfig`: persons label + eksik `relationDisplayField` fallback
- Seed/migration: `personelId.displayName` kaldırıldı / temizlendi
- Child tablo satır modeli ana raporla hizalandı (`reportingCellRawForColumn`)

### Özet metrikler (R2c POC)

- Tipler: `ReportingSummaryConfig` / `ReportingSummaryMetric` (`count` | `sum`, placement: cards/footer/both/none)
- Yer: rapor `summary` + child `tabs[].childList.summary`
- Hesaplama: `POST /api/v1/data/{dataset}/aggregate` (`$match` + `$group`); tarih string → Extended JSON `$date`
- UI: `ReportingSummaryCards`, `ReportingSummaryFooter`, `ReportingSummaryDesignerPanel`
- Runner + designer önizleme + child liste bağlı
- Seed: Eğitim listesi → üst kartlar (kayıt sayısı + `sureDakika` toplamı); Katılımcılar → footer count

### Designer UX

- Sol dikey sekmeler (OC workspace tanımları gibi; md+ dikey, dar ekranda yatay)
- **Tasarım** ilk sekme (başlık, açıklama, kategori, dataset) — sol kart kaldırıldı
- **Varsayılan filtreler** sekmesi kaldırıldı (`defaultFilters` modelde kaldı, geriye dönük)

### Dokümantasyon / backlog

- Major başlıklar kaydedildi (aşağıda)
- `PLAN.md` fazları R2b/R2c güncellendi

---

## Mevcut eksikler + major backlog

### Kısa vadeli / teknik borç

1. **Planlanan + yıl** — yıl filtresi şu an `gerceklesenTarih`; Planlanan sekmesinde `planlananTarih` (coupling olmadan) tartış/uygula
2. **DG deploy** — `DatetimeMatchFilterExpander` test sunucuya (POST `/query` / aggregate tarih match kalitesi)
3. **Personel eğitim geçmişi** (`rpt_odak_egitim_person`) smoke test
4. **Summary + search** — metin arama henüz aggregate özetine dahil değil
5. **Sekme sütun yetkisi / sekme görünürlüğü** — child tab `fieldPolicies` / `visibilityPolicies` (konuşuldu, ertelendi)
6. **Side menu / katalog** — production menü hazırlığı

### Major başlıklar (yol haritası)

| # | Başlık | Not |
|---|--------|-----|
| 1 | **Dokümantasyon entegrasyonu** | Rapor / alan yardımı, sözlük, DI veya yardım paneli köprüsü |
| 2 | **Otomatik raporlar** | Zamanlanmış çalıştırma, e-posta / bildirim, abonelik |
| 3 | **Rapor parametreleri iyileştirmesi** | UX, bağımlı alanlar, search’ün özete yansıması, daha zengin binding’ler |
| 4 | **Export geliştirmeleri** | CSV ötesi (Excel, PDF, seçili sütun, özet satırları) |
| 5 | **Rapor linklemeleri** | Raporlar arası / satır → başka rapor / deep link parametreleri |
| 6 | **Dynamic Form seçenekleri** | Form benzeri parametre / filtre UI; OC form desenleri |
| 7 | **Viewer sayfası** | Salt okunur / gömülü / paylaşımlı görüntüleyici (designer’dan ayrı) |

*(PLAN fazları R3 named query / R4 dashboard ile birlikte bu major’lar sıraya alınacak.)*

---

## Nasıl denerim

1. **Lokal:** `Mng.Ui` → `npm run dev` → `/apps/reporting` · designer · runner `rpt_odak_egitim_trainings`
2. **Test sunucu:** http://192.168.20.20:3000/apps/reporting

**Smoke:**

- [ ] Expand → Katılımcılar: personel adları dolu (tire değil)
- [ ] Rapor özet kartları: kayıt sayısı + süre toplamı (filtreli aggregate)
- [ ] Katılımcılar footer: katılımcı sayısı
- [ ] Designer: sol sekmeler · Tasarım / Expand (Ayarlar+Sekmeler) / Özet

---

## Önemli dosyalar

| Alan | Dosya |
|------|--------|
| Tipler | `Mng.Ui/types/apps/reporting.ts` |
| Summary | `Mng.Ui/utils/reportingSummary.ts`, `ReportingSummaryCards/Footer/DesignerPanel.vue` |
| Expand tabs designer | `ReportingExpandLayoutPanel.vue`, `ReportingExpandChildTabsPanel.vue` |
| Child liste | `ReportingChildListPanel.vue` |
| Persons label | `Mng.Ui/utils/reportingListConfig.ts` |
| Seed / migration | `reportingOdakEgitimSeeds.ts`, `reportingOdakEgitimExpandMigrations.ts` |
| Designer | `ReportingDesignerView.vue` |
| Katalog parse | `reportingCatalogStorage.ts` |
| Plan / devam | `docs/odak/reporting_services/PLAN.md`, `DEVAM.md` |

---

## Kurallar

- Test: `192.168.20.20`
- UI deploy: kullanıcı talebi ile (`deploy-odak-apps.ps1 -Services mngui`)
- Backend deploy: DG ayrı
- Commit/push: yalnızca talep edilince
- SSH bilgileri: `docs/odak/operationcore` · şifre oturumda verildiğinde kullanılır
