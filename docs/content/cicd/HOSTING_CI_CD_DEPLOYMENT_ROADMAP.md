# Hosting CI/CD Deployment Yol Haritası

**Hedef:** Hosting sunucusunda GitLab CI/CD kurulumu ve yapılandırması  
**Durum:** Rehber hazır - Sunucu formatlanacak, kurulum bekliyor  
**Tarih:** 15 Ocak 2025

---

## ⚠️ Önemli Not

**Sunucu Durumu:** Sunucuda sorun tespit edildi, formatlama planlandı.  
**OS Seçimi:** Debian 12 (Bookworm) veya Ubuntu 22.04 LTS - karar verilecek.  
**Kurulum:** Sunucu hazır olduğunda bu rehber adım adım takip edilecek.

---

## 📋 İçindekiler

1. [Minimum Sistem Gereksinimleri](#minimum-sistem-gereksinimleri)
2. [Ön Hazırlık Checklist](#ön-hazırlık-checklist)
3. [Adım Adım Deployment Yol Haritası](#adım-adım-deployment-yol-haritası)
4. [Doğrulama ve Test](#doğrulama-ve-test)
5. [Sorun Giderme](#sorun-giderme)

---

## 🖥️ Minimum Sistem Gereksinimleri

### CI/CD İçin Minimum Gereksinimler

**Sadece GitLab CI/CD için (MonitraNG uygulamaları hariç):**

| Kaynak | Minimum | Önerilen | Açıklama |
|--------|---------|----------|----------|
| **RAM** | 4 GB | 8 GB | GitLab CE + Runner için |
| **CPU** | 2 Core | 4 Core | Pipeline'lar için |
| **Disk** | 50 GB SSD | 100 GB SSD | GitLab data + artifacts |
| **Network** | 100 Mbps | 1 Gbps | Docker image pull için |

**MonitraNG Uygulamaları + CI/CD (Toplam):**

| Kaynak | Minimum | Önerilen | Açıklama |
|--------|---------|----------|----------|
| **RAM** | 20 GB | 32 GB | Infrastructure + Apps + GitLab |
| **CPU** | 4 Core | 8 Core | Tüm servisler için |
| **Disk** | 150 GB SSD | 200 GB SSD | Sistem + Veri + GitLab |
| **Network** | 100 Mbps | 1 Gbps | İç ağ yeterli |

### Önemli Notlar

- **GitLab CE:** Minimum 4 GB RAM gerektirir (küçük projeler için)
- **GitLab Runner:** Docker executor kullanıyorsa, her job için ekstra RAM gerekir
- **Artifacts:** Build çıktıları disk alanı kullanır (1 saat sonra silinir)
- **Docker Images:** Pipeline'lar Docker image'ları pull eder (network bandwidth)

---

## ✅ Ön Hazırlık Checklist

### Hosting Sunucusu Bilgileri

Aşağıdaki bilgileri hazırlayın:

- [ ] **Sunucu IP Adresi:** `_________________`
- [ ] **SSH Kullanıcı Adı:** `_________________` (genellikle `root` veya `ubuntu`)
- [ ] **SSH Port:** `_________________` (genellikle `22`)
- [ ] **Domain (varsa):** `_________________` (örn: `gitlab.yourdomain.com`)
- [ ] **İşletim Sistemi:** `_________________` (Debian 11/12 veya Ubuntu 22.04 önerilir)
- [ ] **RAM:** `_________________` GB
- [ ] **CPU:** `_________________` Core
- [ ] **Disk:** `_________________` GB

### Gerekli Bilgiler

- [ ] SSH erişimi aktif ve test edildi
- [ ] Sunucuya root veya sudo erişimi var
- [ ] Firewall portları açık (22, 80, 443, 2222, 8090)
- [ ] Domain DNS kayıtları yapıldı (domain kullanıyorsanız)

---

## 🗺️ Adım Adım Deployment Yol Haritası

### **Faz 1: Sunucu Hazırlığı** (30-45 dakika)

#### Adım 1.1: SSH Bağlantısı ve Sistem Kontrolü

```bash
# Windows PowerShell'den SSH ile bağlan
ssh user@your-server-ip

# Sistem bilgilerini kontrol et
uname -a
free -h
df -h
lscpu
```

**Kontrol Edilecekler:**
- [ ] İşletim sistemi: Debian 11/12 veya Ubuntu 22.04
- [ ] RAM: Minimum 4 GB (CI/CD için) veya 20 GB (tam sistem için)
- [ ] Disk: Minimum 50 GB boş alan
- [ ] Network: İnternet bağlantısı var

#### Adım 1.2: Sistem Güncellemesi

```bash
# Sistem güncellemesi
sudo apt update
sudo apt upgrade -y

# Gerekli paketleri kur
sudo apt install -y curl wget git ca-certificates gnupg lsb-release nano
```

**Beklenen Süre:** 5-10 dakika

#### Adım 1.3: Docker Kurulumu

```bash
# Docker repository ekle
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Docker repository ekle (Debian için)
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian \
  $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Ubuntu için (eğer Ubuntu kullanıyorsanız):
# echo \
#   "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
#   $(lsb_release -cs) stable" | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Docker kur
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin

# Docker servisini başlat
sudo systemctl start docker
sudo systemctl enable docker

# Kullanıcıyı docker grubuna ekle
sudo usermod -aG docker $USER
newgrp docker

# Docker kurulumunu test et
docker --version
docker compose version
```

**Beklenen Süre:** 5-10 dakika

**Kontrol:**
- [ ] `docker --version` komutu çalışıyor
- [ ] `docker compose version` komutu çalışıyor
- [ ] `docker ps` komutu hata vermiyor

#### Adım 1.4: Firewall Yapılandırması

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

**Kontrol:**
- [ ] Firewall aktif
- [ ] Gerekli portlar açık

---

### **Faz 2: GitLab Kurulumu** (45-60 dakika)

#### Adım 2.1: GitLab Klasör Yapısı

```bash
# GitLab için klasör oluştur
mkdir -p ~/gitlab
cd ~/gitlab
```

#### Adım 2.2: Docker Compose Dosyası Oluşturma

```bash
# Docker Compose dosyası oluştur
nano docker-compose.yml
```

**Önemli:** Aşağıdaki içeriği yapıştırın ve **domain/IP bilgilerinizi güncelleyin:**

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
      POSTGRES_PASSWORD: gitlab123  # ⚠️ Güçlü şifre kullanın!
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
    hostname: YOUR_DOMAIN_OR_IP  # ⚠️ Domain veya IP yazın
    environment:
      GITLAB_OMNIBUS_CONFIG: |
        # ⚠️ Domain kullanıyorsanız HTTPS, IP kullanıyorsanız HTTP
        external_url 'http://YOUR_DOMAIN_OR_IP'
        # Domain için: external_url 'https://gitlab.yourdomain.com'
        
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
        pages_external_url 'http://YOUR_DOMAIN_OR_IP'
        gitlab_pages['external_http'] = ['0.0.0.0:8090']
        pages_nginx['enable'] = true
    ports:
      - "80:80"
      - "443:443"
      - "2222:22"
      - "8090:8090"
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

**Yapılacaklar:**
1. `YOUR_DOMAIN_OR_IP` yerine domain veya IP yazın
2. `gitlab123` şifrelerini güçlü şifrelerle değiştirin
3. Dosyayı kaydedin (`Ctrl+O`, `Enter`, `Ctrl+X`)

**Kontrol:**
- [ ] `docker-compose.yml` dosyası oluşturuldu
- [ ] Domain/IP bilgileri güncellendi
- [ ] Şifreler değiştirildi

#### Adım 2.3: GitLab'ı Başlatma

```bash
cd ~/gitlab

# GitLab'ı başlat
docker compose up -d

# Logları takip et (GitLab'ın hazır olmasını bekle)
docker logs -f gitlab
```

**Beklenen Süre:** 5-10 dakika

**Beklenen Çıktı:**
```
gitlab Reconfigured!
```

`Ctrl+C` ile log takibinden çıkın.

**Kontrol:**
- [ ] GitLab container'ı çalışıyor (`docker ps`)
- [ ] "Reconfigured!" mesajı görüldü

#### Adım 2.4: GitLab Durumunu Kontrol Etme

```bash
# Container'ların çalıştığını kontrol et
docker ps

# GitLab health check
docker exec gitlab gitlab-ctl status

# GitLab'ın hazır olduğunu kontrol et
curl http://localhost
# veya domain kullanıyorsanız:
curl http://YOUR_DOMAIN_OR_IP
```

**Kontrol:**
- [ ] Tüm container'lar çalışıyor
- [ ] GitLab health check başarılı
- [ ] HTTP isteği başarılı (200 OK)

---

### **Faz 3: GitLab İlk Kurulum** (15-20 dakika)

#### Adım 3.1: GitLab Web Arayüzüne Erişim

1. **Tarayıcıda GitLab'ı açın:**
   - IP kullanıyorsanız: `http://YOUR_SERVER_IP`
   - Domain kullanıyorsanız: `http://gitlab.yourdomain.com`

2. **Root şifresini belirleyin:**
   - İlk açılışta root şifresi belirleme ekranı çıkacak
   - Güçlü bir şifre belirleyin (en az 8 karakter)
   - Şifreyi güvenli bir yerde saklayın!

**Kontrol:**
- [ ] GitLab web arayüzü açıldı
- [ ] Root şifresi belirlendi

#### Adım 3.2: MonitraNG Projesini Oluşturma

1. GitLab'da giriş yaptıktan sonra:
   - **"New project"** veya **"Create a project"** butonuna tıklayın
   - **"Create blank project"** seçeneğini seçin
   - **Project name:** `MonitraNG`
   - **Project slug:** `MonitraNG` (otomatik)
   - **Visibility Level:** Private (veya istediğiniz seviye)
   - **"Create project"** butonuna tıklayın

2. **Repository URL'ini not edin:**
   - Örnek: `http://YOUR_SERVER_IP/root/MonitraNG.git`
   - veya: `https://gitlab.yourdomain.com/root/MonitraNG.git`

**Kontrol:**
- [ ] MonitraNG projesi oluşturuldu
- [ ] Repository URL'i not edildi

---

### **Faz 4: Repository Taşıma** (20-30 dakika)

#### Adım 4.1: Local Makinede GitLab Remote Ekleme

**Windows PowerShell'de (lokal makinenizde):**

```powershell
cd C:\Serkan\iSIM\MonitraNG

# Mevcut remote'ları kontrol et
git remote -v

# GitLab remote ekle
git remote add gitlab http://YOUR_SERVER_IP/root/MonitraNG.git
# veya domain için:
git remote add gitlab https://gitlab.yourdomain.com/root/MonitraNG.git

# Remote'ları kontrol et
git remote -v
```

**Kontrol:**
- [ ] GitLab remote eklendi
- [ ] Remote URL doğru

#### Adım 4.2: Personal Access Token Oluşturma (Gerekirse)

Eğer push sırasında authentication hatası alırsanız:

1. GitLab'da: **User Settings > Access Tokens**
2. **Token name:** `monitrang-local`
3. **Scopes:** `write_repository`, `read_repository` işaretleyin
4. **Create personal access token** butonuna tıklayın
5. Token'ı kopyalayın (bir daha gösterilmeyecek!)

#### Adım 4.3: Repository'yi GitLab'a Push Etme

**Windows PowerShell'de:**

```powershell
# Tüm branch'leri push et
git push gitlab main --all

# Tag'leri push et
git push gitlab main --tags
```

**Token kullanıyorsanız:**
```powershell
git push https://oauth2:YOUR_TOKEN@YOUR_SERVER_IP/root/MonitraNG.git main
```

**Beklenen Süre:** 5-15 dakika (repository boyutuna göre)

**Kontrol:**
- [ ] Push başarılı
- [ ] GitLab'da dosyalar görünüyor

---

### **Faz 5: GitLab Runner Yapılandırması** (15-20 dakika)

#### Adım 5.1: Runner Token Alma

1. GitLab'da projeye gidin: `MonitraNG`
2. **Settings > CI/CD** sekmesine gidin
3. **Runners** bölümünü genişletin
4. **"Set up a specific runner manually"** bölümünde
5. **Registration token**'ı kopyalayın

**Kontrol:**
- [ ] Runner token kopyalandı

#### Adım 5.2: Runner'ı Kaydetme

**Sunucuda (SSH ile bağlı):**

```bash
# Runner'ı kaydet
docker exec -it gitlab-runner gitlab-runner register
```

**Sorular ve Cevaplar:**

```
Enter the GitLab instance URL (for example, https://gitlab.com/):
> http://YOUR_SERVER_IP
# veya domain için: https://gitlab.yourdomain.com

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

**Kontrol:**
- [ ] Runner kaydı başarılı
- [ ] "Runner registered successfully" mesajı görüldü

#### Adım 5.3: Runner Durumunu Kontrol Etme

```bash
# Runner'ın çalıştığını kontrol et
docker exec gitlab-runner gitlab-runner verify

# Runner listesi
docker exec gitlab-runner gitlab-runner list
```

**GitLab Web UI'da:**
1. **Settings > CI/CD > Runners** sekmesine gidin
2. **"Available specific runners"** bölümünde runner'ınızı görmelisiniz
3. Status: **"Online"** ve **"Active"** olmalı

**Kontrol:**
- [ ] Runner verify başarılı
- [ ] GitLab UI'da runner görünüyor ve online

---

### **Faz 6: CI/CD Pipeline Testi** (10-15 dakika)

#### Adım 6.1: Pipeline'ı Tetikleme

**Yöntem 1: Yeni Commit**

```powershell
# Windows PowerShell'de (lokal makinenizde)
cd C:\Serkan\iSIM\MonitraNG

# Test commit
git commit --allow-empty -m "test: CI/CD pipeline testi"
git push gitlab main
```

**Yöntem 2: GitLab UI'dan**

1. GitLab'da projeye gidin
2. **CI/CD > Pipelines** sekmesine gidin
3. **"Run pipeline"** butonuna tıklayın

#### Adım 6.2: Pipeline Sonuçlarını Kontrol Etme

1. GitLab'da: **CI/CD > Pipelines**
2. Pipeline'ın çalıştığını kontrol edin
3. Her job'un durumunu kontrol edin

**Beklenen Sonuç:**
- ✅ `test-setup` - Başarılı
- ✅ `build-mngkeeper` - Başarılı
- ✅ `build-mngdatagateway` - Başarılı
- ✅ `build-mnghub` - Başarılı
- ✅ `build-frontend` - Başarılı
- ✅ `deploy-docs` - Başarılı (main branch için)

**Kontrol:**
- [ ] Pipeline başarıyla çalıştı
- [ ] Tüm job'lar başarılı (veya beklenen hatalar var)

---

### **Faz 7: SSL Sertifikası (Domain Kullanıyorsanız)** (30-45 dakika)

#### Adım 7.1: Nginx Reverse Proxy Kurulumu

```bash
# Nginx kur
sudo apt install -y nginx

# Nginx config oluştur
sudo nano /etc/nginx/sites-available/gitlab
```

**İçerik:**

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

#### Adım 7.2: Let's Encrypt SSL Sertifikası

```bash
# Certbot kur
sudo apt install -y certbot python3-certbot-nginx

# SSL sertifikası al
sudo certbot --nginx -d gitlab.yourdomain.com

# Otomatik yenileme test
sudo certbot renew --dry-run
```

**Kontrol:**
- [ ] SSL sertifikası kuruldu
- [ ] HTTPS erişimi çalışıyor

---

## ✅ Doğrulama ve Test

### Genel Kontrol Listesi

- [ ] GitLab web arayüzü erişilebilir
- [ ] MonitraNG projesi oluşturuldu
- [ ] Repository push edildi
- [ ] GitLab Runner kaydedildi ve online
- [ ] CI/CD pipeline başarıyla çalıştı
- [ ] SSL sertifikası kuruldu (domain kullanıyorsanız)
- [ ] Firewall yapılandırıldı

### Pipeline Test Senaryosu

1. **Test Commit:**
   ```powershell
   git commit --allow-empty -m "test: pipeline test"
   git push gitlab main
   ```

2. **Pipeline İzleme:**
   - GitLab'da: **CI/CD > Pipelines**
   - Pipeline'ın çalıştığını kontrol edin
   - Job loglarını inceleyin

3. **Sonuç Kontrolü:**
   - Tüm job'lar başarılı olmalı
   - Artifacts oluşturulmalı
   - Dokümantasyon deploy edilmeli (main branch için)

---

## 🆘 Sorun Giderme

### GitLab Başlamıyor

```bash
# Container durumunu kontrol et
docker ps -a

# Logları incele
docker logs gitlab

# Container'ı yeniden başlat
cd ~/gitlab
docker compose restart gitlab
```

### Runner Bağlanamıyor

```bash
# Runner'ı yeniden kaydet
docker exec -it gitlab-runner gitlab-runner register

# Runner'ı restart et
docker compose restart gitlab-runner

# Runner logları
docker logs gitlab-runner
```

### Pipeline Çalışmıyor

1. **GitLab'da kontrol edin:**
   - Settings > CI/CD > Runners
   - Runner'ın online olduğunu kontrol edin

2. **Runner loglarını inceleyin:**
   ```bash
   docker logs gitlab-runner
   ```

3. **Pipeline loglarını inceleyin:**
   - GitLab UI'da: CI/CD > Pipelines > Job logları

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

## 📝 Özet Checklist

### Faz 1: Sunucu Hazırlığı
- [ ] SSH bağlantısı test edildi
- [ ] Sistem güncellemesi yapıldı
- [ ] Docker kuruldu ve test edildi
- [ ] Firewall yapılandırıldı

### Faz 2: GitLab Kurulumu
- [ ] GitLab klasör yapısı oluşturuldu
- [ ] Docker Compose dosyası oluşturuldu ve yapılandırıldı
- [ ] GitLab başlatıldı ve hazır oldu
- [ ] GitLab durumu kontrol edildi

### Faz 3: GitLab İlk Kurulum
- [ ] Root şifresi belirlendi
- [ ] MonitraNG projesi oluşturuldu
- [ ] Repository URL'i not edildi

### Faz 4: Repository Taşıma
- [ ] GitLab remote eklendi
- [ ] Personal Access Token oluşturuldu (gerekirse)
- [ ] Repository push edildi

### Faz 5: GitLab Runner
- [ ] Runner token alındı
- [ ] Runner kaydedildi
- [ ] Runner durumu kontrol edildi (online)

### Faz 6: CI/CD Pipeline
- [ ] Pipeline test edildi
- [ ] Tüm job'lar başarılı

### Faz 7: SSL (Opsiyonel)
- [ ] Nginx kuruldu
- [ ] SSL sertifikası kuruldu
- [ ] HTTPS erişimi test edildi

---

## 🎯 Sonraki Adımlar

1. ✅ GitLab CI/CD kurulumu tamamlandı
2. ⏳ Otomatik deployment workflow kurulumu (opsiyonel)
3. ⏳ Backup stratejisi oluşturma
4. ⏳ Monitoring ve alerting kurulumu (opsiyonel)

---

## 📚 İlgili Dokümantasyon

- [GitLab Debian Kurulum Rehberi](./GITLAB_DEBIAN_INSTALLATION.md) - Detaylı kurulum rehberi
- [Otomatik Deployment Workflow](./AUTOMATED_DEPLOYMENT_WORKFLOW.md) - Deployment yapılandırması
- [GitLab CI/CD Rehberi](./GITLAB_CI_CD_GUIDE.md) - Pipeline yapılandırması
- [Hosting Kaynak Gereksinimleri](../HOSTING_RESOURCE_REQUIREMENTS.md) - Sistem gereksinimleri

---

**Son Güncelleme:** 15 Ocak 2025  
**Hazırlayan:** AI Assistant  
**Durum:** Başlangıç rehberi - Adım adım takip edilecek

