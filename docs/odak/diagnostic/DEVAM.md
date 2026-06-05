# Diagnostic — devam noktası

**Son güncelleme:** 5 Haziran 2026

## Tamamlanan

- Faz 0 ölçüm araçları + ilk rapor (`DIAGNOSTIC_REPORT_2026-06-02.md`)
- **Faz 1** — Admin workspace tanımları UI (lazy tabs, `useOcWorkspaceCatalog`, paralel person)
- **Faz 1B** — Operasyon workspace explorer + board UI optimizasyonları
- **Odak deploy** — `mngui` (2 Haziran 2026, `http://192.168.20.20:3000`)
- **Faz 2 MO** — profil/pano endpoint + sayfa paketleri (`DIAGNOSTIC_REPORT_2026-06-02-faz2.md`)
- **5 Haz 2026 — Prod müşteri raporu + System dokümanı**
  - `diagnostic-operation-pages.ps1` / `diagnostic-document-intelligence-pages.ps1` → gateway `192.168.20.8` (prod token otomatik)
  - OC: prod helpdesk/feedback seed; board listesi boşsa DG work-item fallback
  - Sonuçlar: `docs/odak/document_intelligence/system/diagnostic-raporu.md` → **Dokümanlar → System → Diagnostic Raporu** (`MonitraNG Users`)
  - Ham JSON: `reports/oc_pages_prod_20260605_final.json`, `reports/di_pages_prod_20260605_final.json`

## Prod koşu özeti (5 Haziran 2026)

| Modül | Senaryo | OK | WARN |
|-------|--------:|---:|-----:|
| Operasyon Merkezi | 10 | 8 | 2 (profil, pano) |
| Dokümanlar | 8 | 5 | 3 (browse/klasör seçimi) |

Workspace: MonitraNG Geri Bildirim · örnek iş `MNG-0001`. DI: `MonitraNG` klasörü + Kullanıcı Rehberi markdown.

## Müşteri / IT rapor akışı

1. Prod gateway ile script'leri çalıştır (`-GatewayBaseUrl http://192.168.20.8:5040`).
2. `diagnostic-raporu.md` içinde **Son koşu** bölümünü güncelle; önceki koşuyu **Önceki koşular** altına taşı.
3. `seed-system-diagnostic-report.ps1` (prod varsayılan).

Metodoloji (sayfa API paketi, cold/warm P95, sınırlamalar) dokümanın üst bölümünde — IT ekibi için.

## Deploy sonrası ölçüm (2 Haz 2026 — test)

| Senaryo | Önce (sabah) | Sonra (UI+MO deploy) |
|---------|--------------|----------------------|
| Scheduled tab (paralel) | ~2,0 sn | ~2,1 sn ✅ |
| profile_view warm P95 | ~2896 ms | ~2023 ms ✅ |
| profile cold (restart sonrası) | ~3942 ms | ~4095 ms ⚠️ (≤4000 hedef) |
| board_list warm P95 | ~322 ms | ~662 ms (değişken) |

Raporlar: `reports/ws_definitions_post_deploy_20260602.json`, `benchmark_post_mo_deploy_20260602.json`  
Not: `ws_definitions` §3 eager storm = **eski eager UI simülasyonu**; canlı kod lazy tab kullanıyor.

## Konuya dönüldüğünde

1. Tarayıcı Network — workspace tanımları / profil / pano waterfall (prod)
2. **Faz 2b MO** — profil warm ~2 sn, pano ~2,6 sn (prod WARN); agregasyon/cache
3. DI — `browse`/`bootstrap` yaygınlaştırma; eski 3-API klasör seçimi kaldırma (UI)
4. (Ops.) JSON → markdown `generate-system-diagnostic-report.ps1`
5. İsteğe bağlı: Faz 0 müşteri baseline sign-off, load test

**Ana referans:** [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md) · [README.md](./README.md)  
**DI System dokümanları:** [../document_intelligence/DEVAM.md](../document_intelligence/DEVAM.md) § DI-SYSTEM  
**Operation Core checkpoint:** [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md) § UI-PERF-F1
