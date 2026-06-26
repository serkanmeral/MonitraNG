# MngDocument — Oturum durumu

## Son Çalışılan Konu

Document Intelligence **Belge tasarımcısı**: sayfa yapısı (kenar boşlukları + antet + altbilgi), footer tablo enjeksiyonu, `documentName` parametresi, Collabora WOPI editör, Gotenberg PDF render altyapısı. Prod (192.168.20.8) deploy ve COC-STANDARD şablon güncellemesi.

## Tamamlanan İşler

- **Sayfa yapısı API:** `PUT /templates/{id}/page-structure` — `pageLayout`, `letterhead`, `footer` tek istekte; `PageLayoutInjector` + `TemplatePageLayoutModel` (schema 1.3).
- **Footer tablo:** `FooterInjector` — 2 sütunlu tablo (header ile aynı Collabora sütun tutamaçları).
- **Antet:** `LetterheadInjector` — orta sütun `{{documentName}}`; sistem parametresi `documentName`.
- **Şablon CRUD genişlemesi:** kategori ağacı, blank/referans/duplicate, metadata, publish, letterhead/footer (legacy uçlar), editor-session, WOPI.
- **Render:** `DocxPlaceholderMerger`, `POST /templates/{id}/render/pdf`, Gotenberg servisi (docker-compose).
- **UI:** Designer liste + editör (`Collabora`), `DiTemplatePageStructureForm`, placeholder envanteri paneli, kopyala modalı.
- **Prod deploy (26 Haz 2026):** `mngdocument` + `mngui` (Dockerfile `NODE_OPTIONS=4096` OOM fix). `update-coc-template-prod.ps1 -SkipParameterize` → page-structure OK, 18 placeholder (`documentName` dahil).

## Devam Eden İşler

- Collabora’da footer tablo sütun tutamaçlarının **görsel doğrulaması** (kullanıcı UAT).
- `docNo` run bölünmesi → placeholder scanner uyarısı (fonksiyonel, 18 key tanınıyor).

## Sonraki Adımlar

1. **D2** — incremental `docNo` runtime (DG `@__counters`).
2. **Parametre tanımı UI** — placeholder envanterinden parametre eşleme akışını tamamlama.
3. **D4 merge** — WorkItem / basit DOCX merge + indirme akışı.
4. **Yeni blank şablon** oluştururken `pageLayout`’u create API’ye taşıma (şu an varsayılan + edit dialog).

## Önemli Notlar

- Prod token: `docs/odak/operationcore/scripts/get-operationcore-token-prod.ps1` → `$env:TEMP\operationcore_dg_token_prod.txt`
- CoC güncelleme: `docs/odak/document_intelligence/scripts/update-coc-template-prod.ps1 -SkipParameterize`
- WOPI host (dışarıdan): `http://192.168.20.8:5095`
- COC-STANDARD id: `a5a7c41f-47b7-4cc1-920b-3d485874c362`

## Son Güncelleme

**26 Haziran 2026** — Sayfa yapısı + footer tablo + prod deploy tamamlandı; checkpoint `docs/odak/document_intelligence/DEVAM.md`.

## Nerede Kalmıştık

Backend ve UI prod’da. COC şablonu page-structure ile yenilendi. Yarın: Collabora UAT, ardından D2 veya parametre UI ile devam.
