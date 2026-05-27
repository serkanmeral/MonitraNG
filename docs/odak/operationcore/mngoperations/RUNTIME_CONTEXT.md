# MngOperations — RuntimeContext

**Son güncelleme:** 26 Mayıs 2026  
**Spec:** [operationcore_phase1.md §3](../operationcore_phase1.md)

---

## 1. Amaç

UI **iş kuralı çalıştırmaz**; ekran başına backend üretilmiş context render eder.

```text
RuntimeContextBase
 ├─ FormRuntimeContext
 ├─ ProfileRuntimeContext
 ├─ BoardRuntimeContext
 └─ DashboardRuntimeContext
```

---

## 2. RuntimeContextBase (ortak)

```json
{
  "workspaceId": "…",
  "permissions": { },
  "fieldBehaviors": { "resolution": { "visible": true, "readonly": false, "required": true, "masked": false } },
  "labels": { },
  "locale": "tr-TR"
}
```

---

## 3. FormRuntimeContext

**Endpoint:** `GET /runtime/work-items/{id}/form?mode=create|edit`

| Bölüm | İçerik |
|-------|--------|
| `layout` | `op_forms.layout` — `sections`, `fieldCols`, `sectionCols`, `formHeading`, `formIntro`, `dialogMaxWidth` ([şema](./FORM_LAYOUT_AND_EXTRA_FIELDS.md)) |
| `fields` | Resolved metadata + current values (layout sırası) |
| `fieldBehaviors` | `op_forms.fieldBehaviors` + rule/permission katmanı |
| `types` | Workspace enabled types (create mode) |
| `initialStateId` | Create mode default |
| `validationHints` | Sunucu tarafı kural özeti (opsiyonel) |

Create: `GET .../work-items/form?workspaceId=&mode=create&formId=` — `id` yok.

**UI taslak önizleme:** MO çağrısı zorunlu değil; editör state’inden üretilir ([ui/OC_UI_FORM_DEFINITIONS.md](../ui/OC_UI_FORM_DEFINITIONS.md)).

**Dosya alanları (`attachments`):** `fieldBehaviors.attachments` — UI upload’u **DG file API** ile yapar (Q8); MO proxy yok.

---

## 4. ProfileRuntimeContext

**Endpoint:** `GET /runtime/work-items/{id}/profile`

| Bölüm | İçerik |
|-------|--------|
| `workItem` | Tam kayıt (ilişkiler expand edilmiş özet) |
| `layout` | `op_profiles` |
| `actions` | Uygulanabilir `transitionKey` listesi (sıra = profile.actions) |
| `sla` | `responseDueAt`, `resolveDueAt`, breach flags |
| `watchers`, `links` | Özet listeler |
| `stateSegments` | Son 5 segment (embed); tam liste: `GET .../state-segments` |

`actions[].enabled` = permission + transition resolve sonucu. Sıra = `op_profiles.actions`.

**Uygulama:** `IRuntimeContextService.GetProfileAsync` (Faz 1).

---

## 5. BoardRuntimeContext

**Endpoint:** `GET /runtime/boards/{boardId}`

| Bölüm | İçerik |
|-------|--------|
| `columns[]` | `stateId`, title, `dropEligible`, `defaultTransitionKey`, `alternativeTransitionKeys` |
| `filters` | Board default + saved filter referansları |
| `cardSchema` | Kartta gösterilecek alanlar |
| `queries` | Kolon başına `queryKey`, `parametersTemplate`, `pageSize` önerisi — **kart verisi yok** (Q7) |

### 5.1 Kolon kart verisi (Q7 — kararlandı)

| Katman | İçerik |
|--------|--------|
| `GET /runtime/boards/{boardId}` | Kolon metadata, drop, transition, queryKey şablonu |
| `POST /runtime/queries/{queryKey}/execute` | Kolon başına kart listesi; sayfalama (`skip`/`take` veya `page`) |

**Gerekçe:** Kolonlar bağımsız yenilenebilir; büyük board’da tek response şişmez; `wi_board_column` / `wi_by_workspace_and_state` parametreleri MO resolver ile dolar.

**UI akışı:** Board açılır → context → her kolon (veya görünür kolonlar) için paralel execute.

---

## 6. DashboardRuntimeContext

**Endpoint:** `GET /runtime/dashboards/{dashboardId}`

`op_dashboards` widget tanımları + her widget için çözülmüş query parametreleri + son çalıştırma özeti (Faz 1: senkron execute).

**Uygulama:** `GET /runtime/dashboards/{dashboardId}` — widget başına `execution` (total, items, success/error).

---

## 7. Query parameter resolver

[phase1 §17.3](../operationcore_phase1.md):

| Token | Değer |
|-------|--------|
| `{{currentUser}}` | JWT username |
| `{{currentWorkspace}}` | Context workspace |
| `{{currentBoard}}` | Board id |
| `{{today}}` | UTC date |
| `{{startOfWeek}}` | Hafta başı |

`POST /runtime/queries/{queryKey}/execute`:

```json
{
  "dataset": "op_work_items",
  "parameters": { "workspaceId": "…", "stateId": "…" }
}
```

---

## 8. Cache invalidation

Context **kısa ömürlü değil** — her istekte taze üretim (komut sonrası UI yeni GET atar). Metadata alt katmanı cache’lenir ([DG_INTEGRATION](./DG_INTEGRATION.md)).
