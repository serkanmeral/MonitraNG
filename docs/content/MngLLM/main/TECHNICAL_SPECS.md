# MngLLM Technical Specs (API Referansı)

Test ekibinin kullandığı birincil API referansı. Tüm endpoint'ler, request/response alanları ve parametre açıklamaları DOCUMENTATION_STANDARDS §3.6'ya uygun biçimde bu dokümanda tutulur.

**Temel bilgiler:**
- **Base path (Gateway üzerinden):** `/llm/api/v1/` (Health servis kökünde ` /health` veya gateway’de `/llm/health` olabilir; ör. `https://gateway.example.com/llm/api/v1/llm/translate`)
- **Kimlik doğrulama:** Çoğu endpoint JWT gerektirir; Translate Authorize. Chatbot ve Docs uygulama ayarıyla AllowAnonymousInDevelopment olabilir.
- **Content-Type:** `application/json`.

---

## 1. Health — `/health`

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/health` (servis kökü) |
| **Auth** | Yok |

#### Response (200 OK)

Servis canlılık bilgisi (status, timestamp vb.).

---

## 2. Version — `api/v1/version`

### 2.1 Detaylı / 2.2 Kısa sürüm

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/version`, `/api/v1/version/short` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

Product, Version, BuildDate, Runtime, Dependencies / kısa sürüm.

---

## 3. LLM — `api/v1/llm`

### 3.1 Metin çevir

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/llm/translate` |
| **Auth** | Evet (JWT) |
| **Amaç** | Kaynak metni hedef dillere çevirir (Ollama). |

#### Request body (TranslationRequestDto)

| Alan adı | Tip | Zorunlu | Açıklama | Örnek |
|----------|-----|---------|----------|--------|
| `text` | string | Evet | Çevrilecek metin. | `"Merhaba dünya"` |
| `sourceLanguage` | string | Hayır | Kaynak dil kodu. | `"tr"` |
| `targetLanguages` | string[] | Evet | Hedef dil kodları (en az bir). | `["en","fr","ar","zh"]` |

#### Response (200 OK)

TranslationResponseDto: `translations` (dil koduna göre çeviri metinleri), `model`, `inferenceTime` vb. 400: text veya targetLanguages eksik. 401: Unauthorized. 500: Translation failed.

---

## 4. Chatbot — `api/v1/chatbot`

### 4.1 Sohbet mesajı gönder

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/chatbot/chat` |
| **Auth** | Uygulama ayarına bağlı (AllowAnonymousInDevelopment) |

#### Request body (ChatRequestDto)

| Alan adı | Tip | Zorunlu | Açıklama |
|----------|-----|---------|----------|
| `message` | string | Evet | Kullanıcı mesajı. |
| `sessionId` | string | Hayır | Oturum ID; yoksa otomatik üretilir. |
| `language` | string | Hayır | Tercih edilen dil kodu. |

#### Response (200 OK)

ChatResponseDto: `sessionId`, `message`, `intent`, `language`, konuşma cevabı vb. 400: ModelState invalid. 401: Unauthorized (policy’e göre). 500: İşlem hatası.

### 4.2 Oturumu temizle

| Özellik | Değer |
|--------|--------|
| **Method** | `DELETE` |
| **Path** | `/api/v1/chatbot/session/{sessionId}` |
| **Auth** | Uygulama ayarına bağlı |

#### Path parametreleri

| Parametre | Tip | Zorunlu | Açıklama |
|-----------|-----|---------|----------|
| `sessionId` | string | Evet | Temizlenecek oturum ID. |

#### Response (200 OK)

Başarı mesajı. 400/404: Geçersiz sessionId veya bulunamadı.

---

## 5. Docs — `api/v1/docs`

Dokümantasyon arama ve içerik.

### 5.1 Dokümantasyonda ara

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/docs/search` |
| **Auth** | Uygulama ayarına bağlı |

#### Query parametreleri

| Parametre | Tip | Zorunlu | Açıklama | Varsayılan |
|-----------|-----|---------|----------|------------|
| `query` | string | Evet | Arama metni. | — |
| `limit` | number | Hayır | Maksimum sonuç sayısı. | `5` |

#### Response (200 OK)

DocumentationResult dizisi. 400: query eksik. 500: Arama hatası.

### 5.2 Doküman içeriği getir

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/docs/{documentId}` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

`{ "content": "..." }`. 404: Document not found. 500: İçerik okuma hatası.

### 5.3 Tüm dokümanları listele

| Özellik | Değer |
|--------|--------|
| **Method** | `GET` |
| **Path** | `/api/v1/docs` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

DocumentationIndex dizisi (indekslenmiş doküman listesi). 500: Listeleme hatası.

### 5.4 Yeniden indeksle

| Özellik | Değer |
|--------|--------|
| **Method** | `POST` |
| **Path** | `/api/v1/docs/reindex` |
| **Auth** | Uygulama ayarına bağlı |

#### Response (200 OK)

`{ "message": "Re-indexing completed successfully" }`. 500: Reindex hatası.

---

## Hata yanıtları

- 400: Bad Request (eksik/geçersiz alan, ModelState).
- 401: Unauthorized.
- 404: Document/Session not found.
- 500: Internal server error (`error`, `message`).

Ortak hata gövdesi: `{ "error": "message" }` veya `{ "error": "...", "message": "..." }`.
