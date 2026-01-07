# admin.monitrang.com Subdomain Setup

## Genel Bakış

`admin.monitrang.com` subdomain'i, tüm infrastructure admin UI'larını tek bir noktadan HTTP Basic Auth ile korumalı olarak erişilebilir hale getirir.

### Erişilebilir Admin UI'lar

| UI | URL | Açıklama |
|---|---|---|
| **Dashboard** | `https://admin.monitrang.com/` | Admin tools ana sayfası |
| **Portainer** | `https://admin.monitrang.com/portainer/` | Docker container management |
| **RabbitMQ** | `https://admin.monitrang.com/rabbitmq/` | Message queue management |
| **Seq** | `https://admin.monitrang.com/seq/` | Centralized logging |
| **Mongo Express** | `https://admin.monitrang.com/mongo/` | MongoDB web admin |
| **Redis Commander** | `https://admin.monitrang.com/redis/` | Redis management UI |
| **Node-RED** | `https://admin.monitrang.com/nodered/` | Flow-based automation |

## Güvenlik Modeli

### HTTP Basic Authentication
- Tüm admin UI'lar tek bir HTTP Basic Auth ile korunur
- Kullanıcı adı/şifre tarayıcı tarafından hatırlanır
- SSL/TLS ile şifrelenmiş iletişim

### Avantajlar
- ✅ IP bağımlılığı yok (her yerden erişilebilir)
- ✅ Kolay kurulum ve yönetim
- ✅ Tek şifre ile tüm admin UI'lara erişim
- ✅ SSL ile güvenli iletişim
- ✅ Test/development ekipleri için uygun

### Gelecek İyileştirmeler
- **Orta vade**: IP whitelist ekleme (double protection)
- **Uzun vade**: VPN entegrasyonu (production için)

## Kurulum

### 1. DNS Kaydı Ekleme

DNS sağlayıcınızda (örn: Cloudflare) aşağıdaki kaydı ekleyin:

```
Type: A
Name: admin
Value: 45.141.151.52
TTL: Auto (veya 300)
Proxy: Disabled (DNS only)
```

### 2. Sunucuda Kurulum Scripti Çalıştırma

```bash
# Sunucuya SSH ile bağlan
ssh root@monitrang-server

# MonitraNG repo'suna git
cd /root/MonitraNG

# Setup scriptini çalıştır
chmod +x scripts/deployment/setup-admin-subdomain.sh
./scripts/deployment/setup-admin-subdomain.sh
```

Script aşağıdaki işlemleri yapar:
1. DNS kaydını kontrol eder
2. HTTP Basic Auth şifre dosyası oluşturur (`/etc/nginx/.htpasswd`)
3. Nginx config'i test eder
4. Nginx'i reload eder

### 3. Manuel Kurulum (Script kullanmadan)

#### a) HTTP Basic Auth Şifre Dosyası Oluşturma

```bash
# htpasswd kurulumu (eğer yoksa)
apt-get update
apt-get install -y apache2-utils

# Şifre dosyası oluşturma
htpasswd -c /etc/nginx/.htpasswd admin
# Şifre: güçlü bir şifre girin

# İzinleri ayarlama
chmod 644 /etc/nginx/.htpasswd
chown root:root /etc/nginx/.htpasswd
```

#### b) Nginx Config'i Container'a Mount Etme

Config dosyası zaten `ApplicationResources/mng_common/nginx/conf.d/admin.monitrang.conf` konumunda. Docker Compose volume mount ile otomatik olarak container'a aktarılır.

#### c) .htpasswd Dosyasını Container'a Mount Etme

`ApplicationResources/mng_common/docker-compose.yml` dosyasında nginx servisine volume ekleyin:

```yaml
nginx:
  image: nginx:alpine
  container_name: nginx
  volumes:
    # ... mevcut volume'ler ...
    - /etc/nginx/.htpasswd:/etc/nginx/.htpasswd:ro  # HTTP Basic Auth
```

#### d) Nginx'i Yeniden Başlatma

```bash
cd /root/MonitraNG/ApplicationResources/mng_common
docker-compose restart nginx
```

## Test

### 1. DNS Testi

```bash
# DNS kaydının çözümlendiğini kontrol et
host admin.monitrang.com

# Beklenen çıktı:
# admin.monitrang.com has address 45.141.151.52
```

### 2. HTTP Basic Auth Testi

```bash
# Tarayıcıda aç
https://admin.monitrang.com/

# Kullanıcı adı ve şifre sorulmalı
# Doğru şifre ile giriş yapınca admin dashboard açılmalı
```

### 3. Admin UI Testleri

Her bir admin UI'ya erişimi test edin:

```bash
# Portainer
https://admin.monitrang.com/portainer/

# RabbitMQ
https://admin.monitrang.com/rabbitmq/

# Seq
https://admin.monitrang.com/seq/

# Mongo Express
https://admin.monitrang.com/mongo/

# Redis Commander
https://admin.monitrang.com/redis/

# Node-RED
https://admin.monitrang.com/nodered/
```

## Sorun Giderme

### DNS kaydı çözümlenmiyor

**Sorun**: `host admin.monitrang.com` komutu hata veriyor.

**Çözüm**:
1. DNS sağlayıcınızda kaydın eklendiğini kontrol edin
2. DNS propagation'ı bekleyin (5-10 dakika)
3. DNS cache'i temizleyin: `systemd-resolve --flush-caches`

### HTTP Basic Auth çalışmıyor

**Sorun**: Şifre sorulmuyor veya yanlış şifre hatası veriyor.

