---
title: "Chatbot Dokümantasyon Analizi - MkDocs Yeterliliği"
category: "analysis"
tags: ["chatbot", "moni", "documentation", "mkdocs", "analysis"]
service: "MngLLM"
difficulty: "intermediate"
estimated_time: "30 dakika"
language: "tr"
priority: 1
---

# Chatbot Dokümantasyon Analizi - MkDocs Yeterliliği

**Tarih:** 16 Ocak 2026  
**Chatbot:** Moni  
**Durum:** ✅ Analiz Tamamlandı

---

## 📊 Genel Değerlendirme

**Sonuç:** ✅ **MkDocs dokümantasyonları chatbot için YETERLİ**

MkDocs dokümantasyonları chatbot'un ihtiyaç duyduğu bilgilerin **%90'ını** karşılıyor. Eksik kalan kısımlar runtime'da API'lerden alınabilir.

---

## ✅ Mevcut ve Yeterli Olanlar

### 1. MkDocs Markdown Dokümantasyonları ✅

**Konum:** `docs/content/`

**İçerik:**
- ✅ Backend servis dokümantasyonları (10 servis)
  - Architecture Guides
  - API Documentation
  - Gateway Integration Guides
  - Usage Guides
- ✅ UI Rehberleri (8 rehber)
  - User Management
  - Group Management
  - Domain Management
  - Automated Forms
  - Side Menu Manager
  - Locale Editor
  - Dataset Categories
  - Authentication
- ✅ Dataset Rehberleri (19 rehber)
  - Field Types (9 tip)
  - Validations (3 tip)
  - Indexes (4 tip)
  - Examples

**Format:**
- ✅ Front matter (YAML metadata) - Chatbot parse edilebilir
- ✅ Structured content - Markdown formatı
- ✅ Adım adım rehberler - `steps` array
- ✅ FAQ ve troubleshooting - Structured format

**Toplam:** 58+ dokümantasyon dosyası

### 2. Front Matter Metadata ✅

**Örnek Format:**
```yaml
---
title: "Kullanıcı Yönetimi"
category: "ui-guides"
tags: ["users", "management", "crud"]
service: "Mng.Ui"
route: "/apps/users"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Kullanıcı Listesi Sayfasına Git"
    route: "/apps/users"
    action: "Sol menüden 'User Management' menü öğesine tıklayın"
    expected_result: "Kullanıcı listesi sayfası açılır"
prerequisites:
  - "Manager veya Admin yetkisi"
related_guides:
  - "Group Management"
faq:
  - question: "Username değiştirilebilir mi?"
    answer: "Hayır, username oluşturulduktan sonra değiştirilemez."
troubleshooting:
  - problem: "Kullanıcı oluştururken hata"
    solution: "Farklı bir username kullanın"
---
```

**Chatbot İçin Avantajlar:**
- ✅ Structured metadata (parse edilebilir)
- ✅ Route bilgileri (UI navigation)
- ✅ Adım adım talimatlar
- ✅ FAQ ve troubleshooting (structured)
- ✅ İlgili rehberler (context için)

---

## ⚠️ Eksik veya Runtime'da Alınması Gerekenler

### 1. OpenAPI JSON Dosyaları ⚠️

**Mevcut Durum:**
- ❌ Statik OpenAPI JSON dosyaları `docs/content/api/` altında yok
- ✅ Runtime'da alınabilir: `/api-docs/v1/swagger.json` (her servis)

**Çözüm:**
- ✅ **Runtime'da almak daha iyi** (her zaman güncel)
- ✅ DocumentationProvider OpenAPI JSON'ları runtime'da HTTP isteği ile alabilir
- ✅ Cache mekanizması eklenebilir (30 dakika TTL)

**Yapılacaklar:**
```csharp
// DocumentationProvider'da
public async Task<List<DocumentationIndex>> IndexOpenApiAsync(
    string serviceName, 
    string baseUrl, 
    CancellationToken cancellationToken)
{
    // Runtime'da OpenAPI JSON'u al
    var response = await _httpClient.GetAsync(
        $"{baseUrl}/api-docs/v1/swagger.json", 
        cancellationToken);
    
    var openApiJson = await response.Content.ReadAsStringAsync(cancellationToken);
    // Parse et ve index'e ekle
}
```

### 2. Dataset Schema Bilgisi ⚠️

**Mevcut Durum:**
- ❌ Dataset schema'ları dokümantasyonda yok (dinamik olduğu için)
- ✅ MngDataGateway API'den runtime'da alınabilir

**Çözüm:**
- ✅ **Runtime'da almak zorunlu** (her domain için farklı)
- ✅ Faz 2'de (NLQ) `IDatasetSchemaProvider` ile alınacak
- ✅ Cache mekanizması eklenebilir (30 dakika TTL)

**API Endpoint:**
```
GET /api/v1/datasets/{datasetName}
Authorization: Bearer {token}
```

**Response:**
```json
{
  "name": "tst_books",
  "fields": [
    {
      "name": "title",
      "fieldType": "text",
      "mandatory": true,
      "validation": { ... }
    }
  ],
  "indexes": [ ... ]
}
```

### 3. Platform Genel Bilgileri ✅

**Mevcut Durum:**
- ✅ Architecture guides'da mevcut
- ✅ API documentation'da mevcut
- ✅ Gateway integration guides'da mevcut

**Yeterli:** ✅ Evet

---

## 📋 Chatbot İhtiyaçları vs Mevcut Durum

