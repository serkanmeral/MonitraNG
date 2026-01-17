---
title: "Dataset Categories (Dataset Kategorileri)"
category: "ui-guides"
tags: ["dataset-categories", "categories", "organization", "management"]
service: "Mng.Ui"
route: "/apps/dataset-categories"
difficulty: "beginner"
estimated_time: "8 dakika"
language: "tr"
priority: 1
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

## İlgili Linkler
- [Dataset Oluşturma](../datasets/creating-dataset.md)
- [Automated Forms](../automated-forms/automated-forms.md)

---

**Son Güncelleme:** 16 Ocak 2026
