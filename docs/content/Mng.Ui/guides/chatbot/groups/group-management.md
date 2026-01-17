---
title: "Grup Yönetimi"
category: "ui-guides"
tags: ["groups", "management", "crud", "permissions"]
service: "Mng.Ui"
route: "/apps/groups"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Grup Listesi Sayfasına Git"
    route: "/apps/groups"
    action: "Sol menüden 'Group Management' menü öğesine tıklayın"
    expected_result: "Grup listesi sayfası açılır"
  - order: 2
    title: "Yeni Grup Oluştur"
    route: "/apps/groups/create"
    action: "'Yeni Grup' butonuna tıklayın ve formu doldurun"
    expected_result: "Yeni grup oluşturulur"
  - order: 3
    title: "Grup Düzenle"
    route: "/apps/groups/edit/[id]"
    action: "Grup listesinde 'Düzenle' butonuna tıklayın"
    expected_result: "Grup bilgileri güncellenir"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "Group Management sayfasına erişim"
related_guides:
  - "User Management"
  - "Domain Management"
---

# Grup Yönetimi

## Özet
Grup yönetimi sayfası ile platform gruplarını görüntüleyebilir, oluşturabilir, düzenleyebilir ve silebilirsiniz.

## Önkoşullar
- Manager veya Admin yetkisi
- Group Management sayfasına erişim izni

## Özellikler
- ✅ Grup listesi (server-side pagination)
- ✅ Grup arama ve filtreleme
- ✅ Yeni grup oluşturma
- ✅ Grup düzenleme
- ✅ Grup detay görüntüleme
- ✅ Grup silme
- ✅ Aktif/Pasif durumu yönetimi
- ✅ Üye yönetimi

## Adımlar

### 1. Grup Listesi Sayfasına Git
**Route:** `/apps/groups`

**Yöntem:**
1. Sol menüden **"Group Management"** menü öğesine tıklayın
2. Grup listesi sayfası açılır

**Beklenen Sonuç:** Grup listesi tablosu görüntülenir

### 2. Yeni Grup Oluştur
**Route:** `/apps/groups/create`

**Yöntem:**
1. Grup listesi sayfasında **"Yeni Grup"** butonuna tıklayın
2. Form alanlarını doldurun:
   - **Grup Adı** (zorunlu)
   - **Açıklama** (opsiyonel)
   - **Aktif Durumu** (varsayılan: aktif)
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Yeni grup oluşturulur ve grup listesine yönlendirilirsiniz

### 3. Grup Düzenle
**Route:** `/apps/groups/edit/[id]`

**Yöntem:**
1. Grup listesinde düzenlemek istediğiniz grubun **"Düzenle"** butonuna tıklayın
2. Form alanlarını güncelleyin
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Grup bilgileri güncellenir

### 4. Grup Detayları Görüntüle
**Route:** `/apps/groups/details/[id]`

**Yöntem:**
1. Grup listesinde görüntülemek istediğiniz grubun **"Görüntüle"** butonuna tıklayın
2. Grup detay sayfası açılır

**Beklenen Sonuç:** Grup bilgileri ve üyeleri görüntülenir

### 5. Grup Silme
**Route:** `/apps/groups`

**Yöntem:**
1. Grup listesinde silmek istediğiniz grubun **"Sil"** butonuna tıklayın
2. Onay dialog'unda **"Sil"** butonuna tıklayın

**Beklenen Sonuç:** Grup silinir ve listeden kaldırılır

**Dikkat:** Silme işlemi geri alınamaz!

## Form Alanları

### Zorunlu Alanlar
- **Grup Adı:** Grubun adı (benzersiz)

### Opsiyonel Alanlar
- **Açıklama:** Grubun açıklaması

### Durum Alanları
- **Aktif Durumu:** Grubun aktif/pasif durumu

## İlgili Linkler
- [User Management](../users/user-management.md)
- [Domain Management](../domain/domain-management.md)

---

**Son Güncelleme:** 16 Ocak 2026
