# 02 — MSI kurulum

## Önkoşullar

- Hedef: Windows 10/11 veya Windows Server (x64)
- Kurulumu çalıştıran hesap: **Yerel Administrators** veya GPO SYSTEM
- Local UI varsayılan portu boş olmalı: **5092** (TCP, localhost)
- Collector erişilebilir olmalı (kurulum anında zorunlu değil; ship sonra başarısız olur)

## Klasörler (kurulum sonrası)

| Yol | İçerik |
|-----|--------|
| `C:\Program Files\MngLogs\Agent\` | Exe, DLL, wwwroot |
| `%ProgramData%\MngLogs\Agent\` | `system.json`, `policy.json`, kuyruk, PIN (`ui-auth.json`), bookmarks |
| `%ProgramData%\MngLogs\Agent\logs\` | `agent-*.log` (rolling) |

## Sessiz kurulum (önerilen)

Yönetici PowerShell:

```powershell
$msi = "\\fileserver\share\MngLogs.Agent-0.2.0.msi"
$log = "$env:TEMP\mnglogs-agent-install.log"

msiexec /i $msi /qn /L*v $log `
  COLLECTORURL=http://siem-collector.sirket.local:5091 `
  APIKEY="site-api-key" `
  HOSTID="" `
  LOCALUIHOST=127.0.0.1 `
  LOCALUIPORT=5092
```

`HOSTID` boş bırakılırsa agent makine adını kullanır.

### MSI public property’leri

| Property | Zorunlu | Açıklama | Varsayılan |
|----------|---------|----------|------------|
| `COLLECTORURL` | Önerilir | Collector base URL | (boşsa seed `system.json` kalır; CA atlanır) |
| `APIKEY` | Ortama göre | Collector API key | boş |
| `HOSTID` | Hayır | Sabit host kimliği | boş → machine name |
| `LOCALUIHOST` | Hayır | Local UI bind host | `127.0.0.1` |
| `LOCALUIPORT` | Hayır | Local UI port | `5092` |

`COLLECTORURL` verildiğinde kurulum, `MngLogs.Agent.exe config set ...` ile `%ProgramData%\MngLogs\Agent\system.json` dosyasını günceller.

## Etkileşimli kurulum

```powershell
msiexec /i MngLogs.Agent-0.2.0.msi
```

UI sihirbazı sınırlıdır; saha için **sessiz + property** tercih edilir.

## Kurulum doğrulama

```powershell
Get-Service MngLogsAgent
Get-Service MngLogsAgent | Format-List Name, Status, StartType, DisplayName

# Health
Invoke-RestMethod http://127.0.0.1:5092/health

# CLI
& "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe" status
& "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe" config show
```

Repo içinde smoke script (kurulum yapılmış makinede):

```powershell
.\scripts\tests\MngLogs\windows-service\smoke-service.ps1
```

Local UI tarayıcı: `http://127.0.0.1:5092/`  
Politika sayfası PIN ister (ilk kurulumda PIN oluşturun veya CLI ile yönetin — [05-cli-referans.md](05-cli-referans.md)).

## MSI dışı kurulum (acil / lab)

MSI yoksa publish klasöründen:

```powershell
cd <repo>\MngLogs
.\scripts\publish-agent.ps1 -SkipFrontend

# Yönetici
.\scripts\install-windows-service.ps1 `
  -SourceDir .\artifacts\agent\win-x64 `
  -CollectorUrl http://siem-collector.sirket.local:5091 `
  -ApiKey "site-api-key" `
  -StartService
```

Üretim filolarında **MSI / GPO** kullanın; script lab ve acil müdahale içindir.

## Log konumları

| Kaynak | Yol |
|--------|-----|
| Agent uygulama logu | `%ProgramData%\MngLogs\Agent\logs\agent-YYYYMMDD.log` |
| msiexec verbose | kurulumda verdiğiniz `/L*v` dosyası |
| Windows Service | services.msc → MngLogs Agent |

## Sonraki adım

Filo dağıtımı: [03-msi-dagitim-gpo.md](03-msi-dagitim-gpo.md)
