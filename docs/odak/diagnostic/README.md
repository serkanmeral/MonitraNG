# Diagnostic (Odak)

Backend servislerinin **response time** ölçümü ve raporlama. OC performans paketi (Faz 1 + 1B + 2 + 2b) test + prod ortamında canlı; prod profil warm P95 **1694 ms** ✅.

**Durum:** Faz 1 + 1B ✅ · Faz 2 MO ✅ · **Faz 2b + PV-PERF-4 + UI cache** ✅ (6 Haz 2026)  
**Devam noktası:** [DEVAM.md](./DEVAM.md)  
**OC checkpoint (kod tabloları):** [../operationcore/mngoperations/DEVAM.md](../operationcore/mngoperations/DEVAM.md) § OC-PERF-F2b  
**Plan:** [DIAGNOSTIC_PLAN.md](./DIAGNOSTIC_PLAN.md)  
**Yol haritası (müşteri):** [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md)

---

## Kilitli kararlar

| Konu | Karar |
|------|-------|
| Ortam | **Test:** `192.168.20.20` · **Müşteri raporu (prod):** `192.168.20.8` — gateway `192.168.20.8` → script otomatik prod token |
| Müşteri çıktısı | **Dokümanlar → System → Diagnostic Raporu** (`MonitraNG Users`); kaynak: `docs/odak/document_intelligence/system/diagnostic-raporu.md` |
| Kapsam | **Backend only** — UI E2E planı: [../testprocs/README.md](../testprocs/README.md) |
| Metrik | **Warm P95** (ana SLA) + **session cold** (ilk istek) + medyan/min/max |
| Hedef | Warm P95 ≤ **3000 ms**, session cold ≤ **4000 ms** (runtime); reference ≤ 100 ms |

---

## Scriptler

| Script | Açıklama |
|--------|----------|
| [scripts/diagnostic-benchmark.ps1](./scripts/diagnostic-benchmark.ps1) | MO P0 endpoint + DG/Keeper referans ölçümü |
| [scripts/diagnostic-workspace-definitions.ps1](./scripts/diagnostic-workspace-definitions.ps1) | Workspace definitions / scheduled tab DG yükü + eager storm simülasyonu |
| [scripts/diagnostic-operation-pages.ps1](./scripts/diagnostic-operation-pages.ps1) | **Operasyon Core sayfaları** — explorer, board, profil, pano, bildirim, admin (API wall-clock) |
| [scripts/diagnostic-document-intelligence-pages.ps1](./scripts/diagnostic-document-intelligence-pages.ps1) | **Document Intelligence** — resources tree/children/klasör/markdown/arama (MngDocument API) |
| [DIAGNOSTIC_REPORT_2026-06-02.md](./DIAGNOSTIC_REPORT_2026-06-02.md) | İlk ölçüm raporu |
| [DIAGNOSTIC_REPORT_2026-06-02-faz2.md](./DIAGNOSTIC_REPORT_2026-06-02-faz2.md) | Faz 2 MO sonrası endpoint + sayfa paketleri |
| [DIAGNOSTIC_REPORT_2026-06-06-perf.md](./DIAGNOSTIC_REPORT_2026-06-06-perf.md) | **Faz 2b paketi** — prod profil SLA, deploy + ölçüm |
| [OPERATIONAL_WORKSPACE_PERF.md](./OPERATIONAL_WORKSPACE_PERF.md) | Operasyon alanı analizi + Faz 1B |
| [../operationcore/scripts/load-operationcore-token.ps1](../operationcore/scripts/load-operationcore-token.ps1) | Token (paylaşımlı) |
| [../operationcore/scripts/operationcore-demo-seed.json](../operationcore/scripts/operationcore-demo-seed.json) | workspace/board/dashboard id'leri |

### Hızlı başlangıç

```powershell
# Repo kökünden
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1

# Gateway vs doğrudan MO portu (5086)
.\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1 -CompareDirect

# JSON çıktı yolu
.\docs\odak\diagnostic\scripts\diagnostic-benchmark.ps1 -OutputJson .\docs\odak\diagnostic\reports\run1.json

# Operasyon sayfaları (UI route bazlı API paketleri)
.\docs\odak\diagnostic\scripts\diagnostic-operation-pages.ps1
.\docs\odak\diagnostic\scripts\diagnostic-workspace-definitions.ps1

# Üretim — müşteri raporu (prod token otomatik)
.\docs\odak\diagnostic\scripts\diagnostic-operation-pages.ps1 -GatewayBaseUrl "http://192.168.20.8:5040" -OutputJson .\docs\odak\diagnostic\reports\oc_pages_prod.json
.\docs\odak\diagnostic\scripts\diagnostic-document-intelligence-pages.ps1 -GatewayBaseUrl "http://192.168.20.8:5040" -OutputJson .\docs\odak\diagnostic\reports\di_pages_prod.json
.\docs\odak\document_intelligence\scripts\seed-system-diagnostic-report.ps1
```

### OC_PERF (DG/Keeper kırılımı)

Script istemci tarafı süre ölçer. Downstream breakdown için Odak'ta:

1. `ApplicationResources/mng_apps/docker-compose.odak.yml` → `MngOperationsSettings__PerfDiagnostics=true`
2. Deploy `mngoperations`
3. Benchmark veya smoke çalıştır
4. `docker logs --since 5m mngoperations | grep OC_PERF`
5. Ölçüm bitince bayrağı tekrar `false`

---

## Raporlar

JSON çıktılar: [reports/](./reports/) (gitignore önerilir — ham ölçüm verisi)

Markdown rapor şablonu: `DIAGNOSTIC_PLAN.md` §9
