# Compliance (ISO 27001 + AS9100) — Devam noktası (checkpoint)

> ## Yeni chat başlangıç prompt'u (kopyala-yapıştır)
>
> ```
> MonitraNG / Compliance (ISO 27001 + AS9100) konusunda çalışıyoruz.
> Repo: c:\Users\monitra\Dev\MonitraNG\MonitraNG
>
> Başlamadan önce şu checkpoint dosyasını oku ve bana kısa bir "kaldığımız yer" özeti ver:
> docs/odak/compliance/DEVAM.md
>
> İlgili dokümanlar:
> - docs/odak/compliance/AS9100_MUSTERI_OZET.md (müşteri bilgilendirme özeti)
> - docs/odak/compliance/COMPLIANCE_ROADMAP.md (fazlı yol haritası)
> - docs/odak/compliance/AS9100_PLAN.md (standart eşleme)
>
> Yanıtlar Türkçe.
> ```

**Son güncelleme:** 9 Haziran 2026  
**Durum:** AS9100 müşteri bilgilendirme özeti + PDF hazır · müşteri ziyareti öncesi · Faz C1 (NCR+CAPA) henüz uygulanmadı

> **⭐ KALDIĞIMIZ YER (9 Haz 2026):** Havacılık müşterisi için AS9100 kapsamında bilgilendirme dokümanı hazırlandı. **Müşteriye verilecek özet:** [AS9100_MUSTERI_OZET.md](./AS9100_MUSTERI_OZET.md) + PDF ([AS9100_MUSTERI_OZET.pdf](./AS9100_MUSTERI_OZET.pdf)). Doküman **teklif/fiyat değil**; üç eksen: **amaç**, **şu an yapabildiklerimiz**, **eklenecekler** (Faz 1/2/3). **Müşteri yüzü:** mevcut araçlara (Excel vb.) atıf yok — **ilk kurulum / dijitalleştirme** çerçevesi. PDF üretimi: `npm run pdf:as9100` ([generate-as9100-pdf.mjs](./generate-as9100-pdf.mjs) + [as9100-pdf.css](./as9100-pdf.css); Edge ile Puppeteer). **Sıradaki:** müşteri ziyareti → geri bildirim → NCR/CAPA alan ve akış kesinleştirme → Operation Core'da Kalite Workspace kurulumu (Faz C1).

**Ana kaynaklar:** [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md) · [AS9100_PLAN.md](./AS9100_PLAN.md) · [README.md](./README.md) · Operation Core: [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md)

---

## Bu oturumda tamamlananlar (9 Haz 2026)

| # | Çıktı | Not |
|---|-------|-----|
| 1 | [AS9100_MUSTERI_OZET.md](./AS9100_MUSTERI_OZET.md) | Müşteri bilgilendirme özeti (amaç · mevcut · eklenecek · NCR/CAPA taslağı) |
| 2 | [AS9100_MUSTERI_OZET.pdf](./AS9100_MUSTERI_OZET.pdf) | Yazdırılabilir PDF (~244 KB, A4) |
| 3 | PDF araçları | `generate-as9100-pdf.mjs`, `as9100-pdf.css`, `package.json` (`npm run pdf:as9100`) |
| 4 | [README.md](./README.md) güncellendi | Müşteri özeti indekse eklendi |
| 5 | Konumlandırma netleşti | Enabler (sertifika vaadi yok); AS9100 önce; NCR+CAPA ilk somut adım |

---

## Verilen kararlar (bu oturum + önceki)

| # | Karar | Kaynak |
|---|-------|--------|
| K1 | Ürün-kolaylaştırıcı; sertifika vaadi yok | COMPLIANCE_ROADMAP §0 |
| K2 | AS9100 önce, ISO 27001 yatay zemin | COMPLIANCE_ROADMAP §0 |
| K4 | İlk somut adım = NCR + CAPA şablonları (Operation Core) | COMPLIANCE_ROADMAP §0 |
| **K7** | Müşteri dokümanı = **bilgilendirme özeti** (tam teklif / fiyat sayfası yok) | 9 Haz 2026 |
| **K8** | Müşteri yüzünde mevcut araçlara atıf yok; **ilk kurulum** dili | 9 Haz 2026 |
| **K9** | İç spec için müşterinin **NCR/CAPA prosedür ve form şablonları** alınacak (ziyaret sonrası) | 9 Haz 2026 |

