---
title: "Dataset Categories (Dataset Kategorileri)"
category: "ui-guides"
tags: ["dataset-categories", "categories", "organization", "management", "dataset-organization"]
service: "Mng.Ui"
route: "/apps/dataset-categories"
difficulty: "beginner"
estimated_time: "8 dakika"
language: "tr"
priority: 1
summary: "Dataset Categories, dataset'leri mantıksal olarak gruplamak için kullanılan organizasyon mekanizmasıdır. Field Type'lardan farklıdır: Kategori dataset seviyesinde tüm dataset'i gruplar, Field Type ise field seviyesinde alanların veri tipini belirler. Örnek kategoriler: 'Book Categories', 'System Datasets'. Dataset oluştururken category field'ına kategori ID'si atanır."
faq:
  - question: "Dataset kategorisi nedir?"
    answer: "Dataset kategorisi, dataset'leri mantıksal olarak gruplamak ve organize etmek için kullanılan bir organizasyon mekanizmasıdır. Field Type'lardan tamamen farklıdır: Kategori dataset seviyesinde tüm dataset'i gruplar, Field Type ise field seviyesinde alanların veri tipini belirler."
  - question: "Dataset kategorisi ile field type arasındaki fark nedir?"
    answer: "Dataset kategorisi dataset'leri gruplamak için kullanılır (örnek: 'Book Categories'), field type ise dataset içindeki alanların veri tipini belirler (örnek: text, number, bool). Kategori dataset seviyesinde, field type ise field seviyesinde kullanılır."
steps:
  - order: 1
    title: "Dataset Categories Listesi Sayfasına Git"
    route: "/apps/dataset-categories"
    action: "Sol menüden 'Dataset Categories' menü öğesine tıklayın"
    expected_result: "Kategori listesi sayfası açılır"
  - order: 2
    title: "Yeni Kategori Oluştur"
    route: "/apps/dataset-categories/create"
    action: "'Yeni Kategori' butonuna tıklayın ve formu doldurun"
    expected_result: "Yeni kategori oluşturulur"
  - order: 3
    title: "Kategori Düzenle"
    route: "/apps/dataset-categories/edit/[dataId]"
    action: "Kategori listesinde 'Düzenle' butonuna tıklayın"
    expected_result: "Kategori bilgileri güncellenir"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Dataset Categories sayfasına erişim"
related_guides:
  - "Dataset Oluşturma"
  - "Automated Forms"
---

# Dataset Categories (Dataset Kategorileri)

## Özet
Dataset Categories ile dataset'leri kategorilere ayırabilir ve organize edebilirsiniz. Kategoriler dataset'lerin daha kolay bulunmasını sağlar.

## Dataset Kategorisi Nedir?

**Dataset Kategorisi**, dataset'leri mantıksal olarak gruplamak ve organize etmek için kullanılan bir organizasyon mekanizmasıdır. **Field Type'lardan tamamen farklıdır:**

### Dataset Kategorisi vs Field Type

| Özellik | Dataset Kategorisi | Field Type |
|---------|-------------------|------------|
| **Amaç** | Dataset'leri gruplamak ve organize etmek | Dataset içindeki alanların veri tipini belirlemek |
| **Kapsam** | Tüm dataset için geçerli (dataset seviyesi) | Tek bir field için geçerli (field seviyesi) |
| **Örnekler** | "Book Categories", "System Datasets", "User Management" | `text`, `number`, `bool`, `datetime`, `object`, `relation`, `persons`, `personGroups`, `incremental` |
| **Kullanım** | Dataset oluştururken `category` field'ına kategori ID'si atanır | Dataset field'larında `fieldType` olarak belirtilir |
| **Koleksiyon** | `@dataset_categories` | - (field definition içinde) |

### Örnek Senaryo

