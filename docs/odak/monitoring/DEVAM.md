# DEVAM — SIEM-Hafif Planlama (Kaldığımız Yer)

**Son güncelleme:** 4 Haziran 2026  
**Durum:** ✅ **SIEM Faz 1 tamam** · ✅ **Faz 2 observation köprüsü (U1)** · Workflow Event Trigger mevcut

**Handoff (yeni chat):** [HANDOFF.md](./HANDOFF.md)

---

## 1. Tek cümlede durum

Faz 0 planlama ✅. **SIEM MVP senaryoları (U1/U2/U4) ✅.** **P0 performans baseline ✅** (`benchmark-siem-p0-baseline.ps1`).

---

## 3. MngReactor / MngEngine durumu (4 Haz 2026)

| Konu | Durum |
|------|--------|
| SIEM `sec_events` ingest | ✅ PR-1…PR-6 |
| Odak deploy Engine | ✅ `mngengine:latest` :5037, syslog :5514 |
| Odak deploy Reactor | ✅ `mngreactor:latest` |
| Workflow `mqtt/publish` | ✅ P4 E2E `reactor_mqtt` |
| **sec_events → observation** | ✅ `SecEventObservationMapper` + `PublishSecEventAsync` |
| **Alarm U1 correlation** | ✅ Odak E2E |
| **U1 → alarm.raised → Workflow** | ✅ `test-siem-u1-workflow-e2e.ps1` |
| **U1 → approval → block.ip** | ✅ `test-siem-u1-approval-block-e2e.ps1` |
| **U4 firewall deny spike** | ✅ `test-siem-u4-alarm-e2e.ps1` |
| **U2 fail→success sequence** | ✅ `test-siem-u2-alarm-e2e.ps1` |
| **P0 benchmark baseline** | ✅ `benchmark-P0-2026-06-04.json` |
| Observation native publish (metrik) | ✅ C6 |

---

## 6. Sıradaki adımlar

1. ~~SIEM PR-1…PR-6~~ ✅
2. ~~Engine Odak + syslog S3~~ ✅
3. ~~Reactor mqtt/publish + P4 E2E~~ ✅
4. ~~Faz 2: sec_events observation map + U1 alarm~~ ✅
5. ~~U1 → `alarm.raised` → Workflow~~ ✅
6. ~~U1 → approval → `block.ip`~~ ✅
7. ~~U4 firewall deny spike~~ ✅
8. ~~U2 sequence~~ ✅
9. ~~Benchmark baseline (P0)~~ ✅
10. P0 5 dk soak (50 evt/s kapı) · U4 → workflow (opsiyonel)

---

## 7. İlgili DEVAM dosyaları

- [workflow/DEVAM.md](../workflow/DEVAM.md) — P4 engine.command
- [alarm/DEVAM.md](../alarm/DEVAM.md) — correlation + observation consumer
- [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) — ✅ implementasyon
