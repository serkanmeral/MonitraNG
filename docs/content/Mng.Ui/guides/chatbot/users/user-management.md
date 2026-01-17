---
title: "Kullanıcı Yönetimi"
category: "ui-guides"
tags: ["users", "management", "crud", "authentication"]
service: "Mng.Ui"
route: "/apps/users"
difficulty: "beginner"
estimated_time: "10 dakika"
language: "tr"
priority: 1
steps:
  - order: 1
    title: "Kullanıcı Listesi Sayfasına Git"
    route: "/apps/users"
    action: "Sol menüden 'User Management' menü öğesine tıklayın"
    expected_result: "Kullanıcı listesi sayfası açılır"
  - order: 2
    title: "Yeni Kullanıcı Oluştur"
    route: "/apps/users/create"
    action: "'Yeni Kullanıcı' butonuna tıklayın ve formu doldurun"
    expected_result: "Yeni kullanıcı oluşturulur"
  - order: 3
    title: "Kullanıcı Düzenle"
    route: "/apps/users/edit/[id]"
    action: "Kullanıcı listesinde 'Düzenle' butonuna tıklayın"
    expected_result: "Kullanıcı bilgileri güncellenir"
  - order: 4
    title: "Kullanıcı Detayları"
    route: "/apps/users/details/[id]"
    action: "Kullanıcı listesinde 'Görüntüle' butonuna tıklayın"
    expected_result: "Kullanıcı detay sayfası açılır"
prerequisites:
  - "Manager veya Admin yetkisi"
  - "User Management sayfasına erişim"
related_guides:
  - "Group Management"
  - "Domain Management"
faq:
  - question: "Username değiştirilebilir mi?"
    answer: "Hayır, username oluşturulduktan sonra değiştirilemez. Sadece görüntülenir."
  - question: "Kullanıcı silindikten sonra geri alınabilir mi?"
    answer: "Hayır, silme işlemi geri alınamaz. Kullanıcıyı tekrar oluşturmanız gerekir."
  - question: "Bir kullanıcıyı birden fazla gruba atayabilir miyim?"
    answer: "Evet, kullanıcıyı birden fazla gruba atayabilirsiniz."
  - question: "Pasif kullanıcılar giriş yapabilir mi?"
    answer: "Hayır, pasif kullanıcılar sisteme giriş yapamaz."
troubleshooting:
  - problem: "Kullanıcı oluştururken 'Username zaten kullanılıyor' hatası"
    solution: "Farklı bir username kullanın veya mevcut kullanıcıyı düzenleyin"
  - problem: "Kullanıcı listesi yüklenmiyor"
    solution: "Sayfayı yenileyin, yetkinizi kontrol edin (Manager veya Admin olmalısınız)"
  - problem: "Kullanıcı düzenlenemiyor"
    solution: "Kullanıcının aktif olduğundan ve düzenleme yetkinizin olduğundan emin olun"
---

# Kullanıcı Yönetimi

## Özet
Kullanıcı yönetimi sayfası ile platform kullanıcılarını görüntüleyebilir, oluşturabilir, düzenleyebilir ve silebilirsiniz.

## Önkoşullar
- Manager veya Admin yetkisi
- User Management sayfasına erişim izni

## Özellikler
- ✅ Kullanıcı listesi (server-side pagination)
- ✅ Kullanıcı arama ve filtreleme
- ✅ Yeni kullanıcı oluşturma
- ✅ Kullanıcı düzenleme
- ✅ Kullanıcı detay görüntüleme
- ✅ Kullanıcı silme
- ✅ Grup atama
- ✅ Aktif/Pasif durumu yönetimi
- ✅ Export (CSV/Excel - planlanan)

## Adımlar

### 1. Kullanıcı Listesi Sayfasına Git
**Route:** `/apps/users`

**Yöntem:**
1. Sol menüden **"User Management"** menü öğesine tıklayın
2. Kullanıcı listesi sayfası açılır

**Beklenen Sonuç:** Kullanıcı listesi tablosu görüntülenir

### 2. Kullanıcı Arama ve Filtreleme
**Route:** `/apps/users`

**Yöntem:**
1. Arama kutusuna kullanıcı adı, e-posta veya isim yazın
2. Status filtresi ile aktif/pasif kullanıcıları filtreleyin
3. Sonuçlar otomatik olarak güncellenir

