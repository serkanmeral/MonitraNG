# MngLogs — Son durum

**Son güncelleme:** 2026-09-02  
**Durum:** Windows + Linux agent canlı. **Günlük hedef = Odak PROD Collector** `http://192.168.20.8:5091`. Host telemetrisi NXLog/rsyslog yerine yalnızca agent. **Alarm observation publish açık** (`SourceProducts=*`). Bu TERMINAL’de DLP lab collector **test** `http://192.168.20.20:5091` (agent 1.0.11); prod’a bilinçsiz retarget yok.

## Son çalışılan konu

Collector → `monitra.observations`: paket-seviye observation key (RDP semantik opsiyonel). PowerShell paket olayları Alarm’a düşer. Ayrıntı: `docs/odak/alarm/AGENT_OBSERVATION_AND_FLOW_LAB.md`.

## Plan / faz

`P3a ✅ → P3b ✅ → P3c ✅ → host cutover ✅ → P3d (deb) → Host Analytics L3 → P3c-bridge (opsiyonel)`

| Faz | Durum |
|-----|--------|
| P3a metrik/iskelet | ✅ |
| P3b systemd/app watch + restart | ✅ |
| P3c journald (sshd/sudo/unit-fail) | ✅ |
| Host NXLog cutover | ✅ — [HOST_TELEMETRY_CUTOVER.md](../../odak/siem/HOST_TELEMETRY_CUTOVER.md) |
| P3d .deb | Sonraki |
| Host Analytics L3 | Park |
| P3c-bridge (rsyslog köprü) | İptale yakın / opsiyonel (ingest kapalı) |

## Pilot / canlı

| | Windows (TERMINAL) | Linux |
|--|-------------------|--------|
| HostId | `TERMINAL-pilot` | `monitrang-linux-pilot` (20.20) · `monitrang-prod` (20.8) |
| Collector | **`http://192.168.20.8:5091`** | aynı (prod host’ta `127.0.0.1:5091`) |
| Local UI | `:5092` | `:5092` |
| Paketler | Security + `rdp-session` vb. | journal sshd/sudo/unit-fail |

### Deploy / retarget

```powershell
# Prod Linux deploy
pwsh -File .\scripts\tests\MngLogs\linux\deploy-agent-odak-prod.ps1

# Kurulu Windows agent → prod collector (elevated)
pwsh -File .\scripts\tests\MngLogs\windows-service\retarget-collector-elevated.ps1

# Yalnızca bilinçli TEST (sonra prod'a geri dön!)
pwsh -File .\scripts\tests\MngLogs\linux\deploy-agent-odak-test.ps1
```

## Kod

| Öğe | Yol |
|-----|-----|
| Core | `MngLogs/Presentation/MngLogs.Agent.Core/` |
| Windows | `MngLogs/Presentation/MngLogs.Agent/` |
| Linux | `MngLogs/Presentation/MngLogs.Agent.Linux/` |
| RDP normalize (sunucu) | `MngLogCollector/.../AgentSecEventActionNormalizer.cs` |

## DLP / Outlook (park — 2 Eyl 2026)

Origin DLP Dilim 0+1 motor bu Windows agent **1.0.11** içinde (`POST /dlp/evaluate`, politika sync). Classic Outlook COM eklentisi kuruldu; **Active teyidi Office IT aktivasyonuna bırakıldı**. Ayrıntı: `docs/odak/dlp/current_status.md`.

## Park

- UI’den parametreli agent indir
- P3d deb · Analytics L3
- Windows→Core refactor (devam)
- DLP Outlook ItemSend lab (Office lisansı sonrası)

## Kontrol

Bu TERMINAL (DLP lab): Local UI `collectorBaseUrl` = **test** `:5091` (`192.168.20.20`), agent 1.0.11.  
Linux prod host varsayılanı hâlâ `192.168.20.8:5091`. SIEM Events: filtre kataloğu modal · RDP `sourceProduct` / `event.code` OpenSearch üzerinden (prod Reactor ✓)
