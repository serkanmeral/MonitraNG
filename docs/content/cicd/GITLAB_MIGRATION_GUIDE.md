# GitLab Hosting Makinesine Taşıma Rehberi

**Durum:** GitLab şu anda localhost'ta Docker container olarak çalışıyor  
**Hedef:** Hosting makinesine taşıma

---

## 📋 Taşıma Seçenekleri

### Seçenek 1: Yedekleme ve Restore (Önerilen - Mevcut Veriler Korunur)
- ✅ Tüm projeler, kullanıcılar, runner'lar korunur
- ✅ Geçmiş commit'ler ve branch'ler korunur
- ⚠️ Daha uzun sürer (backup/restore işlemi)

### Seçenek 2: Yeni Kurulum (Temiz Başlangıç)
- ✅ Daha hızlı
- ✅ Temiz kurulum
- ❌ Mevcut veriler kaybolur (sadece repository'ler yeniden push edilir)

---

## 🔄 Seçenek 1: Yedekleme ve Restore (Detaylı)

### Adım 1: Mevcut GitLab'dan Yedekleme

#### 1.1 GitLab Backup Oluşturma

```bash
# GitLab container'ına bağlan
docker exec -it gitlab bash

# Backup oluştur
gitlab-backup create

# Backup dosyası şu konumda olacak:
# /var/opt/gitlab/backups/
```

#### 1.2 Backup Dosyasını Host'a Kopyalama

```bash
# Backup dosyasını container'dan host'a kopyala
docker cp gitlab:/var/opt/gitlab/backups/[BACKUP_FILE].tar /path/to/backup/

# Örnek:
docker cp gitlab:/var/opt/gitlab/backups/1735420800_2024_12_28_gitlab_backup.tar ./gitlab-backup.tar
```

#### 1.3 Config Dosyalarını Yedekleme

```bash
# GitLab config dosyalarını yedekle
docker cp gitlab:/etc/gitlab/gitlab.rb ./gitlab.rb.backup
docker cp gitlab:/etc/gitlab/gitlab-secrets.json ./gitlab-secrets.json.backup
```

---

### Adım 2: Hosting Makinesine Hazırlık

#### 2.1 Sunucu Gereksinimleri

**Minimum Gereksinimler:**
- CPU: 4 cores
- RAM: 4GB (GitLab için minimum)
- Disk: 50GB+ (GitLab + backup için)
- OS: Ubuntu 22.04 LTS veya Debian 11+ (önerilen)

**Önerilen:**
- CPU: 8 cores
- RAM: 8GB+
- Disk: 100GB+ SSD

#### 2.2 Docker Kurulumu

```bash
# SSH ile sunucuya bağlan
ssh user@your-server-ip

# Docker kurulumu
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Docker Compose kurulumu
sudo apt-get update
sudo apt-get install docker-compose-plugin

# Docker servisini başlat
sudo systemctl start docker
sudo systemctl enable docker

# Kullanıcıyı docker grubuna ekle
sudo usermod -aG docker $USER
# Logout/login yap veya:
newgrp docker
```

#### 2.3 GitLab Docker Compose Dosyası Oluşturma

Hosting makinesinde yeni bir klasör oluşturun:

```bash
mkdir -p ~/gitlab
cd ~/gitlab
```

`docker-compose.yml` dosyası oluşturun:

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
    hostname: gitlab.yourdomain.com  # ⚠️ Domain'inizi buraya yazın
    environment:
      GITLAB_OMNIBUS_CONFIG: |
        external_url 'https://gitlab.yourdomain.com'  # ⚠️ HTTPS için domain
        # veya HTTP için: external_url 'http://your-server-ip'
        gitlab_rails['gitlab_shell_ssh_port'] = 2222
        postgresql['enable'] = false
        gitlab_rails['db_adapter'] = 'postgresql'
        gitlab_rails['db_encoding'] = 'unicode'
        gitlab_rails['db_host'] = 'gitlab-postgres'
        gitlab_rails['db_port'] = 5432
        gitlab_rails['db_username'] = 'gitlab'
        gitlab_rails['db_password'] = 'gitlab123'
        gitlab_rails['db_database'] = 'gitlab'
        redis['enable'] = false
        gitlab_rails['redis_host'] = 'gitlab-redis'
        gitlab_rails['redis_port'] = 6379
        gitlab_rails['redis_password'] = 'gitlab123'
        gitlab_rails['redis_database'] = 0
        # Reduce memory usage
        puma['worker_processes'] = 2
        sidekiq['max_concurrency'] = 10
        # GitLab Pages configuration
        gitlab_pages['enable'] = true
        pages_external_url 'https://gitlab.yourdomain.com'
        gitlab_pages['external_http'] = ['0.0.0.0:8090']
        pages_nginx['enable'] = true
    ports:
      - "80:80"           # HTTP
      - "443:443"         # HTTPS
      - "2222:22"         # SSH
      - "8090:8090"       # GitLab Pages HTTP
    volumes:
      - gitlab_config:/etc/gitlab
      - gitlab_logs:/var/log/gitlab
      - gitlab_data:/var/opt/gitlab
    depends_on:
      - gitlab-postgres
      - gitlab-redis
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

---

### Adım 3: GitLab'ı Başlatma ve Restore

#### 3.1 GitLab'ı İlk Kez Başlatma

```bash
cd ~/gitlab
docker compose up -d

# GitLab'ın hazır olmasını bekle (5-10 dakika)
docker logs -f gitlab
# "gitlab Reconfigured!" mesajını görünce Ctrl+C ile çık
```

#### 3.2 Backup Dosyasını Kopyalama

```bash
# Backup dosyasını sunucuya kopyala (SCP veya SFTP ile)
# Örnek SCP:
scp gitlab-backup.tar user@your-server-ip:~/gitlab/

# Backup dosyasını container'a kopyala
docker cp ~/gitlab/gitlab-backup.tar gitlab:/var/opt/gitlab/backups/
```

#### 3.3 Restore İşlemi

```bash
# GitLab container'ına bağlan
docker exec -it gitlab bash

# Backup dosyasının izinlerini düzelt
chown git:git /var/opt/gitlab/backups/[BACKUP_FILE].tar
chmod 600 /var/opt/gitlab/backups/[BACKUP_FILE].tar

# Restore işlemi
gitlab-backup restore BACKUP=[BACKUP_FILE_WITHOUT_EXTENSION]

# Örnek:
# Backup dosyası: 1735420800_2024_12_28_gitlab_backup.tar
# Komut: gitlab-backup restore BACKUP=1735420800_2024_12_28

# Config dosyalarını restore et
exit  # Container'dan çık

# Config dosyalarını kopyala
docker cp gitlab.rb.backup gitlab:/etc/gitlab/gitlab.rb
docker cp gitlab-secrets.json.backup gitlab:/etc/gitlab/gitlab-secrets.json

# GitLab'ı yeniden yapılandır
docker exec -it gitlab gitlab-ctl reconfigure
docker exec -it gitlab gitlab-ctl restart
```

---

### Adım 4: GitLab Runner'ı Yeniden Kaydetme

```bash
# GitLab'dan yeni runner token al
# GitLab UI > Settings > CI/CD > Runners > "Set up a specific runner manually"

# Runner'ı kaydet
docker exec -it gitlab-runner gitlab-runner register

# Sorular:
# - GitLab instance URL: https://gitlab.yourdomain.com (veya http://your-server-ip)
# - Registration token: (GitLab'dan aldığınız token)
# - Description: monitrang-runner
# - Tags: docker
# - Executor: docker
# - Default Docker image: docker:latest
```

---

### Adım 5: Local Repository Remote URL'lerini Güncelleme

```bash
# Local makinenizde
cd C:\Serkan\iSIM\MonitraNG

# GitLab remote'u güncelle
git remote set-url gitlab https://gitlab.yourdomain.com/root/MonitraNG.git
# veya HTTP için:
git remote set-url gitlab http://your-server-ip/root/MonitraNG.git

# Test et
git remote -v

# Push test
git push gitlab main
```

---

## 🆕 Seçenek 2: Yeni Kurulum (Temiz Başlangıç)

### Adım 1: Hosting Makinesinde GitLab Kurulumu

Yukarıdaki "Adım 2: Hosting Makinesine Hazırlık" bölümünü takip edin.

### Adım 2: GitLab'ı Başlatma

```bash
cd ~/gitlab
docker compose up -d

# GitLab'ın hazır olmasını bekle
docker logs -f gitlab
```

### Adım 3: İlk Kurulum

1. Tarayıcıda `http://your-server-ip` veya `https://gitlab.yourdomain.com` açın
2. Root şifresini belirleyin
3. Giriş yapın (root / belirlediğiniz şifre)
4. MonitraNG projesini oluşturun

### Adım 4: Repository'leri Push Etme

```bash
# Local makinenizde
cd C:\Serkan\iSIM\MonitraNG

# GitLab remote ekle
git remote add gitlab https://gitlab.yourdomain.com/root/MonitraNG.git

# Push et
git push gitlab main --all
git push gitlab main --tags
```

### Adım 5: GitLab Runner Kurulumu

Yukarıdaki "Adım 4: GitLab Runner'ı Yeniden Kaydetme" bölümünü takip edin.

---

## 🔒 Güvenlik ve SSL

### Let's Encrypt SSL Sertifikası (Önerilen)

```bash
# Certbot kurulumu
sudo apt-get update
sudo apt-get install certbot python3-certbot-nginx

# Nginx reverse proxy kurulumu (GitLab önünde)
sudo apt-get install nginx

# Nginx config oluştur
sudo nano /etc/nginx/sites-available/gitlab

# İçerik:
server {
    listen 80;
    server_name gitlab.yourdomain.com;

    location / {
        proxy_pass http://localhost:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}

# Enable site
sudo ln -s /etc/nginx/sites-available/gitlab /etc/nginx/sites-enabled/
sudo nginx -t
sudo systemctl restart nginx

# SSL sertifikası al
sudo certbot --nginx -d gitlab.yourdomain.com
```

---

## 📊 Karşılaştırma

| Özellik | Seçenek 1: Backup/Restore | Seçenek 2: Yeni Kurulum |
|---------|---------------------------|-------------------------|
| Süre | 1-2 saat | 30 dakika |
| Veri Korunur | ✅ Evet | ❌ Hayır |
| Geçmiş Commit'ler | ✅ Korunur | ❌ Sadece push edilenler |
| Runner'lar | ✅ Korunur | ❌ Yeniden kayıt |
| Kullanıcılar | ✅ Korunur | ❌ Yeniden oluştur |
| Önerilen | ✅ Mevcut veriler önemliyse | ✅ Temiz başlangıç istiyorsanız |

---

## ⚠️ Önemli Notlar

1. **Backup Dosyası Boyutu:** GitLab backup dosyası büyük olabilir (GB'lar). Transfer süresini göz önünde bulundurun.

2. **DNS Yapılandırması:** Domain kullanıyorsanız, DNS kayıtlarını güncelleyin:
   ```
   A Record: gitlab.yourdomain.com → your-server-ip
   ```

3. **Firewall:** Sunucuda gerekli portları açın:
   ```bash
   sudo ufw allow 80/tcp
   sudo ufw allow 443/tcp
   sudo ufw allow 2222/tcp
   sudo ufw allow 8090/tcp
   ```

4. **Disk Alanı:** GitLab çok disk alanı kullanır. Yeterli alan olduğundan emin olun.

5. **Memory:** GitLab en az 4GB RAM gerektirir. Sunucunuzun yeterli RAM'i olduğundan emin olun.

---

## 🎯 Sonraki Adımlar

1. ✅ GitLab'ı hosting makinesine taşı
2. ✅ SSL sertifikası kur (Let's Encrypt)
3. ✅ GitLab Runner'ı yeniden kaydet
4. ✅ Local repository remote URL'lerini güncelle
5. ✅ Pipeline'ların çalıştığını test et

---

**Son Güncelleme:** 28 Aralık 2024

