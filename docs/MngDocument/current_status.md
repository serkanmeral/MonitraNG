# MngDocument — Oturum durumu

## Son Çalışılan Konu

**7 Temmuz 2026 (gece):** Faz **D-E** (editör oturum/kilitleme) + **D2** (döküman sürüm UX — Collabora kayıt, changeNote, kapatma akışı).

**Roadmap:** [DI_PRODUCT_ROADMAP.md §25](../odak/document_intelligence/DI_PRODUCT_ROADMAP.md) · Checkpoint: [DEVAM.md](../odak/document_intelligence/DEVAM.md)

## Tamamlanan (bu oturum)

| ID | Özet |
|----|------|
| **D-E1** | `POST …/editor-sessions/{token}/end`, idle timeout, `GetStats`, WOPI `Touch` |
| **D-E2** | Pre-Collabora limit gate (429), `EditorLimitsSettings` |
| **D-E3** | `DiEditorSessionsPanel` — chip, modal, yenile, poll, broadcast, yeni sekme editör |
| **D-E-LOCK** | `editor-lock-status`; uyarı + sert kilit; aynı kullanıcı çift sekme; manager bypass |
| **D2-UX** | Sürüm takibi, `DiSaveVersionNoteDialog`, editör toolbar (`vN`, Geçmiş) |
| **D2-API** | `PATCH …/versions/{n}` changeNote |
| **D2-CLOSE** | Kapatırken kaydet → sürüm notu modalı (`useDiEditorCloseGuard`) |
| **D2-WOPI** | `PostMessageOrigin` — Collabora modified/save postMessage |
| **D2-PAGE** | `r/[id].vue` DOCX detay + `?edit=1`; `editor/resource/[id].vue` |

**Deploy:** `mngdocument` + `mngui` @ test `192.168.20.20` (7 Tem gece, commit sonrası).

## Sıradaki İşler

1. **D4** — manuel şablondan üretim UX, merge + PDF indirme
2. **D-BR2** — kapak sayfası kataloğu
3. **CoC/Activity** uçtan uca smoke
4. **D-N1** — `document.generated` bildirim maili
5. Non-admin izin filtreleme canlı doğrulaması (DI-PERM açık borç)
6. **D2 P1** — Collabora PDF export (opsiyonel)

## Önemli Notlar

- Oturum sayacı **MngDocument in-memory WOPI store** — Collabora websocket değil.
- Kapat-kaydet akışında `checkVersionAfterSave` eşzamanlı çağrılar **tek promise** paylaşır (race fix).
- Editör yeniden açıldığında yeni WOPI oturumu + `PostMessageOrigin` gerekir.
- Prod deploy henüz yapılmadı — test doğrulama sonrası isteğe bağlı.

## Ortam

| Ortam | Gateway |
|-------|---------|
| **Test** | `192.168.20.20:5040` |
| **Prod** | `192.168.20.8:5040` |

## Son Güncelleme

**7 Temmuz 2026 (gece)** — D-E + D2 tamamlandı; sırada **D4** (manuel üretim / merge).

## Nerede Kalmıştık

D2 kapatıldı (sürüm UX + kapatma not akışı). Sonraki odak: **D4** — şablondan manuel döküman üretimi, parametre formu, merge ve PDF indirme kanalı.
