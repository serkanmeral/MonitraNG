---
title: "ChatBot (Moni) Implementasyon Adımları"
category: "implementation"
tags: ["chatbot", "moni", "implementation", "roadmap"]
service: "MngLLM"
difficulty: "advanced"
estimated_time: "8-10 hafta"
language: "tr"
priority: 1
---

# ChatBot (Moni) Implementasyon Adımları

**Tarih:** 16 Ocak 2026  
**Chatbot İsmi:** **Moni**  
**Durum:** 📋 Planlama Tamamlandı - Implementasyona Hazır

---

## 📋 Genel Bakış

ChatBot implementasyonu **3 ana faz** halinde gerçekleştirilecek:

1. **Faz 1: Dokümantasyon Provider** (1-2 hafta) - Backend
2. **Faz 2: Chatbot Backend** (2-3 hafta) - Backend
3. **Faz 3: Chatbot Frontend** (2-3 hafta) - Frontend

**Toplam Süre:** 8-10 hafta

---

## ✅ Mevcut Durum

### Tamamlanan İşler

- ✅ **Faz 0: Temel Altyapı** - Çoklu dil çevirisi (Translation API)
- ✅ **Planlama:** Kapsamlı chatbot planlama dokümantasyonu
- ✅ **Dokümantasyon Hazırlığı:** MkDocs dokümantasyonları chatbot formatında
- ✅ **UI Rehberleri:** Tüm UI fonksiyonları için rehberler oluşturuldu

### Hazır Olan Altyapı

- ✅ MngLLM Service (Clean Architecture)
- ✅ Ollama Integration
- ✅ API Gateway Entegrasyonu
- ✅ JWT Authentication
- ✅ Docker Containerization
- ✅ MkDocs Dokümantasyon Sistemi

---

## 🚀 Faz 1: Dokümantasyon Provider (Backend)

**Süre:** 1-2 hafta  
**Öncelik:** ⭐⭐⭐ Yüksek  
**Bağımlılıklar:** Yok (bağımsız başlanabilir)

### Adım 1.1: Domain Layer - Interface Tasarımı

**Dosya:** `MngLLM/Core/MngLLM.Domain/Interfaces/IDocumentationProvider.cs`

**Yapılacaklar:**
- [ ] `IDocumentationProvider` interface oluştur
- [ ] `DocumentationResult` DTO tanımla
- [ ] `DocumentationIndex` DTO tanımla
- [ ] Method signature'ları belirle

**Tahmini Süre:** 2-3 saat

### Adım 1.2: Infrastructure Layer - Markdown Parser

**Dosya:** `MngLLM/Infrastructure/MngLLM.Infrastructure/Services/DocumentationProvider.cs`

**Yapılacaklar:**
- [ ] Markdig NuGet package ekle
- [ ] Markdown dosyalarını okuyup parse et
- [ ] Front matter (YAML) parse et
- [ ] Başlıkları, paragrafları, kod bloklarını extract et
- [ ] Metadata'yı index'e ekle

**Tahmini Süre:** 1 gün

### Adım 1.3: Infrastructure Layer - OpenAPI Parser

**Yapılacaklar:**
- [ ] OpenAPI JSON dosyalarını oku
- [ ] System.Text.Json ile parse et
- [ ] Endpoint'leri, schema'ları, örnekleri extract et
- [ ] Her endpoint için ayrı dokümantasyon index oluştur

**Tahmini Süre:** 1 gün

### Adım 1.4: Infrastructure Layer - Keyword Index

**Yapılacaklar:**
- [ ] Inverted index oluştur (Dictionary<string, List<string>>)
- [ ] Keyword → Document ID mapping
- [ ] Case-insensitive search
- [ ] Index oluşturma algoritması

**Tahmini Süre:** 1 gün

### Adım 1.5: Infrastructure Layer - Search Algoritması

**Yapılacaklar:**
- [ ] Keyword matching (exact match)
- [ ] Title matching (yüksek öncelik)
- [ ] Content matching (düşük öncelik)
- [ ] Relevance score hesaplama
- [ ] Sonuçları sıralama

**Tahmini Süre:** 1 gün

### Adım 1.6: Configuration

