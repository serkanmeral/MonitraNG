# Document Intelligence — Prod işlemleri ve test ortamına taşıma

**Modül:** MngDocument (Document Intelligence)  
**Son güncelleme:** 26 Haziran 2026  
**Checkpoint özeti:** [DEVAM.md](./DEVAM.md)  
**Ana plan:** [MonitraNG_Document_Intelligence_Planning.md](./MonitraNG_Document_Intelligence_Planning.md)

Bu doküman, **şu an prod’da (`192.168.20.8`) yürütülen** DI / Belge Tasarımcısı işlerini, dataset’leri ve script’leri tek yerde toplar. Test sunucusu (`192.168.20.20`) ayağa kalktığında aynı adımların tekrarlanması için kontrol listesi niteliğindedir.

---

## 1. Strateji (onaylanan yön)

| Katman | Karar | Not |
|--------|--------|-----|
| Şablon hazırlığı | **LibreOffice Writer / Word (masaüstü)** | Alanlar `{{paramKey}}` placeholder |
| Belge Tasarımcısı UI | Placeholder **envanteri + parametre tanımı** | Paragraf seçim / web editör **kullanılmıyor** |
| Önizleme (gelecek) | **DOCX → PDF** (headless LibreOffice, on-prem) | Tarayıcıda PDF viewer |
| Web DOCX editör | **Collabora/OnlyOffice — ertelendi** | Kapalı ağda mümkün ama ayrı sunucu maliyeti |
| Merge (gelecek D4) | Open XML `{{key}}` replace + isteğe bağlı LO PDF | D2 incremental numara backend’de |

---

## 2. Ortamlar

| Ortam | Host | Gateway | MngDocument API | Not |
|-------|------|---------|-----------------|-----|
| **Production** | `192.168.20.8` | `:5040` | `/documents/api/v1/...` | Aktif geliştirme / UAT |
| **Test (Odak)** | `192.168.20.20` | `:5040` | Aynı path | Şu an erişilemez / bekliyor |

**Token (prod):**

```powershell
.\docs\odak\operationcore\scripts\load-operationcore-token-prod.ps1
# $env:TEMP\operationcore_dg_token_prod.txt
```

**Token (test / Odak):**

```powershell
.\docs\odak\operationcore\scripts\load-operationcore-token.ps1
# $env:TEMP\operationcore_dg_token.txt
```

**Deploy (prod backend):**

```powershell
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -Paths @('MngDocument','ApplicationResources/mng_apps')
pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngdocument -NoCache
```

**Deploy (test — sunucu hazır olunca):**

```powershell
pwsh -File .\scripts\odak\sync-odak-source.ps1 -Server 192.168.20.20 -Paths @('MngDocument','ApplicationResources/mng_apps')
pwsh -File .\scripts\odak\deploy-odak-apps.ps1 -Server 192.168.20.20 -Services mngdocument -NoCache
```

**UI (`mngui`):** Yalnızca kullanıcı talebiyle prod/test deploy. Geliştirme yerelde `npm run dev`.

---

## 3. Dataset’ler ve prod provizyon

Kaynak tanım: [datasets/documentintelligence_datasets_phase1.json](./datasets/documentintelligence_datasets_phase1.json)

| Dataset | Amaç | Prod script |
|---------|------|-------------|
| `dm_resources` | Klasör / markdown / dosya ağacı | `setup-document-intelligence-datasets.ps1` |
| `dm_resource_versions` | Sürüm geçmişi | ↑ |
| `dm_resource_permissions` | Klasör ACL | ↑ |
| `dm_document_templates` | Parametreli şablon kayıtları | ↑ + **patch** (aşağıda) |
| `dm_template_categories` | Tasarımcı kategori ağacı | ↑ + **seed** (aşağıda) |

**Prod’da tam kurulum sırası:**

```powershell
$env:DI_GATEWAY = "http://192.168.20.8:5040"
.\docs\odak\document_intelligence\scripts\setup-document-intelligence-datasets.ps1

# dm_document_templates eski şema düzeltmesi (sourceResourceId zorunluluğu vb.)
.\docs\odak\document_intelligence\scripts\patch-document-intelligence-templates-dataset.ps1

# 7 kök kategori (idempotent)
.\docs\odak\document_intelligence\scripts\seed-designer-template-categories.ps1
```

