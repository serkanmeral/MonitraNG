# Operation Core — DG dataset dosyaları

| Dosya | Açıklama |
|-------|----------|
| [tedarikciler_dataset.json](./tedarikciler_dataset.json) | **Tedarikci** master — OC lookup demo |
| [tedarikciler_dataset_category.json](./tedarikciler_dataset_category.json) | Kategori **BusinessDatasets** |
| [operationcore_datasets_phase1_draft_2026-05-26.json](./operationcore_datasets_phase1_draft_2026-05-26.json) | Güncel `op_*` şema taslağı (20 dataset) |
| [operationcore_datasets_phase1_current_final_2026-05-25.json](./operationcore_datasets_phase1_current_final_2026-05-25.json) | Eski export (arşiv) |

**Token / kurulum:** [../scripts/README.md](../scripts/README.md) (Odak: `http://192.168.20.20:5040`, domain `odak`)

```powershell
.\docs\odak\operationcore\scripts\get-operationcore-token.ps1
.\docs\odak\operationcore\scripts\setup-operation-core-datasets.ps1
```

**Yeniden üretim:** `node docs/odak/operationcore/scripts/build-operationcore-datasets-draft.mjs`

Üst indeks: [../README.md](../README.md)
