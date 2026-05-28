# Mng.Ui — Çalışma alanı (workspace) politikaları ve katalog değerleri

**Son güncelleme:** 28 Mayıs 2026  
**Durum:** **W1+ UI + P-UX** ✅ · **R1** `op_rules` ayrı **Kurallar** sekmesi + **R-UX** ✅ · **MO merge** `settings.fieldPolicies` ✅ (Odak deploy).  
**İlişkili:** [OC_UI_FIELD_POLICY.md](./OC_UI_FIELD_POLICY.md) · [RULE_ENGINE.md](../mngoperations/RULE_ENGINE.md) · [PERMISSIONS_AND_FIELD_BEHAVIOR.md](../mngoperations/PERMISSIONS_AND_FIELD_BEHAVIOR.md) · [DEVAM.md](../mngoperations/DEVAM.md)

---

## 0. Bu oturumda tamamlananlar (kayıt — 26 Mayıs 2026)

### 0.1 Workspace tanımları — sekme yapısı

Üst sekmeler sadeleştirildi; katalog seçimleri tek yerde toplandı:

| Üst sekme | İçerik |
|-----------|--------|
| Genel | Kimlik, prefix, workspace tipi |
| **Değerler** | Alt sekmeler: İş tipleri, Durumlar, Öncelikler, Alanlar |
| Durum akışı | `op_state_flows` |
| Formlar | `op_forms` |
| Board'lar | `op_boards` |
| Politikalar | Alan politikaları (`fieldPolicies`) |
| **Kurallar** | `op_rules` — validasyon / otomasyon |

- Route: `?tab=values&valuesTab=states` (alt sekme)
- Eski URL uyumu: `?tab=states` → `?tab=values&valuesTab=states`
- Composable: `useOcWorkspaceDefinitionTabs.ts`, `useOcWorkspaceDefinitionValuesTabs.ts`
- Bileşen: `OcWorkspaceDefinitionsValuesTab.vue`

### 0.2 Workspace katalog seçimi (`enabled*Ids`)

| Alan | Dataset ilişkisi | UI (Değerler alt sekmesi) | Listeleme servisi |
|------|------------------|---------------------------|-------------------|
| `enabledTypeIds` | `op_work_item_types` | İş tipleri | `ocListWorkItemTypesForWorkspace(ws, { fallbackAll? })` |
| `enabledStateIds` | `op_states` | Durumlar | `ocListStatesForWorkspace(ws, { fallbackAll?, includeFlowStates? })` |
| `enabledPriorityIds` | `op_priorities` | Öncelikler | `ocListPrioritiesForWorkspace(ws, { fallbackAll? })` |
| `enabledFieldIds` | `op_fields` | Alanlar | (mevcut alan sekmesi) |

**Filtre mantığı:**

- Global katalogdan yalnızca işaretlenen kayıtlar listelenir (politika şartları, form tip/öncelik seçimi, dinamik form).
- **İstisna — tipler:** `workspaceId` dolu workspace’e özel `op_work_item_types` kayıtları seçimden bağımsız her zaman dahildir.
- **İstisna — durumlar:** `enabledStateIds` boşken akışlardaki geçiş state’leri `includeFlowStates` ile eklenebilir.
- **Yedek persist:** DG alanı henüz yoksa `op_workspaces.settings` içinde `enabledStateIds` / `enabledTypeIds` / `enabledPriorityIds` (kaydet: `ocSaveWorkspaceEnabled*Ids`).

**Dataset taslağı:** `operationcore_datasets_phase1_draft_2026-05-26.json` → `enabledPriorityIds` eklendi; `build-operationcore-datasets-draft.mjs` patch güncellendi.

### 0.3 Workspace alan politikaları (`settings.fieldPolicies`)

Persist anahtarı: `OC_WORKSPACE_FIELD_POLICIES_SETTINGS_KEY` = `fieldPolicies`  
Model: `Mng.Ui/utils/ocWorkspaceFieldPolicies.ts`

