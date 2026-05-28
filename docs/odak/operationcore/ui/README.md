# Operation Core — UI planlama (Mng.Ui)

**Son güncelleme:** 28 Mayıs 2026  
**Backend durumu:** MngOperations Faz 1 MVP + Odak deploy ([../mngoperations/DEVAM.md](../mngoperations/DEVAM.md))  
**UI handoff:** [DEVAM.md](../mngoperations/DEVAM.md) · Politikalar + Kurallar UX: [OC_UI_WORKSPACE_POLICIES.md §0.5](./OC_UI_WORKSPACE_POLICIES.md) · Kurallar detay: [OC_UI_RULES_FAZ1.md §8](./OC_UI_RULES_FAZ1.md)

| Doküman | Konu |
|---------|------|
| [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md) | **Güncel** — `op_forms` editör, önizleme, `OcDynamicForm`, yeni iş sayfası |
| [OC_UI_FIELD_POLICY.md](./OC_UI_FIELD_POLICY.md) | **Form** alan politikaları (statik) |
| [OC_UI_WORKSPACE_POLICIES.md](./OC_UI_WORKSPACE_POLICIES.md) | **Workspace** alan politikaları + **Değerler** sekmesi + tamamlanan işler (§0) |
| [OC_UI_ADMIN_FAZ1_PLAN.md](./OC_UI_ADMIN_FAZ1_PLAN.md) | **Admin Faz 1** — sıra, bugün, kapanış checklist |
| [OC_UI_RULES_FAZ1.md](./OC_UI_RULES_FAZ1.md) | **Kurallar** (`op_rules`) tamamlama |
| [OC_UI_SCHEDULED_WORK_ITEMS.md](./OC_UI_SCHEDULED_WORK_ITEMS.md) | **Zamanlanmış işler** — workspace tanım + cron (plan) |
| [OC_UI_PHASE1_PLAN.md](./OC_UI_PHASE1_PLAN.md) | **Ana plan** — route, ekranlar, bileşenler, sprint sırası |
| [OC_UI_NAVIGATION_AND_TM_INSPIRATION.md](./OC_UI_NAVIGATION_AND_TM_INSPIRATION.md) | **TM ilhamı**, side menu, breadcrumb (baştan tasarım) |
| [../mngoperations/FORM_LAYOUT_AND_EXTRA_FIELDS.md](../mngoperations/FORM_LAYOUT_AND_EXTRA_FIELDS.md) | Backend `layout` JSON + `extraFields` |
| [../operationcore_phase1.md](../operationcore_phase1.md) | OC spec — “Backend decides, UI renders” |
| [../mngoperations/RUNTIME_CONTEXT.md](../mngoperations/RUNTIME_CONTEXT.md) | Runtime DTO sözleşmesi |
| [../mngoperations/API_SURFACE.md](../mngoperations/API_SURFACE.md) | Komut + runtime API |
| [../../content/task_manager/TASK_MANAGER_PLANNING.md](../../content/task_manager/TASK_MANAGER_PLANNING.md) | TM referans (adaptasyon, kopyalanmayacak mantık) |
| [../../ui/WELCOME_HOME.md](../../ui/WELCOME_HOME.md) | Ana sayfa modül kartı ekleme |

**Odak smoke (UI geliştirme öncesi doğrulandı):**

```powershell
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 -SmokeTest `
  -MoBaseUrl "http://192.168.20.20:5040/operations"
```

Demo workspace/board id’leri: [../scripts/operationcore-demo-seed.json](../scripts/operationcore-demo-seed.json)
