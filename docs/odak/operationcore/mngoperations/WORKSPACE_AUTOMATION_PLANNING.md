# Workspace otomasyonu — planlama (onaylı v0.2)

**Son güncelleme:** 11 Haziran 2026  
**Durum:** Planlama **tamamlandı** — **implementasyon başlamadı** (sıradaki: SW-A0)  
**Devam noktası:** [DEVAM.md](./DEVAM.md) · Odak senaryo: [../../is_surecleri/DEVAM.md](../../is_surecleri/DEVAM.md)  
**UI wireframe:** [../ui/OC_UI_WORKSPACE_AUTOMATIONS.md](../ui/OC_UI_WORKSPACE_AUTOMATIONS.md)

> **Yeni chat'te devam:** `DEVAM.md` + bu dosya + `OC_UI_WORKSPACE_AUTOMATIONS.md` okuyun.

---

## 1. Ürün özeti

### 1.1 İki olay × iki aksiyon (kullanıcı çerçevesi)

```text
                    ┌─────────────────────────────────────┐
                    │      Otomatik iş tanımı (tek kayıt) │
                    └─────────────────────────────────────┘
         OLAY (WHEN)                              AKSİYON (WHAT)
    ┌──────────────────────┐              ┌──────────────────────────┐
    │ A) WI belirli duruma │              │ 1) Board'da iş oluştur   │ ← MVP
    │    geldi             │              │ 2) Döküman üret          │ ← faz 2
    └──────────────────────┘              └──────────────────────────┘
    ┌──────────────────────┐
    │ B) Alarm oluştu      │              (aynı aksiyon seti; şema hazır)
    └──────────────────────┘
```

| Olay | MVP | Açıklama |
|------|-----|----------|
| **WI duruma geldi** | ✅ | Geçiş sonrası; scope + koşul |
| **Alarm oluştu** | şema only | Tanım modelinde `alarmRaised`; bağlama faz 2 |

| Aksiyon | MVP | Açıklama |
|---------|-----|----------|
| **createWorkItem** | ✅ | Şablon + alan eşlemesi → `CreateFromOrigin` |
| **generateDocument** | placeholder | Mekanik sonra; action tipi rezerve |

### 1.2 Odak Üretim referans senaryo

**Odak Kompozit** tek workspace içinde (farklı board/tip):

- ODF `hold_quality` + `qualityResult=uygunsuz` → ODF **Kalite bekliyor** kalır
- Paralel **NCR** Kalite kuyruğu board'unda açılır
- `parentItemId` = ODF (CAPA → NCR hiyerarşisi aynı model)

**Bugün (v0.2 seed):** NCR + geçiş **manuel** (demo script). Otomasyon yok.

**Validation mevcut:** `approve_quality` + uygunsuz → engellenir; `hold_quality` kullanılmalı.

---

## 2. Kilitlenen kararlar (11 Haz 2026)

| Konu | Karar |
|------|--------|
| UI adı | **Otomatik işler** — Workspace Tanımları'nda ayrı sekme (`tab=automations`) |
| Politikalar & Kurallar | **Dokunulmaz** — validation, SLA, field policy, hafif automation (mail, bildirim) |
| Zamanlanmış işler | **Kardeş pattern** — aynı şablon fikri; tetik = olay (cron değil) |
| Tetik — duruma geldi | **Hedef durum** (`toStateId`) + isteğe bağlı **geçiş** (`transitionKey`) + **koşullar** (`op_rules` ile aynı condition dili) |
| Tetik scope | `workspaceId` zorunlu; isteğe bağlı `boardId`, `typeId` |
| Cross-workspace | MVP **hayır** |
| Şablon | Zamanlanmış iş create payload + **alan eşleme tablosu** |
| İlişki | Varsayılan **`parentItemId`** = kaynak iş; seçenek: `parent` / `none` |
| Origin audit | Her otomatik WI: `sourceType: workspace_automation` |
| Idempotency | Varsayılan **`none`** (çoklu NCR serbest); opsiyonel `one_per_source` |
| Hata politikası | Geçiş **kalır**; fail → activity + uyarı (**rollback yok**) |
| Mail vs NCR | Mail/bildirim → `op_rules`; NCR spawn → Otomatik işler |
| Dataset | Tek kayıt: **`op_workspace_automations`** (tetik + aksiyon bir arada) |
| Executor | Paylaşımlı **`CreateFromOrigin`** (scheduler ile aynı yol) |

