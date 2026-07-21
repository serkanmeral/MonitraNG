# Uzak (müşteri terminal) Cursor prompt’ları

Bu klasör, **lokal Cursor’da yazılıp** RDP ile müşteri terminalindeki Cursor’a yapıştırılacak prompt metinlerini tutar.

İş akışı: [REMOTE_CURSOR_WORKFLOW.md](../REMOTE_CURSOR_WORKFLOW.md)

| Dosya | Amaç | Durum |
|-------|------|--------|
| [RP01_users_groups_export_odak_test.md](./RP01_users_groups_export_odak_test.md) | Test 20.20 — user + group JSON export | Yapıldı |
| [RP02_mongo_dump_mng_odak_test.md](./RP02_mongo_dump_mng_odak_test.md) | Test 20.20 — `mng_odak` dump (`@users`/`@groups` hariç) | Yapıldı |
| [RP03_di_templates_letterhead_cover_export_test.md](./RP03_di_templates_letterhead_cover_export_test.md) | Test 20.20 — DI şablon + letterhead + cover pack | Hazır |

**Kural:** Prompt’ta parola yok; çıktı paketi git’e girmez.
