# Ollama Performans Analizi ve Timeout Sorunları

**Tarih:** 17 Ocak 2026  
**Durum:** 🔍 Analiz Tamamlandı

---

## 📊 Mevcut Durum

### Konfigürasyon
- **Model:** `qwen2.5:3b` (3 milyar parametre)
- **Timeout:** 120 saniye (2 dakika)
- **Base URL:** `http://ollama:11434`
- **Streaming:** Kapalı (non-streaming)

### Performans Metrikleri
- **Intent Detection:** ~5-10 saniye (LLM çağrısı)
- **Main Response:** ~20-60 saniye (LLM çağrısı)
- **Toplam Süre:** ~25-70 saniye per request
- **Timeout Hataları:** Bazı uzun prompt'larda görülüyor

---

## 🔍 Sorun Analizi

### 1. Neden 30 Saniye Yeterli Değildi?

#### A. İki Ayrı LLM Çağrısı
Her chat request'te **2 ayrı LLM çağrısı** yapılıyor:
1. **Intent Detection:** ~5-10 saniye
2. **Main Response Generation:** ~20-60 saniye

**Toplam:** ~25-70 saniye → 30 saniye yetersiz!

#### B. Uzun Prompt'lar
ChatCommandHandler'da oluşturulan prompt'lar oldukça uzun:
- System prompt (~200-300 token)
- Conversation history (son 5 mesaj, ~500-1000 token)
- Documentation sources (3 kaynak, ~300-600 token)
- User message (~50-200 token)

**Toplam:** ~1000-2000 token input → Model daha uzun süre çalışıyor

#### C. Model Performansı
- **qwen2.5:3b** küçük bir model ama:
  - CPU'da çalışıyor (GPU yoksa yavaş)
  - Cold start problemi (ilk çağrıda model yüklenmesi)
  - Memory'de tutulmuyorsa her seferinde yükleniyor

#### D. Network Latency
- Docker network üzerinden HTTP çağrısı
- Ollama servisi başka bir container'da
- Network gecikmesi ekleniyor

---

## 💡 Çözüm Önerileri

### 1. ✅ Keyword-Based Intent Detection (Yapıldı)
**Durum:** Implement edildi

**Fayda:**
- LLM çağrısı gerektirmiyor
- ~0.1 saniye (100ms)
- %80+ doğruluk oranı

**Sonuç:** Intent detection süresi **5-10 saniye → 0.1 saniye** (50-100x hızlanma)

### 2. ⚠️ Prompt Optimizasyonu (Önerilen)

**Sorun:** Prompt'lar çok uzun ve gereksiz bilgi içeriyor

**Çözüm:**
- System prompt'u kısalt
- Conversation history'yi sınırla (son 3 mesaj yeterli)
- Documentation sources'u özetle (sadece title + 1-2 cümle)
- Context window'u optimize et

**Beklenen İyileştirme:** %30-50 daha hızlı response

### 3. 🔄 Streaming Response (Önerilen)

**Sorun:** Kullanıcı tüm response'u bekliyor

**Çözüm:**
- Ollama streaming API kullan
- Response'u chunk'lar halinde gönder
- Kullanıcı daha hızlı yanıt görür

**Beklenen İyileştirme:** Perceived latency %60-80 azalır

### 4. 💾 Response Caching (Önerilen)

**Sorun:** Benzer sorular tekrar tekrar işleniyor

**Çözüm:**
- Benzer sorular için cache
- Redis veya MemoryCache kullan
- TTL: 1 saat

**Beklenen İyileştirme:** Cache hit'lerde %95+ hızlanma

### 5. ⚙️ Model Optimizasyonu (Önerilen)

**Sorun:** Model her seferinde yükleniyor olabilir

**Çözüm:**
- Ollama'da model'in memory'de tutulduğundan emin ol
- `OLLAMA_NUM_GPU=0` (CPU mode)
- `OLLAMA_NUM_THREAD=4` (CPU thread sayısı)
- Model warm-up (servis başlangıcında bir çağrı yap)

