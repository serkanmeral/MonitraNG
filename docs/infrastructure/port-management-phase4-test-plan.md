# Phase 4: Test ve Doğrulama Planı

**Tarih:** 4 Ocak 2026  
**Durum:** 🔄 Devam Ediyor

---

## 📋 Yapılacaklar

### 1. Nginx Yapılandırma Dosyalarını Oluştur ✅ (Kısmen)
- [x] `nginx/nginx.conf` - Oluşturuldu
- [x] `nginx/ssl/ssl-params.conf` - Oluşturuldu
- [ ] `nginx/conf.d/monitrang.conf` - Boş (doldurulmalı)
- [ ] `nginx/conf.d/mailu.conf` - Boş (doldurulmalı)

### 2. Docker Compose Yapılandırması ✅
- [x] Nginx servisi `docker-compose.yml`'e eklendi
- [x] `nginx_logs` volume eklendi
- [x] `mailu_default` network external olarak eklendi
- [ ] Docker Compose yapılandırması doğrulandı

### 3. Nginx Container'ını Başlat ⏳
- [ ] Nginx yapılandırma dosyalarını doldur
- [ ] `docker compose up -d nginx` çalıştır
- [ ] Container'ın başarıyla başladığını doğrula
- [ ] Nginx yapılandırmasını test et (`nginx -t`)

### 4. Servis Erişim Testleri ⏳
- [ ] `app.monitrang.com` → `mngui:80` test et
- [ ] `api.monitrang.com` → `mnggateway:5000` test et
- [ ] `auth.monitrang.com` → `keycloak:8080` test et
- [ ] `gitlab.monitrang.com` → `gitlab:80` test et
- [ ] `mail.monitrang.com` → `mailu-front-1:80` test et

### 5. Port Kontrolü ⏳
- [ ] Host port 80 ve 443'in sadece Nginx tarafından kullanıldığını doğrula
- [ ] Application servislerinin port mapping'lerinin kaldırıldığını doğrula
- [ ] Container name'lerin çalıştığını doğrula

---

## 🔧 Manuel Adımlar (Gerekirse)

Eğer otomatik dosya kopyalama başarısız olursa:

1. **Nginx Yapılandırma Dosyalarını Kopyala:**
   ```bash
   # Local'den sunucuya kopyala
   scp ApplicationResources/mng_common/nginx/conf.d/monitrang.conf root@monitrang-server:/root/MonitraNG/ApplicationResources/mng_common/nginx/conf.d/
   scp ApplicationResources/mng_common/nginx/conf.d/mailu.conf root@monitrang-server:/root/MonitraNG/ApplicationResources/mng_common/nginx/conf.d/
   ```

2. **Docker Compose Dosyasını Güncelle:**
   ```bash
   # Local'den sunucuya kopyala
   scp ApplicationResources/mng_common/docker-compose.yml root@monitrang-server:/root/MonitraNG/ApplicationResources/mng_common/
   ```

3. **Nginx Container'ını Başlat:**
   ```bash
   ssh root@monitrang-server
   cd /root/MonitraNG/ApplicationResources/mng_common
   docker compose up -d nginx
   docker compose logs nginx
   ```

---

## ✅ Test Komutları

### Container Durumu
```bash
docker ps | grep nginx
docker compose ps nginx
```

### Nginx Yapılandırma Testi
```bash
docker exec nginx nginx -t
```

### Container Name Erişimi
```bash
docker exec nginx ping -c 2 mngui
docker exec nginx ping -c 2 mnggateway
docker exec nginx ping -c 2 keycloak
docker exec nginx ping -c 2 gitlab
```

### Port Kontrolü
```bash
netstat -tlnp | grep -E ':(80|443)'
docker ps --format '{{.Names}}\t{{.Ports}}' | grep -E '80|443'
```

### HTTP Test
```bash
curl -I http://localhost
curl -I https://localhost -k
curl -I http://app.monitrang.com -H "Host: app.monitrang.com"
```

---

## 🐛 Bilinen Sorunlar

1. **Nginx Yapılandırma Dosyaları Boş:**
   - PowerShell heredoc escape karakterleri sorunlu
   - Base64 encoding PowerShell'de sorunlu
   - **Çözüm:** Manuel kopyalama veya scp kullan

2. **Docker Compose Yapılandırması:**
   - Sunucudaki dosya local'deki ile senkronize değil
   - **Çözüm:** Git pull veya manuel kopyalama

---

## 📝 Notlar

- GitLab container içinde port 80'de çalışıyor, `gitlab:80` kullanılmalı (8090 değil)
- Mailu container name'i `mailu-front-1` (Mailu'nun kendi network'ünde)
- Tüm application servisleri `mng_common_mng_network` network'ünde

