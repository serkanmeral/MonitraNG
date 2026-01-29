---
title: "Chatbot — Dokümantasyondan Nasıl Faydalanır?"
category: "guides"
tags: ["chatbot", "documentation", "documentation-provider", "search", "front-matter", "RAG"]
service: "MngLLM"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
---

# Chatbot — Dokümantasyondan Nasıl Faydalanır?

Bu rehber, MonitraNG chatbot’unun (Moni) **dokümanlardan nasıl yararlandığını**, hangi iyileştirmelerin yapılabileceğini ve **doküman yazarken** chatbot’un daha iyi sonuç üretmesi için nelere dikkat edileceğini açıklar.

---

## 1. Mevcut akış: Chatbot dokümanları nasıl kullanıyor?

### 1.1 Veri kaynakları

Chatbot, **DocumentationProvider** aracılığıyla iki tür kaynaktan beslenir:

| Kaynak | Açıklama | Konfigürasyon |
|--------|----------|----------------|
| **Markdown** | `docs/content/` altındaki tüm `*.md` dosyaları | `MngLLMSettings:Documentation:MarkdownPath` (varsayılan: `../../docs/content`) |
| **OpenAPI** | Servislerin Swagger/OpenAPI JSON’ları | `MngLLMSettings:Documentation:ServiceEndpoints` (MngKeeper, MngDataGateway, MngHub, MngLLM, MngGateway, MngNotifier, MngScheduler, MngAdmin) |

Path çözümleme sırası:

1. `AppContext.BaseDirectory + MarkdownPath` (publish ortamı)
2. `Directory.GetCurrentDirectory() + MarkdownPath` (dotnet run)
3. Çözüm köküne kadar üst dizinlerde `README.md` aranır; bulunan kök + `docs/content`

Docker’da **docs** container’a verilmiyorsa indeks boş kalır; production’da `docs/content`’in **volume mount** veya build aşamasında kopyalanması gerekir.

### 1.2 İndeksleme

- **Markdown:** Her dosya okunur, YAML **front matter** parse edilir (`title`, `service`, `category`, `tags`). İçerik Markdig ile plain text’e çevrilir. Başlık (H1) ve keyword’ler indekse yazılır.
- **OpenAPI:** Her endpoint için `method + path`, summary, description, parameters, requestBody, responses metin olarak birleştirilip ayrı bir “doküman” kaydı oluşturulur.
- **Arama:** Kelime tabanlı (keyword) arama. Sorgu kelimelere bölünür, stop word’ler atılır; her doküman için *relevance score* hesaplanır (başlık eşleşmesi, keyword eşleşmesi, içerik eşleşmesi). Sonuçlar skora göre sıralanır.

### 1.3 Intent ve doküman araması

Kullanıcı mesajı önce **intent** ile sınıflandırılır:

| Intent | Örnek anahtar kelimeler | Doküman araması |
|--------|-------------------------|------------------|
| `docs` | dokümantasyon, doküman, api, documentation, swagger, endpoint, reference | ✅ Yapılır |
| `guide` | nasıl, adım, rehber, tutorial, how, step, guide, oluştur, create, ekle, add | ✅ Yapılır |
| `nlq` | göster, listele, getir, bul, sorgula, query, show, list, find, get, dataset, veri, data | ✅ Yapılır |
| `general` | Diğer tüm mesajlar | ❌ Yapılmaz |

Yalnızca `docs`, `guide` ve `nlq` için `DocumentationProvider.SearchAsync(message, limit: 3)` çağrılır. En fazla **3** doküman ve ilk **2** dokümandan **snippet** alınarak LLM prompt’una eklenir.

### 1.4 Prompt’a eklenen bilgi

- Snippet’lar: `"Başlık: … içerik özeti …"` formatında.
- Kaynak listesi: Başlık, servis, kategori; yanıtla birlikte kullanıcıya da döner (`DocumentationSources`).
- Sistem prompt’ta “dokümantasyon bilgisini kullan, yetersizse hangi dokümana bakılacağını söyle” yönlendirmesi vardır.

