# Alarm & Rule Engine (Odak)

MonitraNG'nin platform geneli **tespit / alarm üretim katmanı**. Major Roadmap §4.2'nin somutlaştırılması. Metrik / olay / AI sinyali akışlarını tüketir, kuralları değerlendirir, **alarm üretir**. Aksiyon almaz — o Workflow Engine'in işidir.

**Durum:** Planlama + Faz 0–2 motor ✅ · **Alarm Merkezi UI** ✅ — §15 kararlar kapalı · **Agent observation + Flow Lab işletimi** ✅ (8 Ağu 2026)

---

## UI (Alarm Merkezi)

| Sayfa | Route | Patch |
|-------|--------|--------|
| Açık alarmlar | `/apps/alarm-center/alarms` | [scripts/patch-alarm-center-side-menu.ps1](./scripts/patch-alarm-center-side-menu.ps1) |
| Alarm kuralları | `/apps/alarm-center/rules` | aynı |

Handoff: [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md)

**SIEM operasyonel kurallar:** [../monitoring/SIEM_ALARM_RULE_PACK.md](../monitoring/SIEM_ALARM_RULE_PACK.md) · purge: `scripts/odak/purge-siem-e2e-alarm-rules.ps1`

---

## Kapsam kararı (kilitli)

| Konu | Karar |
|------|-------|
| **Konumlandırma** | Genel Alarm & Rule Engine (major §4.2) — SIEM korelasyonu yalnızca bir kural ailesi |
| **Sınır** | Tespit/alarm üretir; orkestrasyon/aksiyon Workflow Engine'e ait (`planing.md` §2) |
| **AI** | Ayrı scorer servis(ler); çıktı = sinyal event → motoru besler |
| **IFTTT** | `MONITORING_WORKFLOW.md` bölünür (tespit→Alarm Engine, aksiyon→Workflow); superseded |

---

## Dokümanlar

| Dosya | İçerik | Durum |
|-------|--------|--------|
| [DEVAM.md](./DEVAM.md) | Kaldığımız yer, kilitli §15 kararlar, Faz 0/1 checklist | Güncel |
| [ALARM_RULE_ENGINE_PLAN.md](./ALARM_RULE_ENGINE_PLAN.md) | Ana plan: mimari, kural/alarm modeli, fazlar | Güncel (§15 kapalı) |
| [ALARM_NOTIFICATION_POLICIES.md](./ALARM_NOTIFICATION_POLICIES.md) | Bildirim politikaları (çoklu kullanıcı, kanallar, dispatch) | Kararlandı |
| [SCENARIO_STUDIO_SIMPLE_SOURCE.md](./SCENARIO_STUDIO_SIMPLE_SOURCE.md) | Scenario Studio / Flow Lab basit olay kaynağı UX + managed node’lar | Güncel (8 Ağu 2026) |
| [AGENT_OBSERVATION_AND_FLOW_LAB.md](./AGENT_OBSERVATION_AND_FLOW_LAB.md) | Collector → `monitra.observations`, paket key, Açık/Kapalı, birleştirme | Güncel (8 Ağu 2026) |
| [FLOW_MIGRATION_QUEUE.md](./FLOW_MIGRATION_QUEUE.md) | Legacy kural → Flow Lab geçiş kuyruğu (Odak) | Güncel |
| [../siem/current_status.md](../siem/current_status.md) | SIEM oturum checkpoint (nerede kaldık) | Güncel |

---

## İlişkili dokümanlar

| Konu | Konum |
|------|-------|
| Major Roadmap (vizyon) | `docs/odak/operationcore/major_plan.md` §4.2 |
| Workflow Engine (orkestrasyon) | `docs/odak/workflow/Workflow Backend Implementation Plan v1.md` §12 |
| SIEM-hafif (korelasyon kural ailesi) | `docs/odak/monitoring/SIEM_PLANNING.md` §7, §12.1 |
| mon_metrics veri modeli | `docs/content/monitoring_plans/MONITORING_DATA_PRODUCTION.md` |
| IFTTT (superseded) | `docs/content/monitoring_plans/MONITORING_WORKFLOW.md` |