**Dosya:** `MngLLM/Core/MngLLM.Application/Configuration/MngLLMSettings.cs`

**Yapılacaklar:**
- [ ] `DocumentationSettings` class ekle
- [ ] `appsettings.json` yapılandırması
- [ ] Service endpoint'leri yapılandır
- [ ] Path ayarları

**Tahmini Süre:** 2-3 saat

### Adım 1.7: Service Registration

**Dosya:** `MngLLM/Infrastructure/MngLLM.Infrastructure/ServiceRegistration.cs`

**Yapılacaklar:**
- [ ] `IDocumentationProvider` → `DocumentationProvider` registration
- [ ] Hosted service ekle (periodic re-indexing)
- [ ] Startup'ta initial indexing

**Tahmini Süre:** 2-3 saat

### Adım 1.8: API Endpoint (Opsiyonel)

**Dosya:** `MngLLM/Presentation/MngLLM.Api/Controllers/DocumentationController.cs`

**Yapılacaklar:**
- [ ] `POST /api/v1/docs/reindex` - Re-index endpoint
- [ ] `GET /api/v1/docs/search` - Search endpoint (test için)
- [ ] Authorization ekle

**Tahmini Süre:** 2-3 saat

### Adım 1.9: Unit Tests

**Yapılacaklar:**
- [ ] Markdown parsing tests
- [ ] OpenAPI parsing tests
- [ ] Search algoritması tests
- [ ] Index oluşturma tests

**Tahmini Süre:** 1 gün

**Faz 1 Toplam Süre:** 1-2 hafta

---

## 💬 Faz 2: Chatbot Backend

**Süre:** 2-3 hafta  
**Öncelik:** ⭐⭐⭐ Yüksek  
**Bağımlılıklar:** Faz 1 (Dokümantasyon Provider)

### Adım 2.1: Domain Layer - Interface Tasarımı

**Dosya:** `MngLLM/Core/MngLLM.Domain/Interfaces/IChatbotService.cs`

**Yapılacaklar:**
- [ ] `IChatbotService` interface oluştur
- [ ] `ProcessMessageAsync` method signature
- [ ] DTO'ları tanımla

**Tahmini Süre:** 2-3 saat

### Adım 2.2: Application Layer - Chat Command

**Dosya:** `MngLLM/Core/MngLLM.Application/Commands/Chat/ChatCommand.cs`

**Yapılacaklar:**
- [ ] `ChatCommand` class oluştur
- [ ] `ConversationContext` class oluştur
- [ ] `ChatMessage` class oluştur
- [ ] DTO'ları tanımla

**Tahmini Süre:** 2-3 saat

### Adım 2.3: Application Layer - Chat Command Handler

**Dosya:** `MngLLM/Core/MngLLM.Application/Commands/Chat/ChatCommandHandler.cs`

**Yapılacaklar:**
- [ ] Intent detection (LLM ile)
- [ ] Context hazırlama
- [ ] Dokümantasyon arama (DocumentationProvider)
- [ ] LLM prompt oluşturma
- [ ] Response formatting
- [ ] Intent'ler: `nlq`, `docs`, `guide`, `general`

**Tahmini Süre:** 3-4 gün

### Adım 2.4: Infrastructure Layer - Context Manager

**Dosya:** `MngLLM/Infrastructure/MngLLM.Infrastructure/Services/ContextManager.cs`

**Yapılacaklar:**
- [ ] `IContextManager` interface
- [ ] In-memory implementation (Dictionary)
- [ ] Session-based context
- [ ] Son 10 mesaj saklama
- [ ] TTL: 30 dakika (inactive session cleanup)

**Tahmini Süre:** 1 gün

### Adım 2.5: Application Layer - DTOs

**Dosya:** `MngLLM/Core/MngLLM.Application/DTOs/`

**Yapılacaklar:**
- [ ] `ChatRequestDto` class
- [ ] `ChatResponseDto` class
- [ ] `DocumentationSource` class
- [ ] Validation attributes

**Tahmini Süre:** 2-3 saat

### Adım 2.6: API Endpoint - Chatbot Controller

**Dosya:** `MngLLM/Presentation/MngLLM.Api/Controllers/ChatbotController.cs`

