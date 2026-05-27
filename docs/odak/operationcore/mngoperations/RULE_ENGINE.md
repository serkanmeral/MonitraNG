# MngOperations — Rule engine (Faz 1)

**Son güncelleme:** 26 Mayıs 2026  
**Kaynak dataset:** `op_rules` · Spec: [operationcore_phase1.md §12–13](../operationcore_phase1.md)

---

## 1. Rol

```text
Runtime Decision Engine
```

Hardcoded `if (type == …)` yok. Kurallar metadata; MngOperations yorumlar ve uygular.

---

## 2. Rule türleri

| Tür | `trigger` örnekleri | Davranış |
|-----|---------------------|----------|
| **Default** | `WorkItemCreated`, `WorkItemUpdated`, `WorkItemTransitioned` | Alan zenginleştirme; işlemi **reddetmez** |
| **Validation** | Aynı + `WorkItemTransition` | Başarısız → 400, pipeline durur |
| **Automation** | Event sonrası (inline) | Side-effect action listesi |

Automation ayrı `op_automations` dataset **yok** — automation = `op_rules` + `actions[]` ([§5.2.6](../OPERATION_CORE_IMPLEMENTATION_PLAN.md)).

---

## 3. Scope (filtreleme)

Kural çalışmadan önce eşleşme:

| Alan | Açıklama |
|------|----------|
| `workspaceId` | Boş = global |
| `typeId` | WorkItem tipi |
| `transitionKey` | Transition kuralları ([§5.2.3](../OPERATION_CORE_IMPLEMENTATION_PLAN.md)) |
| `fromStateId` / `toStateId` | Kenar scope; `transitionKey` boş → o kenardaki tüm geçişler |
| `isActive` | false ise atla |

**Çakışma:** Aynı olayda birden fazla validation → **en kısıtlayan kazanır** (tüm fail koşulları birleştirilir veya ilk fail — implementasyonda **tümünü değerlendir, birleşik hata listesi** önerilir).

---

## 4. Çalıştırma sırası

[phase1 §11.3](../operationcore_phase1.md) merge önceliği ile uyumlu:

```text
Field Definition → Form/Profile → Workspace → Board → State
 → Permission → Rule → Automation
```

**Pipeline içi:**

1. **Pre-validation** (transition öncesi)
2. State / alan mutasyonu
3. **Default rules**
4. **Post-validation**

---

## 5. Faz 1 action tipleri

| Action | Açıklama |
|--------|----------|
| `setField` | `{ "field": "priorityId", "value": "…" }` |
| `setAssignee` | Kullanıcı adı / person id |
| `setAssignmentGroups` | Grup listesi |
| `addWatcher` | Watcher ekle |
| `createNotification` | `op_notifications` satırı |
| `sendEmailViaMngNotifiers` | templateKey + recipients resolve |
| `createActivity` | Ek audit satırı |

**Faz 1 dışı:** webhook, script, MQTT, external HTTP chain.

---

## 6. Condition modeli (öneri)

`op_rules.conditions` JSON — basit ifade ağacı:

```json
{
  "op": "and",
  "items": [
    { "field": "fields.severity", "cmp": "eq", "value": "critical" },
    { "field": "assignee", "cmp": "empty" }
  ]
}
```

Operatörler Faz 1: `eq`, `ne`, `empty`, `notEmpty`, `in`, `gt`, `lt` (tip uyumlu).

Karmaşık script → Faz 2.

---

## 7. Transition katalog ilişkisi

`op_state_flows.transitions[]` **graf tanımı**; rule yeni geçiş **tanımlamaz**.

Transition nesnesi rule referansı taşıyabilir:

```json
{
  "key": "resolve",
  "fromStateId": "…",
  "toStateId": "…",
  "requiredFields": ["resolution"],
  "validationRuleIds": [],
  "permissions": { "groups": ["maintenance-team"] }
}
```

---

## 8. Test stratejisi (plan)

- Unit: scope matcher, condition evaluator, action executor (mock DG)
- Integration: Odak domain’de örnek workspace + 2 transition + 1 validation rule
