# Chatbot Implementasyon Planı

**Tarih:** 15 Ocak 2026  
**Servis:** MngLLM  
**Durum:** 📋 Planlama Tamamlandı - Implementasyona Hazır  
**Versiyon:** 1.0.0  
**Chatbot İsmi:** **Moni**

---

## 📋 İÇİNDEKİLER

1. [Genel Yaklaşım](#genel-yaklaşım)
2. [Faz 1: Dokümantasyon Provider](#faz-1-dokümantasyon-provider)
3. [Faz 2: Chatbot Backend](#faz-2-chatbot-backend)
4. [Faz 3: Chatbot Frontend](#faz-3-chatbot-frontend)
5. [Token Yönetimi](#token-yönetimi)
6. [Test Stratejisi](#test-stratejisi)

---

## 🎯 Genel Yaklaşım

### Profesyonel Öneriler

1. **İndeksleme:** Basit keyword search ile başla (Faz 1), gelecekte vector search eklenebilir
2. **Dokümantasyon:** MkDocs markdown + OpenAPI JSON (runtime'da al)
3. **Token Yönetimi:** Mevcut auth store yapısını kullan (cookie-based)
4. **Incremental Development:** Her fazı tamamla, test et, sonraki faza geç

### Mimari Kararlar

- **Clean Architecture:** Mevcut MngLLM pattern'ini takip et
- **CQRS:** MediatR ile command/query pattern
- **Dependency Injection:** Interface-based design
- **Error Handling:** Comprehensive error handling ve logging

---

## 📦 Faz 1: Dokümantasyon Provider (Backend)

**Süre:** 1-2 hafta  
**Öncelik:** Yüksek (Chatbot'un temel altyapısı)

### 1.1 Domain Layer

**Dosya:** `MngLLM/Core/MngLLM.Domain/Interfaces/IDocumentationProvider.cs`

```csharp
namespace MngLLM.Domain.Interfaces;

public interface IDocumentationProvider
{
    /// <summary>
    /// Search documentation by query
    /// </summary>
    Task<List<DocumentationResult>> SearchAsync(
        string query, 
        int limit = 5, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get full content of a document
    /// </summary>
    Task<string> GetContentAsync(
        string documentId, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all indexed documents
    /// </summary>
    Task<List<DocumentationIndex>> GetAllDocumentsAsync(
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Re-index all documentation
    /// </summary>
    Task ReindexAsync(CancellationToken cancellationToken = default);
}

public class DocumentationResult
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Snippet { get; set; } // Özet/kısa içerik
    public string Source { get; set; } // "markdown" | "openapi"
    public string Service { get; set; } // "MngDataGateway", "MngKeeper", etc.
    public string Category { get; set; } // "api", "architecture", "guide", etc.
    public string FilePath { get; set; }
    public double RelevanceScore { get; set; } // 0-1 arası
    public Dictionary<string, object> Metadata { get; set; }
}

public class DocumentationIndex
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public string Source { get; set; }
    public string Service { get; set; }
    public string Category { get; set; }
    public string FilePath { get; set; }
    public List<string> Keywords { get; set; }
    public DateTime LastUpdated { get; set; }
}
```

### 1.2 Infrastructure Layer

**Dosya:** `MngLLM/Infrastructure/MngLLM.Infrastructure/Services/DocumentationProvider.cs`

**Özellikler:**
- Markdown parser (Markdig veya basit regex)
- OpenAPI JSON parser (System.Text.Json)
- Keyword index (in-memory Dictionary)
- Search algoritması (basit text matching + keyword matching)

**Dependencies:**
```xml
<PackageReference Include="Markdig" Version="0.33.0" />
```

**Implementasyon Stratejisi:**

1. **Markdown Parser:**
   - Markdig kullan (profesyonel markdown parser)
   - Başlıkları, paragrafları, kod bloklarını extract et
   - Front matter (YAML) parse et (metadata için)

2. **OpenAPI Parser:**
   - System.Text.Json ile parse et
   - Endpoint'leri, schema'ları, örnekleri extract et
   - Her endpoint için ayrı dokümantasyon index oluştur

3. **Keyword Index:**
   - Basit inverted index (Dictionary<string, List<string>>)
   - Keyword → Document ID listesi
   - Case-insensitive search

4. **Search Algoritması:**
   - Keyword matching (exact match)
   - Title matching (yüksek öncelik)
   - Content matching (düşük öncelik)
   - Relevance score hesapla

### 1.3 Configuration

**Dosya:** `MngLLM/Core/MngLLM.Application/Configuration/MngLLMSettings.cs`

```csharp
public class DocumentationSettings
{
    public string MarkdownPath { get; set; } = "docs/content";
    public string OpenApiBaseUrl { get; set; } = "http://localhost:5010"; // MngDataGateway
    public List<ServiceEndpoint> ServiceEndpoints { get; set; } = new();
    public int SearchLimit { get; set; } = 5;
    public int ReindexIntervalMinutes { get; set; } = 60;
    public bool EnableAutoReindex { get; set; } = true;
}

public class ServiceEndpoint
{
    public string ServiceName { get; set; }
    public string BaseUrl { get; set; }
    public string OpenApiPath { get; set; } = "/api-docs/v1/swagger.json";
}
```

**appsettings.json:**
```json
{
  "MngLLMSettings": {
    "Documentation": {
      "MarkdownPath": "../../docs/content",
      "SearchLimit": 5,
      "ReindexIntervalMinutes": 60,
      "EnableAutoReindex": true,
      "ServiceEndpoints": [
        {
          "ServiceName": "MngDataGateway",
          "BaseUrl": "http://mngdatagateway:5010",
          "OpenApiPath": "/api-docs/v1/swagger.json"
        },
        {
          "ServiceName": "MngKeeper",
          "BaseUrl": "http://mngkeeper:5001",
          "OpenApiPath": "/api-docs/v1/swagger.json"
        }
      ]
    }
  }
}
```

### 1.4 Service Registration

**Dosya:** `MngLLM/Infrastructure/MngLLM.Infrastructure/ServiceRegistration.cs`

```csharp
services.AddSingleton<IDocumentationProvider, DocumentationProvider>();
services.AddHostedService<DocumentationIndexingService>(); // Periodic re-indexing
```

### 1.5 API Endpoint (Opsiyonel - Re-index için)

**Dosya:** `MngLLM/Presentation/MngLLM.Api/Controllers/DocumentationController.cs`

```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/docs")]
[Authorize]
public class DocumentationController : ControllerBase
{
    private readonly IDocumentationProvider _documentationProvider;
    
    [HttpPost("reindex")]
    public async Task<IActionResult> ReindexAsync(CancellationToken cancellationToken)
    {
        await _documentationProvider.ReindexAsync(cancellationToken);
        return Ok(new { message = "Re-indexing completed" });
    }
    
    [HttpGet("search")]
    public async Task<IActionResult> SearchAsync(
        [FromQuery] string query,
        [FromQuery] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var results = await _documentationProvider.SearchAsync(query, limit, cancellationToken);
        return Ok(results);
    }
}
```

---

## 💬 Faz 2: Chatbot Backend

**Süre:** 2-3 hafta  
**Öncelik:** Yüksek

### 2.1 Domain Layer

**Dosya:** `MngLLM/Core/MngLLM.Domain/Interfaces/IChatbotService.cs`

```csharp
namespace MngLLM.Domain.Interfaces;

public interface IChatbotService
{
    /// <summary>
    /// Process user message and generate response
    /// </summary>
    Task<ChatResponseDto> ProcessMessageAsync(
        ChatRequestDto request, 
        CancellationToken cancellationToken = default);
}
```

### 2.2 Application Layer - Commands

**Dosya:** `MngLLM/Core/MngLLM.Application/Commands/Chat/ChatCommand.cs`

```csharp
namespace MngLLM.Application.Commands.Chat;

public class ChatCommand : IRequest<ChatResponseDto>
{
    public string Message { get; set; }
    public string SessionId { get; set; }
    public string DomainName { get; set; }
    public string UserId { get; set; }
    public ConversationContext? Context { get; set; }
}

public class ConversationContext
{
    public List<ChatMessage> Messages { get; set; } = new();
    public string? ActiveDataset { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ChatMessage
{
    public string Role { get; set; } // "user" | "assistant"
    public string Content { get; set; }
    public DateTime Timestamp { get; set; }
}
```

**Dosya:** `MngLLM/Core/MngLLM.Application/Commands/Chat/ChatCommandHandler.cs`

**Intent Detection:**
- LLM ile intent belirleme (basit prompt)
- Intent'ler: `nlq`, `docs`, `guide`, `general`

**Context Hazırlama:**
- Kullanıcı mesajını analiz et
- İlgili dokümantasyonları bul (DocumentationProvider)
- Context window'a sığacak şekilde formatla
- LLM'e gönder

**Response Formatting:**
- LLM response'unu parse et
- Intent'e göre formatla
- Kaynak linklerini ekle

### 2.3 Context Management

**Dosya:** `MngLLM/Infrastructure/MngLLM.Infrastructure/Services/ContextManager.cs`

```csharp
public interface IContextManager
{
    Task<ConversationContext> GetContextAsync(string sessionId, CancellationToken cancellationToken);
    Task SaveContextAsync(string sessionId, ConversationContext context, CancellationToken cancellationToken);
    Task ClearContextAsync(string sessionId, CancellationToken cancellationToken);
}

// In-memory implementation (basit başlangıç)
// Gelecekte: Redis veya MongoDB
```

**Strateji:**
- In-memory Dictionary (başlangıç)
- Session-based (her kullanıcı için ayrı session)
- Son 10 mesaj sakla (context window yönetimi)
- TTL: 30 dakika (inactive session'ları temizle)

### 2.4 API Endpoint

**Dosya:** `MngLLM/Presentation/MngLLM.Api/Controllers/ChatbotController.cs`

```csharp
[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/chatbot")]
[Authorize]
public class ChatbotController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IContextManager _contextManager;
    
    [HttpPost("chat")]
    public async Task<ActionResult<ChatResponseDto>> ChatAsync(
        [FromBody] ChatRequestDto request,
        CancellationToken cancellationToken)
    {
        // Get or create session
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString();
        
        // Get context
        var context = await _contextManager.GetContextAsync(sessionId, cancellationToken);
        
        // Add user message to context
        context.Messages.Add(new ChatMessage
        {
            Role = "user",
            Content = request.Message,
            Timestamp = DateTime.UtcNow
        });
        
        // Process message
        var command = new ChatCommand
        {
            Message = request.Message,
            SessionId = sessionId,
            DomainName = request.DomainName,
            UserId = User.FindFirst("sub")?.Value ?? "",
            Context = context
        };
        
        var response = await _mediator.Send(command, cancellationToken);
        
        // Add assistant response to context
        context.Messages.Add(new ChatMessage
        {
            Role = "assistant",
            Content = response.Response,
            Timestamp = DateTime.UtcNow
        });
        
        // Keep only last 10 messages
        if (context.Messages.Count > 10)
        {
            context.Messages = context.Messages.TakeLast(10).ToList();
        }
        
        // Save context
        await _contextManager.SaveContextAsync(sessionId, context, cancellationToken);
        
        return Ok(response);
    }
    
    [HttpDelete("session/{sessionId}")]
    public async Task<IActionResult> ClearSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        await _contextManager.ClearContextAsync(sessionId, cancellationToken);
        return Ok(new { message = "Session cleared" });
    }
}
```

### 2.5 DTOs

**Dosya:** `MngLLM/Core/MngLLM.Application/DTOs/ChatRequestDto.cs`

```csharp
public class ChatRequestDto
{
    public string Message { get; set; }
    public string? SessionId { get; set; }
    public string DomainName { get; set; }
    public string? DatasetId { get; set; } // Optional - aktif dataset
}

public class ChatResponseDto
{
    public string Response { get; set; }
    public string Intent { get; set; } // "nlq" | "docs" | "guide" | "general"
    public string SessionId { get; set; }
    public object? Data { get; set; } // Optional - query results, etc.
    public List<DocumentationSource>? Sources { get; set; } // Optional - documentation links
    public Dictionary<string, object>? Metadata { get; set; }
}

public class DocumentationSource
{
    public string Title { get; set; }
    public string Url { get; set; }
    public string Snippet { get; set; }
}
```

---

## 🎨 Faz 3: Chatbot Frontend

**Süre:** 2-3 hafta  
**Öncelik:** Yüksek

### 3.1 Chatbot Widget Component

**Dosya:** `Mng.Ui/components/apps/chatbot/ChatbotWidget.vue`

**Özellikler:**
- Floating button (sağ alt köşe)
- Chat interface (mesaj geçmişi)
- Input field
- Loading states
- Error handling
- Auto-scroll

**Tasarım:**
- Modern, minimal
- Dark/Light mode desteği
- Responsive
- Animasyonlar

### 3.2 Chat Message Component

**Dosya:** `Mng.Ui/components/apps/chatbot/ChatMessage.vue`

**Özellikler:**
- User/Assistant mesajları
- Timestamp
- Formatting (markdown support)
- Copy message
- Documentation links

### 3.3 Composable

**Dosya:** `Mng.Ui/composables/useChatbot.ts`

```typescript
export const useChatbot = () => {
  const sessionId = ref<string | null>(null);
  const messages = ref<ChatMessage[]>([]);
  const isLoading = ref(false);
  const error = ref<string | null>(null);
  
  const sendMessage = async (message: string, domainName: string) => {
    isLoading.value = true;
    error.value = null;
    
    try {
      // Get or create session
      if (!sessionId.value) {
        sessionId.value = crypto.randomUUID();
      }
      
      // Add user message
      messages.value.push({
        role: 'user',
        content: message,
        timestamp: new Date()
      });
      
      // Call API
      const response = await fetchFromMngLLM('/api/v1/chatbot/chat', 'POST', {
        message,
        sessionId: sessionId.value,
        domainName
      });
      
      // Add assistant response
      messages.value.push({
        role: 'assistant',
        content: response.response,
        timestamp: new Date(),
        intent: response.intent,
        sources: response.sources,
        data: response.data
      });
    } catch (err: any) {
      error.value = err.message || 'Bir hata oluştu';
    } finally {
      isLoading.value = false;
    }
  };
  
  const clearSession = async () => {
    if (sessionId.value) {
      await fetchFromMngLLM(`/api/v1/chatbot/session/${sessionId.value}`, 'DELETE');
      sessionId.value = null;
      messages.value = [];
    }
  };
  
  return {
    sessionId,
    messages,
    isLoading,
    error,
    sendMessage,
    clearSession
  };
};
```

### 3.4 API Service Integration

**Dosya:** `Mng.Ui/services/apiService.ts`

```typescript
// Mevcut fetchFromMngLLM fonksiyonunu kullan
// Token yönetimi otomatik (cookie-based)
```

**Server Route:** `Mng.Ui/server/api/llm/[...path].ts` (zaten mevcut)

### 3.5 Store (Opsiyonel)

**Dosya:** `Mng.Ui/stores/chatbot.ts`

```typescript
export const useChatbotStore = defineStore('chatbot', {
  state: () => ({
    isOpen: false,
    sessionId: null as string | null,
    messages: [] as ChatMessage[]
  }),
  
  actions: {
    toggle() {
      this.isOpen = !this.isOpen;
    },
    // ...
  }
});
```

---

## 🌐 Çoklu Dil Desteği (i18n)

### Desteklenen Diller

- 🇹🇷 **Türkçe (tr)** - Varsayılan dil
- 🇬🇧 **İngilizce (en)** - Fallback dil
- 🇫🇷 **Fransızca (fr)**
- 🇸🇦 **Arapça (ar)** - RTL desteği
- 🇨🇳 **Çince (zh)**

### Mevcut i18n Yapısı

- **Frontend:** `vue-i18n` (zaten kurulu)
- **Locale Store:** `stores/locale.ts` (Pinia)
- **Locale Files:** `utils/locales/{lang}.json`
- **Current Locale:** `localeStore.currentLocale`

### Chatbot İçin Çoklu Dil Stratejisi

#### 1. UI Çevirileri (Frontend)

**Locale Dosyalarına Eklenecek:**

```json
// tr.json
{
  "chatbot": {
    "title": "Yardımcı",
    "placeholder": "Sorunuzu yazın...",
    "send": "Gönder",
    "clear": "Temizle",
    "thinking": "Düşünüyor...",
    "error": "Bir hata oluştu",
    "noResults": "Sonuç bulunamadı"
  }
}
```

**Kullanım:**
```vue
<!-- ChatbotWidget.vue -->
<template>
  <v-card>
    <v-card-title>{{ $t('chatbot.title') }}</v-card-title>
    <v-text-field :placeholder="$t('chatbot.placeholder')" />
    <v-btn>{{ $t('chatbot.send') }}</v-btn>
  </v-card>
</template>
```

#### 2. LLM Dil Algılama ve Cevap

**Strateji:**
- Kullanıcının mevcut locale'ini al (`localeStore.currentLocale`)
- Chat request'e ekle
- LLM'e gönder
- LLM kullanıcının dilinde cevap versin

**Backend Implementation:**

```csharp
public class ChatCommand
{
    public string Message { get; set; }
    public string SessionId { get; set; }
    public string DomainName { get; set; }
    public string UserId { get; set; }
    public string Language { get; set; } = "tr"; // Kullanıcının dil tercihi
    public ConversationContext? Context { get; set; }
}

// ChatCommandHandler'da
var prompt = BuildPrompt(request.Message, request.Language, context);
var response = await _llmService.GenerateAsync(prompt, cancellationToken);

private string BuildPrompt(string message, string language, ConversationContext? context)
{
    var languageName = language switch
    {
        "tr" => "Türkçe",
        "en" => "English",
        "fr" => "Français",
        "ar" => "العربية",
        "zh" => "中文",
        _ => "Türkçe"
    };
    
    return $"You are a helpful assistant for MonitraNG platform. " +
           $"User's language preference: {languageName}. " +
           $"Always respond in {languageName}. " +
           $"User question: {message}";
}
```

#### 3. Dokümantasyon Arama Dil Desteği

**Strateji:**
- Dokümantasyonlar genellikle İngilizce veya Türkçe
- Kullanıcının diline göre dokümantasyonları filtrele veya önceliklendir
- LLM response'u kullanıcının diline çevir

**Implementation:**

```csharp
public class DocumentationProvider
{
    public async Task<List<DocumentationResult>> SearchAsync(
        string query, 
        string language = "tr", // Kullanıcının dil tercihi
        int limit = 5, 
        CancellationToken cancellationToken = default)
    {
        // Search documentation
        var results = await SearchInternalAsync(query, limit, cancellationToken);
        
        // Language-based prioritization
        // Turkish docs for Turkish users, English docs for English users, etc.
        var prioritizedResults = PrioritizeByLanguage(results, language);
        
        return prioritizedResults;
    }
    
    private List<DocumentationResult> PrioritizeByLanguage(
        List<DocumentationResult> results, 
        string language)
    {
        // Priority: Same language > English (fallback) > Others
        return results.OrderByDescending(r => 
        {
            if (r.Metadata.ContainsKey("language") && 
                r.Metadata["language"]?.ToString() == language)
                return 3;
            if (r.Metadata.ContainsKey("language") && 
                r.Metadata["language"]?.ToString() == "en")
                return 2;
            return 1;
        }).ToList();
    }
}
```

#### 4. Frontend Implementation

**useChatbot Composable:**

```typescript
export const useChatbot = () => {
  const localeStore = useLocaleStore();
  const currentLocale = computed(() => localeStore.currentLocale);
  
  const sendMessage = async (message: string, domainName: string) => {
    // Kullanıcının dil tercihini ekle
    const response = await fetchFromMngLLM('/api/v1/chatbot/chat', 'POST', {
      message,
      sessionId: sessionId.value,
      domainName,
      language: currentLocale.value // tr, en, fr, ar, zh
    });
    // ...
  };
  
  return {
    sendMessage,
    // ...
  };
};
```

#### 5. Dokümantasyon Metadata (Gelecekte)

**Markdown Front Matter:**

```markdown
---
title: "Dataset Oluşturma"
language: "tr"  # veya "en"
category: "guide"
service: "MngDataGateway"
---
```

**OpenAPI JSON Metadata:**

- Dokümantasyon metadata'sında `language` field'ı eklenebilir
- Veya ayrı bir metadata dosyası (örn: `docs-metadata.json`)

### Öncelik Sırası

1. **Faz 1:** UI çevirileri (chatbot widget için)
2. **Faz 2:** LLM dil algılama (kullanıcı diline göre cevap)
3. **Faz 3:** Dokümantasyon dil filtreleme (gelecekte)

---

## 🔐 Token Yönetimi

### Mevcut Yapı

- **Access Token:** Cookie'de (`access_token`)
- **Refresh Token:** Cookie'de (`refresh_token`)
- **Auth Store:** `useAuthStore()` - `ensureValidToken()`, `refreshAccessToken()`

### Chatbot İçin Kullanım

**Frontend:**
```typescript
// apiService.ts'deki fetchFromMngLLM zaten token yönetimini yapıyor
// Server route'da cookie'den token alınıyor
```

**Backend:**
```csharp
// MngLLM.Api'de JWT authentication zaten yapılandırılmış
// [Authorize] attribute ile korumalı endpoint'ler
// Token validation otomatik (MngKeeper'dan)
```

### Token Refresh Mekanizması

**Frontend:**
- `fetchFromMngLLM` çağrılmadan önce `authStore.ensureValidToken()` çağrılır
- Token expire olursa otomatik refresh
- Refresh başarısız olursa logout ve login sayfasına yönlendir

**Backend:**
- JWT validation otomatik (ASP.NET Core middleware)
- 401 dönerse frontend refresh mekanizması devreye girer

### Diğer Servislere Erişim (MngDataGateway)

**Backend'de (MngLLM Service):**
```csharp
// HttpClient ile MngDataGateway'e istek yaparken
// Kullanıcının token'ını forward et
public class DataGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
    {
        // Get token from current request context
        var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"]
            .ToString().Replace("Bearer ", "");
        
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        
        var response = await _httpClient.SendAsync(request, cancellationToken);
        // ...
    }
}
```

---

## 🧪 Test Stratejisi

### Unit Tests

1. **DocumentationProvider Tests:**
   - Markdown parsing
   - OpenAPI parsing
   - Search algoritması
   - Index oluşturma

2. **ChatCommandHandler Tests:**
   - Intent detection
   - Context hazırlama
   - Response formatting

3. **ContextManager Tests:**
   - Context save/load
   - Session management
   - TTL handling

### Integration Tests

1. **Documentation Integration:**
   - Markdown dosyalarını oku
   - OpenAPI endpoint'lerinden JSON al
   - İndeksle ve ara

2. **Chatbot Integration:**
   - End-to-end chat akışı
   - Context persistence
   - Token authentication

### E2E Tests

1. **Kullanıcı Senaryoları:**
   - Dokümantasyon sorusu → Cevap
   - NLQ sorusu → Query → Sonuçlar
   - Genel soru → Yardım

2. **Hata Senaryoları:**
   - Token expire
   - LLM servisi çalışmıyor
   - Dokümantasyon bulunamadı

---

## 📅 Zaman Çizelgesi

### Hafta 1-2: Dokümantasyon Provider
- ✅ Interface tasarımı
- ✅ Markdown parser
- ✅ OpenAPI parser
- ✅ Keyword index
- ✅ Search algoritması
- ✅ Unit tests

### Hafta 3-4: Chatbot Backend
- ✅ ChatCommand/Handler
- ✅ Intent detection
- ✅ Context management
- ✅ API endpoints
- ✅ Integration tests

### Hafta 5-6: Chatbot Frontend
- ✅ ChatbotWidget component
- ✅ ChatMessage component
- ✅ useChatbot composable
- ✅ API integration
- ✅ UI/UX polish
- ✅ E2E tests

---

## 📝 Sonraki Adımlar

1. ✅ Implementasyon planı hazırlandı
2. 📋 Faz 1: Dokümantasyon Provider implementasyonuna başla
3. 📋 Code review ve test
4. 📋 Faz 2: Chatbot Backend implementasyonu
5. 📋 Faz 3: Chatbot Frontend implementasyonu

---

**Son Güncelleme:** 15 Ocak 2026
