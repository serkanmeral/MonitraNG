# LogAlarm parite — yol haritası (özet)

**Durum:** Planlama · MVP sonrası uzun vadeli hedef  
**Referans:** [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md)

MonitraNG SIEM-hafif MVP (U1–U7) tamamlandı. LogAlarm seviyesi parite ayrı bir programdır.

---

## Faz A — Operatör UX (devam ediyor)

| # | Alan | Durum | Not |
|---|------|--------|-----|
| A1 | Olay arama UI | ✅ | `/apps/siem-center/events` |
| A2 | Güvenlik paneli MVP | ✅ | `/apps/siem-center` · [SIEM_DASHBOARD.md](./SIEM_DASHBOARD.md) |
| A3 | Timeline / gelişmiş filtre | ✅ | URL sync, U1–U7 presets, new_flow badge |
| A4 | Özelleştirilebilir dashboard | ✅ | localStorage widget düzeni · `/apps/siem-center` |

## Faz B — Kapsam & parser

| # | Alan | Durum | Not |
|---|------|--------|-----|
| B1 | Parser kütüphanesi genişletme | 🟡 | `linux.auth.v1` ✅ · FortiGate + PAN-OS ✅ · `windows.security.extended.v1` pilot ✅ |
| B2 | WEF tam entegrasyon | ✅ | [SIEM_WEF_WEC_FORWARDER.md](./SIEM_WEF_WEC_FORWARDER.md) |
| **B3 hazır kural paketi (MITRE / ISO)** | ✅ | `siem-mvp-v1` · [SIEM_ALARM_RULE_PACK.md](./SIEM_ALARM_RULE_PACK.md) |

## Faz C — Uyum & arşiv (Türkiye pazarı)

> **Ertelendi:** Kendi SIEM yol haritası (Faz 1–4) tamamlanana kadar kod yok. Bkz. [SIEM_ROADMAP.md §6](./SIEM_ROADMAP.md#6-faz-5--ertelenen-logalarm--uyum).

| # | Alan | Öncelik | Not |
|---|------|---------|-----|
| C1 | 5651 imza / WORM arşiv | P0 | Spike ✅ [SIEM_WORM_5651_SPIKE.md](./SIEM_WORM_5651_SPIKE.md) · C1.1 kod sırada |
| C2 | Denetim raporları | P1 | C1 sonrası |

## Faz D — İleri analitik

| # | Alan | Öncelik |
|---|------|---------|
| D1 | Threat intel enrichment | P2 |
| D2 | AI anomali skoru | P3 · [AI_PLANNING_DECISION.md](../AI_PLANNING_DECISION.md) |
| D3 | Yatay ölçek / HA | P2 |

---

## Kanıtlanmış MonitraNG zinciri (4 Haz 2026)

U1–U7 + workflow + onaylı müdahale Odak E2E suite ile doğrulandı (`test-siem-e2e-suite.ps1 -Quick`).

Parite hedefi bu zinciri genişletir; MVP sprint’inin yerini almaz.
