# Adım 4 — Document Intelligence şablonlarını lokal’e taşıma

## Amaç

Müşteri ortamında Belge Tasarımcısı ile oluşturulan / güncellenen **doküman şablonlarını** (ve ilgili katalogları) lokal Docker ortamına almak.

Önkoşul: Adım 1–3 (domain `odak`, user/group normalize, Mongo iş verisi stratejisi). DI, `mngdocument` + DG + MinIO’ya bağlıdır.

---

## Şablonlar nerede durur?

| Katman | Ne | Not |
|--------|-----|-----|
| Mongo / DG | `dm_document_templates` | Metadata: code, name, `modelJson`, parametreler, kategori, dosya alanı path |
| Mongo / DG | `dm_template_categories` | Kategori ağacı |
| Mongo / DG | `dm_letterheads`, `dm_cover_pages`, … | Antet / kapak katalogları (şablonlarla ilişkili) |
| MinIO (DG file field) | DOCX / XLSX / PPTX binary | `referenceFile` / `sourceStoragePath` — **ortam path’i** |
| Collabora / WOPI | Düzenlenen güncel içerik | Export için `editor-session` + WOPI contents (ör. `export-coc-template-from-prod.ps1`) |

**Kritik:** Yalnızca `mng_odak` Mongo dump’ı almak, şablon **dosyalarını** getirmez. MinIO path’leri müşteri ortamına özeldir; lokal’de geçersiz kalır. Bu yüzden DI şablonları Adım 3 dump’tan **ayrı** bir taşıma adımı ister (veya dump + MinIO senkronu birlikte).

Mevcut Odak notu: [PROD_OPERATIONS_AND_MIGRATION.md §6](../../../odak/document_intelligence/PROD_OPERATIONS_AND_MIGRATION.md).

---

## Yöntem seçenekleri

| | A — API export / re-import (önerilen) | B — Mongo + MinIO birlikte | C — Repo seed script’leri |
|--|--------------------------------------|----------------------------|---------------------------|
| Ne | Müşteriden DOCX(+meta) çek → lokal `from-reference` + parametre PUT | `dm_*` dump + ilgili MinIO prefix kopyala | `docs/odak/document_intelligence/scripts/seed-*.ps1` |
| Artı | Path bağımlılığı yok; bilinen API; seçici | Tam birebir (id/path) | Repoda hazır, tekrarlanabilir |
| Eksi | Script yazımı / manuel paket | MinIO erişim + path eşleme zor | Müşterideki **canlı** düzenlemeleri kaçırır |
| Ne zaman | Lokal geliştirme; güncel şablonlar | Tam klon şart | Yalnızca seed’deki referans set |

**Öneri (Adım 4):** **A — export paketi + lokal import.**  
Export **müşteri terminal Cursor**’da (test 20.20) çalışır; paket buraya alınır → [REMOTE_CURSOR_WORKFLOW.md](./REMOTE_CURSOR_WORKFLOW.md).  
Repo seed’leri (C) tamamlayıcı: boş ortamda hızlı smoke; müşteri “gerçek” şablonları için A.

Birebir Mongo iş verisi (Adım 3) şablon **kayıt id**’lerine referans veriyorsa: import sonrası `code` ile eşleyip id map veya B düşünülür. Çoğu generation akışı `templateCode` kullanır → A yeterli olabilir.

---

## Önerilen akış (A)

```text
Müşteri TEST (192.168.20.20 gateway :5040)
  1. Kategori ağacı listele (API)
  2. Şablon listesi (code, id, category)
  3. Letterhead + cover katalog listesi
  4. Her varlık için:
       - Metadata (parameters / modelJson / letterhead-cover alanları)
       - Binary: WOPI contents veya DG file download
  5. Lokal paket klasörü (gitignore):
       templates/<code>/…
       letterheads/<code>/…
       cover-pages/<code>/…

Lokal
  6. DI dataset şemaları: setup-document-intelligence-datasets.ps1
  7. patch-document-intelligence-templates-dataset.ps1
  8. Kategoriler recreate
  9. Letterheads + cover pages import (katalog + dosya)
 10. Şablonlar: POST .../templates/from-reference + parametre PUT
 11. Smoke: structure / designer list / letterhead-cover görünürlük
```

Referans script’ler (müşteri/repo):

- Export örneği: `docs/odak/document_intelligence/scripts/export-coc-template-from-prod.ps1`
- Import/seed örneği: `seed-designer-template-*-standard.ps1` (`from-reference`)
- Toplu repo→ortam: `scripts/odak/seed-di-templates-prod-from-repo.ps1`

Adım 4 için ileride tek bir `export-di-templates.ps1` / `import-di-templates-local.ps1` (gitignore çıktı) planlanabilir — henüz yazılmadı.

---

## İlgili kataloglar (şablonla birlikte düşün)

| Katalog | Taşıma |
|---------|--------|
| `dm_template_categories` | Seed veya export ağaç |
| `dm_letterheads` | DOCX + meta; antet path’leri de MinIO’ya bağlı |
| `dm_cover_pages` | Aynı |
| `dm_document_producers` / context types | Çoğunlukla JSON seed (repoda var) |

Letterhead prod notu: [LETTERHEAD_CATALOG_MIGRATION_PROD.md](../../../odak/document_intelligence/LETTERHEAD_CATALOG_MIGRATION_PROD.md).

---

## Karar

| Konu | Seçenek | Karar |
|------|---------|-------|
| DI şablon taşıma | A API pack · B Mongo+MinIO · C yalnız repo seed | **A — API export / re-import** |
| Kapsam | Yalnız designer templates · + letterhead/cover | **Designer şablonları + letterhead + cover** |
| Kaynak ortam | Müşteri test (`20.20`) · prod (`20.8`) | **Test — `192.168.20.20`** (bu aşama) |

**Tarih:** 2026-07-11

### Kapsam listesi (import hedefi)

| Dataset / varlık | Taşıma |
|------------------|--------|
| `dm_template_categories` | Export ağaç veya seed + gerekirse düzeltme |
| `dm_document_templates` | DOCX + meta (from-reference + parameters) |
| `dm_letterheads` | Tasarım DOCX/meta + katalog kaydı |
| `dm_cover_pages` | Kapak dosyası/meta + katalog kaydı |

Prod (`20.8`) bu aşamada kaynak değil; ileride aynı A yöntemiyle tekrarlanabilir.

---

## Güvenlik

- Export paketi (DOCX + meta) **git’e commit edilmez** (müşteri içeriği / boyutu).
- Token ve gateway URL lokal credentials / env.