**Beklenen Sonuç:** Arama sonuçları görüntülenir

### 3. Yeni Kullanıcı Oluştur
**Route:** `/apps/users/create`

**Yöntem:**
1. Kullanıcı listesi sayfasında **"Yeni Kullanıcı"** butonuna tıklayın
2. Form alanlarını doldurun:
   - **Username** (zorunlu, min 3 karakter)
   - **E-posta** (zorunlu, geçerli format)
   - **Ad** (zorunlu)
   - **Soyad** (zorunlu)
   - **Grup Seçimi** (opsiyonel)
   - **Ünvan** (opsiyonel)
   - **Departman** (opsiyonel)
   - **Telefon** (opsiyonel)
   - **Aktif Durumu** (varsayılan: aktif)
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Yeni kullanıcı oluşturulur ve kullanıcı listesine yönlendirilirsiniz

### 4. Kullanıcı Düzenle
**Route:** `/apps/users/edit/[id]`

**Yöntem:**
1. Kullanıcı listesinde düzenlemek istediğiniz kullanıcının **"Düzenle"** butonuna tıklayın
2. Form alanlarını güncelleyin
3. **"Kaydet"** butonuna tıklayın

**Beklenen Sonuç:** Kullanıcı bilgileri güncellenir

**Not:** Username değiştirilemez (sadece görüntülenir)

### 5. Kullanıcı Detayları Görüntüle
**Route:** `/apps/users/details/[id]`

**Yöntem:**
1. Kullanıcı listesinde görüntülemek istediğiniz kullanıcının **"Görüntüle"** butonuna tıklayın
2. Kullanıcı detay sayfası açılır

**Beklenen Sonuç:** Kullanıcı bilgileri, grupları ve diğer detaylar görüntülenir

### 6. Kullanıcı Silme
**Route:** `/apps/users`

**Yöntem:**
1. Kullanıcı listesinde silmek istediğiniz kullanıcının **"Sil"** butonuna tıklayın
2. Onay dialog'unda **"Sil"** butonuna tıklayın

**Beklenen Sonuç:** Kullanıcı silinir ve listeden kaldırılır

**Dikkat:** Silme işlemi geri alınamaz!

## Form Alanları

### Zorunlu Alanlar
- **Username:** Kullanıcı adı (min 3 karakter, benzersiz)
- **E-posta:** E-posta adresi (geçerli format)
- **Ad:** Kullanıcının adı
- **Soyad:** Kullanıcının soyadı

### Opsiyonel Alanlar
- **Grup Seçimi:** Kullanıcının ait olduğu gruplar
- **Ünvan:** Kullanıcının ünvanı
- **Departman:** Kullanıcının departmanı
- **Telefon:** Telefon numarası
- **Fotoğraf URL:** Kullanıcı fotoğrafı (edit sayfasında)

### Durum Alanları
- **Aktif Durumu:** Kullanıcının aktif/pasif durumu

## Server-Side Pagination

Kullanıcı listesi server-side pagination kullanır:
- Sayfa başına kayıt sayısı: 10 (varsayılan)
- Sıralama: Tıklanabilir sütun başlıkları
- Sayfa navigasyonu: Alt kısımdaki sayfa numaraları

## Sık Sorulan Sorular

**S: Username değiştirilebilir mi?**  
C: Hayır, username oluşturulduktan sonra değiştirilemez. Sadece görüntülenir.

**S: Kullanıcı silindikten sonra geri alınabilir mi?**  
C: Hayır, silme işlemi geri alınamaz. Kullanıcıyı tekrar oluşturmanız gerekir.

**S: Bir kullanıcıyı birden fazla gruba atayabilir miyim?**  
C: Evet, kullanıcıyı birden fazla gruba atayabilirsiniz.

**S: Pasif kullanıcılar giriş yapabilir mi?**  
C: Hayır, pasif kullanıcılar sisteme giriş yapamaz.

## İlgili Linkler
- [Group Management](../groups/group-management.md)
- [Domain Management](../domain/domain-management.md)
- [Authentication](../authentication/login.md)

---

**Son Güncelleme:** 16 Ocak 2026
