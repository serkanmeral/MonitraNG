# MngDocument — Oturum durumu

## Son Çalışılan Konu

**7 Temmuz 2026 oturumu:** D-META / D-CREATE / D-FILE-PREV prod smoke; performans dilimleri (D-PERF); prod `dm_tags` dataset; döküman lifecycle kararı (Minimal).

**Roadmap:** [DI_PRODUCT_ROADMAP.md §23](../odak/document_intelligence/DI_PRODUCT_ROADMAP.md)

## Tamamlanan (bu oturum)

| ID | Özet | Commit / kanıt |
|----|------|----------------|
| **D-META** | `origin=upload`; Collabora yalnızca yönetilen docx; UI helper | `c5880739` |
| **D-CREATE** | `POST /resources/documents`; kod + antet; `origin=native` | `c5880739` |
| **D-FILE-PREV** | `GET …/preview/pdf` (Gotenberg); upload docx PDF iframe | `c5880739` |
| **D-VERSIONS** | DOCX sürüm geçmişi + restore API | `c5880739` |
| **D-CLONE** | Markdown + yönetilen DOCX klonlama | `c5880739` |
| **D-PERF-1** | Lazy tree (`tree/roots`, `children`, `path`, `search`) | `c5880739` + `dff51a2c` |
| **D-PERF-2** | Permission snapshot cache (domain TTL) | `dff51a2c` |
| **D-PERF-3** | Bootstrap / browse / children pagination | `dff51a2c` |
| **Prod smoke** | 9/9 kabul kriteri | `scripts/tests/MngDocument/smoke-di-meta-create-preview-prod.ps1` |
| **Prod dm_tags** | Eksik dataset oluşturuldu; tag API çalışır | `setup-document-intelligence-datasets.ps1` |
| **Prod deploy** | `mngdocument` + `mngui` | Kullanıcı onayı ile tamamlandı |

## Ürün Kararları (7 Temmuz 2026)

| Konu | Karar |
|------|--------|
| Sayfa / Döküman / Dosya | `origin` + extension ile UI ayrımı |
| Yüklenen docx | **Dosya** — Collabora yok; Gotenberg PDF preview |
| Native docx | **Döküman** — antet seçimi; Collabora edit |
| Döküman lifecycle | **Minimal** — taslak/yayın yalnızca **Sayfa** (markdown); docx için sürüm geçmişi yeterli; geniş lifecycle **Faz M** |
| `documentNo` benzersizliği (#16) | **Domain geneli** — uygulandı (`EnsureDocumentNoUniqueAsync`) |
| Create sonrası Collabora (#17) | **Evet** — `r/[id]` sayfasında otomatik editör açılır |
| Üretim dialog antet | Yok (şablonda `defaultLetterheadId`) |
| Faz D-P | Ertelendi |

## Sıradaki İşler

1. **D-E1–E2** — Collabora oturum sonlandırma + pre-Collabora limit gate
2. **D2** — döküman sürüm UX (backend hazır; UI iyileştirme)
3. **D2–D4** — merge/PDF export, manuel üretim UX
4. **D-BR2** — kapak sayfası kataloğu
5. CoC/Activity uçtan uca smoke · **D-N1**

## Bilinen Notlar

- **PDF preview:** DI minimal docx ile Gotenberg OK; karmaşık harici MS Word docx dönüşümü LibreOffice’te başarısız olabilir (`uno exception`) — beklenen sınırlama.
- **Prod dataset checklist:** `dm_tags` kurulum script’inde 9. dataset; prod’da bir kez atlanmıştı — düzeltildi.
- **Pagination footer:** 51+ kayıtta sayfa numarası; küçük klasörlerde sayaç + sayfa boyutu görünür.

## Ortam

| Ortam | Gateway | Token script |
|-------|---------|--------------|
| **Test** | `192.168.20.20:5040` | `load-operationcore-token.ps1` |
| **Prod** | `192.168.20.8:5040` | `load-operationcore-token-prod.ps1` |

Backend deploy otomatik; UI deploy kullanıcı talebi ile.

## Son Güncelleme

**7 Temmuz 2026 (akşam)** — D-META/CREATE/FILE-PREV prod smoke ✅; Minimal lifecycle kararı; sırada D-E1–E2.

## Nerede Kalmıştık

Prod smoke geçti. Döküman taslak/yayın **Faz M**'e ertelendi (Minimal). **D-E1–E2** implementasyonuna geçilecek.
