# MngLogs — Son durum (PARKED)

**Son güncelleme:** 2026-07-30  
**Durum:** MngLogs saha agent çalışmaları bu noktada **park edildi**. Sonraki oturum: Mng.Ui SIEM Center UI planlaması (ayrı chat). MngLogs’a dönüşte bu dosyadan devam.

## Son çalışılan konu

Windows-first: Local UI / PIN / CLI, MSI+GPO hazırlığı, collector event-log katalog pull, SIEM’de ince agent health paneli, HostId varsayılanı = PC adı.

## Tamamlanan işler

### Agent / Local UI
- Durum / Kaynaklar / Politika tab UI; Loglar detay modal
- PIN koruması; host services + exe browse
- CLI: `status`, `pin`, `port`, `config`, `catalog show|sync`
- Event Log: sunucu katalog ⊕ agent override / disabled; merger
- `HostId` boşsa ilk açılışta **PC adı** (`Environment.MachineName`) yazılıyor; Politika API efektif değer döner

### Collector (P2)
- `GET /api/v1/policy/eventlog-packages` (ingest API key)
- ETag / `If-None-Match` → 304
- Agent `RefreshAsync` pull + builtin/cache fallback
- Ingest: `fields["event.action"]` öncelikli OpenSearch map

### Dağıtım (P0 — paket hazır, smoke admin bekliyor)
- WiX MSI: `MngLogs.Agent.Setup` → `artifacts\msi\MngLogs.Agent-0.2.0.msi`
- Script’ler: `publish-agent`, `build-msi`, `install/uninstall-windows-service`, `write-system-config`
- Servis adı: `MngLogsAgent`; data: `%ProgramData%\MngLogs\Agent`
- Self-update **yok** → GPO/MSI MajorUpgrade

### Merkez UI (P4 ince dilim)
- SIEM Center: `AcAgentHealthPanel` (`host.up` / `watch.inventory`)
- Olay filtreleri: `metric`, `windows-eventlog` source type

### Dokümanlar
- MkDocs: `docs/content/MngLogs/` (changelog, roadmap, technical specs, guides)
- IT helper: `docs/odak/siem/mnglogs/it_helper/` (01–07 + README)
- Sözleşme: `docs/odak/siem/mnglogs/POLICY_EVENTLOG_PACKAGES.md`

## Bilinçli ertelemeler / bekleyen testler

| Madde | Not |
|--------|-----|
| MSI / Windows Service smoke | Lab’da admin yok (`msiexec` 1625). Elevated oturumda sonra |
| P3 Linux | Acele yok; Windows bitince |
| P5 Event Log parser kuralları | **Sıradaki ürün işi** (park sonrası dönüşte) |
| P4 genişletme | Unhealthy inventory özeti, widget kaydı — opsiyonel |
| Katalog admin CRUD | Collector’da hâlâ builtin seed |
| Auto port | Düşük öncelik |

## Sıradaki işler (öncelik — Windows)

1. **P5** — Event Log parser (Event ID → `event.action` / alan map; sunucu/collector ağırlıklı önerildi)
2. **MSI/Service smoke** — admin ortamında (`it_helper` 02–04)
3. P4 genişletme / katalog admin (ihtiyaca göre)
4. **P3 Linux** — en sonda

## Park öncesi bağlam (sonraki chat’e)

- Yeni chat konusu: **Mng.Ui SIEM Center UI planlaması** (MngLogs park; SIEM panel UX/plan).
- MngLogs’a dönüşte: P5 parser tartışması + kararlar (`current_status` + ROADMAP).

## Önemli yollar

| Öğe | Yol |
|-----|-----|
| Agent exe (publish) | `MngLogs\artifacts\agent\win-x64\MngLogs.Agent.exe` |
| MSI | `MngLogs\artifacts\msi\MngLogs.Agent-0.2.0.msi` |
| TFM | `net9.0-windows` |
| Local UI | `http://127.0.0.1:5092/` |
| Collector (Odak örn.) | `http://192.168.20.8:5091` |
| Collector (local) | `http://127.0.0.1:5091` |
| IT kılavuz | `docs/odak/siem/mnglogs/it_helper/` |
