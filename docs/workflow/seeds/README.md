# Workflow — Seeds

Lokal Docker Desktop / lab ortamı için **örnek workflow tanımları**, **dataset seed** ve **demo senaryo** script’leri burada tutulur.

## Kurallar

- Script’ler mümkünse PowerShell 7+ (`*.ps1`).
- Token: `scripts/tests/MngDataGateway/auth/load-token.ps1` veya eşdeğeri (relative path).
- Secret / gerçek bot token / production URL yazılmaz.
- İsimlendirme: `seed-*.ps1`, `setup-*.ps1`.

## Mevcut (henüz taşınmamış) seed’ler

| Konum | Açıklama |
|-------|----------|
| [scripts/tests/MngDataGateway/workflow/setup-wf-validation-pipelines.ps1](../../../scripts/tests/MngDataGateway/workflow/setup-wf-validation-pipelines.ps1) | `@wf_validation_pipelines` + TM örnek pipeline |
| [scripts/odak/test-alarm-lifecycle-e2e.ps1](../../../scripts/odak/test-alarm-lifecycle-e2e.ps1) | Alarm → workflow E2E (örnek tanım üretir) |
| [scripts/odak/test-parallel-fork-e2e.ps1](../../../scripts/odak/test-parallel-fork-e2e.ps1) | Parallel fork E2E |

Yeni seed’ler bu klasöre eklenecek; gerekirse eski script’lere buradan link verilir (zorunlu taşıma yok).
