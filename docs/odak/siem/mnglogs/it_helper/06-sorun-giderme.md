# 06 — Sorun giderme

## Hızlı kontrol sırası

1. `Get-Service MngLogsAgent` → Status **Running** mi?  
2. `%ProgramData%\MngLogs\Agent\logs\agent-*.log` son satırlar  
3. `MngLogs.Agent.exe status` ve `port check`  
4. `http://127.0.0.1:5092/health` (veya ayarlı port)  
5. `config show` → collector URL doğru mu?  
6. msiexec log (`/L*v`) — kurulum sorunlarında  

## Sık hatalar

### msiexec exit 1625

**Anlam:** Bu kurulum sistem politikası tarafından yasaklandı / yetki yok.

**Çözüm:** Yönetici (elevated) oturum; veya GPO/Software Restriction / DisableMSI politikasını kontrol edin.

### Servis kurulu ama Running değil

```powershell
Get-Service MngLogsAgent | Format-List *
Get-WinEvent -LogName Application -MaxEvents 20 |
  Where-Object { $_.ProviderName -match "MngLogs|Service Control Manager" }
Get-Content "$env:ProgramData\MngLogs\Agent\logs\agent-*.log" -Tail 100
```

Sık neden: **Local UI port dolu** (çıkış 3).  

```powershell
& "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe" port check
& "C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe" port set 5093
Restart-Service MngLogsAgent
```

### Health / UI açılmıyor

- Servis Running mi?  
- Doğru port mu? (`config show` / `port show`)  
- Bind host `127.0.0.1` — uzak makineden tarayıcı ile açılamaz (bilinçli).  
- wwwroot eksik MSI? Yeniden kurun / doğru MSI kullandığınızdan emin olun.

### Collector’a veri gitmiyor

- `config show` → `CollectorBaseUrl` / ApiKey  
- Ağ / firewall collector portuna  
- Agent loglarında ship / HTTP hataları  
- Disk kuyruk: `%ProgramData%\MngLogs\Agent\queue` (veya UI Kuyruk sayfası)  

### Security Event Log okunamıyor

- Servis LocalSystem ile çalışıyor mu?  
- Log: access denied → hesap / kanal izni  
- Security paketi opsiyonel; elevation gerekebilir  

### PIN kilit / unutuldu

```powershell
& $exe pin reset --yes
```

UI’da yeniden PIN setup.

### GPO uygulandı ama yazılım yok

- UNC share erişimi (makine hesabı okuyabiliyor mu?)  
- Computer Configuration kullanıldı mı?  
- Reboot sonrası `gpresult /r`  
- Event Viewer → Group Policy / Application logları  

### Upgrade sonrası eski davranış

- ProgramData eski config’i tutar — beklenen  
- Yeni default’lar için bilinçli `config set` veya pilot wipe  
- Servisi restart ettiniz mi?  

## Faydalı yollar (tekrar)

| Öğe | Yol |
|-----|-----|
| Exe | `C:\Program Files\MngLogs\Agent\MngLogs.Agent.exe` |
| Config | `%ProgramData%\MngLogs\Agent\system.json` |
| Log | `%ProgramData%\MngLogs\Agent\logs\` |
| Servis | `MngLogsAgent` |

## İletişim için toplanacak bilgiler

IT destek kaydına ekleyin:

- Agent / MSI sürümü  
- `status` ve `config show` çıktısı (API key maskeli)  
- Son log dosyasından ~100 satır  
- `Get-Service MngLogsAgent` çıktısı  
- Kurulum ise msiexec log’undan hata bloğu  
