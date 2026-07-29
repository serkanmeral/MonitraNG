# MngLogs

Saha uygulaması (Windows Service + yerel UI). Log/metrik toplar, disk kuyruğuna yazar, **MngLogCollector** endpoint’ine gönderir.

Sunucu backend stack’inin parçası değildir.

## Yetenekler

- Metrik: `host.up`, CPU, bellek, disk
- Event Log paketleri (admin gerekir — özellikle Security)
- **Service watch**: policy’deki Windows servisleri; fail/missing/recovered event; opsiyonel `restartAllowed`
- **Yerel UI** (Nuxt, MngEngine Edge kalıbı): Durum · Kuyruk · Kaynaklar · Loglar · Politika

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
