# Document Intelligence — Managed Office (Sheet & Sunum) Yol Haritası

**Durum:** **O-0 → Pr2 ✅** (8 Temmuz 2026 gece) — S3/Pr3 senaryoya bağlı  
**Tarih:** 8 Temmuz 2026 (kararlar: 8 Tem 2026 · implementasyon: 8 Tem 2026 gece)  
**İlişkili:** [DI_PRODUCT_ROADMAP.md §15–16](./DI_PRODUCT_ROADMAP.md) · [DEVAM.md](./DEVAM.md) · [KURUMSAL_ICERIK_SUNUM.md](./KURUMSAL_ICERIK_SUNUM.md)

> **Strateji:** Sheet (xlsx) ve Sunum (pptx) ayrı ürünler değil; **Döküman (docx) omurgasının** Collabora üzerindeki uzantılarıdır. WOPI / sürüm / kilit / oturum **bir kez genellenir** (`O-0`); Sheet ve Sunum ince katmanlar olarak üstüne eklenir. PPTX’i «sonra düşünürüz» diye xlsx’e özel kısayol **yapılmaz**.

### Kilitli kararlar (8 Tem 2026)

| # | Karar | Seçim |
|---|--------|--------|
| **K-API** | Native office API yüzeyi | **Üç ayrı endpoint** + ortak `CreateNativeOfficeAsync(kind, …)` servisi |
| **K-SIR** | Uygulama sırası | **O-0 → O-1 → S1 → S2 → Pr1 → Pr2** (S3 / Pr3 senaryoya bağlı, sonra) |
| **K-PptxO0** | O-0’da pptx | Profil + `MinimalPptxFactory` **tam minimal paket** (Pr1’de yalnızca endpoint + UI) |
| **K-Menü** | Sunum menüsü (S1 döneminde) | ~~Gizli~~ → **Pr1’de açıldı** ✅ |

**API (K-API) endpoint’leri:**

```text
POST /api/v1/resources/documents/native      → docx (mevcut D-CREATE ile hizalanır / taşınır)
POST /api/v1/resources/sheets/native       → xlsx
POST /api/v1/resources/presentations/native → pptx
```

İçeride tek implementasyon; OpenAPI ve UI net semantik için üç yüzey.

---

## 1. Vizyon — Office üçlüsü

**Amaç:** Kurumsal **Word · Excel · Sunum** içeriğini tek kaynak ağacında, aynı yetki / sürüm / audit kurallarıyla yönetmek.

**Ürün mesajı:** *«Üç ayrı klasör, üç ayrı yetki modeli yok — tek platform, tarayıcıda Collabora.»*

| Tür (UI) | Uzantı | Collabora | Örnek kullanım |
|----------|--------|-----------|----------------|
| **Döküman** | `.docx` | Writer | CoC, aktivite, kalite formu ✅ **canlı** |
| **Sheet** | `.xlsx` | Calc | Fiyat listesi, kapasite, plan |
| **Sunum** | `.pptx` | Impress | Müşteri sunumu, eğitim, brifing |
| **Dosya** | upload (office dahil) | ❌ | Harici binary; editör yok |

---

## 2. UI sınıflandırma (hedef)

```text
Sayfa     → type = markdown
Döküman   → type = file AND origin ∈ { native, manual, system } AND ext = docx
Sheet     → type = file AND origin ∈ { native, manual, system } AND ext = xlsx
Sunum     → type = file AND origin ∈ { native, manual, system } AND ext = pptx
Dosya     → origin = upload  OR  ext ∉ managed-office seti
```

**Managed office seti:** `{ docx, xlsx, pptx }` — tek enum / sabit listesi; UI ve backend aynı kaynaktan okur.

---

## 3. Ortak mimari — `O-0` (önce bu)

Sheet veya Sunum’a başlamadan önce **docx’e özel kod** genellenir. PPTX sonradan «üçüncü satır» eklemekle gelir; WOPI yeniden yazılmaz.

### 3.1 `ManagedOfficeKind` (önerilen sözleşme)

| Kind | Extension | MIME (WOPI GetFile) | Empty factory | Collabora app |
|------|-----------|---------------------|---------------|---------------|
| `document` | `.docx` | `application/vnd...wordprocessingml.document` | `MinimalDocxFactory` ✅ | Writer |
| `sheet` | `.xlsx` | `application/vnd...spreadsheetml.sheet` | `MinimalXlsxFactory` | Calc |
| `presentation` | `.pptx` | `application/vnd...presentationml.presentation` | `MinimalPptxFactory` | Impress |

**Backend yardımcıları (tek yer):**

