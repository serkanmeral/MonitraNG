# Diagnostic — devam noktası

**Son güncelleme:** 2 Haziran 2026

## Tamamlanan

- Faz 0 ölçüm araçları + ilk rapor (`DIAGNOSTIC_REPORT_2026-06-02.md`)
- **Faz 1** — Admin workspace tanımları UI (lazy tabs, `useOcWorkspaceCatalog`, paralel person)
- **Faz 1B** — Operasyon workspace explorer + board UI optimizasyonları
- **Odak deploy** — `mngui` (2 Haziran 2026, `http://192.168.20.20:3000`)

## Deploy sonrası ölçüm (2 Haz 2026)

| Senaryo | Önce (sabah) | Sonra (UI+MO deploy) |
|---------|--------------|----------------------|
| Scheduled tab (paralel) | ~2,0 sn | ~2,1 sn ✅ |
| profile_view warm P95 | ~2896 ms | ~2023 ms ✅ |
| profile cold (restart sonrası) | ~3942 ms | ~4095 ms ⚠️ (≤4000 hedef) |
| board_list warm P95 | ~322 ms | ~662 ms (değişken) |

Raporlar: `reports/ws_definitions_post_deploy_20260602.json`, `benchmark_post_mo_deploy_20260602.json`  
Not: `ws_definitions` §3 eager storm = **eski eager UI simülasyonu**; canlı kod lazy tab kullanıyor.

## Konuya dönüldüğünde

1. Tarayıcı Network — workspace tanımları ilk açılış (lazy tab gerçek davranış)
2. **Faz 2** — MO profil cold (~4 sn), dashboard (~2 sn), metadata cache
3. İsteğe bağlı: Faz 0 müşteri baseline, Faz 4 sign-off

**Ana referans:** [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md)  
**Operation Core checkpoint (kod + deploy):** [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md) § UI-PERF-F1