### 2.1 Sınır — üç katman

```text
op_rules (Politikalar & Kurallar)
  → validation (DURDURUR), default, hafif automation (aynı WI: mail, bildirim, startWorkflow)

Otomatik işler (op_workspace_automations)                    ← YENİ
  → olay-tetikli orchestration; yeni WI; şablon + field mapping

Zamanlanmış işler (op_work_item_schedules)
  → cron → from-origin

MngWorkflow
  → çok adım, async, modüller arası (ağır senaryolar)
```

---

## 3. Mevcut platform parçaları

| Parça | Rol | Bu özellik için |
|-------|-----|-----------------|
| `op_rules` + `ExecuteAutomationSideEffectsAsync` | Olay yakalama + mail/bildirim | Tetik değerlendirme **aynı mantık**; WI spawn **burada değil** |
| `op_work_item_schedules` | Cron şablon + `from-origin` | Şablon UX referansı |
| `CreateFromOrigin` | Otomatik köken WI + idempotency | **Birincil executor** |
| `parentItemId` | Parent-child hiyerarşi | **Birincil ilişki** (ODF→NCR→CAPA) |
| `op_links` | `relates_to`, `blocks`, … | İkincil; MVP'de otomasyon seçeneği yok |
| `origin` | Audit izi | Zorunlu her spawn'da |
| Alarm ingest | Monitoring → MO | Faz 2; `from-origin` + `sourceType: monitoring` mevcut |

**İlgili:** [SCHEDULED_WORK_ITEMS.md](./SCHEDULED_WORK_ITEMS.md) · [RULE_ENGINE.md](./RULE_ENGINE.md) · [PIPELINES.md](./PIPELINES.md) · [../ui/OC_UI_WORKSPACE_AUTOMATIONS.md](../ui/OC_UI_WORKSPACE_AUTOMATIONS.md)

---

## 4. Veri modeli — `op_workspace_automations`

### 4.1 Dataset alanları

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `name` | text | evet | Liste adı |
| `description` | text | | |
| `workspaceId` | relation → `op_workspaces` | evet | |
| `isActive` | bool | evet | |
| `trigger` | object | evet | Bkz. §4.2 |
| `idempotency` | object | evet | `{ "mode": "none" \| "one_per_source" }` |
| `relation` | object | evet | `{ "mode": "parent" \| "none" }` |
| `actions` | object (array) | evet | Bkz. §4.3 |
| `lastRunAt` | datetime | | |
| `lastCreatedWorkItemId` | relation → `op_work_items` | | |
| `runCount` | number | | Varsayılan 0 |

### 4.2 Index

| Index | Alanlar | Unique |
|-------|---------|--------|
| `idx_workspaceId` | `workspaceId` | hayır |
| `idx_workspaceId_isActive` | `workspaceId`, `isActive` | hayır |
| `idx_workspaceId_name` | `workspaceId`, `name` | evet |

### 4.3 Trigger şeması

**WI durumu (MVP):**

```json
{
  "kind": "workItemStateReached",
  "boardId": "<opsiyonel>",
  "typeId": "<opsiyonel>",
  "toStateId": "<opsiyonel — hedef durum>",
  "transitionKey": "<opsiyonel>",
  "conditions": {
    "op": "and",
    "items": [
      { "field": "fields.qualityResult", "cmp": "eq", "value": "uygunsuz" }
    ]
  }
}
```

**Alarm (şema hazır, MVP evaluate edilmez):**

```json
{
  "kind": "alarmRaised",
  "alarmProfileId": "<opsiyonel>",
  "severity": ["critical", "warning"],
  "conditions": []
}
```

Mapping kaynağı alarm context'te `{{alarm.*}}` (faz 2).

### 4.4 Action şeması

**createWorkItem (MVP):**

```json
{
  "type": "createWorkItem",
  "order": 1,
  "target": { "boardId": "…", "typeId": "…" },
  "title": "Uygunsuzluk — {{source.key}}",
  "description": null,
  "assignee": "{{source.assignee}}",
  "priorityId": null,
  "fieldMappings": [
    { "target": "parentItemId", "source": "relation", "relation": "parent" },
    { "target": "lotSerial", "source": "field", "path": "fields.lotSerial" },
    { "target": "defectDescription", "source": "field", "path": "fields.qualityNotes" },
    { "target": "ncrSource", "source": "static", "value": "final_inspection" }
  ]
}
```

**Field mapping `source` türleri:**

