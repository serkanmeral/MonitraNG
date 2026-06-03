# DEVAM — Alarm & Rule Engine (Kaldığımız Yer)

**Son güncelleme:** 3 Haziran 2026  
**Durum:** ✅ **Faz 0/1 tamam** · ✅ **Faz 2** · ✅ **P4-A** · ✅ **Native observation consumer + Odak E2E** (Reactor repo publish bekliyor)

> Workflow entegrasyonu: [docs/odak/workflow/DEVAM.md](../workflow/DEVAM.md) · Seam: `mng.alarms` → Workflow Event Trigger (Faz 4 tamamlayıcı)

---

## 1. Tek cümlede durum

`MngAlarm` solution (Api `:5087` + Worker) Odak'ta ayakta: threshold kural, dedup/cooldown, `@mon_alarm_rules` / `@mon_alarms`, `mng.alarms` publish. Workflow worker `mng.alarms` bind (`*.alarm.#`) + routing normalize. **E2E:** `cpu_usage > 90` → alarm → workflow `alarm.raised` → 3 node Success.

---

## 2. Kilitli kararlar (§15 — kapandı)

| # | Soru | Karar | Gerekçe |
|---|------|-------|---------|
| A1 | Servis adı | **`MngAlarm`** | Korelasyon yalnızca bir kural ailesi; SIEM+IT+AI tek motor |
| A2 | State store | **Bellek + Mongo checkpoint** (Faz 1–2); OpenSearch **Faz 2+ opsiyon** | Odak hacmi için yeterli; stream arama ihtiyacı kanıtlanınca |
| A3 | `mon_alarms` erişimi | **Faz 1: MngAlarm doğrudan Mongo** (`@mon_alarms`); DG read mirror **Faz 2** | Yazma frekansı düşük; motor latency öncelikli |
| A4 | Reactor publish kapsamı | **Faz 1: metrik stream mevcut publish**; sec_events + signal **aynı observation zarfı** Faz 1.1 | Birleşik observation hedefi; kademeli genişletme |
| A5 | Scheduled validation | **MngScheduler** (Workflow Delay/Schedule ile aynı desen) | Mevcut altyapı; cron granülaritesi yeterli |
| A6 | Partitioning | **Faz 1: uygulama seviyesi** `hash(domainId+groupKey)%N`; consistent-hash exchange **Faz 2 ölçek** | Basit başlangıç; kanıtlanmış ihtiyaçta RabbitMQ hash |

---

## 3. Faz 0/1 — implementasyon planı

### Faz 0 — İskelet

| # | Görev | Kabul |
|---|-------|-------|
| 0.1 | `MngAlarm` solution (Api + Worker), Mongo, RabbitMQ `mng.alarms` exchange declare | ✅ |
| 0.2 | Domain: `@mon_alarm_rules`, `@mon_alarms` koleksiyonları | ✅ |
| 0.3 | Observation envelope modeli (kind, key, value, dimensions) | ✅ |

### Faz 1 — Threshold + alarm yaşam döngüsü

| # | Görev | Kabul |
|---|-------|-------|
| 1.1 | Threshold kural değerlendirme (stateless) | ✅ |
| 1.2 | Alarm raise/update + dedupKey + cooldown | ✅ |
| 1.3 | `mng.alarms` publish: `{domainId}.alarm.raised.{severity}` | ✅ |
| 1.4 | Workflow `mng.alarms` bind + routing normalize | ✅ |
| 1.5 | **Odak E2E:** rule → observation → workflow instance | ✅ 3 Haz 2026 |
| 1.6 | **Reactor metric bridge:** `mng.topics` → `monitra.observations` | ✅ deploy + consumer ayakta |
| 1.7 | **Auto-resolve:** eşik altı observation → `alarm.resolved` publish | ✅ |
| 1.8 | **Odak E2E:** raised → updated → resolved → workflow (3 trigger) | ✅ 3 Haz 2026 |

**API (Odak):** `POST /alarm/api/v1/rules?domainName=odak`, `POST /alarm/api/v1/dev/observations/ingest`  
**Workflow trigger config:**

```json
{ "type": "event", "config": { "eventType": "alarm.raised" } }
{ "type": "event", "config": { "eventType": "alarm.updated" } }
{ "type": "event", "config": { "eventType": "alarm.resolved" } }
```

**E2E script:** `scripts/odak/test-alarm-lifecycle-e2e.ps1`

