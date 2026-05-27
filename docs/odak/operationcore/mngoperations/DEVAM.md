# MngOperations & Operation Core UI — Devam noktası (checkpoint)

**Son güncelleme:** 26 Mayıs 2026 (mola öncesi)  
**Durum:** Faz 1 backend MVP + Odak deploy; **UI workspace form tanımı + create akışı** büyük ölçüde tamam; profil/board runtime ve kalan form iyileştirmeleri sırada.

Bu dosya **yeni Cursor chat**’te kaldığınız yerden devam için ana handoff’tur.

---

## Yeni chat — hızlı özet

1. **Ne yaptık:** `op_forms` admin (4 sekme), layout grid, taslak önizleme, `OcDynamicForm` widget’ları, `work-items/new`, MO `extraFields` + `FormRuntimeBuilder` layout sırası.
2. **Sıradaki:** (2) kullanıcı/atanan select → (3) zorunlu alan UI → profil/board runtime (S2–S4).
3. **Dokümanlar:** [FORM_LAYOUT_AND_EXTRA_FIELDS.md](./FORM_LAYOUT_AND_EXTRA_FIELDS.md), [ui/OC_UI_FORM_DEFINITIONS.md](../ui/OC_UI_FORM_DEFINITIONS.md)
4. **Deploy:** Layout/MO değişikliği sonrası `deploy-odak-apps.ps1 -Services mngoperations`

---

## 1. Backend (MngOperations) — tamamlanan (bu dönem)

| Alan | Durum | Doküman |
|------|--------|---------|
| Faz 1 komut + runtime API | ✅ | [API_SURFACE.md](./API_SURFACE.md) |
| `extraFields` bucket (§8.5) | ✅ | [FORM_LAYOUT_AND_EXTRA_FIELDS.md](./FORM_LAYOUT_AND_EXTRA_FIELDS.md) |
| `WorkItemFieldWriter`, `FormLayoutHelper`, `FormRuntimeBuilder` | ✅ | aynı |
| Rule engine, pipeline, notifications | ✅ | [PIPELINES.md](./PIPELINES.md) |
| Odak deploy + smoke | ✅ (önceki oturum) | [GATEWAY_AND_DEPLOY.md](./GATEWAY_AND_DEPLOY.md) |

**Build:** `dotnet build MngOperations/MngOperations.sln`

**Metadata cache:** Form kayıtları ~30 sn+ TTL — UI’da DG’ye kayıt sonrası «Yeni iş» gecikebilir; taslak önizleme cache kullanmaz.

---

## 2. UI (Mng.Ui Operation Core) — tamamlanan (bu dönem)

| Alan | Durum |
|------|--------|
| Workspace tanımları (genel, tipler, alanlar, akış) | ✅ (önceki sprintler) |
| **Forms** sekmesi — `op_forms` CRUD | ✅ |
| Form editör: Genel / Yerleşim / Davranışlar / Varsayılan değerler | ✅ |
| Layout: section/field order, `fieldCols`, `sectionCols`, `dialogMaxWidth` | ✅ |
| `OcFormPreviewDialog` + `buildFormPreviewContextFromDraft` | ✅ |
| `OcDynamicForm` + alan tipi widget’ları | ✅ |
| `work-items/new` + `ocCreateWorkItem` | ✅ |
| Boards sekmesi temel CRUD | ✅ |
| Profil / board **runtime** ekranları | ⏳ placeholder (Sprint 2–3) |

Detay: [ui/OC_UI_FORM_DEFINITIONS.md](../ui/OC_UI_FORM_DEFINITIONS.md)

---

## 3. Nerede kaldık? (görsel ilerleme)

```text
[✓] MO Faz 1 backend + deploy
[✓] extraFields + form layout runtime (MO)
[✓] UI workspace definitions — forms/boards
[✓] UI form editor + taslak preview + dynamic widgets
[✓] UI work-items/new (create)
[ ] UI — assignee/user select (sıradaki #2)
[ ] UI — required field indicators (#3)
[ ] UI — board kanban/list runtime (S2)
[ ] UI — work item profile runtime (S3)
[ ] op_forms advanced schema (modal, visibilityRules, …)
```

