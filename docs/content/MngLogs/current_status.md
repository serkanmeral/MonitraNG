# MngLogs — Son durum

**Son güncelleme:** 2026-08-03  
**Durum:** Windows + Linux agent canlı. **Günlük hedef = Odak PROD Collector** `http://192.168.20.8:5091`. Host telemetrisi NXLog/rsyslog yerine yalnızca agent.

## Son çalışılan konu

Prod sabitleme + host cutover: agent’lar ve lokal UI prod’a; RDP action normalize LogCollector’da; NXLog/Linux syslog ingest kapalı.

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

## Park

- UI’den parametreli agent indir
- P3d deb · Analytics L3
- Windows→Core refactor (devam)

## Kontrol

Local UI Durum: `collectorBaseUrl` = prod `:5091`, `collectorHealthy: true`  
SIEM Events (prod UI): RDP intent / host filtreleri OpenSearch üzerinden
