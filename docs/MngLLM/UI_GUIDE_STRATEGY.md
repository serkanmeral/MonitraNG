# UI Rehber Desteği Stratejisi

**Tarih:** 15 Ocak 2026  
**Servis:** MngLLM  
**Amaç:** Chatbot'un UI fonksiyonları için rehber vermesi

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Mevcut Durum](#mevcut-durum)
3. [UI Rehber Yapısı](#ui-rehber-yapısı)
4. [Rehber Formatı](#rehber-formatı)
5. [Chatbot Entegrasyonu](#chatbot-entegrasyonu)
6. [Implementasyon Planı](#implementasyon-planı)

---

## 🎯 Genel Bakış

### Amaç

Chatbot'un kullanıcılara UI fonksiyonları için adım adım rehberlik verebilmesi:
- "Dataset nasıl oluşturulur?"
- "Automated Form nasıl oluşturulur?"
- "Side Menu'ye nasıl item eklenir?"
- "Şifremi nasıl değiştiririm?"

### Strateji

1. **Mevcut rehberleri kullan:** `docs/Mng.Ui/guides/` klasöründeki markdown dosyaları
2. **Chatbot-optimized format:** Rehberleri chatbot'un anlayabileceği formatta hazırla
3. **Structured data:** Adım adım talimatlar, route'lar, örnekler
4. **Dinamik içerik:** LLM ile kullanıcının diline göre özelleştir

---

## 📚 Mevcut Durum

### Mevcut UI Rehberleri

**Klasör:** `docs/Mng.Ui/guides/`

1. ✅ **AUTOMATED_FORMS_USAGE.md** - Automated Forms kullanım kılavuzu
2. ✅ **I18N_GUIDE.md** - Dil desteği rehberi
3. ✅ **GATEWAY_INTEGRATION.md** - API Gateway entegrasyonu
4. ✅ **DOCKER_DEPLOYMENT.md** - Docker deployment
5. ✅ **HUB_TEST_GUIDE.md** - Hub test rehberi

### Mevcut Spec'ler

**Klasör:** `docs/Mng.Ui/specs/`

1. ✅ **DATASET_UI_DESIGN.md** - Dataset UI tasarımı
2. ✅ **SIDE_MENU_PLANNING.md** - Side Menu planlama
3. ✅ **AUTOMATED_FORMS_PLANNING.md** - Automated Forms planlama

### Eksikler

- ❌ UI fonksiyonları için kullanıcı dostu adım adım rehberler
- ❌ Chatbot'un anlayabileceği structured format
- ❌ Route linkleri ve navigation bilgileri
- ❌ Örnek senaryolar

---

## 📖 UI Rehber Yapısı

### Önerilen Klasör Yapısı

```
docs/Mng.Ui/guides/
├── automated-forms/
│   ├── creating-form.md          # Form oluşturma
│   ├── configuring-fields.md     # Field yapılandırma
│   └── using-form.md             # Form kullanımı
├── datasets/
│   ├── creating-dataset.md       # Dataset oluşturma
│   ├── adding-fields.md          # Field ekleme
│   ├── configuring-validation.md # Validasyon ayarlama
│   └── managing-data.md          # Veri yönetimi
├── side-menu/
│   ├── adding-menu-item.md       # Menu item ekleme
│   └── configuring-permissions.md # Yetkilendirme
├── user-management/
│   ├── changing-password.md      # Şifre değiştirme
│   ├── updating-profile.md       # Profil güncelleme
│   └── managing-users.md         # Kullanıcı yönetimi
└── ...
```

### Chatbot-Optimized Format

Her rehber için:

**Klasör:** `docs/Mng.Ui/guides/chatbot/` (Chatbot için optimize edilmiş)

```
docs/Mng.Ui/guides/chatbot/
├── automated-forms.md
├── datasets.md
├── side-menu.md
├── user-management.md
└── ...
```

**Alternatif:** Mevcut rehberleri chatbot için optimize et (metadata ekle)

---

## 📄 Rehber Formatı

### Standard UI Guide Template

```markdown
---
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create", "schema"]
route: "/apps/datasets/create"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
---

# Dataset Oluşturma Rehberi

## Özet
Bu rehber, MonitraNG platformunda yeni bir dataset oluşturmayı adım adım açıklar.

## Önkoşullar
- Manager veya Admin yetkisi
- Dataset Management sayfasına erişim

## Adımlar

### 1. Dataset Management Sayfasına Git
**Route:** `/apps/datasets`

**Yöntem:**
1. Sol menüden **"Datasets"** menü öğesine tıklayın
2. Veya doğrudan `/apps/datasets` adresine gidin

### 2. Yeni Dataset Oluştur
**Action:** Sol üst köşedeki **"Yeni Dataset"** butonuna tıklayın

**Route:** `/apps/datasets/create` otomatik olarak açılır

### 3. Temel Bilgileri Doldur
**Form Fields:**
- **Dataset Adı:** `@books` (örn: `@` ile başlamalı)
- **Kategori:** Bir kategori seçin veya yeni oluşturun
- **Açıklama:** Dataset'in amacını açıklayın

### 4. Field'ları Ekle
**Action:** "Field Ekle" butonuna tıklayın

**Field Types:**
- `text` - Metin alanı
- `number` - Sayı alanı
- `boolean` - Evet/Hayır
- `datetime` - Tarih/Saat
- `relation` - İlişkili dataset
- vb.

**Örnek Field:**
```json
{
  "name": "title",
  "type": "text",
  "required": true,
  "label": "Kitap Adı"
}
```

### 5. Validasyon Kuralları Ekle (Opsiyonel)
**Action:** Her field için validasyon kuralları ekleyebilirsiniz

**Örnek:**
- `minLength: 3` - Minimum 3 karakter
- `maxLength: 100` - Maksimum 100 karakter
- `pattern: "^[A-Z]"` - Regex pattern

### 6. Kaydet
**Action:** Form'un altındaki **"Kaydet"** butonuna tıklayın

**Sonuç:** Dataset başarıyla oluşturulur ve `/apps/datasets` listesinde görünür

## İlgili Linkler
- [Dataset Yönetimi](/apps/datasets)
- [Field Types Dokümantasyonu](/docs/datasets/field-types)
- [Validation Rules](/docs/datasets/validation)

## Örnek Senaryolar
- [Basit Dataset Oluşturma](/guides/examples/simple-dataset.md)
- [Relation Field ile Dataset](/guides/examples/dataset-with-relation.md)

## Sık Sorulan Sorular
**S: Dataset adı neden @ ile başlamalı?**  
C: `@` sembolü sistem dataset'lerini işaretler ve özel işlevsellik sağlar.

**S: Kaç field ekleyebilirim?**  
C: Sınırsız field ekleyebilirsiniz, ancak performans için 50'den fazla field önerilmez.

## Sorun Giderme
**Problem:** "Dataset adı zaten kullanılıyor" hatası  
**Çözüm:** Farklı bir dataset adı kullanın veya mevcut dataset'i düzenleyin.

**Problem:** Field eklerken hata alıyorum  
**Çözüm:** Field tipinin doğru olduğundan ve gerekli alanların doldurulduğundan emin olun.
```

### Chatbot-Optimized Format (Structured)

**Daha Yapılandırılmış Format:**

```yaml
---
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create", "schema"]
route: "/apps/datasets/create"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
steps:
  - order: 1
    title: "Dataset Management Sayfasına Git"
    route: "/apps/datasets"
    action: "Menüden 'Datasets' tıkla"
    expected_result: "Dataset listesi sayfası açılır"
  - order: 2
    title: "Yeni Dataset Oluştur"
    route: "/apps/datasets/create"
    action: "'Yeni Dataset' butonuna tıkla"
    expected_result: "Dataset oluşturma formu açılır"
  - order: 3
    title: "Temel Bilgileri Doldur"
    fields:
      - name: "datasetName"
        label: "Dataset Adı"
        type: "text"
        required: true
        example: "@books"
        validation: "Must start with @"
      - name: "category"
        label: "Kategori"
        type: "select"
        required: true
      - name: "description"
        label: "Açıklama"
        type: "textarea"
        required: false
    expected_result: "Form doldurulur"
  - order: 4
    title: "Field'ları Ekle"
    action: "'Field Ekle' butonuna tıkla"
    example_fields:
      - name: "title"
        type: "text"
        required: true
      - name: "pageCount"
        type: "number"
        required: false
    expected_result: "Field'lar eklendi"
  - order: 5
    title: "Kaydet"
    action: "'Kaydet' butonuna tıkla"
    expected_result: "Dataset başarıyla oluşturulur"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Dataset Management sayfasına erişim"
related_guides:
  - "Field Types"
  - "Validation Rules"
faq:
  - question: "Dataset adı neden @ ile başlamalı?"
    answer: "@ sembolü sistem dataset'lerini işaretler"
troubleshooting:
  - problem: "Dataset adı zaten kullanılıyor"
    solution: "Farklı bir dataset adı kullanın"
---

# Dataset Oluşturma Rehberi

[Markdown content - normal rehber metni]
```

---

## 🤖 Chatbot Entegrasyonu

### Rehber Arama

**DocumentationProvider:**
```csharp
public class DocumentationProvider : IDocumentationProvider
{
    public async Task<List<DocumentationResult>> SearchGuidesAsync(
        string query,
        string language = "tr",
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        // UI guides'ları ara
        var guideFiles = Directory.GetFiles(
            Path.Combine(_settings.MarkdownPath, "Mng.Ui/guides/chatbot"),
            "*.md",
            SearchOption.AllDirectories);
        
        // Parse front matter
        var guides = guideFiles.Select(f => ParseGuideMetadata(f)).ToList();
        
        // Search by query
        var results = guides
            .Where(g => 
                g.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                g.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                g.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList();
        
        return results;
    }
}
```

### Rehber İçeriğini Formatla

**ChatCommandHandler:**
```csharp
public class ChatCommandHandler : IRequestHandler<ChatCommand, ChatResponseDto>
{
    public async Task<ChatResponseDto> Handle(ChatCommand request, CancellationToken cancellationToken)
    {
        // Intent detection
        var intent = await DetectIntentAsync(request.Message, cancellationToken);
        
        if (intent == "guide")
        {
            // Search guides
            var guides = await _documentationProvider.SearchGuidesAsync(
                request.Message, 
                request.Language, 
                limit: 1, 
                cancellationToken);
            
            if (guides.Count > 0)
            {
                var guide = guides[0];
                
                // Get full guide content
                var content = await _documentationProvider.GetContentAsync(
                    guide.Id, 
                    cancellationToken);
                
                // Format for LLM
                var formattedGuide = FormatGuideForLLM(guide, content, request.Language);
                
                // Generate response
                var prompt = BuildGuidePrompt(request.Message, formattedGuide, request.Language);
                var llmResponse = await _llmService.GenerateAsync(prompt, cancellationToken);
                
                return new ChatResponseDto
                {
                    Response = llmResponse,
                    Intent = "guide",
                    Sources = new List<DocumentationSource>
                    {
                        new DocumentationSource
                        {
                            Title = guide.Title,
                            Url = $"/docs/guides/{guide.Category}/{guide.Id}",
                            Snippet = guide.Snippet
                        }
                    }
                };
            }
        }
        
        // Fallback to general response
        // ...
    }
    
    private string FormatGuideForLLM(
        DocumentationResult guide, 
        string content, 
        string language)
    {
        // Parse markdown
        // Extract steps
        // Format as structured text
        
        return $@"# {guide.Title}

{content}

## Adımlar:
{ExtractSteps(content)}

## Route:
{guide.Metadata["route"]}

## Tahmini Süre:
{guide.Metadata["estimated_time"]}";
    }
}
```

### LLM Prompt Örneği

```
You are a helpful assistant for MonitraNG platform.
User wants to learn: "{userQuestion}"

Here is a step-by-step guide:

{formattedGuide}

IMPORTANT: 
- Always respond in {languageName}
- Provide clear, numbered steps
- Include route paths (e.g., /apps/datasets)
- Mention expected results after each step
- Be friendly and encouraging

User question: {userQuestion}

Provide a helpful response based on the guide above.
```

---

## 🛠️ Implementasyon Planı

### Faz 1: Rehber Formatı Standardizasyonu (1 hafta)

**Görevler:**
1. ✅ Standard UI Guide Template oluştur
2. ✅ Front matter metadata yapısı belirle
3. ✅ Mevcut rehberleri yeni formata dönüştür
4. ✅ Chatbot için optimize edilmiş rehberler oluştur

**Öncelikli Rehberler:**
1. Dataset Oluşturma (`datasets/creating-dataset.md`)
2. Automated Form Oluşturma (`automated-forms/creating-form.md`)
3. Side Menu Item Ekleme (`side-menu/adding-menu-item.md`)
4. Şifre Değiştirme (`user-management/changing-password.md`)

### Faz 2: DocumentationProvider Geliştirme (1 hafta)

**Görevler:**
1. ✅ `SearchGuidesAsync()` metodu ekle
2. ✅ Guide metadata parser (front matter)
3. ✅ Step extractor (markdown'dan adımları çıkar)
4. ✅ Route link extractor

### Faz 3: Chatbot Entegrasyonu (1 hafta)

**Görevler:**
1. ✅ Intent detection: "guide" intent'i
2. ✅ Guide arama entegrasyonu
3. ✅ Guide formatlama (LLM için)
4. ✅ Response formatting (adım adım gösterim)

### Faz 4: Test ve İyileştirme (1 hafta)

**Görevler:**
1. ✅ Test senaryoları
2. ✅ Kullanıcı geri bildirimleri
3. ✅ Rehber güncellemeleri

---

## 📝 Örnek Rehber Yapısı

### Örnek 1: Basit Rehber (Markdown)

```markdown
---
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create"]
route: "/apps/datasets/create"
language: "tr"
---

# Dataset Oluşturma

## Adımlar

1. `/apps/datasets` sayfasına gidin
2. "Yeni Dataset" butonuna tıklayın
3. Form'u doldurun
4. "Kaydet" butonuna tıklayın
```

### Örnek 2: Detaylı Rehber (Structured)

```markdown
---
title: "Dataset Oluşturma"
steps:
  - order: 1
    title: "Dataset Management Sayfasına Git"
    route: "/apps/datasets"
    action: "Menüden 'Datasets' tıkla"
  - order: 2
    title: "Yeni Dataset Oluştur"
    route: "/apps/datasets/create"
    action: "'Yeni Dataset' butonuna tıkla"
---
[Detaylı içerik]
```

---

## ✅ Öneri

### İki Yöntem Karşılaştırma

**Yöntem 1: Ayrı Dokümantasyon (Önerilen)**
- ✅ Chatbot için optimize edilmiş
- ✅ Structured format
- ✅ Front matter metadata
- ✅ Kolay parse edilebilir
- ❌ İki dokümantasyon tutmak gerekir (maintainability)

**Yöntem 2: Mevcut Rehberleri Kullan**
- ✅ Tek dokümantasyon
- ✅ Maintainability kolay
- ❌ Chatbot için optimize değil
- ❌ Parse etmesi zor

### Önerilen Strateji

**Hibrit Yaklaşım:**
1. Mevcut rehberleri chatbot için optimize et (front matter ekle)
2. Chatbot-specific klasör oluştur: `docs/Mng.Ui/guides/chatbot/`
3. Mevcut rehberleri chatbot formatına dönüştür
4. Gelecekte: Automated guide generation (mevcut rehberlerden)

---

## 📊 Rehber Kategorileri

1. **Datasets**
   - Dataset oluşturma
   - Field ekleme
   - Validasyon ayarlama
   - Veri yönetimi

2. **Automated Forms**
   - Form oluşturma
   - Field yapılandırma
   - Liste ayarları
   - Form kullanımı

3. **Side Menu**
   - Menu item ekleme
   - Yetkilendirme ayarlama

4. **User Management**
   - Şifre değiştirme
   - Profil güncelleme
   - Kullanıcı yönetimi

5. **Settings**
   - Tema ayarları
   - Dil değiştirme
   - Preference'lar

---

**Son Güncelleme:** 15 Ocak 2026
