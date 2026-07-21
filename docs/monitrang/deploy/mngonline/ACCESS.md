# MonitraNG Online — Sunucu Erişim Bilgileri

**Ortam:** Production / online (`monitrang.com`)  
**Sunucu:** `monitrang-server`  
**Son bağlantı testi:** 16 Temmuz 2026 — `ssh root@monitrang-server` başarılı

> Parolalar, private key içerikleri ve uygulama secret'ları **bu dosyaya yazılmaz**. Yerel kimlik bilgileri için kişisel `~/.ssh/` yapılandırması ve sunucudaki `.env` dosyaları kullanılır.

---

## Sunucu özeti

| Alan | Değer |
|------|--------|
| Hostname (SSH alias) | `monitrang-server` |
| IP adresi | `45.141.151.52` |
| İşletim sistemi | Debian GNU/Linux 12 (bookworm) |
| Kernel | Linux 6.1.x (amd64) |
| SSH kullanıcı | `root` |
| SSH port | `22` |
| Kimlik doğrulama | SSH public key (parola gerekmez) |
| Repo yolu (sunucu) | `/root/MonitraNG` |
| Docker | Kurulu (29.x) |

---

## SSH bağlantısı

### Hızlı bağlantı

```powershell
ssh root@monitrang-server
```

```bash
ssh root@monitrang-server
```

### Bağlantı testi

```powershell
ssh root@monitrang-server "echo CONNECTION_OK && hostname && uname -a"
```

**Beklenen çıktı (özet):** `CONNECTION_OK`, hostname `debian`, Debian 12.

### Yerel SSH yapılandırması (`~/.ssh/config`)

Geliştirme makinesinde aşağıdaki blok tanımlı olmalıdır:

```
Host monitrang-server
    HostName 45.141.151.52
    User root
    IdentityFile C:\Users\<kullanici>\.ssh\id_rsa_monitrang
    IdentitiesOnly yes
    PreferredAuthentications publickey
```

Linux/macOS için `IdentityFile` yolunu kendi anahtar konumunuza göre güncelleyin.

**Not:** `monitrang-server` hostname'i yerel DNS'te çözülmeyebilir; bu normaldir. SSH alias'ı `HostName` üzerinden doğrudan IP'ye bağlanır.

### IP ile doğrudan bağlantı (yedek)

```powershell
ssh -i "$env:USERPROFILE\.ssh\id_rsa_monitrang" root@45.141.151.52
```

### GitLab Git SSH (kod push/pull)

```
Host gitlab-monitrang
    HostName 45.141.151.52
    Port 2222
    User git
    IdentityFile ~/.ssh/id_rsa
    IdentitiesOnly yes
    PreferredAuthentications publickey
```

**Remote URL örneği:** `ssh://git@gitlab.monitrang.com:2222/root/MonitraNG.git`

---

## Ağ ve portlar

### Dışarıya açık portlar

