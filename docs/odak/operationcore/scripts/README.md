# Operation Core — DG scriptleri (Odak)

**API Gateway:** `http://192.168.20.20:5040`  
**Domain:** `odak` · **Kullanici:** `odak_admin` (sifre: `get-operationcore-token.ps1` icinde)

| Script | Aciklama |
|--------|----------|
| [get-operationcore-token.ps1](./get-operationcore-token.ps1) | MngKeeper token (`/keeper/api/auth/token`) |
| [load-operationcore-token.ps1](./load-operationcore-token.ps1) | Token dosyasindan yukle |
| [setup-operation-core-datasets.ps1](./setup-operation-core-datasets.ps1) | Kategori + 20 `op_*` dataset |
| [patch-op-publish-mode-none.ps1](./patch-op-publish-mode-none.ps1) | Mevcut kurulum: `op_*` **`publish_mode: none`** (Q14) |
| [build-operationcore-datasets-draft.mjs](./build-operationcore-datasets-draft.mjs) | JSON taslagini kaynaktan uret |

| [seed-operation-core-demo.ps1](./seed-operation-core-demo.ps1) | Demo metadata: states, workspace, flow, type, form, board |
| [seed-operation-core-helpdesk-reference.ps1](./seed-operation-core-helpdesk-reference.ps1) | IT Help Desk referans workspace ([reference](../reference/IT_HELP_DESK_REFERENCE.md)) |
| [get-operationcore-token-prod.ps1](./get-operationcore-token-prod.ps1) | Token — **prod** (`192.168.20.8`) |
| [load-operationcore-token-prod.ps1](./load-operationcore-token-prod.ps1) | Prod token yükle |
| [setup-operation-core-datasets-prod.ps1](./setup-operation-core-datasets-prod.ps1) | Kategori + `op_*` dataset — **prod** |
| [resolve-odak-group-ids-prod.ps1](./resolve-odak-group-ids-prod.ps1) | `mng_odak.@groups` → `__dataId` (personGroups) |
| [seed-operation-core-monitrang-feedback.ps1](./seed-operation-core-monitrang-feedback.ps1) | **MonitraNG Geri Bildirim** workspace — yalnızca prod ([reference](../reference/MONITRANG_FEEDBACK_WORKSPACE.md)) |
| [seed-operation-core-helpdesk-prod.ps1](./seed-operation-core-helpdesk-prod.ps1) | **IT Destek** workspace — yalnızca prod ([reference](../reference/IT_HELP_DESK_WORKSPACE.md)) |
| [patch-oc-side-menu.ps1](./patch-oc-side-menu.ps1) | `@side_menu`: Operasyon Merkezi, Bekleyen onaylar (üst seviye), Tanımlamalar (OC admin) |

Alarm Merkezi ve Otomasyon Merkezi ayrı script'ler:

| Script | Konum |
|--------|--------|
| `patch-alarm-center-side-menu.ps1` | [../../alarm/scripts/](../../alarm/scripts/patch-alarm-center-side-menu.ps1) |
| `patch-automation-side-menu.ps1` | [../../automation/scripts/](../../automation/scripts/patch-automation-side-menu.ps1) |

```powershell
# Repo kokunden
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\setup-operation-core-datasets.ps1
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1
.\docs\odak\operationcore\scripts\patch-oc-side-menu.ps1   # @side_menu (onay + alarm dahil)
# Opsiyonel: MngOperations calisiyorsa tam smoke
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 -SmokeTest
```

Dataset JSON: [../datasets/README.md](../datasets/README.md)
