# SIEM performans benchmark sonuçları

**Durum:** ✅ **P0 + P1 Odak baseline, soak, syslog, queue depth** (4 Haz 2026)

## Scriptler

```powershell
# Kısa baseline + detection lag
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -IncludeDetectionLag

# P0 soak kapısı (5 dk, 50 evt/s hedef, batch=5)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -Soak

# P1 lab profil (2 dk, 100 evt/s hedef, batch=10)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -P1

# P2 soak (5 dk, 150 evt/s hedef, batch=10) — lab kapısı
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -P2

# SIEM parser unit gate (CI ile aynı filtre)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\test-siem-unit-gate.ps1

# Yerel CI kapisi (unit + benchmark JSON — Odak yok)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\run-siem-local-gate.ps1

# Benchmark baseline JSON dogrulama (CI adimi)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\ci\verify-siem-benchmark-baselines.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-engine-syslog.ps1

# Engine sec_event.queue_depth under load (SLO: max < 80% MaxItems)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-engine-queue-depth.ps1
```

## Profiller

| Profil | Açıklama | Kaynak |
|--------|----------|--------|
| P0 | Deny-only firewall + dar AD (4625/4740) | [SIEM_PERFORMANCE_PLAN §3](./SIEM_PERFORMANCE_PLAN.md) |
| P1 | Tam MVP hacim tahmini | Aynı |
| engine-syslog | UDP :5514 → Engine queue → flush → Reactor | Engine S3/S4 |
| engine-queue-depth | UDP yük altında kuyruk derinliği (manual flush yok) | [SIEM_PERFORMANCE_PLAN §3.1](./SIEM_PERFORMANCE_PLAN.md) |

## Dosya adlandırma

```text
benchmark-{profile}-{date}.json
benchmark-engine-syslog-{date}.json
benchmark-engine-queue-depth-{date}.json
```

## P0 Odak özeti (2026-06-04)

### Kısa baseline (`benchmark-P0-2026-06-04.json`)

| Metrik | Değer | Hedef |
|--------|-------|-------|
| achievedEps | ~15 | — |
| ingest P95 | ~6 ms | < 1000 ms ✅ |
| U1 detection lag | ~1.7 s | < 60 s ✅ |

### Soak kapısı (`benchmark-soak-2026-06-04.json`)

| Metrik | Değer | Hedef (P0 kapı) |
|--------|-------|-----------------|
| süre | 300 s | 5 dk ✅ |
| achievedEps | **41.25** | ≥ 40 (%80×50) ✅ |
| ingest P95 | **7 ms** | < 1000 ms ✅ |
| dropRate | **0** | < 5% ✅ |
| events | 12 420 | — |
| mongo savedDelta | 12 420 | = accepted ✅ |

## P1 Odak özeti (2026-06-04)

`benchmark-P1-2026-06-04.json`

| Metrik | Değer | Hedef |
|--------|-------|-------|
| süre | 120 s | 2 dk ✅ |
| achievedEps | **77.75** | ≥ 50 (%50×100) ✅ |
| ingest P95 | **7 ms** | < 1000 ms ✅ |
| dropRate | **0** | < 5% ✅ |
| events | 9 430 | — |
| mongo savedDelta | 9 430 | = accepted ✅ |

## Engine syslog özeti (2026-06-04)

`benchmark-engine-syslog-2026-06-04.json`

| Metrik | Değer | Hedef |
|--------|-------|-------|
| süre | 60 s | — |
| targetEps | 30 | — |
| achievedEps | **17.79** | ≥ 15 (%50×30) ✅ |
| syslog sent | 1 150 | — |
| flush accepted | 1 146 | — |
| flush P95 | 377 ms | — |
| mongo savedDelta | 1 150 | ~ sent ✅ |

## Engine queue depth özeti (2026-06-04)

`benchmark-engine-queue-depth-2026-06-04.json`

| Metrik | Değer | Hedef |
|--------|-------|-------|
| süre | 45 s | — |
| targetEps | 80 | — |
| achievedEps | **57.63** | — |
| queue max | **107** | < 4000 (%80×5000) ✅ |
| queue P95 | **94** | — |
| aboveGatePct | **0%** | — |
| mongo savedDelta | 2 874 | = sent ✅ |

## E2E suite

```powershell
# Hızlı (Faz1 + benchmark atlanır; kuyruk purge dahil)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\test-siem-e2e-suite.ps1 -Quick

# Tam (Faz1 + kısa P0 baseline dahil)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\test-siem-e2e-suite.ps1
```

**Not:** Benchmark sonrası E2E koşmadan önce kuyruk purge gerekebilir; suite bunu otomatik yapar.

## Ölçüm checklist

- [x] Reactor `sec_events` insert throughput (HTTP ingest)
- [x] U1 correlation lag (Faz 2)
- [x] P0 5 dk / 50 evt/s soak kapısı
- [x] P1 2 dk / 100 evt/s lab profil
- [x] Engine syslog UDP :5514 → batch flush
- [x] `sec_event.queue_depth` under load
