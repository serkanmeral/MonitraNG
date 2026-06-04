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
| A3 | Timeline / gelişmiş filtre | ⬜ | Tam metin, kaydedilmiş sorgular |
| A4 | Özelleştirilebilir dashboard | ⬜ | Widget düzeni |

## Faz B — Kapsam & parser

| # | Alan | Öncelik |
|---|------|---------|
| B1 | Parser kütüphanesi genişletme | P1 · [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md) |
| B2 | WEF tam entegrasyon | P1 |
| B3 | Hazır kural paketi (MITRE / ISO) | P2 |

## Faz C — Uyum & arşiv (Türkiye pazarı)

| # | Alan | Öncelik |
|---|------|---------|
| C1 | 5651 imza / WORM arşiv | P0 |
| C2 | Denetim raporları | P1 |

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
