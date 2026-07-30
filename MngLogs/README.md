# MngLogs

Saha uygulaması (Windows Service + yerel UI). Log/metrik toplar, disk kuyruğuna yazar, **MngLogCollector** endpoint’ine gönderir.

Sunucu backend stack’inin parçası değildir.

## Yetenekler

- Metrik: `host.up`, CPU, bellek, disk, üst süreç özeti, `watch.inventory`
- Event Log paketleri (sunucu katalog ⊕ agent override; Security opsiyonel / elevation)
- **Service / uygulama izleme**: snapshot, OS SCM korelasyonu, opsiyonel restart
- **Yerel UI** (Nuxt): Durum · Kuyruk · Kaynaklar · Loglar · Politika (PIN korumalı yazma)
- **CLI kurtarma**: PIN reset/set, port check/set, status

Dokümantasyon: `docs/content/MngLogs/` (changelog, roadmap, technical specs, Local UI/CLI rehberi).

## Windows Service / GPO (P0)

Hedef dağıtım: müşteri IT’sinin **AD Group Policy / Software Installation** ile per-machine MSI dağıtması.
Kurulum **sessiz, makine kapsamlı, LocalSystem**; config `%ProgramData%\MngLogs\Agent` altında (upgrade’de binary gider, data kalır).

### MSI (önerilen)

```powershell
cd MngLogs
.\scripts\build-msi.ps1 -SkipFrontend

# Yönetici PowerShell / GPO eşdeğeri:
msiexec /i .\artifacts\msi\MngLogs.Agent-0.2.0.msi /qn /L*v $env:TEMP\mnglogs-agent-install.log `
  COLLECTORURL=http://192.168.20.8:5091 `
  APIKEY=your-key `
  HOSTID= `
  LOCALUIHOST=127.0.0.1 `
  LOCALUIPORT=5092
```

Public property’ler (MST ile de verilebilir): `COLLECTORURL`, `APIKEY`, `HOSTID`, `LOCALUIHOST`, `LOCALUIPORT`.

```powershell
# Smoke
..\scripts\tests\MngLogs\windows-service\smoke-service.ps1
msiexec /x .\artifacts\msi\MngLogs.Agent-0.2.0.msi /qn
```

### Script ile (MSI öncesi / acil)

```powershell
.\scripts\publish-agent.ps1
.\scripts\install-windows-service.ps1 `
  -SourceDir .\artifacts\agent\win-x64 `
  -CollectorUrl http://192.168.20.8:5091 `
  -ApiKey 'your-key' `
  -StartService
```

- Servis adı: `MngLogsAgent` (görünen ad: MngLogs Agent)
- Loglar: `%ProgramData%\MngLogs\Agent\logs\agent-*.log`
- CLI config: `MngLogs.Agent.exe config show|set ...`

Local UI: `http://127.0.0.1:5092/`  
Collector URL (ör. Odak): `http://192.168.20.8:5091`

### CLI kurtarma (PIN / port)

Agent exe aynı binary üzerinden CLI çalıştırır (web host başlamaz):

```powershell
cd Presentation/MngLogs.Agent
dotnet run -- status
dotnet run -- port check
dotnet run -- port set 5093
dotnet run -- pin reset --yes
dotnet run -- pin set
# veya derlenmiş:
.\bin\Release\net9.0-windows\MngLogs.Agent.exe port set 5093 --data-dir "$env:TEMP\MngLogs-Agent-Pilot"
```

Port çakışmasında agent açılışta hata verir ve aynı komutları önerir. Port/PIN değişiminden sonra süreci yeniden başlatın.

Policy örneği (`%ProgramData%\MngLogs\Agent\policy.json` veya DataDirectory):

```json
"serviceWatch": {
  "enabled": true,
  "pollIntervalSeconds": 15,
  "services": [
    { "name": "Spooler", "restartAllowed": false }
  ]
}
```
