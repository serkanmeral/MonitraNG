# `sec_events` → Observation Zarfı (Faz 2 entegrasyon)

**Durum:** ✅ **Implementasyon tamam** (4 Haz 2026) — Reactor publish + MngAlarm `*.event.*` queue bind  
**Bağımlılık:** [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) · [alarm/DEVAM.md](../alarm/DEVAM.md)

---

## 1. Amaç

`MngAlarm` gözlem motoru `ObservationEnvelope` tüketir (`kind`, `key`, `value`, `dimensions`). SIEM olayları `sec_events` belgesinden bu zarfına map edilir; korelasyon kuralları (`matchKey`, `groupByFields`) aynı kalır.

---

## 2. Zarf şeması (sec_event)

```json
{
  "domainName": "odak",
  "domainId": "6a0f8fc43d6ba5d774ee37c1",
  "timestamp": "2026-06-03T14:00:02Z",
  "kind": "event",
  "key": "login_failed",
  "dimensions": {
    "userId": "admin",
    "srcIp": "192.168.1.50",
    "dstIp": null,
    "sourceType": "ad",
    "sourceHost": "DC01.odak.local",
    "eventCategory": "authentication",
    "eventOutcome": "failure",
    "parserId": "windows.security.v1",
    "secEventId": "abc123..."
  }
}
```

| Alan | Kaynak (`sec_events`) | Not |
|------|------------------------|-----|
| `kind` | sabit `"event"` | Metrik `kind=metric` ile ayrılır |
| `key` | `event.action` | U1: `login_failed`, U4: `denied_flow` |
| `timestamp` | `@timestamp` | Event-time; geç gelen olay toleransı Alarm Faz 2+ |
| `dimensions.userId` | `actor.user` | Korelasyon groupBy |
| `dimensions.srcIp` | `network.srcIp` | Korelasyon + workflow block_ip |
| `dimensions.dstIp` | `network.dstIp` | U4 groupBy |
| `dimensions.dstPort` | `network.dstPort` | Opsiyonel filtre |
| `dimensions.sourceType` | `source.type` | `ad`, `firewall`, `bastion` |
| `dimensions.secEventId` | Mongo `_id` | Drill-down / audit |

**4624 başarılı login:** `key: login_success` (U2 sequence kuralının 2. adımı).

---

## 3. Publish yolu (öneri)

```text
MngReactor sec_event persist
  → monitra.observations (topic)
  → routing: {domainId}.event.{event.action}
  → MngAlarm ObservationConsumer (mevcut)
```

Bridge deseni metrik ile aynı; routing key örneği: `6a0f8fc43d6ba5d774ee37c1.event.login_failed`.

**Alternatif (yüksek hacim):** `sec_events.created` batch summary → Alarm partition worker — [SIEM_THROUGHPUT_AND_QUEUES.md §5](./SIEM_THROUGHPUT_AND_QUEUES.md).

---

## 4. Kural eşlemesi

| Senaryo | `matchKey` | `groupByFields` | Taslak JSON |
|---------|------------|-----------------|-------------|
| U1 | `login_failed` | `userId`, `srcIp` | `tests/fixtures/siem/alarm_rules/u1_*.json` |
| U4 | `denied_flow` | `dstIp` | `tests/fixtures/siem/alarm_rules/u4_*.json` |
| U5 | `allowed_flow` | `dstIp`, `dstPort` | `tests/fixtures/siem/alarm_rules/u5_*.json` |
| U3 | `privileged_login_outside_window` | `userId`, `srcIp` | `tests/fixtures/siem/alarm_rules/u3_*.json` |
| U6 | `rule_change` | `userId`, `sourceHost` | `tests/fixtures/siem/alarm_rules/u6_*.json` |
| U8 | `group_member_added` | `userId`, `sourceHost` | `tests/fixtures/siem/alarm_rules/u8_*.json` |
| U9 | `account_created` | `userId`, `sourceHost` | `tests/fixtures/siem/alarm_rules/u9_*.json` |
| U10 | `directory_object_modified` | `userId`, `sourceHost` | `tests/fixtures/siem/alarm_rules/u10_*.json` |
| U7 | `new_flow` | `srcIp`, `dstIp` | `tests/fixtures/siem/alarm_rules/u7_*.json` |

**U7 çift observation:** Baseline sonrası yeni src→dst çiftinde birincil `key` korunur (`denied_flow` / `allowed_flow`); ek olarak `key=new_flow` observation yayınlanır (U4/U5 spike kuralları etkilenmez).

| U2 | `login_success_after_failures` | `userId`, `srcIp` | `sequence` — `tests/fixtures/siem/alarm_rules/u2_*.json` |

---

## 5. Implementasyon sırası

1. Faz 1 spike: `sec_events` Mongo + `sec_events.created` MQ (MngReactor)
2. Reactor: `SecEventObservationPublisher` → `monitra.observations`
3. MngAlarm: `kind=event` kuralları mevcut `ObservationProcessor` ile değerlendirilir
4. Odak E2E: fixture replay → U1 kural → `alarm.raised` → workflow (P4 şablonu)

---

## 6. Referanslar

- [REACTOR_OBSERVATION_PUBLISH_SPEC.md](../alarm/REACTOR_OBSERVATION_PUBLISH_SPEC.md)
- [SIEM_WORKFLOW_SEAM.md](./SIEM_WORKFLOW_SEAM.md)
- [tests/fixtures/siem/alarm_rules/README.md](../../tests/fixtures/siem/alarm_rules/README.md)