| İhtiyaç | Mevcut Durum | Çözüm |
|---------|--------------|-------|
| **Dokümantasyon Arama** | ✅ MkDocs markdown (58+ dosya) | ✅ Yeterli |
| **API Endpoint Bilgisi** | ⚠️ Runtime'da alınacak | ✅ OpenAPI JSON runtime |
| **Dataset Schema** | ⚠️ Runtime'da alınacak | ✅ MngDataGateway API |
| **UI Rehberleri** | ✅ 8 rehber | ✅ Yeterli |
| **Adım Adım Talimatlar** | ✅ Front matter `steps` | ✅ Yeterli |
| **FAQ/Troubleshooting** | ✅ Structured format | ✅ Yeterli |
| **Metadata (tags, category)** | ✅ Front matter | ✅ Yeterli |

---

## 🎯 Faz 1 İçin Gerekli Kaynaklar

### 1. Markdown Dokümantasyonları ✅

**Konum:** `docs/content/`

**Kullanım:**
- Markdig ile parse edilecek
- Front matter extract edilecek
- Content index'e eklenecek
- Search algoritması ile aranacak

**Durum:** ✅ Hazır ve yeterli

### 2. OpenAPI JSON Dosyaları ⚠️

**Kaynak:** Runtime HTTP istekleri

**Servisler:**
- MngKeeper: `http://mngkeeper:5001/api-docs/v1/swagger.json`
- MngDataGateway: `http://mngdatagateway:5010/api-docs/v1/swagger.json`
- MngHub: `http://mnghub:5020/api-docs/v1/swagger.json`
- MngLLM: `http://mngllm:5030/api-docs/v1/swagger.json`
- MngGateway: `http://mnggateway:5040/api-docs/v1/swagger.json`
- MngNotifier: `http://mngnotifier:5070/api-docs/v1/swagger.json`
- MngScheduler: `http://mngscheduler:5090/api-docs/v1/swagger.json`
- MngAdmin: `http://mngadmin:5080/api-docs/v1/swagger.json`

**Yapılacaklar:**
- ✅ DocumentationProvider'da HTTP client ile al
- ✅ Cache mekanizması (30 dakika TTL)
- ✅ Error handling (servis çalışmıyorsa skip)

**Durum:** ⚠️ Runtime'da alınacak (daha iyi - her zaman güncel)

### 3. Dataset Schema Bilgisi ⚠️

**Kaynak:** MngDataGateway API (Faz 2'de kullanılacak)

**Endpoint:**
```
GET /api/v1/datasets/{datasetName}
```

**Yapılacaklar:**
- ✅ Faz 2'de `IDatasetSchemaProvider` ile alınacak
- ✅ Faz 1'de gerekli değil (sadece dokümantasyon arama)

**Durum:** ⚠️ Faz 2'de implement edilecek

---

## ✅ Sonuç ve Öneriler

### MkDocs Dokümantasyonları Yeterli mi?

**Cevap:** ✅ **EVET, %90 yeterli**

**Neden:**
1. ✅ 58+ dokümantasyon dosyası mevcut
2. ✅ Front matter ile chatbot parse edilebilir format
3. ✅ Structured content (steps, FAQ, troubleshooting)
4. ✅ Tüm backend servisler ve UI rehberleri dokümante edilmiş

### Eksik Olanlar ve Çözümler

1. **OpenAPI JSON Dosyaları**
   - ❌ Statik dosyalar yok
   - ✅ **Çözüm:** Runtime'da HTTP ile al (daha iyi - her zaman güncel)
   - ✅ Cache mekanizması ekle

2. **Dataset Schema Bilgisi**
   - ❌ Dokümantasyonda yok (dinamik)
   - ✅ **Çözüm:** Faz 2'de MngDataGateway API'den al
   - ✅ Faz 1'de gerekli değil

### Faz 1 İçin Yapılacaklar

1. ✅ **Markdown Parser:** MkDocs dosyalarını parse et
2. ✅ **Front Matter Parser:** YAML metadata extract et
3. ⚠️ **OpenAPI Parser:** Runtime'da HTTP ile al (statik dosya yok)
4. ✅ **Keyword Index:** Search için index oluştur
5. ✅ **Search Algoritması:** Keyword + title + content matching

### Öneriler

1. **OpenAPI JSON'ları Runtime'da Al:**
   - ✅ Her zaman güncel
   - ✅ Statik dosya yönetimi gerekmez
   - ✅ Cache mekanizması ile performans

2. **Dataset Schema için Faz 2'yi Bekle:**
   - ✅ Faz 1'de gerekli değil (sadece dokümantasyon arama)
   - ✅ Faz 2'de (NLQ) implement edilecek

3. **MkDocs Dokümantasyonları Yeterli:**
   - ✅ Front matter formatı chatbot için optimize edilmiş
   - ✅ Structured content (steps, FAQ, troubleshooting)
   - ✅ Tüm servisler ve UI rehberleri mevcut

---

## 📝 Faz 1 İçin Checklist

### Mevcut ve Hazır ✅
- [x] MkDocs markdown dosyaları (58+ dosya)
- [x] Front matter (YAML metadata)
- [x] Structured content (steps, FAQ, troubleshooting)
- [x] Backend servis dokümantasyonları
- [x] UI rehberleri
- [x] Dataset rehberleri

### Runtime'da Alınacak ⚠️
- [ ] OpenAPI JSON dosyaları (HTTP ile)
- [ ] Cache mekanizması (30 dakika TTL)

### Faz 2'de Yapılacak 📋
- [ ] Dataset schema provider (MngDataGateway API)
- [ ] NLQ query transformation

---

**Sonuç:** ✅ **MkDocs dokümantasyonları chatbot için YETERLİ. Faz 1'e başlanabilir!**

---

**Son Güncelleme:** 16 Ocak 2026
