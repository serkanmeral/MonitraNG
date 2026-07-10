# Reporting Services — Devam

**Son güncelleme:** 10 Temmuz 2026  
**Ortam:** Odak test `192.168.20.20` · lokal `npm run dev`  
**Deploy:** UI deploy kullanıcı talebi ile

---

## Nerede kaldık

**Generic raporlama (R2+)** çalışıyor. Bu oturumda Expand designer Faz 2, özet metrikler (count/sum + aggregate), designer UX (sol dikey sekmeler, Tasarım ilk sekme) ve Katılımcılar hücre düzeltmesi (persons label) tamamlandı.

Odak Eğitim **Eğitim listesi** + expand **Katılımcılar** POC; özet kartlar (rapor) / footer (katılımcılar) seed ile geliyor.

---

## Bu oturumda tamamlananlar (10 Tem 2026)

- Expand designer: Ayarlar / Sekmeler; sekme Bağlantı + Sütunlar + Özet
- Persons hücre: `displayName` yerine firstName/lastName/username
- Summary: `ReportingSummaryConfig` (count/sum, cards/footer/both) · DG `POST …/aggregate`
- Designer: sol dikey sekmeler (OC workspace tanımları gibi); Tasarım ilk sekme; Varsayılan filtreler sekmesi kaldırıldı
- Seed/migration: eğitim listesi özet kartları; katılımcı footer count

---

## Mevcut eksikler + major backlog

### Kısa vadeli / teknik borç

1. **Planlanan + yıl** — yıl filtresi şu an `gerceklesenTarih`; Planlanan sekmesinde `planlananTarih` (coupling olmadan) tartış/uygula
2. **DG deploy** — `DatetimeMatchFilterExpander` test sunucuya (POST `/query` / aggregate tarih match)
3. **Personel eğitim geçmişi** (`rpt_odak_egitim_person`) smoke test
4. **Summary + search** — metin arama henüz aggregate özetine dahil değil
5. **Sekme sütun yetkisi / sekme görünürlüğü** — child tab `fieldPolicies` / `visibilityPolicies` (konuşuldu, ertelendi)
6. **Side menu / katalog** — production menü hazırlığı
7. **UI deploy** — bu oturum değişiklikleri test sunucuya (talep ile)

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

*(Mevcut PLAN fazları R3 named query / R4 dashboard ile birlikte bu major’lar sıraya alınacak.)*

---

## Nasıl denerim

1. **Lokal:** `Mng.Ui` → `npm run dev` → `/apps/reporting` · designer · runner `rpt_odak_egitim_trainings`
2. **Test sunucu:** `http://192.168.20.20:3000` (mngui deploy sonrası)

**Smoke:**

- [ ] Expand → Katılımcılar: personel adları dolu
- [ ] Rapor özet kartları: kayıt sayısı + süre toplamı (filtreli aggregate)
- [ ] Katılımcılar footer: katılımcı sayısı
- [ ] Designer: sol sekmeler · Tasarım / Expand / Özet

---

## Önemli dosyalar

| Alan | Dosya |
|------|--------|
| Tipler | `Mng.Ui/types/apps/reporting.ts` |
| Summary | `Mng.Ui/utils/reportingSummary.ts`, `ReportingSummary*.vue` |
| Expand tabs designer | `ReportingExpandLayoutPanel.vue`, `ReportingExpandChildTabsPanel.vue` |
| Child liste | `ReportingChildListPanel.vue` |
| Seed / migration | `reportingOdakEgitimSeeds.ts`, `reportingOdakEgitimExpandMigrations.ts` |
| Designer | `ReportingDesignerView.vue` |

---

## Kurallar

- Test: `192.168.20.20`
- UI deploy: kullanıcı talebi ile
- Backend deploy: DG ayrı
- Commit/push: yalnızca talep edilince
