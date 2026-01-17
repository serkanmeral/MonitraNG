# Documentation Provider Test Rehberi

Bu rehber, Faz 1'de implement edilen `DocumentationProvider`'ı test etmek için adım adım talimatlar içerir.

## Ön Gereksinimler

1. **MngLLM servisi çalışıyor olmalı**
2. **Markdown dosyaları mevcut**: `docs/content/` klasöründe
3. **Authentication**: Eğer servis authentication gerektiriyorsa, JWT token gerekli

## Test Senaryoları

### Senaryo 1: Servisi Çalıştırma

#### Adım 1: Servisi Başlat

```powershell
cd c:\Serkan\iSIM\MonitraNG\MngLLM\Presentation\MngLLM.Api
dotnet run
```

Veya Visual Studio/Rider'dan `MngLLM.Api` projesini çalıştırın.

**Beklenen Çıktı:**
- Servis `http://localhost:5030` üzerinde çalışmalı
- Log'larda "MngLLM Starting" mesajı görünmeli

#### Adım 2: Servis Durumunu Kontrol Et

Tarayıcıda veya PowerShell'de:

```powershell
# HTTP endpoint (eğer varsa)
Invoke-WebRequest -Uri "http://localhost:5030/health"

# Swagger UI
Start-Process "http://localhost:5030/swagger"
```

### Senaryo 2: Otomatik Test Script'i

#### Adım 1: Test Script'ini Çalıştır

```powershell
cd c:\Serkan\iSIM\MonitraNG\MngLLM\tests
.\test-documentation-provider.ps1 -BaseUrl "http://localhost:5030" -SkipAuth
```

**Not:** Eğer authentication aktifse, token ile:

```powershell
.\test-documentation-provider.ps1 -BaseUrl "http://localhost:5030" -Token "YOUR_JWT_TOKEN"
```

#### Beklenen Sonuçlar:

1. **✓ Service is running** - Servis çalışıyor
2. **✓ Found X indexed documents** - İndekslenmiş dokümantasyonlar bulundu
3. **✓ Re-indexing completed** - Re-indexing başarılı
4. **✓ Search queries return results** - Arama sorguları sonuç döndürüyor
5. **✓ Document content retrieved** - Dokümantasyon içeriği alındı

### Senaryo 3: Manuel API Testleri

#### Test 1: Tüm Dokümantasyonları Listele

```powershell
# PowerShell
$response = Invoke-RestMethod -Uri "http://localhost:5030/api/v1/docs" -Method GET
$response | ConvertTo-Json -Depth 5
```

**Beklenen:** JSON array of `DocumentationIndex` objects

#### Test 2: Re-index

```powershell
# PowerShell
$response = Invoke-RestMethod -Uri "http://localhost:5030/api/v1/docs/reindex" -Method POST
$response | ConvertTo-Json
```

**Beklenen:**
```json
{
  "message": "Re-indexing completed successfully"
}
```

**Log Kontrolü:**
- Console'da "Starting documentation re-indexing..." mesajı görünmeli
- "Indexing markdown files from: ..." mesajı görünmeli
- "Indexed X markdown files" mesajı görünmeli
- "Documentation re-indexing completed. Total documents: X" mesajı görünmeli

#### Test 3: Arama Yap

```powershell
# PowerShell
$query = "user management"
$encodedQuery = [System.Web.HttpUtility]::UrlEncode($query)
$response = Invoke-RestMethod -Uri "http://localhost:5030/api/v1/docs/search?query=$encodedQuery&limit=5" -Method GET
$response | ConvertTo-Json -Depth 5
```

**Beklenen:** JSON array of `DocumentationResult` objects with:
- `Id`: Document identifier
- `Title`: Document title
- `Snippet`: Content snippet
- `RelevanceScore`: 0-1 arası skor
- `Service`: Service name
- `Category`: Category

**Örnek Sorgular:**
- "user management" → User Management guide'ı bulmalı
- "dataset" → Dataset ile ilgili dokümantasyonlar
- "authentication" → Authentication guide'ı
- "api" → API dokümantasyonları
- "architecture" → Architecture guide'ları

