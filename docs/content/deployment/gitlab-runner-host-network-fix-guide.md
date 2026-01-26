# GitLab Runner Host Network Fix - Uygulama Rehberi

**Tarih:** 1 Ocak 2026  
**Çözüm:** Seçenek 1 - Runner Container'ını Host Network'te Çalıştırmak

---

## 🎯 Amaç

GitLab CI/CD pipeline'ında Git fetch sorununu çözmek için runner container'ını host network'te çalıştırmak. Bu sayede build container'ları external IP'ye (`45.141.151.52:8090`) erişebilecek.

---

## ✅ Yapılan Değişiklikler

### 1. docker-compose.yml Güncellendi

**Dosya:** `ApplicationResources/mng_common/docker-compose.yml`

**Değişiklikler:**
- `gitlab-runner` servisine `network_mode: host` eklendi
- `networks: - mng_network` yorumlandı (host network kullanıldığı için gerekmez)

**Not:** Bu değişiklik lokal dosyada yapıldı, sunucuya push edilmeli.

---

## 🚀 Uygulama Yöntemleri

### Yöntem 1: Otomatik Script (Önerilen)

**Adımlar:**

1. **Lokal değişiklikleri push et:**
   ```bash
   git add ApplicationResources/mng_common/docker-compose.yml
   git commit -m "fix: Runner container'ını host network'te çalıştır"
   git push origin main
   ```

2. **Sunucuya SSH ile bağlan:**
   ```bash
   ssh monitrang-server
   # veya
   ssh root@45.141.151.52
   ```

3. **Repository'yi güncelle:**
   ```bash
   cd /root/MonitraNG
   git pull origin main
   ```

4. **Script'i çalıştır:**
   ```bash
   chmod +x scripts/fix-gitlab-runner-host-network.sh
   ./scripts/fix-gitlab-runner-host-network.sh
   ```

**Script'in Yaptığı İşlemler:**
- GitLab container IP'sini bulur
- Runner config'i yedekler
- Runner config URL'yi IP'ye çevirir (`http://gitlab` → `http://172.18.0.6`)
- Runner container'ını restart eder
- Runner durumunu kontrol eder

---

### Yöntem 2: Manuel Adımlar

Eğer script çalışmazsa veya manuel kontrol etmek isterseniz:

#### Adım 1: GitLab Container IP'sini Bul

```bash
# Sunucuda
docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab
```

**Beklenen Sonuç:** `172.18.0.6` (veya benzeri bir IP)

**Alternatif:**
```bash
docker inspect gitlab | grep IPAddress
```

#### Adım 2: Runner Config'i Yedekle

```bash
# Runner config'i container içinden oku
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml > /tmp/runner-config-backup.toml

# Veya volume'dan (eğer erişilebilirse)
docker volume inspect mng_common_gitlab_runner_config
# Volume mount point'ini kullanarak yedek al
```

#### Adım 3: Runner Config URL'yi Güncelle

**Mevcut Config'i Kontrol Et:**
```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep url
```

**URL'yi Güncelle:**
```bash
# GitLab IP'sini değişkene al (örnek: 172.18.0.6)
GITLAB_IP="172.18.0.6"

# URL'yi güncelle
docker exec gitlab-runner sed -i "s|url = \"http://gitlab\"|url = \"http://${GITLAB_IP}\"|g" /etc/gitlab-runner/config.toml

# Doğrula
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep url
```

**Manuel Düzenleme (Gerekirse):**
```bash
docker exec -it gitlab-runner vi /etc/gitlab-runner/config.toml
# URL'yi şu şekilde değiştir:
# url = "http://gitlab"  →  url = "http://172.18.0.6"
```

#### Adım 4: docker-compose.yml'i Güncelle

**Sunucuda repository'yi güncelle:**
```bash
cd /root/MonitraNG
git pull origin main
```

**Runner container'ını restart et:**
```bash
cd ApplicationResources/mng_common
docker compose stop gitlab-runner
docker compose up -d gitlab-runner
```

**Alternatif (docker-compose olmadan):**
```bash
docker stop gitlab-runner
docker rm gitlab-runner
# docker-compose.yml'deki ayarlarla yeniden oluştur
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose up -d gitlab-runner
```

#### Adım 5: Runner Durumunu Kontrol Et

**Container Durumu:**
```bash
docker ps | grep gitlab-runner
```

**Network Mode:**
```bash
docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'
# Beklenen: "host"
```

**Runner Verify:**
```bash
docker exec gitlab-runner gitlab-runner verify
```

