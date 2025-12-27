# GitLab'a Push Yapma Rehberi

**Durum:** GitLab remote eklendi, authentication gerekiyor

---

## 🔐 Authentication Seçenekleri

GitLab'a push yapmak için authentication gerekiyor. İki yöntem var:

### Yöntem 1: Personal Access Token (Önerilen - Güvenli)

#### Adım 1: Personal Access Token Oluşturun

1. GitLab'da sağ üst köşedeki profil ikonuna tıklayın
2. **"Edit profile"** veya **"Preferences"** seçin
3. Sol menüden **"Access Tokens"** seçin
4. Token bilgilerini doldurun:
   - **Token name:** `monitrang-push-token` (veya istediğiniz isim)
   - **Expiration date:** (isteğe bağlı - boş bırakabilirsiniz)
   - **Scopes:** 
     - ✅ `write_repository` (mutlaka seçin)
     - ✅ `read_repository` (opsiyonel)
5. **"Create personal access token"** butonuna tıklayın
6. **ÖNEMLİ:** Token'ı kopyalayın (bir daha gösterilmeyecek!)

#### Adım 2: Token ile Push Yapın

```powershell
# Token'ı bir değişkene kaydedin
$token = Read-Host "Personal Access Token'ınızı girin"

# GitLab URL'ini token ile güncelleyin
git remote set-url gitlab http://root:$token@localhost/root/MonitraNG.git

# Push yapın
git push -u gitlab main
```

Veya tek komutla:

```powershell
git push http://root:YOUR_TOKEN@localhost/root/MonitraNG.git main
```

---

### Yöntem 2: Root Şifresi ile (Basit ama Güvenli Değil)

```powershell
# Root şifresi ile push (önerme - güvenli değil)
git push http://root:wj+JGsy/xaGSCBKIX+WimW1A+G+zGz5KSd2b1GTUMAk=@localhost/root/MonitraNG.git main
```

⚠️ **Not:** Şifre URL'de görünecek, bu yüzden önerilmez.

---

### Yöntem 3: SSH ile (En Güvenli - İleride)

SSH key oluşturup GitLab'a ekleyerek SSH ile push yapabilirsiniz:

```powershell
# SSH key oluştur (eğer yoksa)
ssh-keygen -t ed25519 -C "your_email@example.com"

# Public key'i göster
cat ~/.ssh/id_ed25519.pub
```

GitLab'da:
1. **User Settings > SSH Keys**
2. Public key'i yapıştırın
3. Remote URL'ini SSH'a çevirin:
   ```powershell
   git remote set-url gitlab ssh://git@localhost:2222/root/MonitraNG.git
   ```

---

## 🚀 Hızlı Push (Personal Access Token ile)

1. **Personal Access Token oluşturun** (yukarıdaki adımlar)
2. **Push komutunu çalıştırın:**

```powershell
# Token'ı girin (güvenli olması için Read-Host kullanın)
$token = Read-Host "GitLab Personal Access Token:"

# Remote URL'ini token ile güncelleyin
git remote set-url gitlab http://root:$token@localhost/root/MonitraNG.git

# Push yapın
git push -u gitlab main
```

---

## 📋 Tüm Branch'leri Push Etme

Sadece main değil, tüm branch'leri push etmek için:

```powershell
# Tüm branch'leri push et
git push -u gitlab --all

# Tag'leri de push et (varsa)
git push -u gitlab --tags
```

---

## 🔄 GitHub + GitLab Dual Push

Her push'ta hem GitHub hem GitLab'a push etmek için:

### Yöntem 1: Her seferinde iki komut

```powershell
git push origin main
git push gitlab main
```

### Yöntem 2: Tek komutla (Git config ile)

```powershell
# Origin'i multiple push için yapılandır
git remote set-url --add --push origin https://github.com/serkanmeral/MonitraNG.git
git remote set-url --add --push origin http://root:TOKEN@localhost/root/MonitraNG.git

# Artık tek komutla her ikisine push
git push origin main
```

---

## 🆘 Sorun Giderme

### "Authentication failed" hatası

**Çözüm:**
- Personal Access Token'ın `write_repository` scope'una sahip olduğundan emin olun
- Token'ı doğru yazdığınızdan emin olun
- URL'deki token'ı kontrol edin

### "Remote URL already exists" hatası

**Çözüm:**
```powershell
# Mevcut remote'u güncelle
git remote set-url gitlab http://root:TOKEN@localhost/root/MonitraNG.git
```

### "Repository not found" hatası

**Çözüm:**
- GitLab proje URL'inin doğru olduğundan emin olun
- Proje adının doğru olduğundan emin olun (büyük/küçük harf duyarlı)

---

**Son Güncelleme:** 27 Aralık 2024

