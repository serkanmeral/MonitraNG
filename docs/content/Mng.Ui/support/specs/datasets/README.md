# Dataset Şema Dosyaları

Bu klasör, UI tarafında kullanılan dataset'lerin **oluşturma (create)** şemalarını JSON olarak tutar.

## @dashboards

**Dosya:** `dashboards-dataset-create.json`

**Kullanım:** MngDataGateway `POST /api/v1/datasets` ile `@dashboards` dataset'ini oluşturmak için.

### cURL örneği

```bash
curl -X POST "https://localhost:5010/api/v1/datasets" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d @dashboards-dataset-create.json
```

### Notlar

- Token: MngKeeper üzerinden alınır (`scripts/tests/MngDataGateway/auth/load-token.ps1` vb.).
- API base URL ortama göre değişir (localhost, docker, production).
- `category` zorunlu değil; kullanmak isterseniz önce bir dataset category oluşturup ID'sini ekleyebilirsiniz.
