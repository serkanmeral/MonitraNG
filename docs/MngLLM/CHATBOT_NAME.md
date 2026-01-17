# Chatbot İsim Belirleme

**Tarih:** 15 Ocak 2026  
**Servis:** MngLLM  
**Durum:** ✅ İsim Seçildi: **Moni**

---

## 🎯 Seçilen İsim: **Moni**

### İsim Hakkında

**Moni** - MonitraNG'in sevimli ve profesyonel yardımcısı

**Telaffuz:**
- 🇹🇷 Türkçe: **Moni** (MOH-nee)
- 🇬🇧 İngilizce: **Moni** (MOH-nee)
- 🇫🇷 Fransızca: **Moni** (moh-NEE)
- 🇸🇦 Arapça: **موني** (MO-nee)
- 🇨🇳 Çince: **莫尼** (Mò ní)

### Özellikler

- ✅ **Kısa ve Hatırlanabilir:** 2 hece, kolay telaffuz
- ✅ **Platform ile Bağlantılı:** MonitraNG'den türetilmiş
- ✅ **Profesyonel:** Kurumsal ortama uygun
- ✅ **Dostane:** Kullanıcı dostu, yardımcı
- ✅ **Çok Dilli:** Tüm desteklenen dillerde kolay telaffuz

---

## 💬 Kullanım Senaryoları

### Kullanıcı Etkileşimi

**Örnek 1: İsim ile Hitap**
```
Kullanıcı: "Merhaba Moni!"
Moni: "Merhaba! Size nasıl yardımcı olabilirim?"
```

**Örnek 2: Doğrudan Soru**
```
Kullanıcı: "Moni, dataset nasıl oluşturulur?"
Moni: "Dataset oluşturmak için şu adımları izleyebilirsiniz..."
```

**Örnek 3: İsim ile Soru**
```
Kullanıcı: "Moni yardım et"
Moni: "Tabii ki! Hangi konuda yardımcı olabilirim?"
```

---

## 🤖 Backend Implementation

### System Prompt

```csharp
private string BuildPrompt(string message, string language, string context, ConversationContext? conversationContext)
{
    var languageName = GetLanguageName(language);
    
    var systemPrompt = $"You are Moni, a helpful assistant for MonitraNG platform. " +
                      $"Your name is Moni (short for MonitraNG). " +
                      $"User's language preference: {languageName}. " +
                      $"IMPORTANT: Always respond in {languageName}. " +
                      $"Be friendly, professional, and helpful. " +
                      $"You can be addressed as 'Moni' by users. " +
                      $"Always introduce yourself as Moni when needed.";
    
    // ... rest of prompt
}
```

### Intent Detection - Name Recognition

**ChatCommandHandler:**
```csharp
private async Task<string> DetectIntentAsync(string message, CancellationToken cancellationToken)
{
    // Check if user is addressing Moni by name
    var lowerMessage = message.ToLowerInvariant();
    var isAddressingMoni = lowerMessage.Contains("moni");
    
    if (isAddressingMoni)
    {
        // Remove "moni" from message for processing
        message = Regex.Replace(message, @"\bmoni\b", "", RegexOptions.IgnoreCase).Trim();
    }
    
    // Detect intent (nlq, docs, guide, general)
    // ...
}
```

---

## 🎨 Frontend Implementation

### UI'da İsim Kullanımı

**ChatbotWidget.vue:**
```vue
<template>
  <v-card>
    <v-card-title>
      <v-avatar>
        <v-icon>mdi-robot</v-icon>
      </v-avatar>
      Moni
      <v-chip small color="primary">AI Assistant</v-chip>
    </v-card-title>
    
    <v-card-subtitle>
      MonitraNG Yardımcısı
    </v-card-subtitle>
    
    <!-- Empty state -->
    <v-card-text v-if="messages.length === 0">
      <div class="empty-state">
        <h3>Merhaba! Ben Moni 👋</h3>
        <p>Size nasıl yardımcı olabilirim?</p>
        <!-- Example questions -->
      </div>
    </v-card-text>
    
    <!-- Messages -->
    <v-card-text>
      <ChatMessage 
        v-for="msg in messages" 
        :key="msg.id"
        :message="msg"
      />
    </v-card-text>
    
    <!-- Input -->
    <v-card-actions>
      <v-text-field
        :placeholder="`Moni'ye sor...`"
        v-model="inputMessage"
        @keyup.enter="sendMessage(inputMessage)"
      />
    </v-card-actions>
  </v-card>
</template>
```

### Locale Dosyalarına İsim Ekleme

**tr.json:**
```json
{
  "chatbot": {
    "name": "Moni",
    "title": "Moni",
    "subtitle": "MonitraNG Yardımcısı",
    "greeting": "Merhaba! Ben Moni 👋",
    "placeholder": "Moni'ye sor...",
    "examples": {
      "q1": "Moni, dataset nasıl oluşturulur?",
      "q2": "Moni, automated form nedir?",
      "q3": "Moni, API authentication nasıl yapılır?"
    }
  }
}
```

**en.json:**
```json
{
  "chatbot": {
    "name": "Moni",
    "title": "Moni",
    "subtitle": "MonitraNG Assistant",
    "greeting": "Hello! I'm Moni 👋",
    "placeholder": "Ask Moni...",
    "examples": {
      "q1": "Moni, how do I create a dataset?",
      "q2": "Moni, what is an automated form?",
      "q3": "Moni, how do I authenticate with the API?"
    }
  }
}
```

---

## 📝 Diğer İsim Alternatifleri

### Alternatif 1: Mira

**Özellikler:**
- "Harika" anlamı (Türkçe)
- Modern ve profesyonel
- MonitraNG ile bağlantılı (MI-RA)

**Dezavantaj:**
- Moni kadar kısa değil

### Alternatif 2: Mona

**Özellikler:**
- MonitraNG'den türetilmiş
- Klasik isim
- Hatırlanabilir

**Dezavantaj:**
- Moni kadar sevimli değil

### Alternatif 3: Nitra

**Özellikler:**
- MonitraNG'den türetilmiş
- Modern

**Dezavantaj:**
- Telaffuzu Moni kadar kolay değil

---

## ✅ Sonuç

**Seçilen İsim:** **Moni**

**Neden:**
- ✅ En kısa ve hatırlanabilir
- ✅ Platform ile en güçlü bağlantı
- ✅ Tüm dillerde kolay telaffuz
- ✅ Profesyonel ve dostane
- ✅ Marka uyumu en yüksek

---

**Son Güncelleme:** 15 Ocak 2026