**Beklenen İyileştirme:** Cold start sorununu çözer

### 6. 🔁 Retry Mekanizması (Önerilen)

**Sorun:** Geçici hatalarda request fail oluyor

**Çözüm:**
- Exponential backoff ile retry
- Max 2-3 retry
- Sadece timeout ve network hatalarında

**Beklenen İyileştirme:** Geçici hatalarda %50+ başarı oranı artışı

### 7. 📊 Timeout Stratejisi (Yapıldı)

**Durum:** 120 saniye olarak ayarlandı

**Öneri:**
- Intent detection: 10 saniye (yapıldı)
- Main response: 120 saniye (yapıldı)
- Fallback response: Anında (yapıldı)

---

## 📈 Performans İyileştirme Öncelikleri

### Yüksek Öncelik (Hemen Yapılabilir)
1. ✅ Keyword-based intent detection (Yapıldı)
2. ⚠️ Prompt optimizasyonu (Yapılmalı)
3. ⚠️ Response caching (Yapılmalı)

### Orta Öncelik (Kısa Vadede)
4. Streaming response
5. Retry mekanizması
6. Model warm-up

### Düşük Öncelik (Uzun Vadede)
7. GPU desteği (production için)
8. Daha büyük model (qwen2.5:7b)
9. Load balancing (multiple Ollama instances)

---

## 🎯 Hedef Performans Metrikleri

### Mevcut
- Intent Detection: 5-10 saniye
- Main Response: 20-60 saniye
- Toplam: 25-70 saniye

### Optimizasyon Sonrası (Hedef)
- Intent Detection: 0.1 saniye (keyword-based) ✅
- Main Response: 10-30 saniye (prompt optimization)
- Toplam: 10-30 saniye
- Cache Hit: <1 saniye

**İyileştirme:** %60-80 daha hızlı

---

## 🔧 Implementasyon Önerileri

### 1. Prompt Optimizasyonu

```csharp
// Önceki: ~2000 token
// Sonra: ~800-1000 token

// System prompt kısalt
var systemPrompt = "Sen Moni, MonitraNG chatbot'usun. {langName} dilinde yardımcı ol.";

// Conversation history sınırla
var recentHistory = conversationHistory.TakeLast(3).ToList();

// Documentation sources özetle
var docsSummary = documentationSources
    .Select(d => $"- {d.Title}")
    .Take(3)
    .ToList();
```

### 2. Response Caching

```csharp
// Cache key: message hash + language
var cacheKey = $"chatbot:response:{HashMessage(request.Message)}:{request.Language}";

if (_cache.TryGetValue(cacheKey, out string cachedResponse))
{
    return cachedResponse; // <1 saniye
}

// Generate response...
_cache.Set(cacheKey, response, TimeSpan.FromHours(1));
```

### 3. Streaming Response

```csharp
// Ollama streaming API
var request = new
{
    model = model,
    prompt = prompt,
    stream = true  // Streaming açık
};

// Response'u chunk'lar halinde gönder
await foreach (var chunk in streamResponse)
{
    yield return chunk;
}
```

---

## 📝 Sonuç

**Ana Sorun:** Her request'te 2 LLM çağrısı + uzun prompt'lar → 30 saniye yetersiz

**Çözüm:**
1. ✅ Keyword-based intent detection (yapıldı)
2. ⚠️ Prompt optimizasyonu (yapılmalı)
3. ⚠️ Response caching (yapılmalı)
4. Timeout: 120 saniye (yapıldı, geçici çözüm)

**Sonraki Adımlar:**
1. Prompt optimizasyonu implement et
2. Response caching ekle
3. Streaming response düşün (frontend hazır olduğunda)

---

**Not:** Timeout artırmak geçici bir çözümdü. Asıl sorun performans optimizasyonu ile çözülmeli.
