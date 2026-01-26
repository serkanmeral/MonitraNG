# MkDocs Dokümantasyon Planlaması - Chatbot & İnsanlar İçin Hibrit Format

**Tarih:** 16 Ocak 2026  
**Servis:** MngLLM  
**Amaç:** MkDocs dosyalarını hem chatbot hem de insanlar için uygun hale getirme planı

---

## 📋 İÇİNDEKİLER

1. [Genel Bakış](#genel-bakış)
2. [Mevcut Durum Analizi](#mevcut-durum-analizi)
3. [Hibrit Format Stratejisi](#hibrit-format-stratejisi)
4. [Front Matter Standardı](#front-matter-standardı)
5. [Markdown Yapılandırma Standartları](#markdown-yapılandırma-standartları)
6. [Chatbot Parse Edilebilirlik](#chatbot-parse-edilebilirlik)
7. [İnsan Okunabilirliği](#insan-okunabilirliği)
8. [Template ve Örnekler](#template-ve-örnekler)
9. [Implementasyon Planı](#implementasyon-planı)

---

## 🎯 Genel Bakış

### Hedef

Aynı MkDocs markdown dosyası hem:
- ✅ **İnsanlar** tarafından okunabilir (MkDocs Material theme ile render edilir)
- ✅ **Chatbot** tarafından parse edilebilir (structured metadata ve content)

### Yaklaşım: Hibrit Format

1. **Front Matter (YAML):** Chatbot için metadata
2. **Markdown Content:** İnsanlar için içerik
3. **Structured Sections:** Chatbot parse edebilir yapı (adımlar, route'lar, vb.)

### Avantajlar

- ✅ **Tek Dokümantasyon:** Maintainability kolay
- ✅ **MkDocs Uyumlu:** Material theme ile render edilir
- ✅ **Chatbot Parse Edilebilir:** Front matter ve structured sections
- ✅ **İnsan Okunabilir:** Normal markdown formatı
- ✅ **Geriye Dönük Uyumlu:** Mevcut dosyalara front matter eklenebilir

---

## 📊 Mevcut Durum Analizi

### MkDocs Yapısı

**Konum:** `docs/content/`

**Yapı:**
```
docs/
├── mkdocs.yml                    # MkDocs konfigürasyonu
├── content/                      # Dokümantasyon içeriği
│   ├── index.md
│   ├── api/
│   ├── MngKeeper/
│   ├── MngDataGateway/
│   ├── Mng.Ui/
│   │   └── guides/
│   │       ├── AUTOMATED_FORMS_USAGE.md
│   │       ├── I18N_GUIDE.md
│   │       └── ...
│   └── ...
└── site/                         # Build output (gitignore)
```

### Mevcut Rehber Formatı

**Örnek:** `docs/Mng.Ui/guides/AUTOMATED_FORMS_USAGE.md`

```markdown
# Automated Forms Kullanım Kılavuzu

**Son Güncelleme:** 12 Ocak 2026  
**Versiyon:** 1.0

---

## 📋 Genel Bakış
...
```

**Özellikler:**
- ✅ Markdown formatı
- ✅ Başlıklar (H1, H2, H3)
- ✅ Kod blokları
- ✅ Listeler
- ❌ Front matter yok
- ❌ Structured metadata yok
- ❌ Chatbot parse edilebilir yapı yok

### MkDocs Material Theme Özellikleri

**Desteklenen Özellikler:**
- ✅ Front matter (YAML) - Material theme destekler
- ✅ Markdown extensions (admonitions, code blocks, tables)
- ✅ Navigation (mkdocs.yml'de tanımlı)
- ✅ Search (built-in)
- ✅ Code highlighting

---

## 🔄 Hibrit Format Stratejisi

### Format Yapısı

```markdown
---
# Front Matter (YAML) - Chatbot için metadata
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create", "tutorial"]
service: "MngDataGateway"
route: "/apps/datasets/create"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
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
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Dataset Management sayfasına erişim"
related_guides:
  - "Field Types"
  - "Validation Rules"
---

# Dataset Oluşturma Rehberi

[Markdown content - İnsanlar için normal rehber metni]

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

[Devam eden markdown içerik...]
```

### İki Formatın Birlikte Çalışması

**Front Matter (YAML):**
- Chatbot tarafından parse edilir
- MkDocs Material theme tarafından görmezden gelinir (render edilmez)
- Metadata olarak kullanılır

**Markdown Content:**
- İnsanlar tarafından okunur (MkDocs render eder)
- Chatbot tarafından da parse edilebilir (structured sections)
- Normal markdown formatı

---

## 📝 Front Matter Standardı

### Zorunlu Alanlar

```yaml
---
title: string          # Rehber başlığı (H1 ile aynı olmalı)
category: string       # Kategori (datasets, automated-forms, side-menu, vb.)
tags: array           # Arama için keyword'ler
language: string      # Dil kodu (tr, en, fr, ar, zh)
---
```

### Opsiyonel Alanlar

```yaml
---
service: string       # Hangi servis (MngDataGateway, Mng.Ui, vb.)
route: string         # İlgili route (örn: /apps/datasets/create)
difficulty: string    # Zorluk seviyesi (beginner, intermediate, advanced)
estimated_time: string # Tahmini süre (örn: "5 dakika")
priority: number      # Öncelik (1-10, yüksek öncelikli önce gösterilir)
steps: array          # Adım adım talimatlar (structured)
prerequisites: array  # Önkoşullar
related_guides: array # İlgili rehberler
faq: array           # Sık sorulan sorular
troubleshooting: array # Sorun giderme
---
```

### Steps Yapısı (Structured)

```yaml
steps:
  - order: 1
    title: "Adım Başlığı"
    route: "/apps/datasets"  # Opsiyonel
    action: "Yapılacak işlem"
    expected_result: "Beklenen sonuç"  # Opsiyonel
    code_example: |  # Opsiyonel
      ```
      // Kod örneği
      ```
  - order: 2
    title: "Sonraki Adım"
    ...
```

### Örnek Front Matter

```yaml
---
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create", "schema", "tutorial"]
service: "MngDataGateway"
route: "/apps/datasets/create"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Dataset Management Sayfasına Git"
    route: "/apps/datasets"
    action: "Sol menüden 'Datasets' menü öğesine tıklayın"
    expected_result: "Dataset listesi sayfası açılır"
  - order: 2
    title: "Yeni Dataset Oluştur"
    route: "/apps/datasets/create"
    action: "Sol üst köşedeki 'Yeni Dataset' butonuna tıklayın"
    expected_result: "Dataset oluşturma formu açılır"
  - order: 3
    title: "Temel Bilgileri Doldur"
    action: "Form'u doldurun: Dataset Adı, Kategori, Açıklama"
    expected_result: "Form doldurulur"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Dataset Management sayfasına erişim"
related_guides:
  - "Field Types"
  - "Validation Rules"
---
```

---

## 📖 Markdown Yapılandırma Standartları

### Başlık Yapısı

```markdown
# Ana Başlık (H1) - Front matter'daki title ile aynı olmalı
## Bölüm Başlığı (H2)
### Alt Bölüm (H3)
```

**Kural:** H1 başlığı front matter'daki `title` ile aynı olmalı.

### Adım Adım Talimatlar

**Structured Format (Chatbot için):**
```markdown
## Adımlar

### 1. [Adım Başlığı]
**Route:** `/apps/datasets`

**Yöntem:**
1. Sol menüden **"Datasets"** menü öğesine tıklayın
2. Veya doğrudan `/apps/datasets` adresine gidin

**Beklenen Sonuç:** Dataset listesi sayfası açılır
```

**Alternatif (Daha Basit):**
```markdown
## Adımlar

1. **Dataset Management Sayfasına Git** (`/apps/datasets`)
   - Sol menüden **"Datasets"** menü öğesine tıklayın
   - Veya doğrudan `/apps/datasets` adresine gidin

2. **Yeni Dataset Oluştur** (`/apps/datasets/create`)
   - Sol üst köşedeki **"Yeni Dataset"** butonuna tıklayın
```

### Route Linkleri

**Format:**
```markdown
**Route:** `/apps/datasets`
**Route:** `/apps/datasets/create`
```

**Chatbot Parse:** Route'ları extract edebilir (regex: `/apps/[\w/-]+`)

### Kod Örnekleri

```markdown
### Örnek

```typescript
// Kod örneği
const example = "value";
```
```

### Önemli Notlar

```markdown
> **Not:** Önemli bilgi
> **Uyarı:** Dikkat edilmesi gereken
> **İpucu:** Yardımcı bilgi
```

---

## 🤖 Chatbot Parse Edilebilirlik

### Parse Stratejisi

**1. Front Matter Parse:**
```csharp
// YAML front matter'ı parse et
var frontMatter = ParseYamlFrontMatter(markdownContent);
var title = frontMatter["title"];
var steps = frontMatter["steps"];
var tags = frontMatter["tags"];
```

**2. Markdown Content Parse:**
```csharp
// Markdown içeriğini parse et
var markdown = RemoveFrontMatter(markdownContent);
var headings = ExtractHeadings(markdown);
var routes = ExtractRoutes(markdown); // Regex: /apps/[\w/-]+
var codeBlocks = ExtractCodeBlocks(markdown);
```

**3. Structured Sections Parse:**
```csharp
// "## Adımlar" bölümünü bul
var stepsSection = ExtractSection(markdown, "Adımlar");
var steps = ParseSteps(stepsSection); // Numbered list veya structured format
```

### Parse Edilebilir Elementler

1. **Front Matter:**
   - title, category, tags, route, steps, vb.

2. **Markdown Content:**
   - Başlıklar (H1, H2, H3)
   - Route linkleri (`/apps/...`)
   - Kod blokları
   - Listeler (numbered, bulleted)
   - Adım adım talimatlar

3. **Structured Sections:**
   - "## Adımlar" bölümü
   - "## Önkoşullar" bölümü
   - "## İlgili Linkler" bölümü
   - "## Sık Sorulan Sorular" bölümü

---

## 👥 İnsan Okunabilirliği

### MkDocs Material Theme Render

**Front Matter:**
- Material theme front matter'ı görmezden gelir (render etmez)
- Sadece markdown content render edilir

**Markdown Content:**
- Normal markdown olarak render edilir
- Material theme özellikleri kullanılabilir:
  - Admonitions (notlar, uyarılar)
  - Code highlighting
  - Tables
  - Tabs
  - vb.

### Okunabilirlik İyileştirmeleri

1. **Açık Başlıklar:** Her bölüm için net başlıklar
2. **Görsel Düzen:** Adımlar, örnekler, notlar ayrı bölümler
3. **Kod Örnekleri:** Syntax highlighting ile
4. **Linkler:** İlgili sayfalara linkler
5. **Örnekler:** Pratik örnekler

---

## 📋 Template ve Örnekler

### Standard UI Guide Template

**Dosya:** `docs/Mng.Ui/guides/templates/ui-guide-template.md`

```markdown
---
title: "[Rehber Başlığı]"
category: "[kategori]"
tags: ["tag1", "tag2", "tag3"]
service: "[MngDataGateway | Mng.Ui | vb.]"
route: "/apps/[route]"
difficulty: "beginner | intermediate | advanced"
estimated_time: "[X dakika]"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "[Adım 1 Başlığı]"
    route: "/apps/[route1]"
    action: "[Yapılacak işlem]"
    expected_result: "[Beklenen sonuç]"
  - order: 2
    title: "[Adım 2 Başlığı]"
    route: "/apps/[route2]"
    action: "[Yapılacak işlem]"
    expected_result: "[Beklenen sonuç]"
prerequisites:
  - "[Önkoşul 1]"
  - "[Önkoşul 2]"
related_guides:
  - "[İlgili Rehber 1]"
  - "[İlgili Rehber 2]"
---

# [Rehber Başlığı]

## Özet
[Kısa özet - 2-3 cümle]

## Önkoşullar
- [Önkoşul 1]
- [Önkoşul 2]

## Adımlar

### 1. [Adım 1 Başlığı]
**Route:** `/apps/[route1]`

**Yöntem:**
1. [Açıklama 1]
2. [Açıklama 2]

**Beklenen Sonuç:** [Beklenen sonuç]

### 2. [Adım 2 Başlığı]
**Route:** `/apps/[route2]`

**Yöntem:**
1. [Açıklama 1]
2. [Açıklama 2]

**Beklenen Sonuç:** [Beklenen sonuç]

## İlgili Linkler
- [Link 1](/path/to/guide1.md)
- [Link 2](/path/to/guide2.md)

## Sık Sorulan Sorular

**S: [Soru 1]**  
C: [Cevap 1]

**S: [Soru 2]**  
C: [Cevap 2]

## Sorun Giderme

**Problem:** [Problem 1]  
**Çözüm:** [Çözüm 1]

**Problem:** [Problem 2]  
**Çözüm:** [Çözüm 2]
```

### Örnek: Dataset Oluşturma Rehberi

**Dosya:** `docs/Mng.Ui/guides/chatbot/datasets/creating-dataset.md`

```markdown
---
title: "Dataset Oluşturma"
category: "datasets"
tags: ["dataset", "create", "schema", "tutorial"]
service: "MngDataGateway"
route: "/apps/datasets/create"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Dataset Management Sayfasına Git"
    route: "/apps/datasets"
    action: "Sol menüden 'Datasets' menü öğesine tıklayın"
    expected_result: "Dataset listesi sayfası açılır"
  - order: 2
    title: "Yeni Dataset Oluştur"
    route: "/apps/datasets/create"
    action: "Sol üst köşedeki 'Yeni Dataset' butonuna tıklayın"
    expected_result: "Dataset oluşturma formu açılır"
  - order: 3
    title: "Temel Bilgileri Doldur"
    action: "Form'u doldurun: Dataset Adı (@books), Kategori, Açıklama"
    expected_result: "Form doldurulur"
  - order: 4
    title: "Field'ları Ekle"
    action: "'Field Ekle' butonuna tıklayın ve field'ları ekleyin"
    expected_result: "Field'lar eklendi"
  - order: 5
    title: "Kaydet"
    action: "'Kaydet' butonuna tıklayın"
    expected_result: "Dataset başarıyla oluşturulur"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Dataset Management sayfasına erişim"
related_guides:
  - "Field Types"
  - "Validation Rules"
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

**Beklenen Sonuç:** Dataset listesi sayfası açılır

### 2. Yeni Dataset Oluştur
**Route:** `/apps/datasets/create`

**Action:** Sol üst köşedeki **"Yeni Dataset"** butonuna tıklayın

**Beklenen Sonuç:** Dataset oluşturma formu açılır

### 3. Temel Bilgileri Doldur
**Form Fields:**
- **Dataset Adı:** `@books` (örn: `@` ile başlamalı)
- **Kategori:** Bir kategori seçin veya yeni oluşturun
- **Açıklama:** Dataset'in amacını açıklayın

**Beklenen Sonuç:** Form doldurulur

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

**Beklenen Sonuç:** Field'lar eklendi

### 5. Kaydet
**Action:** Form'un altındaki **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Dataset başarıyla oluşturulur ve `/apps/datasets` listesinde görünür

## İlgili Linkler
- [Dataset Yönetimi](/apps/datasets)
- [Field Types Dokümantasyonu](/docs/datasets/field-types)
- [Validation Rules](/docs/datasets/validation)

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

---

## 🛠️ Implementasyon Planı

### Faz 1: Front Matter Standardı Belirleme (1 gün)

**Görevler:**
1. ✅ Front matter standardı dokümanı oluştur (bu dosya)
2. ✅ Template oluştur
3. ✅ Örnek rehber hazırla

### Faz 2: Mevcut Rehberleri Güncelleme (2-3 gün)

**Görevler:**
1. ✅ Mevcut rehberleri analiz et
2. ✅ Front matter ekle
3. ✅ Structured format'a dönüştür
4. ✅ Test et (MkDocs render + Chatbot parse)

**Öncelikli Rehberler:**
1. `AUTOMATED_FORMS_USAGE.md` → Front matter ekle
2. `I18N_GUIDE.md` → Front matter ekle
3. Yeni rehberler: `datasets/creating-dataset.md` (template kullanarak)

### Faz 3: Chatbot Parser Implementasyonu (1-2 hafta)

**Görevler:**
1. ✅ YAML front matter parser
2. ✅ Markdown content parser
3. ✅ Structured sections extractor
4. ✅ Route link extractor
5. ✅ Test et

### Faz 4: Dokümantasyon Güncelleme Süreci (Sürekli)

**Görevler:**
1. ✅ Yeni rehberler template kullanarak oluştur
2. ✅ Mevcut rehberleri güncellerken front matter ekle
3. ✅ Chatbot parse edilebilirliğini kontrol et

---

## ✅ Sonuç

### Hibrit Format Avantajları

1. ✅ **Tek Dokümantasyon:** Maintainability kolay
2. ✅ **MkDocs Uyumlu:** Material theme ile render edilir
3. ✅ **Chatbot Parse Edilebilir:** Front matter ve structured sections
4. ✅ **İnsan Okunabilir:** Normal markdown formatı
5. ✅ **Geriye Dönük Uyumlu:** Mevcut dosyalara front matter eklenebilir

### Sonraki Adımlar

1. ✅ MkDocs planlama dokümanı hazırlandı
2. 📋 Template oluştur
3. 📋 İlk örnek rehberi hazırla (Dataset Oluşturma)
4. 📋 Chatbot parser implementasyonu

---

**Son Güncelleme:** 16 Ocak 2026
