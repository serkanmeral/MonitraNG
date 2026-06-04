# SIEM — `mon_alarm_rules` taslakları (U1/U2/U4)

**Amaç:** Faz 2 alarm entegrasyonu öncesi kural JSON'ları — `POST /alarm/api/v1/rules` gövdesi ile uyumlu.

**Kaynak senaryolar:** [SIEM_PLANNING.md §6–7](../../../docs/odak/monitoring/SIEM_PLANNING.md)

## Dosyalar

| Dosya | Senaryo | `type` | Durum |
|-------|---------|--------|-------|
| `u1_brute_force_login_failed.json` | U1 — brute force | `correlation` | ✅ Faz 2'de canlı (motor hazır) |
| `u4_firewall_deny_spike.json` | U4 — deny artışı | `correlation` | ✅ Faz 2'de canlı |
| `u2_fail_then_success_login.json` | U2 — fail→success | `sequence` | ✅ Faz 2+ (MngAlarm `sequence` tipi) |

## Önkoşul: `sec_events` → observation

Kurallar `matchKey` + `dimensions` ile çalışır. Parser çıktısından observation eşlemesi:

| `sec_events` alanı | Observation `key` / `dimensions` |
|--------------------|----------------------------------|
| `event.action=login_failed` | `key: login_failed` |
| `event.action=login_success` | `key: login_success` |
| `event.action=denied_flow` | `key: denied_flow` |
| `actor.user` | `dimensions.userId` |
| `network.srcIp` | `dimensions.srcIp` |
| `network.dstIp` | `dimensions.dstIp` |
| `network.dstPort` | `dimensions.dstPort` |

Detay: [SEC_EVENT_OBSERVATION_MAP.md](../../../docs/odak/monitoring/SEC_EVENT_OBSERVATION_MAP.md)

## Odak'ta yükleme (Faz 2 entegrasyon sonrası)

```powershell
$base = "http://192.168.20.20:5040/alarm/api/v1"
$hdr = @{ Authorization = "Bearer $token"; "X-Domain-Name" = "odak"; "Content-Type" = "application/json" }
$body = Get-Content .\tests\fixtures\siem\alarm_rules\u1_brute_force_login_failed.json -Raw
Invoke-RestMethod -Uri "$base/rules?domainName=odak" -Method POST -Headers $hdr -Body $body
```

## Eşik notları

- U1 `threshold: 10` / `windowMinutes: 5` — pilot müşteri ile ayarlanır.
- U4 `threshold: 50` — deny-only filtre + P0 profil ile başlangıç; [SIEM_PERFORMANCE_PLAN §2](../../../docs/odak/monitoring/SIEM_PERFORMANCE_PLAN.md).
