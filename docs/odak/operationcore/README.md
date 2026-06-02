# Operation Core (OC)

MonitraNG operasyonel iş yönetim modülü. Backend servisi: **MngOperations**.

**Sözlük:** Workspace (eski: Project) · WorkItem (eski: Task/Issue) · State (eski: Status)

---

## Dokümanlar

| Dosya | Rol | Durum |
|-------|-----|--------|
| [major_plan.md](./major_plan.md) | MonitraNG stratejik vizyon ve fazlar (platform geneli) | Mevcut |
| [operationcore_phase1.md](./operationcore_phase1.md) | OC Faz 1 mimari ve runtime modeli (spec) | Mevcut |
| [datasets/README.md](./datasets/README.md) | DG kategori + `op_*` şema dosyaları | Güncel |
| [OPERATION_CORE_IMPLEMENTATION_PLAN.md](./OPERATION_CORE_IMPLEMENTATION_PLAN.md) | Uygulama planı (üst çerçeve) | §5.2 tamam |
| [mngoperations/](./mngoperations/README.md) | **MngOperations** backend — Faz 1 MVP + Odak deploy | Güncel |
| [ui/](./ui/README.md) | **Mng.Ui OC** Faz 1 planı + form tanımı handoff | Güncel |
| [ui/OC_UI_FIELD_POLICY.md](./ui/OC_UI_FIELD_POLICY.md) | Form **alan politikaları** + backlog | Güncel |
| [ui/OC_UI_WORKSPACE_POLICIES.md](./ui/OC_UI_WORKSPACE_POLICIES.md) | **Workspace politikaları + Değerler** · P-UX / R-UX (§0.5) | Güncel |
| [mngoperations/DEVAM.md](./mngoperations/DEVAM.md) | **Devam noktası** — Admin Faz 1 plan | Güncel |
| [ui/OC_UI_ADMIN_FAZ1_PLAN.md](./ui/OC_UI_ADMIN_FAZ1_PLAN.md) | **Admin UX Faz 1** — sıra + bugün | Güncel |
| [ui/OC_UI_RULES_FAZ1.md](./ui/OC_UI_RULES_FAZ1.md) | **Kurallar** R-Core + R-UX (§8) | Güncel |
| [mngoperations/SLA_FAZ1_PLAN.md](./mngoperations/SLA_FAZ1_PLAN.md) | SLA planlama (geliştirme Faz 1.5+) | Güncel |
| [../diagnostic/PERFORMANCE_ROADMAP.md](../diagnostic/PERFORMANCE_ROADMAP.md) | **Performans yol haritası** — Faz 1+1B UI ✅; Faz 2 backend bekliyor | Güncel |
| [reference/IT_HELP_DESK_REFERENCE.md](./reference/IT_HELP_DESK_REFERENCE.md) | IT Help Desk referans workspace + tip kategorileri | Güncel |

---

## DG kurulum (Odak)

**API Gateway:** `http://192.168.20.20:5040` · **Domain:** `odak` · Scriptler: [scripts/](./scripts/)

1. [scripts/get-operationcore-token.ps1](./scripts/get-operationcore-token.ps1) — kimlik bilgileri dosya icinde  
2. [scripts/setup-operation-core-datasets.ps1](./scripts/setup-operation-core-datasets.ps1) — kategori + 20 dataset  
3. Şema yeniden üretmek: `node docs/odak/operationcore/scripts/build-operationcore-datasets-draft.mjs`

**Kategori adı:** `OperationCoreDatasets` — tüm `op_*` dataset'lerinde `category` bu kategoriye bağlanır (setup script çalışma anında DG `__dataId` çözer).

### Taslak revizyonları (2026-05-26)

- `op_rules.transitionKey`, state flow transition kataloğu açıklaması
- `op_work_items.key` → text (MngOperations üretir); `sourceModule*` kaldırıldı; `slaPolicyId` → relation
- `op_workspaces.enabledTypeIds` / `enabledStateIds` / `enabledPriorityIds` / `enabledFieldIds`
- `op_work_item_types` / `op_fields` → isteğe bağlı `workspaceId`
- `op_work_item_timelines.transitionKey`
- `op_work_items` → 5 predefined query
- Tüm dataset'ler → kategori **OperationCoreDatasets**

---

## İlgili repo kaynakları

| Alan | Konum |
|------|--------|
| Eski Task Manager UI (referans) | `Mng.Ui/pages/apps/task-manager/`, `docs/content/task_manager/TASK_MANAGER_PLANNING.md` |
| TM dataset setup | `scripts/tests/MngDataGateway/task-manager/setup-task-manager-datasets.ps1` |
| OC dataset setup | `docs/odak/operationcore/scripts/setup-operation-core-datasets.ps1` |
| MngOperations plan | `docs/odak/operationcore/mngoperations/` |
| MngOperations kod | `MngOperations/` (solution) |

---

## Hızlı bağlantılar

- Üst odak indeksi: [../README.md](../README.md)
- Tam kurulum: [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md)
