# 01 — MSI üretimi

Bu belge, release MSI paketinin nasıl üretileceğini anlatır. Günlük saha dağıtımında IT’nin MSI **üretmesi zorunlu değildir**; hazır `.msi` dosyasını dağıtır. Üretim genelde Monitra / geliştirme tarafında yapılır.

## Gereksinimler

- Windows x64
- PowerShell 7+
- .NET 9 SDK
- Node.js / npm (Local UI `wwwroot` derlemesi için; `-SkipFrontend` ile atlanabilir)
- WiX Toolset 5 (`dotnet tool install --global wix --version 5.0.2`)

## Kaynak konumları

| Öğe | Yol |
|-----|-----|
| Repo kökü | `MonitraNG` |
| Agent projesi | `MngLogs\Presentation\MngLogs.Agent\` |
| WiX / MSI projesi | `MngLogs\Presentation\MngLogs.Agent.Setup\` |
| Üretim script’i | `MngLogs\scripts\build-msi.ps1` |
| Publish çıktısı | `MngLogs\artifacts\agent\win-x64\` |
| MSI çıktısı | `MngLogs\artifacts\msi\MngLogs.Agent-<sürüm>.msi` |

## Tek komutla MSI üretme

```powershell
cd <repo>\MngLogs

# Tam üretim (UI generate + publish + WiX)
.\scripts\build-msi.ps1

# wwwroot zaten güncel ise
.\scripts\build-msi.ps1 -SkipFrontend

# Sadece WiX (önceden publish edilmiş payload varsa)
.\scripts\build-msi.ps1 -SkipFrontend -SkipPublish

# Sürüm numarası
.\scripts\build-msi.ps1 -SkipFrontend -AgentVersion 0.2.1
```

Başarılı çıktı örneği:

```text
MSI ready: ...\MngLogs\artifacts\msi\MngLogs.Agent-0.2.0.msi
```

## Paket içeriği (özet)

- Self-contained `win-x64` agent + Local UI (`wwwroot`)
- Windows Service kaydı: `MngLogsAgent`
- `%ProgramData%\MngLogs\Agent` klasörleri + varsayılan `system.json` seed
- Public property’ler: `COLLECTORURL`, `APIKEY`, `HOSTID`, `LOCALUIHOST`, `LOCALUIPORT`

## Sadece binary publish (MSI’siz)

Acil test veya script ile servis kurulumu için:

```powershell
cd <repo>\MngLogs
.\scripts\publish-agent.ps1
# Çıktı: artifacts\agent\win-x64\
```

Script ile kurulum (MSI alternatifi): `.\scripts\install-windows-service.ps1` — ayrıntı için [02-msi-kurulum.md](02-msi-kurulum.md) içindeki “MSI dışı” bölümüne bakın.

## IT’ye teslim checklist

- [ ] `MngLogs.Agent-<sürüm>.msi` dosyası
- [ ] Sürüm numarası ve Release notu (kısa)
- [ ] Önerilen `COLLECTORURL` / port bilgisi
- [ ] Bu `it_helper` klasörünün kopyası
- [ ] (İsteğe bağlı) Site bazlı `.mst` transform şablonu — [03-msi-dagitim-gpo.md](03-msi-dagitim-gpo.md)

## Notlar

- `artifacts\` klasörü genelde git’e commit edilmez; MSI ayrı artifact deposuna / share’e konur.
- Aynı **UpgradeCode** ile major upgrade desteklenir; downgrade MSI tarafından engellenir.
