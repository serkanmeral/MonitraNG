---
title: "Automated Forms (Otomatik Formlar)"
category: "ui-guides"
tags: ["automated-forms", "forms", "dataset", "dynamic-forms"]
service: "Mng.Ui"
route: "/apps/automated-forms"
difficulty: "intermediate"
estimated_time: "15 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Automated Forms Listesi Sayfasına Git"
    route: "/apps/automated-forms"
    action: "Sol menüden 'Automated Forms' menü öğesine tıklayın"
    expected_result: "Form listesi sayfası açılır"
  - order: 2
    title: "Yeni Form Oluştur"
    route: "/apps/automated-forms/create"
    action: "'Yeni Form' butonuna tıklayın ve formu doldurun"
    expected_result: "Yeni form oluşturulur"
  - order: 3
    title: "Form Görüntüle ve Kullan"
    route: "/apps/automated-forms/view/[formCode]"
    action: "Form listesinde 'Aç' butonuna tıklayın"
    expected_result: "Form görüntülenir ve kullanılabilir"
prerequisites:
  - "Manager veya Admin yetkisi (form oluşturma için)"
  - "Dataset oluşturulmuş olmalı"
related_guides:
  - "Dataset Oluşturma"
  - "Dataset Categories"
faq:
  - question: "Form oluştururken hangi dataset'i seçmeliyim?"
    answer: "Form oluşturmak istediğiniz veri yapısına uygun dataset'i seçin. Dataset'in field'ları form alanlarına otomatik dönüşür."
  - question: "Form oluşturduktan sonra field'ları değiştirebilir miyim?"
    answer: "Form field'ları dataset schema'sına bağlıdır. Field'ları değiştirmek için dataset'i düzenlemeniz gerekir."
  - question: "Form'u side menu'de nasıl gösteririm?"
    answer: "Form oluştururken veya düzenlerken 'Side Menu Config' bölümünden 'Enabled' seçeneğini işaretleyin."
troubleshooting:
  - problem: "Form oluştururken dataset listesi boş"
    solution: "Önce bir dataset oluşturmanız gerekir. Dataset Management sayfasından dataset oluşturun."
  - problem: "Form görüntülenirken veri kaydedilemiyor"
    solution: "Dataset'in field validasyon kurallarını kontrol edin. Form alanları validasyon kurallarına uygun olmalıdır."
  - problem: "Form'da field'lar görünmüyor"
    solution: "Dataset'in field'larının doğru tanımlandığından emin olun. Dataset'i düzenleyip field'ları kontrol edin."
---

# Automated Forms (Otomatik Formlar)

## Özet
Automated Forms, dataset'lerden otomatik olarak dinamik formlar oluşturmanıza olanak sağlar. Dataset schema'sına göre form alanları otomatik oluşturulur.

## Önkoşullar
- Manager veya Admin yetkisi (form oluşturma için)
- Dataset oluşturulmuş olmalı
- Automated Forms sayfasına erişim izni

## Özellikler
- ✅ Dataset'ten otomatik form oluşturma
- ✅ Dinamik form alanları (field type'a göre)
- ✅ Form listesi ve arama
- ✅ Form düzenleme
- ✅ Form görüntüleme ve kullanma
- ✅ Veri CRUD işlemleri (form üzerinden)
- ✅ Side menu entegrasyonu

## Adımlar

### 1. Automated Forms Listesi Sayfasına Git
**Route:** `/apps/automated-forms`

**Yöntem:**
1. Sol menüden **"Automated Forms"** menü öğesine tıklayın
2. Form listesi sayfası açılır

**Beklenen Sonuç:** Form listesi tablosu görüntülenir

### 2. Yeni Form Oluştur
**Route:** `/apps/automated-forms/create`

**Yöntem:**
1. Form listesi sayfasında **"Yeni Form"** butonuna tıklayın
2. Form alanlarını doldurun:
   - **Form Code** (zorunlu, benzersiz)
   - **Form Title** (zorunlu)
   - **Dataset Seçimi** (zorunlu, mevcut dataset'lerden)
   - **Açıklama** (opsiyonel)
   - **Side Menu Config** (opsiyonel)
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Yeni form oluşturulur ve form listesine yönlendirilirsiniz

**Not:** Form oluşturulduktan sonra, seçilen dataset'in field'larına göre dinamik form alanları otomatik oluşturulur.

### 3. Form Görüntüle ve Kullan
**Route:** `/apps/automated-forms/view/[formCode]`

**Yöntem:**
1. Form listesinde görüntülemek istediğiniz formun **"Aç"** butonuna tıklayın
2. Form görüntüleme sayfası açılır

**Beklenen Sonuç:** Form görüntülenir ve kullanılabilir

**Özellikler:**
- Veri listesi görüntüleme
- Yeni veri ekleme (form dialog)
- Veri düzenleme (form dialog)
- Veri silme
- Arama ve filtreleme
- Pagination

### 4. Form Düzenle
**Route:** `/apps/automated-forms/edit/[formCode]`

**Yöntem:**
1. Form listesinde düzenlemek istediğiniz formun **"Düzenle"** butonuna tıklayın
2. Form bilgilerini güncelleyin
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Form bilgileri güncellenir

### 5. Form Silme
**Route:** `/apps/automated-forms`

**Yöntem:**
1. Form listesinde silmek istediğiniz formun **"Sil"** butonuna tıklayın
2. Onay dialog'unda **"Sil"** butonuna tıklayın

**Beklenen Sonuç:** Form silinir ve listeden kaldırılır

**Dikkat:** Silme işlemi geri alınamaz!

## Form Alanları

### Zorunlu Alanlar
- **Form Code:** Form kodu (benzersiz, URL'de kullanılır)
- **Form Title:** Form başlığı
- **Dataset:** Form'un bağlı olduğu dataset

### Opsiyonel Alanlar
- **Açıklama:** Form açıklaması
- **Side Menu Config:** Side menu'de görünmesi için yapılandırma

## Dinamik Form Alanları

Form, dataset'in field'larına göre otomatik oluşturulur:
- **Text Field:** Text input
- **Number Field:** Number input
- **Bool Field:** Checkbox
- **DateTime Field:** Date picker
- **Relation Field:** Select (dataset referansı)
- **Persons Field:** User selector
- **PersonGroups Field:** Group selector
- **Incremental Field:** Otomatik oluşturulur (gösterilmez)

## İlgili Linkler
- [Dataset Oluşturma](../datasets/creating-dataset.md)
- [Dataset Categories](../dataset-categories/dataset-categories.md)

---

**Son Güncelleme:** 16 Ocak 2026
