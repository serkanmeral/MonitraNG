# GitLab Kurulum ve Proje Oluşturma Rehberi

**Tarih:** 27 Aralık 2024  
**Durum:** GitLab Docker container'ı çalışıyor

---

## 🔐 İlk Giriş Bilgileri

### Root Kullanıcı Bilgileri
- **URL:** `http://localhost` veya `http://gitlab.local`
- **Kullanıcı Adı:** `root`
- **Şifre:** `wj+JGsy/xaGSCBKIX+WimW1A+G+zGz5KSd2b1GTUMAk=`

> ⚠️ **Güvenlik Notu:** İlk girişten sonra şifreyi mutlaka değiştirin!

---

## 📋 Proje Oluşturma Adımları

### Adım 1: GitLab'a Giriş Yapın

1. Tarayıcınızda şu adreslere gidin:
   - `http://localhost`
   - Veya `http://gitlab.local` (hosts dosyasına eklediyseniz)

2. İlk giriş ekranında:
   - Kullanıcı adı: `root`
   - Şifre: Yukarıdaki şifreyi girin

3. Şifre değiştirme ekranı gelecek, güçlü bir şifre belirleyin.

### Adım 2: Yeni Proje Oluşturun

**Yöntem A: Web UI ile Oluşturma (Önerilen)**

