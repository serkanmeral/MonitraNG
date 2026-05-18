# tm_issues ↔ MngWorkflow bağlantısı

**Amaç:** Issue oluşturma/güncellemeden önce `projectKey` alanının, `projectId` ile seçilen `tm_projects` kaydının `key` alanı ile eşleştiğini doğrulamak.

## 1. Önkoşullar

- `tm_projects`, `tm_issues` dataset’leri mevcut (`setup-task-manager-datasets.ps1`).
- **MngWorkflow** API çalışır (ör. `http://localhost:5085`; Gateway: `/workflow/api/v1/...`).
- `@wf_validation_pipelines` dataset’i ve `tm_issues_project_key` pipeline kaydı oluşturulmuş:

```powershell
.\scripts\tests\MngDataGateway\workflow\setup-wf-validation-pipelines.ps1
```

## 2. tm_issues HTTP validation (dataset şeması)

MngDataGateway’de `tm_issues` dataset tanımına **`validations`** içinde bir **HTTP** kuralı ekleyin (Admin UI veya Dataset API ile PUT).

| Alan | Değer |
|------|--------|
| `type` | `http` |
| `name` | `workflow_tm_issues_project_key` |
| `method` | `POST` |
| `when` | `both` (create + update) |
| `url` | Gateway üzerinden: `https://localhost:5040/workflow/api/v1/validate/tm_issues` |

**Not:** DG, doğrulama isteğinde **Authorization: Bearer** (kullanıcı JWT) iletir; MngWorkflow aynı token ile DG’den pipeline ve `tm_projects` okur. Domain, JWT `domain_name` claim’inden alınır.

**Docker / compose:** `localhost` yerine Gateway servis adresini kullanın; TLS ve iç ağ adresini ortamınıza göre ayarlayın.

## 3. Örnek `validations` parçası (referans)

```json
{
  "validations": [
    {
      "name": "workflow_tm_issues_project_key",
      "description": "projectKey ile projectId uyumu (MngWorkflow)",
      "type": "http",
      "url": "https://localhost:5040/workflow/api/v1/validate/tm_issues",
      "method": "POST",
      "when": "both",
      "order": 0,
      "timeoutSeconds": 15
    }
  ]
}
```

Mevcut şemadaki diğer alanlarla birleştirerek dataset’i güncelleyin; yalnızca `validations` eklemek yeterli değilse tüm şema gövdesini PUT ile gönderin.

## 4. Pipeline mantığı (@wf_validation_pipelines)

`tm_issues_project_key` kaydı:

1. **fetch:** `tm_projects` içinde `__dataId` = payload `projectId`
2. **assert:** `result.key == payload.projectKey`
3. Başarısızlıkta DG’ye `isValid: false` ve mesaj döner.

## 5. Sorun giderme

| Belirti | Olası neden |
|--------|--------------|
| 400 Domain not found | JWT’de `domain_name` yok; veya Workflow’a `X-Domain-Name` header (sadece test). |
| Pipeline bulunamadı | `@wf_validation_pipelines` içinde `dataset:eq:tm_issues` ile kayıt yok. |
| fetch null | `projectId` payload’da yok veya yanlış. |
| DG HTTP validation atlanıyor | `tm_issues` şemasında `validations` kaydı yok veya `when` uyumsuz. |

---

**İlgili:** [WORKFLOW_PLANNING.md](WORKFLOW_PLANNING.md), [Task Manager planlama](../task_manager/TASK_MANAGER_PLANNING.md)