**Bridge:** `MetricObservationBridgeConsumer` — MngReactor repoda olmadığı için geçici köprü. Native publish gelince `ReactorBridge__Enabled=false`.

**Faz 1 dışı (erteleme):** correlation window, AI scorer, suppression/dependency, DG mirror. **AI zamanlama:** [AI_PLANNING_DECISION.md](../AI_PLANNING_DECISION.md) — scorer Alarm Faz 4 / önkoşul P1–P5 sonrası.

---

## 4. Seam — Workflow

```text
Reactor → observation stream → MngAlarm (threshold) → mng.alarms
                                                      ↓
                              MngWorkflow EventListener (Faz 4) → instance
```

Event şeması: [ALARM_RULE_ENGINE_PLAN §8](./ALARM_RULE_ENGINE_PLAN.md)

---

## 5. Sonraki adımlar

1. ~~Reactor observation stream bind~~ → **Faz 1.1 bridge** ✅
2. ~~`alarm.updated` / `alarm.resolved` workflow triggers~~ ✅
3. MngReactor repo: native `monitra.observations` publish — sözleşme: [REACTOR_OBSERVATION_PUBLISH_SPEC.md](./REACTOR_OBSERVATION_PUBLISH_SPEC.md) · handoff: [REACTOR_NATIVE_PUBLISH_HANDOFF.md](./REACTOR_NATIVE_PUBLISH_HANDOFF.md) · MonitraNG consumer ✅
4. ~~Faz 2 — correlation window, scheduled validation (MngScheduler)~~ ✅ 3 Haz 2026
5. Faz 2+ — suppression/dependency, DG mirror, Mongo checkpoint, sequence rules

---

## 7. Faz 2 — correlation + scheduled validation ✅

| # | Görev | Kabul |
|---|-------|-------|
| 2.1 | `correlation` kural tipi — sliding window + groupBy + count threshold | ✅ |
| 2.2 | Bellek içi `ICorrelationWindowStore` + `IObservationActivityStore` | ✅ |
| 2.3 | Validation scan — pencere düşünce correlation auto-resolve | ✅ |
| 2.4 | `scheduled` kural tipi — stalenessMinutes → validation scan raise/resolve | ✅ |
| 2.5 | `POST /alarm/api/v1/validation/run` | ✅ |
| 2.6 | MngScheduler `alarm-validation-{domain}` → MA validation/run | ✅ |

**Correlation kural örneği:**

```json
{
  "name": "Brute force",
  "type": "correlation",
  "matchKey": "auth_failure",
  "groupByFields": ["userId", "srcIp"],
  "windowMinutes": 5,
  "threshold": 10,
  "severity": 7,
  "cooldownMinutes": 15
}
```

**Scheduled (staleness) kural örneği:**

```json
{
  "name": "Agent heartbeat missing",
  "type": "scheduled",
  "matchKey": "agent_heartbeat",
  "stalenessMinutes": 15,
  "severity": 5
}
```

**Scheduler job ID:** `alarm-validation-odak` (cron `@scheduled_jobs` ile)

**E2E script:** `scripts/odak/test-alarm-faz2-e2e.ps1`

**Deploy durumu (3 Haz 2026):** Lifecycle E2E ✅ · Faz 2 E2E ✅ · **P4-A** E2E ✅ · **Native observation** E2E ✅ (`test-observation-native-e2e.ps1`)

**P4-A kapsamı (minimal):** correlation alarm → `alarm.raised` → approval → `workitem.create`. **Ertelenen (SIEM olgunlaşınca):** Block IP / Engine komut, TTL unblock, güvenlik paneli UI — bkz. [workflow/DEVAM §P4](../workflow/DEVAM.md).

```powershell
.\scripts\odak\sync-odak-source.ps1 -Paths @('MngAlarm','MngScheduler','ApplicationResources/mng_apps')
.\scripts\odak\deploy-odak-apps.ps1 -Services mngalarm,mngalarm-worker,mngscheduler -NoCache
.\scripts\odak\test-alarm-faz2-e2e.ps1
```

---

## 6. İlgili dokümanlar

- [ALARM_RULE_ENGINE_PLAN.md](./ALARM_RULE_ENGINE_PLAN.md)
- [Workflow DEVAM](../workflow/DEVAM.md)
- [SIEM_PLANNING.md](../monitoring/SIEM_PLANNING.md)