---

## Sıradaki adımlar (önerilen sıra)

### 1. Müşteri ziyareti (hemen)

- [ ] [AS9100_MUSTERI_OZET.pdf](./AS9100_MUSTERI_OZET.pdf) ile bilgilendirme sunumu
- [ ] Müşterinin kalite prosedürleri / NCR-CAPA form şablonları (varsa)
- [ ] Rol haritası: kalite, üretim, MRB, yönetim
- [ ] Öncelik netleştirme: FAI? tedarikçi? sadece NCR/CAPA?
- [ ] Denetim / sertifikasyon takvimi (varsa)
- [ ] On-prem altyapı beklentisi

### 2. Ziyaret sonrası — tasarım kesinleştirme

- [ ] [AS9100_MUSTERI_OZET.md](./AS9100_MUSTERI_OZET.md) §5 NCR/CAPA taslağını müşteri girdisiyle güncelle
- [ ] [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md) §4 ile senkronize et
- [ ] Gerekirse PDF yeniden üret: `cd docs/odak/compliance && npm run pdf:as9100`

### 3. Faz C1 — Operation Core kurulumu

- [ ] Kalite Workspace oluştur (`op_workspaces`)
- [ ] NCR WorkItem tipi: form + state flow + key prefix `NCR`
- [ ] CAPA WorkItem tipi: form + state flow + key prefix `CAPA`
- [ ] NCR → CAPA parent-child (`op_links`)
- [ ] Rol/yetki tanımı (Keycloak grupları)
- [ ] Kalite dashboard (açık NCR/CAPA widget'ları)
- [ ] Demo kayıtları + smoke test

**Referans implementasyon:** IT Destek workspace → [../operationcore/reference/IT_HELP_DESK_WORKSPACE.md](../operationcore/reference/IT_HELP_DESK_WORKSPACE.md)

---

## Açık sorular

| Soru | Durum |
|------|-------|
| Müşteri ziyareti geri bildirimi | 🔲 Ziyaret sonrası |
| NCR/CAPA prosedür ve form şablonları | 🔲 Ziyarette alınacak |
| FAI ihtiyacı var mı? | 🔲 Müşteriyle netleşecek |
| Tedarikçi yönetimi önceliği | 🔲 Müşteriyle netleşecek |
| Denetim takvimi | 🔲 Müşteriyle netleşecek |
| Risk Register: ayrı modül mü WorkItem tipi mi? | 🔲 C2'de |
| Kanıt export otomatik mi? | 🔲 C2'de |

---

## Dosya envanteri (compliance klasörü)

| Dosya | Rol |
|-------|-----|
| [DEVAM.md](./DEVAM.md) | Bu dosya — checkpoint |
| [README.md](./README.md) | Klasör indeksi, metodoloji |
| [COMPLIANCE_ROADMAP.md](./COMPLIANCE_ROADMAP.md) | Fazlı yol haritası, NCR/CAPA taslağı |
| [AS9100_PLAN.md](./AS9100_PLAN.md) | Detaylı standart eşleme |
| [ISO27001_PLAN.md](./ISO27001_PLAN.md) | ISO 27001 eşleme |
| [AS9100_MUSTERI_OZET.md](./AS9100_MUSTERI_OZET.md) | Müşteri bilgilendirme (kaynak) |
| [AS9100_MUSTERI_OZET.pdf](./AS9100_MUSTERI_OZET.pdf) | Müşteri bilgilendirme (PDF) |
| [generate-as9100-pdf.mjs](./generate-as9100-pdf.mjs) | PDF üretim scripti |
| [as9100-pdf.css](./as9100-pdf.css) | PDF stil dosyası |

---

## PDF yeniden üretim

```powershell
cd docs\odak\compliance
npm install
npm run pdf:as9100
```

Çıktı: `AS9100_MUSTERI_OZET.pdf` (Microsoft Edge ile headless render).
