# Mng.Ui — Oturum durumu (Document Intelligence)

## Son Çalışılan Konu

Belge tasarımcısı UI: sayfa yapısı formu, designer diyalogları, Collabora editör entegrasyonu, i18n.

## Tamamlanan İşler

- `DiTemplatePageStructureForm.vue` — kenar boşlukları (cm), antet, altbilgi tek panel.
- Designer `index.vue` — yeni/düzenle/kopyala diyaloglarında sayfa yapısı; `diUpdateTemplatePageStructure`.
- `diPageLayout.ts` — twips ↔ cm dönüşümü.
- `DiCollaboraEditor.vue`, `designer/[id]/edit.vue`, placeholder paneli.
- Locale: `documentIntelligence.designer.pageStructure.*`, `documentName` etiketi.
- **Prod deploy:** `Mng.Ui/Dockerfile` — `NODE_OPTIONS=--max-old-space-size=4096` (nuxt generate OOM fix).

## Sonraki Adımlar

- Editör sayfasında sayfa yapısı paneli (metadata dialog dışında) — isteğe bağlı.
- Parametre tanımı UI tamamlama.

## Son Güncelleme

**26 Haziran 2026** — mngui prod deploy edildi (192.168.20.8:3000).
