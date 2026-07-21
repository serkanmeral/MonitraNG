# Document Intelligence — Roadmap (Faz 3)

**Teklif:** §4.1 Döküman Zekası  
**Klasör:** `docs/monitrang/faz3/document_intelligence/`  
**Başlangıç paketi:** Evet — Faz 3 ana hat buradan açılır  
**Durum:** DI-0 gap gözden geçirildi; ürün olgun + DI-T; kalan iş sıralı  
**Son güncelleme:** 13 Temmuz 2026 (öğleden sonra · DI-0 review)

**Ürün kaynağı (baseline):**  
`docs/odak/document_intelligence/` — özellikle [DI_PRODUCT_ROADMAP.md](../../odak/document_intelligence/DI_PRODUCT_ROADMAP.md), [DEVAM.md](../../odak/document_intelligence/DEVAM.md)

---

## 1. Amaç

Teklif §4.1’i **mevcut DI ürününe** bağlamak: kalan non-AI boşlukları kapatmak, AI tüketimini `ai_platform` omurgasına oturtmak. Sıfırdan klasör/Collabora/generation yazmak değil.

Paralel zorunlu hat: **yetkilendirme odaklı otomatik test omurgası (DI-T)**.

## 2. Kapsam özeti (tekliften)

| Alan | Madde |
|:---|:---|
| Dosya | Klasör, yetki, metadata, etiket, bildirim, AI (RAG, benzer, diff, …) |
| Döküman | DOCX/XLSX/PPTX, edit/export/print, antet/kapak, üretim tetikleri, inject, sürüm |
| AI (döküman) | Çeviri seti, ton, parametre önerisi, özet sunum, checklist |
| Opsiyon | Onaylı yayın (O1), toplu içerik güncelleme (O2) |

**Dışarıda:** Markdown Sayfa (mevcut ürün; bu teklif kalemi değil).

## 3. DI-0 — Teklif §4.1 ↔ ürün (gözden geçirme)

Kaynak: müşteri teklifi §4.1 · [DI_PRODUCT_ROADMAP.md](../../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) §3.

**Sınıflar:** ✅ Var · ⚠️ Cilala / kısmi · 🔲 Yeni · 📦 Opsiyon · — Teklif dışı

### 3.1 Dosyalar (§4.1.1)

| Madde | Sınıf | Not |
|:---|:---:|:---|
| Klasör hiyerarşisi / merkezi barındırma | ✅ | Faz 1, lazy tree |
| Yetki: görme / indirme / ekleme | ✅ | Grup ACL + miras; **otomatik test yok → DI-T** |
| Metadata alanları | ⚠️ | Temel metadata/patch var; kurumsal özel alan seti teklifte netleşebilir |
| Manuel etiketleme | ✅ | `dm_tags` + tag API |
| Bildirim (in-app / e-posta / Telegram) | 🔲 | **D-N** |
| AI: RAG, benzer, diff, tutarsızlık, çok dilli özet, klasör önerisi, varlık, otomatik etiket/özet | 🔲 | **Faz AI** + [ai_platform](../ai_platform/Roadmap.md) |
| Arama / hazır sorgular | ⚠️ | Klasör/arama var; “hazır sorgular” ürün cilası olabilir |

### 3.2 Dökümanlar (§4.1.2)

| Madde | Sınıf | Not |
|:---|:---:|:---|
| DOCX / XLSX / PPTX + Collabora | ✅ | Managed Office O-0→Pr2 |
| Yetki: gör / indir / oluştur / düzenle / export / yazdır | ✅ | ACL modeli; export/yazdır UI+API smoke var, yetki matrisi T-3 |
| Antet kataloğu + üretimde uygulama | ✅ | D-BR1 |
| Kapak sayfası | ⚠️ | D-BR2 kısmi; **BR2-FIX backlog** |
| Manuel oluşturma | ✅ | D-CREATE, D4 |
| Otomatik üretim (tıklama / olay) | ✅ | G0–G5, CoC/Activity/medya |
| Zamanlanmış üretim | 🔲 | **D-S** (teklifte var) |
| Parametreli şablon, belge kodu, PDF, sürüm | ✅ | D1/D4/D2; D-P derinliği erteli (kısmi G2/G5) |
| Mevcut Office alma (inject, makro sınırı) | ⚠️ | Upload/from-reference var; best-effort inject cilası |
| Kullanım izlenebilirliği / süre raporları | ⚠️ / 🔲 | D-E oturum kısmen; süre raporları net değil → DI-2’de sınıfla |
| AI: çeviri seti, ton, parametre önerisi, özet sunum, checklist (+ dosya AI) | 🔲 | AI + MngLLM translate genişletme |
| Markdown Sayfa | — | Teklif dışı (ürün ✅ Faz P) |

### 3.3 Opsiyonlar

