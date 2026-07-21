# Döküman Zekası — Modül özellik envanteri

**Kod:** `document-intelligence` · **Durum:** Canlı (genişletme devam ediyor)  
**UI:** `/apps/document-intelligence` · **Belge Tasarımcısı:** `/apps/document-intelligence/designer`

**Referanslar:** [DI Ürün Yol Haritası](../../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) · [Referans teklif §4.1 (iç)](../../odak/commercial/Odak_Kompozit_Fiyat_Teklifi.md)

> **Bu dosyanın amacı (şu an):** Modülün **tüm özelliklerini** ve **Sayfa / Dosya / Döküman ayrımını** netleştirmek; geliştirme durumu (✅ 🔶 🔲 ⏸️ 📋) ile roadmap kaynağı olmak. Broşür metinleri **henüz doldurulmayacak** — bkz. [§Broşür (ertelendi)](#broşür-ertelendi).

**Durum kodları:** ✅ Canlı · 🔶 Kısmi · 🔲 Planlandı · ⏸️ Ertelendi · 📋 Teklifte tanımlı, geliştirilmedi

---

## 1. Modül kapsamı

Döküman Zekası (DI), kurumsal içeriği tek **kaynak ağacında** yönetir. Üç **içerik bandı** vardır; broşür/teklif dilinde “Dosya” ve “Döküman” ayrı paketler, **Sayfa** platform özelliğidir (tipik teklif paketlerinde ayrı fiyat kalemi değil).

| Band | Kısa tanım |
|------|------------|
| **Sayfa** | Markdown bilgi içeriği — rehber, runbook, prosedür; isteğe bağlı **dış web yayını** (headless MD) |
| **Dosya** | Dışarıdan yüklenen binary — arşiv, PDF, harici DOCX… |
| **Döküman** | Platformda yaşayan Office içeriği — DOCX, XLSX, PPTX |

**Döküman alt türleri** (ayrı band değil, aynı kurallar): **Belge** (DOCX) · **Sheet** (XLSX) · **Sunum** (PPTX).

**Çapraz bileşenler:** Klasör · yetki · sürüm · arama · **Belge Tasarımcısı** (şablon/üretim) · **antet kataloğu** · **yapay zekâ** (§7) · **yedekleme** (§12) · entegrasyonlar (OC, Raporlama, Notifier, Scheduler, Workflow).

---

## 2. Sayfa · Dosya · Döküman — ayrım (temel referans)

### 2.1 Karar özeti

| | **Sayfa** | **Dosya** | **Döküman** |
|---|-----------|-----------|-------------|
| **Ne?** | Platformda yazılan bilgi metni | Kullanıcının yüklediği dosya | Platformda oluşturulan / üretilen Office kaydı |
| **Teknik tip** | `type=markdown` | `type=file` + `origin=upload` (veya collabora-dışı uzantı) | `type=file` + `origin ∈ {native, manual, system}` + `.docx/.xlsx/.pptx` |
| **Editör** | Markdown (split önizleme) | Yok — önizleme / indir | Collabora (WOPI) |
| **Yaşam döngüsü** | Taslak → yayın ✅ | Sürüm (yükleme) | Sürüm ✅; taslak/yayın docx 🔲 (Faz M) |
| **Antet / belge kodu** | ❌ | ❌ | ✅ |
| **Şablondan otomatik üretim** | ❌ | ❌ | ✅ |
| **Tipik kullanım** | IT runbook, prosedür, SSS, duyuru; dış web beslemesi | Ek, PDF arşiv, eski Word kopyası | CoC, aktivite formu, fiyat listesi, müşteri sunumu |
| **Dış web yayını** | ✅ planlanan (headless MD) | ❌ | ❌ (müşteri sitesi için Sayfa bandı) |
| **Referans paket** | Platform; ayrı kalem yok | Dosyalar | Dökümanlar |

### 2.2 `origin` — Dosya vs Döküman ayrımı

İkisi de teknik olarak `type=file` olabilir (ör. ikisi de `.docx`). UI sınıfı **`origin`** + uzantı ile belirlenir:

| `origin` | Anlam | UI bandı |
|----------|--------|----------|
| **`upload`** | Kullanıcı yüklemesi | **Dosya** |
| **`native`** | Klasörde «Yeni döküman/sheet/sunum» ile oluşturulan | **Döküman** |
| **`manual`** | Belge Tasarımcısı şablonundan parametre ile üretilen | **Döküman** |
| **`system`** | Olay / API / zamanlama ile otomatik üretilen | **Döküman** |

**UI türetme kuralı:**

```text
Sayfa    → type = markdown

Döküman  → type = file
           AND origin ∈ { native, manual, system }
           AND uzantı ∈ { docx, xlsx, pptx }

Dosya    → type = file
           AND ( origin = upload
                 OR uzantı ∉ { docx, xlsx, pptx } )
```

### 2.3 Davranış farkları (aynı uzantı olsa bile)

| Davranış | Sayfa | Dosya | Döküman |
|----------|-------|-------|---------|
| Collabora ile düzenle | ❌ | ❌ | ✅ |
| Markdown editör | ✅ | ❌ | ❌ |
| Önizleme | Render edilmiş MD | Gotenberg→PDF (docx dahil), görsel/PDF/metin | Collabora veya PDF |
| `letterheadId` (antet) | ❌ | ❌ | ✅ |
| `documentNo` (belge kodu) | ❌ | ❌ | ✅ |
| Belge Tasarımcısı hedefi | ❌ | ❌ | ✅ |
| Taslak / yayın | ✅ | ❌ | 🔲 (yalnızca Sayfa’da; docx Faz M) |

### 2.4 Hangi banda ne konur?

| Senaryo | Band |
|---------|------|
| «Sunucu restart prosedürü» metni | **Sayfa** |
| Müşteriden gelen imzalı PDF | **Dosya** |
| Eski Word şablonunun yükleme ile alınması (henüz inject edilmemiş) | **Dosya** |
| Klasörde yeni boş Word + kurumsal antet | **Döküman** |
| Sipariş kaleminden CoC üretimi | **Döküman** (`system`) |
| Kullanıcının şablondan doldurduğu kalite formu | **Döküman** (`manual`) |
| Merkezi fiyat listesi XLSX (native, Collabora) | **Döküman** — Sheet |
| Executive müşteri brifingi PPTX | **Döküman** — Sunum |

### 2.5 Kök ağaç convention (önerilen)

```text
(kök)
├── Sayfalar/          → Sayfa bandı
├── Dökümanlar/        → Döküman (DOCX) + üretilen çıktılar
├── Sheets/            → Döküman (XLSX)
├── Sunumlar/          → Döküman (PPTX)
└── …                  → Dosya yüklemeleri (Klasör altında, band Dosya)
```

Convention zorunlu değil; tür filtresi + `origin` yeterli. UI sol panelde alan kısayolları kullanılabilir.

---

## 3. Ortak platform özellikleri

Tüm bandlar için geçerli altyapı.

| Özellik | Durum | Not |
|---------|-------|-----|
| Kaynak ağacı (`dm_resources`) | ✅ | Klasör, lazy tree, pagination |
| Klasör arama | ✅ | **D-PERF-1/3** |
| Grup bazlı klasör yetkisi + miras | ✅ | `dm_resource_permissions` |
| Yetki snapshot cache | ✅ | **D-PERF-2** |
| Sürüm geçmişi (`dm_resource_versions`) | ✅ | Band bazında UI farklı |
| Etiket kataloğu (`dm_tags`) | ✅ | API + Sayfa UI |
| Genel arama | 🔶 | Sayfa keşif ✅; Dosya zengin arama AI ile 📋 |
| Tür filtreleri (Sayfa / Döküman / Dosya) | ✅ | Bölümlü liste |
| Toplu seçim / silme | ✅ | |
| OC iş kaydına bağlama / deep link | ✅ | Kalem belgeleri paneli |
| Belge Tasarımcısı (ayrı UI) | ✅ | Şablon, parametre, üretim — bkz. §6 |
| System / Öğreticiler seed | ✅ | |

### 3.1 Etiketleme — iki katman (Dosya · Döküman · isteğe bağlı Sayfa)

**Karıştırılmaması gereken iki tür etiket:**

| Katman | Ad | Ne etiketlenir? | Kim / nasıl? | Örnek |
|--------|-----|-----------------|--------------|-------|
| **1 — İçerik etiketi** | Content tag | Dosyanın / dökümanın **içeriği** (konu, varlık, anlam) | **AI** (öncelik) veya kullanıcı; `dm_tags` | `kalite-raporu`, `coc`, `fatura`, `sipariş-12345` |
| **2 — Meta etiket** | Meta tag / sınıflandırma | **Kaynağın kendisi** (governance, gizlilik, yayın sınırı) | **Kullanıcı** veya politika (klasör mirası, zorunlu alan); AI değil | `public`, `internal`, `confidential`, `restricted` |

**Meta etiket amacı:** «Bu kayıt **kimin görebileceği / dışarı çıkabileceği** / hangi kurallara tabi olduğu» — içeriğin konusu değil. Referans teklif paketindeki **gizlilik düzeyi** bu katmana girer.

**İçerik etiketi amacı:** «Bu kayıt **ne hakkında**» — arama, keşif, benzer dosya, **F-FILE-TRIGGER / D-DOC-TRIGGER** (içerik kuralları).

**Meta etiket — tipik kullanım (Dosya + Döküman):**

| Meta değer | Anlam (örnek skala) |
|------------|---------------------|
| `public` | Kurum dışına çıkabilir / geniş kitle |
| `internal` | Yalnızca kurum içi |
| `confidential` | Kısıtlı dağıtım, export uyarısı |
| `restricted` | En yüksek kısıt; ek onay |

*(Tam sözlük tenant / sektör bazlı tanımlanır — savunma, banka, KVKK.)*

**Davranış hedefleri (ürün kararı — 🔲):**

- Meta etiket → **yetki / export / web yayın** kısıtı (Sayfa P-WEB: yalnızca `public` + webPublish)
- Meta + içerik etiketi birlikte **kural motorunda** (§5.7, §6.11): örn. `confidential` + AI etiket `sözleşme` → Workflow
- Klasör **varsayılan meta** mirası (ör. «Gizli» klasörü → `confidential`)
- AI **meta etiket önermez** (varsayılan); istisna: yalnızca «içerik analizi → gizlilik önerisi» onay kartı

**Teknik not (hedef):** Meta alanları `dm_resources` üzerinde yapılandırılmış alan (`classification`, `visibilityScope`) ve/veya ayrı **meta etiket kataloğu** (`dm_meta_tags` / tag `kind=meta`) — implementasyon kararı açık.

| Özellik | Durum | Not |
|---------|-------|-----|
| İçerik etiketi — manuel (`dm_tags`) | ✅ | Sayfa + Dosya/Döküman |
| İçerik etiketi — AI | 📋 | **Faz AI** |
| Meta etiket — tanım kataloğu | 🔲 | **D-META-TAG** |
| Meta etiket — kaynak atama UI | 🔲 | Yükleme / oluşturma / toplu güncelleme |
| Meta etiket — klasör varsayılanı | 🔲 | |
| Meta → yetki / export / P-WEB kapısı | 🔲 | Politika motoru |
| Meta + içerik → birleşik kural (D-TRIGGER) | 🔲 | §5.7 · §6.11 |

---

## 4. Sayfa — özellik envanteri

**Teknik:** `type=markdown` · **Editör:** Markdown only (WYSIWYG bilinçli yok)

**Ürün kimliği:** Sayfa öncelikle **kurumsal wiki hafızasıdır** (iç ekip, taslak/yayın). **Web yayını (headless)**, aynı bandın **dışa açık koludur** — müşterinin kendi web sitesine SSS, duyuru vb. MD beslemesi. Döküman üretiminin parçası değildir; otomatik sayfa üretimi ile karıştırılmaz.

### 4.1 Oluşturma ve düzenleme

| Özellik | Durum | Not |
|---------|-------|-----|
| Yeni sayfa | ✅ | Oluştur menüsü |
| Markdown editör — split view | ✅ | **Faz P** |
| Tam ekran edit | ✅ | |
| Başlık hiyerarşisi, tablo, kod, checklist | ✅ | |
| Görsel yükleme / embed | ✅ | |
| İç link picker (kaynak ağacı) | 🔶 | Kısmen |
| Sayfa şablonları (Runbook, Kurulum, Sürüm notu) | ✅ | |
| WYSIWYG / blok editör | ⏸️ | Bilinçli kaldırıldı |

### 4.2 Yaşam döngüsü ve sürüm

| Özellik | Durum | Not |
|---------|-------|-----|
| Taslak / yayın | ✅ | **Yalnızca Sayfa bandında** (docx lifecycle Faz M) |
| Sürüm geçmişi | ✅ | |
| Sürüm notu (`changeNote`) | ✅ | |
| Geri alma / önceki sürüme dönüş | ✅ | |

### 4.3 Keşif ve bağlantılar

| Özellik | Durum | Not |
|---------|-------|-----|
| Ana ekran: arama | ✅ | Yayınlanmış varsayılan; taslaklar ayrı |
| Son güncellenenler | ✅ | |
| Taslaklarım | ✅ | |
| Backlink («bu sayfaya link verenler») | ✅ | |
| Alan giriş sayfası (üst klasör index) | ✅ | Convention |
| Etiket atama + filtre | ✅ | |

### 4.4 Web yayını (headless) — dış site beslemesi

Müşterinin **kendi web sitesi** (kurumsal site, portal, landing) SSS, duyuru, yardım metni gibi içerikleri MonitraNG’de hazırlanan **yayınlanmış Sayfa** kaynaklarından alsın. Çıktı: **ham markdown** (+ isteğe bağlı metadata); render müşterinin stack’inde (Next.js, Hugo, WordPress, vb.).

**Dış Katılım Portalı (Anket modülü) ile fark:**

| | Dış Katılım (Anket) | Sayfa web yayını |
|---|---------------------|------------------|
| Barındırma | MonitraNG bulutu (örn. `{tenant}.monitrang.com`) | Müşterinin **kendi** sitesi |
| Amaç | Anket davet / yanıt | Statik içerik syndication |
| DI rolü | Sonuç senkronu | **İçerik kaynağı** (MD) |

#### 4.4.1 Yayın kuralları (ürün kararı)

Public API’ye yalnızca **üç koşul** birlikte sağlanan sayfalar çıkar:

1. `type = markdown`
2. `status = published` (taslak asla)
3. **`webPublish = true`** (veya eşdeğer bayrak / «Web Yayın» klasörü convention)

İç runbook ve operasyon rehberleri varsayılan olarak **webPublish kapalı** kalır. Klasör convention önerisi:

```text
Sayfalar/
├── IT Runbook'ları/          → webPublish: false
├── Operasyon Rehberleri/     → webPublish: false
└── Web Yayın/                → webPublish: true (veya klasör mirası)
    ├── SSS/
    └── Duyurular/
```

#### 4.4.2 Özellik envanteri

| Özellik | Durum | Not |
|---------|-------|-----|
| `webPublish` bayrağı (veya klasör mirası) | 🔲 | **Faz P-WEB** |
| Public read API — sayfa listesi | 🔲 | slug, title, lang, updatedAt |
| Public read API — tekil sayfa (markdown gövde) | 🔲 | Yalnızca published + webPublish |
| Slug (`webSlug`) — URL-dostu anahtar | 🔲 | Benzersiz / domain içi |
| Locale / dil (`lang`: tr, en…) | 🔲 | TR+EN senaryoları |
| Frontmatter / metadata JSON (title, summary, tags) | 🔲 | MD gövdeden ayrı alanlar |
| `ETag` / `Last-Modified` / cache header | 🔲 | Müşteri CDN önbelleği |
| API key veya domain allowlist (B2B) | 🔲 | Tam public vs kısıtlı mod |
| Webhook: yayınlandığında müşteri URL’ine bildirim | 🔲 | Opsiyonel Faz 2 |
| Statik export (S3 / dosya drop) | 🔲 | Opsiyonel; API öncelikli |
| RSS/Atom feed (duyurular) | 🔲 | Opsiyonel |
| HTML render endpoint | 🔲 | Opsiyonel; birincil çıktı **markdown** |
| MD içi görsel — public asset URL | 🔲 | MinIO signed/public URL stratejisi |
| Dahili DI link rewrite (public slug’a) | 🔲 | Aksi halde müşteri kırık link yönetir |
| UI: «Web’de yayınla» + önizleme | 🔲 | Editör / metadata paneli |

#### 4.4.3 API taslağı (hedef)

Kimlik doğrulama: oturumsuz (tam public SSS) veya `X-Api-Key` / domain allowlist — tenant bazlı yapılandırma.

```http
GET /documents/api/v1/public/pages?lang=tr
→ [{ "slug", "title", "summary?", "updatedAt", "lang" }]

GET /documents/api/v1/public/pages/{slug}?lang=tr
→ {
    "slug", "title", "markdown", "updatedAt", "lang",
    "tags"?, "publishedAt"?
  }
```

- Gateway’de ayrı route veya MngDocument **PublicPagesController** (`[AllowAnonymous]` + tenant/domain çözümü).
- Rate limit + yalnızca GET.
- **Döküman / Dosya** bandına endpoint açılmaz.

#### 4.4.4 Akış

```text
Editör (Mng.Ui) → taslak → yayınla (status=published)
                              ↓
                    webPublish = true (bilinçli)
                              ↓
              Public API / (opsiyonel) webhook
                              ↓
              Müşteri web sitesi — MD → HTML
```

#### 4.4.5 Bilinçli sınırlar

- Resmi, antetli, denetim kaydı gerektiren içerik → **Döküman** bandı (indirme / arşiv); web yayını değil.
- Olay tetikli otomatik **Sayfa** üretimi bu fazın kapsamı değil (ayrı ürün kararı).
- Her Döküman üretimine otomatik eşlik eden Sayfa **varsayılan değil**; istenirse ayrı profil tanımlanır.
- Çeviri / yeminli metin garantisi yok; web metni editör sorumluluğunda.

**Roadmap fazı:** **Faz P-WEB** (Sayfa alt fazı) · Detay: §8

---

### 4.5 Diğer planlanan (Sayfa)

| Özellik | Durum | Not |
|---------|-------|-----|
| Sayfa yorumu (thread) | 🔲 | **Faz P+** |
| İzle / bildirim | 🔲 | **Faz P+** + Notifier |
| AI: özet, RAG, benzer sayfa | 📋 | **Faz AI** |
| Otomatik sayfa üretimi (olay → MD taslağı) | 🔲 | Web yayınından **ayrı** karar; varsayılan roadmap dışı |

---

## 5. Dosya — özellik envanteri

**Teknik:** `type=file`, `origin=upload` (veya collabora-dışı uzantı; harici kaynak içe alımında `origin=upload` veya `system` — bkz. §5.7) · **Referans paket:** Dosyalar *(ürün dili sektörden bağımsızdır)*

**Ürün kimliği:** **Dosyalar**, kurumun **dışarıdan getirdiği** içeriğin (PDF, tarama, sözleşme, spec, görsel…) merkezi arşividir. Kullanıcı yükler *(veya harici kaynaktan otomatik içe alınır)*; yetki uygulanır; **AI ile etiketlenir ve içerikten aranır**; eşleşen dosyalar keyword/etiket ile bulunur. Gerekirse etiket/kural sonucu **OC süreci veya Workflow** tetiklenir. Resmi Office üretimi ve wiki metni **Dosya değil** — Döküman / Sayfa.

**Müşteri özeti (tek cümle):** Yüklediğiniz evraklar tek yerde, yetkili ve aranabilir; AI içeriği etiketler, siz kaynağı sınıflandırırsınız (gizlilik); kurallara uygunsa süreç başlar.

**Etiketleme:** İçerik etiketi (AI) + **meta etiket** (public/confidential vb.) — bkz. §3.1

### 5.1 Barındırma ve erişim

| Özellik | Durum | Not |
|---------|-------|-----|
| Klasör hiyerarşisi | ✅ | |
| Kullanıcı dosya yükleme | ✅ | UI + API |
| Çoklu format yükleme (PDF, DOCX, zip, img…) | ✅ | Öncelik PDF/DOCX |
| Klasör yetkisi: görme | ✅ | |
| Klasör yetkisi: indirme | ✅ | |
| Klasör yetkisi: dosya ekleme | ✅ | |
| Silme / yönetme yetkileri | 🔶 | Teklifte analizde netleşecek |
| Sürüm (yeni yükleme = yeni sürüm) | ✅ | |

### 5.2 Önizleme, metadata ve etiketleme

| Özellik | Durum | Not |
|---------|-------|-----|
| Görsel / PDF / metin önizleme | ✅ | Faz 1 |
| Yüklenen DOCX → PDF önizleme (Gotenberg) | ✅ | **D-FILE-PREV** |
| Collabora düzenleme | ❌ | Bilinçli — Döküman bandına geçiş inject ile |

**İçerik etiketi** *(içerik hakkında — §3.1 katman 1)*

| Özellik | Durum | Not |
|---------|-------|-----|
| Manuel içerik etiketi | ✅ | `dm_tags` |
| **AI otomatik içerik etiketleme** | 📋 | Yükleme / içe alım sonrası — **Faz AI** |

**Meta etiket** *(kaynağın sınıflandırması — §3.1 katman 2)*

| Özellik | Durum | Not |
|---------|-------|-----|
| Meta etiket kataloğu (`public`, `confidential`…) | 🔲 | **D-META-TAG** · Teklif §4.1.1-C gizlilik |
| Yükleme / düzenleme sırasında meta atama | 🔲 | Zorunlu veya opsiyonel alan |
| Klasör varsayılan meta mirası | 🔲 | |
| Meta → export / indirme / dış paylaşım kısıtı | 🔲 | Politika |
| Diğer metadata alanları (proje, revizyon, sipariş no…) | 🔲 | Yapılandırılabilir alan seti — teklif §4.1.1-C |

| Metadata / etiket ile filtreleme | 🔶 | İçerik etiketi ✅; meta + birleşik filtre 🔲 |

### 5.3 Arama ve keşif

| Özellik | Durum | Not |
|---------|-------|-----|
| Dosya adı / açıklama araması | ✅ | DG regex / tree search |
| **Keyword / etiket ile eşleşen tüm dosyalar** | 🔶 | Etiket filtresi ✅; tam keyword envanteri AI + indeks ile **Faz AI** |
| **Dosya içeriği araması** (full-text) | 📋 | PDF/DOCX metin çıkarımı + indeks; OCR taranan PDF için ayrı karar |
| Metadata + etiket + içerik birleşik arama | 📋 | Teklif §4.1.1-G |
| Hazır / varsayılan sorgular | 🔲 | «Son yüklenenler», «etiket X», «varlık Y içeren» — AI ile |

### 5.4 Bildirimler

| Özellik | Durum | Not |
|---------|-------|-----|
| Yeni dosya bildirimi | 🔲 | **Faz D-N** |
| Yeni sürüm bildirimi | 🔲 | **Faz D-N** |
| Kanallar: in-app, e-posta, Telegram | 🔲 | Notifier ortak model |

### 5.5 AI — Dosya bandı (referans paket + ürün genişlemesi)

| # | Yetenek | Durum |
|---|---------|-------|
| — | **Otomatik etiketleme** (içerikten) | 📋 | Öncelikli AI çıktısı; kullanıcı onayı opsiyonel |
| — | Otomatik özet | 📋 | Arama / keşif |
| 1 | Akıllı soru–cevap (RAG, yetki sınırlı) | 📋 |
| 2 | Benzer / ilgili dosyalar | 📋 |
| 3 | Sürüm fark özeti | 📋 |
| 4 | Eksik / tutarsızlık uyarısı | 📋 |
| 5 | Çok dilli köprü (özet / terim listesi) | 📋 |
| 6 | Otomatik klasör önerisi (yükleme anı) | 📋 |
| 7 | Varlık çıkarma (firma, parça no, tarih…) | 📋 | Etiket ve tetik kurallarına girdi |

> Model yerleşimi (on-prem / müşteri onaylı servis) ve OCR kapsamı proje bazlı. **Faz AI**

### 5.6 Harici kaynak içe alımı (FTP / SFTP)

Periyodik olarak tanımlı **FTP veya SFTP** sunucusu taranır; kriterlere uyan dosyalar DI **Dosya** bandına otomatik alınır.

| Özellik | Durum | Not |
|---------|-------|-----|
| FTP / SFTP bağlantı profili (host, kimlik, yol) | 🔲 | **F-FILE-INGEST** |
| Periyot (Scheduler ile: saat/gün/cron) | 🔲 | Platform **Scheduler** omurgası |
| Dosya filtresi (uzantı, maske, min/max boyut, tarih) | 🔲 | |
| Hedef klasör + `origin` (upload / system) | 🔲 | |
| Yinelenen / delta (checksum, dosya adı, mtime) | 🔲 | Aynı dosyada sürüm mü yeni kayıt mı — kural |
| Başarısız aktarım / karantina klasörü | 🔲 | |
| İçe alım sonrası AI etiket pipeline tetigi | 🔲 | §5.5 ile zincir |
| Audit: kim/ne zaman/hangi kaynaktan | 🔲 | |

**Akış:**

```text
[Scheduler] → FTP/SFTP poll → filtre → DI Dosya (upload)
                              ↓
                    AI etiket + varlık çıkarımı
                              ↓
                    (opsiyonel) kural eşleşmesi → §5.7
```

### 5.7 Kural tabanlı süreç tetikleme (etiket / AI sonucu)

AI etiketleri, varlık çıkarımı veya metadata belirli **kritere** uyduğunda **Operasyon Merkezi** süreci veya **Workflow** otomatik başlatılır.

| Özellik | Durum | Not |
|---------|-------|-----|
| Tetik kural tanımı (içerik etiket, **meta etiket**, keyword, varlık, metadata) | 🔲 | **F-FILE-TRIGGER** |
| Koşul: «meta `confidential` AND içerik etiket `sözleşme`» | 🔲 | İki katman birlikte |
| Koşul: «içerik etiket X AND dosya tipi PDF» | 🔲 | |
| Aksiyon: OC WorkItem / workspace süreci oluştur | 🔲 | **OC** entegrasyonu |
| Aksiyon: Workflow flow başlat (HTTP / iç olay) | 🔲 | **D-WF** |
| Aksiyon: bildirim only (D-N) | 🔲 | Hafif senaryo |
| Kural çalıştırma audit + dry-run | 🔲 | |
| İnsan onayı kapısı (tetik öncesi) | 🔲 | Opsiyonel; savunma/banka |

**Örnek senaryolar (sektörden bağımsız):**

| Olay | Kural | Sonuç |
|------|-------|--------|
| SFTP’den kalite raporu PDF içe alındı | AI etiket `kalite-raporu` | OC: inceleme WorkItem |
| Yüklenen sözleşmede meta `confidential` | meta etiket | Workflow: onay zinciri |
| SFTP PDF + meta mirası `internal` | klasör varsayılanı | Export kısıtı |
| Tedarikçi spec’inde parça no eşleşmesi | varlık çıkarımı | OC: satın alma süreci |

**Platform bağlantısı:** Tetik kaynağı = **modül olayı** (mor ok → Workflow) veya doğrudan OC API; zamanlama = FTP poll (**Scheduler**, kehribar ok).

### 5.8 Diğer entegrasyonlar

| Özellik | Durum | Not |
|---------|-------|-----|
| İş kaydına (OC) manuel bağlama | ✅ | Teklifte ayrı kalem değil |
| Office inject → Döküman bandına taşıma | 📋 | Best-effort — bkz. §6.8 |

---

## 6. Döküman — özellik envanteri

**Teknik:** `type=file`, `origin ∈ {native, manual, system}`, `.docx/.xlsx/.pptx` · **Referans paket:** Dökümanlar *(ürün dili sektörden bağımsızdır)*

**Ürün kimliği:** **Dökümanlar**, kurumun **resmi Office kayıtlarıdır** — Word, Excel, sunum. Platformda oluşturulur veya şablon/veri ile **üretilir**; tarayıcıdan düzenlenir; antet ve belge kodu taşır. **Manuel** (`native`, şablondan) veya **sistem** (olay, API, zamanlama) oluşturulduktan sonra **AI ile etiketlenebilir**; etiket/kural eşleşmesinde **OC veya Workflow** otomatik veya **kullanıcı onayıyla** başlatılır. Yüklenen arşiv → **Dosya**; wiki metni → **Sayfa**.

**Müşteri özeti (tek cümle):** Resmi belgelerinizi tek yerde üretin ve düzenleyin; AI içeriği etiketler, siz gizlilik sınıfını belirlersiniz; kurallara göre süreç devreye girer.

**Etiketleme:** İçerik etiketi (AI) + **meta etiket** (public/confidential vb.) — bkz. §3.1

### 6.1 Türler ve Collabora

| Tür | Uzantı | Editör | Durum |
|-----|--------|--------|-------|
| **Belge** | DOCX | Collabora Writer | ✅ |
| **Sheet** | XLSX | Collabora Calc | ✅ **S1+S2** |
| **Sunum** | PPTX | Collabora Impress | ✅ **Pr1+Pr2** |

| Özellik | Durum | Not |
|---------|-------|-----|
| Native oluşturma (boş) | ✅ | **D-CREATE** |
| Collabora düzenleme (WOPI) | ✅ | |
| Export / print yetkileri | ✅ | Dosya bandından geniş |
| PDF önizleme / export | ✅ | |
| DOCX sürüm geçmişi + UI | ✅ | **D2 / D-VERSIONS** |
| Sürüme geri dönüş / klonlama | ✅ | |
| Eşzamanlı düzenleme | ✅ | |
| Oturum sayımı, limit, kilitleme | ✅ | **Faz D-E** |
| Döküman taslak → yayın (docx) | 🔲 | **Faz M** — yalnızca Sayfa’da var |

### 6.2 Yetkilendirme (Döküman)

Klasörden miras; Dosya bandından **ek** yetkiler:

| Yetki | Durum |
|-------|-------|
| Görme | ✅ |
| İndirme | ✅ |
| Oluşturma / ekleme | ✅ |
| Düzenleme (edit) | ✅ |
| Export | ✅ |
| Print | ✅ |

### 6.2a Meta etiketleme (kaynak sınıflandırması)

Döküman oluşturulurken veya sonrasında kaynağa **meta etiket** atanır — içerik AI etiketinden bağımsız (§3.1).

| Özellik | Durum | Not |
|---------|-------|-----|
| Meta katalog (`public`, `internal`, `confidential`…) | 🔲 | **D-META-TAG** |
| Native / şablondan üretimde meta seçimi | 🔲 | Zorunlu alan opsiyonu |
| Sistem üretiminde meta (profil / klasör varsayılanı) | 🔲 | Örn. CoC → `internal` |
| Meta → export / print kısıtı | 🔲 | `confidential`: watermark / onay |
| Meta → D-DOC-TRIGGER koşulu | 🔲 | §6.11 |
| Şablonda varsayılan meta | 🔲 | Belge Tasarımcısı profili |

### 6.3 Kurumsal kimlik

| Özellik | Durum | Not |
|---------|-------|-----|
| Antet kataloğu (`dm_letterheads`) | ✅ | **D-BR1** |
| Antet Collabora ile tasarım | ✅ | Tablo footer |
| Native oluşturmada antet seçimi | ✅ | |
| Şablonda varsayılan antet | ✅ | `defaultLetterheadId` |
| Üretim anında antet override | ✅ | |
| Kapak sayfası kataloğu | 🔶 | **D-BR2** kısmi — backlog |
| Üretimde opsiyonel kapak | 🔶 | **D-BR2** |

### 6.4 Belge Tasarımcısı (şablon ve parametre)

Ayrı UI: `/apps/document-intelligence/designer`

| Özellik | Durum | Not |
|---------|-------|-----|
| Şablon kategorileri (`dm_template_categories`) | ✅ | |
| Şablondan referans upload (`from-reference`) | ✅ | LibreOffice `{{param}}` |
| Parametre envanteri + tanım (skaler) | ✅ | **D1** |
| Şablon publish / unpublish | ✅ | |
| Manuel şablondan üretim UX | ✅ | **D4** |
| Merge + PDF çıktı | ✅ | |
| Belge kodu (`documentNo`) / sayaç | ✅ | |
| Yapısal parametreler (tablo, kart, chart) | 🔶 | **D-P** ertelendi; **G2/G5** kısmi |
| Parametre ↔ dataset envanter UI | ⏸️ | **D-P** |
| `dataSourceRef` + DG executor | 🔶 | **G1/G4** |
| RuntimeEnvelope, producer katalog | ✅ | **G0–G5** |
| DOCX tablo merge | ✅ | **G2** |
| XLSX tablo / scalar merge | ✅ | **S3-lite**, **G5** |

### 6.5 Üretim kanalları

| Kanal | Durum | Örnek |
|-------|-------|-------|
| Kullanıcı tıklaması (manuel üretim) | ✅ | Belge Tasarımcısı UX |
| Native boş oluşturma | ✅ | Klasör menüsü |
| Olay tetikli (`system`) | ✅ | CoC, Activity, sevkiyat listesi |
| API / generation profile | ✅ | `coc.fromLine` vb. tenant profil kodları |
| Zamanlanmış (`system` + scheduler) | 🔲 | **Faz D-S** |
| Medya paketi: dashboard XLSX | ✅ | PACKAGE-DASHBOARD-STD |
| Medya paketi: brief PPTX | ✅ | PACKAGE-BRIEF-STD |
| Writeback (üretilen id → iş kaydı alanı) | ✅ | dashboard/brief/sevkiyat |
| **Oluşturma sonrası AI otomatik etiket** | 📋 | `native` · `manual` · `system` — bkz. §6.10 |
| **Etiket/kural → OC / Workflow tetik** | 🔲 | Otomatik veya onaylı — bkz. §6.11 |

### 6.6 Bildirim ve izlenebilirlik

| Özellik | Durum | Not |
|---------|-------|-----|
| Üretim bildirimi | 🔲 | **D-N** |
| Güncelleme / yeni sürüm bildirimi | 🔲 | **D-N** |
| Kanallar: in-app, e-posta, Telegram | 🔲 | Raporlama ile ortak |
| Kim üretti / ne zaman güncelledi | ✅ | Audit |
| Anlık kim düzenliyor (Collabora) | ✅ | **D-E** |
| Süre kullanım raporu (DOCX çıktı) | 🔲 | Teklif §4.1.2-I — ayrı yönetim ekranı değil |

### 6.7 Opsiyonel olgunluk (referans opsiyon paketleri)

| Opsiyon | Durum | Not |
|---------|-------|-----|
| **O1** Onaylı yayın (taslak→onay→yayında) | 🔲 | **D-WF / Faz M** |
| **O2** Toplu içerik güncelleme | 🔲 | dry-run + onay önerilir |

### 6.8 Office inject (Dosya → Döküman geçişi)

| Özellik | Durum | Not |
|---------|-------|-----|
| Mevcut Word/Excel/PPT’yi döküman olarak alma | 📋 | Best-effort |
| Makro / ActiveX kaybı kabulü | 📋 | Teklif §4.1.2-H |
| Manuel tamamlama (geliştirici) | 📋 | |

### 6.9 AI — Döküman bandı

**Dosya AI mirası** (RAG, benzer, fark özeti, varlık çıkarma…) — 📋 **Faz AI**

| — | **Otomatik etiketleme** (oluşturma / yeni sürüm sonrası) | 📋 | §6.10 — öncelikli tetik zinciri girdisi |
| — | Otomatik özet | 📋 | |

**Dökümana özgü** (referans paket — Döküman AI):

| # | Yetenek | Durum |
|---|---------|-------|
| 1 | Tam döküman çevirisi | 📋 |
| 2 | Tek tık dil varyantı | 📋 |
| 3 | Seçili bölüm çevirisi | 📋 |
| 4 | Çift dilli sürüm | 📋 |
| 5 | Hedef dilde özet | 📋 |
| 6 | Terim sözlüğü ile çeviri | 📋 |
| 7 | Ton uyarlama | 📋 |
| 8 | Şablon parametresi önerisi | 📋 |
| 9 | Özet sunum (PPTX) üretimi | 📋 |
| 10 | Kontrol listesi AI | 📋 | Eksik alan → §6.11 kural girdisi |

### 6.10 Oluşturma sonrası AI etiketleme

Döküman **manuel** veya **sistem** ile oluşturulduktan sonra içerik AI ile analiz edilir; **içerik etiketi** (ve isteğe bağlı özet / varlık) önerilir — **meta etiket AI tarafından otomatik atanmaz** (varsayılan; gizlilik önerisi ayrı onay kartı opsiyonel).

| Özellik | Durum | Not |
|---------|-------|-----|
| Tetik: `origin = native` (yeni döküman) | 📋 | Kayıt sonrası async pipeline |
| Tetik: `origin = manual` (şablondan) | 📋 | Merge tamamlandıktan sonra |
| Tetik: `origin = system` (otomatik üretim) | 📋 | CoC, Activity, XLSX/PPTX üretimi dahil |
| Tetik: yeni sürüm (Collabora kaydet) | 📋 | Opsiyonel yeniden etiket |
| Onaylanabilir etiket modeli | 📋 | Otomatik uygula vs kullanıcı onayı |
| Etiket → §6.11 kural değerlendirme | 🔲 | Zincir |

**Akış:**

```text
Oluştur / üret (native | manual | system)
        ↓
   AI: etiket + (opsiyonel) varlık / checklist
        ↓
   Kural motoru (§6.11)
        ↓
   OC / Workflow / bildirim / kullanıcı onay kuyruğu
```

### 6.11 Kural tabanlı süreç tetikleme (etiket / AI sonucu)

Dosya bandındaki **F-FILE-TRIGGER** (§5.7) ile **aynı kural mantığı**; kaynak bandı `Döküman`, olaylar farklı.

| Özellik | Durum | Not |
|---------|-------|-----|
| Tetik kuralı (içerik etiket, **meta etiket**, şablon kodu, `documentNo`, varlık) | 🔲 | **D-DOC-TRIGGER** |
| Koşul: «etiket `coc` AND origin=system» | 🔲 | |
| **Mod: otomatik başlat** | 🔲 | Doğrudan OC / Workflow |
| **Mod: kullanıcı onayı ile başlat** | 🔲 | In-app onay kartı; reddedilirse audit |
| Aksiyon: OC WorkItem / workspace süreci | 🔲 | |
| Aksiyon: Workflow flow | 🔲 | **D-WF** |
| Aksiyon: yalnızca bildirim (D-N) | 🔲 | Hafif senaryo |
| Üretim profili / şablon bazlı varsayılan kural | 🔲 | Örn. CoC → kalite inceleme WI |
| Dry-run + audit | 🔲 | |

**Örnek senaryolar (sektörden bağımsız):**

| Olay | Kural | Sonuç |
|------|-------|--------|
| CoC sistem üretildi | AI etiket `kalite-coc` | **Onaylı:** kalite WI taslağı → kullanıcı onaylar |
| Aylık rapor DOCX (D-S) | şablon kodu + etiket | Workflow: dağıtım zinciri |
| Checklist AI «eksik alan» | kontrol listesi sonucu | OC: tamamlama görevi — otomatik |
| Native boş sözleşme şablonu | etiket `hukuk-inceleme` | Workflow: inceleme adımı |

**Onaylı yayın (O1) ile ilişki:** O1 yaşam döngüsü docx için **Faz M / D-WF**; §6.11 tetikleri onay zincirine **bağlanabilir** veya onay öncesi/sonrası ayrı kurallar tanımlanabilir.

**Ortak kural motoru (hedef):** Dosya (§5.7) + Döküman (§6.11) → tek **içerik tetik** katmanı (`D-TRIGGER`); band ve olay tipi parametre.

---

## 7. Yapay zekâ — ürün perspektifi

Bu bölüm **ürün vizyonunu** tanımlar; geliştirme fazı veya öncelik sırası içermez. Kaynak: referans teklif paketleri (Dosya / Döküman AI) — **sektörden bağımsız** MonitraNG DI perspektifi.

### 7.1 Konumlandırma

DI’da yapay zekâ, kurumsal içeriği **anlamlandırır**, **bulunur** ve **aksiyona** dönüştürür:

| Rol | Açıklama |
|-----|----------|
| **Anlama** | İçerikten özet, etiket, varlık, tutarsızlık |
| **Keşif** | RAG soru–cevap, benzer kayıt, içerik araması |
| **Üretim desteği** | Çeviri, parametre önerisi, özet sunum (döküman) |
| **Orkestrasyon girdisi** | AI çıktısı → kural → OC / Workflow (§5.7 · §6.11) |

**AI ne yapmaz (ürün sınırı):**

- **Meta etiket** atamaz (`public`, `confidential`…) — §3.1; governance kullanıcı/politika alanıdır  
- Yeminli çeviri veya layout-mükemmel garanti vermez  
- Yetki dışı içeriğe RAG cevap üretmez  

### 7.2 Etiketleme ile ilişki (§3.1)

| | AI rolü |
|---|---------|
| **İçerik etiketi** | AI önerir / uygular (onaylanabilir) — «bu ne hakkında» |
| **Meta etiket** | AI **değil** — «buna nasıl davranılır» |

AI içerik etiketleri arama, benzer kayıt ve tetik kurallarının birincil girdisidir.

### 7.3 Genel ilkeler

| Konu | Ürün kararı |
|------|-------------|
| **Öncelikli formatlar** | PDF, DOCX (Dosya ve Döküman içerik analizi) |
| **Taranmış PDF** | OCR gerekebilir — kapsam müşteri / proje bazlı |
| **Model yerleşimi** | On-prem veya müşteri onaylı servis |
| **Çıktı modeli** | Öneri + insan onayı (özellikle etiket ve çeviri) |
| **Yetki** | Tüm AI erişimi mevcut klasör/dosya yetkileri ile sınırlı |

### 7.4 Dosyalar — AI yetenek seti

Yüklenen dosya içeriği (ve FTP/SFTP ile içe alınanlar) üzerinde:

| # | Yetenek | Açıklama |
|---|---------|----------|
| 1 | **Akıllı soru–cevap (RAG)** | Yetki sınırında soru; cevap + kaynak dosya referansı |
| 2 | **Benzer / ilgili dosyalar** | İçerik benzerliğiyle ilişkili kayıtlar |
| 3 | **Sürüm fark özeti** | Yeni sürümde «ne değişti?» |
| 4 | **Eksik / tutarsızlık uyarısı** | Tanımlı kontrol listesine göre eksik / tutarsız bilgi |
| 5 | **Çok dilli köprü** | Örn. TR içerikten EN özet / terim listesi |
| 6 | **Otomatik klasör önerisi** | Yükleme anında hedef klasör önerisi |
| 7 | **Varlık çıkarma** | Firma, parça no, sipariş, tarih vb. yapılandırılmış alanlar |
| — | **Otomatik içerik etiketleme** | Konu / anahtar kavram etiketleri |
| — | **Otomatik özet** | Dosya özeti; arama ve keşif |

**Arama ve keşif (AI destekli):** Metadata, içerik etiketi, varlık çıkarımı ve full-text indeks üzerinde arama; hazır sorgular (son yüklenenler, etiket X, varlık Y içerenler…).

### 7.5 Dökümanlar — AI yetenek seti

**Miras:** §7.4’teki tüm yetenekler dökümanlara da uygulanır (RAG, benzer, fark özeti, tutarsızlık, varlık, içerik etiketi, özet…). Oluşturma kanalı (`native`, `manual`, `system`) fark etmez — §6.10.

**Dökümana özgü ek yetenekler:**

| # | Yetenek | Açıklama |
|---|---------|----------|
| 1 | **Tam döküman çevirisi** | Hedef dilde yeni sürüm veya bağlı kopya (iş amaçlı çeviri) |
| 2 | **Tek tık dil varyantı** | Aynı içerikten hedef dilde kopya |
| 3 | **Seçili bölüm çevirisi** | Paragraf / slayt / seçim bazlı |
| 4 | **Çift dilli sürüm** | Kaynak + hedef dil aynı dökümanda |
| 5 | **Hedef dilde özet** | Export / paylaşım öncesi kısa özet |
| 6 | **Terim sözlüğü ile çeviri** | Kurumsal terim tutarlılığı |
| 7 | **Ton uyarlama** | Müşteri mektubu ↔ iç rapor dili |
| 8 | **Şablon parametresi önerisi** | İş kaydı veya kaynak içerikten alan doldurma önerisi |
| 9 | **Özet sunum üretimi** | Uzun belgeden kısa PPTX özeti |
| 10 | **Kontrol listesi AI** | Zorunlu alan / eksik bilgi kontrolü ve uyarı |

**Çeviri sınırı (müşteri beklentisi):** İş amaçlı çeviri ve içerik adaptasyonu; yeminli çeviri veya piksel-mükemmel biçim garantisi değildir. Karmaşık yerleşim, makro ve formül koruması kapsam dışı; çıktı insan kontrolüne açıktır.

### 7.6 Sayfalar — AI (platform genişlemesi)

Referans tekliflerde Sayfa ayrı AI paketi değildir. DI perspektifinde Sayfa bandı için de anlamlı yetenekler: özet, RAG (yetkili runbook/prosedür), benzer sayfa, (ileride) taslak önerisi. Web yayını (§4.4) ile birlikte düşünülür; **meta etiket** web’de yayın kararında §3.1 ile birlikte geçer.

### 7.7 AI → aksiyon zinciri

Ürün perspektifinde AI yalnızca «akıllı arama» değil; operasyonla bağlanır:

```text
İçerik (Dosya / Döküman) oluştu veya güncellendi
        ↓
   AI: içerik etiketi · özet · varlık · checklist
        ↓
   Kural motoru (içerik etiket + meta etiket + metadata)
        ↓
   OC WorkItem · Workflow · bildirim · (kullanıcı onayı)
```

Detay: §5.7 · §6.11 · §3.1

### 7.8 Müşteri dili — tek paragraf

Kurum evrakları ve resmi belgeler yalnızca arşivlenmez; platform içeriği **okur**, **sınıflandırır** ve **bulur**. Soru sorabilir, benzer kayıtları görür, sürüm farkını özetletir; dökümanlarda çeviri ve parametre desteği alır. Kurallar tanımlandığında AI sonuçları operasyon sürecini tetikler — gizlilik sınıfı (meta etiket) her zaman insan ve politika kontrolünde kalır.

---

## 8. Platform entegrasyonları

| Entegrasyon | Durum | Not |
|-------------|-------|-----|
| OC kalem belgeleri + deep link | ✅ | |
| OC WorkItem ↔ doküman tam UI | 🔲 | **Faz D5** |
| Work item ↔ üretilen belge writeback (G6) | ⏸️ | **G6** ertelendi |
| Raporlama → DI şablon / belge | 🔶 | Müşteri bazlı |
| Workflow: onay, dağıtım, arşiv taşıma | 🔲 | **D-WF** |
| Scheduler → periyodik generate | 🔲 | **D-S** |
| Notifier (üretim, sürüm, dosya) | 🔲 | **D-N** |
| FTP/SFTP → Dosya içe alımı | 🔲 | **F-FILE-INGEST** · §5.6 |
| AI etiket / kriter → OC veya Workflow (Dosya) | 🔲 | **F-FILE-TRIGGER** · §5.7 |
| AI etiket / kriter → OC veya Workflow (Döküman) | 🔲 | **D-DOC-TRIGGER** · §6.11 |
| Ortak kural motoru (Dosya + Döküman) | 🔲 | **D-TRIGGER** |
| DI içerik yedekleme → NAS (çoklu hedef) | 🔲 | **D-BACKUP** · §12 |

**Workflow ayrım ilkesi:** Tek adım üretim + tek mail → DI + Notifier yeterli. İnsan onayı + çok adım → Workflow.

**Olay sözleşmesi (taslak):** `document.generated`, `document.published`, `document.created`, `document.tagged`, `document.ruleMatched` → Workflow / OC · `file.*` → §5.7

---

## 9. Roadmap fazları (özet)

Detay: [DI_PRODUCT_ROADMAP.md](../../odak/document_intelligence/DI_PRODUCT_ROADMAP.md)

```text
Faz P   → Sayfa                         ✅
  P-WEB → Sayfa web yayını (headless)   🔲
Faz D   → Döküman omurgası              ✅ (alt fazlar sürüyor)
  D-BR  → Antet & kapak                  ✅ / 🔶
  D-E   → Collabora oturumları           ✅
  D-P   → Parametre 2.0                  ⏸️ (kısmi G*)
  D-N   → Bildirimler                    🔲
  D-S   → Zamanlama                      🔲
  D-WF  → Workflow                       🔲
  D5    → OC entegrasyonu                 🔶
Faz S   → Sheet                          ✅
Faz Pr  → Sunum                          ✅
Faz AI  → Yapay zekâ (dosya etiket, içerik arama, RAG)  🔲
  D-META-TAG   → Meta etiket (public/confidential…)         🔲
  F-FILE-INGEST  → FTP/SFTP içe alım                    🔲
  F-FILE-TRIGGER → Etiket/kural → OC / Workflow (Dosya)    🔲
  D-DOC-TRIGGER  → Etiket/kural → OC / Workflow (Döküman) 🔲
  D-TRIGGER      → Ortak kural motoru                       🔲
  D-BACKUP       → İçerik yedekleme (çoklu NAS)             🔲
Faz M   → Kurumsal olgunlaşma (docx LC)  🔲
Faz P+  → Sayfa yorum / izle             🔲
```

**Öncelik sırası (Temmuz 2026):** D-N → D-S → D-BR2 → **Faz AI (dosya etiket + içerik arama)** → **F-FILE-INGEST** → **F-FILE-TRIGGER** → D-WF → D5 → P-WEB

---

## 10. Referans teklif eşlemesi (iç kullanım)

| Teklif | Bu doküman |
|--------|------------|
| §4.1.1 Dosyalar | §5 |
| Dosya: FTP/SFTP içe alım | §5.6 — teklif öncesi ürün genişlemesi |
| Meta etiket (gizlilik sınıfı) | §3.1 · §5.2 · §6.2a — Teklif §4.1.1-C |
| Dosya: AI içerik etiket → süreç tetik | §5.7 |
| §4.1.2 Dökümanlar | §6 |
| Döküman: oluşturma sonrası AI etiket + süreç tetik | §6.10–6.11 — teklif AI mirası + ürün genişlemesi |
| Sayfa (platform) | §4 |
| Sayfa web yayını (headless) | §4.4 — platform genişlemesi; teklifte ayrı kalem yok *(henüz)* |
| Dış Katılım — Anket modülü | §4.4.1 ile ayrım — farklı ürün |
| O1 / O2 | §6.7 |
| AI — Dosyalar | §7.4 · §5.5 |
| AI — Dökümanlar | §7.5 · §6.9 |
| AI — genel perspektif | §7 |
| DI yedekleme (içerik → NAS) | §12 |
| Ek A matris | §4–§6 durum sütunları ile senkron |

---

## 11. Teknik referans

| Bileşen | Değer |
|---------|--------|
| Backend | `MngDocument` — `/documents/api/v1/...` |
| UI | `Mng.Ui/pages/apps/document-intelligence/` |
| Designer | `.../document-intelligence/designer/` |
| Dataset’ler | `dm_resources`, `dm_resource_versions`, `dm_resource_permissions`, `dm_document_templates`, `dm_letterheads`, `dm_tags`, `dm_template_categories` |
| Prod operasyon | [PROD_OPERATIONS_AND_MIGRATION.md](../../odak/document_intelligence/PROD_OPERATIONS_AND_MIGRATION.md) |
| Platform yedek (DB) | **MngAdmin** — MongoDB / PostgreSQL dump; DI **içerik dosyalarını** kapsamaz |

---

## 12. Yedekleme — ürün perspektifi

Bu bölüm **ürün sorumluluğu ve hedef yeteneği** tanımlar; implementasyon fazı içermez. Artımlı sync kuralları, restore sihirbazı, key rotation vb. **ileride netleştirilecek** — şu an için fonksiyon tanımı yeterlidir.

### 12.1 Bugün: ne yedekleniyor, ne eksik?

MonitraNG **MngAdmin** ile platform veritabanlarını yedekleyebilir (MongoDB domain DB — `dm_*` metadata dahil; Keeper; PostgreSQL / Keycloak vb.). Dump’lar MinIO’daki **backup** bucket’larına yazılır.

| Katman | Yedekleniyor mu? | Nerede? |
|--------|------------------|---------|
| `dm_resources` metadata (ağaç, izinler, şablon kayıtları…) | ✅ (Mongo dump) | MngAdmin |
| **Dosya / Döküman / Sayfa içerik baytları** | ❌ platform DI hattında | **DataGateway → MinIO** (§12.2 — şifreli/sıkıştırılmış object) |
| Antet / şablon binary (`designStoragePath`, `sourceStoragePath`) | ❌ aynı | DG object storage |

**Sonuç:** Mongo geri yüklense bile **içerik dosyaları** olmadan DI tam restore olmaz. Bugün bu katman **müşteri IT** sorumluluğunda (MinIO volume, NAS snapshot, storage replication).

### 12.2 Birincil depolama — DG / MinIO formatı

DI içerik dosyaları **DataGateway (DG)** üzerinden MinIO’da tutulur. Diskte / object storage’da **ham PDF veya DOCX değil**, DG’nin yazdığı **şifreli ve (yapılandırmaya göre) sıkıştırılmış** object yapısı bulunur:

| Katman | Açıklama |
|--------|----------|
| **Sıkıştırma** | Gzip (opsiyonel, tenant/DG ayarı) |
| **Şifreleme** | AES-256-GCM (DG `FileEncryptionService`) |
| **Adresleme** | MinIO bucket + object path (`sourceStoragePath`, sürüm path’leri…) |
| **Metadata eşlemesi** | Mongo `dm_*` — path referansları |

Platform içinden okuma/yazma: DG decrypt/decompress hattı. MinIO’ya doğrudan bakan biri **okunabilir Office/PDF görmez**.

### 12.3 Ürün hedefi: DI yedekleme (NAS sync)

DI, isteğe bağlı olarak içerik object’lerini tanımlı **bir veya birden fazla NAS hedefine** senkronize eder — müşteri IT’ye bırakmak **varsayılan**, platform yönetimli sync **opsiyon**.

**Kritik ürün kararı:** NAS’a **açılmış dosya ağacı (PDF/DOCX)** kopyalanmaz. Sync, MinIO’daki ile **aynı object yapısı** — **aynı path düzeni**, **aynı şifreli/sıkıştırılmış bayt** — olacak şekilde yapılır. NAS = birincil depolamanın **yapı-koruyan kopyası** (replica), format dönüşümü yok.

**Neden:**

- Birincil depo ile **bit-identical / yapı-identical** restore mümkün  
- NAS’te de içerik **şifreli** kalır (anahtar MonitraNG/DG tarafında)  
- Büyük dosyalarda gereksiz decrypt→re-encrypt maliyeti yok  

**Kapsam (hedef):**

| İçerik | Dahil |
|--------|--------|
| DG object’leri — Dosya / Döküman / Sayfa içerik baytları | ✅ |
| Şablon / antet tasarım object’leri | ✅ |
| MinIO path + object metadata (DG uyumlu) | ✅ |
| Mongo `dm_*` metadata | ❌ — **MngAdmin** (ayrı yedek; restore’da birlikte gerekir) |

**Kapsam dışı (varsayılan):** Collabora geçici oturum, önizleme önbelleği, AI embedding indeksleri.

**Restore ilkesi:** Tam DI geri dönüş = **Mongo dump (MngAdmin)** + **NAS’taki DG object ağacının** birincil MinIO’ya (veya eşdeğer depoya) geri yazılması + DG anahtarının aynı kalması. NAS paylaşımından dosya çift tıklayarak açılmaz — bu **bilinçli güvenlik** özelliğidir.

### 12.4 Çoklu NAS — fan-out modeli

Müşteri **birden fazla NAS sunucusu** tanımlayabilir; DI aynı object setini **eşzamanlı** hedeflere **aynı yapıda** kopyalar (3-2-1, coğrafi kopya).

```text
[MinIO — DG şifreli/sıkıştırılmış object’ler]
        ↓  sync (tam / artımlı object delta)
   DI yedekleme servisi
        ├── NAS-1  (SMB / NFS)  ← aynı path + aynı blob
        ├── NAS-2  (SMB / NFS)
        └── NAS-3  (SFTP)
```

| Özellik | Ürün kararı |
|---------|-------------|
| Sync birimi | **Object** (path + encrypted blob), dosya başına export değil |
| Hedef düzen | Kaynak MinIO path convention ile **aynı ağaç** |
| Hedef protokolleri | SMB, NFS, SFTP |
| Eşzamanlı çok hedef | ✅ — aynı job, tüm aktif NAS’lara aynı object seti |
| Periyot | Scheduler + manuel «şimdi sync» |
| Mod | Tam · artımlı (yeni/değişen object key’leri) |
| Doğrulama | Job durumu, object sayısı, checksum/size karşılaştırma |
| Geri yükleme | v1: IT prosedürü (object’leri MinIO’ya geri); sihirbaz ileride |

### 12.5 Sorumluluk matrisi (müşteri dili)

| Senaryo | Kim? |
|---------|------|
| Mongo / Keeper DB yedek | Platform — **MngAdmin** |
| MinIO altyapı snapshot (VM / volume) | Müşteri IT *(varsayılan)* |
| DI object’lerini NAS’a sync (şifreli, aynı yapı) | **Opsiyon — DI yedekleme** (§12) veya IT (MinIO replication) |
| DR testi, off-site taşıma | Müşteri IT + DI job raporları |

**Satış mesajı:** «Veritabanı yedeği tek başına yetmez. DI yedekleme, belgelerinizi MinIO’daki **güvenli formatta** — şifreli ve sıkıştırılmış — kurum NAS’ınıza **aynı yapıda** kopyalar; birden fazla siteye eşzamanlı. NAS’te ham dosya açılmaz; anahtar platformda kalır.»

### 12.6 Tetikleyiciler ve entegrasyon

| Tetik | Açıklama |
|-------|----------|
| Zamanlama | Platform Scheduler — günlük tam / saatlik delta |
| Olay | Yeni sürüm, toplu üretim sonrası «hot folder» sync *(opsiyonel)* |
| Manuel | Admin: «tüm DI içeriğini yedekle» / klasör bazlı |

MngAdmin backup job’larından **ayrı** job tipi: `di-content-sync` — Mongo metadata ayrı (MngAdmin).

### 12.7 Güvenlik ve uyumluluk

- NAS’taki kopya **şifreli blob** — DG encryption key olmadan okunamaz  
- Encryption key rotation: sync + restore prosedürü dokümante edilmeli *(ileri)*  
- Yedek trafiği tenant ağı içinde; NAS credential’ları şifreli yapılandırma  
- **Meta etiket** (`confidential`) yedek hedef politikası: örn. `confidential` yalnızca «Gizli NAS» hedefine  
- Yedek dosyalarında yetki ACL taşınmaz — NAS paylaşım izinleri müşteri IT  
- Silinen kaynak: yedekte retention / legal hold politikası müşteri tarafı  

### 12.8 Özellik envanteri (hedef)

| Özellik | Durum |
|---------|-------|
| NAS sync — DG object (şifreli/sıkıştırılmış, aynı path) | 🔲 **D-BACKUP** |
| Çoklu NAS eşzamanlı fan-out | 🔲 **D-BACKUP** |
| SMB / NFS / SFTP hedef profili | 🔲 |
| Tam + artımlı mod | 🔲 |
| Scheduler + manuel job | 🔲 |
| Job durumu / hata bildirimi (D-N) | 🔲 |
| Meta etiket → hedef NAS filtresi | 🔲 |
| Platform restore sihirbazı | 🔲 *(ileri)* |

**Müşteri özeti (tek cümle):** DI yedekleme, belgelerinizi NAS’a **MinIO ile aynı güvenli formatta** kopyalar — çoklu site, tek tık restore için Mongo yedeği ile birlikte.

---

## Broşür (ertelendi)

Landing / broşür metinleri modül özellikleri netleşene kadar **doldurulmayacak**. Taslak: [platform-tanitimi.md § Döküman Zekası](./platform-tanitimi.md) · İç sunum: [KURUMSAL_ICERIK_SUNUM.md](../../odak/document_intelligence/KURUMSAL_ICERIK_SUNUM.md)

---

## Görseller (bekleyen)

| Dosya | Açıklama |
|-------|----------|
| `../Files/di-ekran-kaynak-agaci.png` | Kaynak ağacı — üç band |
| `../Files/di-ekran-sayfa-editor.png` | Sayfa markdown editör |
| `../Files/di-ekran-dosya-onizleme.png` | Dosya PDF önizleme |
| `../Files/di-ekran-collabora-docx.png` | Döküman Collabora |
| `../Files/di-ekran-belge-tasarimcisi.png` | Belge Tasarımcısı |

---

*Son güncelleme: Temmuz 2026 · MonitraNG Pazarlama · Özellik envanteri v2.7 (§12 NAS = DG object sync)*