Bir "Books" dataset'i oluştururken:
- **Dataset Kategorisi:** "Book Categories" (tüm kitap ile ilgili dataset'leri gruplar)
- **Field Type'lar:** 
  - `title` → `text` field type
  - `price` → `number` field type
  - `publishedDate` → `datetime` field type
  - `isActive` → `bool` field type

**Önemli:** Dataset kategorisi, dataset'in hangi gruba ait olduğunu belirler. Field type ise dataset içindeki her bir alanın ne tür veri saklayacağını belirler.

## Önkoşullar
- Manager veya Admin yetkisi
- Dataset Categories sayfasına erişim izni

## Özellikler
- ✅ Kategori listesi (server-side pagination)
- ✅ Kategori arama
- ✅ Yeni kategori oluşturma
- ✅ Kategori düzenleme
- ✅ Kategori silme

## Adımlar

### 1. Dataset Categories Listesi Sayfasına Git
**Route:** `/apps/dataset-categories`

**Yöntem:**
1. Sol menüden **"Dataset Categories"** menü öğesine tıklayın
2. Kategori listesi sayfası açılır

**Beklenen Sonuç:** Kategori listesi tablosu görüntülenir

### 2. Yeni Kategori Oluştur
**Route:** `/apps/dataset-categories/create`

**Yöntem:**
1. Kategori listesi sayfasında **"Yeni Kategori"** butonuna tıklayın
2. Form alanlarını doldurun:
   - **Kategori Adı** (zorunlu)
   - **Açıklama** (opsiyonel)
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Yeni kategori oluşturulur ve kategori listesine yönlendirilirsiniz

### 3. Kategori Düzenle
**Route:** `/apps/dataset-categories/edit/[dataId]`

**Yöntem:**
1. Kategori listesinde düzenlemek istediğiniz kategorinin **"Düzenle"** butonuna tıklayın
2. Form alanlarını güncelleyin
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Kategori bilgileri güncellenir

### 4. Kategori Silme
**Route:** `/apps/dataset-categories`

**Yöntem:**
1. Kategori listesinde silmek istediğiniz kategorinin **"Sil"** butonuna tıklayın
2. Onay dialog'unda **"Sil"** butonuna tıklayın

**Beklenen Sonuç:** Kategori silinir ve listeden kaldırılır

## Form Alanları

### Zorunlu Alanlar
- **Kategori Adı:** Kategorinin adı (benzersiz)

### Opsiyonel Alanlar
- **Açıklama:** Kategorinin açıklaması

## Dataset'lerde Kullanım

Dataset oluştururken veya düzenlerken kategori seçebilirsiniz. Bu sayede dataset'ler kategorilere göre organize edilir.

## Sık Sorulan Sorular (FAQ)

### Dataset Kategorisi ile Field Type arasındaki fark nedir?

**Dataset Kategorisi:**
- Dataset'leri mantıksal olarak gruplamak için kullanılır
- Örnek: "Book Categories", "System Datasets"
- Dataset oluştururken `category` field'ına kategori ID'si atanır
- `@dataset_categories` koleksiyonunda saklanır

**Field Type:**
- Dataset içindeki alanların veri tipini belirler
- Örnek: `text`, `number`, `bool`, `datetime`
- Dataset field tanımlarında `fieldType` olarak belirtilir
- 9 farklı field type vardır: text, number, bool, datetime, object, relation, persons, personGroups, incremental

### Dataset kategorisi zorunlu mu?

Hayır, dataset kategorisi opsiyoneldir. Dataset oluştururken kategori seçmek zorunlu değildir, ancak dataset'leri organize etmek için önerilir.

### Bir dataset birden fazla kategoriye ait olabilir mi?

Hayır, bir dataset sadece bir kategoriye ait olabilir. Dataset'in `category` field'ı tek bir kategori ID'si içerir.

### Kategori silinebilir mi?

Evet, kategori silinebilir. Ancak o kategoriye ait dataset'ler varsa, bu dataset'lerin `category` field'ı boş kalır veya başka bir kategoriye atanması gerekir.

## İlgili Linkler
- [Dataset Oluşturma](../datasets/creating-dataset.md)
- [Dataset Field Types](../datasets/index.md)
- [Automated Forms](../automated-forms/automated-forms.md)

---

**Son Güncelleme:** 16 Ocak 2026
