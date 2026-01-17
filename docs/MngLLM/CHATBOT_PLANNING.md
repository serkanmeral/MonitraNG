# Chatbot Planlama Dokümanı

**Tarih:** 15 Ocak 2026  
**Servis:** MngLLM  
**Durum:** 📋 Planlama Aşaması  
**Versiyon:** 1.0.0

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Kullanım Senaryoları](#kullanım-senaryoları)
3. [Mimari Tasarım](#mimari-tasarım)
4. [Özellikler ve Öncelikler](#özellikler-ve-öncelikler)
5. [Teknik Gereksinimler](#teknik-gereksinimler)
6. [UI/UX Tasarım](#uiux-tasarım)
7. [Güvenlik ve Performans](#güvenlik-ve-performans)
8. [Implementasyon Planı](#implementasyon-planı)
9. [Test Stratejisi](#test-stratejisi)

---

## 🎯 Genel Bakış

### Amaç

MonitraNG platformu için kapsamlı bir chatbot sistemi geliştirmek. Chatbot, kullanıcılara:
- **Dataset Sorgulama (NLQ)**: Doğal dil ile veri sorgulama
- **Dokümantasyon Yardımı**: Platform dokümantasyonu arama ve açıklama
- **Kullanıcı Rehberi**: Adım adım kullanım talimatları
- **Genel Platform Yardımı**: Platform özellikleri hakkında bilgi

### Kapsam

Chatbot, MngLLM servisi üzerinden çalışacak ve şu servislerle entegre olacak:
- **MngLLM**: LLM işlemleri (mevcut)
- **MngDataGateway**: Dataset sorgulama ve schema bilgisi
- **MngKeeper**: Kullanıcı bilgileri ve yetkilendirme
- **Dokümantasyon Sistemi**: `docs/` klasöründeki markdown dosyaları

---

## 💬 Kullanım Senaryoları

### Senaryo 1: Dataset Sorgulama (NLQ)

**Kullanıcı:** "Sayfa sayısı 50'den fazla kaç kitap var?"

**Chatbot İşlemi:**
1. Doğal dili anla
2. Dataset schema'sını al (MngDataGateway)
3. MongoDB filter'a dönüştür (LLM)
4. MngDataGateway API'ye sorgu gönder
5. Sonuçları kullanıcı dostu formatta göster

**Örnekler:**
- "2020 yılından sonra yayınlanan kitaplar"
- "En çok satan 10 ürün"
- "Bu ay tamamlanan görevlerin sayısı"

### Senaryo 2: Dokümantasyon Arama

**Kullanıcı:** "Validasyon kuralları nasıl çalışır?"

**Chatbot İşlemi:**
1. Dokümantasyon dosyalarını ara (`docs/` klasörü)
2. İlgili bölümleri bul
3. LLM ile özetle ve açıkla
4. Kaynak linklerini göster

**Örnekler:**
- "Dataset nasıl oluşturulur?"
- "API authentication nasıl yapılır?"
- "MongoDB connection string formatı nedir?"

### Senaryo 3: Kullanıcı Rehberi

**Kullanıcı:** "Yeni bir dataset nasıl oluştururum?"

**Chatbot İşlemi:**
1. Adım adım talimatlar oluştur
2. Gerekirse ekran görüntüleri veya linkler ekle
3. İlgili dokümantasyonu referans göster

**Örnekler:**
- "Şifremi nasıl değiştiririm?"
- "Yeni bir kullanıcı nasıl eklerim?"
- "Dataset'e nasıl veri eklerim?"

### Senaryo 4: Genel Platform Yardımı

**Kullanıcı:** "MonitraNG'de hangi özellikler var?"

**Chatbot İşlemi:**
1. Platform özelliklerini listele
2. Kullanıcının rolüne göre özelleştir
3. İlgili sayfalara yönlendir

**Örnekler:**
- "Hangi servisler mevcut?"
- "Real-time event nasıl çalışır?"
- "Multi-tenant yapı nasıl çalışıyor?"

---

## 🏗️ Mimari Tasarım

### Genel Mimari

```
┌─────────────────────────────────────────────────────────────┐
│                    Mng.Ui (Frontend)                         │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Chatbot UI Component                         │   │
│  │  - Chat Interface                                    │   │
│  │  - Message History                                   │   │
│  │  - Context Management                                │   │
│  └──────────────────────────────────────────────────────┘   │
└───────────────────────┬─────────────────────────────────────┘
                        │ HTTP/REST
                        ▼
┌─────────────────────────────────────────────────────────────┐
│              MngGateway (API Gateway)                        │
│  - Authentication                                            │
│  - Rate Limiting                                             │
│  - Routing                                                   │
└───────────────────────┬─────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┐
        │               │               │
        ▼               ▼               ▼
┌──────────────┐ ┌──────────────┐ ┌──────────────┐
│   MngLLM     │ │ MngDataGateway│ │  MngKeeper   │
│   Service    │ │    Service    │ │   Service    │
│              │ │               │ │              │
│ - Chat API   │ │ - Dataset API │ │ - User Info  │
│ - NLQ        │ │ - Schema API  │ │ - Auth       │
│ - Docs       │ │ - Query API   │ │              │
│ - Context    │ │               │ │              │
└──────┬───────┘ └───────────────┘ └──────────────┘
       │
       ▼
┌──────────────┐
│   Ollama     │
│   Service    │
│              │
│ - LLM Model  │
│ - Inference  │
└──────────────┘
```

### Chatbot Akış Diyagramı

```
Kullanıcı Mesajı
      │
      ▼
┌─────────────────┐
│ Intent Detection│ (LLM ile)
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌────────┐ ┌──────────┐
│ NLQ    │ │ Docs     │
│ Query  │ │ Search   │
└───┬────┘ └────┬─────┘
    │          │
    ▼          ▼
┌──────────┐ ┌──────────┐
│ Data     │ │ LLM      │
│ Gateway  │ │ Summary  │
└────┬─────┘ └────┬──────┘
     │           │
     └─────┬─────┘
           │
           ▼
    ┌──────────────┐
    │ Response     │
    │ Formatting   │
    └──────┬───────┘
           │
           ▼
    Kullanıcıya Cevap
```

### Context Yönetimi

**Konuşma Geçmişi:**
- Son N mesaj saklanır (örn: 10 mesaj)
- Session-based context (kullanıcı bazlı)
- Context window: 4096 token (model bağımlı)

**Context İçeriği:**
- Kullanıcı bilgileri (rol, domain)
- Aktif dataset (eğer varsa)
- Son sorgular ve sonuçlar
- Platform durumu (hata mesajları, vb.)

---

## 🎯 Özellikler ve Öncelikler

### Faz 1: Temel Chatbot Altyapısı (Yüksek Öncelik)

**Süre:** 2-3 hafta

**Özellikler:**
- ✅ Chat UI component (Vue 3 + Vuetify)
- ✅ Mesaj gönderme/alma
- ✅ Context management (session-based)
- ✅ Temel chatbot API endpoint
- ✅ Error handling ve loading states

**Backend:**
- `POST /api/v1/llm/chat` - Chat endpoint
- Context storage (memory/Redis)
- Intent detection (basit)

**Frontend:**
- `components/apps/chatbot/ChatbotWidget.vue`
- `components/apps/chatbot/ChatMessage.vue`
- `composables/useChatbot.ts`

### Faz 2: Dataset Sorgulama (NLQ) (Yüksek Öncelik)

**Süre:** 3-4 hafta

**Özellikler:**
- ✅ Natural Language → MongoDB Filter dönüşümü
- ✅ Dataset schema context provider
- ✅ Query execution (MngDataGateway)
- ✅ Sonuç formatlama ve gösterim
- ✅ Hata durumları için kullanıcı dostu mesajlar

**Backend:**
- `POST /api/v1/llm/query` - NLQ endpoint
- `IDatasetSchemaProvider` interface
- `NaturalLanguageQueryCommand` (CQRS)
- Schema caching (30 dakika)

**Frontend:**
- Dataset seçimi
- Query input ve sonuç gösterimi
- Tablo formatında sonuçlar

### Faz 3: Dokümantasyon Arama (Orta Öncelik)

**Süre:** 2-3 hafta

**Özellikler:**
- ✅ Dokümantasyon dosyalarını indeksleme
- ✅ Semantic search (veya keyword search)
- ✅ LLM ile özetleme ve açıklama
- ✅ Kaynak linklerini gösterme

**Backend:**
- `POST /api/v1/llm/docs/search` - Docs search endpoint
- `IDocumentationProvider` interface
- Markdown parsing ve indexing
- Vector search (opsiyonel - gelecekte)

**Frontend:**
- Dokümantasyon sonuçları gösterimi
- Link navigation
- Code snippet highlighting

### Faz 4: Kullanıcı Rehberi (Orta Öncelik)

**Süre:** 1-2 hafta

**Özellikler:**
- ✅ Adım adım talimatlar oluşturma
- ✅ Platform özelliklerini açıklama
- ✅ İlgili sayfalara yönlendirme

**Backend:**
- `POST /api/v1/llm/guide` - Guide endpoint
- Predefined guide templates
- LLM ile özelleştirme

**Frontend:**
- Step-by-step guide gösterimi
- Navigation links

### Faz 5: Gelişmiş Özellikler (Düşük Öncelik)

**Süre:** 2-3 hafta

**Özellikler:**
- ✅ Multi-turn conversation (context-aware)
- ✅ Voice input (gelecekte)
- ✅ Export conversation
- ✅ Conversation history
- ✅ Suggested queries
- ✅ Analytics ve monitoring

---

## 🛠️ Teknik Gereksinimler

### Backend (MngLLM Service)

#### Yeni Endpoints

```csharp
// Chat endpoint
POST /api/v1/llm/chat
{
  "message": "Sayfa sayısı 50'den fazla kaç kitap var?",
  "context": {
    "sessionId": "...",
    "domainName": "...",
    "datasetId": "..." // optional
  }
}

Response:
{
  "response": "...",
  "intent": "nlq" | "docs" | "guide" | "general",
  "data": { ... }, // optional (query results, etc.)
  "sources": [ ... ], // optional (documentation links)
  "sessionId": "..."
}

// NLQ endpoint (alternatif - direkt query için)
POST /api/v1/llm/query
{
  "dataset": "tst_books",
  "query": "Sayfa sayısı 50'den fazla kaç kitap var?",
  "domainName": "..."
}

// Docs search endpoint
POST /api/v1/llm/docs/search
{
  "query": "Validasyon kuralları nasıl çalışır?",
  "limit": 5
}
```

#### Yeni Servisler ve Interface'ler

```csharp
// Application Layer
public interface IChatbotService
{
    Task<ChatResponseDto> ProcessMessageAsync(ChatRequestDto request, CancellationToken cancellationToken);
    Task<NaturalLanguageQueryResponseDto> ProcessQueryAsync(NaturalLanguageQueryRequestDto request, CancellationToken cancellationToken);
    Task<DocumentationSearchResponseDto> SearchDocumentationAsync(DocumentationSearchRequestDto request, CancellationToken cancellationToken);
}

// Domain Layer
public interface IDatasetSchemaProvider
{
    Task<DatasetSchema> GetSchemaAsync(string datasetName, string domainName, CancellationToken cancellationToken);
}

public interface IDocumentationProvider
{
    Task<List<DocumentationResult>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
    Task<string> GetContentAsync(string filePath, CancellationToken cancellationToken);
}

// Infrastructure Layer
public class DatasetSchemaProvider : IDatasetSchemaProvider
{
    // MngDataGateway API client
    // Schema caching
}

public class DocumentationProvider : IDocumentationProvider
{
    // Markdown file reading
    // Indexing (basit keyword search veya vector search)
}
```

#### Context Management

```csharp
public interface IContextManager
{
    Task<ConversationContext> GetContextAsync(string sessionId, CancellationToken cancellationToken);
    Task SaveContextAsync(string sessionId, ConversationContext context, CancellationToken cancellationToken);
    Task ClearContextAsync(string sessionId, CancellationToken cancellationToken);
}

public class ConversationContext
{
    public string SessionId { get; set; }
    public string DomainName { get; set; }
    public string UserId { get; set; }
    public List<ChatMessage> Messages { get; set; } // Son N mesaj
    public string? ActiveDataset { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
}
```

### Frontend (Mng.Ui)

#### Yeni Component'ler

```
Mng.Ui/
├── components/
│   └── apps/
│       └── chatbot/
│           ├── ChatbotWidget.vue      # Ana chatbot widget
│           ├── ChatMessage.vue        # Mesaj component'i
│           ├── ChatInput.vue          # Input component'i
│           ├── QueryResults.vue       # NLQ sonuçları
│           └── DocumentationResults.vue # Docs sonuçları
├── composables/
│   └── useChatbot.ts                  # Chatbot logic
└── stores/
    └── chatbot.ts                     # Chatbot state management
```

#### API Integration

```typescript
// services/apiService.ts
export async function sendChatMessage(
  message: string,
  context?: ChatContext
): Promise<ChatResponse> {
  return await fetchFromMngLLM('/api/v1/llm/chat', 'POST', {
    message,
    context
  });
}

export async function queryDataset(
  dataset: string,
  query: string,
  domainName: string
): Promise<QueryResponse> {
  return await fetchFromMngLLM('/api/v1/llm/query', 'POST', {
    dataset,
    query,
    domainName
  });
}
```

---

## 🎨 UI/UX Tasarım

### Chatbot Widget

**Konum:**
- Floating button (sağ alt köşe) - her sayfada erişilebilir
- Veya sidebar'da sabit panel
- Veya full-page chatbot sayfası

**Tasarım Özellikleri:**
- Modern, minimal tasarım
- Dark/Light mode desteği
- Responsive (mobil uyumlu)
- Animasyonlar (mesaj gönderme/alma)
- Typing indicator
- Error states
- Loading states

**Mesaj Formatları:**
- Text messages
- Query results (tablo formatında)
- Documentation links
- Code snippets
- Step-by-step guides

### Kullanıcı Deneyimi

**Özellikler:**
- Auto-scroll to latest message
- Message timestamps
- Copy message
- Regenerate response
- Clear conversation
- Export conversation

---

## 🔒 Güvenlik ve Performans

### Güvenlik

- ✅ JWT authentication (mevcut)
- ✅ Domain-based access control
- ✅ Rate limiting (API Gateway'de)
- ✅ Input validation ve sanitization
- ✅ SQL injection koruması (MongoDB filter validation)
- ✅ XSS koruması (frontend'de)

### Performans

- ✅ Context caching (Redis veya memory)
- ✅ Schema caching (30 dakika)
- ✅ Response streaming (gelecekte - SSE)
- ✅ Query timeout (30 saniye)
- ✅ LLM timeout (30 saniye)

### Monitoring

- ✅ Request/response logging
- ✅ Error tracking
- ✅ Performance metrics (response time)
- ✅ Usage analytics (hangi intent'ler daha çok kullanılıyor)

---

## 📅 Implementasyon Planı

### Faz 1: Temel Altyapı (2-3 hafta)

**Hafta 1:**
- Backend: Chat endpoint ve context management
- Frontend: Temel chat UI component

**Hafta 2:**
- Backend: Intent detection
- Frontend: Message history ve state management
- Test: Temel chat akışı

**Hafta 3:**
- Polish ve bug fixes
- Documentation

### Faz 2: NLQ (3-4 hafta)

**Hafta 1:**
- Backend: Dataset schema provider
- Backend: NLQ command handler

**Hafta 2:**
- Backend: Query transformation logic
- Backend: MngDataGateway integration

**Hafta 3:**
- Frontend: Dataset selection
- Frontend: Query results display

**Hafta 4:**
- Test ve polish
- Error handling improvements

### Faz 3: Dokümantasyon (2-3 hafta)

**Hafta 1:**
- Backend: Documentation provider
- Backend: Markdown parsing ve indexing

**Hafta 2:**
- Backend: Search logic
- Frontend: Documentation results display

**Hafta 3:**
- Test ve polish

### Faz 4: Kullanıcı Rehberi (1-2 hafta)

**Hafta 1:**
- Backend: Guide templates
- Frontend: Guide display

**Hafta 2:**
- Test ve polish

---

## 🧪 Test Stratejisi

### Unit Tests

- Intent detection logic
- Query transformation
- Schema provider
- Documentation search

### Integration Tests

- Chat endpoint → LLM → Response
- NLQ → Schema → Query → Results
- Documentation search → Results

### E2E Tests

- Kullanıcı chat akışı
- NLQ sorgulama akışı
- Documentation arama akışı

### Test Senaryoları

1. **Basit Chat:**
   - Kullanıcı: "Merhaba"
   - Expected: Chatbot selamlaşır

2. **NLQ Sorgu:**
   - Kullanıcı: "Sayfa sayısı 50'den fazla kaç kitap var?"
   - Expected: Doğru filter oluşturulur, sonuçlar gösterilir

3. **Dokümantasyon Arama:**
   - Kullanıcı: "Validasyon kuralları nasıl çalışır?"
   - Expected: İlgili dokümantasyon bulunur ve özetlenir

4. **Hata Durumları:**
   - Dataset bulunamadı
   - LLM servisi çalışmıyor
   - Query anlaşılamadı

---

## 📝 Sonraki Adımlar

1. ✅ Planlama dokümanı oluşturuldu
2. 📋 Kullanıcı onayı al
3. 📋 Detaylı teknik spesifikasyon
4. 📋 Faz 1 implementasyonuna başla

---

**Son Güncelleme:** 15 Ocak 2026
