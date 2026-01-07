# Nginx Container Manuel Kurulum Rehberi

**Tarih:** 4 Ocak 2026  
**Durum:** Manuel kurulum gerekiyor

---

## 📋 Ön Hazırlık

Nginx yapılandırma dosyaları hazır:
- ✅ `nginx/nginx.conf` - Oluşturuldu
- ✅ `nginx/ssl/ssl-params.conf` - Oluşturuldu  
- ✅ `nginx/conf.d/monitrang.conf` - 365 satır, kopyalandı
- ✅ `nginx/conf.d/mailu.conf` - 70 satır, kopyalandı

---

## 🔧 Adım 1: Docker Compose Dosyasını Düzenle

Sunucuya SSH ile bağlanın:
```bash
ssh root@monitrang-server
```

Docker Compose dosyasını düzenleyin:
```bash
cd /root/MonitraNG/ApplicationResources/mng_common
nano docker-compose.yml
```

**`mkdocs` servisinden sonra (yaklaşık satır 353'ten sonra) şu servisi ekleyin:**

```yaml
  # Nginx Reverse Proxy
  nginx:
    image: nginx:alpine
    container_name: nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      # Nginx configuration files
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/conf.d:/etc/nginx/conf.d:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      # SSL certificates (Let's Encrypt)
      - /etc/letsencrypt:/etc/letsencrypt:ro
      # Let's Encrypt challenge files
      - /var/www/html:/var/www/html:ro
      # Logs
      - nginx_logs:/var/log/nginx
    networks:
      - mng_network
      - mailu_default  # Mailu network for mailu-front-1 access
    restart: unless-stopped
    depends_on:
      - keycloak
      - gitlab
    healthcheck:
      test: ["CMD", "nginx", "-t"]
      interval: 30s
      timeout: 10s
      retries: 3
```

**ÖNEMLİ:** `networks:` bölümünde `mailu_default` network'ünün external olarak tanımlı olduğundan emin olun:

```yaml
networks:
  mailu_default:
    external: true
  mng_network:
    driver: bridge
```

**ÖNEMLİ:** `volumes:` bölümünde `nginx_logs` volume'ünün tanımlı olduğundan emin olun:

```yaml
volumes:
  nginx_logs:
  postgres_data:
  # ... diğer volumes
```

---

## ✅ Adım 2: Yapılandırmayı Doğrula

Docker Compose yapılandırmasını test edin:
```bash
docker compose config
```

Hata yoksa, Nginx servisinin listede olduğunu kontrol edin:
```bash
docker compose config --services | grep nginx
```

---

## 🚀 Adım 3: Nginx Container'ını Başlat

Nginx container'ını başlatın:
```bash
docker compose up -d nginx
```

Container'ın başladığını kontrol edin:
```bash
docker ps | grep nginx
```

Çıktı şöyle olmalı:
```
CONTAINER ID   IMAGE           COMMAND                  CREATED        STATUS          PORTS
xxxxx          nginx:alpine    "/docker-entrypoint.…"   X seconds ago  Up X seconds    0.0.0.0:80->80/tcp, 0.0.0.0:443->443/tcp   nginx
```

---

## 🧪 Adım 4: Nginx Yapılandırmasını Test Et

Nginx yapılandırmasını test edin:
```bash
docker exec nginx nginx -t
```

Başarılı çıktı:
```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

---

## 🔍 Adım 5: Container Name Erişimini Test Et

Nginx container'ının diğer container'lara erişebildiğini test edin:
```bash
# mngui container'ına ping
docker exec nginx ping -c 2 mngui

# mnggateway container'ına ping
docker exec nginx ping -c 2 mnggateway

# keycloak container'ına ping
docker exec nginx ping -c 2 keycloak

# gitlab container'ına ping
docker exec nginx ping -c 2 gitlab

# mailu-front-1 container'ına ping (mailu network'ünde)
docker exec nginx ping -c 2 mailu-front-1
```

**Not:** `mailu-front-1` erişimi başarısız olabilir çünkü Mailu farklı bir network'te. Bu normal, Nginx `mailu_default` network'üne bağlı olduğu için erişebilir.

---

## 🌐 Adım 6: HTTP/HTTPS Erişimini Test Et

Local'den test edin:
```bash
# HTTP test (localhost)
curl -I http://localhost

# HTTPS test (localhost, self-signed cert için -k)
curl -I https://localhost -k

# Domain test (Host header ile)
curl -I http://app.monitrang.com -H "Host: app.monitrang.com"
curl -I http://api.monitrang.com -H "Host: api.monitrang.com"
curl -I http://auth.monitrang.com -H "Host: auth.monitrang.com"
```

---

## 📊 Adım 7: Port Kontrolü

Port 80 ve 443'ün sadece Nginx tarafından kullanıldığını doğrulayın:
```bash
# Host portları kontrol et
netstat -tlnp | grep -E ':(80|443)'

# Docker container portları kontrol et
docker ps --format '{{.Names}}\t{{.Ports}}' | grep -E '80|443'
```

Sadece Nginx container'ı port 80 ve 443'ü kullanmalı.

---

## 🐛 Sorun Giderme

### Nginx Container Başlamıyor

1. **Logları kontrol edin:**
   ```bash
   docker compose logs nginx
   ```

2. **Yapılandırma dosyalarını kontrol edin:**
   ```bash
   ls -la nginx/conf.d/
   ls -la nginx/ssl/
   ```

3. **Docker Compose yapılandırmasını kontrol edin:**
   ```bash
   docker compose config | grep -A 20 nginx
   ```

### Container Name Erişilemiyor

1. **Network'leri kontrol edin:**
   ```bash
   docker network ls
   docker network inspect mng_common_mng_network
   ```

2. **Container'ların aynı network'te olduğunu kontrol edin:**
   ```bash
   docker inspect mngui | grep -A 10 Networks
   docker inspect nginx | grep -A 10 Networks
   ```

### Port Çakışması

Eğer port 80 veya 443 zaten kullanılıyorsa:

1. **Hangi process kullanıyor kontrol edin:**
   ```bash
   sudo lsof -i :80
   sudo lsof -i :443
   ```

2. **Eski Nginx servisini durdurun (eğer varsa):**
   ```bash
   systemctl stop nginx
   systemctl disable nginx
   ```

---

## ✅ Başarı Kriterleri

- [ ] Nginx container başarıyla başladı
- [ ] `nginx -t` komutu başarılı
- [ ] Port 80 ve 443 sadece Nginx tarafından kullanılıyor
- [ ] Container name'ler erişilebilir (ping başarılı)
- [ ] HTTP/HTTPS istekleri Nginx üzerinden çalışıyor

---

## 📝 Notlar

- GitLab container içinde port 80'de çalışıyor, bu yüzden `gitlab:80` kullanılıyor (8090 değil)
- Mailu container'ı `mailu_default` network'ünde, Nginx bu network'e bağlı
- Tüm application servisleri `mng_common_mng_network` network'ünde
- SSL sertifikaları `/etc/letsencrypt` dizininden mount ediliyor

---

## 🎯 Sonraki Adımlar

Nginx başarıyla çalıştıktan sonra:
1. Application servislerinin port mapping'lerini kaldırın (Phase 3'te yapıldı)
2. Servis erişimlerini test edin
3. Production'da test edin

