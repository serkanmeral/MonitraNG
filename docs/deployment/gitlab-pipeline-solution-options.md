# GitLab Pipeline Git Fetch Sorunu - Çözüm Seçenekleri

**Tarih:** 1 Ocak 2026  
**Durum:** Seçenekler değerlendiriliyor

---

## 🔍 Sorun Özeti

**Problem:**
- Pipeline'da `test-setup` job'u Git fetch yaparken başarısız oluyor
- Hata: `fatal: unable to access 'http://45.141.151.52:8090/root/monitrang.git/': Failed to connect`
- Build container'ları external IP'ye (`45.141.151.52:8090`) erişemiyor

**Kök Neden:**
- GitLab repository URL'leri external IP döndürüyor
- Runner container'ı `mng_network` (bridge) içinde
- Build container'ları host network'te olsa bile external IP'ye erişemiyorlar
- Docker network izolasyonu nedeniyle bridge network'ten host network'e geçiş sorunlu

---

## 💡 Çözüm Seçenekleri

### Seçenek 1: Runner Container'ını Host Network'te Çalıştırmak ⭐ (ÖNERİLEN)

**Yaklaşım:**
1. `docker-compose.yml`'de runner container'ı için `network_mode: host` eklemek
2. Runner config'de URL'yi IP'ye çevirmek (`http://172.18.0.6` veya `http://45.141.151.52:8090`)
3. Runner container'ını restart etmek

**Artıları:**
- ✅ Build container'ları host network'te olur
- ✅ External IP'ye erişebilirler
- ✅ Git fetch çalışabilir
- ✅ En az riskli çözüm
- ✅ Sadece runner container'ı etkilenir
- ✅ Diğer servisler etkilenmez

**Eksileri:**
- ⚠️ Runner URL'ini IP'ye çevirmek gerekir
- ⚠️ Runner GitLab'a hostname (`gitlab`) ile erişemez
- ⚠️ GitLab IP'si değişirse runner config güncellenmeli

**Uygulama Adımları:**
1. GitLab container'ının IP'sini bul: `docker inspect gitlab | grep IPAddress`
2. `docker-compose.yml`'de runner'a `network_mode: host` ekle
3. Runner config'de URL'yi IP'ye çevir
4. Runner container'ını restart et
5. Pipeline'ı test et

**Karmaşıklık:** 🟡 Orta  
**Risk:** 🟢 Düşük  
**Etki:** 🟢 Sadece Runner

---

### Seçenek 2: GitLab'ı Host Network'te Çalıştırmak

**Yaklaşım:**
- GitLab container'ını host network'te çalıştırmak
- Tüm GitLab servisleri host network stack'ini kullanır

**Artıları:**
- ✅ GitLab external IP'ye direkt erişilebilir
- ✅ Repository URL'leri doğru çalışır

