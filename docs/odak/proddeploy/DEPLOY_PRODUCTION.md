# Production deploy — komut rehberi

**Hedef sunucu:** `192.168.20.8` (`odak@192.168.20.8`)  
**Bağımsızlık:** Production, test sunucudan **ayrı** çalışan tam yığın (kendi Mongo, Keycloak, Redis, …) — [INDEPENDENCE.md](./INDEPENDENCE.md)  
**Strateji:** Lokal repo → tar/scp → **production** sunucuda `docker compose` + `docker-compose.odak.prod.yml`  
**Ön koşul:** Production’da **kendi** `mng_common` ayakta — [INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md)

Test sunucu komutları (referans): [../deploy/README.md](../deploy/README.md).

---

## Hızlı komutlar (wrapper scriptler)

| İşlem | Komut |
|--------|--------|
| Senkron | `pwsh -File .\scripts\odak\sync-odak-prod.ps1 -Paths Mng.Ui` |
| Deploy | `pwsh -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngui` |
| İlk `.env` | `pwsh -File .\scripts\odak\bootstrap-odak-prod.ps1` |
| mng_common sync | `pwsh -File .\scripts\odak\sync-mng-common-prod.ps1` |
| mng_common up | `pwsh -File .\scripts\odak\setup-mng-common-odak-prod.ps1` |

Sunucu ön koşulları: [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md).

---

## 0. Her production deploy öncesi (agent / otomasyon)

1. **Yanlış sunucuya deploy etmeyin** — komutlarda `-Server 192.168.20.8` zorunlu.
2. SSH parolasını yükle (test `.env.odak.local` **kullanılmaz**):

```powershell
# Repo kökünde .env.odak.prod.local varsa:
Get-Content .env.odak.prod.local | ForEach-Object {
  if ($_ -match '^\s*ODAK_PROD_SSH_PASSWORD\s*=\s*(.+)\s*$') {
    $env:ODAK_SSH_PASSWORD = $matches[1].Trim().Trim('"').Trim("'")
  }
}
```

3. `pwsh` (PowerShell 7) kullanın — Posh-SSH test deploy ile aynı.

---

## 1. Standart akış

```
① sync-odak-source.ps1 -Server 192.168.20.8
② deploy-odak-apps.ps1   -Server 192.168.20.8
```

**Repo kökünden örnek (tam deploy):**

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG

# SSH parolası (prod)
# ... §0 ...

pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Full -Server 192.168.20.8
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Server 192.168.20.8
```

---

## 2. UI deploy (`mngui`)

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths Mng.Ui -Server 192.168.20.8
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngui -Server 192.168.20.8
```

**Doğrulama:**

| Kontrol | Beklenen |
|---------|----------|
| http://192.168.20.8:3000/ | HTTP 200 |
| http://192.168.20.8:5040/health | HTTP 200 |
| Tarayıcı | Ctrl+F5 |

`mngui` image’ında `GATEWAY_URL` build-arg vardır; production sunucudaki `.env` içinde `GATEWAY_URL=http://192.168.20.8:5040` olmalıdır.

---

## 3. Backend — tek veya çoklu servis

**MngOperations:**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths MngOperations,ApplicationResources/mng_apps -Server 192.168.20.8
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations -Server 192.168.20.8
```

**MngWorkflow (API + worker):**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -Paths MngWorkflow,ApplicationResources/mng_apps -Server 192.168.20.8
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngworkflow,mngworkflow-worker -Server 192.168.20.8
```

**Kritik backend fix (cache şüphesi):**

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-apps.ps1 -Services mngoperations -NoCache -Server 192.168.20.8
```

Servis adları ve portlar: [../setup/MNG_APPS_ODAK.md](../setup/MNG_APPS_ODAK.md).

---

## 4. Altyapı değişikliği (`mng_common`)

Compose veya `mng_common` env değiştiyse:

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-source.ps1 -IncludeMngCommon -Server 192.168.20.8
```

Ardından SSH ile production sunucuda:

```bash
cd /home/odak/mng_common
docker compose -f docker-compose.yml -f docker-compose.odak.yml --env-file .env up -d
```

`ODAK_KEYCLOAK_HOSTNAME` production IP (`192.168.20.8`) olmalıdır.

---

## 5. Script parametreleri (production)

| Script | Production için ek/zorunlu |
|--------|----------------------------|
| `sync-odak-source.ps1` | `-Server 192.168.20.8` |
| `deploy-odak-apps.ps1` | `-Server 192.168.20.8` |
| `-Paths`, `-Full`, `-Services`, `-NoCache` | Test ile aynı anlam |

`RemoteMonitraRoot` ve `RemoteAppsDir` varsayılan olarak `/home/odak/MonitraNG` — test ile aynı yol.

---

## 6. Sorun giderme

| Belirti | Çözüm |
|---------|--------|
| Test sunucu güncellendi, prod eski | Prod komutlarında `-Server 192.168.20.8` eksik olabilir |
| UI login çalışmıyor | Prod `.env`: `GATEWAY_URL`, `CORS_ALLOWED_ORIGIN_1`, `MNG_KEEPER_UI_BASE_URL` → `192.168.20.8` |
| Keycloak redirect hatalı | `mng_common` `.env`: `ODAK_KEYCLOAK_HOSTNAME=192.168.20.8` |
| `Permission denied` SSH | `.env.odak.prod.local` / prod parola |
| `Start mng_common first` | Production’da altyapı compose up değil — INITIAL_SETUP |
| Yanlışlıkla test deploy | Varsayılan script IP `20.20`; prod’da **her zaman** `-Server 192.168.20.8` |

---

## 7. Deploy sonrası smoke (isteğe bağlı)

```powershell
Invoke-WebRequest -Uri "http://192.168.20.8:5040/health" -UseBasicParsing
Invoke-WebRequest -Uri "http://192.168.20.8:3000/" -UseBasicParsing
```

Workflow dev smoke (yalnızca endpoint production’da açıksa):

```powershell
Invoke-RestMethod -Method Post -Uri "http://192.168.20.8:5040/workflow/api/v1/dev/runs/smoke" -ContentType "application/json" -Body '{"domainName":"odak","eventValue":10}'
```

---

## 8. Test vs production — bilinçli ayrım

| İşlem | Komut hedefi |
|--------|----------------|
| Günlük geliştirme deploy | **Varsayılan** script → `192.168.20.20` — [../deploy/README.md](../deploy/README.md) |
| Müşteri production deploy | `-Server 192.168.20.8` + bu dosya |

Kullanıcı “deploy et” dediğinde **production varsayılmaz**. “Production deploy” veya bu klasöre atıf varsa bu rehber uygulanır.
