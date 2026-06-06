# Diagnostic — devam noktası

**Son güncelleme:** 6 Haziran 2026

## ⭐ Son durum — OC performans paketi (6 Haziran 2026)

**Paket:** PV-PERF-4 + Faz 2b (dashboard query dedup) + metadata TTL 600s + UI profil cache  
**Deploy:** Test `20.20` + Prod `20.8` — `mngoperations` + `mngui` (`--no-cache`) · `oc_live=200`  
**Rapor:** [DIAGNOSTIC_REPORT_2026-06-06-perf.md](./DIAGNOSTIC_REPORT_2026-06-06-perf.md)

| Ortam | profile_open warm P95 | Durum |
|-------|----------------------|--------|
| **Prod** `20.8` | **1694 ms** (önce 2377) | ✅ ≤ 1800 |
| Test `20.20` | 2963 ms | ⚠️ |

Ham JSON: `reports/oc_pages_prod_post_perf_20260606.json`, `reports/oc_pages_test_post_perf_20260606.json`

**Sıradaki planlama:** Pano ≤ 1,2 sn · profil cold · Faz 3 DG katalog cache (ayrı oturum).

---

## Tamamlanan (kronoloji)

- Faz 0 ölçüm araçları + ilk rapor (`DIAGNOSTIC_REPORT_2026-06-02.md`)
- **Faz 1** — Admin workspace tanımları UI (lazy tabs, `useOcWorkspaceCatalog`, paralel person)
- **Faz 1B** — Operasyon workspace explorer + board UI optimizasyonları
- **Faz 2 MO** — profil/pano endpoint + sayfa paketleri (`DIAGNOSTIC_REPORT_2026-06-02-faz2.md`)
- **5 Haz 2026** — Prod müşteri raporu + System dokümanı (`oc_pages_prod_20260605_final.json`)
- **6 Haz 2026** — **OC-PERF-F2b paketi** deploy + ölçüm (bu dosya §⭐)

---

## Prod koşu özeti (5 Haziran 2026 — önceki)

| Modül | Senaryo | OK | WARN |
|-------|--------:|---:|-----:|
| Operasyon Merkezi | 10 | 8 | 2 (profil, pano) |

5 Haziran sonrası 6 Haziran deploy ile **prod profil WARN → OK** (warm P95 1694 ms).

---

## Müşteri / IT rapor akışı

1. Prod gateway ile script'leri çalıştır (`-GatewayBaseUrl http://192.168.20.8:5040`).
2. `diagnostic-raporu.md` içinde **Son koşu** bölümünü güncelle.
3. `seed-system-diagnostic-report.ps1` (prod varsayılan).

---

## Konuya dönüldüğünde

1. [DIAGNOSTIC_REPORT_2026-06-06-perf.md](./DIAGNOSTIC_REPORT_2026-06-06-perf.md) §5 açık kalemler
2. Yeni planlama oturumu (kullanıcı yönlendirmesi)
3. İsteğe bağlı: tarayıcı waterfall · `OC_PERF` regresyon kapısı

**Ana referans:** [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md) · [README.md](./README.md)  
**Operation Core checkpoint:** [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md) § OC-PERF-F2b