```text
ManagedOfficeProfiles.cs     → kind → ext, mime, default file name
IManagedOfficeEmptyFactory   → CreateBlank(kind)
ResourceEditorService        → EnsureManagedOfficeFile(resource)  // docx|xlsx|pptx
ResolveOfficeMime(bytes|ext) → WOPI Content-Type
```

### 3.2 WOPI / editör (değişmeyenler)

| Bileşen | Davranış |
|---------|----------|
| `WopiController` | Aynı route; `CheckFileInfo.BaseFileName` uzantısı Collabora’ya app seçtirir |
| `EditorSessionService` | Document key = `resourceId`; limitler ortak (D-E) |
| Sürüm / kilit / changeNote | Kind-agnostic (D2) |
| `DiCollaboraEditor` | Aynı iframe; PostMessage chrome — **app bazlı** menü id’leri ayrı smoke |

### 3.3 Native oluşturma API — **kilitli (K-API)**

Tür başına net endpoint; ortak servis:

```text
POST /api/v1/resources/documents/native
POST /api/v1/resources/sheets/native
POST /api/v1/resources/presentations/native
        │
        └─→ IResourceService.CreateNativeOfficeAsync(ManagedOfficeKind, request)
```

Mevcut `CreateNativeDocument` docx yolu gradual migrate veya thin wrapper.

### 3.4 Mimari diyagram

```text
                    ┌─────────────────────────────────────┐
                    │         dm_resources (file)         │
                    │  origin: native | manual | system   │
                    └──────────────────┬──────────────────┘
           ┌───────────────────────────┼───────────────────────────┐
           ▼                           ▼                           ▼
      .docx (✅)                   .xlsx (S)                    .pptx (Pr)
           │                           │                           │
           └───────────────────────────┴───────────────────────────┘
                                       │
                              O-0 ManagedOffice
                         (Ensure*, MIME, factories)
                                       │
                              ResourceEditorService
                              WopiController · D-E · D2
                                       │
                              Collabora Writer|Calc|Impress
```

---

## 4. Ortak ürün kararları (O-K)

Sheet ve Sunum için **aynı kurallar**; docx’ten sapmalar bilinçli.

| # | Konu | Karar |
|---|------|--------|
| O-K1 | Upload (xlsx/pptx) | **Dosya** — Collabora yok (upload docx ile simetri) |
| O-K2 | Antet / kapak | Yalnızca **Döküman** (docx); sheet/sunumda yok |
| O-K3 | `documentNo` | Docx: mevcut kural; sheet/sunum: **opsiyonel** |
| O-K4 | Eski formatlar | `.xls`, `.ppt` desteklenmez; OOXML only |
| O-K5 | Makro | `.docm` / `.xlsm` / `.pptm` — kapsam dışı |
| O-K6 | Marka adı UI | «Sheet» / «Sunum» — «Excel» / «PowerPoint» kullanılmaz |
| O-K7 | PDF export | Managed office → Gotenberg (docx ✅; xlsx/pptx O-2) |

---

## 5. Mevcut durum (8 Tem 2026 gece)

| Alan | Docx | Sheet | Sunum |
|------|------|-------|-------|
| Native create | ✅ | ✅ | ✅ |
| WOPI edit | ✅ | ✅ | ✅ |
| Upload → Dosya (editör yok) | ✅ | ✅ | ✅ |
| Sürüm UX (D2) | ✅ | ✅ | ✅ |
| export/pdf | ✅ | ✅ | ✅ |
| Şablondan üretim | ✅ D4 | backlog S3 | backlog Pr3 |
| UI create menü | ✅ | ✅ | ✅ |
| `isDi*` sınıfı | ✅ | ✅ | ✅ |

---

## 6. Dilimler — faz haritası

```text
O-0  Managed office çekirdeği (WOPI genelleme)     ← PPTX için kritik; S1 ile birlikte
O-1  Ortak native API sözleşmesi + UI iskeleti
────────────────────────────────────────────────────
S1   Sheet native + Calc                           ← ilk kullanıcı değeri
S2   Sheet sürüm/PDF doğrulama
S3   Şablondan sheet (senaryo sonrası)
────────────────────────────────────────────────────
Pr1  Sunum native + Impress                        ← O-0 sayesinde ince (~3–5 gün)
Pr2  Sunum sürüm/PDF doğrulama
Pr3  Şablondan sunum / kurumsal arşiv senaryoları
────────────────────────────────────────────────────
O-2  Upload office → PDF önizleme (opsiyonel, üç tür)
O-3  AI extract (xlsx/pptx metin) — Faz AI ile örtüşür
```

### O-0 — Managed office çekirdeği — **P0, Sheet’ten önce**