| `kind` | Etki |
|--------|------|
| `visibility` | `visible` true/false |
| `readonly` | `readonly` true/false |
| `defaultValue` | `value` (şartlı veya her zaman) |

| `scope` | Anlam |
|---------|--------|
| `always` | Koşulsuz |
| `conditional` | `conditions.clauses[]` — alan + `eq`/`ne` + değer, **AND** |

**Şart alanları:** Havuz + çekirdek alanlar; `stateId` layout dışında da seçilebilir (`OC_POLICY_CONDITION_ALWAYS_CORE_KEYS`).

**UI bileşenleri:**

| Bileşen | Rol |
|---------|-----|
| `OcWorkspaceDefinitionsPoliciesTab.vue` | Sekme kabuğu |
| `OcWorkspaceFieldPolicyExplorer.vue` | Sol alan kataloğu, sağ politika listesi |
| `OcWorkspacePolicyDialog.vue` | Politika ekle/düzenle (tür menüsü) |
| `OcWorkspacePolicyConditionList.vue` | Şart satırları |
| `OcFormPolicyDefaultValueInput.vue` | Değer widget (tip, durum, öncelik, board, kişi, relation…) |
| `OcPersonPickerAutocomplete.vue` | Kişi seçimi (form + politika) |

**Liste özeti:** `formatWorkspaceFieldPolicySummary` — durum/tip/öncelik/board ID → ad; kişi alanları için `personTitleById` (Keeper).

**Runtime merge:** `settings.fieldPolicies` MO `FieldBehaviorResolver` ile birleştirilir (Odak deploy — MO-W ✅).

### 0.4 `op_rules` — Kurallar sekmesi ✅

| Konu | Durum |
|------|--------|
| UI | `OcWorkspaceDefinitionsRulesTab.vue` → `OcWorkspaceRulesExplorer.vue` |
| Persist | Ayrı dataset `op_rules` (validation + default: `setField`, `setAssignee`) |
| Tetikleyiciler | `WorkItemCreated`, `WorkItemUpdated`, `WorkItemTransition` |

**Ürün kararı (26 Mayıs 2026):** Validasyon ve olay kuralları (`op_rules`) **Politikalar sekmesinde değil**; ayrı üst sekme **Kurallar** altında yönetilir. Alan politikaları Politikalar sekmesinde kalır.

**Tamamlandı (28 Mayıs 2026 — R-UI):** `rules` üst sekmesi; Politikalar altındaki geçici panel kaldırıldı; Formlar → Alan politikaları bölümündeki link `?tab=rules` olarak güncellendi.

### 0.5 Admin UX yenileme — Politikalar + Kurallar ayrımı (28 Mayıs 2026)

**Amaç:** Politikalar sekmesini Kurallar ekranıyla aynı «rehberli admin» kalıbına taşımak; iki kavramın karışmasını önlemek.

| Konu | Politikalar | Kurallar |
|------|-------------|----------|
| Soru | Alan formda **nasıl görünsün / davransın?** | Olay anında **ne doğrulansın / otomatik olsun?** |
| Persist | `settings.fieldPolicies` | `op_rules` |
| Kaydı durdurur mu? | Hayır | Evet (validation) |
| UI | `OcWorkspaceFieldPolicyExplorer` | `OcWorkspaceRulesExplorer` |

**Politikalar (P-UX) — bileşenler:**

| Bileşen | UX öğeleri |
|---------|------------|
| `OcWorkspaceFieldPolicyExplorer.vue` | Hero; «Nasıl çalışır?» (3 adım); `vsRulesBanner`; istatistik chip’leri; sol alan kataloğu + sağ politika listesi; hızlı ekleme (Görünürlük / Salt okunur / Varsayılan); boş katalog/alan durumları; teknik dipnot |
| `OcWorkspacePolicyDialog.vue` | ~960px; 3 adım (Ne zaman → Etki → Şartlar); her zaman / koşullu alt açıklamalar; switch + ipuçları; sağda canlı önizleme; `vsRulesHint` |
| `OcWorkspaceDefinitionsPoliciesTab.vue` | Yalnızca explorer kabuğu (alan kataloğu yükler) |
| i18n | `operationCore.workspaceDefinitions.policies.*` — TR/EN |