**Yapılacaklar:**
- [ ] `POST /api/v1/chatbot/chat` - Chat endpoint
- [ ] `DELETE /api/v1/chatbot/session/{sessionId}` - Clear session
- [ ] Authorization ekle
- [ ] Error handling
- [ ] Swagger documentation

**Tahmini Süre:** 1 gün

### Adım 2.7: Intent Detection Logic

**Yapılacaklar:**
- [ ] LLM prompt template oluştur
- [ ] Intent classification logic
- [ ] Intent confidence scoring
- [ ] Fallback mekanizması

**Tahmini Süre:** 1 gün

### Adım 2.8: Context Window Yönetimi

**Yapılacaklar:**
- [ ] Token sayma (approximate)
- [ ] Context window limit (4096 token)
- [ ] Mesaj önceliklendirme
- [ ] Context truncation

**Tahmini Süre:** 1 gün

### Adım 2.9: Integration Tests

**Yapılacaklar:**
- [ ] Chat endpoint → LLM → Response test
- [ ] Context persistence test
- [ ] Intent detection test
- [ ] Dokümantasyon arama test

**Tahmini Süre:** 1 gün

**Faz 2 Toplam Süre:** 2-3 hafta

---

## 🎨 Faz 3: Chatbot Frontend

**Süre:** 2-3 hafta  
**Öncelik:** ⭐⭐⭐ Yüksek  
**Bağımlılıklar:** Faz 2 (Chatbot Backend)

### Adım 3.1: Chatbot Widget Component

**Dosya:** `Mng.Ui/components/apps/chatbot/ChatbotWidget.vue`

**Yapılacaklar:**
- [ ] Floating button (sağ alt köşe)
- [ ] Chat interface (mesaj geçmişi)
- [ ] Input field
- [ ] Loading states
- [ ] Error handling
- [ ] Auto-scroll
- [ ] Dark/Light mode desteği
- [ ] Responsive design

**Tahmini Süre:** 2-3 gün

### Adım 3.2: Chat Message Component

**Dosya:** `Mng.Ui/components/apps/chatbot/ChatMessage.vue`

**Yapılacaklar:**
- [ ] User/Assistant mesajları
- [ ] Timestamp gösterimi
- [ ] Markdown formatting
- [ ] Copy message button
- [ ] Documentation links
- [ ] Code snippet highlighting

**Tahmini Süre:** 1-2 gün

### Adım 3.3: Chat Input Component

**Dosya:** `Mng.Ui/components/apps/chatbot/ChatInput.vue`

**Yapılacaklar:**
- [ ] Text input field
- [ ] Send button
- [ ] Enter key support
- [ ] Character limit
- [ ] Disabled state (loading)

**Tahmini Süre:** 1 gün

### Adım 3.4: useChatbot Composable

**Dosya:** `Mng.Ui/composables/useChatbot.ts`

**Yapılacaklar:**
- [ ] Session management
- [ ] Message state management
- [ ] `sendMessage` function
- [ ] `clearSession` function
- [ ] Error handling
- [ ] Loading states
- [ ] i18n locale integration

**Tahmini Süre:** 2-3 gün

### Adım 3.5: API Service Integration

**Dosya:** `Mng.Ui/services/apiService.ts`

**Yapılacaklar:**
- [ ] `sendChatMessage` function (zaten `fetchFromMngLLM` var)
- [ ] Token yönetimi (otomatik - cookie-based)
- [ ] Error handling

**Tahmini Süre:** 2-3 saat

### Adım 3.6: Chatbot Store (Opsiyonel)

**Dosya:** `Mng.Ui/stores/chatbot.ts`

**Yapılacaklar:**
- [ ] Pinia store oluştur
- [ ] `isOpen` state
- [ ] `sessionId` state
- [ ] `messages` state
- [ ] `toggle` action
- [ ] `clearSession` action

**Tahmini Süre:** 1 gün

### Adım 3.7: i18n Entegrasyonu

**Dosya:** `Mng.Ui/utils/locales/{lang}.json`

**Yapılacaklar:**
- [ ] Chatbot UI çevirileri ekle (5 dil)
- [ ] Placeholder text'ler
- [ ] Error mesajları
- [ ] Button text'leri

