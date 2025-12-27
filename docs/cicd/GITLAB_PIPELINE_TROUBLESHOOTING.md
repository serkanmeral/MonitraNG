# GitLab Pipeline Sorun Giderme Rehberi

**Durum:** Pipeline başarısız oluyor  
**Tarih:** 27 Aralık 2024

---

## 🔍 Yaygın Sorunlar ve Çözümleri

### 1. Docker Executor Sorunları

**Sorun:** Runner Docker container'ları başlatamıyor.

**Kontrol:**
```bash
# Runner container'ının çalıştığını kontrol edin
docker ps | grep gitlab-runner

# Runner loglarını kontrol edin
docker logs gitlab-runner --tail 50

# Runner yapılandırmasını kontrol edin
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml
```

**Çözüm:**
- Docker socket'in mount edildiğinden emin olun
- Runner'ın aynı network'te olduğundan emin olun
- Privileged mode gerekebilir (config.toml'da)

---

### 2. Image İndirme Sorunları

**Sorun:** Docker image'ları indirilemiyor.

**Kontrol:**
- Runner'ın internet erişimi var mı?
- Docker registry erişilebilir mi?

**Çözüm:**
- Runner network yapılandırmasını kontrol edin
- Proxy ayarları gerekebilir

---

### 3. Path/File Bulunamıyor

**Sorun:** Solution dosyaları bulunamıyor.

**Kontrol:**
- GitLab'da job loglarına bakın
- `test-setup` job'u çalıştırıldıysa, dosya yapısını görebilirsiniz

**Çözüm:**
- Path'lerin doğru olduğundan emin olun
- Working directory'yi kontrol edin

---

### 4. Build Hataları

**Sorun:** dotnet build/restore başarısız oluyor.

**Kontrol:**
- Job loglarını inceleyin
- NuGet package erişimi var mı?
- Solution dosyası geçerli mi?

**Çözüm:**
- NuGet source'larını kontrol edin
- Package restore loglarını inceleyin

---

## 🔧 Debug Yöntemleri

### Test Setup Job

`test-setup` job'u environment'ı kontrol eder:
- Working directory
- Dosya yapısı
- .NET versiyonu
- Solution dosyalarının varlığı

Bu job başarısız olursa, temel yapılandırma sorunu var demektir.

### Job Logları İnceleme

GitLab'da:
1. Pipeline'a tıklayın
2. Başarısız job'a tıklayın
3. Log'ları inceleyin
4. Hata mesajını arayın

### Runner Logları

```bash
# Runner loglarını görüntüle
docker logs gitlab-runner --tail 100 -f

# Belirli bir job için log
docker logs gitlab-runner | grep "job-name"
```

---

## 📋 Kontrol Listesi

Pipeline başarısız olduğunda kontrol edin:

- [ ] Runner online mı? (Settings > CI/CD > Runners)
- [ ] Runner'ın doğru tag'lere sahip mi? (`docker`)
- [ ] Docker image'ları erişilebilir mi?
- [ ] Solution dosyaları mevcut mu?
- [ ] Path'ler doğru mu?
- [ ] Network bağlantısı var mı?
- [ ] Runner Docker socket'e erişebiliyor mu?

---

## 🆘 Hata Mesajı Paylaşma

Sorun gidermek için şu bilgileri paylaşın:

1. **Hangi job başarısız oldu?**
   - Job adı
   - Stage

2. **Hata mesajı nedir?**
   - Job loglarından hata satırları
   - Exception mesajları

3. **Runner durumu:**
   - Online/Offline?
   - Tag'ler doğru mu?

---

**Son Güncelleme:** 27 Aralık 2024

