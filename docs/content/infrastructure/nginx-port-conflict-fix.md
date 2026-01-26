# Nginx Port Çakışması Çözümü

**Sorun:** GitLab container'ı port 80 ve 443'ü kullanıyor, Nginx başlatılamıyor.

**Çözüm:** GitLab'ın port mapping'lerini kaldırın (Phase 3'te yapılması gerekiyordu).

---

## 🔧 Adım 1: GitLab Port Mapping'lerini Kaldır

SSH ile sunucuya bağlanın:
```bash
ssh root@monitrang-server
```

Docker Compose dosyasını düzenleyin:
```bash
cd /root/MonitraNG/ApplicationResources/mng_common
nano docker-compose.yml
```

**GitLab servisinde `ports:` bölümünü bulun ve şu şekilde değiştirin:**

**ÖNCE (Yanlış):**
```yaml
  gitlab:
    # ... diğer ayarlar
    ports:
      - "80:80"           # HTTP
      - "443:443"         # HTTPS
      - "2222:22"         # SSH (mapped to 2222 to avoid conflict)
```

**SONRA (Doğru):**
```yaml
  gitlab:
    # ... diğer ayarlar
    ports:
      # Port mapping removed - Access via Nginx reverse proxy only
      # - "80:80"           # Removed - Nginx will proxy
      # - "443:443"         # Removed - Nginx will proxy
      - "2222:22"         # SSH (mapped to 2222 to avoid conflict - kept for Git operations)
```

**Dosyayı kaydedin:** `Ctrl+O`, `Enter`, `Ctrl+X`

---

## 🔄 Adım 2: GitLab Container'ını Yeniden Başlat

GitLab container'ını yeniden başlatın (port mapping'leri kaldırmak için):
```bash
docker compose up -d gitlab
```

GitLab'ın port 80 ve 443'ü artık kullanmadığını kontrol edin:
```bash
docker ps | grep gitlab
```

Çıktıda sadece `2222:22` port mapping'i olmalı, `80:80` ve `443:443` olmamalı.

---

## ✅ Adım 3: Nginx Container'ını Başlat

Artık Nginx container'ını başlatabilirsiniz:
```bash
docker compose up -d nginx
```

Nginx'in başarıyla başladığını kontrol edin:
```bash
docker ps | grep nginx
```

---

## 🧪 Adım 4: Port Kontrolü

Port 80 ve 443'ün sadece Nginx tarafından kullanıldığını doğrulayın:
```bash
netstat -tlnp | grep -E ':(80|443)'
```

Çıktıda sadece Nginx container'ı görünmeli:
```
tcp        0      0 0.0.0.0:80              0.0.0.0:*               LISTEN      xxxxx/docker-prox
tcp        0      0 0.0.0.0:443             0.0.0.0:*               LISTEN      xxxxx/docker-prox
```

---

## 📝 Notlar

- GitLab artık sadece Nginx reverse proxy üzerinden erişilebilir (`gitlab.monitrang.com`)
- SSH portu (`2222:22`) korundu, Git operations için gerekli
- GitLab container içinde hala port 80'de çalışıyor, sadece host port mapping'i kaldırıldı

---

## 🎯 Sonuç

- ✅ GitLab port mapping'leri kaldırıldı
- ✅ Nginx port 80 ve 443'ü kullanıyor
- ✅ GitLab Nginx üzerinden erişilebilir

