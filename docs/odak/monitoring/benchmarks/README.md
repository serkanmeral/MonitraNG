# SIEM performans benchmark sonuçları

**Durum:** ✅ **P0 Odak baseline + soak** (4 Haz 2026)

## Script

```powershell
# Kısa baseline + detection lag
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -IncludeDetectionLag

# P0 soak kapısı (5 dk, 50 evt/s hedef, batch=5)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -Soak
```

## Profiller

| Profil | Açıklama | Kaynak |
|--------|----------|--------|
| P0 | Deny-only firewall + dar AD (4625/4740) | [SIEM_PERFORMANCE_PLAN §3](./SIEM_PERFORMANCE_PLAN.md) |
| P1 | Tam MVP hacim tahmini | Aynı |

## Dosya adlandırma

```text
benchmark-{profile}-{date}.json
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

## İlk ölçüm checklist

- [x] Reactor `sec_events` insert throughput (HTTP ingest)
- [x] U1 correlation lag (Faz 2)
- [x] P0 5 dk / 50 evt/s soak kapısı
- [ ] Engine syslog UDP :514 → batch size
- [ ] `sec_event.queue_depth` under load
