# GitLab Namespace/Group Oluşturma Rehberi

**Sorun:** "Pick a group or namespace where you want to create this project" hatası

---

## 🎯 Çözüm: Namespace/Group Oluşturma

GitLab'da proje oluşturmadan önce bir namespace (isim alanı) veya group oluşturmanız gerekiyor.

---

## 📋 Adım Adım Çözüm

### Yöntem 1: Web UI ile Namespace Oluşturma (Önerilen)

1. **GitLab Ana Sayfası:**
   - Tarayıcıda `http://localhost` açın
   - Sol menüden **"Groups"** veya sağ üst köşedeki **"+"** butonuna tıklayın

2. **Yeni Group Oluştur:**
   - **"New group"** seçeneğine tıklayın
   - Group bilgilerini doldurun:
     - **Group name:** `monitrang` (veya istediğiniz isim)
     - **Group URL:** `monitrang` (otomatik doldurulur)
     - **Visibility Level:**
       - `Private` - Sadece grup üyeleri görebilir (önerilen)
       - `Internal` - Tüm logged-in kullanıcılar görebilir
       - `Public` - Herkes görebilir
   - **"Create group"** butonuna tıklayın

3. **Proje Oluştur:**
   - Group oluşturulduktan sonra, group sayfasında **"New project"** butonuna tıklayın
   - Artık namespace seçilmiş olacak, direkt proje bilgilerini girebilirsiniz

---

### Yöntem 2: Root Namespace'i Kullanma

Eğer grup oluşturmak istemiyorsanız, root kullanıcısının namespace'ini kullanabilirsiniz:

1. **Proje Oluşturma Sayfasında:**
   - "Pick a group or namespace" alanına `root` yazın
   - Dropdown'dan `root` seçeneğini seçin
   - Proje adını girin: `MonitraNG`
   - "Create project" butonuna tıklayın

**Not:** Root namespace kullanırsanız, proje URL'i şöyle olur: `http://localhost/root/monitrang`

---

### Yöntem 3: Komut Satırı ile Group Oluşturma (İleri Seviye)

GitLab API kullanarak group oluşturabilirsiniz:

```bash
# 1. Personal Access Token oluşturun
# GitLab > User Settings > Access Tokens
# Scope: api seçin

# 2. Group oluşturun
curl -X POST "http://localhost/api/v4/groups" \
  -H "PRIVATE-TOKEN: YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "MonitraNG",
    "path": "monitrang",
    "visibility": "private"
  }'
```

---

## 🎯 Önerilen Yaklaşım

**Group Oluşturma (Önerilen):**
- ✅ Daha organize
- ✅ İleride birden fazla proje ekleyebilirsiniz
- ✅ Grup bazlı permission yönetimi
- ✅ Proje URL'i: `http://localhost/monitrang/monitrang`

**Root Namespace:**
- ✅ Hızlı ve basit
- ⚠️ Sadece root altında projeler
- ⚠️ URL: `http://localhost/root/monitrang`

---

## 📝 Proje Oluşturma (Group Oluşturduktan Sonra)

Group oluşturulduktan sonra:

1. Group sayfasına gidin
2. **"New project"** butonuna tıklayın
3. **"Create blank project"** seçin
4. Proje bilgilerini doldurun:
   - **Project name:** `MonitraNG` (veya farklı bir isim)
   - **Project slug:** Otomatik doldurulur
   - **Visibility:** Group visibility'yi devralır veya override edebilirsiniz
   - ❌ **"Initialize repository with a README"** kutusunu işaretlemeyin
5. **"Create project"** butonuna tıklayın

---

## 🔍 Mevcut Namespace'leri Kontrol Etme

GitLab'da mevcut namespace'leri görmek için:

1. Sol menüden **"Groups"** sekmesine gidin
2. Veya URL'den: `http://localhost/groups`
3. Tüm gruplar ve root namespace görünecektir

---

## 🆘 Sorun Giderme

### "Pick a group or namespace" alanı boş görünüyor

**Çözüm:**
- Sayfayı yenileyin (F5)
- Bir namespace oluşturun (yukarıdaki adımları takip edin)
- Root kullanıcısı ile giriş yaptığınızdan emin olun

### Namespace bulunamıyor

**Çözüm:**
- Group oluşturmayı deneyin
- Root namespace'i kullanmayı deneyin (`root` yazın)
- GitLab'ı yeniden başlatın (gerekirse):
  ```bash
  docker restart gitlab
  ```

---

**Son Güncelleme:** 27 Aralık 2024