| Madde | Sınıf | Not |
|:---|:---:|:---|
| O1 Onaylı yayın | 📦 | D-WF / Faz M |
| O2 Toplu içerik güncelleme | 📦 | Ayrı talep |

### 3.4 Çapraz (Faz 3 zorunlu)

| Madde | Sınıf | Not |
|:---|:---:|:---|
| Yetki otomatik test (DI-T) | 🔲 | T-0…T-5; T-1 = gate |
| Functional smoke düzeni | ⚠️ | Çok smoke var; T-4’te `suites/functional` |

### 3.5 Bilinçli ertelenen / düşük öncelik (teklif dışı veya sonra)

| Madde | Not |
|:---|:---|
| D-P parametre stüdyosu 2.0 (tam) | Erteli; skaler + G2/G5 kısmi yeterli varsayımı |
| G6 WI ↔ belge | Erteli |
| D5 OC tam UI | İsteğe bağlı / üretim paketi ile kesişebilir |
| Sayfa P+ yorum | Teklif dışı |

---

## 4. Yapılacaklar ve geliştirme sırası

### 4.1 Özet sıra (DI paketi)

```text
P0  DI-0 ✅ (bu gözden geçirme)
P0  T-0 → T-1          yetki test omurgası (gate)
P0  ai_platform AI-0…2  paralel (DI AI önkoşulu)
P1  DI-1  D-N           bildirim (in-app, e-posta, Telegram)
P1  T-2                 miras / cache yetki testleri
P1  DI-2a BR2-FIX       kapak cilası (gerekirse)
P1  DI-2b               inject / metadata / arama cilası (DI-0 ⚠️ satırları)
P1  T-3                 generate/export yetki
P1  D-S                 zamanlanmış üretim (teklif maddesi)
P1  DI-3                AI tüketici: extract, tag, özet
P1  T-4                 smoke → functional suites
P1  DI-4                AI+: RAG, benzer, diff, çeviri seti…
P1  T-5                 AI yetki-aware test
P2  DI-2c               kullanım/süre raporları (kapsam netleşince)
📦  DI-O                O1 / O2 (ayrı talep)
```

### 4.2 Ürün fazları (eşleme)

| Faz | Hedef | Bağımlılık |
|:---|:---|:---|
| **DI-0** | Gap tablosu (§3) | ✅ Yapıldı (13 Tem 2026) |
| **DI-1** | **D-N** bildirimler | D-N1 mail omurga; **TG-4** Telegram `document.generated` (Channels); in-app sonra |
| **DI-2** | Cilalar: BR2-FIX, inject, metadata/arama, izlenebilirlik | T-1 yeşil tercih |
| **DI-S** | Zamanlanmış üretim (**D-S**) | Scheduler; D-N ile bildirim |
| **DI-3** | AI tag/özet/extract UI+API | `ai_platform` AI-1/2 |
| **DI-4** | RAG / benzer / diff / çeviri seti | `ai_platform` AI-3/5 |
| **DI-O** | O1 / O2 | Ayrı talep |
| **T-0…T-5** | Test omurgası | §5 |

### 4.3 Paralel hatlar

| Hat | İçerik |
|:---|:---|
| **A — Ürün** | DI-1 → DI-2 → DI-S → DI-3 → DI-4 |
| **A2 — Test** | T-0 → T-1 → T-2 → T-3 → T-4 → T-5 |
| **B — Omurga** | `ai_platform` AI-0…5 (A’nın AI dilimlerinden önce/yanında) |

**Kural:** Yetkiyi etkileyen dilimde **T-1** (gerekirse T-2) yeşil olmadan kapanmış sayılmaz.

---

## 5. Test omurgası — DI-T (zorunlu paralel hat)

**İlke:** DI’de regresyon riskinin en yükseği **yetkilendirme** (klasör ACL, miras, snapshot cache, generate/AI sızıntısı). Tek admin token’lı smoke yeterli değildir.

**Konum:** `scripts/tests/MngDocument/` (proje kuralı)  
**Yöntem:** API-first PowerShell suite; gerçek Keeper persona token’ları; UI E2E seyrek/sonra.

### 5.1 Hedef yapı

```text
scripts/tests/MngDocument/
  auth/                 # persona token helper (Admin, Editor-A, Viewer-B, Outsider, Cross)
  fixtures/             # idempotent klasör ağacı + ACL seed (yalnız local/test)
  suites/
    permissions/        # matris: persona × kaynak × işlem → 200|403
    functional/         # mevcut smoke’ların toplanacağı yer
    ai/                 # yetki-aware RAG/benzer (DI-3/4 sonrası)
  runner.ps1            # hepsini çalıştır; PASS/FAIL özet; exit ≠ 0
```

### 5.2 Persona modeli

