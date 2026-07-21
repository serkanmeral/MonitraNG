# RP03 — Odak test: DI şablon + letterhead + cover export paketi

**Kullanım:** **PROMPT** bölümünü müşteri terminal Cursor’a yapıştırın.  
**Ortam:** Yalnızca **test** `192.168.20.20` — production yok.  
**Amaç:** MinIO path’lerinden bağımsız binary + meta paketi (lokal `from-reference` / katalog import için).

Karar: [../DOCUMENT_TEMPLATES.md](../DOCUMENT_TEMPLATES.md) · İş akışı: [../REMOTE_CURSOR_WORKFLOW.md](../REMOTE_CURSOR_WORKFLOW.md)

Referans: `docs/odak/document_intelligence/scripts/export-coc-template-from-prod.ps1` (tek şablon; bunu **tüm** şablon/letterhead/cover için genelleştir).

---

## PROMPT (aşağıyı kopyala)

```
MonitraNG repo kökündesin (müşteri terminal PC). Görev: Odak TEST’ten Document Intelligence varlıklarını export et — designer şablonları + letterhead + cover pages. Lokal import / Create YAPMA — sadece paket.

## Ortam (zorunlu)
- Gateway: http://192.168.20.20:5040
- MngDocument / WOPI host: http://192.168.20.20:5095  (WOPI contents için)
- Domain: odak
- Production 192.168.20.8 KULLANMA
- Token/parola chat’e yazma

## Token
pwsh -File .\docs\odak\operationcore\scripts\get-operationcore-token.ps1
veya load-operationcore-token.ps1
Token: $env:TEMP\operationcore_dg_token.txt
Header: Authorization: Bearer <token>
Gerekirse X-Domain-Name: odak

## API (Gateway)
Base: http://192.168.20.20:5040/documents/api/v1

Kategoriler:
  GET .../template-categories/tree

Şablonlar (her kategori veya liste endpoint’i ile tümünü topla):
  GET .../templates?categoryId=<id>
  GET .../templates/{id}   (tam meta: code, name, modelJson/parameters, letterheadId, status, …)

Şablon DOCX (tercih — Collabora güncel içerik):
  GET .../templates/{id}/editor-session
  → accessToken al
  GET http://192.168.20.20:5095/wopi/files/{id}/contents?access_token=...
  → binary kaydet

Letterheads:
  GET .../letterheads
  GET .../letterheads/{id}
  GET .../letterheads/{id}/design-session
  → WOPI: http://192.168.20.20:5095/wopi/files/{id}/contents?access_token=...
  (WOPI id/session letterhead design için editor’ün döndürdüğü alanları kullan; belirsizse LetterheadDesignSessionDto / mevcut export-coc script desenine bak)

Cover pages:
  GET .../cover-pages
  GET .../cover-pages/{id}
  GET .../cover-pages/{id}/design-session
  → aynı WOPI contents indirme

WOPI veya design-session başarısız olursa: DG file download (path alanından) dene; yine olmazsa meta’yı kaydet, binary’yi failures’a yaz.

## Çıktı klasörü (repo içine commit etme)
C:\Users\monitra\Dev\exports\odak-di-pack-YYYYMMDD\
veya docs/odak/exports/odak-di-pack-YYYYMMDD\ (gitignore’da exports/)

Yapı:
  manifest.json
  categories/tree.json
  templates/<code>/meta.json
  templates/<code>/source.docx   (veya .xlsx / .pptx — gerçek uzantı)
  letterheads/<code-or-id>/meta.json
  letterheads/<code-or-id>/design.docx
  cover-pages/<code-or-id>/meta.json
  cover-pages/<code-or-id>/design.docx

meta.json: API’den gelen kaydı olduğu gibi (id, code, name, categoryId, modelJson/parameters, letterhead refs, status, creationMode, …).
manifest.json:
{
  "exportedAt": "<ISO UTC>",
  "source": { "host": "192.168.20.20", "domain": "odak", "gateway": "http://192.168.20.20:5040", "wopi": "http://192.168.20.20:5095" },
  "counts": { "categories": n, "templates": n, "templatesWithBinary": n, "letterheads": n, "coverPages": n },
  "failures": [ { "kind": "template|letterhead|cover", "id": "...", "code": "...", "error": "..." } ],
  "notes": "For local import: categories first, then letterheads/covers, then templates via from-reference + parameters PUT. Mongo dm_* ids will change; match by code."
}

## Kurallar
- PowerShell 7 (pwsh); script yazıp çalıştırabilirsin
- Her binary için byte length > 0 doğrula
- Şifre/token loglama
- Kısmi başarı OK ama failures listesi zorunlu
- İş bitince: path, sayılar, failure özeti chat’te

## Başarı kriteri
- En az 1 template binary + meta
- letterheads ve cover-pages listelendiyse hepsi denenmiş (binary veya failure)
- categories/tree.json var
- manifest.json tutarlı
```

---

## Bu PC’de sonra

1. Paketi `docs/odak/exports/` altına alın.  
2. Lokal import script / prosedür (categories → letterhead/cover → `from-reference`).  
3. (Paralel opsiyon) Person id remap — uzak prompt gerekmez; lokal iş.
