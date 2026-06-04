# SIEM × Workflow Seam Değerlendirmesi

**Son güncelleme:** 3 Haziran 2026  
**Durum:** ✅ **Workflow çekirdeği SIEM müdahale hattı için yeterli** — Faz 1 spike + `sec_events` ingest sonrası uçtan uca bağlanabilir

**Referans:** [workflow/DEVAM.md §P4](../workflow/DEVAM.md) · [SIEM_PLANNING.md §8](./SIEM_PLANNING.md)

---

## 1. Özet

| Seam | Durum | Not |
|------|-------|-----|
| Alarm → Workflow Event Trigger | ✅ | `mng.alarms` · `alarm.raised` / `.updated` / `.resolved` Odak E2E |
| Onaylı müdahale | ✅ | `approval.wait` + `POST /approvals/{id}/decide` |
| Geçici IP blok | ✅ MVP | `engine.command` + `block_ip` / `unblock_ip` (Reactor MQTT publish) |
| SOC WorkItem | ✅ | `workitem.create` / `transition` / `update` |
| Alarm context → workflow | ✅ | `filterExpression`, `{{event.*}}`, `{{outputs.*}}` |
| `sec_events` → alarm | ✅ | U1 Odak E2E |
| Güvenlik paneli UI | ⏸️ | Onay kartı — Mng.Ui backlog |
| Gerçek firewall API | ⏸️ | Engine handler + vendor API — SIEM Faz 3 |

**Sonuç:** Workflow “SIEM için bitti” sayılabilir. Implementasyon kapısı artık **MngEngine/MngReactor Faz 1 spike** (harici repo).

---

## 2. Referans akış (U1 brute-force)

```text
sec_events (login_failed × N)
  → MngAlarm correlation (U1 kural)
  → mng.alarms: alarm.raised
  → Workflow Event Trigger (severity ≥ 7, matchKey filtresi)
  → approval.wait (SecurityAdmins)
  → [onay] engine.command block_ip (srcIp = context.srcIp)
  → workitem.create (SOC workspace)
  → WriteLog (audit)
```

Odak P4-A şablonu: `scripts/odak/test-alarm-approval-e2e.ps1` · `matchKey: auth_failure_p4_e2e`

**SIEM U1 tam hattı:** `scripts/odak/test-siem-u1-approval-block-e2e.ps1` · `matchKey: login_failed` · `filterExpression` + `block.ip` (`{{event.context.srcIp}}`)

---

## 3. Event Trigger yapılandırması (SIEM)

```json
{
  "type": "event",
  "config": {
    "eventType": "alarm.raised",
    "filterExpression": "event.severity >= 7 && event.context.key == 'login_failed'"
  }
}
```

Alarm payload alanları (`AlarmEventMessage`): `severity`, `ruleId`, `dedupKey`, `context` (groupKey, windowCount, userId, srcIp…).

---

## 4. Onay + Block IP node'ları

**Approval:**

```json
{ "type": "approval.wait", "config": { "approverGroup": "SecurityAdmins" } }
```

**Block IP (onay sonrası):**

```json
{
  "type": "engine.command",
  "config": {
    "command": "block_ip",
    "engineId": "{{variables.targetEngineId}}",
    "payload": {
      "ip": "{{event.context.srcIp}}",
      "ttlMinutes": 60,
      "reason": "SIEM U1 brute-force"
    }
  }
}
```

**Kilitli karar:** Varsayılan onaylı **geçici** blok; otomatik kalıcı blok yok. TTL sonrası `unblock_ip` veya Scheduler delay node — [SIEM_PLANNING §8](./SIEM_PLANNING.md).

---

## 5. Eksikler (SIEM spike sonrası)

| # | Madde | Sahip |
|---|-------|-------|
| S1 | `sec_events` ingest + observation publish | MngReactor |
| S2 | U1/U4 kural yükleme + replay E2E | ✅ Odak script |
| S3 | `test-siem-u1-workflow-e2e.ps1` | ✅ Odak E2E |
| S4 | Onay kartı UI | Mng.Ui |
| S5 | Engine gerçek firewall handler | MngEngine |

---

## 6. Alert payload (workflow context)

Workflow instance `triggerData` / `event` içinde beklenen minimum:

| Alan | Kaynak |
|------|--------|
| `eventType` | `alarm.raised` |
| `severity` | Kural `severity` |
| `ruleId` | `@mon_alarm_rules` id |
| `context.key` | `login_failed` / `denied_flow` |
| `context.userId` | groupBy |
| `context.srcIp` | block_ip hedefi |
| `context.dstIp` | U4 triage |
| `correlationId` | Audit zinciri |

---

## 7. Sonraki adım

1. [SIEM_FAZ1_HANDOFF.md](./SIEM_FAZ1_HANDOFF.md) — MngEngine/MngReactor spike başlat
2. Spike bitince: observation map + U1 kural + P4 workflow klonu → Odak E2E
