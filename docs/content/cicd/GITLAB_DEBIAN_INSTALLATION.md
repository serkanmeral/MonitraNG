# GitLab Debian Kurulum Rehberi

**Hedef:** Temiz Debian makineye GitLab CE kurulumu  
**Durum:** Yeni kurulum (temiz başlangıç)

---

## 📋 Ön Gereksinimler

### Sistem Gereksinimleri

**Minimum:**
- CPU: 4 cores
- RAM: 4GB
- Disk: 50GB+ SSD
- OS: Debian 11 (Bullseye) veya Debian 12 (Bookworm)

**Önerilen:**
- CPU: 8 cores
- RAM: 8GB+
- Disk: 100GB+ SSD

### Gerekli Portlar

- `80` - HTTP
- `443` - HTTPS
- `22` - SSH (GitLab için 2222'ye map edilecek)
- `8090` - GitLab Pages

---

## 🔧 Adım 1: Debian Sistem Güncellemesi

```bash
# SSH ile sunucuya bağlan
ssh user@your-server-ip

# Sistem güncellemesi
sudo apt update
sudo apt upgrade -y

# Gerekli paketleri kur
sudo apt install -y curl wget git ca-certificates gnupg lsb-release
```

---

## 🐳 Adım 2: Docker Kurulumu

### 2.1 Docker Repository Ekleme

```bash
# Docker'ın resmi GPG key'ini ekle
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Docker repository ekle
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null
```

### 2.2 Docker Kurulumu

```bash
# Paket listesini güncelle
sudo apt update

# Docker Engine, CLI ve Containerd kur
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Docker servisini başlat ve otomatik başlatmayı etkinleştir
sudo systemctl start docker
sudo systemctl enable docker

# Kullanıcıyı docker grubuna ekle (sudo olmadan docker kullanmak için)
sudo usermod -aG docker $USER

# Yeni grup ayarlarını aktif et
newgrp docker

# Docker kurulumunu test et
docker --version
docker compose version
```

---

## 📁 Adım 3: GitLab Klasör Yapısı Oluşturma

```bash
# GitLab için klasör oluştur
mkdir -p ~/gitlab
cd ~/gitlab
```

---

## 📝 Adım 4: Docker Compose Dosyası Oluşturma

`docker-compose.yml` dosyası oluşturun:

```bash
nano docker-compose.yml
```

Aşağıdaki içeriği yapıştırın (domain'inizi veya IP'nizi güncelleyin):

```yaml
version: '3.8'

services:
  # PostgreSQL for GitLab
  gitlab-postgres:
    image: postgres:16-alpine
    container_name: gitlab-postgres
    environment:
      POSTGRES_DB: gitlab
      POSTGRES_USER: gitlab
      POSTGRES_PASSWORD: gitlab123
      POSTGRES_INITDB_ARGS: "-E UTF8"
    volumes:
      - gitlab_postgres_data:/var/lib/postgresql/data
    networks:
      - gitlab_network
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U gitlab"]
      interval: 30s
      timeout: 10s
      retries: 3

  # Redis for GitLab
  gitlab-redis:
    image: redis:7-alpine
    container_name: gitlab-redis
    command: redis-server --requirepass gitlab123 --maxmemory 256mb --maxmemory-policy allkeys-lru
    volumes:
      - gitlab_redis_data:/data
    networks:
      - gitlab_network
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "redis-cli", "--raw", "incr", "ping"]
      interval: 30s
      timeout: 10s
      retries: 3

  # GitLab CE
  gitlab:
    image: gitlab/gitlab-ce:latest
    container_name: gitlab
    hostname: gitlab.yourdomain.com  # ⚠️ Domain'inizi buraya yazın veya IP kullanın
    environment:
      GITLAB_OMNIBUS_CONFIG: |
        # ⚠️ Domain kullanıyorsanız HTTPS, IP kullanıyorsanız HTTP
        external_url 'https://gitlab.yourdomain.com'
        # veya IP için: external_url 'http://YOUR_SERVER_IP'
        
        gitlab_rails['gitlab_shell_ssh_port'] = 2222
        
        # PostgreSQL yapılandırması
        postgresql['enable'] = false
        gitlab_rails['db_adapter'] = 'postgresql'
        gitlab_rails['db_encoding'] = 'unicode'
        gitlab_rails['db_host'] = 'gitlab-postgres'
        gitlab_rails['db_port'] = 5432
        gitlab_rails['db_username'] = 'gitlab'
        gitlab_rails['db_password'] = 'gitlab123'
        gitlab_rails['db_database'] = 'gitlab'
        
        # Redis yapılandırması
        redis['enable'] = false
        gitlab_rails['redis_host'] = 'gitlab-redis'
        gitlab_rails['redis_port'] = 6379
        gitlab_rails['redis_password'] = 'gitlab123'
        gitlab_rails['redis_database'] = 0
        
        # Memory optimizasyonu (küçük sunucular için)
        puma['worker_processes'] = 2
        sidekiq['max_concurrency'] = 10
        
        # GitLab Pages yapılandırması
        gitlab_pages['enable'] = true
        pages_external_url 'https://gitlab.yourdomain.com'
        gitlab_pages['external_http'] = ['0.0.0.0:8090']
        pages_nginx['enable'] = true
        
        # Email yapılandırması (opsiyonel - SMTP ayarlarınızı ekleyin)
        # gitlab_rails['smtp_enable'] = true
        # gitlab_rails['smtp_address'] = "smtp.example.com"
        # gitlab_rails['smtp_port'] = 587
        # gitlab_rails['smtp_user_name'] = "your-email@example.com"
        # gitlab_rails['smtp_password'] = "your-password"
        # gitlab_rails['smtp_domain'] = "example.com"
        # gitlab_rails['smtp_authentication'] = "login"
        # gitlab_rails['smtp_enable_starttls_auto'] = true
    ports:
      - "80:80"           # HTTP
      - "443:443"         # HTTPS
      - "2222:22"         # SSH (GitLab için)
      - "8090:8090"       # GitLab Pages HTTP
    volumes:
      - gitlab_config:/etc/gitlab
      - gitlab_logs:/var/log/gitlab
      - gitlab_data:/var/opt/gitlab
    depends_on:
      gitlab-postgres:
        condition: service_healthy
      gitlab-redis:
        condition: service_healthy
    networks:
      - gitlab_network
    restart: unless-stopped
    shm_size: '256m'

  # GitLab Runner
  gitlab-runner:
    image: gitlab/gitlab-runner:latest
    container_name: gitlab-runner
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
      - gitlab_runner_config:/etc/gitlab-runner
    environment:
      - DOCKER_HOST=unix:///var/run/docker.sock
    networks:
      - gitlab_network
    restart: unless-stopped
    depends_on:
      - gitlab

networks:
  gitlab_network:
    driver: bridge

volumes:
  gitlab_postgres_data:
  gitlab_redis_data:
  gitlab_config:
  gitlab_logs:
  gitlab_data:
  gitlab_runner_config:
```

**Önemli:** `hostname` ve `external_url` değerlerini kendi domain'inize veya IP'nize göre güncelleyin!

---

## 🚀 Adım 5: GitLab'ı Başlatma

```bash
cd ~/gitlab

# GitLab'ı başlat
docker compose up -d

# Logları takip et (GitLab'ın hazır olmasını bekle)
docker logs -f gitlab
```

**Bekleme Süresi:** GitLab ilk başlatmada 5-10 dakika sürebilir. Şu mesajı görünce hazır demektir:
```
gitlab Reconfigured!
```

`Ctrl+C` ile log takibinden çıkın.

---

## 🔍 Adım 6: GitLab Durumunu Kontrol Etme

```bash
# Container'ların çalıştığını kontrol et
docker ps

# GitLab health check
docker exec gitlab gitlab-ctl status

# GitLab'ın hazır olduğunu kontrol et
curl http://localhost
# veya domain kullanıyorsanız:
curl https://gitlab.yourdomain.com
```

---

## 🔐 Adım 7: İlk Kurulum ve Root Şifresi

1. **Tarayıcıda GitLab'ı açın:**
   - IP kullanıyorsanız: `http://YOUR_SERVER_IP`
   - Domain kullanıyorsanız: `https://gitlab.yourdomain.com`

2. **Root şifresini belirleyin:**
   - İlk açılışta root şifresi belirleme ekranı çıkacak
   - Güçlü bir şifre belirleyin (en az 8 karakter)

3. **Giriş yapın:**
   - Username: `root`
   - Password: Belirlediğiniz şifre

---

## 📦 Adım 8: MonitraNG Projesini Oluşturma

1. GitLab'da giriş yaptıktan sonra:
   - **"New project"** veya **"Create a project"** butonuna tıklayın
   - **"Create blank project"** seçeneğini seçin
   - **Project name:** `MonitraNG`
   - **Project slug:** `MonitraNG` (otomatik)
   - **Visibility Level:** Private (veya istediğiniz seviye)
   - **"Create project"** butonuna tıklayın

2. **Repository URL'ini not edin:**
   - Örnek: `https://gitlab.yourdomain.com/root/MonitraNG.git`
   - veya: `http://YOUR_SERVER_IP/root/MonitraNG.git`

---

## 🔄 Adım 9: Local Repository'yi GitLab'a Push Etme

### 9.1 Local Makinenizde (Windows)

```powershell
cd C:\Serkan\iSIM\MonitraNG

# GitLab remote ekle
git remote add gitlab https://gitlab.yourdomain.com/root/MonitraNG.git
# veya IP için:
git remote add gitlab http://YOUR_SERVER_IP/root/MonitraNG.git

# Remote'ları kontrol et
git remote -v

# Tüm branch'leri push et
git push gitlab main --all

# Tag'leri push et
git push gitlab main --tags
```

### 9.2 Personal Access Token (Gerekirse)

Eğer push sırasında authentication hatası alırsanız:

1. GitLab'da: **User Settings > Access Tokens**
2. **Token name:** `monitrang-local`
3. **Scopes:** `write_repository`, `read_repository` işaretleyin
4. **Create personal access token** butonuna tıklayın
5. Token'ı kopyalayın (bir daha gösterilmeyecek!)

6. Push yaparken token kullanın:
```powershell
# Token ile push
git push https://oauth2:YOUR_TOKEN@gitlab.yourdomain.com/root/MonitraNG.git main
```

---

## 🤖 Adım 10: GitLab Runner Kurulumu

### 10.1 Runner Token Alma

1. GitLab'da projeye gidin: `MonitraNG`
2. **Settings > CI/CD** sekmesine gidin
3. **Runners** bölümünü genişletin
4. **"Set up a specific runner manually"** bölümünde
5. **Registration token**'ı kopyalayın

### 10.2 Runner'ı Kaydetme

```bash
# Sunucuda (SSH ile bağlı)
docker exec -it gitlab-runner gitlab-runner register
```

**Sorular ve Cevaplar:**

```
Enter the GitLab instance URL (for example, https://gitlab.com/):
> https://gitlab.yourdomain.com
# veya IP için: http://YOUR_SERVER_IP

Enter the registration token:
> [GitLab'dan kopyaladığınız token]

Enter a description for the runner:
> monitrang-runner

Enter tags for the runner (comma separated):
> docker

Enter optional executor: docker, shell, docker-ssh, ssh, virtualbox, docker+machine, docker-ssh+machine, kubernetes, custom, parallels:
> docker

Enter the default Docker image (for example, ruby:2.7):
> docker:latest
```

### 10.3 Runner Durumunu Kontrol Etme

```bash
# Runner'ın çalıştığını kontrol et
docker exec gitlab-runner gitlab-runner verify

# Runner listesi
docker exec gitlab-runner gitlab-runner list
```

GitLab UI'da **Settings > CI/CD > Runners** bölümünde runner'ınızı görmelisiniz.

---

## 🔒 Adım 11: SSL Sertifikası (Domain Kullanıyorsanız)

### 11.1 Nginx Reverse Proxy Kurulumu

```bash
# Nginx kur
sudo apt install -y nginx

# Nginx config oluştur
sudo nano /etc/nginx/sites-available/gitlab
```

Aşağıdaki içeriği yapıştırın:

```nginx
server {
    listen 80;
    server_name gitlab.yourdomain.com;

    location / {
        proxy_pass http://localhost:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        
        # WebSocket desteği
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

```bash
# Config'i aktif et
sudo ln -s /etc/nginx/sites-available/gitlab /etc/nginx/sites-enabled/

# Nginx config test
sudo nginx -t

# Nginx'i başlat
sudo systemctl restart nginx
sudo systemctl enable nginx
```

### 11.2 Let's Encrypt SSL Sertifikası

```bash
# Certbot kur
sudo apt install -y certbot python3-certbot-nginx

# SSL sertifikası al
sudo certbot --nginx -d gitlab.yourdomain.com

# Otomatik yenileme test
sudo certbot renew --dry-run
```

---

## 🔥 Adım 12: Firewall Yapılandırması

```bash
# UFW firewall kur (eğer yoksa)
sudo apt install -y ufw

# Gerekli portları aç
sudo ufw allow 22/tcp    # SSH
sudo ufw allow 80/tcp    # HTTP
sudo ufw allow 443/tcp   # HTTPS
sudo ufw allow 2222/tcp  # GitLab SSH
sudo ufw allow 8090/tcp  # GitLab Pages

# Firewall'u aktif et
sudo ufw enable

# Durumu kontrol et
sudo ufw status
```

---

## ✅ Adım 13: Test ve Doğrulama

### 13.1 GitLab Erişimi

```bash
# HTTP test
curl -I http://YOUR_SERVER_IP
# veya
curl -I https://gitlab.yourdomain.com

# GitLab health check
docker exec gitlab gitlab-ctl status
```

### 13.2 CI/CD Pipeline Testi

1. GitLab'da projeye gidin
2. Herhangi bir dosyayı düzenleyin ve commit edin
3. **CI/CD > Pipelines** sekmesine gidin
4. Pipeline'ın çalıştığını kontrol edin

### 13.3 Runner Testi

```bash
# Runner'ın çalıştığını kontrol et
docker exec gitlab-runner gitlab-runner verify

# Runner logları
docker logs gitlab-runner
```

---

## 📝 Adım 14: Local Repository Remote URL Güncelleme

### 14.1 Dual Sync Yapılandırması (GitHub + GitLab)

```powershell
# Local makinenizde
cd C:\Serkan\iSIM\MonitraNG

# Mevcut remote'ları kontrol et
git remote -v

# Origin'i hem GitHub hem GitLab'a push edecek şekilde yapılandır
git remote set-url --add --push origin https://github.com/serkanmeral/MonitraNG.git
git remote set-url --add --push origin https://gitlab.yourdomain.com/root/MonitraNG.git

# Test et
git remote -v

# Push test
git push origin main
```

---

## 🔧 Adım 15: GitLab Yapılandırma Optimizasyonu

### 15.1 Memory Kullanımını Optimize Etme

Küçük sunucular için `docker-compose.yml` içinde zaten optimize edilmiş ayarlar var. Daha fazla optimizasyon için:

```bash
# GitLab container'ına bağlan
docker exec -it gitlab bash

# Config dosyasını düzenle
vi /etc/gitlab/gitlab.rb

# Ek optimizasyonlar (opsiyonel):
# puma['worker_processes'] = 1  # Daha az worker
# sidekiq['max_concurrency'] = 5  # Daha az concurrency

# Değişiklikleri uygula
gitlab-ctl reconfigure
gitlab-ctl restart
```

---

## 📊 Monitoring ve Bakım

### Disk Kullanımını Kontrol Etme

```bash
# Docker volume'ların boyutunu kontrol et
docker system df -v

# GitLab backup oluşturma (düzenli yedekleme için)
docker exec -it gitlab gitlab-backup create
```

### Log Kontrolü

```bash
# GitLab logları
docker logs gitlab --tail 100

# Runner logları
docker logs gitlab-runner --tail 100
```

---

## 🆘 Sorun Giderme

### GitLab Başlamıyor

```bash
# Container durumunu kontrol et
docker ps -a

# Logları incele
docker logs gitlab

# Container'ı yeniden başlat
docker compose restart gitlab
```

### Runner Bağlanamıyor

```bash
# Runner'ı yeniden kaydet
docker exec -it gitlab-runner gitlab-runner register

# Runner'ı restart et
docker compose restart gitlab-runner
```

### SSL Sertifikası Sorunları

```bash
# Certbot logları
sudo tail -f /var/log/letsencrypt/letsencrypt.log

# Nginx config test
sudo nginx -t

# Nginx restart
sudo systemctl restart nginx
```

---

## 📋 Özet Checklist

- [ ] Debian sistem güncellemesi yapıldı
- [ ] Docker ve Docker Compose kuruldu
- [ ] GitLab klasör yapısı oluşturuldu
- [ ] `docker-compose.yml` dosyası oluşturuldu ve yapılandırıldı
- [ ] GitLab başlatıldı ve hazır oldu
- [ ] Root şifresi belirlendi
- [ ] MonitraNG projesi oluşturuldu
- [ ] Local repository GitLab'a push edildi
- [ ] GitLab Runner kaydedildi ve çalışıyor
- [ ] SSL sertifikası kuruldu (domain kullanıyorsanız)
- [ ] Firewall yapılandırıldı
- [ ] CI/CD pipeline test edildi
- [ ] Local repository remote URL'leri güncellendi

---

## 🎯 Sonraki Adımlar

1. ✅ GitLab'ı hosting makinesine kur
2. ✅ SSL sertifikası kur (domain varsa)
3. ✅ Repository'leri push et
4. ✅ Runner'ı kaydet
5. ✅ Pipeline'ları test et
6. ✅ Düzenli backup stratejisi oluştur

---

**Son Güncelleme:** 28 Aralık 2024  
**OS:** Debian 11/12  
**GitLab Version:** Latest CE

