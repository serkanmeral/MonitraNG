# MngLLM - LLM Service Roadmap

**Microservice:** LLM Service (Ollama Integration)  
**Version:** 1.0.1  
**Son Güncelleme:** 11 Ocak 2026  
**Durum:** ✅ Faz 1 Tamamlandı - Çoklu Dil Desteği Aktif

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Öncelik Sırası](#öncelik-sırası)
3. [Faz 1: Dil Dosyası Güncelleme (Çeviri)](#faz-1-dil-dosyası-güncelleme-çeviri)
4. [Faz 2: Dataset Sorgulama (NLQ)](#faz-2-dataset-sorgulama-nlq)
5. [Faz 3: Dokümantasyon & Yardım](#faz-3-dokümantasyon--yardım)
6. [Faz 4: Kullanıcı Rehberi](#faz-4-kullanıcı-rehberi)
7. [Faz 5: Chatbot Cevaplarının İyileştirilmesi](#faz-5-chatbot-cevaplarının-iyileştirilmesi)
8. [Teknik Detaylar](#teknik-detaylar)
9. [Kaynak Gereksinimleri](#kaynak-gereksinimleri)

---

## 🎯 Genel Bakış

**MngLLM**, MonitraNG platformuna dahili LLM (Ollama) entegrasyonu sağlayan ayrı bir mikroservistir. Test ortamında hafif modeller (Qwen2.5 3B) kullanılacak, production için ayrı sunucu planlanmaktadır.

### Temel Özellikler

- ✅ **Çoklu Dil Çevirisi** - Türkçe metinleri diğer dillere çevirme (Aktif)
- 📋 **Natural Language Query (NLQ)** - Doğal dil ile dataset sorgulama (Planlandı)
- 📋 **Dokümantasyon Yardımı** - Platform kullanım rehberi (Planlandı)
- 📋 **Kullanıcı Rehberi** - Adım adım talimatlar (Planlandı)
- 📋 **Chatbot Uygulamaları** - İleride planlanacak

### Mimari

```
┌─────────────────────────────────────────────────────────┐
│                    MngLLM Service                        │
├─────────────────────────────────────────────────────────┤
│  Presentation Layer (REST API)                          │
│  Application Layer (CQRS Commands/Queries)              │
│  Domain Layer (Interfaces)                              │
│  Infrastructure Layer (Ollama Adapter, HTTP Clients)    │
└─────────────────────────────────────────────────────────┘
         ▲                    ▲
         │                    │
    ┌────┴────┐         ┌─────┴─────┐
    │  Other  │         │  Ollama   │
    │Services │         │  Service  │
    └─────────┘         └───────────┘
```

---

## 🎯 Öncelik Sırası

| Faz | Özellik | Öncelik | Zorluk | Tahmini Süre |
|-----|---------|---------|--------|--------------|
| **Faz 1** | Dil Dosyası Güncelleme (Çeviri) | ⭐⭐⭐ Yüksek | Orta | 2-3 hafta |
| **Faz 2** | Dataset Sorgulama (NLQ) | ⭐⭐⭐ Yüksek | Yüksek | 3-4 hafta |
| **Faz 3** | Dokümantasyon & Yardım | ⭐⭐ Orta | Düşük | 1-2 hafta |
| **Faz 4** | Kullanıcı Rehberi | ⭐ Düşük | Düşük | 1 hafta |
| **Faz 5** | Chatbot Cevaplarının İyileştirilmesi | ⭐⭐ Orta | Orta | 2-3 hafta |

---

## ✅ Faz 1: Dil Dosyası Güncelleme (Çeviri)

**Durum:** ✅ **TAMAMLANDI**  
**Öncelik:** ⭐⭐⭐ Yüksek  
**Tamamlanma Tarihi:** 15 Ocak 2026

### Genel Bakış

Side Menu Manager'daki "Dil Dosyalarını Güncelle" butonu, Türkçe metinleri otomatik olarak İngilizce, Fransızca, Arapça ve Çince'ye çevirerek tüm dil dosyalarını güncelleyecek.

### ✅ Tamamlanan İşler

**MngLLM Service:**
- ✅ Ollama Docker container kurulumu
- ✅ MngLLM Service proje yapısı (Clean Architecture)
- ✅ Translation API endpoint (`POST /api/v1/llm/translate`)
- ✅ OllamaLLMAdapter implementation
- ✅ Swagger/Scalar dokümantasyon
- ✅ API versioning
- ✅ API Gateway entegrasyonu
- ✅ HTTP (SSL/TLS termination Gateway'de)
- ✅ Health check ve version endpoints (`/health`)
- ✅ Docker containerization
- ✅ API Gateway Pattern entegrasyonu (v1.0.1 - 11 Ocak 2026)
  - HTTPS'den HTTP'ye geçiş (SSL/TLS termination artık Gateway'de)
  - CORS yapılandırması kaldırıldı (Gateway'de merkezi yönetim)
  - Sertifika yönetimi kaldırıldı (Gateway'de yönetiliyor)
  - Health endpoint standartlaştırıldı (`/health`)
  - Internal network'te çalışıyor (external exposure yok)

**Side Menu Manager:**
- ✅ "Dil Dosyalarını Güncelle" butonu LLM entegrasyonu ile çalışıyor
- ✅ MngKeeper `/system/locales/{locale}` API endpoint'leri kullanılıyor
- ✅ LLM çeviri entegrasyonu tamamlandı
- ✅ Otomatik çeviri: Türkçe → İngilizce, Fransızca, Arapça, Çince
- ✅ Fallback mekanizması (LLM çalışmıyorsa placeholder)

**MngKeeper API:**
- ✅ `GET /system/locales/{locale}` - Locale dosyası okuma
- ✅ `PUT /system/locales/{locale}` - Locale dosyası güncelleme
- ✅ MinIO'da `System/locales/` klasöründe dosyalar saklanıyor

### ✅ Tamamlanan İşler

#### Backend (MngLLM Service)

**1. ✅ MngLLM Service Oluşturuldu**

```
MngLLM/
├── Core/
│   ├── MngLLM.Domain/
│   │   ├── Interfaces/
│   │   │   └── ILLMService.cs ✅
│   │   └── Exceptions/
│   │       └── MngLLMException.cs ✅
│   │
│   └── MngLLM.Application/
│       ├── Commands/
│       │   └── TranslateText/
│       │       ├── TranslateTextCommand.cs ✅
│       │       └── TranslateTextCommandHandler.cs ✅
│       ├── DTOs/
│       │   └── TranslationRequestDto.cs ✅
│       │   └── TranslationResponseDto.cs ✅
│       └── Configuration/
│           └── MngLLMSettings.cs ✅
│
├── Infrastructure/
│   └── MngLLM.Infrastructure/
│       ├── Adapters/
│       │   └── OllamaLLMAdapter.cs ✅
│       └── Services/
│           └── Certificate/
│               └── CertificateHandler.cs ✅
│
└── Presentation/
    └── MngLLM.Api/
        ├── Controllers/
        │   ├── LLMController.cs ✅
        │   ├── HealthController.cs ✅
        │   └── VersionController.cs ✅
        ├── Config/
        │   ├── AuthConfig.cs ✅
        │   ├── Extensions.cs ✅
        │   └── SwaggerConfigureOptions.cs ✅
        └── Program.cs ✅
```

**2. ✅ API Endpoints**

- ✅ `POST /api/v1/llm/translate` - Çoklu dil çevirisi
  - Request: `{ "text": "Kitaplar", "sourceLanguage": "tr", "targetLanguages": ["en", "fr", "ar", "zh"] }`
  - Response: `{ "translations": { "en": "Books", "fr": "Livres", "ar": "كتب", "zh": "书籍" }, "model": "...", "inferenceTime": "..." }`
- ✅ `GET /health` - Health check (versiyonlanmamış)
- ✅ `GET /version` - Full version bilgisi
- ✅ `GET /version/short` - Kısa version bilgisi

**3. ✅ Ollama Integration**

- ✅ Ollama Docker container kurulumu
- ✅ Ollama API client (HTTP) - `OllamaLLMAdapter`
- ✅ Model: Qwen2.5 3B (test ortamı)

**4. ✅ Configuration**

```json
{
  "MngLLMSettings": {
    "Server": {
      "Host": "0.0.0.0",
      "Port": 5030,
      "Scheme": "https"
    },
    "Ollama": {
      "BaseUrl": "http://ollama:11434",
      "DefaultModel": "qwen2.5:3b",
      "Timeout": 30
    },
    "Translation": {
      "SupportedLanguages": ["tr", "en", "fr", "ar", "zh"],
      "CacheEnabled": true,
      "CacheTTL": 3600
    }
  }
}
```

**5. ✅ API Dokümantasyonu ve Versioning**

- ✅ Swagger/Scalar desteği
- ✅ API versioning (`Asp.Versioning.Mvc`)
- ✅ OpenAPI dokümantasyonu

**6. ✅ API Gateway Entegrasyonu**

- ✅ Ocelot route'ları eklendi
- ✅ `MngGatewaySettings` güncellendi
- ✅ Rate limiting yapılandırıldı

**7. ✅ API Gateway Integration (v1.0.1 - 11 Ocak 2026)**

**Yapılan Değişiklikler:**
- ✅ HTTPS'den HTTP'ye geçiş (SSL/TLS termination artık Gateway'de)
- ✅ CORS yapılandırması kaldırıldı (Gateway'de merkezi yönetim)
- ✅ Sertifika yönetimi kaldırıldı (Gateway'de yönetiliyor)
- ✅ Health endpoint standartlaştırıldı (`/health`)
- ✅ Internal network'te çalışıyor (external exposure yok)

**Faydalar:**
- ✅ Tek sertifika yönetimi (Gateway'de)
- ✅ Merkezi CORS yönetimi
- ✅ Servis basitleştirildi (CORS, sertifika kaldırıldı)
- ✅ API Gateway pattern'ine uygun mimari
- ✅ Production'da Nginx ile Let's Encrypt SSL termination

**Gateway URL:**
- Production: `https://api.monitra.local/llm/api/v1/*`
- Development: `https://localhost:5040/llm/api/v1/*`

**Internal URL (Docker network):**
- `http://mngllm:5030/api/v1/*`

**8. ✅ Docker Entegrasyonu**

- ✅ Dockerfile oluşturuldu
- ✅ docker-compose.yml'e eklendi
- ✅ Docker build ve compose up tamamlandı

#### Frontend (Mng.Ui)

**1. ✅ MenuItemForm.vue Güncellendi**

- ✅ `updateLocales` fonksiyonu LLM API çağrısı yapıyor
- ✅ Çeviri sonuçları locale dosyalarına yazılıyor
- ✅ Fallback mekanizması (LLM çalışmıyorsa placeholder)
- ✅ Loading state ve error handling

**2. ✅ API Integration**

- ✅ `apiService.ts` - `fetchFromMngLLM` fonksiyonu eklendi
- ✅ Nuxt server API route: `server/api/llm/[...path].ts`
- ✅ `nuxt.config.ts` - `llmUrl` eklendi

### Test Senaryoları

1. **Basit Çeviri Testi**
   - Input: "Kitaplar" (TR)
   - Expected: EN: "Books", FR: "Livres", AR: "كتب", ZH: "书籍"

2. **Cümle Çevirisi Testi**
   - Input: "Dataset Yönetimi" (TR)
   - Expected: EN: "Dataset Management", FR: "Gestion de Dataset", AR: "إدارة مجموعة البيانات", ZH: "数据集管理"

3. **Hata Durumları**
   - LLM servisi çalışmıyorsa fallback (mevcut placeholder davranışı)
   - Çeviri başarısız olursa error handling
   - Network timeout handling

4. **Performance Testi**
   - Çeviri süresi (target: < 5 saniye)
   - Eşzamanlı istekler
   - Cache etkinliği

### ✅ Tamamlanan Özellikler

- ✅ **Model Seçimi:** Qwen2.5 3B kullanılıyor (test ortamı)
- ✅ **Fallback Mekanizması:** LLM servisi çalışmıyorsa placeholder davranışına dönüyor
- ⚠️ **Caching:** Henüz implement edilmedi (opsiyonel - gelecekte eklenebilir)
- ✅ **Error Handling:** Çeviri başarısız olursa kullanıcıya bilgi veriliyor, placeholder kullanılıyor

### 📝 Notlar

- **Model Seçimi:** Test için Qwen2.5 3B yeterli, ancak Arapça ve Çince çeviriler için daha büyük model gerekebilir (Qwen2.5 7B veya RN_TR_R1) - Production için
- **API Gateway Entegrasyonu (Mng.Ui):** Şu anda MngLLM için gatewayUrl kontrolü yok, direkt servis URL'i kullanılıyor. İleride Keeper ve DataGateway pattern'i takip edilerek eklenebilir.

---

## 📋 Faz 2: Dataset Sorgulama (NLQ)

**Durum:** 📋 Planlandı  
**Öncelik:** ⭐⭐⭐ Yüksek  
**Tahmini Süre:** 3-4 hafta

### Genel Bakış

Kullanıcılar doğal dil ile dataset sorgulama yapabilecek. Örnek: "Sayfa sayısı 50'den fazla kaç kitap var?"

### Gereksinimler

#### Backend (MngLLM Service)

**1. API Endpoints**

- `POST /api/v1/llm/query` - Natural Language Query
  - Request: `{ "dataset": "tst_books", "query": "Sayfa sayısı 50'den fazla kaç kitap var?", "domainName": "..." }`
  - Response: `{ "filter": { "pageCount": { "$gt": 50 } }, "count": true, "explanation": "..." }`

**2. Dataset Schema Context**

- MngDataGateway'den dataset schema bilgisi alınmalı
- Field isimleri, tipleri LLM'e context olarak sağlanmalı
- Caching: Schema bilgisi cache'lenebilir

**3. Query Transformation**

- Doğal dil → MongoDB filter formatı
- Doğal dil → MngDataGateway API parametreleri
- Hata durumlarında kullanıcı dostu mesajlar

#### Frontend (Mng.Ui)

**1. Chatbot UI Component**

- Chat interface (mesaj geçmişi)
- Dataset seçimi
- Query input
- Results display

**2. API Integration**

- MngLLM Service → Query transformation
- MngDataGateway API → Data retrieval
- Results formatting

### Implementation Steps

#### Step 1: Dataset Schema Context Provider

```csharp
// Application/Services/IDatasetSchemaProvider.cs
public interface IDatasetSchemaProvider
{
    Task<DatasetSchema> GetSchemaAsync(string datasetName, string domainName, CancellationToken cancellationToken);
}

// Infrastructure/MngLLM.Infrastructure/Services/DatasetSchemaProvider.cs
public class DatasetSchemaProvider : IDatasetSchemaProvider
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    
    public async Task<DatasetSchema> GetSchemaAsync(string datasetName, string domainName, CancellationToken cancellationToken)
    {
        var cacheKey = $"dataset_schema_{domainName}_{datasetName}";
        
        if (_cache.TryGetValue(cacheKey, out DatasetSchema cached))
            return cached;
        
        var response = await _httpClient.GetAsync(
            $"{_settings.DataGatewayUrl}/api/v1/datasets/{datasetName}",
            cancellationToken);
        
        var schema = await response.Content.ReadFromJsonAsync<DatasetSchema>();
        
        _cache.Set(cacheKey, schema, TimeSpan.FromMinutes(30));
        return schema;
    }
}
```

#### Step 2: NLQ Command Handler

```csharp
// Application/Commands/NaturalLanguageQuery/NaturalLanguageQueryCommandHandler.cs
public class NaturalLanguageQueryCommandHandler : IRequestHandler<NaturalLanguageQueryCommand, NaturalLanguageQueryResponseDto>
{
    private readonly ILLMService _llmService;
    private readonly IDatasetSchemaProvider _schemaProvider;
    
    public async Task<NaturalLanguageQueryResponseDto> Handle(NaturalLanguageQueryCommand request, CancellationToken cancellationToken)
    {
        // Get dataset schema
        var schema = await _schemaProvider.GetSchemaAsync(request.Dataset, request.DomainName, cancellationToken);
        
        // Build context for LLM
        var context = BuildSchemaContext(schema);
        
        // Generate MongoDB filter from natural language
        var prompt = BuildNLQPrompt(request.Query, context);
        var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);
        
        // Parse LLM response to MongoDB filter
        var filter = ParseLLMResponse(llmResponse);
        
        return new NaturalLanguageQueryResponseDto 
        { 
            Filter = filter,
            Explanation = "..." 
        };
    }
    
    private string BuildSchemaContext(DatasetSchema schema)
    {
        var fields = schema.Fields.Select(f => $"- {f.Name} ({f.Type}): {f.Description}");
        return $"Dataset: {schema.Name}\nFields:\n{string.Join("\n", fields)}";
    }
    
    private string BuildNLQPrompt(string query, string context)
    {
        return $"Given the following dataset schema:\n\n{context}\n\n" +
               $"User question: {query}\n\n" +
               $"Convert this to a MongoDB filter JSON. Only return valid JSON, no explanation.";
    }
}
```

#### Step 3: Frontend Chatbot Component

```vue
<!-- components/apps/chatbot/NLQChatbot.vue -->
<template>
  <v-card>
    <v-card-title>Dataset Sorgulama</v-card-title>
    
    <v-card-text>
      <!-- Dataset Selection -->
      <v-select v-model="selectedDataset" :items="datasets" label="Dataset" />
      
      <!-- Chat Messages -->
      <v-list>
        <v-list-item v-for="msg in messages" :key="msg.id">
          {{ msg.text }}
        </v-list-item>
      </v-list>
      
      <!-- Query Input -->
      <v-text-field v-model="query" label="Soru sorun..." @keyup.enter="sendQuery" />
    </v-card-text>
  </v-card>
</template>

<script setup>
const sendQuery = async () => {
  // Call MngLLM API for query transformation
  const queryResponse = await $fetch('/api/v1/llm/query', {
    method: 'POST',
    body: { dataset: selectedDataset.value, query: query.value }
  });
  
  // Call MngDataGateway API with filter
  const dataResponse = await $fetch(`/api/v1/data/${selectedDataset.value}`, {
    query: { filter: JSON.stringify(queryResponse.filter) }
  });
  
  // Display results...
};
</script>
```

### Test Senaryoları

1. **Basit Sorgu**
   - Input: "Sayfa sayısı 50'den fazla kaç kitap var?"
   - Expected Filter: `{ "pageCount": { "$gt": 50 } }`

2. **Karmaşık Sorgu**
   - Input: "2020 yılından sonra yayınlanan ve sayfa sayısı 200'den fazla olan kitaplar"
   - Expected Filter: `{ "publishDate": { "$gt": "2020-01-01" }, "pageCount": { "$gt": 200 } }`

3. **Hata Durumları**
   - Dataset bulunamadı
   - Field ismi yanlış
   - Query anlaşılamadı

---

## 📋 Faz 3: Dokümantasyon & Yardım

**Durum:** 📋 Planlandı  
**Öncelik:** ⭐⭐ Orta  
**Tahmini Süre:** 1-2 hafta

### Genel Bakış

Kullanıcılar platform hakkında sorular sorabilecek. LLM mevcut dokümantasyonu analiz ederek cevap verecek.

### Gereksinimler

- Dokümantasyon erişimi (docs/ klasörü veya kod analizi)
- Context management
- Örnek üretme
- Kullanıcı dostu açıklamalar

---

## 📋 Faz 4: Kullanıcı Rehberi

**Durum:** 📋 Planlandı  
**Öncelik:** ⭐ Düşük  
**Tahmini Süre:** 1 hafta

### Genel Bakış

Kullanıcılar "Şifremi nasıl değiştiririm?" gibi sorular sorabilecek. LLM adım adım talimatlar verecek.

---

## 📋 Faz 5: Chatbot Cevaplarının İyileştirilmesi

**Durum:** 📋 Planlandı  
**Öncelik:** ⭐⭐ Orta  
**Tahmini Süre:** 2-3 hafta

### Genel Bakış

Chatbot (Moni) implementasyonu tamamlandı, ancak cevapların kalitesini ve doğruluğunu artırmak için sürekli iyileştirme yapılması gerekiyor. Bu faz, chatbot'un daha doğru, tutarlı ve kullanıcı dostu cevaplar vermesini sağlayacak.

### Mevcut Durum

✅ **Tamamlanan:**
- Chatbot backend implementasyonu (Phase 2)
- Chatbot frontend implementasyonu (Phase 3)
- Intent detection (keyword + LLM hybrid)
- Documentation provider entegrasyonu
- Context management
- Response caching
- Retry mechanism
- Multi-language support

⚠️ **Tespit Edilen Sorunlar:**
- Dataset kategorisi ile field type karıştırılması gibi kavram yanlışlıkları
- Bazı cevaplarda tutarsızlık
- Dokümantasyon snippet'lerinin yeterince etkili kullanılmaması
- Prompt engineering'in daha da optimize edilmesi gerekiyor

### İyileştirme Hedefleri

#### 1. Dokümantasyon Kalitesi Artırma

**Amaç:** Chatbot'un daha doğru bilgi vermesi için dokümantasyonun kalitesini artırmak.

**Görevler:**
- [ ] Tüm dokümantasyon dosyalarında `summary` ve `faq` front matter'larının eklenmesi
- [ ] Kavramların net bir şekilde açıklandığı bölümlerin eklenmesi (örnek: Dataset Kategorisi vs Field Type)
- [ ] Örnek senaryoların ve kullanım durumlarının eklenmesi
- [ ] Dokümantasyon snippet'lerinin daha etkili seçilmesi (relevance scoring)

**Örnek İyileştirme:**
- Dataset kategorisi dokümantasyonuna "Dataset Kategorisi Nedir?" bölümü eklendi ✅
- FAQ bölümü eklendi ✅
- Front matter'a `summary` ve `faq` eklendi ✅

#### 2. Prompt Engineering Optimizasyonu

**Amaç:** LLM'in daha doğru ve tutarlı cevaplar vermesi için prompt'ları optimize etmek.

**Görevler:**
- [ ] System prompt'un daha detaylı hale getirilmesi
- [ ] Intent-specific prompt'ların iyileştirilmesi
- [ ] Documentation snippet'lerinin prompt'a daha etkili entegre edilmesi
- [ ] Few-shot examples eklenmesi (örnek soru-cevap çiftleri)
- [ ] Response format'ının standardize edilmesi

**Örnek İyileştirmeler:**
- System prompt'a Moni'nin görevleri daha detaylı eklendi ✅
- Intent-specific instructions eklendi ✅
- Documentation snippet'leri prompt'a dahil edildi ✅

#### 3. Response Quality Monitoring

**Amaç:** Chatbot cevaplarının kalitesini izlemek ve iyileştirmek.

**Görevler:**
- [ ] Kullanıcı feedback mekanizması (👍/👎 butonları)
- [ ] Response quality metrics (doğruluk, tutarlılık, kullanıcı memnuniyeti)
- [ ] Hatalı cevapların loglanması ve analizi
- [ ] Düzenli dokümantasyon review'ları

#### 4. Context Management İyileştirmesi

**Amaç:** Chatbot'un konuşma bağlamını daha iyi anlaması.

**Görevler:**
- [ ] Conversation history'nin daha etkili kullanılması
- [ ] Context window'un optimize edilmesi
- [ ] Önceki soruların ve cevapların daha iyi referans edilmesi
- [ ] Multi-turn conversation desteğinin iyileştirilmesi

#### 5. Dokümantasyon Coverage Artırma

**Amaç:** Chatbot'un daha fazla konuda yardımcı olabilmesi.

**Görevler:**
- [ ] Eksik dokümantasyon bölümlerinin tamamlanması
- [ ] Yeni özellikler için dokümantasyon eklenmesi
- [ ] API dokümantasyonlarının chatbot için optimize edilmesi
- [ ] UI guide'ların chatbot için yapılandırılması

### Implementation Steps

#### Step 1: Dokümantasyon Review ve İyileştirme

1. Mevcut dokümantasyon dosyalarını gözden geçir
2. Eksik `summary` ve `faq` bölümlerini ekle
3. Kavram karışıklıklarını gider (örnek: Dataset Kategorisi vs Field Type)
4. Örnek senaryolar ekle

#### Step 2: Prompt Engineering İyileştirmesi

1. System prompt'u gözden geçir ve optimize et
2. Intent-specific prompt'ları iyileştir
3. Few-shot examples ekle
4. Response format'ını standardize et

#### Step 3: Response Quality Monitoring

1. Feedback mekanizması ekle (frontend)
2. Metrics toplama sistemi kur (backend)
3. Log analizi için dashboard oluştur

#### Step 4: Context Management İyileştirmesi

1. Conversation history yönetimini optimize et
2. Context window'u ayarla
3. Multi-turn conversation desteğini test et

### Test Senaryoları

1. **Kavram Doğruluğu Testi**
   - Soru: "Dataset kategorisi nedir?"
   - Beklenen: Field type'larla karıştırılmamalı, doğru açıklama verilmeli

2. **Tutarlılık Testi**
   - Aynı soru farklı zamanlarda sorulduğunda benzer cevaplar alınmalı

3. **Dokümantasyon Referans Testi**
   - Cevaplarda ilgili dokümantasyon kaynakları belirtilmeli

4. **Multi-turn Conversation Testi**
   - Önceki sorulara referans verilebilmeli

### Öncelikli İyileştirmeler

1. **Yüksek Öncelik:**
   - Dokümantasyon kalitesi artırma (summary, faq ekleme)
   - Prompt engineering optimizasyonu
   - Kavram karışıklıklarının giderilmesi

2. **Orta Öncelik:**
   - Response quality monitoring
   - Context management iyileştirmesi

3. **Düşük Öncelik:**
   - Dokümantasyon coverage artırma
   - Advanced features (sentiment analysis, etc.)

### Notlar

- Chatbot cevaplarının iyileştirilmesi sürekli bir süreçtir
- Kullanıcı feedback'leri önemlidir
- Dokümantasyon kalitesi chatbot cevaplarının kalitesini doğrudan etkiler
- Prompt engineering, model değişikliklerinde tekrar gözden geçirilmelidir

---

## 🛠️ Teknik Detaylar

### Clean Architecture Pattern

MngLLM servisi, MngDataGateway pattern'ini takip eder:

- **Domain Layer:** Entities, Interfaces, Exceptions
- **Application Layer:** Commands/Queries (CQRS), DTOs, Configuration
- **Infrastructure Layer:** Ollama Adapter, HTTP Clients, Caching
- **Presentation Layer:** REST API Controllers, Middleware

### Ollama Integration

**Model Seçimi:**
- Test: Qwen2.5 3B (hafif, hızlı)
- Production: Qwen2.5 7B veya RN_TR_R1 (daha iyi Türkçe)

**API Endpoints:**
- `POST /api/generate` - Text generation
- `GET /api/tags` - Available models
- `POST /api/pull` - Model download

### Caching Strategy

- **Translation Cache:** Redis, TTL: 1 saat
- **Schema Cache:** Memory cache, TTL: 30 dakika
- **Query Cache:** Redis, TTL: 5 dakika (opsiyonel)

### Error Handling

- LLM servisi çalışmıyorsa fallback mekanizması
- Timeout handling (default: 30 saniye)
- Rate limiting (opsiyonel)
- Retry logic (opsiyonel)

---

## 💻 Kaynak Gereksinimleri

### Test Ortamı (Mevcut Sunucu)

- **CPU:** 4-6 core (Ollama için)
- **RAM:** 4-6 GB (Ollama + model)
- **Disk:** 10-20 GB (model storage)
- **Model:** Qwen2.5 3B (yaklaşık 2-3 GB)

### Production Ortamı (Ayrı Sunucu - Önerilen)

- **CPU:** 8+ core (veya GPU)
- **RAM:** 16+ GB
- **Disk:** 50+ GB
- **Model:** Qwen2.5 7B veya RN_TR_R1 (4-8 GB)

---

## 📝 Sonraki Adımlar

1. ✅ Roadmap oluşturuldu
2. 📋 Ollama Docker container kurulumu
3. 📋 MngLLM Service proje yapısı oluşturma
4. 📋 Faz 1: Dil Dosyası Güncelleme implementasyonu
5. 📋 Faz 2: Dataset Sorgulama (NLQ) implementasyonu

---

## 🔗 İlgili Dokümanlar

- [Scenario Analysis](SCENARIO_ANALYSIS.md) - Senaryo analiz raporu
- [MngDataGateway Architecture](../MngDataGateway/architecture/ARCHITECTURE_GUIDE.md) - Mimari rehber
- [MngKeeper Roadmap](../../MngKeeper/ROADMAP.md) - MngKeeper roadmap

---

**Son Güncelleme:** 16 Ocak 2026