1. GitLab ana sayfasında **"New project"** veya **"Create project"** butonuna tıklayın
2. **"Create blank project"** sekmesini seçin
3. Proje bilgilerini doldurun:
   - **Project name:** `MonitraNG`
   - **Project slug:** `monitrang` (otomatik doldurulur)
   - **Visibility Level:** 
     - `Private` (önerilen - sadece size görünür)
     - `Internal` (tüm kullanıcılar görebilir)
     - `Public` (herkes görebilir)
   - **Initialize repository with a README:** ❌ **İŞARETLEMEYİN** (çünkü zaten mevcut bir repo'yu push edeceğiz)
4. **"Create project"** butonuna tıklayın

**Yöntem B: Komut Satırı ile Oluşturma (Gelecekte)**

GitLab API ile de proje oluşturulabilir, şimdilik web UI kullanıyoruz.

### Adım 3: Mevcut Repository'yi GitLab'a Push Edin

Proje oluşturulduktan sonra GitLab size push komutlarını gösterecek. İki seçeneğiniz var:

#### Seçenek A: Mevcut Repository'yi GitLab'a Push (Önerilen)

```bash
# 1. Mevcut repository'de olduğunuzdan emin olun
cd C:\Serkan\iSIM\MonitraNG

# 2. GitLab remote'unu ekleyin (GitLab'dan alacağınız URL ile)
# GitLab proje sayfasında "Clone" butonuna tıklayın ve HTTP URL'ini kopyalayın
# Örnek: http://localhost/root/monitrang.git
git remote add gitlab http://localhost/root/monitrang.git

# Veya SSH kullanıyorsanız (port 2222):
git remote add gitlab ssh://git@localhost:2222/root/monitrang.git

# 3. Tüm branch'leri GitLab'a push edin
git push -u gitlab --all

# 4. Tag'leri de push edin (varsa)
git push -u gitlab --tags
```

#### Seçenek B: GitHub + GitLab Dual Repository (Önerilen Strateji)

Hem GitHub hem GitLab'a push yapmak için:

```bash
# 1. Mevcut remote'ları kontrol edin
git remote -v

# 2. GitLab remote'unu ekleyin
git remote add gitlab http://localhost/root/monitrang.git

# 3. Push işlemlerini her iki remote'a yapın
git push origin main          # GitHub'a
git push gitlab main          # GitLab'a

# Veya her ikisini birden push etmek için:
git remote set-url --add --push origin http://localhost/root/monitrang.git
git remote set-url --add --push origin https://github.com/serkanmeral/MonitraNG.git
```

---

## 🔄 GitHub + GitLab Sync Stratejisi

### Strateji 1: Manuel Push (Basit)

Her push'ta her iki remote'a da push yapın:

```bash
git push origin main
git push gitlab main
```

### Strateji 2: Git Push Multiple (Önerilen)

Bir komutla her iki remote'a push:

```bash
# Remote'ları multiple push için yapılandırın
git remote set-url --add --push origin https://github.com/serkanmeral/MonitraNG.git
git remote set-url --add --push origin http://localhost/root/monitrang.git

# Artık tek komutla her ikisine push edilir
git push origin main
```

### Strateji 3: GitLab Push Mirror (Otomatik - İleride)

GitLab Enterprise özelliği, Community Edition'da yok. Ancak CI/CD pipeline ile otomatik sync yapılabilir.

---

## 🔧 Hosts Dosyasına GitLab Erişimi Ekleme (Opsiyonel)

Windows'ta `gitlab.local` kullanmak için:

1. Notepad'i **Yönetici olarak çalıştırın**
2. Şu dosyayı açın: `C:\Windows\System32\drivers\etc\hosts`
3. Şu satırı ekleyin:
   ```
   127.0.0.1    gitlab.local
   ```
4. Dosyayı kaydedin
5. Artık `http://gitlab.local` adresinden erişebilirsiniz

---

## 🚀 GitLab Runner Kaydı (CI/CD için)

Proje oluşturulduktan sonra CI/CD pipeline'larını çalıştırmak için runner'ı kaydetmeniz gerekir:

### Adım 1: GitLab'dan Runner Token Alın

1. GitLab proje sayfasında: **Settings > CI/CD**
2. **Runners** bölümünü genişletin
3. **Registration token**'ı kopyalayın

### Adım 2: Runner'ı Kaydedin

```bash
# Runner'ı kaydedin (interaktif)
docker exec -it gitlab-runner gitlab-runner register

# Sorular:
# 1. GitLab URL: http://gitlab
# 2. Registration token: (GitLab'dan kopyaladığınız token)
# 3. Description: monitrang-runner
# 4. Tags: docker, windows (opsiyonel)
# 5. Executor: docker
# 6. Default Docker image: docker:latest
```

---

## 📝 Sonraki Adımlar

1. ✅ GitLab'a giriş yapın
2. ✅ Proje oluşturun
3. ✅ Repository'yi push edin
4. ✅ GitLab Runner'ı kaydedin
5. ⏳ `.gitlab-ci.yml` dosyasını oluşturun
6. ⏳ CI/CD pipeline'ını yapılandırın
7. ⏳ Dokümantasyon pipeline'ını ekleyin

---

## 🆘 Sorun Giderme

### GitLab'a Erişemiyorum

```bash
# Container'ın çalıştığını kontrol edin
docker ps | grep gitlab

# Logları kontrol edin
docker logs gitlab --tail 100

# GitLab servislerini kontrol edin
docker exec gitlab gitlab-ctl status
```

### Push Hatası: Authentication Failed

```bash
# HTTP kullanıyorsanız, şifre yerine Personal Access Token kullanın
# GitLab > User Settings > Access Tokens
# Scope: write_repository seçin

# Token ile push:
git push http://root:YOUR_TOKEN@localhost/root/monitrang.git main
```

### Port Çakışması

Eğer port 80 zaten kullanılıyorsa, docker-compose.yml'de port'u değiştirin:

```yaml
gitlab:
  ports:
    - "8082:80"  # 8082 portunu kullan
```

---

## 📚 İlgili Dokümantasyon

- [GitLab CI/CD Pipeline Yapılandırması](GITLAB_CI_CD_GUIDE.md) (oluşturulacak)
- [MkDocs Dokümantasyon Pipeline'ı](MKDOCS_PIPELINE_GUIDE.md) (oluşturulacak)
- [Dual Repository Sync Stratejisi](DUAL_REPO_SYNC.md) (oluşturulacak)

---

**Son Güncelleme:** 27 Aralık 2024

