# Alarm & Rule Engine (Odak)

MonitraNG'nin platform geneli **tespit / alarm üretim katmanı**. Major Roadmap §4.2'nin somutlaştırılması. Metrik / olay / AI sinyali akışlarını tüketir, kuralları değerlendirir, **alarm üretir**. Aksiyon almaz — o Workflow Engine'in işidir.

**Durum:** Planlama — **§15 kararlar kapatıldı** (3 Haziran 2026); Faz 0/1 sırada

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

---

## İlişkili dokümanlar

| Konu | Konum |
|------|-------|
| Major Roadmap (vizyon) | `docs/odak/operationcore/major_plan.md` §4.2 |
| Workflow Engine (orkestrasyon) | `docs/odak/workflow/Workflow Backend Implementation Plan v1.md` §12 |
| SIEM-hafif (korelasyon kural ailesi) | `docs/odak/monitoring/SIEM_PLANNING.md` §7, §12.1 |
| mon_metrics veri modeli | `docs/content/monitoring_plans/MONITORING_DATA_PRODUCTION.md` |
| IFTTT (superseded) | `docs/content/monitoring_plans/MONITORING_WORKFLOW.md` |