| Port | Servis | Açıklama |
|------|--------|----------|
| 22 | SSH | Sunucu yönetimi |
| 80 | Nginx | HTTP → HTTPS yönlendirme |
| 443 | Nginx | HTTPS (tüm public subdomain'ler) |
| 2222 | GitLab | Git SSH |

### Sunucu iç IP'leri (Docker bridge)

Örnek: `45.141.151.52` + Docker bridge ağları (`172.17.x`, `172.18.x`, …). Uygulama servisleri genelde Nginx reverse proxy üzerinden erişilir; doğrudan port mapping kullanılmaz.

---

## Public URL'ler

| URL | Amaç |
|-----|------|
| https://monitrang.com | Ana domain (landing) |
| https://www.monitrang.com | WWW |
| https://app.monitrang.com | MngUI (frontend) |
| https://api.monitrang.com | API Gateway |
| https://auth.monitrang.com | Keycloak |
| https://gitlab.monitrang.com | GitLab UI |
| https://docs.monitrang.com | Dokümantasyon |
| https://mail.monitrang.com | Mailu (e-posta) |
| https://admin.monitrang.com | Altyapı admin paneli (HTTP Basic Auth) |

### admin.monitrang.com alt yolları

| URL | Servis |
|-----|--------|
| https://admin.monitrang.com/ | Admin dashboard |
| https://admin.monitrang.com/portainer/ | Portainer |
| https://admin.monitrang.com/rabbitmq/ | RabbitMQ Management |
| https://admin.monitrang.com/seq/ | Seq (log) |
| https://admin.monitrang.com/mongo/ | Mongo Express |
| https://admin.monitrang.com/redis/ | Redis Commander |
| https://admin.monitrang.com/nodered/ | Node-RED |

`admin.monitrang.com` için kullanıcı adı/şifre bu dosyada tutulmaz; sunucudaki Nginx htpasswd yapılandırmasından veya ilgili operasyon notlarından alınır.

---

## Sunucu dizin yapısı

| Yol | İçerik |
|-----|--------|
| `/root/MonitraNG` | Ana repo |
| `/root/MonitraNG/ApplicationResources/mng_common` | Altyapı compose (MongoDB, Redis, Nginx, GitLab, Keycloak, …) |
| `/root/MonitraNG/ApplicationResources/mng_apps` | Uygulama compose (MngUI, Gateway, Keeper, …) |
| `/var/www/docs.monitrang.com` | MkDocs / statik dokümantasyon (deploy hedefi) |

### Git remote'lar (sunucu)

| Remote | URL |
|--------|-----|
| origin | `https://github.com/serkanmeral/MonitraNG.git` |
| gitlab | `http://45.141.151.52:8090/root/MonitraNG.git` |

---

## Çalışan servisler (örnek envanter)

Bağlandıktan sonra güncel listeyi almak için:

```bash
ssh root@monitrang-server "docker ps --format 'table {{.Names}}\t{{.Status}}'"
```

**Örnek uygulama container'ları:** `mngui`, `mnggateway`, `mngkeeper`, `mngdatagateway`, `mnghub`, `mngnotifier`, `mngadmin`, `mngscheduler`, `mngllm`, `mngdomainui`, `keycloak`

**Örnek altyapı container'ları:** `mongo`, `postgres`, `redis`, `rabbitmq`, `minio`, `gitlab`, `gitlab-runner`, `mailu-*`, `portainer`, `seq`, `mosquitto`, `ollama`

---

## Sık kullanılan komutlar

```bash
# Sunucuya bağlan
ssh root@monitrang-server

# Repo dizinine git
cd /root/MonitraNG

# Uygulama stack
cd /root/MonitraNG/ApplicationResources/mng_apps
docker compose ps

# Altyapı stack
cd /root/MonitraNG/ApplicationResources/mng_common
docker compose ps

# Nginx yapılandırma testi (nginx container çalışıyorsa)
docker exec nginx nginx -t
```

---

## DNS

Tüm `*.monitrang.com` kayıtları A record olarak **`45.141.151.52`** adresine yönlenir.

---

## İlişkili dokümanlar

| Dosya | İçerik |
|-------|--------|
| [README.md](./README.md) | Online deploy giriş noktası |
| [DEPLOY_STRATEGY.md](./DEPLOY_STRATEGY.md) | PC-driven sync modeli (eski GitLab Runner deploy → legacy) |
| [DEPLOY.md](./DEPLOY.md) | Günlük sync + compose komutları |
| [../local/README.md](../local/README.md) | Lokal Docker Desktop ortamı |
| [../../localdocker/PORTS.md](../../localdocker/PORTS.md) | Lokal port özeti (online ile karıştırılmamalı) |
| [../../../content/infrastructure/port-management-completion-report.md](../../../content/infrastructure/port-management-completion-report.md) | Nginx / port mimarisi |
| [../../../content/infrastructure/admin-subdomain-setup.md](../../../content/infrastructure/admin-subdomain-setup.md) | admin.monitrang.com kurulumu |
| [../../../content/infrastructure/gitlab-ssh-key-setup.md](../../../content/infrastructure/gitlab-ssh-key-setup.md) | CI/CD SSH key yapılandırması (legacy deploy hattı) |

---

## Sorun giderme

| Belirti | Olası neden | Çözüm |
|---------|-------------|--------|
| `Permission denied (publickey)` | SSH key eksik veya authorized_keys'te yok | Doğru `IdentityFile` kullanın; public key'i sunucuya ekleyin |
| `monitrang-server` çözülmüyor | Yerel DNS kaydı yok | `~/.ssh/config` içinde `HostName 45.141.151.52` tanımlı olduğundan emin olun |
| Bağlantı zaman aşımı | Firewall / ağ | Port 22 erişimini kontrol edin: `Test-NetConnection 45.141.151.52 -Port 22` |
| GitLab SSH hata | Yanlış port | Port `2222`, kullanıcı `git` |
