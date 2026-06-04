# Agent talimatı — Production deploy

Bu dosya, Cursor veya başka bir coding agent’ın **“production deploy”** isteğini test deploy’dan ayırması içindir.

**Zorunlu ilke:** Test ve production **tamamen bağımsız** — production kendi `mng_common` (Mongo, Keycloak, Redis, …) ve `mng_apps` yığınını **yalnızca `192.168.20.8` üzerinde** çalıştırır. Test sunucuya (`20.20`) bağlanan URL, secret veya veri kopyası kullanma. Bkz. [INDEPENDENCE.md](./INDEPENDENCE.md).

---

## Tetikleyici ifadeler

Aşağıdakilerden biri geçiyorsa **production** akışı uygula (test varsayılanı **değil**):

- “production deploy”
- “prod’a deploy”
- “192.168.20.8’e deploy”
- `docs/odak/proddeploy` referansı

Yalnızca “deploy et”, “sunucuya at”, “mngui deploy” → **test** sunucu: [../deploy/README.md](../deploy/README.md) (`192.168.20.20`, script varsayılanı).

---

## Zorunlu sabitler

| Değişken | Değer |
|----------|--------|
| Production IP | `192.168.20.8` |
| SSH kullanıcı | `odak` |
| Script `-Server` | `192.168.20.8` (her sync ve deploy çağrısında) |

---

## SSH kimlik bilgisi

1. Repo kökünde `.env.odak.prod.local` var mı kontrol et (gitignore).
2. Yoksa: kullanıcıdan production SSH parolasını iste veya [SERVER_ACCESS.md](./SERVER_ACCESS.md) — **repoya parola yazma**.
3. Deploy öncesi:

```powershell
# .env.odak.prod.local okuma
$prodEnv = Join-Path (Get-Location) ".env.odak.prod.local"
if (Test-Path $prodEnv) {
  Get-Content $prodEnv | ForEach-Object {
    if ($_ -match '^\s*ODAK_PROD_SSH_PASSWORD\s*=\s*(.+)\s*$') {
      $env:ODAK_SSH_PASSWORD = $matches[1].Trim().Trim('"').Trim("'")
    }
  }
}
```

`OdakSshCommon.ps1` yalnızca `ODAK_SSH_PASSWORD` okur; production için bu değişkeni prod dosyasından doldur.

**Test `.env.odak.local` production deploy’da kullanılmamalı** (farklı sunucu / parola).

---

## Komut şablonu (tercih: wrapper scriptler)

```powershell
cd C:\Users\monitra\Dev\MonitraNG\MonitraNG

# SSH: .env.odak.prod.local otomatik okunur (Initialize-OdakSshEnvironment)

pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\sync-odak-prod.ps1 -PathsCsv "Mng.Ui"
pwsh -NoProfile -ExecutionPolicy Bypass -File .\scripts\odak\deploy-odak-prod.ps1 -Services mngui
```

Alternatif (aynı iş): `sync-odak-source.ps1` / `deploy-odak-apps.ps1` + `-Server 192.168.20.8`  
Production compose: `docker-compose.odak.prod.yml` (otomatik seçilir).

### Sık senaryolar

| İstek | Sync `-Paths` | Deploy `-Services` |
|-------|---------------|---------------------|
| Sadece UI | `Mng.Ui` | `mngui` |
| MngOperations | `MngOperations,ApplicationResources/mng_apps` | `mngoperations` |
| Workflow | `MngWorkflow,ApplicationResources/mng_apps` | `mngworkflow,mngworkflow-worker` |
| Tam deploy | `-Full` | (servis belirtme) |

Kritik backend: `-NoCache` ekle.

---

## Doğrulama (production URL)

```
http://192.168.20.8:5040/health  → 200
http://192.168.20.8:3000/        → 200
```

Test URL (`192.168.20.20`) ile doğrulama yapma — kullanıcı production istedi.

---

## İlk kurulum / devam noktası

**Her production oturumunda önce:** [DEVAM.md](./DEVAM.md) ve [PROD_SERVER_STATUS.md](./PROD_SERVER_STATUS.md).

Remote hata: `Docker yok` → IT sudo/docker; sonra `setup-mng-common-odak-prod.ps1`.  
`Start mng_common first` → P1 tamamlanmamış.  
`Missing apps dir` / `Missing .env` → [INITIAL_SETUP_PRODUCTION.md](./INITIAL_SETUP_PRODUCTION.md); test sunucu varsayımı kullanma.

---

## Doküman önceliği

1. Bu dosya (agent)
2. [DEPLOY_PRODUCTION.md](./DEPLOY_PRODUCTION.md) (komut detayı)
3. [../deploy/README.md](../deploy/README.md) (yalnızca test için parametre/sorun giderme mantığı)

---

## Yapılmaması gerekenler

- Varsayılan `deploy-odak-apps.ps1` / `sync-odak-source.ps1` çağrısı (**`-Server` olmadan**) → test sunucuya gider.
- Production deploy sonrası “tamam” demeden `20.8` health kontrolü.
- Test sunucu `.env` veya secret’larını production’a kopyalama önerisi (bilinçli migration olmadan).
