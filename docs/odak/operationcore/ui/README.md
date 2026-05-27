# Operation Core — UI planlama (Mng.Ui)

**Son güncelleme:** 26 Mayıs 2026  
**Backend durumu:** MngOperations Faz 1 MVP + Odak deploy ([../mngoperations/DEVAM.md](../mngoperations/DEVAM.md))  
**UI handoff:** Form tanımı + create — [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md)

| Doküman | Konu |
|---------|------|
| [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md) | **Güncel** — `op_forms` editör, önizleme, `OcDynamicForm`, yeni iş sayfası |
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
