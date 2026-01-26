# Nginx Container Hızlı Kurulum Adımları

## 🎯 Yapılacaklar

Sunucudaki `docker-compose.yml` dosyasına sadece nginx servisini eklemeniz gerekiyor.

---

## 📝 Adım 1: SSH ile Sunucuya Bağlan

```bash
ssh root@monitrang-server
```

---

## 📝 Adım 2: Docker Compose Dosyasını Düzenle

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
nano docker-compose.yml
```

**`mkdocs` servisinden sonra (satır 353 civarı) şu kodu ekleyin:**

```yaml
  # Nginx Reverse Proxy
  nginx:
    image: nginx:alpine
    container_name: nginx
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/conf.d:/etc/nginx/conf.d:ro
      - ./nginx/ssl:/etc/nginx/ssl:ro
      - /etc/letsencrypt:/etc/letsencrypt:ro
      - /var/www/html:/var/www/html:ro
      - nginx_logs:/var/log/nginx
    networks:
      - mng_network
      - mailu_default
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

**Dosyayı kaydedin:** `Ctrl+O`, `Enter`, `Ctrl+X`

---

## ✅ Adım 3: Yapılandırmayı Test Et

```bash
docker compose config
```

Hata yoksa devam edin.

---

## 🚀 Adım 4: Nginx Container'ını Başlat

```bash
docker compose up -d nginx
```

---

## 🧪 Adım 5: Kontrol Et

```bash
# Container çalışıyor mu?
docker ps | grep nginx

# Nginx yapılandırması doğru mu?
docker exec nginx nginx -t
```

Başarılı çıktı:
```
nginx: the configuration file /etc/nginx/nginx.conf syntax is ok
nginx: configuration file /etc/nginx/nginx.conf test is successful
```

---

## 🎉 Tamamlandı!

Nginx container'ı başarıyla çalışıyor. Artık tüm servisler Nginx reverse proxy üzerinden erişilebilir.

---

## 📋 Kontrol Listesi

- [ ] Nginx servisi docker-compose.yml'e eklendi
- [ ] `docker compose config` hatasız çalıştı
- [ ] Nginx container başarıyla başladı
- [ ] `nginx -t` başarılı
- [ ] Port 80 ve 443 Nginx tarafından kullanılıyor

---

## 🐛 Sorun Olursa

**Nginx başlamıyorsa:**
```bash
docker compose logs nginx
```

**Yapılandırma hatası varsa:**
```bash
docker exec nginx nginx -t
```

**Port çakışması varsa:**
```bash
sudo lsof -i :80
sudo lsof -i :443
# Eski nginx servisini durdurun:
systemctl stop nginx
```

