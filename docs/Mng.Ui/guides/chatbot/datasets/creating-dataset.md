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
Bu rehber, MonitraNG platformunda yeni bir dataset oluşturmayı adım adım açıklar. Dataset oluşturduktan sonra field'lar ekleyebilir, validasyon kuralları tanımlayabilir ve veri yönetimi yapabilirsiniz.

## Önkoşullar
- Manager veya Admin yetkisi
- Dataset Management sayfasına erişim

## Adımlar

### 1. Dataset Management Sayfasına Git
**Route:** `/apps/datasets`

**Yöntem:**
1. Sol menüden **"Datasets"** menü öğesine tıklayın
2. Veya doğrudan `/apps/datasets` adresine gidin

**Beklenen Sonuç:** Dataset listesi sayfası açılır ve mevcut dataset'ler görüntülenir.

### 2. Yeni Dataset Oluştur
**Route:** `/apps/datasets/create`

**Action:** Sol üst köşedeki **"Yeni Dataset"** butonuna tıklayın

**Beklenen Sonuç:** Dataset oluşturma formu açılır.

### 3. Temel Bilgileri Doldur
**Form Fields:**
- **Dataset Adı:** `@books` (örn: `@` ile başlamalı)
  - Dataset adı benzersiz olmalıdır
  - `@` sembolü sistem dataset'lerini işaretler
- **Kategori:** Bir kategori seçin veya yeni oluşturun
  - Kategoriler dataset'leri organize etmek için kullanılır
- **Açıklama:** Dataset'in amacını açıklayın (opsiyonel)
  - Açıklama, dataset'in ne için kullanıldığını belirtir

**Beklenen Sonuç:** Form doldurulur ve bir sonraki adıma geçilebilir.

### 4. Field'ları Ekle
**Action:** "Field Ekle" butonuna tıklayın

**Field Types:**
- `text` - Metin alanı (string)
- `number` - Sayı alanı (integer, decimal)
- `boolean` - Evet/Hayır (true/false)
- `datetime` - Tarih/Saat
- `relation` - İlişkili dataset referansı
- `persons` - Kullanıcı referansı
- `personGroups` - Kullanıcı grubu referansı
- `object` - İç içe obje
- `incremental` - Otomatik artan sayı

**Örnek Field:**
```json
{
  "name": "title",
  "type": "text",
  "required": true,
  "label": "Kitap Adı",
  "description": "Kitabın başlığı"
}
```

**Beklenen Sonuç:** Field'lar eklendi ve form'da görüntülenir.

### 5. Kaydet
**Action:** Form'un altındaki **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** 
- Dataset başarıyla oluşturulur
- `/apps/datasets` listesinde görünür
- Dataset'e veri eklemeye başlayabilirsiniz

## İlgili Linkler
- [Dataset Yönetimi](/apps/datasets)
- [Field Types Dokümantasyonu](/docs/datasets/field-types)
- [Validation Rules](/docs/datasets/validation)
- [Automated Forms](/apps/automated-forms)

## Sık Sorulan Sorular

**S: Dataset adı neden @ ile başlamalı?**  
C: `@` sembolü sistem dataset'lerini işaretler ve özel işlevsellik sağlar. Örneğin, `@books` dataset'i sistem tarafından özel olarak işlenebilir.

**S: Kaç field ekleyebilirim?**  
C: Sınırsız field ekleyebilirsiniz, ancak performans için 50'den fazla field önerilmez. Çok fazla field varsa, object type kullanarak nested yapı oluşturabilirsiniz.

**S: Field'ları sonradan değiştirebilir miyim?**  
C: Evet, dataset'i düzenleyerek field'ları ekleyebilir, değiştirebilir veya silebilirsiniz. Ancak mevcut verilerle uyumlu olmasına dikkat edin.

**S: Dataset'i silebilir miyim?**  
C: Evet, ancak dataset içinde veri varsa önce verileri silmeniz gerekebilir. Dikkatli olun, silme işlemi geri alınamaz.

## Sorun Giderme

**Problem:** "Dataset adı zaten kullanılıyor" hatası  
**Çözüm:** 
- Farklı bir dataset adı kullanın
- Veya mevcut dataset'i düzenleyin
- Dataset adının benzersiz olduğundan emin olun

**Problem:** Field eklerken hata alıyorum  
**Çözüm:** 
- Field tipinin doğru olduğundan emin olun
- Gerekli alanların (name, type) doldurulduğundan emin olun
- Field adının geçerli karakterler içerdiğinden emin olun (alfanumerik, underscore, dash)

**Problem:** Dataset kaydedilmiyor  
**Çözüm:**
- Form validasyon hatalarını kontrol edin
- Gerekli alanların doldurulduğundan emin olun
- Yetkinizin olduğundan emin olun (Manager veya Admin)

---

**Son Güncelleme:** 16 Ocak 2026