**Eksileri:**
- ❌ Tüm GitLab servisleri etkilenir
- ❌ Port çakışmaları olabilir (80, 443, 22, vb.)
- ❌ Network yapısı tamamen değişir
- ❌ GitLab'ın internal servisleri (PostgreSQL, Redis) de etkilenir
- ❌ Diğer servislerle (MongoDB, RabbitMQ, vb.) network izolasyonu kaybolur
- ❌ Güvenlik riski (tüm servisler host network'te)

**Karmaşıklık:** 🔴 Çok Yüksek  
**Risk:** 🔴 Yüksek  
**Etki:** 🔴 Tüm Sistem

**Öneri:** ❌ Önerilmez - Çok riskli ve karmaşık

---

### Seçenek 3: Network Yapısını Tamamen Değiştirmek

**Yaklaşım:**
- Runner ve GitLab'ı aynı bridge network'te tutmak
- Build container'ları için network_mode kullanmamak
- External IP erişimi için NAT/iptables kuralları eklemek
- Docker network routing yapılandırması

**Artıları:**
- ✅ Network yapısı kontrol altında
- ✅ Güvenlik ve izolasyon korunur

**Eksileri:**
- ❌ Kompleks network yapılandırması
- ❌ Docker network yapısını değiştirmek
- ❌ NAT/iptables kuralları gerekir
- ❌ Routing yapılandırması gerekir
- ❌ Maintenance zorluğu
- ❌ Hata ayıklama zor

**Karmaşıklık:** 🔴 Çok Yüksek  
**Risk:** 🟡 Orta  
**Etki:** 🟡 Network Yapısı

**Öneri:** ❌ Önerilmez - Çok karmaşık

---

### Seçenek 4: GitLab Repository URL'ini Override Etmek

**Yaklaşım:**
- Pipeline'da GitLab repository URL'ini environment variable olarak override etmek
- Git fetch yerine manuel git clone kullanmak

**Sorunlar:**
- ❌ GitLab Runner'ın "Getting source from Git repository" adımını bypass edemezsiniz
- ❌ Bu adım GitLab tarafından otomatik yapılır, override edilemez
- ❌ Pipeline'ın ilk adımı, job script'lerinden önce çalışır

**Karmaşıklık:** ❌ Mümkün Değil  
**Öneri:** ❌ Uygulanamaz

---

### Seçenek 5: GitLab'ın Internal Git URL'ini Yapılandırmak

**Yaklaşım:**
- GitLab'ın repository clone URL'lerini internal IP/hostname döndürmesi
- `gitlab_rails["gitlab_shell_ssh_host"]` ve benzeri ayarlar

**Sorunlar:**
- ❌ Daha önce denendi, çalışmadı
- ❌ GitLab'ın repository URL'leri `external_url` ayarına bağlı
- ❌ `external_url` değiştirilmeden internal git URL'lerini yapılandırmak mümkün değil
- ❌ External URL zaten external IP'ye ayarlı

**Karmaşıklık:** ❌ Mümkün Değil  
**Öneri:** ❌ Daha önce denendi, çalışmadı

---

## 🎯 Önerilen Çözüm: Seçenek 1

**Neden Seçenek 1?**
1. ✅ En az riskli - Sadece runner container'ı etkilenir
2. ✅ En hızlı uygulanabilir - Tek container değişikliği
3. ✅ En az karmaşık - Basit network mode değişikliği
4. ✅ Diğer servisler etkilenmez
5. ✅ Geri alınabilir - Kolayca eski haline döndürülebilir

**Uygulama Planı:**
1. GitLab container IP'sini belirle
2. `docker-compose.yml`'de runner'a `network_mode: host` ekle
3. Runner config'de URL'yi IP'ye çevir
4. Runner container'ını restart et
5. Pipeline'ı test et
6. Sorun devam ederse alternatif çözümler düşün

---

## 📋 Uygulama Adımları (Seçenek 1)

### Adım 1: GitLab Container IP'sini Bul

```bash
# Sunucuda
docker inspect gitlab | grep IPAddress
# veya
docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}}{{end}}' gitlab
```

**Beklenen Sonuç:** `172.18.0.6` (veya benzeri bir IP)

### Adım 2: docker-compose.yml Güncelle

```yaml
gitlab-runner:
  image: gitlab/gitlab-runner:latest
  container_name: gitlab-runner
  network_mode: host  # ← EKLE
  volumes:
    - /var/run/docker.sock:/var/run/docker.sock
    - gitlab_runner_config:/etc/gitlab-runner
  environment:
    - DOCKER_HOST=unix:///var/run/docker.sock
  # networks:  # ← KALDIR (network_mode kullanıyorsak networks gerekmez)
  #   - mng_network
  restart: unless-stopped
  depends_on:
    - gitlab
```

### Adım 3: Runner Config Güncelle

```bash
# Runner config dosyasını düzenle
docker exec -it gitlab-runner cat /etc/gitlab-runner/config.toml

# URL'yi değiştir:
# url = "http://gitlab"  →  url = "http://172.18.0.6"
# veya
# url = "http://gitlab"  →  url = "http://45.141.151.52:8090"
```

**Not:** Config dosyası volume'da saklanıyor, direkt düzenlenebilir veya runner'ı yeniden kaydetmek gerekebilir.

### Adım 4: Runner Container'ını Restart Et

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose restart gitlab-runner
```

### Adım 5: Pipeline'ı Test Et

```bash
# GitLab'da yeni bir commit push et veya pipeline'ı manuel tetikle
```

---

## ⚠️ Potansiyel Sorunlar ve Çözümleri

### Sorun 1: Runner GitLab'a Bağlanamıyor

**Belirtiler:**
- Runner "offline" görünüyor
- Pipeline job'ları başlamıyor

**Çözüm:**
- Runner URL'ini kontrol et (`http://172.18.0.6` veya `http://45.141.151.52:8090`)
- GitLab container'ının çalıştığını kontrol et
- Runner loglarını kontrol et: `docker logs gitlab-runner`

### Sorun 2: Build Container'ları Hala External IP'ye Erişemiyor

**Belirtiler:**
- Git fetch hala başarısız
- Aynı hata mesajı

**Çözüm:**
- Runner config'de `network_mode = "host"` kaldırılmalı (artık gerekmez, runner zaten host network'te)
- Runner container'ının gerçekten host network'te olduğunu kontrol et: `docker inspect gitlab-runner | grep NetworkMode`
- Firewall kurallarını kontrol et

### Sorun 3: Docker Socket Erişimi Sorunu

**Belirtiler:**
- Docker build job'ları başarısız
- "Cannot connect to Docker daemon" hatası

**Çözüm:**
- Docker socket mount'un doğru olduğunu kontrol et
- `privileged = true` ayarının runner config'de olduğunu kontrol et

---

## 🔄 Geri Alma Planı

Eğer Seçenek 1 çalışmazsa:

1. `docker-compose.yml`'den `network_mode: host` kaldır
2. `networks: - mng_network` geri ekle
3. Runner config'de URL'yi `http://gitlab` olarak geri değiştir
4. Runner container'ını restart et

**Alternatif:** Seçenek 2 veya 3'ü değerlendir (daha riskli)

---

## 📊 Karşılaştırma Tablosu

| Seçenek | Karmaşıklık | Risk | Etki | Uygulama Süresi | Öneri |
|---------|-------------|------|------|-----------------|-------|
| 1. Runner Host Network | 🟡 Orta | 🟢 Düşük | 🟢 Sadece Runner | ⏱️ 15-30 dk | ✅ **ÖNERİLEN** |
| 2. GitLab Host Network | 🔴 Çok Yüksek | 🔴 Yüksek | 🔴 Tüm Sistem | ⏱️ 1-2 saat | ❌ Önerilmez |
| 3. Network Yapısı Değişikliği | 🔴 Çok Yüksek | 🟡 Orta | 🟡 Network | ⏱️ 2-4 saat | ❌ Önerilmez |
| 4. URL Override | ❌ Mümkün Değil | - | - | - | ❌ Uygulanamaz |
| 5. Internal Git URL | ❌ Mümkün Değil | - | - | - | ❌ Daha önce denendi |

---

## 🎯 Sonuç

**Önerilen Çözüm:** Seçenek 1 - Runner Container'ını Host Network'te Çalıştırmak

**Neden:**
- En az riskli
- En hızlı uygulanabilir
- En az karmaşık
- Geri alınabilir
- Diğer servisler etkilenmez

**Sonraki Adım:** Seçenek 1'i uygulamak ve test etmek

---

**Son Güncelleme:** 1 Ocak 2026