| Persona | Amaç |
|:---|:---|
| **Admin** | Geniş DI yetkisi (fixture kurulumu / kontrol) |
| **Editor-A** | Klasör A: gör + ekle + düzenle |
| **Viewer-B** | Klasör B: gör / indir; yazma yok |
| **Outsider** | Hiçbir DI test klasörü yok → her yerde 403 |
| **Cross** | A’da editor, B’de yok (sızıntı / yanlış miras) |

Keeper’da sabit test kullanıcı + grup; her koşuda taze token. Prod’a fixture **yazılmaz**.

**Odak pratik not:** Yeni kullanıcı şart değil — mevcut Odak kullanıcı/grup havuzu kullanılır. `odak_admin` hariç ortak şifre `Sm123!?` ([USERS_AND_AUTH.md](../../deploy/local/USERS_AND_AUTH.md)). T-0’da persona → gerçek username/grup eşlemesi seçilir; fixture ACL bu gruplara bağlanır.

### 5.3 Permission matrisi (çekirdek)

Her satır: `persona × resourceId × action → expectedHttpStatus`

| Aksiyon grubu | Örnekler |
|:---|:---|
| Keşif | `tree`, `list`, `get`, `search` |
| İçerik | `download`, `upload`, `createNative`, `delete` |
| ACL | `getAcl`, `setAcl`, `breakInheritance`, `restoreInheritance` |
| Üretim | `generate`, `exportPdf`, preview session |
| AI (sonra) | `summarize`, `tag`, `similar`, `rag` — **göremediği kaynak cevaba/embedding’e girmez** |

**Kritik senaryolar (T-1/T-2):**

1. Miras: çocuk, parent ACL’ini doğru görür  
2. Break inheritance: çocuk kopar; kardeş etkilenmez  
3. Restore inheritance  
4. Permission snapshot cache: ACL değişince (TTL / invalidate sonrası) yeni yetki yansır  
5. Outsider hiçbir resourceId ile 200 almaz  
6. Viewer yazma / generate denemesi → 403  
7. Cross, B altındaki dosyayı tree/get/download ile göremez  

### 5.4 DI-T fazları

| Faz | Hedef | Öncelik | Not |
|:---|:---|:---:|:---|
| **T-0** | Persona token helper + fixture klasör/ACL seed (idempotent) + runner iskeleti | **P0** | ✅ 13 Tem 2026 — `scripts/tests/MngDocument/` |
| **T-1** | Permission matrisi v1: tree/list/get/download/upload (+ Outsider/Cross) | **P0** | ✅ 13 Tem 2026 — local 31/31 PASS |
| **T-2** | Miras break/restore + cache invalidation senaryoları | P0 | ✅ 13 Tem 2026 — `test-inheritance.ps1` 12/12 PASS |
| **T-3** | Generate / export / preview yetki kapıları | P1 | Functional ile kesişir |
| **T-4** | Mevcut `smoke-*.ps1` → `suites/functional/` + runner’a bağla | P1 | Davranış aynı, düzen |
| **T-5** | AI yetki-aware suite (`suites/ai/`) | P1 | `ai_platform` + DI-3/4 sonrası; K5 kanıtı |

### 5.5 Çalıştırma / kabul

- Varsayılan hedef: **test gateway**; local Docker opsiyonel  
- `runner.ps1` exit code ≠ 0 → kırmızı  
- Yeni DI yetki davranışı dilim kabulü: ilgili matris satır(lar)ı güncellenir  
- CI/cron: sonra (T-4 sonrası aday)

### 5.6 Ürün fazları ile ilişki

```text
DI-0 (gap) ──┬──► DI-1 / DI-2 …
             │
T-0 → T-1 ───┴──► T-2 → T-3 → T-4
                      │
DI-3 / DI-4 ──────────┴──► T-5 (AI yetki)
```

**Kural:** Yetkiyi etkileyen her DI değişikliğinde T-1 (ve gerekirse T-2) yeşil olmadan dilim kapanmış sayılmaz.

---

## 6. Bağımlılıklar

- Mevcut `MngDocument` / `Mng.Ui` / Collabora / Gotenberg  
- AI özellikleri → [../ai_platform/Roadmap.md](../ai_platform/Roadmap.md) (CPU-first)  
- DI-T persona’lar → MngKeeper test kullanıcı/grup (T-0’da netleşir)  
- Migration → [../MIGRATION.md](../MIGRATION.md) (gerekirse test kullanıcı seed notu)  
- Seed/script’ler: `docs/odak/document_intelligence/scripts/`  
- Mevcut smoke: `scripts/tests/MngDocument/smoke-*.ps1`

## 7. Kabul (özet)

- Teklif §4.1 senaryoları baseline + gap kapanmış; makro/ActiveX inject edilemez; AI öneri niteliğinde  
- **T-1 permission matrisi test ortamında yeşil** (DI yetki regresyon kapısı)  
- AI özellikleri açıkken T-5 ile yetki sızıntısı yok kanıtı

---

İş takibi: [work.md](./work.md)