| İş | Not |
|----|-----|
| `ManagedOfficeKind` + profil tablosu | docx + xlsx + **pptx profili tanımlı** (pptx factory S1’de boş da olabilir) |
| `EnsureManagedOfficeFile` | `ResourceEditorService` refactor |
| WOPI MIME + `BaseFileName` | Uzantıdan türet |
| `MinimalXlsxFactory` | Boş xlsx paketi |
| `MinimalPptxFactory` | Boş pptx paketi (**O-0’da iskelet**; Pr1’de smoke) |
| Regresyon | Tüm DOCX smoke’ları geçer |

**Kabul:** Kod yolu pptx eklemek için yalnızca profil + factory + endpoint; WOPI’ye dokunulmaz.

**Tahmini:** 3–5 gün (refactor + xlsx factory; pptx factory minimal)

### O-1 — API + UI iskeleti — **P0**

| İş | Not |
|----|-----|
| `CreateNativeOfficeAsync(kind, …)` | Ortak servis |
| Üç endpoint (veya tek + kind) | Karar O-0’da kilitlenir |
| `isDiDocument` / `isDiSheet` / `isDiPresentation` | Tek `diOfficeKind.ts` |
| Menü: Yeni döküman · sheet · sunum | Sunum menüsü **disabled + «yakında»** olabilir (Pr1 öncesi) |
| Filtreler | Sayfa · Döküman · Sheet · Sunum · Dosya |

### S1 — Sheet (xlsx) — **P0**

O-0 üzerine ince katman:

| İş | Not |
|----|-----|
| `POST …/sheets/native` | `origin=native`, xlsx |
| UI `DiCreateSheetDialog` | D-CREATE sheet analoğu |
| Collabora Calc smoke | PostMessage chrome (Calc menü id’leri) |
| Upload xlsx | Dosya; editör kapalı mesajı |

**Kabul:** Klasör → Yeni sheet → Calc → kaydet → sürüm.

**Tahmini:** 4–6 gün (O-0 sonrası)

### S2 — Sheet sürüm + PDF — **P0**

| İş | Not |
|----|-----|
| D2 UX sheet üzerinde doğrulama | changeNote, kapat-kaydet |
| `export/pdf` xlsx | Gotenberg |
| Smoke | `smoke-sheet-native-test.ps1` |

**Tahmini:** 3–5 gün

### S3 — Şablondan sheet — **P1**

| Seçenek | Karmaşıklık |
|---------|---------------|
| Hücrede `{{key}}` metin | Orta |
| Named range | Yüksek |
| Dataset → satır doldurma | Orta |

Senaryo netleşene kadar ertelenir (fiyat listesi / kapasite planı).

### Pr1 — Sunum (pptx) — **P1** (O-0 + S1 pattern sonrası)

**Neden hızlı:** O-0’da pptx profili + `MinimalPptxFactory` zaten var; Pr1 ≈ S1 kopyası.

| İş | Not |
|----|-----|
| `POST …/presentations/native` | `origin=native`, pptx |
| UI `DiCreatePresentationDialog` | |
| Collabora Impress smoke | PostMessage chrome |
| Upload pptx | Dosya |

**Tahmini:** 3–5 gün (O-0 doğru yapıldıysa)

### Pr2 — Sunum sürüm + PDF — **P1**

S2 ile simetrik; çoğu kod kind-agnostic.

**Tahmini:** 2–3 gün

### Pr3 — Şablondan sunum — **P2**

| Senaryo | Not |
|---------|-----|
| Kurumsal şablon slayt + `{{title}}` | D4 analoğu |
| Müşteri logosu slayt master | Letterhead benzeri ama Impress master — ayrı tasarım |
| OC / eğitim paketi üretimi | Sistem kanalı |

Belge Tasarımcısı pptx desteği **Pr3-Designer** — docx designer’dan bağımsız değerlendirilir.

### O-2 — Upload office PDF önizleme (opsiyonel)

D-FILE-PREV genişletmesi: upload docx/xlsx/pptx → Gotenberg PDF. Managed türler zaten Collabora kullanır.

---

## 7. Sunum’a özel notlar (Pr)

| Konu | Plan |
|------|------|
| Slayt boyutu | Varsayılan 16:9; şablon master’da |
| Gömülü medya | DG file storage; büyük pptx sürüm boyutu — mevcut dosya limitleri |
| Animasyon / geçiş | Collabora Impress destekler; test smoke’ta basit slayt yeterli |
| PDF export | Sunum → PDF (tek slayt veya tüm deck); Gotenberg |
| Antet slaytı | Pr3’te «kurumsal kapak slaytı» — D-BR2 kapak DOCX’ten **ayrı** kavram |

---

