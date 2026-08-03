# Host telemetry — MngLogs agent yolu (NXLog cutover)

**Son güncelleme:** 3 Ağustos 2026  
**İlgili:** [current_status.md](./current_status.md) · [ENVIRONMENTS.md](../proddeploy/ENVIRONMENTS.md) · [MngLogs current_status](../../content/MngLogs/current_status.md)

## Karar

Windows ve Linux **host** telemetrisi (Event Log / journal / RDP oturumları / watch) yalnızca **MngLogs agent → MngLogCollector → OpenSearch** yolundan gelir.

| Kaynak | Durum |
|--------|--------|
| MngLogs Windows / Linux agent | ✅ Aktif — Collector `:5091` |
| FortiGate syslog | ✅ Engine UDP `:541` / `:542` (firewall) |
| NXLog → Engine/Reactor | ❌ Kapalı (`AcceptNxlogIngest=false`) |
| Linux rsyslog → Engine/Reactor | ❌ Kapalı (`AcceptLinuxSyslogIngest=false`) |
| WEC batch | ❌ Kapalı (`WecIngestEnabled=false`) |

## Veri yolu

```
Host (Windows/Linux)
  → MngLogs.Agent (Local UI :5092)
  → MngLogCollector (:5091)  → OpenSearch (mng-{domain}-sec-events-*)
  → MngReactor SIEM okuma (OpenSearchReadEnabled=true)
```

Mongo `sec_events` host NXLog dönemi geçmiş veri içerebilir; canlı host olayları OS’tedir.

## RDP oturum olayları

| Event ID | `event.action` (normalize) |
|----------|----------------------------|
| 21 | `rdp.logon` |
| 23 | `rdp.logoff` |
| 24 | `rdp.disconnect` |
| 25 | `rdp.reconnect` |

- LogCollector: `AgentSecEventActionNormalizer` (+ EventData actor/network).
- Reactor/UI sorgu: `eventActionPrefix=rdp.` ayrıca `event.code` 21–25 ve `source.product=rdp-session` ile genişler.
- Paket: katalog `rdp-session` (LocalSessionManager/Operational).

## Ortam (PROD varsayılan)

Günlük çalışma ve lokal Mng.Ui **production** (`192.168.20.8`) hedefler.

| Bileşen | Prod hedef |
|---------|------------|
| Lokal `Mng.Ui/.env` | `GATEWAY_URL=http://192.168.20.8:5040` |
| Nuxt `ODAK_HOST` fallback | `192.168.20.8` |
| Windows agent collector | `http://192.168.20.8:5091` |
| Linux agent collector | `http://192.168.20.8:5091` (veya aynı host’ta `http://127.0.0.1:5091`) |

### Scriptler

| Script | Amaç |
|--------|------|
| `scripts/tests/MngLogs/linux/deploy-agent-odak-prod.ps1` | Linux deploy → **prod** collector |
| `scripts/tests/MngLogs/linux/deploy-agent-odak-test.ps1` | Yalnızca bilinçli **test** (20.20) |
| `scripts/tests/MngLogs/windows-service/retarget-collector-elevated.ps1` | Kurulu Windows agent collector URL (varsayılan prod) |
| `scripts/tests/MngLogs/windows-service/reinstall-agent-odak-elevated.ps1` | Yeniden kurulum (varsayılan prod `:5091`) |

Test’e geçici dönüş: `cp Mng.Ui/.env.odak.test.example Mng.Ui/.env` + agent retarget `-CollectorUrl http://192.168.20.20:5091`. İş bitince **mutlaka prod’a geri alın**.

## Compose notları

- `docker-compose.odak.yml` / `.odak.prod.yml`: NXLog portları (1514/1541/1542) ve Linux syslog 5514 kaldırıldı; FortiGate 541/542 kaldı.
- Test stack’te OpenSearch + `mnglogcollector` gerekli (agent yolu için).
- Prod’da Collector zaten ayakta; agent’lar prod `:5091`’e bakar.

## Bilinen tuzaklar

1. **UI test gateway + prod agent (veya tersi)** — SIEM boş / login `User account is inactive` (test Keeper’da `isActive:false`). Ortamları karıştırmayın.
2. **RDP filtresi boş** — ham mesaj `event.action` iken UI `rdp.*` bekliyordu; normalizer + prefix genişletmesi şart (yukarıda).
3. Lokal Nuxt `.env` değişince **dev server restart** gerekir.
