# GitLab Runner Kayıt Rehberi

**Durum:** Runner container çalışıyor, kayıt yapılacak  
**Tarih:** 27 Aralık 2024

---

## 🎯 GitLab Runner Nedir?

GitLab Runner, CI/CD pipeline'larını çalıştırmak için kullanılan servistir. GitLab'dan gelen job'ları alır ve belirtilen executor'da (Docker, shell, vb.) çalıştırır.

---

## 📋 Adım Adım Runner Kaydı

### Adım 1: GitLab'dan Runner Token Alın

1. **GitLab'da proje sayfasına gidin:**
   - URL: `http://localhost/root/MonitraNG`
   - Veya ana sayfadan projeye tıklayın

2. **CI/CD Settings'e gidin:**
   - Sol menüden **"Settings"** seçin
   - **"CI/CD"** sekmesine tıklayın
   - **"Runners"** bölümünü genişletin

3. **Registration Token'ı kopyalayın:**
   - **"Set up a specific runner manually"** bölümünde
   - **"Registration token"** değerini kopyalayın
   - Bu token'ı kayıt işleminde kullanacağız

### Adım 2: Runner'ı Kaydedin

Runner'ı kaydetmek için interaktif komut kullanacağız. Şu sorular sorulacak:

**Komut:**
```bash
docker exec -it gitlab-runner gitlab-runner register
```

**Sorular ve Önerilen Cevaplar:**

1. **GitLab instance URL:**
   ```
   http://gitlab
   ```
   > Not: Container içinden `localhost` yerine `gitlab` kullanıyoruz (Docker network ismi)

2. **Registration token:**
   ```
   (GitLab'dan kopyaladığınız token)
   ```

3. **Description:**
   ```
   monitrang-runner
   ```
   > İstediğiniz bir açıklama yazabilirsiniz

4. **Tags:**
   ```
   docker, windows
   ```
   > Boş bırakabilirsiniz veya pipeline'larda kullanmak için tag ekleyebilirsiniz

5. **Executor:**
   ```
   docker
   ```
   > Docker executor kullanıyoruz

6. **Default Docker image:**
   ```
   docker:latest
   ```
   > Veya `mcr.microsoft.com/dotnet/sdk:9.0` (.NET projeleri için)

---

## 🚀 Hızlı Kayıt (Token ile)

Token'ı aldıktan sonra, non-interaktif olarak da kaydedebilirsiniz:

```bash
docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url "http://gitlab" \
  --registration-token "YOUR_TOKEN" \
  --executor "docker" \
  --docker-image "docker:latest" \
  --description "monitrang-runner" \
  --tag-list "docker,windows" \
  --run-untagged="true" \
  --locked="false"
```

---

## ✅ Kayıt Kontrolü

### Runner'ın Kayıtlı Olduğunu Kontrol Edin

1. **GitLab'da:**
   - Settings > CI/CD > Runners
   - "Available specific runners" bölümünde runner'ınızı görmelisiniz
   - Status: **"Online"** ve **"Active"** olmalı

2. **Komut satırından:**
   ```bash
   docker exec gitlab-runner gitlab-runner list
   ```

---

## 🔧 Runner Yapılandırması

Runner kaydedildikten sonra yapılandırma dosyası şurada oluşturulur:
```
/etc/gitlab-runner/config.toml
```

Bu dosyayı Docker container içinde görmek için:
```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml
```

---

## 🐳 Docker Executor Yapılandırması

Docker executor kullanıyorsanız, bazı ek ayarlar yapılabilir:

### Privileged Mode (Docker-in-Docker için)

Eğer pipeline'larda Docker komutları çalıştıracaksanız (Docker build, vb.), runner'ı privileged mode'da çalıştırmanız gerekebilir:

```toml
[[runners]]
  [runners.docker]
    privileged = true
    volumes = ["/var/run/docker.sock:/var/run/docker.sock", "/cache"]
```

Bu ayar kayıt sırasında sorulmaz, sonradan config.toml'da düzenlenebilir.

---

## 🆘 Sorun Giderme

### Runner GitLab'a Bağlanamıyor

**Sorun:** Runner kaydı sırasında "connection refused" hatası.

**Çözüm:**
- GitLab URL'inin doğru olduğundan emin olun: `http://gitlab` (container network ismi)
- GitLab container'ının çalıştığını kontrol edin: `docker ps | grep gitlab`
- GitLab ve Runner'ın aynı network'te olduğunu kontrol edin

### Token Geçersiz

**Sorun:** "Registration token is invalid" hatası.

**Çözüm:**
- Token'ı GitLab'dan yeniden kopyalayın
- Token'ın doğru projeye ait olduğundan emin olun
- Token'ın süresi dolmamış olmalı (project token'lar süresizdir)

### Runner Kayıtlı ama Offline Görünüyor

**Sorun:** GitLab'da runner kayıtlı ama "Offline" durumunda.

**Çözüm:**
```bash
# Runner container'ının çalıştığını kontrol edin
docker ps | grep gitlab-runner

# Runner'ı yeniden başlatın
docker restart gitlab-runner

# Runner loglarını kontrol edin
docker logs gitlab-runner
```

### Docker Executor Hatası

**Sorun:** Pipeline çalışırken Docker executor hatası.

**Çözüm:**
- Docker socket'in mount edildiğinden emin olun (docker-compose.yml'de)
- Runner container'ının Docker'a erişebildiğini test edin:
  ```bash
  docker exec gitlab-runner docker ps
  ```

---

## 📚 Sonraki Adımlar

Runner kaydedildikten sonra:

1. ✅ Runner kaydı tamamlandı
2. ⏳ `.gitlab-ci.yml` dosyası oluşturulacak
3. ⏳ İlk pipeline test edilecek
4. ⏳ Build ve test job'ları eklenecek

---

**Son Güncelleme:** 27 Aralık 2024

