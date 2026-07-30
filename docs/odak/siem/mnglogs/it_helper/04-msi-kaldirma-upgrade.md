# 04 — Kaldırma ve yükseltme (upgrade)

## Yükseltme (upgrade)

Aynı ürün ailesi **MajorUpgrade** kullanır (sabit UpgradeCode). Yeni MSI kurulduğunda eski sürüm kaldırılır, yeni binary yazılır.

> Strateji özeti (self-update yok): [07-surum-guncelleme.md](07-surum-guncelleme.md)

```powershell
msiexec /i \\fileserver\Software\MngLogs.Agent-0.2.1.msi /qn /L*v $env:TEMP\mnglogs-upgrade.log `
  COLLECTORURL=http://siem-collector:5091 `
  APIKEY="..."
```

### Upgrade’de ne korunur?

| Öğe | Davranış |
|-----|----------|
| `%ProgramData%\MngLogs\Agent\system.json` | Korunur (`NeverOverwrite` + Permanent seed) |
| `policy.json`, PIN (`ui-auth.json`), kuyruk, bookmarks | Korunur (ProgramData) |
| `C:\Program Files\MngLogs\Agent\*` | Yeni sürümle değişir |
| Servis kaydı | Yenilenir / devam eder |

> Downgrade (eski MSI üzerine yeni varken eskiyi zorla kurmak) engellenir. Gerekirse önce uninstall.

Upgrade sonrası config property vermezseniz mevcut `system.json` kalır. Property verirseniz `config set` ile üzerine yazılabilir (CA koşulu `COLLECTORURL` doluysa).

## Kaldırma (uninstall)

### MSI ile

```powershell
# Ürün kodu veya MSI yolu ile
msiexec /x \\fileserver\Software\MngLogs.Agent-0.2.0.msi /qn /L*v $env:TEMP\mnglogs-uninstall.log

# veya Programs and Features / Get-Package üzerinden
Get-Package "*MngLogs*" | Uninstall-Package -Force
```

### Kaldırma sonrası

| Öğe | Varsayılan |
|-----|------------|
| Program Files binary | Silinir |
| Windows Service `MngLogsAgent` | Kaldırılır |
| `%ProgramData%\MngLogs\Agent` | **Kalır** (`system.json` Permanent; kuyruk/PIN genelde yerinde) |

### ProgramData’yı da silmek (temiz silme)

Yalnızca bilinçli wipe için (yeniden kurulumda sıfır config isteniyorsa):

```powershell
# Servis / MSI kaldırıldıktan sonra, yönetici:
Remove-Item -Recurse -Force "$env:ProgramData\MngLogs\Agent"
# İsteğe bağlı registry izi:
Remove-Item -Recurse -Force "HKLM:\Software\MonitraNG\MngLogsAgent" -ErrorAction SilentlyContinue
```

Script yolu (MSI kullanılmadan kurulmuş lab):

```powershell
.\scripts\uninstall-windows-service.ps1 -RemoveBinaries
# Data dahil:
.\scripts\uninstall-windows-service.ps1 -RemoveBinaries -RemoveData
```

## GPO scope dışına çıkma

GPO’da “Uninstall when falls out of scope” açıksa makine OU’dan çıkınca MSI uninstall tetiklenir. ProgramData yine kalabilir — filo politikasında “tam wipe” isteniyorsa ek cleanup script gerekir.

## Servisi geçici durdurma (kaldırmadan)

```powershell
Stop-Service MngLogsAgent
Start-Service MngLogsAgent
Restart-Service MngLogsAgent
```

Bakım penceresinde Local UI / ship durur; kuyruk diskte birikir (policy’ye bağlı).

## Doğrulama

```powershell
Get-Service MngLogsAgent -ErrorAction SilentlyContinue
Test-Path "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe"
Test-Path "$env:ProgramData\MngLogs\Agent"
```