**Kurallar (R-UX):** [OC_UI_RULES_FAZ1.md §8](./OC_UI_RULES_FAZ1.md).

**Paylaşılan:** `OcConditionClauseList.vue`, `ocConditionClauses.ts` — politika şartları `OcWorkspacePolicyConditionList` üzerinden aynı satır editörünü kullanır.

**DoD P-UX + R-UX:** Ürün sahibi onayı (28 Mayıs 2026).

---

## 1. Kavram — üç katman

| Katman | Soru | UI yeri | Metadata |
|--------|------|---------|----------|
| **Form alan politikaları** | Bu **form şablonunda** alan statik nasıl? | Formlar → Alan politikaları | `op_forms.fieldBehaviors`, `defaultValues` |
| **Workspace alan politikaları** | Bu **workspace’te** alan koşullu nasıl davransın? | Politikalar → alan explorer | `op_workspaces.settings.fieldPolicies` |
| **Olay kuralları** | Belirli **olayda** doğrula / ata / durdur? | **Kurallar** sekmesi | `op_rules` |

```text
Runtime merge (MO — hedef):
  Alan tanımı → Form/Profil → Workspace fieldPolicies → Board → State → Permission → op_rules
```

Form politikası **şablona özgü**. Workspace alan politikası **ortam geneli**. `op_rules` **olay motoru** (geçişte validation, setAssignee vb.).

---

## 2. Politikalar sekmesi — hedef yerleşim (güncel)

```text
[Genel] [Değerler ▾] [Akışlar] [Formlar] [Boards] [Politikalar] [Kurallar] [Zamanlanmış işler]

Politikalar sekmesi (P-UX):
┌─ Hero + «Nasıl çalışır?» + Kurallar ayrım bandı ─────────────────┐
├─────────────────────┬────────────────────────────────────────────┤
│ Alan kataloğu       │  Seçili alan — politikalar                  │
│ (enabled pool+core) │  [+ Görünürlük] [+ Salt okunur] [+ Varsayılan] │
└─────────────────────┴────────────────────────────────────────────┘
  Dialog: Ne zaman → Etki → Şartlar + canlı önizleme

Kurallar sekmesi (R-UX):
  Hero + filtreler + tablo + dialog (4 adım + canlı önizleme)
  op_rules CRUD — validation / default (setField, setAssignee)
```

Değerler sekmesi:

```text
[Değerler]
  ├─ İş tipleri      → enabledTypeIds + workspace scoped types
  ├─ Durumlar        → enabledStateIds
  ├─ Öncelikler      → enabledPriorityIds
  └─ Alanlar         → enabledFieldIds
```

---

## 3. Persist sözleşmesi — `fieldPolicies`

```json
{
  "fieldPolicies": {
    "policiesByField": {
      "assignee": [
        {
          "id": "...",
          "kind": "defaultValue",
          "scope": "conditional",
          "conditions": {
            "clauses": [
              { "id": "...", "fieldKey": "typeId", "operator": "eq", "value": "<typeId>" },
              { "id": "...", "fieldKey": "stateId", "operator": "eq", "value": "<stateId>" }
            ]
          },
          "value": "<userId>"
        }
      ]
    }
  }
}
```

Eski `visibilityByField` okunurken `policiesByField` formatına migrate edilir (`parseWorkspaceFieldPoliciesFromSettings`).

---

## 4. Form politikaları ile ilişki

> Form editöründeki **Alan politikaları** yalnızca o `op_forms` kaydına özgüdür. **Politikalar** sekmesindeki kurallar workspace genelindedir; runtime’da MO birleştirir.

Workspace politikası **layout’a alan eklemez**; yalnızca Değerler/Forms’ta zaten tanımlı alanlara uygulanır.

---

## 5. Uygulama fazları (güncel)

