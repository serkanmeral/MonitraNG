# Chatbot Çoklu Dil Desteği (i18n) Planı

**Tarih:** 15 Ocak 2026  
**Servis:** MngLLM  
**Desteklenen Diller:** tr, en, fr, ar, zh (5 dil)

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Desteklenen Diller](#desteklenen-diller)
3. [UI Çevirileri](#ui-çevirileri)
4. [LLM Dil Algılama](#llm-dil-algılama)
5. [Dokümantasyon Dil Desteği](#dokümantasyon-dil-desteği)
6. [Implementasyon](#implementasyon)

---

## 🎯 Genel Bakış

Chatbot, uygulamanın desteklediği **5 dilde** çalışacak:
- 🇹🇷 Türkçe (tr) - Varsayılan
- 🇬🇧 İngilizce (en) - Fallback
- 🇫🇷 Fransızca (fr)
- 🇸🇦 Arapça (ar) - RTL desteği
- 🇨🇳 Çince (zh)

### Strateji

1. **UI Çevirileri:** Chatbot widget'ının tüm metinleri i18n ile çevrilecek
2. **LLM Cevap:** LLM kullanıcının dil tercihine göre cevap verecek
3. **Dokümantasyon:** Dokümantasyon araması kullanıcının diline göre önceliklendirilecek

---

## 🌍 Desteklenen Diller

### Dil Kodları

| Dil | Kod | İsim | RTL |
|-----|-----|------|-----|
| 🇹🇷 Türkçe | `tr` | Türkçe | ❌ |
| 🇬🇧 İngilizce | `en` | English | ❌ |
| 🇫🇷 Fransızca | `fr` | Français | ❌ |
| 🇸🇦 Arapça | `ar` | العربية | ✅ |
| 🇨🇳 Çince | `zh` | 中文 | ❌ |

**Not:** Arapça için i18n'de `ro` kodu kullanılıyor (mevcut yapı ile uyumlu).

### Mevcut i18n Yapısı

**Locale Store:**
```typescript
// stores/locale.ts
const localeStore = useLocaleStore();
const currentLocale = localeStore.currentLocale; // "tr", "en", "fr", "ar", "zh"
```

**Locale Files:**
```
Mng.Ui/utils/locales/
├── tr.json  # Türkçe
├── en.json  # İngilizce
├── fr.json  # Fransızca
├── ar.json  # Arapça
└── zh.json  # Çince
```

---

## 🎨 UI Çevirileri

### Locale Dosyalarına Eklenecek Key'ler

**tr.json:**
```json
{
  "chatbot": {
    "title": "Yardımcı",
    "subtitle": "Size nasıl yardımcı olabilirim?",
    "placeholder": "Sorunuzu yazın...",
    "send": "Gönder",
    "clear": "Temizle",
    "newChat": "Yeni Sohbet",
    "thinking": "Düşünüyor...",
    "error": "Bir hata oluştu",
    "errorMessage": "Mesaj gönderilirken bir hata oluştu. Lütfen tekrar deneyin.",
    "noResults": "Sonuç bulunamadı",
    "emptyState": "Merhaba! Size nasıl yardımcı olabilirim?",
    "examples": {
      "title": "Örnek Sorular",
      "q1": "Dataset nasıl oluşturulur?",
      "q2": "Validasyon kuralları nelerdir?",
      "q3": "API authentication nasıl yapılır?"
    },
    "sources": {
      "title": "Kaynaklar",
      "viewDocs": "Dokümantasyonu Görüntüle"
    },
    "intents": {
      "nlq": "Veri Sorgulama",
      "docs": "Dokümantasyon",
      "guide": "Kullanım Rehberi",
      "general": "Genel Yardım"
    }
  }
}
```

**en.json:**
```json
{
  "chatbot": {
    "title": "Assistant",
    "subtitle": "How can I help you?",
    "placeholder": "Type your question...",
    "send": "Send",
    "clear": "Clear",
    "newChat": "New Chat",
    "thinking": "Thinking...",
    "error": "An error occurred",
    "errorMessage": "An error occurred while sending the message. Please try again.",
    "noResults": "No results found",
    "emptyState": "Hello! How can I help you?",
    "examples": {
      "title": "Example Questions",
      "q1": "How do I create a dataset?",
      "q2": "What are validation rules?",
      "q3": "How do I authenticate with the API?"
    },
    "sources": {
      "title": "Sources",
      "viewDocs": "View Documentation"
    },
    "intents": {
      "nlq": "Data Query",
      "docs": "Documentation",
      "guide": "User Guide",
      "general": "General Help"
    }
  }
}
```

**fr.json, ar.json, zh.json:** Benzer yapı (çeviriler)

### Component'te Kullanım

**ChatbotWidget.vue:**
```vue
<template>
  <v-card>
    <v-card-title>{{ $t('chatbot.title') }}</v-card-title>
    <v-card-subtitle>{{ $t('chatbot.subtitle') }}</v-card-subtitle>
    
    <!-- Messages -->
    <v-card-text>
      <div v-if="messages.length === 0" class="empty-state">
        {{ $t('chatbot.emptyState') }}
        
        <!-- Example questions -->
        <v-list>
          <v-list-item v-for="(example, key) in examples" :key="key">
            <v-btn text @click="sendMessage(example)">
              {{ example }}
            </v-btn>
          </v-list-item>
        </v-list>
      </div>
      
      <ChatMessage 
        v-for="msg in messages" 
        :key="msg.id"
        :message="msg"
      />
      
      <div v-if="isLoading" class="thinking">
        {{ $t('chatbot.thinking') }}
      </div>
    </v-card-text>
    
    <!-- Input -->
    <v-card-actions>
      <v-text-field
        :placeholder="$t('chatbot.placeholder')"
        v-model="inputMessage"
        @keyup.enter="sendMessage(inputMessage)"
      />
      <v-btn @click="sendMessage(inputMessage)">
        {{ $t('chatbot.send') }}
      </v-btn>
      <v-btn @click="clearSession()">
        {{ $t('chatbot.clear') }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>

<script setup>
import { useI18n } from 'vue-i18n';
import { useLocaleStore } from '@/stores/locale';

const { t } = useI18n();
const localeStore = useLocaleStore();

const examples = computed(() => [
  t('chatbot.examples.q1'),
  t('chatbot.examples.q2'),
  t('chatbot.examples.q3')
]);
</script>
```

---

## 🤖 LLM Dil Algılama

### Strateji

1. **Kullanıcı Dil Tercihi:** Frontend'den `currentLocale` al
2. **Chat Request:** Language field'ını ekle
3. **LLM Prompt:** Kullanıcının diline göre prompt oluştur
4. **Response:** LLM kullanıcının dilinde cevap versin

### Backend Implementation

**ChatRequestDto:**
```csharp
public class ChatRequestDto
{
    public string Message { get; set; }
    public string? SessionId { get; set; }
    public string DomainName { get; set; }
    public string? DatasetId { get; set; }
    public string Language { get; set; } = "tr"; // Kullanıcının dil tercihi (tr, en, fr, ar, zh)
}
```

**ChatCommandHandler:**
```csharp
public class ChatCommandHandler : IRequestHandler<ChatCommand, ChatResponseDto>
{
    private readonly ILLMService _llmService;
    private readonly IDocumentationProvider _documentationProvider;
    
    public async Task<ChatResponseDto> Handle(ChatCommand request, CancellationToken cancellationToken)
    {
        // Detect intent
        var intent = await DetectIntentAsync(request.Message, request.Language, cancellationToken);
        
        // Prepare context based on intent
        string context = "";
        if (intent == "docs")
        {
            var docs = await _documentationProvider.SearchAsync(
                request.Message, 
                request.Language, 
                limit: 5, 
                cancellationToken);
            context = BuildDocumentationContext(docs, request.Language);
        }
        
        // Build prompt with language preference
        var prompt = BuildPrompt(request.Message, request.Language, context, request.Context);
        
        // Generate response
        var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);
        
        // Parse and format response
        var response = ParseResponse(llmResponse, intent, request.Language);
        
        return response;
    }
    
    private string BuildPrompt(
        string message, 
        string language, 
        string context, 
        ConversationContext? conversationContext)
    {
        var languageName = GetLanguageName(language);
        
        var systemPrompt = $"You are a helpful assistant for MonitraNG platform. " +
                          $"User's language preference: {languageName}. " +
                          $"IMPORTANT: Always respond in {languageName}. " +
                          $"Be friendly, concise, and helpful. ";
        
        var conversationHistory = "";
        if (conversationContext?.Messages != null && conversationContext.Messages.Count > 0)
        {
            conversationHistory = "\n\nConversation History:\n";
            foreach (var msg in conversationContext.Messages.TakeLast(5))
            {
                conversationHistory += $"{msg.Role}: {msg.Content}\n";
            }
        }
        
        return $"{systemPrompt}\n\n{context}\n{conversationHistory}\n\nUser: {message}\n\nAssistant:";
    }
    
    private string GetLanguageName(string languageCode)
    {
        return languageCode switch
        {
            "tr" => "Türkçe",
            "en" => "English",
            "fr" => "Français",
            "ar" => "العربية",
            "zh" => "中文",
            _ => "Türkçe"
        };
    }
}
```

### Frontend Implementation

**useChatbot.ts:**
```typescript
export const useChatbot = () => {
  const localeStore = useLocaleStore();
  const currentLocale = computed(() => localeStore.currentLocale); // "tr", "en", "fr", "ar", "zh"
  
  const sendMessage = async (message: string, domainName: string) => {
    try {
      const response = await fetchFromMngLLM('/api/v1/chatbot/chat', 'POST', {
        message,
        sessionId: sessionId.value,
        domainName,
        language: currentLocale.value // Kullanıcının dil tercihini gönder
      });
      
      // Response zaten kullanıcının dilinde gelecek
      messages.value.push({
        role: 'assistant',
        content: response.response,
        // ...
      });
    } catch (error) {
      // Error handling
    }
  };
  
  return {
    sendMessage,
    // ...
  };
};
```

---

## 📚 Dokümantasyon Dil Desteği

### Strateji

1. **Dokümantasyon Metadata:** Dil bilgisi ekle (gelecekte)
2. **Arama Önceliklendirme:** Kullanıcının diline göre önceliklendir
3. **Çeviri:** LLM response'u kullanıcının diline çevir (zaten yapılıyor)

### Implementation

**DocumentationProvider:**
```csharp
public class DocumentationProvider : IDocumentationProvider
{
    public async Task<List<DocumentationResult>> SearchAsync(
        string query, 
        string language = "tr", // Kullanıcının dil tercihi
        int limit = 5, 
        CancellationToken cancellationToken = default)
    {
        // Search documentation (language-agnostic)
        var results = await SearchInternalAsync(query, limit, cancellationToken);
        
        // Prioritize by language
        var prioritizedResults = PrioritizeByLanguage(results, language);
        
        return prioritizedResults;
    }
    
    private List<DocumentationResult> PrioritizeByLanguage(
        List<DocumentationResult> results, 
        string language)
    {
        // Priority scoring:
        // 1. Same language: +3
        // 2. English (fallback): +2
        // 3. Other languages: +1
        
        return results
            .Select(r => new
            {
                Result = r,
                Score = GetLanguageScore(r, language)
            })
            .OrderByDescending(x => x.Score)
            .Select(x => x.Result)
            .ToList();
    }
    
    private int GetLanguageScore(DocumentationResult result, string userLanguage)
    {
        // Check metadata for language
        if (result.Metadata?.ContainsKey("language") == true)
        {
            var docLanguage = result.Metadata["language"]?.ToString();
            
            if (docLanguage == userLanguage)
                return 3; // Same language - highest priority
            if (docLanguage == "en")
                return 2; // English - fallback
            return 1; // Other languages
        }
        
        // Default: English (most docs are in English)
        return userLanguage == "en" ? 3 : 2;
    }
}
```

### Gelecekte: Dokümantasyon Metadata

**Markdown Front Matter:**
```markdown
---
title: "Dataset Oluşturma"
language: "tr"  # veya "en", "fr", "ar", "zh"
category: "guide"
service: "MngDataGateway"
---
```

**OpenAPI JSON Metadata:**
```json
{
  "info": {
    "title": "MngDataGateway API",
    "version": "v1",
    "x-language": "en"  // Custom extension
  }
}
```

---

## 🛠️ Implementasyon

### Faz 1: UI Çevirileri (1 gün)

**Görevler:**
1. ✅ Locale dosyalarına chatbot key'lerini ekle (tr, en, fr, ar, zh)
2. ✅ ChatbotWidget component'inde `$t()` kullan
3. ✅ ChatMessage component'inde `$t()` kullan
4. ✅ Test: Dil değiştir, chatbot metinlerinin çevrildiğini kontrol et

### Faz 2: LLM Dil Algılama (2-3 gün)

**Görevler:**
1. ✅ `ChatRequestDto.Language` field ekle
2. ✅ Frontend'den `currentLocale` gönder
3. ✅ `ChatCommandHandler`'da prompt'a dil bilgisi ekle
4. ✅ LLM'in kullanıcının dilinde cevap vermesini test et

### Faz 3: Dokümantasyon Dil Desteği (1 gün)

**Görevler:**
1. ✅ `DocumentationProvider.SearchAsync()` metoduna `language` parametresi ekle
2. ✅ Language-based prioritization implement et
3. ✅ Test: Farklı dillerde arama, önceliklendirmeyi kontrol et

---

## 📝 Örnek Senaryo

### Senaryo: Türkçe Kullanıcı

1. **Kullanıcı:** `currentLocale = "tr"`
2. **Kullanıcı Sorar:** "Dataset nasıl oluşturulur?"
3. **Frontend:** `language: "tr"` gönder
4. **Backend:** 
   - Intent detection (docs)
   - Dokümantasyon ara (Türkçe dokümanlar öncelikli)
   - LLM'e Türkçe prompt gönder
5. **LLM:** Türkçe cevap verir
6. **Kullanıcı:** Türkçe cevap alır

### Senaryo: İngilizce Kullanıcı

1. **Kullanıcı:** `currentLocale = "en"`
2. **Kullanıcı Sorar:** "How do I create a dataset?"
3. **Frontend:** `language: "en"` gönder
4. **Backend:** 
   - Intent detection (docs)
   - Dokümantasyon ara (İngilizce dokümanlar öncelikli)
   - LLM'e İngilizce prompt gönder
5. **LLM:** English response
6. **Kullanıcı:** English answer

---

## ✅ Sonuç

Chatbot, uygulamanın desteklediği **5 dilde** tam çalışır:
- ✅ UI metinleri çevrilir (i18n)
- ✅ LLM kullanıcının dilinde cevap verir
- ✅ Dokümantasyon araması dil bazlı önceliklendirilir

---

**Son Güncelleme:** 15 Ocak 2026
