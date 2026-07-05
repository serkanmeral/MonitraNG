# MngDocument — Oturum durumu

## Son Çalışılan Konu

**D-BR1 — Paylaşımlı antet kataloğu (Sprint A tamamlandı):** Antet CRUD, Collabora tasarım oturumu, header/footer skeleton modeli (tablo tabanlı footer), üretimde design DOCX merge.

**Migration rehberi (prod):** [LETTERHEAD_CATALOG_MIGRATION_PROD.md](../odak/document_intelligence/LETTERHEAD_CATALOG_MIGRATION_PROD.md)

## Tamamlanan İşler (5–6 Tem 2026 oturumu)

### Antet kataloğu (D-BR1 — backend + UI + Odak test)
- **`dm_letterheads`** dataset, `LetterheadsController`, `LetterheadService`, `LetterheadEditorService` (WOPI).
- **`LetterheadDesignSkeletonBuilder`** — modal ayarlarından header şablonu + boş footer tablosu (satır×sütun).
- **Collabora tasarım:** Header/footer tasarım DOCX içinde düzenlenir; WOPI okumada programmatic overlay yok (margin + eksik parça garantisi).
- **`LetterheadDesignMerger`** — üretimde design header/footer merge; `HasDesignHeader`, `HasFooterTableStructure`.
- **Footer ayarları:** `LetterheadFooterSettingsDto` (`enabled`, `tableRows`, `tableColumns`); Odak legacy boolean → `legacyOdakFooter` migrasyonu.
- **UI:** `DiLetterheadCatalogForm` (tablo boyutu seçici), antet listesi/tasarım sayfaları, `DiLetterheadDesignFooterSummary`.
- **Seed:** `seed-letterheads-odak.ps1`, `seed-letterheads-odak.json` (ODK-STD, ODK-MINIMAL).
- **Onarım script:** `regenerate-letterhead-design.ps1` (bozuk tasarım DOCX sıfırlama).
- **Odak test deploy:** `192.168.20.20` — ODK Test Antet 1 header + boş 2×2 footer tablosu doğrulandı.

### Önceki oturumlar (özet)
- Kalem belgeleri (CoC + Activity), generation profilleri, unpublish API, LINE-ACTIVITY-STD şablonu.
- DI_PRODUCT_ROADMAP birleşik faz planı (D-BR, D-WF, D-P, …).

## Devam Eden / Sıradaki

| Öncelik | Konu | Faz |
|---------|------|-----|
| P0 | **Prod migration** — dataset + seed + deploy + antet tasarım regen | D-BR1 |
| P1 | Üretim dialogunda antet seçimi (şablondan farklı antet) | D-BR1 |
| P1 | Parametre yönetimi UI (placeholder envanteri, context binding) | D-P |
| P2 | Sprint B — `dm_document_context_types` (C# catalog → dataset) | Generic platform |
| P2 | Sprint C — `poDocNo` decouple, footer plugin | Generic platform |
| P3 | Kapak sayfası kataloğu | D-BR2 |
| P3 | Collabora oturum limitleri | D-E |

## Önemli Notlar

- **Footer modeli:** Platform varsayılanı = tablo boyutu + Collabora'da doldurma. Odak kurumsal içerik yalnızca seed/migrasyon (`legacyOdakFooter`, `LegacyOdakFooterEnabled`).
- **WOPI:** Collabora `:9980`, WOPI host internal `mngdocument:5095`; gateway'de `/wopi` route yok.
- **Test:** `http://192.168.20.20:5040` · Token: `docs/odak/operationcore/scripts/load-operationcore-token.ps1`
- **Deploy test:** `sync-odak-source.ps1 -Paths MngDocument,Mng.Ui` → `deploy-odak-apps.ps1 -Services mngdocument,mngui -NoCache`

## Son Güncelleme

**6 Temmuz 2026** — D-BR1 Sprint A (antet katalog + tablo footer skeleton + Collabora tasarım) tamamlandı; Odak test doğrulandı. Prod kurulum için migration dokümanı hazır.

## Nerede Kalmıştık

Antet katalog MVP hazır. Yarın: **prod migration** uygulaması veya **üretim dialog antet seçimi** + **Sprint B (context catalog dataset)** ile devam.