**Çözüm**:
1. `.htpasswd` dosyasının varlığını kontrol edin:
   ```bash
   docker exec nginx cat /etc/nginx/.htpasswd
   ```
2. Dosya yoksa, volume mount'u kontrol edin ve container'ı restart edin
3. Şifreyi yeniden oluşturun:
   ```bash
   htpasswd -c /etc/nginx/.htpasswd admin
   docker-compose restart nginx
   ```

### Admin UI'lar açılmıyor

**Sorun**: 502 Bad Gateway veya 504 Gateway Timeout hatası.

**Çözüm**:
1. Container'ların çalıştığını kontrol edin:
   ```bash
   docker ps | grep -E "portainer|rabbitmq|seq|mongo_express|redis-commander|nodered"
   ```
2. Container loglarını kontrol edin:
   ```bash
   docker logs portainer
   docker logs rabbitmq
   # ... diğer container'lar
   ```
3. Nginx loglarını kontrol edin:
   ```bash
   docker logs nginx
   docker exec nginx tail -f /var/log/nginx/admin.monitrang.com-error.log
   ```

### SSL sertifikası hatası

**Sorun**: "Your connection is not private" hatası.

**Çözüm**:
1. Wildcard sertifikasının geçerliliğini kontrol edin:
   ```bash
   docker exec nginx openssl x509 -in /etc/letsencrypt/live/monitrang.com/fullchain.pem -noout -text | grep -A2 "Subject Alternative Name"
   ```
2. Sertifika `*.monitrang.com` içeriyorsa, tarayıcı cache'ini temizleyin
3. Sertifika yoksa veya süresi dolmuşsa, yenileyin:
   ```bash
   certbot renew
   ```

## Şifre Yönetimi

### Yeni Kullanıcı Ekleme

```bash
# Mevcut dosyaya yeni kullanıcı ekle (-c flag'i KULLANMA!)
htpasswd /etc/nginx/.htpasswd yeni_kullanici

# Nginx'i reload et
docker exec nginx nginx -s reload
```

### Kullanıcı Silme

```bash
# Kullanıcıyı sil
htpasswd -D /etc/nginx/.htpasswd kullanici_adi

# Nginx'i reload et
docker exec nginx nginx -s reload
```

### Şifre Değiştirme

```bash
# Kullanıcının şifresini değiştir
htpasswd /etc/nginx/.htpasswd kullanici_adi

# Nginx'i reload et
docker exec nginx nginx -s reload
```

### Tüm Kullanıcıları Listeleme

```bash
# Kullanıcı listesini göster
cat /etc/nginx/.htpasswd | cut -d: -f1
```

## Ekip ile Paylaşım

### Erişim Bilgileri

Ekip üyelerine aşağıdaki bilgileri paylaşın:

```
URL: https://admin.monitrang.com
Kullanıcı adı: admin (veya oluşturduğunuz kullanıcı adı)
Şifre: [güvenli bir şekilde paylaşın]

Admin UI'lar:
- Dashboard: https://admin.monitrang.com/
- Portainer: https://admin.monitrang.com/portainer/
- RabbitMQ: https://admin.monitrang.com/rabbitmq/
- Seq: https://admin.monitrang.com/seq/
- Mongo Express: https://admin.monitrang.com/mongo/
- Redis Commander: https://admin.monitrang.com/redis/
- Node-RED: https://admin.monitrang.com/nodered/
```

### Güvenlik Notları

- ⚠️ Şifreyi güvenli bir kanal üzerinden paylaşın (örn: password manager, encrypted message)
- ⚠️ Şifreyi email veya Slack gibi platformlarda paylaşmayın
- ⚠️ Her ekip üyesi için ayrı kullanıcı oluşturmayı düşünün (audit için)
- ⚠️ Şifreleri düzenli olarak değiştirin (örn: 3 ayda bir)

## Nginx Config Detayları

### Path-based Routing

Her admin UI farklı bir path altında erişilebilir:

```nginx
location /portainer/ {
    proxy_pass http://portainer:9000/;
    # ...
}

location /rabbitmq/ {
    rewrite ^/rabbitmq/(.*)$ /$1 break;
    proxy_pass http://rabbitmq:15672;
    # ...
}
```

### HTTP Basic Auth

Tüm location'lar için global auth:

```nginx
server {
    # ...
    auth_basic "MonitraNG Admin Tools - Team Access Only";
    auth_basic_user_file /etc/nginx/.htpasswd;
    # ...
}
```

### WebSocket Desteği

Portainer ve Node-RED için WebSocket desteği:

```nginx
proxy_set_header Upgrade $http_upgrade;
proxy_set_header Connection "upgrade";
```

## İlgili Dosyalar

- **Nginx Config**: `ApplicationResources/mng_common/nginx/conf.d/admin.monitrang.conf`
- **Setup Script**: `scripts/deployment/setup-admin-subdomain.sh`
- **Docker Compose**: `ApplicationResources/mng_common/docker-compose.yml`
- **Dokümantasyon**: `docs/infrastructure/admin-subdomain-setup.md`

## Sonraki Adımlar

1. ✅ admin.monitrang.com subdomain kurulumu
2. ⏳ MinIO için `files.monitrang.com` subdomain kurulumu
3. ⏳ IP whitelist ekleme (opsiyonel, orta vade)
4. ⏳ VPN entegrasyonu (opsiyonel, uzun vade)

## Tarihçe

- **4 Ocak 2026**: İlk kurulum ve dokümantasyon

