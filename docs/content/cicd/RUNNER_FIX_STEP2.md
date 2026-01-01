# GitLab Runner Düzeltme - Adım 2: Runner'ı Yeniden Kaydetme

**Tarih:** 15 Ocak 2025  
**Durum:** Network mode düzeltildi, runner'ı yeniden kaydetme gerekiyor

---

## ✅ Tamamlanan Adımlar

1. ✅ docker-compose.yml güncellendi (`network_mode: host`)
2. ✅ Runner container host network'te çalışıyor
3. ⏳ Runner'ı IP ile yeniden kaydetme (ŞİMDİ YAPILACAK)

---

## 🔄 Runner'ı Yeniden Kaydetme

### Adım 1: GitLab'dan Registration Token Al

1. GitLab UI'ya git: `http://45.141.151.52:8090`
2. Proje sayfasına git: `http://45.141.151.52:8090/root/MonitraNG`
3. **Settings > CI/CD > Runners** sekmesine git
4. **"Set up a specific runner manually"** bölümünü genişlet
5. **Registration token** değerini kopyala

**Not:** Token formatı: `glrt-...` veya `GR13489412...` gibi bir şey olabilir.

---

### Adım 2: Runner'ı Kaydet

Token'ı aldıktan sonra şu komutu çalıştırın (YOUR_TOKEN_HERE yerine gerçek token'ı yazın):

```bash
ssh root@monitrang-server "docker exec -it gitlab-runner gitlab-runner register \
  --non-interactive \
  --url \"http://172.18.0.6\" \
  --registration-token \"YOUR_TOKEN_HERE\" \
  --executor \"docker\" \
  --docker-image \"mcr.microsoft.com/dotnet/sdk:9.0\" \
  --description \"monitrang-runner\" \
  --tag-list \"docker\" \
  --run-untagged=\"true\" \
  --locked=\"false\" \
  --docker-privileged=\"true\" \
  --docker-network-mode=\"host\""
```

**Önemli Parametreler:**
- `--url "http://172.18.0.6"` - GitLab container IP (host network'te hostname çalışmaz)
- `--docker-network-mode="host"` - Build container'ları host network'te çalışacak
- `--docker-privileged="true"` - Docker-in-Docker için gerekli

---

### Adım 3: Doğrulama

Runner kaydedildikten sonra kontrol edin:

```bash
# Runner config URL kontrolü
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep -E '^[[:space:]]*url[[:space:]]*='"
# Beklenen: url = "http://172.18.0.6"

# Runner config network mode kontrolü
ssh root@monitrang-server "docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep network_mode"
# Beklenen: network_mode = "host"

# Runner verify
ssh root@monitrang-server "docker exec gitlab-runner gitlab-runner verify"
# Beklenen: Verifying runner... is alive
```

---

## 📊 Beklenen Sonuçlar

Runner kaydedildikten sonra:

- ✅ Runner config URL: `http://172.18.0.6` (IP formatında)
- ✅ Runner config network_mode: `host`
- ✅ Runner verify: Başarılı
- ✅ Pipeline'lar çalışabilir (Git fetch başarılı olacak)

---

**Son Güncelleme:** 15 Ocak 2025

