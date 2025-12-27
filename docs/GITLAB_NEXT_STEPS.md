# GitLab Kurulum Sonrası Adımlar

**Durum:** ✅ GitLab'a push başarılı  
**Proje URL:** `http://localhost/root/MonitraNG`

---

## ✅ Tamamlanan Adımlar

- [x] GitLab Docker container kurulumu
- [x] GitLab'a giriş yapıldı
- [x] MonitraNG projesi oluşturuldu
- [x] GitLab remote eklendi
- [x] Repository GitLab'a push edildi

---

## 📋 Sonraki Adımlar

### 1. GitLab Runner Kaydı (CI/CD için)

CI/CD pipeline'larını çalıştırmak için runner'ı kaydetmeniz gerekiyor.

#### Adım 1: GitLab'dan Runner Token Alın

1. GitLab proje sayfasında: **Settings > CI/CD**
2. **Runners** bölümünü genişletin
3. **"Set up a specific runner manually"** bölümünde
4. **Registration token**'ı kopyalayın

#### Adım 2: Runner'ı Kaydedin

```bash
# Runner'ı kaydedin (interaktif)
docker exec -it gitlab-runner gitlab-runner register
```

**Sorular ve Cevaplar:**
- **GitLab instance URL:** `http://gitlab`
- **Registration token:** (GitLab'dan kopyaladığınız token)
- **Description:** `monitrang-runner`
- **Tags:** `docker, windows` (opsiyonel, boş bırakabilirsiniz)
- **Executor:** `docker`
- **Default Docker image:** `docker:latest`

---

### 2. .gitlab-ci.yml Dosyası Oluşturma

GitLab CI/CD pipeline'ı için `.gitlab-ci.yml` dosyası oluşturulacak.

**Yapılacaklar:**
- Build pipeline (.NET servisleri için)
- Test pipeline
- Docker build pipeline
- Dokümantasyon build pipeline (MkDocs)

---

### 3. GitHub + GitLab Dual Sync

Her push'ta hem GitHub hem GitLab'a otomatik push yapılacak şekilde yapılandırılacak.

**Seçenekler:**
- **Yöntem 1:** Git config ile multiple push URL
- **Yöntem 2:** Git hook script'i
- **Yöntem 3:** GitLab CI/CD pipeline ile otomatik sync (push sonrası)

---

### 4. MkDocs Dokümantasyon Pipeline

GitLab Pages ile dokümantasyon otomatik deploy edilecek.

**Yapılacaklar:**
- MkDocs build stage
- GitLab Pages deployment
- OpenAPI spec export entegrasyonu

---

## 🔄 GitHub + GitLab Sync Yapılandırması

### Yöntem 1: Git Config Multiple Push (Önerilen)

```powershell
# Origin remote'unu multiple push için yapılandır
git remote set-url --add --push origin https://github.com/serkanmeral/MonitraNG.git
git remote set-url --add --push origin http://root:TOKEN@localhost/root/MonitraNG.git

# Artık tek komutla her ikisine push
git push origin main
```

### Yöntem 2: Git Hook (Local)

`.git/hooks/post-commit` dosyası oluşturulabilir (ileride).

---

## 📚 Oluşturulacak Dosyalar

1. **`.gitlab-ci.yml`** - CI/CD pipeline yapılandırması
2. **`scripts/gitlab-sync.ps1`** - GitHub + GitLab sync script'i (opsiyonel)
3. **MkDocs pipeline yapılandırması** - Dokümantasyon için

---

## 🎯 Öncelik Sırası

1. **GitLab Runner Kaydı** (CI/CD çalıştırmak için gerekli)
2. **`.gitlab-ci.yml` Oluşturma** (Build ve test pipeline)
3. **GitHub + GitLab Dual Sync** (Her push'ta sync)
4. **MkDocs Pipeline** (Dokümantasyon otomasyonu)

---

**Son Güncelleme:** 27 Aralık 2024

