# Chatbot Dokümantasyon Hazırlık Stratejisi

**Tarih:** 15 Ocak 2026  
**Servis:** MngLLM  
**Amaç:** Chatbot'un kullanacağı dokümantasyonları hazırlama ve indeksleme stratejisi

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Dokümantasyon Kaynakları](#dokümantasyon-kaynakları)
3. [Dokümantasyon Formatları](#dokümantasyon-formatları)
4. [İndeksleme Stratejisi](#indeksleme-stratejisi)
5. [LLM Context Hazırlama](#llm-context-hazırlama)
6. [Implementasyon Planı](#implementasyon-planı)
7. [Dokümantasyon Standartları](#dokümantasyon-standartları)

---

## 🎯 Genel Bakış

### Amaç

Chatbot'un kullanacağı dokümantasyonları:
- **MkDocs Markdown dosyaları** - Platform dokümantasyonu
- **OpenAPI/Swagger JSON** - API dokümantasyonu
- **Özel dokümanlar** - API description'ları, rehberler, vb.

Bu dokümanları chatbot için hazırlamak, indekslemek ve LLM'e context olarak sağlamak.

### Yaklaşım

1. **MkDocs Markdown dosyalarını kullan** - Mevcut dokümantasyon yapısını koru
2. **OpenAPI JSON'ları parse et** - API endpoint'lerini ve schema'ları çıkar
3. **İndeksleme** - Basit keyword search veya semantic search (gelecekte vector search)
4. **Context hazırlama** - LLM'e uygun formatta context sağla

---

## 📚 Dokümantasyon Kaynakları

### 1. MkDocs Markdown Dosyaları

**Konum:** `docs/content/`

**Yapı:**
```
docs/content/
├── index.md                          # Ana sayfa
├── api/                              # API dokümantasyonu
│   ├── overview.md
│   ├── mngkeeper/
│   │   ├── index.md
│   │   └── openapi.json
│   ├── mngdatagateway/
│   │   ├── index.md
│   │   └── openapi.json
│   └── ...
├── MngKeeper/                        # Servis dokümantasyonu
│   ├── architecture/
│   ├── guides/
│   └── changelog/
├── MngDataGateway/
│   ├── architecture/
│   ├── guides/
│   └── api/
└── ...
```

**Kullanım:**
- Platform genel dokümantasyonu
- Servis mimarisi açıklamaları
- Kullanım rehberleri
- Kurulum talimatları

### 2. OpenAPI/Swagger JSON Dosyaları

**Kaynak:**
- **Runtime:** `/api-docs/{documentName}/swagger.json` (her servis)
- **Build-time:** CI/CD pipeline'da extract edilen JSON'lar (`docs/content/api/{service}/openapi.json`)

**Servisler:**
- MngKeeper: `/api-docs/v1/swagger.json`
- MngDataGateway: `/api-docs/v1/swagger.json`
- MngHub: `/api-docs/v1/swagger.json`
- MngLLM: `/api-docs/v1/swagger.json`
- MngGateway: `/api-docs/v1/swagger.json`
- MngNotifier: `/api-docs/v1/swagger.json`
- MngScheduler: `/api-docs/v1/swagger.json`
- MngAdmin: `/api-docs/v1/swagger.json`

**Kullanım:**
- API endpoint'leri
- Request/Response schema'ları
- Authentication gereksinimleri
- Örnek request/response'lar

### 3. Özel Dokümanlar (Gelecekte)

**Örnekler:**
- API description'ları (detaylı endpoint açıklamaları)
- Use case örnekleri
- Troubleshooting rehberleri
- Best practices

**Konum:** `docs/{ServiceName}/guides/` veya `docs/{ServiceName}/specs/`

---

## 📄 Dokümantasyon Formatları

### Markdown Formatı

**Yapı:**
```markdown
# Başlık

## Alt Başlık

Açıklama metni...

### Kod Örneği

```csharp
// Kod örneği
```

### Notlar

> Önemli not
```

**Parse Edilecek Bilgiler:**
- Başlıklar (H1, H2, H3) - hiyerarşi
- Paragraflar - içerik
- Kod blokları - örnekler
- Linkler - referanslar
- Tablolar - yapılandırılmış bilgi

### OpenAPI JSON Formatı

**Yapı:**
```json
{
  "openapi": "3.0.1",
  "info": {
    "title": "MngDataGateway API",
    "version": "v1"
  },
  "paths": {
    "/api/v1/data/{datasetName}": {
      "get": {
        "summary": "Get data from dataset",
        "parameters": [...],
        "responses": {...}
      }
    }
  },
  "components": {
    "schemas": {...}
  }
}
```

**Parse Edilecek Bilgiler:**
- Endpoint'ler (`paths`)
- HTTP metodları (GET, POST, PUT, DELETE)
- Request/Response schema'ları
- Parameter'lar
- Örnek request/response'lar

---

## 🔍 İndeksleme Stratejisi

### Faz 1: Basit Keyword Search (İlk Aşama)

**Yaklaşım:**
- Markdown dosyalarını parse et
- Keyword'leri extract et (başlıklar, önemli terimler)
- Inverted index oluştur
- Basit text matching ile arama

**Avantajlar:**
- Hızlı implementasyon
- Düşük kaynak gereksinimi
- Yeterli başlangıç çözümü

**Dezavantajlar:**
- Semantic search yok
- Synonym'ler bulunamaz
- Context anlama sınırlı

### Faz 2: Semantic Search (Gelecekte - Vector Search)

**Yaklaşım:**
- Dokümantasyonları vector embedding'e çevir
- Vector database kullan (örn: Qdrant, Pinecone)
- Semantic similarity ile arama

**Avantajlar:**
- Daha iyi sonuçlar
- Synonym ve context anlama
- Benzer kavramları bulma

**Dezavantajlar:**
- Daha karmaşık implementasyon
- Daha fazla kaynak gereksinimi
- Vector database gerekli

### İndeksleme Yapısı

```csharp
public class DocumentIndex
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string Source { get; set; } // "markdown" | "openapi"
    public string Service { get; set; } // "MngDataGateway", "MngKeeper", etc.
    public string Category { get; set; } // "api", "architecture", "guide", etc.
    public string FilePath { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public List<string> Keywords { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

---

## 🤖 LLM Context Hazırlama

### Context Formatı

LLM'e gönderilecek context şu formatta olmalı:

```
# Context: {Title}

{Content}

## Metadata
- Source: {Source}
- Service: {Service}
- Category: {Category}
- Last Updated: {Date}
```

### Context Seçimi

**Strateji:**
1. Kullanıcı sorusunu analiz et
2. İlgili dokümantasyonları bul (keyword/semantic search)
3. En relevant N dokümanı seç (örn: top 5)
4. Context window'a sığacak şekilde kısalt
5. LLM'e gönder

**Context Window Yönetimi:**
- Model: Qwen2.5 3B → ~4096 token context window
- Kullanıcı mesajı: ~100-200 token
- LLM response: ~500-1000 token
- Kalan: ~3000 token → dokümantasyon context'i

**Kısaltma Stratejisi:**
- En önemli bölümleri seç (başlıklar, özetler)
- Kod örneklerini kısalt
- Gereksiz detayları çıkar

### Örnek Context

**Kullanıcı Sorusu:** "Dataset'e nasıl veri eklerim?"

**Context:**
```
# Context: MngDataGateway API - Create Data

## POST /api/v1/data/{datasetName}

Creates a new data record in the specified dataset.

### Request Body
{
  "field1": "value1",
  "field2": "value2"
}

### Response
201 Created - Returns the created record with __dataId

### Example
POST /api/v1/data/@books
{
  "title": "Example Book",
  "pageCount": 200
}

## Metadata
- Source: openapi
- Service: MngDataGateway
- Category: api
```

---

## 🛠️ Implementasyon Planı

### Faz 1: Dokümantasyon Provider (Backend)

**Süre:** 1-2 hafta

**Görevler:**
1. ✅ `IDocumentationProvider` interface oluştur
2. ✅ Markdown parser implementasyonu
3. ✅ OpenAPI JSON parser implementasyonu
4. ✅ Basit keyword index oluşturma
5. ✅ Search fonksiyonu

**Kod Yapısı:**
```csharp
// Domain Layer
public interface IDocumentationProvider
{
    Task<List<DocumentationResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
    Task<string> GetContentAsync(string documentId, CancellationToken cancellationToken);
    Task<List<DocumentationIndex>> GetAllDocumentsAsync(CancellationToken cancellationToken);
}

// Infrastructure Layer
public class DocumentationProvider : IDocumentationProvider
{
    private readonly IMarkdownParser _markdownParser;
    private readonly IOpenApiParser _openApiParser;
    private readonly IIndexService _indexService;
    
    // Markdown dosyalarını oku ve parse et
    // OpenAPI JSON'ları parse et
    // İndeks oluştur
    // Search yap
}
```

### Faz 2: Dokümantasyon İndeksleme

**Süre:** 1 hafta

**Görevler:**
1. ✅ Startup'ta dokümantasyonları indeksle
2. ✅ Periodic re-indexing (değişiklikleri takip et)
3. ✅ Cache mekanizması

**İndeksleme Stratejisi:**
- **Startup:** Tüm dokümantasyonları oku ve indeksle
- **Periodic:** Her 1 saatte bir kontrol et (değişiklik varsa re-index)
- **On-demand:** Dokümantasyon güncellendiğinde manuel re-index

### Faz 3: Chatbot Entegrasyonu

**Süre:** 1 hafta

**Görevler:**
1. ✅ Chat endpoint'inde dokümantasyon araması
2. ✅ Context hazırlama ve LLM'e gönderme
3. ✅ Sonuçları formatla ve kullanıcıya göster

**Akış:**
```
Kullanıcı Mesajı
    ↓
Intent Detection (Docs araması mı?)
    ↓
DocumentationProvider.SearchAsync()
    ↓
Top N doküman seç
    ↓
Context hazırla
    ↓
LLM'e gönder
    ↓
Response formatla
    ↓
Kullanıcıya göster
```

---

## 📝 Dokümantasyon Standartları

### Markdown Dokümantasyon Standartları

**Başlık Yapısı:**
```markdown
# Ana Başlık (H1) - Sadece bir tane
## Bölüm Başlığı (H2)
### Alt Bölüm (H3)
```

**Kod Örnekleri:**
```markdown
### Örnek

```csharp
// Kod örneği
```
```

**Linkler:**
```markdown
[Link Metni](relative/path/to/file.md)
```

**Önemli Notlar:**
```markdown
> **Not:** Önemli bilgi
> **Uyarı:** Dikkat edilmesi gereken
```

### OpenAPI Dokümantasyon Standartları

**Summary ve Description:**
```yaml
summary: "Kısa açıklama"
description: |
  Detaylı açıklama.
  Örnekler ve kullanım bilgileri.
```

**Örnek Request/Response:**
```yaml
examples:
  example1:
    summary: "Örnek 1"
    value:
      field1: "value1"
```

### Chatbot İçin Özel Metadata

**Front Matter (Markdown):**
```markdown
---
title: "Dataset Oluşturma"
category: "guide"
service: "MngDataGateway"
tags: ["dataset", "create", "tutorial"]
priority: 1
---
```

**Kullanım:**
- `category`: Dokümantasyon kategorisi (api, guide, architecture, vb.)
- `service`: Hangi servisle ilgili
- `tags`: Arama için keyword'ler
- `priority`: Öncelik (yüksek öncelikli dokümanlar önce gösterilir)

---

## 🔄 Dokümantasyon Güncelleme Süreci

### Otomatik Güncelleme

**OpenAPI JSON:**
- CI/CD pipeline'da otomatik extract
- `docs/content/api/{service}/openapi.json` dosyasına yazılır
- Chatbot servisi bu dosyaları okuyup indeksler

**Markdown:**
- Manuel güncelleme (geliştiriciler tarafından)
- Chatbot servisi dosya değişikliklerini takip eder
- Periodic re-indexing

### Re-indexing Stratejisi

**Trigger'lar:**
1. **Startup:** Servis başladığında
2. **Periodic:** Her 1 saatte bir
3. **File Change:** Dokümantasyon dosyası değiştiğinde (FileSystemWatcher)
4. **Manual:** API endpoint ile manuel trigger

**Re-indexing Endpoint:**
```csharp
POST /api/v1/llm/docs/reindex
{
  "service": "MngDataGateway", // optional - belirli servis
  "force": true // optional - cache'i bypass et
}
```

---

## 📊 Performans ve Ölçeklenebilirlik

### İndeks Boyutu

**Tahminler:**
- Markdown dosyaları: ~100-200 dosya, ~5-10 MB
- OpenAPI JSON'ları: ~8 servis, ~1-2 MB
- Toplam: ~10-15 MB raw data
- İndeks: ~20-30 MB (keyword index)

### Arama Performansı

**Hedefler:**
- Arama süresi: < 100ms
- Context hazırlama: < 200ms
- Toplam (arama + LLM): < 5 saniye

### Ölçeklenebilirlik

**Gelecekte:**
- Vector database (semantic search için)
- Distributed indexing
- Caching stratejisi (Redis)

---

## 🧪 Test Stratejisi

### Unit Tests

- Markdown parser testleri
- OpenAPI parser testleri
- Index service testleri
- Search algoritması testleri

### Integration Tests

- Dokümantasyon okuma testleri
- İndeksleme testleri
- Arama testleri
- Context hazırlama testleri

### E2E Tests

- Kullanıcı dokümantasyon sorusu → Chatbot cevabı
- Farklı soru tipleri
- Hata durumları (dokümantasyon bulunamadı)

---

## 📝 Sonraki Adımlar

1. ✅ Dokümantasyon hazırlık stratejisi oluşturuldu
2. 📋 `IDocumentationProvider` interface tasarla
3. 📋 Markdown parser implementasyonu
4. 📋 OpenAPI parser implementasyonu
5. 📋 Basit keyword index implementasyonu
6. 📋 Chatbot entegrasyonu

---

**Son Güncelleme:** 15 Ocak 2026
