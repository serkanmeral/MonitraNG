---
title: "MngLLM API Documentation"
category: "api"
tags: ["llm", "api", "endpoints", "rest", "ollama"]
service: "MngLLM"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
---

# MngLLM API Documentation

## Base URL

```
http://localhost:5050/api/v1
```

## Endpoints

### Translation Endpoints

#### Translate Text
```http
POST /translation/translate
Content-Type: application/json

{
  "text": "Merhaba dünya",
  "targetLanguage": "en"
}
```

**Response:**
```json
{
  "translatedText": "Hello world",
  "sourceLanguage": "tr",
  "targetLanguage": "en"
}
```

#### Batch Translation
```http
POST /translation/batch
Content-Type: application/json

{
  "texts": ["text1", "text2"],
  "targetLanguage": "en"
}
```

### NLQ Endpoints (Planlanan)

#### Natural Language Query
```http
POST /nlq/query
Content-Type: application/json

{
  "query": "Show me all books published in 2025",
  "dataset": "@books"
}
```

### Chatbot Endpoints (Planlanan)

#### Send Message
```http
POST /chatbot/message
Content-Type: application/json

{
  "message": "How do I create a dataset?",
  "sessionId": "session-id"
}
```

## İlgili Linkler

- [Architecture Guide](../architecture/ARCHITECTURE_GUIDE.md)
- [ROADMAP](../../../../MngLLM/ROADMAP.md)

---

**Son Güncelleme:** 16 Ocak 2026
