# SIEM performans benchmark sonuçları

**Durum:** ✅ **P0 Odak baseline** (4 Haz 2026) — `benchmark-P0-2026-06-04.json`

## Profiller

| Profil | Açıklama | Kaynak |
|--------|----------|--------|
| P0 | Deny-only firewall + dar AD (4625/4740) | [SIEM_PERFORMANCE_PLAN §3](./SIEM_PERFORMANCE_PLAN.md) |
| P1 | Tam MVP hacim tahmini | Aynı |

## Script

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\benchmark-siem-p0-baseline.ps1 -IncludeDetectionLag
```

## Dosya adlandırma

```text
benchmark-{profile}-{date}.json
```

## P0 Odak özeti (2026-06-04)

| Metrik | Değer | Hedef (P0 kapı) |
|--------|-------|-----------------|
| achievedEps | ~15 | 50 (5 dk soak — henüz ölçülmedi) |
| ingest P95 | ~6 ms | < 1000 ms ✅ |
| errorRate | 0 | < 5% ✅ |
| U1 detection lag | ~1.7 s | < 60 s ✅ |
| mongo savedDelta | = accepted | — |

## İlk ölçüm checklist

- [x] Reactor `sec_events` insert throughput (HTTP ingest)
- [x] U1 correlation lag (Faz 2)
- [ ] Engine syslog UDP :514 → batch size
- [ ] `sec_event.queue_depth` under load
- [ ] P0 5 dk / 50 evt/s soak kapısı
