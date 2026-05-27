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
| [patch-oc-side-menu.ps1](./patch-oc-side-menu.ps1) | `@side_menu`: Operasyon Merkezi menu maddesi |

```powershell
# Repo kokunden
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\setup-operation-core-datasets.ps1
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1
# Opsiyonel: MngOperations calisiyorsa tam smoke
.\docs\odak\operationcore\scripts\seed-operation-core-demo.ps1 -SmokeTest
```

Dataset JSON: [../datasets/README.md](../datasets/README.md)
