# SIEM performans benchmark sonuçları

**Durum:** ⬜ Henüz ölçüm yok — Faz 1 spike sonrası doldurulacak

## Profiller

| Profil | Açıklama | Kaynak |
|--------|----------|--------|
| P0 | Deny-only firewall + dar AD (4625/4740) | [SIEM_PERFORMANCE_PLAN §3](./SIEM_PERFORMANCE_PLAN.md) |
| P1 | Tam MVP hacim tahmini | Aynı |

## Dosya adlandırma

```text
benchmark-{profile}-{date}.json
```

Örnek alanlar: `eps_ingest`, `queue_depth_p95`, `parse_duration_ms_p95`, `detection_lag_p95`.

## İlk ölçüm checklist

- [ ] Engine syslog UDP :514 → batch size
- [ ] Reactor `sec_events` insert throughput
- [ ] `sec_event.queue_depth` under load
- [ ] U1 correlation lag (Faz 2)