Özet: **Chatbot, dokümanlardan şu an sadece intent “docs/guide/nlq” iken, keyword tabanlı arama ile bulunan en fazla 3 dokümanın snippet’ları ve kaynak listesiyle yanıt üretiyor.**

---

## 2. Yapılabilecek iyileştirmeler

Aşağıdaki maddeler, chatbot’un dokümanlardan **daha iyi ve güvenilir** faydalanması için yapılabilecek teknik ve dokümantasyon tarafı iyileştirmeleri özetler.

### 2.1 Doküman erişimi ve konfigürasyon

| Öncelik | Konu | Öneri |
|---------|------|--------|
| Yüksek | **Production’da docs yolu** | Docker/Compose’ta `docs/content` için volume mount veya build aşamasında `COPY` ile MngLLM konteynerine verilmesi; `MarkdownPath`’in ortama göre (env/config) ayarlanması. |
| Orta | **Include/exclude pattern** | Belirli dosya/klasörleri indeksten hariç tutma (örn. `DUPLICATE_*.md`, sadece internal notlar). `DocumentationSettings`’e glob veya prefix listesi eklenebilir. |

### 2.2 Arama kalitesi

| Öncelik | Konu | Öneri |
|---------|------|--------|
| Yüksek | **Sadece keyword araması** | “Kullanıcı nasıl eklenir?” ile “User Management” / “Kullanıcı Yönetimi” eşleşmeyebilir. Türkçe/İngilizce eş anlamlı kelimeler (synonym map), isteğe bağlı basit stemmer veya ileride embedding/semantic arama ile iyileştirilebilir. |
| Orta | **Dil farkı** | Front matter’da `language: "tr"` gibi alan var; sorgu diline göre öncelik (ranking) veya filtre eklenebilir. |
| Düşük | **Chunking** | Şu an dosya = 1 doküman. Uzun TECHNICAL_SPECS veya rehberler `##` başlıklarına göre parçalara bölünüp ayrı indeks kaydı yapılabilir; daha ince parça ile snippet kalitesi artar. |

### 2.3 Intent ve tetikleme

| Öncelik | Konu | Öneri |
|---------|------|--------|
| Orta | **Sadece docs/guide/nlq’de arama** | “Dataset nedir?”, “Moni ne yapar?” gibi sorular `general`’e düşüp doküman çekilmiyor. Bu tür sorularda da az sayıda (örn. 1–2) doküman çekmek veya “general” için de hafif doküman araması yapmak faydalı olabilir. |
| Düşük | **Limit ve snippet sayısı** | `limit: 3`, snippet için 2 doc sabit. Intent’e veya konfigürasyona göre (örn. “docs” için 5) artırılabilir. |

### 2.4 İndeks güncellemesi

| Öncelik | Konu | Öneri |
|---------|------|--------|
| Orta | **Reindex tetikleme** | Şu an sadece süreye göre (örn. 60 dk) veya ilk istekte reindex var. Dokümanlar deployment sonrası güncelleniyorsa, bir admin API veya webhook ile “reindex now” çağrısı eklenebilir. |
| Düşük | **Reindex interval** | `ReindexIntervalMinutes` config’ten ayarlanabilir; yük ve güncellik dengesine göre değiştirilebilir. |

### 2.5 Doküman yapısı ve standartlar

| Öncelik | Konu | Öneri |
|---------|------|--------|
| Yüksek | **Front matter tutarlılığı** | Chatbot’un arama ve kategorilemesi `title`, `service`, `category`, `tags` kullanıyor. Chatbot’un kullanacağı **tüm** sayfalarda bu alanların dolu ve tutarlı olması (bkz. §3) önemli. |
| Orta | **Teknik spec’ler** | TECHNICAL_SPECS zaten “hem insan hem Chatbot tarafından parse edilebilir” olacak şekilde tanımlı (DOCUMENTATION_STANDARDS §3.6). Başlık hiyerarşisi, tablolar ve örnekler chatbot için de anlamlı; sadece metin özeti kritik bilginin resme hapsedilmemesine dikkat edilmeli. |