**Test ortamına taşıma:** Aynı üç script; `-BaseUrl` / gateway parametrelerini `192.168.20.20:5040` yapın. MongoDB **ayrı volume** — prod verisi otomatik kopyalanmaz; şablon kayıtları ve DG dosya alanları ayrıca export/import gerektirir (bkz. §6).

---

## 4. Uygulanan dilimler (iş günlüğü)

### Faz 1 — Doküman yöneticisi (canlı)

- Resources tree, markdown, dosya upload, izinler, önizleme (görsel/PDF/metin).
- Bkz. [DEVAM.md § Faz 1](./DEVAM.md).

### D1-alpha — Şablon API iskeleti

- `DocumentTemplatesController`, `DocxStructureParser`, `dm_document_templates`.
- `POST /templates/from-source`, `PUT /templates/{id}/parameters`.

### D1-beta — Kategori katalog + referans upload

| Bileşen | Durum |
|---------|--------|
| `dm_template_categories` + CRUD API | ✅ Prod dataset + seed |
| `POST /templates/from-reference` | ✅ Prod smoke 201 |
| `GET /templates?categoryId=` | ✅ |
| `GET /templates/{id}/source/structure` | ✅ |
| Dataset patch (`patch-document-intelligence-templates-dataset.ps1`) | ✅ Prod |
| UI: kategori ağacı, şablon listesi, upload | ✅ Yerel (`mngui` deploy bekliyor) |

### D1-PLACEHOLDER — LibreOffice `{{param}}` modeli (26 Haz 2026)

**Backend (MngDocument):**

- `DocxPlaceholderScanner.cs` — `word/document.xml` + header/footer XML taraması.
- `GET .../source/structure` yanıtına `placeholders[]`, `placeholderWarnings[]` eklendi.
- Placeholder sözdizimi: `{{anahtar}}` — harf ile başlar, `[a-zA-Z0-9_]*`.

**UI (Mng.Ui — yerel):**

- `DiDesignerPlaceholderPanel.vue` — envanter, eksik/fazla sayaç, “Eksikleri içe aktar”.
- `utils/diDesignerPlaceholders.ts`
- Paragraf metin seçimi UI’si **kaldırıldı** (`DiDesignerSourceMarkingPanel`, `diDesignerParameterMarking.ts`).

**Smoke:**

```powershell
.\scripts\tests\MngDocument\smoke-templates-d1beta-prod.ps1
.\scripts\tests\MngDocument\smoke-template-placeholders-prod.ps1   # yapı + placeholder alanları
```

### D4-INFRA — Headless LibreOffice / Gotenberg (26 Haz 2026)

**Docker (on-prem, dış port yok):**

| Servis | Image | Ağ | Açıklama |
|--------|-------|-----|----------|
| `gotenberg` | `gotenberg/gotenberg:8` | `mng_common_mng_network` | Arka planda LibreOffice; DOCX→PDF |
| `mngdocument` | build | aynı | `DocumentRendering__GotenbergBaseUrl=http://gotenberg:3000` |

**Prod deploy sırası:**

```powershell
# mng_apps dizininde (sunucu) veya deploy script ile:
docker compose -f docker-compose.production.yml -f docker-compose.odak.prod.yml --env-file .env up -d gotenberg mngdocument

# Yerel PC'den:
pwsh -File .\scripts\odak\sync-odak-prod.ps1 -Paths @('MngDocument','ApplicationResources/mng_apps')
pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services gotenberg,mngdocument -NoCache
```

**Backend:**

- `DocxPlaceholderMerger.cs` — `{{key}}` → değer (OOXML w:t)
- `GotenbergDocumentRenderService` — `/forms/libreoffice/convert`
- `GET /documents/api/v1/rendering/status` — Gotenberg sağlık
- `POST /documents/api/v1/templates/{id}/render/pdf` — merge + PDF (smoke / önizleme)

**Probe:**

```powershell
.\docs\odak\document_intelligence\scripts\setup-document-rendering-prod.ps1
# veya
.\scripts\tests\MngDocument\probe-document-rendering-prod.ps1
```

**Not:** Parametre tanımı UI’si bu altyapıdan **sonra** devam eder; önce Gotenberg ayakta olmalı.

---

## 5. LibreOffice şablon kuralları (operasyon)