**Tahmini Süre:** 2-3 saat

### Adım 3.8: UI/UX Polish

**Yapılacaklar:**
- [ ] Animasyonlar (mesaj gönderme/alma)
- [ ] Typing indicator
- [ ] Smooth scrolling
- [ ] Mobile responsive
- [ ] Accessibility improvements

**Tahmini Süre:** 1-2 gün

### Adım 3.9: E2E Tests

**Yapılacaklar:**
- [ ] Kullanıcı chat akışı test
- [ ] Session management test
- [ ] Error handling test
- [ ] Multi-language test

**Tahmini Süre:** 1 gün

**Faz 3 Toplam Süre:** 2-3 hafta

---

## 📊 Implementasyon Sırası ve Bağımlılıklar

```
Faz 1: Dokümantasyon Provider
  └─> Bağımsız (başlanabilir)
      └─> Faz 2'ye input sağlar

Faz 2: Chatbot Backend
  └─> Faz 1'e bağımlı (DocumentationProvider)
      └─> Faz 3'e input sağlar

Faz 3: Chatbot Frontend
  └─> Faz 2'ye bağımlı (Chatbot API)
```

---

## 🎯 Öncelik Sırası

1. **Faz 1: Dokümantasyon Provider** ⭐⭐⭐
   - Chatbot'un temel altyapısı
   - Bağımsız başlanabilir
   - En hızlı tamamlanabilir

2. **Faz 2: Chatbot Backend** ⭐⭐⭐
   - Core chatbot functionality
   - Faz 1'e bağımlı
   - En kritik faz

3. **Faz 3: Chatbot Frontend** ⭐⭐⭐
   - Kullanıcı arayüzü
   - Faz 2'ye bağımlı
   - Kullanıcı deneyimi için kritik

---

## 📝 Her Faz İçin Checklist

### Faz 1 Checklist

- [ ] Domain Layer interface tasarımı
- [ ] Markdown parser implementasyonu
- [ ] OpenAPI parser implementasyonu
- [ ] Keyword index oluşturma
- [ ] Search algoritması
- [ ] Configuration
- [ ] Service registration
- [ ] API endpoint (opsiyonel)
- [ ] Unit tests
- [ ] Integration test

### Faz 2 Checklist

- [ ] Domain Layer interface tasarımı
- [ ] Chat Command/Handler
- [ ] Intent detection
- [ ] Context Manager
- [ ] DTOs
- [ ] API Controller
- [ ] Context window yönetimi
- [ ] Integration tests
- [ ] Error handling
- [ ] Logging

### Faz 3 Checklist

- [ ] ChatbotWidget component
- [ ] ChatMessage component
- [ ] ChatInput component
- [ ] useChatbot composable
- [ ] API integration
- [ ] Store (opsiyonel)
- [ ] i18n entegrasyonu
- [ ] UI/UX polish
- [ ] E2E tests
- [ ] Responsive design

---

## 🔄 Sonraki Adımlar (Faz 4+)

### Faz 4: Dataset Sorgulama (NLQ) - Gelecek

**Süre:** 3-4 hafta  
**Öncelik:** ⭐⭐⭐ Yüksek (Faz 2-3'ten sonra)

**Yapılacaklar:**
- Dataset schema provider
- Natural Language → MongoDB filter
- Query execution
- Results formatting

### Faz 5: Gelişmiş Özellikler - Gelecek

**Süre:** 2-3 hafta  
**Öncelik:** ⭐ Düşük

**Yapılacaklar:**
- Multi-turn conversation
- Conversation history
- Export conversation
- Suggested queries
- Analytics

---

## 📚 İlgili Dokümanlar

- [Chatbot Planning](./CHATBOT_PLANNING.md) - Genel chatbot planlaması
- [Implementation Plan](./IMPLEMENTATION_PLAN.md) - Detaylı implementasyon planı
- [Current Status](./current_status.md) - Mevcut durum raporu
- [Roadmap](./ROADMAP.md) - Genel roadmap

---

**Son Güncelleme:** 16 Ocak 2026  
**Sonraki Adım:** Faz 1: Dokümantasyon Provider implementasyonuna başla