## 8. UI bilgi mimarisi

```text
Document Intelligence
├── Dökümanlar/     (docx — mevcut)
├── Sheets/         (xlsx)
│   ├── Fiyat Listeleri/
│   └── Planlama/
├── Sunumlar/       (pptx — Pr1 sonrası)
│   ├── Müşteri/
│   └── Eğitim/
└── Dosyalar        (upload)
```

**Oluştur menüsü:**

- Yeni döküman (docx)
- Yeni sheet (xlsx) — S1
- Yeni sunum (pptx) — Pr1
- Yükle → Dosya

**Keşif kısayolları (opsiyonel):** Sheets · Sunumlar alan banner’ları (Faz P alan girişi deseni).

---

## 9. Zaman çizelgesi (özet)

| Faz | Süre | Bağımlılık |
|-----|------|------------|
| **O-0** | 3–5 gün | Döküman WOPI ✅ |
| **O-1** | 2–3 gün | O-0 |
| **S1+S2** | ~1.5 hf | O-0, O-1 |
| **Pr1+Pr2** | ~1 hf | O-0, S1 pattern kanıtı |
| S3, Pr3 | senaryo | ayrı |

**Toplam (Sheet + Sunum çekirdek, O-0 dahil):** ~4–5 hafta  
**Sadece Sheet (O-0+S1+S2):** ~2.5 hafta — Sunum O-0’da hazırlanır, Pr1 sonra ince eklenir.

```text
Hafta 1    O-0 + O-1 (genelleme + pptx iskelet)
Hafta 2    S1 Sheet Calc
Hafta 3    S2 Sheet PDF/sürüm + regresyon
Hafta 4    Pr1+Pr2 Sunum Impress  ← pptx «bedava» gelir
Hafta 5+   S3 / Pr3 senaryolar (ihtiyaç halinde)
```

---

## 10. Başarı kriterleri

### O-0

- [x] DOCX smoke regresyonu yeşil
- [x] `ManagedOfficeKind` üç türü tanımlar
- [x] WOPI docx/xlsx/pptx MIME doğru

### S1+S2

- [x] Native sheet → Calc → sürüm
- [x] Upload xlsx editör yok
- [x] Sheet export/pdf

### Pr1+Pr2

- [x] Native sunum → Impress → sürüm
- [x] Upload pptx editör yok
- [x] Sunum export/pdf

**Smoke scriptleri:**

- `smoke-sheet-native-test.ps1` (6/6)
- `smoke-presentation-native-test.ps1` (4/4)
- `smoke-presentation-pr2-test.ps1` (8/8)

---

## 11. Bilinçli kapsam dışı (O/S/Pr çekirdek)

- Antet/kapak sheet veya sunuma bindirme (docx + D-BR2 ayrı)
- VBA / makro dosyaları
- Gerçek zamanlı co-authoring (Collabora özelliği; D-E exclusive edit yeterli)
- Microsoft 365 / Graph senkron
- Video ağır sunum arşivi (streaming CDN — ayrı proje)
- Şablondan üretim (S3/Pr3) — ilk dilimlerde yok; **Odak G5** ile XLSX sevkiyat listesi generation hattı ayrıca ✅ (8 Tem 2026)

---

## 12. Açık sorular

| # | Konu | Durum |
|---|------|--------|
| 1 | O-0 pptx factory | **Kilitli (K-PptxO0):** tam minimal paket |
| 2 | API yüzeyi | **Kilitli (K-API):** üç endpoint |
| 3 | Sunum menüsü S1 | **Kapatıldı:** Pr1’de açıldı ✅ |
| 4 | Pr3 senaryo (müşteri vs eğitim) | Açık — Pr1+Pr2 sonrası |
| 5 | Uygulama sırası | **Kilitli (K-SIR):** O-0 → S1 → S2 → Pr1 → Pr2 |

---

## 13. Sonraki adım

1. ~~Kararları kilitle~~ ✅ (8 Tem 2026)
2. ~~**O-0 → Pr2 implementasyonu**~~ ✅ (8 Tem 2026 gece)
3. ~~**G5 — XLSX sevkiyat listesi (Odak generation)**~~ ✅ (8 Tem 2026 sabah) — [DI_PRODUCT_ROADMAP §26](./DI_PRODUCT_ROADMAP.md)
4. **G5+** — iş paketi writeback; prod deploy
5. **S3 / Pr3** — şablondan sheet/sunum (senaryo netleşince)
6. **D-BR2** / **CoC smoke** / **D-N1** ile paralel gidebilir

**Durum:** Managed Office + Odak G5 tamam — test `mngdocument` + `mngui` deploy ✅ (8 Tem sabah).
