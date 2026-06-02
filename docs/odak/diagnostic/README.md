# Diagnostic (Odak)

Backend servislerinin **response time** ölçümü ve raporlama. UI performans paketi (Faz 1 + 1B) Odak’ta deploy edildi; deploy sonrası ölçüm ve Faz 2 backend **ertelendi**.

**Durum:** Faz 1 + 1B ✅ (UI deploy Odak, 2 Haz 2026) · Faz 2 backend bekliyor  
**Devam noktası:** [DEVAM.md](./DEVAM.md)  
**Plan:** [DIAGNOSTIC_PLAN.md](./DIAGNOSTIC_PLAN.md)  
**Yol haritası (müşteri):** [PERFORMANCE_ROADMAP.md](./PERFORMANCE_ROADMAP.md)

---

## Kilitli kararlar

| Konu | Karar |
|------|-------|
| Ortam | **Odak sunucusu** (`192.168.20.20`) — lokal backend yok |
| Kapsam | **Backend only** — UI E2E sonraki faz |
| Metrik | **Warm P95** (ana SLA) + **session cold** (ilk istek) + medyan/min/max |
| Hedef | Warm P95 ≤ **3000 ms**, session cold ≤ **4000 ms** (runtime); reference ≤ 100 ms |

---

## Scriptler

| Script | Açıklama |
|--------|----------|
| [scripts/diagnostic-benchmark.ps1](./scripts/diagnostic-benchmark.ps1) | MO P0 endpoint + DG/Keeper referans ölçümü |
| [scripts/diagnostic-workspace-definitions.ps1](./scripts/diagnostic-workspace-definitions.ps1) | Workspace definitions / scheduled tab DG yükü + eager storm simülasyonu |
| [DIAGNOSTIC_REPORT_2026-06-02.md](./DIAGNOSTIC_REPORT_2026-06-02.md) | İlk ölçüm raporu |
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