1. Placeholder’ları **tek parça** yazın: `{{musteriAdi}}` — kopyala-yapıştır tercih edin.
2. Anahtar adı parametre `key` ile **birebir** aynı olmalı (büyük/küçük harf duyarlı değil — UI karşılaştırması case-insensitive).
3. Header/footer’daki placeholder’lar da taranır.
4. Tablo hücrelerindeki metin taranır (w:t birleştirme).
5. Karmaşık Word-only özellikler için çıktıyı hedef ortamda test edin.

Örnek şablonlar: [sample/](./sample/) (`ODK-COC-*.docx` — placeholder eklenmiş sürümler ayrıca hazırlanabilir).

---

## 6. Prod → test veri taşıma (plan)

Test sunucusu hazır olunca:

| Veri | Yöntem | Not |
|------|--------|-----|
| Dataset şemaları | §3 script’leri test gateway ile | Idempotent |
| Kategori seed | `seed-designer-template-categories.ps1` | `-Reset` dikkatli |
| Şablon kayıtları (`dm_document_templates`) | DG export/import veya API ile yeniden oluşturma | `referenceFile` / `sourceStoragePath` dosya alanları |
| Yüklenen DOCX binary | MinIO/dosya storage path — ortam bağımlı | Prod path’ler test’te geçersiz olabilir |
| `dm_resources` içeriği | Ayrı migrasyon kararı | Faz 1 verisi |

**Öneri:** Test’te önce boş dataset + seed; ardından birkaç referans DOCX’i `from-reference` ile yeniden yükleyin. Prod’dan toplu kopya ancak storage eşlemesi netleştikten sonra.

---

## 7. Sıradaki teknik dilimler

| ID | İş | Bağımlılık | Durum |
|----|-----|------------|--------|
| **D4-INFRA** | Gotenberg + merge + PDF API | — | ✅ Prod deploy 26 Haz 2026 |
| **D2** | Incremental numara runtime (`@__counters`) | Parametre modeli | ⏳ |
| **D4** | Merge + DOCX indirme (üretim akışı) | D4-INFRA | ⏳ |
| **D4-UI** | PDF önizleme (tarayıcı) | D4-INFRA prod | ⏳ |
| **D3** | Tablo/liste parametreleri | Planlama | ⏳ |

Collabora embed yalnızca “tarayıcıda DOCX düzenleme” ihtiyacı netleşirse değerlendirilir.

---

## 8. Hızlı doğrulama (prod)

```powershell
$gw = "http://192.168.20.8:5040"
$tok = (Get-Content "$env:TEMP\operationcore_dg_token_prod.txt" -Raw).Trim()
$h = @{ Authorization = "Bearer $tok" }

# Kategori ağacı
Invoke-RestMethod "$gw/documents/api/v1/template-categories/tree" -Headers $h

# Şablon listesi (kategori id ile)
Invoke-RestMethod "$gw/documents/api/v1/templates?categoryId=<id>" -Headers $h

# Placeholder envanteri (şablon id ile)
Invoke-RestMethod "$gw/documents/api/v1/templates/<id>/source/structure" -Headers $h

# Rendering altyapısı (Gotenberg / LibreOffice)
Invoke-RestMethod "$gw/documents/api/v1/rendering/status" -Headers $h
# gotenbergReachable: true beklenir
```

---

## 9. İlgili dosyalar (repo)

| Path | Açıklama |
|------|----------|
| `MngDocument/.../DocxPlaceholderScanner.cs` | Placeholder tarama |
| `MngDocument/.../DocumentTemplateService.cs` | Şablon CRUD + structure |
| `Mng.Ui/pages/apps/document-intelligence/designer/` | Belge Tasarımcısı |
| `docs/odak/document_intelligence/scripts/` | Dataset / seed / patch |
| `scripts/tests/MngDocument/` | Smoke script’leri |
| `docs/odak/proddeploy/PROD_SERVER_STATUS.md` | Prod sunucu durumu |

---

## 10. Bilinen prod notları

- `from-reference` öncesi `dm_document_templates` şeması patch edilmediyse DG 400 → backend 500 (çözüldü: patch script).
- Test sunucusu `192.168.20.20` şu an DI geliştirmesinde birincil hedef değil; prod öncelikli.
- `mngui` prod’da eski UI olabilir; Tasarımcı değişiklikleri yerel dev ile doğrulanır.
