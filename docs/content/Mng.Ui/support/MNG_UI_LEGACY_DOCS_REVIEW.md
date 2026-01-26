# docs/Mng.Ui — Taşıma Özeti

**Tarih:** 26 Ocak 2026  
**Amaç:** `docs/Mng.Ui/` (content dışı) altındaki dokümanları `docs/content/Mng.Ui/` yapısına taşımak.

---

## Yapılan taşımalar

| Kaynak (legacy) | Hedef (content) |
|-----------------|------------------|
| architecture/* | support/architecture/ |
| current_status.md, domain-menu-item.md | support/guides/ |
| guides/AUTOMATED_FORMS_USAGE.md, HUB_TEST_GUIDE.md, HUB_TROUBLESHOOTING.md | support/guides/ |
| guides/templates/* | support/guides/templates/ |
| i18n/*.md | support/guides/i18n/ |
| specs/*.md, specs/datasets/*, locale-editor-menu-item.json | support/specs/ |
| tst_book_data/* | support/guides/tst_book_data/ |

Zaten content’te olan ve **tekrar taşınmayan** alanlar: `guides/DOCKER_DEPLOYMENT.md`, `guides/GATEWAY_INTEGRATION.md`, `guides/I18N_GUIDE.md`, `guides/chatbot/` (datasets, field-types, indexes, validations, examples). Bunlar `content/Mng.Ui/guides/` altında kaldı.

## Legacy temizliği

- `docs/Mng.Ui/` içindeki tüm dosya ve klasörler silindi.
- Klasörde yalnızca “içerik content/Mng.Ui/ altına taşındı” diyen `README.md` bırakıldı.

## Nav

MkDocs nav’da Mng.Ui altına **Support** bölümü eklendi: Current Status, Hub Test Guide, Hub Troubleshooting, Automated Forms Usage, Domain Menu Item, UI Guide Template, Architecture (2 dosya), I18n support (Summary, Roadmap), Specs (Dashboard Builder, Widget Library, Dataset UI Design).
