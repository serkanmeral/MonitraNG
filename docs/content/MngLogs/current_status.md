# MngLogs — Son durum

**Son güncelleme:** 2026-07-31  
**Durum:** Ajan **1.0.4** — Windows Service kurulu (TERMINAL); watch prune + host.up zenginleştirme + Local UI LAN bind.

## Son çalışılan konu

Service/App Watch snapshot prune; process adı normalize; ajan 1.0.4 publish + elevated reinstall (`LocalUiHost=0.0.0.0`).

## Bu dilimde tamamlananlar

- Sürüm **1.0.4** (csproj / UI package / WiX yolu)
- `host.up` / inventory: IP, users, boot, uptime, sessions, `localUiPort` / `localUiHost`
- Watch snapshot: politika dışı hedefler budanır; inventory boşalınca ship
- `NormalizeProcessName`: path / `.exe` → kısa process adı; policy save dedupe
- Local UI Politika: uygulama kaydında ad normalize
- Session 0 notu: servisten başlatılan GUI (notepad) masaüstünde görünmez; process Task Manager’da görünür
- Reinstall script: `scripts/tests/MngLogs/windows-service/reinstall-agent-odak-elevated.ps1`

## Bekleyen

1. P5 Event Log parser (park)
2. Uygulama restart’ı aktif kullanıcı oturumuna alma (WTS / CreateProcessAsUser) — ürün kararı
3. MSI/GPO saha yayılımı (lab kurulumu yapıldı)

## Önceki ertelemeler

| Madde | Not |
|--------|-----|
| P5 parser | Park |
| P3 Linux | Sonra |
| Alarm/Notifier watch kuralları | SIEM tarafında erken |

## Önemli yollar

| Öğe | Yol |
|-----|-----|
| Agent | `MngLogs\Presentation\MngLogs.Agent\` |
| Publish | `.\MngLogs\scripts\publish-agent.ps1` |
| Service install | `.\MngLogs\scripts\install-windows-service.ps1` |
| Reinstall (Odak) | `.\scripts\tests\MngLogs\windows-service\reinstall-agent-odak-elevated.ps1` |
| Local UI | `http://127.0.0.1:5092/` · LAN `:5092` |
| Collector | `http://192.168.20.8:5091` |
| HostId (pilot) | `TERMINAL-pilot` |
| Sürüm | csproj `Version` / `AgentVersion.Current` → **1.0.4** |
