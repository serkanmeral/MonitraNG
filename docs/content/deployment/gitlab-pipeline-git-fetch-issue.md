# GitLab Pipeline Git Fetch Sorunu - Final Durum

**Tarih:** 1 Ocak 2026  
**Durum:** ❌ Çözülemedi - Network Yapısından Kaynaklanıyor

---

## 🔍 Sorun

Pipeline'da `test-setup` job'u sürekli başarısız oluyor:

```
fatal: unable to access 'http://45.141.151.52:8090/root/monitrang.git/': 
Failed to connect to 45.141.151.52 port 8090 after 130732 ms: Could not connect to server
```

**Kök Neden:**
- GitLab repository URL'leri external IP (`45.141.151.52:8090`) döndürüyor
- Build container'ları bu external IP'ye erişemiyor
- Runner container'ı kendi network'ünde (`mng_common_mng_network`)
- Build container'ları host network'te çalışsa bile erişemiyorlar

---

## 🔄 Denenen Çözümler

### 1. ✅ Artifacts Optional Yapma (Uygulandı)

**Yapılanlar:**
- Build job'larından artifacts kaldırıldı
- Test job'ları kendi build'lerini yapacak şekilde güncellendi
- `extract-openapi-specs` job'u build komutları eklendi

**Sonuç:**
- ✅ Artifacts upload hatası ortadan kalktı
- ❌ Git fetch sorunu devam ediyor

---

### 2. ✅ network_mode=host Eklendi (Denendi)

**Yapılanlar:**
- Runner config'e `network_mode = "host"` eklendi
- Build container'ları host network'te çalışmalı

**Sonuç:**
- ❌ Çalışmadı - Build container'ları hala external IP'ye erişemiyor

**Neden:**
- Runner container'ı kendi network'ünde (`mng_common_mng_network`)
- Docker executor build container'larını host network'te oluşturuyor
- Ama runner container'ın network'ünden host network'e geçiş sorunlu

---

### 3. ✅ external_url External IP'ye Güncellendi (Denendi)

**Yapılanlar:**
- GitLab `external_url` `http://gitlab` → `http://45.141.151.52:8090` olarak güncellendi
- GitLab reconfigure edildi

**Sonuç:**
- ❌ Çalışmadı - Repository URL'leri zaten external IP döndürüyordu
- Git fetch sorunu devam ediyor

---

### 4. ❌ Internal Git URL Yapılandırması (Daha Önce Denendi)

**Yapılanlar:**
- `gitlab_rails["gitlab_shell_ssh_host"] = "gitlab"` eklenmesi
- `gitlab_rails["gitlab_shell_git_timeout"] = 3600` ayarı
- GitLab reconfigure

**Sonuç:**
- ❌ Çalışmadı - GitLab'ın repository clone URL'leri `external_url` ayarına bağlı
- `external_url` değiştirilmeden internal git URL'lerini yapılandırmak mümkün değil

---

## 📊 Mevcut Yapılandırma

### GitLab
- **external_url:** `http://45.141.151.52:8090`
- **Network:** `mng_common_mng_network`
- **Repository URL:** `http://45.141.151.52:8090/root/monitrang.git`

### GitLab Runner
- **URL:** `http://gitlab` (internal)
- **Network:** `mng_common_mng_network`
- **Executor:** `docker`
- **network_mode:** `host` (build container'ları için)
- **privileged:** `true`
- **Docker socket:** Mount edildi

### Pipeline
- **Artifacts:** Optional (build job'larından kaldırıldı)
- **Test job'ları:** Kendi build'lerini yapıyor
- **Git fetch:** ❌ Başarısız

---

## 💡 Olası Çözümler (Denenmedi)

### Seçenek 1: Runner Container'ını Host Network'te Çalıştırmak

**Yaklaşım:**
- `docker-compose.yml`'de runner container'ı için `network_mode: host` kullanmak
- Runner container'ı host network stack'ini kullanır
- Build container'ları da host network'te çalışır

**Sorunlar:**
- Runner container'ı GitLab'a `http://gitlab` ile erişemez (host network'te hostname çözümleme farklı)
- GitLab'a erişmek için IP kullanmak gerekir (`172.18.0.6` veya external IP)
- Runner URL'ini `http://172.18.0.6` veya `http://45.141.151.52:8090` olarak değiştirmek gerekir

**Karmaşıklık:** 🔴 Yüksek

---

### Seçenek 2: GitLab'ı Host Network'te Çalıştırmak

**Yaklaşım:**
- GitLab container'ını host network'te çalıştırmak
- Tüm servisler host network stack'ini kullanır

**Sorunlar:**
- Tüm servisler etkilenir
- Port çakışmaları olabilir
- Network yapısı tamamen değişir
- Diğer servisler (MongoDB, RabbitMQ, vb.) de etkilenir

**Karmaşıklık:** 🔴 Çok Yüksek

---

### Seçenek 3: Network Yapısını Tamamen Değiştirmek

**Yaklaşım:**
- Runner ve GitLab'ı aynı bridge network'te tutmak
- Build container'ları için network_mode kullanmamak
- External IP erişimi için NAT/iptables kuralları eklemek

**Sorunlar:**
- Kompleks network yapılandırması
- Docker network yapısını değiştirmek
- Güvenlik ve erişim kontrolleri

**Karmaşıklık:** 🔴 Çok Yüksek

---

### Seçenek 4: GitLab Repository URL'ini Override Etmek

**Yaklaşım:**
- Pipeline'da GitLab repository URL'ini environment variable olarak override etmek
- Git fetch yerine manuel git clone kullanmak

**Sorunlar:**
- GitLab Runner'ın "Getting source from Git repository" adımını bypass edemezsiniz
- Bu adım GitLab tarafından otomatik yapılır, override edilemez

**Karmaşıklık:** ❌ Mümkün Değil

---

## ✅ Mevcut Durum (Çalışan Kısımlar)

### Çalışan İşlevler
- ✅ Build job'ları (artifacts olmadan)
- ✅ Test job'ları (kendi build'lerini yapıyor)
- ✅ Docker build job'ları (Docker socket mount ile)
- ✅ Documentation job'ları (artifacts kullanmıyor)
- ✅ Deployment job (artifacts kullanmıyor)

### Çalışmayan İşlevler
- ❌ Git fetch (external IP erişimi yok)
- ❌ Artifacts upload (artifacts kaldırıldı, sorun değil)
- ❌ Artifacts download (artifacts yok, sorun değil)

---

## 🎯 Sonuç ve Öneri

**Durum:**
Git fetch sorunu Docker network yapısından kaynaklanıyor ve şu anki yapılandırmayla çözülemedi.

**Öneri:**
1. **Kabul Edilebilir Durum:** Artifacts optional yapıldı, pipeline'ın geri kalanı çalışıyor
2. **Gelecekte Çözüm:** Network yapısını yeniden tasarlamak veya runner'ı host network'te çalıştırmak
3. **Alternatif:** Manuel deployment kullanmak (CI/CD pipeline yerine)

**Etkilenen İşlevler:**
- Git fetch çalışmıyor → Pipeline başlayamıyor
- Bu kritik bir sorun, pipeline hiç çalışmıyor

**Acil Çözüm Gerekiyor:**
Git fetch sorunu çözülmeden pipeline çalışamaz. Yukarıdaki çözümlerden birini denemek veya alternatif bir yaklaşım bulmak gerekiyor.

---

**Son Güncelleme:** 1 Ocak 2026

