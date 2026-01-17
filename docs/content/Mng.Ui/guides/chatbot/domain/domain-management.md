---
title: "Domain Yönetimi"
category: "ui-guides"
tags: ["domain", "management", "multi-tenant", "settings"]
service: "Mng.Ui"
route: "/apps/domain"
difficulty: "beginner"
estimated_time: "5 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Domain Yönetimi Sayfasına Git"
    route: "/apps/domain"
    action: "Sol menüden 'Domain' menü öğesine tıklayın"
    expected_result: "Domain yönetimi sayfası açılır"
  - order: 2
    title: "Domain Bilgilerini Görüntüle"
    route: "/apps/domain"
    action: "Mevcut domain bilgileri görüntülenir"
    expected_result: "Domain bilgileri görüntülenir"
  - order: 3
    title: "Domain Bilgilerini Düzenle"
    route: "/apps/domain"
    action: "'Düzenle' butonuna tıklayın ve bilgileri güncelleyin"
    expected_result: "Domain bilgileri güncellenir"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Domain yönetimi sayfasına erişim"
related_guides:
  - "User Management"
  - "Group Management"
---

# Domain Yönetimi

## Özet
Domain yönetimi sayfası ile mevcut domain'in bilgilerini görüntüleyebilir ve düzenleyebilirsiniz.

## Önkoşullar
- Manager veya Admin yetkisi
- Domain yönetimi sayfasına erişim izni

## Özellikler
- ✅ Mevcut domain bilgilerini görüntüleme
- ✅ Domain bilgilerini düzenleme
- ✅ Domain ayarları yönetimi

## Adımlar

### 1. Domain Yönetimi Sayfasına Git
**Route:** `/apps/domain`

**Yöntem:**
1. Sol menüden **"Domain"** menü öğesine tıklayın
2. Domain yönetimi sayfası açılır

**Beklenen Sonuç:** Mevcut domain bilgileri görüntülenir

### 2. Domain Bilgilerini Düzenle
**Route:** `/apps/domain`

**Yöntem:**
1. **"Düzenle"** butonuna tıklayın
2. Form alanlarını güncelleyin:
   - **Display Name** (Görünen Ad)
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Domain bilgileri güncellenir

## Form Alanları

### Düzenlenebilir Alanlar
- **Display Name:** Domain'in görünen adı

### Sadece Görüntülenen Alanlar
- **Domain Name:** Domain adı (değiştirilemez)
- **Domain ID:** Domain ID (değiştirilemez)

## İlgili Linkler
- [User Management](../users/user-management.md)
- [Group Management](../groups/group-management.md)

---

**Son Güncelleme:** 16 Ocak 2026