| source | Alanlar | Örnek |
|--------|---------|-------|
| `field` | `path` | `fields.lotSerial` |
| `static` | `value` | `final_inspection` |
| `token` | `template` | `{{source.key}}` (title/assignee için de kullanılır) |
| `relation` | `relation: "parent"` | `parentItemId` |

**generateDocument (placeholder):**

```json
{ "type": "generateDocument", "order": 2, "templateId": "…", "fieldMappings": [] }
```

### 4.5 Tam örnek — Odak NCR

```json
{
  "workspaceId": "9f9cc085-81c7-4a92-9fa2-357ad5c654cd",
  "name": "Uygunsuzluk → NCR",
  "isActive": true,
  "trigger": {
    "kind": "workItemStateReached",
    "typeId": "<üretim emri tip id>",
    "transitionKey": "hold_quality",
    "conditions": {
      "op": "and",
      "items": [{ "field": "fields.qualityResult", "cmp": "eq", "value": "uygunsuz" }]
    }
  },
  "idempotency": { "mode": "none" },
  "relation": { "mode": "parent" },
  "actions": [
    {
      "type": "createWorkItem",
      "order": 1,
      "target": {
        "boardId": "<kalite kuyruğu board id>",
        "typeId": "<NCR tip id>"
      },
      "title": "Uygunsuzluk — {{source.key}}",
      "assignee": "{{source.assignee}}",
      "fieldMappings": [
        { "target": "parentItemId", "source": "relation", "relation": "parent" },
        { "target": "lotSerial", "source": "field", "path": "fields.lotSerial" },
        { "target": "defectDescription", "source": "field", "path": "fields.qualityNotes" },
        { "target": "ncrSource", "source": "static", "value": "final_inspection" }
      ]
    }
  ]
}
```

---

## 5. İlişkili işler modeli

```text
ODF-0001 (Üretim emri)
  └─ parent-child → NCR-0001
        └─ parent-child → CAPA-0001
```

| Mekanizma | Rol | Otomasyonda |
|-----------|-----|-------------|
| **`parentItemId`** | Hiyerarşik alt iş | **Varsayılan** — profil alt kayıtlar, silme guard |
| **`op_links`** | Eş düzey ilişki | MVP dışı; ileride `linkType` seçeneği |
| **`origin`** | Audit: hangi otomasyon tetikledi | Her spawn'da zorunlu |

**Origin örneği:**

```json
{
  "sourceType": "workspace_automation",
  "sourceSystem": "MngOperations",
  "sourceId": "<automation __dataId>",
  "correlationId": "<automationId>:<sourceWiId>:<runGuid>"
}
```

**Idempotency `one_per_source`:** `correlationId = "{automationId}:{sourceWorkItemId}"` → `from-origin` mevcut lookup, `ALREADY_EXISTS`.

---

## 6. MO executor

### 6.1 Runtime sırası (onaylı)

Mevcut transition pipeline ([PIPELINES.md](./PIPELINES.md)) içinde, persist sonrası:

```text
1. op_rules pre-validation
2. Geçiş / mutasyon
3. op_rules default + post-validation
4. DG persist (op_work_items)
5. Activity / timeline / SLA / bildirim politikaları
6. op_rules inline automation (mail, bildirim, startWorkflow)   ← mevcut
7. Workspace automations (YENİ)                                   ← burada
8. RabbitMQ publish
```

### 6.2 Bileşenler

```text
WorkItemCommandService (transition/create)
  └─ RunWorkspaceAutomationsAsync(context)
       └─ WorkspaceAutomationService
            ├─ LoadActiveAutomations(workspaceId)     [metadata cache]
            ├─ MatchTrigger(automation, context)
            ├─ EvaluateConditions(...)                [rule engine condition paylaşımı]
            ├─ ResolveIdempotency → correlationId
            └─ AutomationActionExecutor
                 └─ CreateWorkItemAction
                      ├─ ResolveFieldMappings(...)
                      ├─ ApplyRelation(parentItemId)
                      └─ IAutomationTemplateBuilder → CreateFromOriginAsync(...)
```

**Paylaşımlı builder:** `WorkItemScheduleExecuteService.BuildFromOriginRequest` genelleştirilir → `IAutomationTemplateBuilder` (schedule + otomasyon).

### 6.3 Trigger context (transition)

