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

```powershell
# UI build (wwwroot)
cd MngLogs
.\scripts\build-frontend.ps1

# Agent
cd Presentation/MngLogs.Agent
dotnet run
```

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