**Runner Logları:**
```bash
docker logs gitlab-runner --tail 50
```

---

## ✅ Doğrulama

### 1. Runner Container Durumu

```bash
docker ps | grep gitlab-runner
# Runner container'ı çalışıyor olmalı
```

### 2. Network Mode

```bash
docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'
# Çıktı: "host"
```

### 3. Runner Config URL

```bash
docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep url
# Çıktı: url = "http://172.18.0.6" (veya GitLab IP'si)
```

### 4. Runner GitLab'a Bağlanabiliyor mu?

```bash
docker exec gitlab-runner gitlab-runner verify
# Runner "online" ve "active" olmalı
```

### 5. Pipeline Test

1. GitLab'da yeni bir commit push et veya pipeline'ı manuel tetikle
2. `test-setup` job'unun Git fetch yapabildiğini kontrol et
3. Hata mesajı olmamalı: `fatal: unable to access 'http://45.141.151.52:8090/...'`

---

## ⚠️ Sorun Giderme

### Sorun 1: Runner GitLab'a Bağlanamıyor

**Belirtiler:**
- Runner "offline" görünüyor
- `gitlab-runner verify` başarısız

**Çözüm:**
1. GitLab container'ının çalıştığını kontrol et: `docker ps | grep gitlab`
2. Runner config URL'yi kontrol et: `docker exec gitlab-runner cat /etc/gitlab-runner/config.toml | grep url`
3. GitLab IP'sini doğrula: `docker inspect gitlab | grep IPAddress`
4. Runner loglarını kontrol et: `docker logs gitlab-runner --tail 50`

### Sorun 2: Build Container'ları Hala External IP'ye Erişemiyor

**Belirtiler:**
- Git fetch hala başarısız
- Aynı hata mesajı

**Çözüm:**
1. Runner container'ının network mode'unu kontrol et: `docker inspect gitlab-runner --format '{{.HostConfig.NetworkMode}}'`
2. Runner config'de `network_mode = "host"` kaldırılmalı (artık gerekmez, runner zaten host network'te)
3. Firewall kurallarını kontrol et
4. GitLab external URL'ini kontrol et: `docker exec gitlab env | grep GITLAB_OMNIBUS_CONFIG`

### Sorun 3: Docker Socket Erişimi Sorunu

**Belirtiler:**
- Docker build job'ları başarısız
- "Cannot connect to Docker daemon" hatası

**Çözüm:**
1. Docker socket mount'un doğru olduğunu kontrol et: `docker inspect gitlab-runner | grep docker.sock`
2. `privileged = true` ayarının runner config'de olduğunu kontrol et
3. Runner container'ını restart et

### Sorun 4: Script Çalışmıyor

**Çözüm:**
- Script'i manuel adımlarla çalıştır (Yöntem 2)
- Script loglarını kontrol et
- Her adımı manuel olarak doğrula

---

## 🔄 Geri Alma

Eğer çözüm çalışmazsa veya sorun çıkarsa:

### Adım 1: docker-compose.yml'i Geri Al

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
# network_mode: host satırını kaldır
# networks: - mng_network satırını geri ekle
```

### Adım 2: Runner Config URL'yi Geri Al

```bash
docker exec gitlab-runner sed -i 's|url = "http://172.18.0.6"|url = "http://gitlab"|g' /etc/gitlab-runner/config.toml
```

### Adım 3: Runner Container'ını Restart Et

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose restart gitlab-runner
```

---

## 📊 Beklenen Sonuçlar

### Başarılı Durum

- ✅ Runner container host network'te çalışıyor
- ✅ Runner GitLab'a IP ile bağlanabiliyor
- ✅ Pipeline'lar başlayabiliyor
- ✅ Git fetch başarılı
- ✅ Build job'ları çalışıyor

### Başarısız Durum

- ❌ Runner GitLab'a bağlanamıyor
- ❌ Pipeline'lar hala başlayamıyor
- ❌ Git fetch hala başarısız

**Sonraki Adım:** Alternatif çözümleri değerlendir (Seçenek 2 veya 3)

---

## 📝 Notlar

- Runner config dosyası Docker volume'da saklanıyor: `mng_common_gitlab_runner_config`
- Runner container'ı restart edildiğinde config korunur
- GitLab IP'si değişirse runner config'i güncellemek gerekir
- Host network kullanıldığında runner container'ı host'un network stack'ini kullanır

---

**Son Güncelleme:** 1 Ocak 2026

