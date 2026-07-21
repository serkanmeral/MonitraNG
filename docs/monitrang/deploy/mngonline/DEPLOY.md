# monitrang.com — Günlük deploy

**Strateji:** [DEPLOY_STRATEGY.md](./DEPLOY_STRATEGY.md)  
**Erişim:** [ACCESS.md](./ACCESS.md)  
**Script kökü:** `scripts/mngonline/` (repo kökünden çalıştırın)

---

## 1. Ön koşullar (bir kez)

- `ssh root@monitrang-server` başarılı (SSH public key)
- Sunucuda `/root/MonitraNG/ApplicationResources/mng_apps/.env` mevcut
- Docker network: `mng_common_mng_network`
- Host nginx 80/443 aktif (Docker `nginx` container kapalı olabilir — normal)

Windows’ta `tar` (bsdtar) genelde hazırdır. Script’ler **OpenSSH** `ssh` / `scp` kullanır (Posh-SSH gerekmez).

---

## 2. Standart akış

```
① sync-mngonline-source.ps1  →  seçili path’leri paketle, yükle, extract
② deploy-mngonline-apps.ps1  →  compose build + up -d
```

```powershell
cd C:\Serkan\iSIM\MonitraNG
```

---

## 3. Sık senaryolar

### 3.1 Yalnızca UI (`mngui`)

```powershell
.\scripts\mngonline\sync-mngonline-source.ps1 -Paths Mng.Ui,ApplicationResources/mng_apps
.\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngui
```

Compose: `docker-compose.production.yml` + `docker-compose.mngonline.yml` (host nginx için 3000/5000 publish).

Doğrulama: https://app.monitrang.com/ (Ctrl+F5)

### 3.2 Keeper + Gateway

```powershell
.\scripts\mngonline\sync-mngonline-source.ps1 -Paths MngKeeper,MngGateway,ApplicationResources/mng_apps
.\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngkeeper,mnggateway
```

### 3.3 Compose dosyası / env şablonu değişti

Compose değiştiyse `ApplicationResources/mng_apps` sync’e dahil edin. **Sunucu `.env` ezilmez.**

```powershell
.\scripts\mngonline\sync-mngonline-source.ps1 -Paths ApplicationResources/mng_apps
.\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngui
```

### 3.4 Zaten `main`’e push edildi (`-FromGit`)

Sync yok; sunucu `origin/main`’e çekilir (reset --hard).

```powershell
.\scripts\mngonline\deploy-mngonline-apps.ps1 -FromGit -Services mngnotifier
```

### 3.5 Tam apps sync + seçili rebuild

```powershell
.\scripts\mngonline\sync-mngonline-source.ps1 -Full
.\scripts\mngonline\deploy-mngonline-apps.ps1 -Services mngkeeper,mngdatagateway,mnggateway,mngui -Backup
```

---

## 4. Script parametreleri

### sync-mngonline-source.ps1

| Parametre | Anlam |
|-----------|--------|
| `-Paths A,B` | Sync edilecek relative path’ler |
| `-Full` | Varsayılan uygulama path seti |
| `-Server` | Varsayılan `monitrang-server` |

### deploy-mngonline-apps.ps1

| Parametre | Anlam |
|-----------|--------|
| `-Services a,b` | Compose servis adları (zorunlu önerilir) |
| `-NoBuild` | Sadece `up -d` |
| `-NoCache` | `docker compose build --no-cache` |
| `-FromGit` | Sync yerine sunucuda `git fetch` + `reset --hard origin/main` |
| `-Backup` | Önce `scripts/backup-pre-deploy.sh` (sunucuda varsa) |
| `-DryRun` | Uzak komutu yazdır, çalıştırma |

---

## 5. Deploy sonrası kontrol

```powershell
ssh root@monitrang-server "docker ps --format 'table {{.Names}}\t{{.Status}}' | head -30"
```

| URL | Beklenen |
|-----|----------|
| https://app.monitrang.com/ | 200 |
| https://api.monitrang.com/health (veya gateway health path) | 200 / sağlıklı yanıt |
| https://auth.monitrang.com/ | Keycloak |

---

## 6. Landing (www) — ayrı hat

Statik landing CI/apps sync’inden bağımsızdır:

```powershell
.\scripts\deployment\deploy-www-landing.ps1
```

---

## 7. Yapılmaması gerekenler

- Günlük deploy için GitLab `deploy-services` Play’e güvenmek (legacy)
- Bilinçsiz tam stack `-Services` vermeden production rebuild
- Sync ile sunucu `.env`’i local’den üzerine yazmaya çalışmak (script kasıtlı olarak `.env` taşımaz)
- Odak script’lerini (`scripts/odak/*`) monitrang.com IP’sine çevirerek kullanmak — ayrı script seti kullanın
