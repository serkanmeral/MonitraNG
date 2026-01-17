---
title: "Side Menu Manager (Yan Menü Yöneticisi)"
category: "ui-guides"
tags: ["side-menu", "menu", "navigation", "management"]
service: "Mng.Ui"
route: "/apps/side-menu-manager"
difficulty: "intermediate"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Side Menu Manager Sayfasına Git"
    route: "/apps/side-menu-manager"
    action: "Sol menüden 'Side Menu Manager' menü öğesine tıklayın"
    expected_result: "Side Menu Manager sayfası açılır"
  - order: 2
    title: "Menü Öğesi Ekle"
    route: "/apps/side-menu-manager"
    action: "'Yeni Menü Öğesi' butonuna tıklayın ve formu doldurun"
    expected_result: "Yeni menü öğesi eklenir"
  - order: 3
    title: "Menü Öğesi Düzenle"
    route: "/apps/side-menu-manager"
    action: "Menü öğesinin 'Düzenle' butonuna tıklayın"
    expected_result: "Menü öğesi güncellenir"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Side Menu Manager sayfasına erişim"
related_guides:
  - "Automated Forms"
---

# Side Menu Manager (Yan Menü Yöneticisi)

## Özet
Side Menu Manager ile sol menüdeki menü öğelerini yönetebilirsiniz. Menü öğeleri MongoDB'de `@side_menu` dataset'inde saklanır.

## Önkoşullar
- Manager veya Admin yetkisi
- Side Menu Manager sayfasına erişim izni

## Özellikler
- ✅ Menü öğesi listesi
- ✅ Yeni menü öğesi ekleme
- ✅ Menü öğesi düzenleme
- ✅ Menü öğesi silme
- ✅ Menü hiyerarşisi yönetimi
- ✅ Permission bazlı görünürlük
- ✅ Page type bazlı filtreleme

## Adımlar

### 1. Side Menu Manager Sayfasına Git
**Route:** `/apps/side-menu-manager`

**Yöntem:**
1. Sol menüden **"Side Menu Manager"** menü öğesine tıklayın
2. Side Menu Manager sayfası açılır

**Beklenen Sonuç:** Menü öğeleri listesi görüntülenir

### 2. Yeni Menü Öğesi Ekle
**Route:** `/apps/side-menu-manager`

**Yöntem:**
1. **"Yeni Menü Öğesi"** butonuna tıklayın
2. Form alanlarını doldurun:
   - **Title** (zorunlu)
   - **Route** (zorunlu)
   - **Icon** (opsiyonel)
   - **Page Type** (admin/manager/user)
   - **Permission** (opsiyonel)
   - **Parent Menu** (opsiyonel, alt menü için)
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Yeni menü öğesi eklenir ve menüde görünür

### 3. Menü Öğesi Düzenle
**Route:** `/apps/side-menu-manager`

**Yöntem:**
1. Düzenlemek istediğiniz menü öğesinin **"Düzenle"** butonuna tıklayın
2. Form alanlarını güncelleyin
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Menü öğesi güncellenir

## Menü Öğesi Yapısı

### Zorunlu Alanlar
- **Title:** Menü öğesinin başlığı
- **Route:** Sayfa route'u (örn: `/apps/users`)

### Opsiyonel Alanlar
- **Icon:** Menü ikonu
- **Page Type:** Sayfa tipi (admin/manager/user)
- **Permission:** Görünürlük için gerekli yetki
- **Parent Menu:** Üst menü öğesi (alt menü için)

## Permission Yönetimi

Menü öğeleri permission bazlı görünürlük kontrolü yapar:
- **Admin:** Tüm menü öğelerini görür
- **Manager:** `pageType: 'manager'` ve `pageType: 'user'` olanları görür
- **User:** Sadece `pageType: 'user'` ve yetkisi olanları görür

## İlgili Linkler
- [Automated Forms](../automated-forms/automated-forms.md)

---

**Son Güncelleme:** 16 Ocak 2026