Bu maddeler product/tech roadmap’te “Chatbot + Dokümantasyon” başlığı altında kısa görevlere bölünebilir.

---

## 3. Doküman yazarken chatbot için öneriler

Chatbot’un ilgili sayfayı bulup doğru snippet üretmesi için aşağıdaki kurallar önerilir.

### 3.1 Front matter (YAML)

Chatbot’un indekslediği Markdown dosyalarında **en az** şu alanlar kullanılmalıdır:

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| `title` | Evet | Sayfa başlığı; arama ve snippet’ta görünür. |
| `service` | Önerilen | İlgili servis: MngKeeper, MngDataGateway, Mng.Ui, MngLLM, vb. |
| `category` | Önerilen | Kategori: api, guides, datasets, architecture, setup, troubleshooting, vb. |
| `tags` | Önerilen | Anahtar kelime listesi; Türkçe/İngilizce karşılıklar eklenebilir (örn. `["dataset", "veri kümesi", "oluşturma"]`). |

Örnek:

```yaml
---
title: "Dataset Field Types ve Özellikleri"
category: "datasets"
tags: ["dataset", "field-types", "veri kümesi", "alan tipleri"]
service: "MngDataGateway"
language: "tr"
---
```

İsteğe bağlı: `difficulty`, `estimated_time`, `priority` (DATASET_DOCUMENTATION_PLAN ile uyum için kullanılabilir; provider şu an bunları skorlamada kullanmıyor ama ileride kullanılabilir).

### 3.2 Başlık ve metin yapısı

- **İlk H1** mümkünse `title` ile aynı veya çok yakın olsun; provider H1’i başlık fallback’i olarak kullanıyor.
- Anlamlı **H2/H3** kullanın; ileride chunking eklenirse bölüm bazlı arama için faydalı olur.
- Kritik kavramlar **hem başlıkta hem paragrafta** geçsin; sadece tablo veya kod içinde kalmasın.

### 3.3 Arama dostu ifadeler

- Kullanıcıların soracağı sorulara yakın cümleler kurun: “Kullanıcı nasıl eklenir?”, “Dataset nasıl oluşturulur?”, “API endpoint’i nerede?”
- Türkçe ve İngilizce terimleri birlikte kullanmak (örn. “veri kümesi (dataset)”) hem insan hem keyword araması için iyileştirir.

### 3.4 Teknik spec ve tablolar

- DOCUMENTATION_STANDARDS §3.6’ya uygun yapı (tablolar, alan açıklamaları, örnekler) chatbot’un metin tabanlı parse etmesi için uygundur.
- Özet veya “Bu endpoint ne yapar?” gibi tek cümlelik açıklamalar, snippet’ların anlamlı çıkmasına yardım eder.

---

## 4. İlgili dokümanlar

- [DOCUMENTATION_STANDARDS](../../../DOCUMENTATION_STANDARDS.md) — Dokümantasyon kuralları; §3.6 Technical Specs, §3.7 ChatBot ve insan okunabilirliği.
- [Dataset Dokümantasyon Planı](DATASET_DOCUMENTATION_PLAN.md) — Dataset sayfaları için front matter şablonları ve chatbot uyumu.
- MngLLM projesi `tests/TEST_GUIDE.md` — İndeks, arama ve reindex testleri (kaynak dizininde).
- [MngLLM Technical Specs](../../main/TECHNICAL_SPECS.md) — Docs arama endpoint’leri ve parametreleri.

---

*Bu rehber, chatbot’un dokümanlardan faydalanma modeli ve iyileştirme alanlarının tek referansı olarak güncellenebilir.*