| Faz | İş | Durum |
|-----|-----|--------|
| W0 | `policies` sekmesi; `op_rules` Forms’tan taşındı | ✅ |
| W1 | Alan explorer + görünürlük / readonly / defaultValue + şartlı AND | ✅ |
| W1b | Kişi picker; özet metinde ad; katalog filtreleri; Değerler sekmesi | ✅ |
| **P-UX** | Politikalar explorer + dialog UX (Kurallar kalıbı) | ✅ |
| **R-UX** | Kurallar explorer + dialog UX | ✅ — [OC_UI_RULES_FAZ1.md §8](./OC_UI_RULES_FAZ1.md) |
| **MO-W** | `settings.fieldPolicies` merge runtime | ✅ |
| **R1** | `op_rules` → ayrı üst sekme (validasyon / otomasyon) | ✅ |
| W2 | Politika scope: `typeId` / `boardId` daraltma | backlog |
| W3 | Gelişmiş koşul builder | backlog |
| W4+ | `op_profiles` aynı hub kalıbı | backlog |

**Paralel:** Board/profil runtime ([DEVAM.md](../mngoperations/DEVAM.md) #4–5).

---

## 6. Kod indeksi

| Ne | Dosya |
|----|--------|
| Üst sekmeler | `composables/useOcWorkspaceDefinitionTabs.ts` |
| Değerler alt sekmeler | `composables/useOcWorkspaceDefinitionValuesTabs.ts`, `OcWorkspaceDefinitionsValuesTab.vue` |
| Tip / durum / öncelik / alan seçim UI | `OcWorkspaceDefinitionsTypesTab.vue`, `StatesTab.vue`, `PrioritiesTab.vue`, `FieldsTab.vue` |
| Servis — katalog filtre | `services/operationCoreService.ts` (`ocList*ForWorkspace`, `ocSaveWorkspaceEnabled*Ids`) |
| Politika modeli | `utils/ocWorkspaceFieldPolicies.ts` |
| Politika UI | `OcWorkspaceFieldPolicyExplorer.vue`, `OcWorkspacePolicyDialog.vue`, `OcWorkspacePolicyConditionList.vue` |
| Koşul (paylaşılan) | `OcConditionClauseList.vue`, `utils/ocConditionClauses.ts` |
| op_rules | `OcWorkspaceDefinitionsRulesTab.vue`, `OcWorkspaceRulesExplorer.vue`, `OcWorkspaceRuleDialog.vue` |
| op_rules (eski) | `OcWorkspaceFormRulesPanel.vue` — kullanılmıyor; silme backlog |
| Sayfa | `pages/apps/operation-core/admin/workspace-definitions/index.vue` |

---

## 7. Test checklist

1. **Değerler** → tip/durum/öncelik seç → kaydet → **Politikalar** / **Formlar** şart listesinde yalnızca seçilenler (boş seçimde politika/form `fallbackAll` davranışı).
2. Politikalar → assignee → şartlı varsayılan → listede **kişi adı** (ID değil).
3. Politikalar → şart `stateId` → değer combobox dolu (seçili durumlar veya fallback).
4. `settings.fieldPolicies` DG’de persist → sayfa yenile → politikalar geri gelir.
5. `?tab=states` eski link → Değerler / Durumlar alt sekmesine yönlenir.

---

## 8. Açık kararlar

| # | Soru | Karar / durum |
|---|------|----------------|
| A1 | `op_rules` nerede? | **Kurallar** üst sekmesi (R-UI ✅) |
| A2 | Politika `typeId`/`boardId` scope | W2 backlog |
| A3 | MO `fieldPolicies` merge | ✅ MO-W |
| A4 | Şartlı defaultValue geçişte assign | UI hazır; MO `op_rules` / merge ile tamamlanacak |

---

*Form politikaları: [OC_UI_FIELD_POLICY.md](./OC_UI_FIELD_POLICY.md). Handoff: [DEVAM.md](../mngoperations/DEVAM.md).*
