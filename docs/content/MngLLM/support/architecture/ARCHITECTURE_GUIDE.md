---
title: "MngLLM Architecture Guide"
category: "architecture"
tags: ["llm", "ollama", "translation", "nlq", "chatbot", "architecture"]
service: "MngLLM"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
---

# MngLLM Architecture Guide

## Özet
MngLLM, MonitraNG platformunun LLM (Large Language Model) entegrasyon servisidir. Ollama kullanarak çoklu dil çevirisi, doğal dil sorgulama (NLQ), dokümantasyon yardımı ve chatbot özellikleri sağlar.

## Genel Bakış

### Amaç
MngLLM, MonitraNG platformuna dahili LLM (Ollama) entegrasyonu sağlayan ayrı bir mikroservistir. Test ortamında hafif modeller (Qwen2.5 3B) kullanılacak, production için ayrı sunucu planlanmaktadır.

### Temel Özellikler
- ✅ Clean Architecture yapısı
- ✅ Çoklu dil çevirisi (Aktif)
- 📋 Natural Language Query (NLQ) (Planlanan)
- 📋 Dokümantasyon yardımı (Planlanan)
- 📋 Kullanıcı rehberi (Planlanan)
- 📋 Chatbot uygulamaları (Planlanan)

## Mimari Yapı

### Clean Architecture Katmanları

```
MngLLM/
├── Core/
│   ├── MngLLM.Domain/          # Domain entities, exceptions
│   └── MngLLM.Application/    # Interfaces, configurations, DTOs
├── Infrastructure/
│   └── MngLLM.Infrastructure/ # Ollama adapter, HTTP clients
└── Presentation/
    └── MngLLM.Api/            # API controllers, middleware
```

### Katman Sorumlulukları

**Domain Layer:**
- Translation entities
- NLQ entities (planlanan)
- Chatbot entities (planlanan)
- Domain exceptions

**Application Layer:**
- Service interfaces
- DTOs (Request/Response)
- Application settings
- Configuration

**Infrastructure Layer:**
- Ollama adapter
- HTTP client services
- Documentation provider (planlanan)
- Dataset query service (planlanan)

**Presentation Layer:**
- REST API controllers
- TranslationController
- NLQController (planlanan)
- ChatbotController (planlanan)
- VersionController

## Ana Bileşenler

### 1. Çoklu Dil Çevirisi (Aktif)
- Türkçe → İngilizce, Fransızca, Arapça, Çince
- Ollama model integration
- Batch translation support

### 2. Natural Language Query (Planlanan)
- Doğal dil ile dataset sorgulama
- MongoDB query generation
- Result interpretation

### 3. Dokümantasyon Yardımı (Planlanan)
- MkDocs dokümantasyon parsing
- OpenAPI/Swagger parsing
- Context-based search
- LLM context preparation

### 4. Chatbot (Planlanan)
- Multi-language support
- Context management
- Session handling
- User guidance

## API Endpoints

### Translation Endpoints
- `POST /api/v1/translation/translate` - Metin çevirisi
- `POST /api/v1/translation/batch` - Toplu çeviri

### NLQ Endpoints (Planlanan)
- `POST /api/v1/nlq/query` - Doğal dil sorgulama

### Chatbot Endpoints (Planlanan)
- `POST /api/v1/chatbot/message` - Chatbot mesajı
- `GET /api/v1/chatbot/session/{id}` - Session bilgisi

### Version Endpoints
- `GET /api/v1/version` - Versiyon bilgisi

## Teknoloji Stack

- **.NET 9.0** - Framework
- **Ollama** - LLM service
- **MongoDB** - Veritabanı (planlanan)
- **Serilog** - Logging
- **Swagger/Scalar** - API dokümantasyonu

## Bağımlılıklar

### Internal Services
- MngKeeper (Authentication)
- MngDataGateway (Dataset queries - planlanan)
- MngGateway (API Gateway)

### External Services
- Ollama Service
- MongoDB (planlanan)

## Güvenlik

- JWT token authentication
- Rate limiting
- Context size limits
- Secure model access

## Deployment

### Port
- **Default:** 5050

### Docker
```bash
docker build -t mngllm -f Dockerfile .
docker run -p 5050:5050 mngllm
```

### Ollama Service
Ollama servisi ayrı bir container'da çalışmalıdır:
```bash
docker run -d -p 11434:11434 ollama/ollama
```

## İlgili Dokümantasyon

- [Technical Specs](../../main/TECHNICAL_SPECS.md)
- [Gateway Integration](../guides/GATEWAY_INTEGRATION.md)
- [ROADMAP](../../../MngLLM/ROADMAP.md)
- [Chatbot Planning](../../../MngLLM/CHATBOT_PLANNING.md)

---

**Son Güncelleme:** 16 Ocak 2026