#### Test 4: Dokümantasyon İçeriği Al

```powershell
# PowerShell
# Önce bir document ID al
$allDocs = Invoke-RestMethod -Uri "http://localhost:5030/api/v1/docs" -Method GET
$docId = $allDocs[0].Id

# İçeriği al
$encodedId = [System.Web.HttpUtility]::UrlEncode($docId)
$response = Invoke-RestMethod -Uri "http://localhost:5030/api/v1/docs/$encodedId" -Method GET
$response.content
```

**Beklenen:** Markdown içeriği (plain text)

### Senaryo 4: Swagger UI ile Test

1. Tarayıcıda `http://localhost:5030/swagger` açın
2. `DocumentationController` endpoint'lerini bulun
3. Her endpoint'i "Try it out" ile test edin

**Endpoint'ler:**
- `GET /api/v1/docs` - Tüm dokümantasyonlar
- `GET /api/v1/docs/search` - Arama
- `GET /api/v1/docs/{documentId}` - İçerik
- `POST /api/v1/docs/reindex` - Re-index

### Senaryo 5: Log Kontrolü

Servis çalışırken console log'larını kontrol edin:

**Başarılı Indexing:**
```
[Information] Starting documentation re-indexing...
[Information] Indexing markdown files from: C:\Serkan\iSIM\MonitraNG\docs\content
[Information] Indexed 101 markdown files
[Information] Fetching OpenAPI spec from: http://mngdatagateway:5010/api-docs/v1/swagger.json
[Information] Indexed OpenAPI spec for MngDataGateway: 15 endpoints
[Information] Documentation re-indexing completed. Total documents: 116
```

**Hata Durumları:**
```
[Warning] Markdown path does not exist: ...
[Warning] Failed to fetch OpenAPI spec from ...: 404
[Error] Error indexing markdown file: ...
```

## Sorun Giderme

### Problem 1: "Markdown path does not exist"

**Çözüm:** `appsettings.json`'da `Documentation.MarkdownPath` yolunu kontrol edin. Path, `MngLLM.Api` projesinden `docs/content` klasörüne göre relative olmalı.

**Test:**
```powershell
cd c:\Serkan\iSIM\MonitraNG\MngLLM\Presentation\MngLLM.Api
$markdownPath = Resolve-Path "..\..\..\docs\content"
Test-Path $markdownPath
```

### Problem 2: "No documents found"

**Çözüm:** 
1. Re-index endpoint'ini çağırın: `POST /api/v1/docs/reindex`
2. Markdown dosyalarının `docs/content/` altında olduğundan emin olun
3. Log'larda indexing hatalarını kontrol edin

### Problem 3: "Authentication required"

**Çözüm:**
1. JWT token alın (MngKeeper'dan login yaparak)
2. Test script'ine `-Token` parametresi ile geçin
3. Veya development'ta `[AllowAnonymous]` attribute ekleyin (sadece test için)

### Problem 4: OpenAPI endpoints fail

**Çözüm:**
- OpenAPI endpoint'leri runtime'da HTTP ile alınıyor
- Servisler çalışmıyorsa veya erişilemiyorsa, sadece markdown dosyaları indexlenecek
- Bu normal bir durum, sadece markdown dokümantasyonu kullanılabilir

### Problem 5: Search returns no results

**Kontrol Listesi:**
1. Re-index yapıldı mı? (`POST /api/v1/docs/reindex`)
2. Dokümantasyonlar indexlendi mi? (`GET /api/v1/docs` ile kontrol)
3. Query doğru mu? (case-insensitive, keyword matching)
4. Log'larda hata var mı?

## Başarı Kriterleri

✅ **Tüm testler başarılı olmalı:**
1. Servis çalışıyor
2. En az 50+ markdown dosyası indexlenmiş
3. Arama sorguları sonuç döndürüyor
4. Relevance score'lar mantıklı (0-1 arası)
5. Document content retrieval çalışıyor
6. Re-indexing başarılı

## Sonraki Adımlar

Testler başarılı olduktan sonra:
1. **Faz 2**: Chatbot Backend implementasyonu
2. **Faz 3**: Chatbot Frontend implementasyonu
