# Odak deploy — Windows geliştirme PC

**Sunucu:** `192.168.20.20` (`odak` kullanıcısı) — **yalnızca test**; production (`192.168.20.8`) ayrı yığın → [../proddeploy/INDEPENDENCE.md](../proddeploy/INDEPENDENCE.md)  
**Strateji:** Lokal repo → tar/scp senkron → sunucuda `docker compose build` + `up` (git push **gerekmez**)

Bu klasör, günlük deploy için **doğrulanmış komutları** içerir. Genel mimari ve ilk kurulum: [../setup/MNG_APPS_ODAK_DEPLOY.md](../setup/MNG_APPS_ODAK_DEPLOY.md).

---

## 1. Ön koşullar (bir kez)

### 1.1 PowerShell 7 (`pwsh`) — zorunlu

Deploy scriptleri **Posh-SSH** modülünü kullanır. Bu makinede modül **PowerShell 7** altında yüklüdür; klasik **Windows PowerShell 5.1** (`powershell.exe`) ile çalıştırmayın — `Import-Module Posh-SSH` hatası alırsınız.

```powershell
# Doğru
pwsh -NoProfile -Command "Get-Module -ListAvailable Posh-SSH"

# Yanlış (modül yoksa fail)
powershell -NoProfile -Command "Get-Module -ListAvailable Posh-SSH"
```

Posh-SSH yoksa (yalnızca `pwsh` içinde):

```powershell
pwsh -NoProfile -Command "Install-PackageProvider -Name NuGet -MinimumVersion 2.8.5.201 -Force -Scope CurrentUser; Set-PSRepository -Name PSGallery -InstallationPolicy Trusted; Install-Module -Name Posh-SSH -Force -Scope CurrentUser -AllowClobber -SkipPublisherCheck"
```

### 1.2 SSH kimlik bilgisi — zorunlu (otomasyon için)

Scriptler parolayı şu sırayla arar (`scripts/odak/OdakSshCommon.ps1`):

| Kaynak | Dosya / değişken | Git’e girer mi? |
|--------|------------------|-----------------|
| 1 | `$env:ODAK_SSH_PASSWORD` | Hayır |
| 2 | Repo kökü **`.env.odak.local`** → `ODAK_SSH_PASSWORD=...` | Hayır (gitignore) |
| 3 | `scripts/odak/local-credentials.ps1` | Hayır (gitignore) |
| 4 | İnteraktif `Read-Host` | Agent/CI için uygun değil |

**Önerilen kurulum (bir kez):**

```powershell
# Repo kökünden
Copy-Item scripts\odak\local-credentials.ps1.example scripts\odak\local-credentials.ps1
# local-credentials.ps1 içinde parolayı doldurun

# veya repo kökünde .env.odak.local oluşturun:
# ODAK_SSH_PASSWORD=...
```

Parolayı dokümana yazmayın.

### 1.3 Sunucu tarafı

- `/home/odak/mng_common` ayakta, `mng_common_mng_network` mevcut
- `/home/odak/MonitraNG/ApplicationResources/mng_apps/.env` oluşturulmuş (ilk kurulum)
- İlk deploy öncesi en az bir kez tam senkron: `sync-odak-source.ps1 -Full`

---

## 2. Standart akış (tüm servisler için)

```
① sync-odak-source.ps1   →  kaynak paketle + SCP + sunucuda aç
② deploy-odak-apps.ps1   →  uzaktan docker compose build + up -d
```

Her iki adım da **repo kökünden** ve **`pwsh` ile** çalıştırılır.

---

## 3. UI deploy (`mngui`) — en sık senaryo

**Ne zaman:** Yalnızca `Mng.Ui` değişti (Nuxt/nginx, sayfa, composable, locale, …).

**Süre (tipik):** Senkron ~5 sn · build+up ~2–3 dk (sunucu cache’ine bağlı).

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG

# 1) Kaynak senkronu (yalnızca UI)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui

# 2) Build + konteyner yenileme
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngui
```

**Doğrulama** (script sonunda otomatik curl; elle de):

| Kontrol | Beklenen |
|---------|----------|
| http://192.168.20.20:3000/ | HTTP 200 |
| http://192.168.20.20:5040/health | HTTP 200 |
| Tarayıcı | Ctrl+F5 (önbellek temizliği) |

**Not:** `mngui` image’ı build-arg `GATEWAY_URL` içerir; UI deploy **her zaman rebuild** gerektirir (`-NoBuild` kullanmayın).

---

## 4. Backend deploy (tek servis)

Servis adları (`-Services` parametresi, virgülle ayrılabilir):

| Klasör | Compose servis adı | Host portu |
|--------|-------------------|------------|
| MngOperations | `mngoperations` | 5086 |
| MngWorkflow API | `mngworkflow` | 5085 |
| MngWorkflow Worker | `mngworkflow-worker` | (internal) |
| MngKeeper | `mngkeeper` | 5001 |
| MngDataGateway | `mngdatagateway` | 5010 |
| MngGateway | `mnggateway` | 5040 |
| Mng.Ui | `mngui` | 3000 |
| MngDomainUI | `mngdomainui` | 3001 |
| … | (tam liste: [MNG_APPS_ODAK.md](../setup/MNG_APPS_ODAK.md)) | |

**Örnek — MngWorkflow (API + Worker):**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths MngWorkflow,ApplicationResources/mng_apps
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngworkflow,mngworkflow-worker
```

