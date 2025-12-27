# API Versioning Documentation

**Date:** 26 Aralık 2025  
**Status:** ✅ COMPLETED

---

## 📋 Overview

MngDataGateway API'ye versioning desteği eklendi. MngHub ile uyumlu şekilde URL, Header ve Query string based versioning destekleniyor.

---

## 🎯 Özellikler

- ✅ URL-based versioning (`/api/v1/...`)
- ✅ Query string-based versioning (`?version=1.0`)
- ✅ Header-based versioning (`Api-Version: 1.0`)
- ✅ Default version: v1.0
- ✅ Swagger/OpenAPI entegrasyonu
- ✅ Version-specific Swagger documents

---

## 📝 Version Belirtme Yöntemleri

### 1. URL Segment (Recommended)

```
GET /api/v1/data/tst_books
POST /api/v1/data/tst_books
GET /api/v1/datasets
```

### 2. Query String

```
GET /api/data/tst_books?version=1.0
POST /api/data/tst_books?version=1.0
```

### 3. Header

```
GET /api/data/tst_books
Headers:
  Api-Version: 1.0
```

---

## 🔧 Implementation Details

### NuGet Paketleri

- `Asp.Versioning.Mvc` v8.1.0
- `Asp.Versioning.Mvc.ApiExplorer` v8.1.0

### Program.cs Configuration

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
        new Asp.Versioning.QueryStringApiVersionReader("version"),
        new Asp.Versioning.HeaderApiVersionReader("Api-Version"),
        new Asp.Versioning.UrlSegmentApiVersionReader()
    );
})
.AddMvc()
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

### Controller Attributes

Tüm controller'lara version attribute eklendi:

```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/data/{datasetName}")]
public class DataController : ControllerBase
{
    // ...
}
```

### Updated Controllers

- ✅ `DataController` - `/api/v1/data/{datasetName}`
- ✅ `DatasetsController` - `/api/v1/datasets`
- ✅ `DatasetCategoriesController` - `/api/v1/dataset-categories`
- ✅ `HealthController` - `/api/v1/health`
- ✅ `VersionController` - `/api/v1/version`

---

## 📚 Swagger/OpenAPI Integration

### SwaggerConfigureOptions

**Dosya:** `MngDataGateway.Api/Config/SwaggerConfigureOptions.cs`

Her API version için ayrı Swagger document oluşturuluyor.

### Swagger UI

- Version selector: Swagger UI'da tüm version'lar listeleniyor
- Document path: `/api-docs/{documentName}/swagger.json`
- Example: `/api-docs/v1.0/swagger.json`

---

## 🔄 Migration Guide

### Önceki Endpoint'ler (Artık desteklenmiyor)

```
❌ GET /api/data/tst_books
❌ POST /api/datasets
```

### Yeni Endpoint'ler

```
✅ GET /api/v1/data/tst_books
✅ POST /api/v1/datasets
```

**Not:** Default version v1.0 olduğu için, version belirtilmezse otomatik olarak v1.0 kullanılır. Ancak explicit version belirtmek recommended'dır.

---

## 🚀 Örnekler

### cURL Examples

```bash
# URL-based versioning
curl -X GET "https://localhost:5010/api/v1/data/tst_books" \
  -H "Authorization: Bearer {token}"

# Query string-based versioning
curl -X GET "https://localhost:5010/api/data/tst_books?version=1.0" \
  -H "Authorization: Bearer {token}"

# Header-based versioning
curl -X GET "https://localhost:5010/api/data/tst_books" \
  -H "Authorization: Bearer {token}" \
  -H "Api-Version: 1.0"
```

### PowerShell Examples

```powershell
# URL-based (recommended)
Invoke-RestMethod -Uri "https://localhost:5010/api/v1/data/tst_books" `
  -Headers @{ "Authorization" = "Bearer $token" }

# Query string-based
Invoke-RestMethod -Uri "https://localhost:5010/api/data/tst_books?version=1.0" `
  -Headers @{ "Authorization" = "Bearer $token" }

# Header-based
Invoke-RestMethod -Uri "https://localhost:5010/api/data/tst_books" `
  -Headers @{ 
    "Authorization" = "Bearer $token"
    "Api-Version" = "1.0"
  }
```

---

## 📝 Version Management

### Yeni Version Eklemek

1. Controller'a yeni version attribute ekle:
```csharp
[ApiVersion(1.0)]
[ApiVersion(2.0)]  // Yeni version
[Route("api/v{version:apiVersion}/data/{datasetName}")]
public class DataController : ControllerBase
{
    // ...
}
```

2. Swagger document otomatik olarak oluşturulur

### Deprecation

Version deprecation için `DeprecatedApiVersion` attribute kullanılabilir (gelecekte eklenecek).

---

## ✅ Test Durumu

- ✅ Build: Başarılı
- ✅ Swagger UI: Version selector çalışıyor
- ✅ OpenAPI documents: Version-specific documents oluşturuluyor
- ✅ Endpoint'ler: Tüm endpoint'ler versioning ile çalışıyor

---

**Son Güncelleme:** 26 Aralık 2025  
**Durum:** ✅ Tamamlandı ve Build Edildi