```text
Event: WorkItemTransitioned
WorkspaceId, BoardId, TypeId
WorkItemId, WorkItemKey
ToStateId, TransitionKey, FromStateId
WorkItem snapshot (persist sonrası)
TransitionFields (geçiş dialog alanları)
```

### 6.4 Hata politikası

| Durum | Davranış |
|-------|----------|
| Action başarısız | Geçiş **geri alınmaz** |
| Kayıt | `op_activities` → `AutomationFailed` + otomasyon adı + hata |
| HTTP | 200 + isteğe bağlı `warnings[]` |

### 6.5 Alarm (faz 2 stub)

`trigger.kind === alarmRaised` kayıtları yüklenir; MVP'de **evaluate edilmez** (log: skipped). Bağlama: monitoring ingest → `EvaluateAlarmContext`.

---

## 7. UI özeti

Detay: [OC_UI_WORKSPACE_AUTOMATIONS.md](../ui/OC_UI_WORKSPACE_AUTOMATIONS.md)

| Öğe | Değer |
|-----|--------|
| Sekme | `automations` — Zamanlanmış işler'in yanında |
| Bileşen | `OcWorkspaceDefinitionsAutomationsTab.vue` |
| Editör | Genel · Tetik · Aksiyon · Eşleme · İlişki/idempotency · Önizleme |
| Liste aksiyonları | Düzenle · Sil · **Simüle et** |
| CRUD | UI → DG direkt (schedule deseni) |
| Yetki | Domain manager |

---

## 8. Uygulama fazları

| Faz | Kod | Kapsam | Doğrulama |
|-----|-----|--------|-----------|
| **SW-A0** | Dataset | `op_workspace_automations` generator + setup script | DG GET 200 |
| **SW-A1** | MO | `WorkspaceAutomationService` + transition hook + mapping + `CreateFromOrigin` | Integration / manuel transition |
| **SW-A2** | UI | Sekme + liste + editör | Workspace tanımları Odak |
| **SW-A3** | Seed | Odak NCR otomasyon kaydı; manuel demo script NCR kaldır | E2E hold_quality + uygunsuz |
| **SW-A4** | Polish | Simüle et, `AutomationExecuted` activity, i18n | Tarayıcı demo |

**Bilinçli ertelenen:** alarm bağlama, `generateDocument`, `op_link` ilişki modu, cross-workspace, `startWorkflow` otomasyon action (SLA breach `op_rules`'ta kalır).

---

## 9. Kapalı sorular (v0.2 kararları)

| # | Soru | Karar |
|---|------|--------|
| Q1 | Cross-workspace? | MVP hayır |
| Q2 | Fail → rollback? | Hayır — best-effort + activity |
| Q3 | İki liste vs tek kayıt? | **Tek kayıt** `op_workspace_automations` |
| Q4 | Döküman servisi? | Faz 2; action placeholder |
| Q5 | Alarm tanım yeri? | Workspace metadata; Alarm Center referans (faz 2 bağlama) |
| Q6 | `startWorkflow` duplicate? | Executor paylaşımı; basit senaryolar `op_rules`'ta kalır |

---

## 10. Konuşma geçmişi (özet)

| Aşama | Fikir |
|-------|--------|
| 1 | `op_rules` içinde `createWorkItem` |
| 2 | Zamanlanmış işler modeline benzer şablon + tetik |
| 3 | Ayrı **Otomatik işler** ekranı |
| 4 | 2 olay × 2 aksiyon; alan eşlemesi; ilişkili işler |
| 5 | v0.2 — UI wireframe + dataset + MO executor + faz planı **onaylandı** |

---

## 11. İlgili dosyalar

| Dosya | Rol |
|-------|-----|
| [DEVAM.md](./DEVAM.md) | MngOperations / OC checkpoint |
| [../../is_surecleri/DEVAM.md](../../is_surecleri/DEVAM.md) | Odak Üretim checkpoint |
| [../ui/OC_UI_WORKSPACE_AUTOMATIONS.md](../ui/OC_UI_WORKSPACE_AUTOMATIONS.md) | UI wireframe |
| [../../is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md](../../is_surecleri/referans/ODAK_URETIM_WORKSPACE_TASLAK.md) | ODF / NCR / CAPA |
| [SCHEDULED_WORK_ITEMS.md](./SCHEDULED_WORK_ITEMS.md) | Kardeş pattern (cron) |
| [RULE_ENGINE.md](./RULE_ENGINE.md) | Validation + hafif automation |
| [PIPELINES.md](./PIPELINES.md) | Komut pipeline sırası |
