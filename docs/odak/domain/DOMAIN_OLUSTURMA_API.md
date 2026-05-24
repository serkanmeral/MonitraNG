# Domain oluşturma — Keeper API (UI olmadan)

MngDomainUI kullanmadan doğrudan **MngKeeper** `POST /api/domain` ile aynı pipeline tetiklenir.

**Odak:** http://192.168.20.20:5001

---

## Ön koşullar

[DOMAIN_OLUSTURMA.md](./DOMAIN_OLUSTURMA.md) — Bölüm 3 (servisler, `.env`, Keycloak) ile aynı.

---

## Örnek istek

```bash
curl -s -X POST http://192.168.20.20:5001/api/domain \
  -H "Content-Type: application/json" \
  -d '{
    "domainName": "testodak",
    "displayName": "Test Odak",
    "adminEmail": "admin@testodak.local",
    "adminPassword": "Admin123!@#",
    "relatedPersonEmail": "ops@example.com",
    "settings": {
      "maxUsers": 100,
      "maxAssets": 1000,
      "enableMqtt": false
    }
  }'
```

Şablon ile:

```json
{
  "domainName": "demo",
  "displayName": "Demo Domain",
  "adminEmail": "admin@demo.local",
  "adminPassword": "Admin123!@#",
  "initialDataTemplateName": "SABLON_ADI"
}
```

---

## Başarılı yanıt (örnek)

```json
{
  "isSuccess": true,
  "domainId": "...",
  "domainName": "testodak",
  "databaseName": "mng_testodak",
  "adminUsername": "...",
  "adminEmail": "admin@testodak.local",
  "createdAt": "..."
}
```

`isSuccess: false` ise `errorMessage` ve `failedStep` alanlarına bakın.

---

## PowerShell (Windows)

```powershell
$body = @{
  domainName    = "testodak"
  displayName   = "Test Odak"
  adminEmail    = "admin@testodak.local"
  adminPassword = "Admin123!@#"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://192.168.20.20:5001/api/domain" `
  -Method POST -Body $body -ContentType "application/json"
```

---

## Repo script

Geliştirme/test için örnek: `MngKeeper/tests/create-meral-domain.ps1` (domain + mapper + test verisi).

---

## İlgili

- [DOMAIN_OLUSTURMA.md](./DOMAIN_OLUSTURMA.md) — UI adımları