---

## 4. Sıradaki işler (onaylanmış sıra)

| # | İş | Not |
|---|-----|-----|
| **2** | **İlişki / kullanıcı select** | `assignee`, `reporter`, `watchers` — Keeper veya DG kullanıcı listesi |
| **3** | **Zorunlu alan göstergesi** | `fieldBehaviors.required` → yıldız, submit özeti |
| 4 | Board runtime | `ocGetBoardContext`, kolon sorguları, kartlar |
| 5 | Profil runtime | transition actions, timeline embed |
| 6 | `op_forms` ileri alanlar | modal, visibilityRules, panels (admin UI) |
| 7 | Editör yan panel canlı önizleme | TM `ProjectIssueCreateLayoutEditor` benzeri (opsiyonel) |
| 8 | MO vs taslak runtime karşılaştırma | Opsiyonel toggle |

**Form tanımı** kullanıcı kararı: admin + MO runtime + create yolu yeterli sayılana kadar profil/board’a geçmeyin.

---

## 5. Önemli kod yolları

### Backend (`MngOperations/`)

| Ne | Dosya |
|----|--------|
| extraFields yazma | `Core/.../Utilities/WorkItemFieldWriter.cs` |
| Layout sırası | `Core/.../Utilities/FormLayoutHelper.cs` |
| Form runtime fields | `Core/.../Utilities/FormRuntimeBuilder.cs` |
| Form context | `Infrastructure/.../RuntimeContextService.cs` |
| Create API | `Presentation/.../Controllers/WorkItemsController.cs` |

### UI (`Mng.Ui/`)

| Ne | Dosya |
|----|--------|
| Forms tab | `components/.../OcWorkspaceDefinitionsFormsTab.vue` |
| Layout editor | `components/.../OcWorkspaceFormLayoutEditor.vue` |
| Preview | `components/.../OcFormPreviewDialog.vue` |
| Dynamic form | `components/.../OcDynamicForm.vue`, `OcDynamicFormField.vue` |
| Create page | `pages/.../work-items/new/index.vue` |
| OC service | `services/operationCoreService.ts` |

---

## 6. Hızlı komutlar

```powershell
# MO build
dotnet build MngOperations\MngOperations.sln

# UI dev
cd Mng.Ui
npm run dev

# Odak deploy (MO)
.\scripts\odak\sync-odak-source.ps1
.\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations

# Token + demo seed
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 -SmokeTest `
  -MoBaseUrl "http://192.168.20.20:5040/operations"
```

---

## 7. Doküman indeksi

| Doküman | Rol |
|---------|-----|
| **Bu dosya** | Handoff / DEVAM |
| [FORM_LAYOUT_AND_EXTRA_FIELDS.md](./FORM_LAYOUT_AND_EXTRA_FIELDS.md) | Backend layout + extraFields |
| [ui/OC_UI_FORM_DEFINITIONS.md](../ui/OC_UI_FORM_DEFINITIONS.md) | UI form editor + preview |
| [RUNTIME_CONTEXT.md](./RUNTIME_CONTEXT.md) | Runtime DTO |
| [ui/OC_UI_PHASE1_PLAN.md](../ui/OC_UI_PHASE1_PLAN.md) | Sprint planı |
| [operationcore_phase1.md](../operationcore_phase1.md) | Spec |
| [MVP_CHECKLIST.md](./MVP_CHECKLIST.md) | Backend MVP checklist |

---

## 8. Yeni chat’te ilk mesaj önerisi

> Operation Core form tanımına devam: `docs/odak/operationcore/mngoperations/DEVAM.md` ve `ui/OC_UI_FORM_DEFINITIONS.md` oku. Sıradaki iş #2: assignee/user select, sonra #3 zorunlu alan göstergesi.

---

*Mola sonrası bu dosyayı ilerleme kaydettikçe güncelleyin.*