Smoke test (dev endpoint):

```powershell
Invoke-RestMethod -Method Post -Uri "http://192.168.20.20:5040/workflow/api/v1/dev/runs/smoke" -ContentType "application/json" -Body '{"domainName":"odak","eventValue":10}'
```

**Örnek — MngOperations:**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths MngOperations,ApplicationResources/mng_apps
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations
```

**Kritik backend fix** (cache eski binary üretebiliyorsa):

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations -NoCache
```

**UI + backend birlikte:**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui,MngOperations
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngui,mngoperations
```

---

## 5. Tam / ilk deploy

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Full
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1
```

Uzun sürer (tüm servisler build). İlk kurulum checklist: [MNG_APPS_ODAK_DEPLOY.md § İlk kurulum](../setup/MNG_APPS_ODAK_DEPLOY.md).

---

## 6. Script parametreleri (özet)

### `sync-odak-source.ps1`

| Parametre | Açıklama |
|-----------|----------|
| `-Paths Mng.Ui,MngKeeper,...` | Yalnızca belirtilen klasörler (önerilen günlük döngü) |
| `-Full` | Varsayılan tam liste (tüm servisler + mng_apps) |
| `-IncludeMngCommon` | Altyapı compose dosyalarını da senkron et |

`node_modules`, `.nuxt`, `bin/obj` tar’a **alınmaz** (sunucuda build sırasında üretilir).

### `deploy-odak-apps.ps1`

| Parametre | Açıklama |
|-----------|----------|
| `-Services mngui,mngkeeper,...` | Yalnızca seçili servisler |
| `-NoBuild` | Build atla (nadiren; image zaten güncelse) |
| `-NoCache` | Docker build cache’siz (kritik backend fix sonrası) |

---

## 7. Sorun giderme

| Belirti | Olası neden | Çözüm |
|---------|-------------|--------|
| `The specified module 'Posh-SSH' was not loaded` | `powershell.exe` (5.1) kullanıldı | **`pwsh`** kullanın |
| `Posh-SSH` kurulumu takılıyor | NuGet / admin scope | `pwsh` + `-Scope CurrentUser`; bkz. §1.1 |
| SSH `Permission denied (publickey,password)` | Parola yok, key yok | `.env.odak.local` veya `local-credentials.ps1` |
| `Missing apps dir. Run sync-odak-source.ps1 first` | Sunucuda repo yok | `-Full` sync |
| `Start mng_common first` | Altyapı kapalı | Sunucuda `mng_common` compose up |
| UI eski görünüyor | Tarayıcı cache | Ctrl+F5; `mngui` rebuild yapıldığından emin olun |
| Build çok hızlı (~36 sn) ama fix yok | Docker layer cache | `-NoCache` ile tekrar build |

**Agent / Cursor otomasyonu:** Komutları her zaman `pwsh -NoProfile -ExecutionPolicy Bypass -File ...` formunda verin; repo kökünde `.env.odak.local` bulundurun.

**`-Paths` dizisi:** `-File` ile virgüllü liste tek string olur. Dizi geçmek için:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -Command "& '...\sync-odak-source.ps1' -Paths @('MngReactor','ApplicationResources/mng_apps')"
```

---

## 8. İlgili dokümanlar

| Doküman | İçerik |
|---------|--------|
| [../setup/MNG_APPS_ODAK_DEPLOY.md](../setup/MNG_APPS_ODAK_DEPLOY.md) | Deploy stratejisi, modlar, git ilişkisi |
| [../setup/MNG_APPS_ODAK.md](../setup/MNG_APPS_ODAK.md) | Servis listesi, portlar, Keycloak |
| [../ODAK_FULL_SETUP.md](../ODAK_FULL_SETUP.md) | Tam kurulum özeti |
| [../../../scripts/odak/sync-odak-source.ps1](../../../scripts/odak/sync-odak-source.ps1) | Senkron script |
| [../../../scripts/odak/deploy-odak-apps.ps1](../../../scripts/odak/deploy-odak-apps.ps1) | Deploy script |
| [../../../scripts/odak/OdakSshCommon.ps1](../../../scripts/odak/OdakSshCommon.ps1) | SSH kimlik bilgisi mantığı |

---

**Son doğrulama:** 2 Haziran 2026 — Faz 1+1B UI performans paketi `mngui` deploy (pwsh + `-Paths Mng.Ui` + `-Services mngui`).
