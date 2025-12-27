# GitHub + GitLab Dual Sync Yapılandırması

**Durum:** ✅ Yapılandırıldı ve aktif  
**Tarih:** 27 Aralık 2024

---

## 🎯 Yapılandırma Özeti

Origin remote'u hem GitHub hem GitLab'a push yapacak şekilde yapılandırılmıştır. Artık `git push origin <branch>` komutu otomatik olarak her iki repository'ye de push yapar.

---

## 📋 Yapılandırma Detayları

### Remote Yapılandırması

```bash
# Fetch URL (GitHub)
origin  https://github.com/serkanmeral/MonitraNG.git (fetch)

# Push URL'leri (GitHub + GitLab)
origin  https://github.com/serkanmeral/MonitraNG.git (push)
origin  http://root:TOKEN@localhost/root/MonitraNG.git (push)
```

### Nasıl Çalışır?

Git'in `--add --push` özelliği sayesinde, origin remote'una birden fazla push URL eklenebilir. `git push origin main` komutu çalıştırıldığında, Git her iki URL'ye de push yapar.

---

## 🚀 Kullanım

### Normal Push (Her İkisine Push)

```powershell
# Tek komutla hem GitHub hem GitLab'a push
git push origin main

# Tüm branch'leri push et
git push origin --all

# Tag'leri push et
git push origin --tags
```

### Sadece GitHub'a Push

```powershell
# GitHub remote'u ayrı olarak tanımlı (origin)
git push origin main
# Ancak yukarıdaki yapılandırma nedeniyle GitLab'a da push edilir
# Sadece GitHub'a push için:
git push https://github.com/serkanmeral/MonitraNG.git main
```

### Sadece GitLab'a Push

```powershell
# GitLab remote'u ayrı olarak tanımlı (gitlab)
git push gitlab main
```

---

## 🔧 Yeni Ortamda Kurulum

Eğer yeni bir ortamda (yeni bilgisayar, yeni clone) çalışıyorsanız, dual sync yapılandırmasını yeniden yapın:

### Adım 1: GitLab Personal Access Token Alın

1. GitLab'da: **User Settings > Access Tokens**
2. Yeni token oluşturun: `write_repository` scope ile
3. Token'ı kopyalayın

### Adım 2: Dual Sync Yapılandırması

```powershell
# Mevcut remote'ları kontrol et
git remote -v

# Origin remote'unu multiple push için yapılandır
git remote set-url --add --push origin https://github.com/serkanmeral/MonitraNG.git
git remote set-url --add --push origin http://root:YOUR_TOKEN@localhost/root/MonitraNG.git

# Yapılandırmayı kontrol et
git remote -v
```

---

## 🔍 Yapılandırmayı Kontrol Etme

```powershell
# Remote'ları listele
git remote -v

# Origin remote detaylarını göster
git remote show origin
```

**Beklenen Çıktı:**
```
origin  https://github.com/serkanmeral/MonitraNG.git (fetch)
origin  https://github.com/serkanmeral/MonitraNG.git (push)
origin  http://root:TOKEN@localhost/root/MonitraNG.git (push)
```

---

## ⚠️ Güvenlik Notları

1. **Token Güvenliği**: GitLab Personal Access Token remote URL'de saklanmıştır. Bu token'ı:
   - Public repository'lere commit etmeyin
   - `.git/config` dosyasını paylaşmayın
   - Token'ı düzenli olarak yenileyin

2. **Token Yenileme**: Token yenilendiğinde remote URL'i güncelleyin:
   ```powershell
   git remote set-url --add --push origin http://root:NEW_TOKEN@localhost/root/MonitraNG.git
   ```

3. **Alternatif**: Token'ı environment variable olarak saklayabilirsiniz (ileride).

---

## 🆘 Sorun Giderme

### Push Sadece Birine Gidiyor

**Sorun:** Push sadece GitHub'a veya sadece GitLab'a gidiyor.

**Çözüm:**
```powershell
# Remote yapılandırmasını kontrol et
git remote -v

# Eğer sadece bir push URL varsa, diğerini ekle
git remote set-url --add --push origin <MISSING_URL>
```

### Authentication Hatası

**Sorun:** GitLab'a push yaparken authentication hatası.

**Çözüm:**
1. GitLab Personal Access Token'ın geçerli olduğundan emin olun
2. Token'ın `write_repository` scope'una sahip olduğundan emin olun
3. Remote URL'i güncelleyin:
   ```powershell
   git remote set-url --add --push origin http://root:NEW_TOKEN@localhost/root/MonitraNG.git
   ```

### Push Çok Yavaş

**Sorun:** Push işlemi çok uzun sürüyor.

**Açıklama:** Normal, çünkü iki farklı remote'a push yapılıyor. Her push için iki ayrı network isteği yapılır.

---

## 📚 İlgili Dokümantasyon

- [GitLab Setup Guide](GITLAB_SETUP_GUIDE.md)
- [GitLab Push Instructions](GITLAB_PUSH_INSTRUCTIONS.md)
- [GitLab Next Steps](GITLAB_NEXT_STEPS.md)

---

**Son Güncelleme:** 27 Aralık 2024

