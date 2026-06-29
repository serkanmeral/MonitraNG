# MngDocument — Oturum durumu

## Son Çalışılan Konu

Odak **kalem belgeleri** (CoC + Activity), **LINE-ACTIVITY-STD** şablon tasarımı, belge üretimi (generation profiles), parametre uyarıları, DI deep link, şablon **görüntüle / kilidi aç** akışı.

## Tamamlanan İşler (bu oturum)

### Belge üretimi (backend)
- `DocumentGenerationService` — merge, placeholder koruma, `HasParameterWarnings`, tanımsız/boş parametre analizi.
- Generation profilleri: `odak.coc.fromLine`, `odak.line.activity.fromLine` (`appsettings.json`).
- `DocumentContextCatalog` — kalem/paket/sevkiyat/CoC/activity alanları.
- Activity writeback: `activityDiResourceId`, `activityDocNo`, `activityGeneratedAt`, vb.
- `DocumentParameterResolver` — bool → Evet/Hayır.
- **`POST /templates/{id}/unpublish`** — üretimde aktif şablonu düzenlenebilir yapma (belge üretimi unpublish sonrası durur).

### Activity şablonu (LINE-ACTIVITY-STD)
- Profesyonel DOCX gövdesi: `build-line-activity-seed-docx.ps1` + `line-activity-docx-content.json` (UTF-8, Türkçe karakter düzeltmesi).
- Seed/deploy: `seed-designer-template-line-activity-standard.ps1`, `deploy-line-activity-design-test.ps1`, `patch-line-activity-standard-test.ps1`.
- Test sunucusunda şablon **published**; Türkçe etiketler doğrulandı.
- `styles.xml` bozukluğu giderildi (Collabora “belge yüklenemedi” hatası).

### UI — Kalem belgeleri (Odak)
- Sekme: **Kalem Belgeleri** (`documents`, `?tab=coc` alias).
- `OdakSiparisLineDocumentsPanel`, `OdakSiparisLineDocumentsCreateDialog`.
- Combobox: COC-STANDARD + LINE-ACTIVITY-STD.
- `odakSiparisLineDocumentService.ts`, deep link `/apps/document-intelligence/r/{id}`.

### UI — Belge tasarımcısı
- **Üretimde aktif** / **Düzenlenebilir** terminolojisi.
- Published şablon: **Görüntüle** (Collabora salt okunur) + **Kilidi aç** (unpublish → düzenle).
- Editör sayfası: unpublish + üretimde aktif et butonları.

### Dataset
- `odak_siparis_kalemleri` — activity writeback alanları (36 alan).

## Devam Eden / Faz 2

- Activity DOCX: sevkiyat satır tablosu, timeline, computed parametreler.
- Parametre scanner uyarısı (33 incomplete `{` — footer/header XML, fonksiyonel).
- `mngdocument` deploy sonrası bool Evet/Hayır canlı doğrulama.

## Sonraki Adımlar (yeni oturum)

1. **Antet yönetimi** — letterhead seçenekleri, önizleme, şablon başına ince ayar.
2. **Parametre yönetimi UI** — placeholder envanteri, context binding, incremental docNo (D2), designer parametre diyalogu iyileştirme.
3. İsteğe bağlı: published şablon salt okunur önizleme iyileştirmeleri.

## Önemli Notlar

- Test gateway: `http://192.168.20.20:5040`, WOPI: `:5095`.
- Activity deploy: `docs/odak/document_intelligence/scripts/deploy-line-activity-design-test.ps1`
- Published şablon WOPI yazılamaz; düzenleme için **unpublish** veya seed `-Replace` (ops).
- Token: `docs/odak/operationcore/scripts/load-operationcore-token.ps1`

## Son Güncelleme

**29 Haziran 2026** — Activity şablon tasarımı, kalem belgeleri, unpublish akışı tamamlandı; sırada antet + parametre yönetimi.

## Nerede Kalmıştık

Belge tasarımı oturumu hedeflerine ulaşıldı. Yeni chat’te **antet** ve **parametre yönetimi** ile devam edilecek. Test’e `mngdocument` + `mngui` deploy (unpublish API + UI) gerekebilir.
