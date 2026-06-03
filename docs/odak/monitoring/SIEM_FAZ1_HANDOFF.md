# SIEM Faz 1 Spike — Handoff (MngEngine + MngReactor)

**Hedef:** `MngReactor/`, `MngEngine/` (MonitraNG monorepo)  
**MonitraNG hazırlığı:** fixture + plan dokümanları ✅  
**Implementasyon rehberi:** [MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md](./MNGREACTOR_SIEM_FAZ1_IMPLEMENTATION.md) · **Odak deploy:** [MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md](./MNGREACTOR_ODAK_DEPLOY_CHECKLIST.md)  
**Ana plan:** [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md) · [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md)

Spike **alarm engine ve workflow dışında** kalır; çıktı: `sec_events` Mongo + minimal `sec_events.created` MQ.

---

## MonitraNG'den kopyalanacak fixture'lar

Kaynak (bu repo):

| Dosya | Kullanım |
|-------|----------|
| `tests/fixtures/siem/firewall_deny.syslog.txt` | `firewall.generic_syslog.v1` unit + E2E S2 |
| `tests/fixtures/siem/windows_4625_failed_logon.json` | `windows.security.v1` unit + E2E S3 |
| `tests/fixtures/siem/windows_4624_success_logon.json` | U2 parser / sequence test (Faz 2+) |
| `tests/fixtures/siem/unparseable_01.txt` | Parse fail fallback S4 |
| `tests/fixtures/siem/alarm_rules/*.json` | U1/U2/U4 kural taslağı (Faz 2) |

**MngReactor repo'ya öneri:** `tests/fixtures/siem/` altına aynı dosyaları kopyala (veya git submodule / CI artifact).

---

## Fixture → beklenen parse çıktısı

### `firewall_deny.syslog.txt`

Ham satır:

```text
2026-06-03T14:00:01 fw01 kernel: DENY IN=eth0 ... SRC=203.0.113.5 DST=10.0.0.10 ... PROTO=TCP ... DPT=445
```

| Alan | Beklenen |
|------|----------|
| `event.action` | `denied_flow` |
| `event.outcome` | `failure` |
| `network.srcIp` | `203.0.113.5` |
| `network.dstIp` | `10.0.0.10` |
| `network.dstPort` | `445` |
| `network.protocol` | `tcp` |
| `source.type` | `firewall` |
| `source.product` | `generic-syslog` (veya ingest'te set edilen değer) |
| `parser.id` | `firewall.generic_syslog.v1` |
| `raw` | Ham satır korunmuş |

### `windows_4625_failed_logon.json`

| Alan | Beklenen |
|------|----------|
| `event.action` | `login_failed` |
| `event.outcome` | `failure` |
| `actor.user` | `admin` |
| `network.srcIp` | `192.168.1.50` |
| `@timestamp` | `2026-06-03T14:00:02Z` (TimeCreated) |
| `source.type` | `ad` |
| `source.product` | `windows` |
| `parser.id` | `windows.security.v1` |
| Windows EventID | `4625` (metadata veya `event.code`) |

---

## Faz S1 — MngReactor (hafta 1)

| # | Görev | Kabul |
|---|-------|-------|
| S1.1 | Ingest batch: `kind: "sec_event"` discriminator | Metrik batch ile aynı auth; ayrı işlem yolu |
| S1.2 | `ISecEventParser` + registry | [SIEM_PARSER_PLAN §4](./SIEM_PARSER_PLAN.md) |
| S1.3 | `sec_events` Mongo collection + insert repo | DB: `mng_{domain}` |
| S1.4 | İndeksler | `@timestamp`, `source.type`, `event.action`, `network.srcIp` |
| S1.5 | Parse fail fallback | Belge yazılır; `event.action=unknown`, `raw` dolu |
| S1.6 | MQ publish `sec_events.created` | Minimal JSON body; en az 1 test subscriber/log |

**Önerilen dosya yapısı (MngReactor):**

```text
Core/.../SecEvents/ISecEventParser.cs
Core/.../SecEvents/SecEventRawContext.cs
Infrastructure/.../Parsers/FirewallGenericSyslogParser.cs
Infrastructure/.../Parsers/WindowsSecurityParser.cs
Infrastructure/.../Persistence/SecEventRepository.cs
```

---

## Faz S2 — Parser unit test (MngReactor)

| # | Test | Fixture |
|---|------|---------|
| S2.1 | Firewall deny parse | `firewall_deny.syslog.txt` → S2 beklenen alanlar |
| S2.2 | Windows 4625 parse | `windows_4625_failed_logon.json` |
| S2.3 | Unparseable input | `event.action=unknown`, ingest başarılı |
| S2.4 | Registry routing | `source.product=windows` → `windows.security.v1` |

---

## Faz S3 — MngEngine (hafta 2)

| # | Görev | Kabul |
|---|-------|-------|
| S3.1 | Syslog UDP listener `:514` | Mesaj internal queue |
| S3.2 | Syslog → batch item builder | `source.type`, `source.product`, `source.host`, `raw.message`, `receivedAt` |
| S3.3 | Batch → Reactor ingest (Bearer) | Mevcut schedule/eşik ile uyumlu |
| S3.4 | **Spike B (öncelik):** Windows fixture dosyasından batch push | WEC olmadan parser testi |
| S3.5 | Spike A (opsiyonel Odak): WEC okuma | Lab'de WEC varsa paralel |

**Batch öğesi örneği:** [SIEM_FAZ1_SPIKE §4](./SIEM_FAZ1_SPIKE.md)

---

## Faz S4 — E2E doğrulama (Odak)

| # | Senaryo | Adım | Beklenen |
|---|---------|------|----------|
| S4.1 | S1 | netcat/logger → Engine:514 | Batch Reactor'a gider |
| S4.2 | S2 | firewall syslog ingest | Mongo `denied_flow`, IP'ler dolu |
| S4.3 | S3 | Windows 4625 fixture ingest | `login_failed`, `actor.user`, `network.srcIp` |
| S4.4 | S4 | Bilinmeyen format | Kayıt var, `unknown`, `raw` korunmuş |
| S4.5 | S5 | Mongo query | Son 1 saat `source.type=ad` |
| S4.6 | S6 | MQ | `sec_events.created` en az 1 mesaj |

**MonitraNG'ye eklenecek E2E (spike bitince):** `scripts/odak/test-siem-faz1-e2e.ps1` — S4.2–S4.6 otomasyonu.

---

## Spike kararları (onaylı — değiştirmeyin)

| # | Karar | Değer |
|---|-------|-------|
| D1 | Ingest discriminator | `kind: sec_event` ✅ |
| D2 | Windows yolu | Fixture first (B) |
| D3 | Syslog | UDP MVP |
| D4 | DB | Mongo `mng_{domain}` |
| D5 | Retention | TTL yok veya 30 gün test |

---

## Bilinçli dışarıda (Faz 2+)

MonitraNG tarafında **bu spike'ta yapılmayacak:**

| Madde | Neden |
|-------|-------|
| MngAlarm `sec_event` consumer | U1/U4 correlation — Faz 2 |
| Workflow Event Trigger | P4 tam — SIEM olgunlaşınca |
| Mng.Ui güvenlik paneli | Faz 2 |
| MITRE metadata | Faz 2 |
| `mon_alarm_rules` U1/U4 canlı | Alarm Faz 2+ entegrasyon |

---

## Definition of Done

- [ ] S1.1–S1.6 MngReactor PR
- [ ] S2.1–S2.4 unit test PASS
- [ ] S3.1–S3.3 MngEngine PR (+ S3.4 fixture push)
- [ ] Odak S4.1–S4.6 manuel veya script PASS
- [ ] `SIEM_FAZ1_SPIKE.md` durum: implementasyon ✅
- [ ] MonitraNG `docs/odak/monitoring/` DEVAM notu (opsiyonel)

---

## Sonraki MonitraNG adımları (spike sonrası)

1. `sec_events` → observation stream (Alarm Faz 2 entegrasyon tasarımı)
2. Workflow `sec_events.created` Event Trigger (P4 tam)
3. `test-siem-faz1-e2e.ps1` + Odak CI

---

## Referanslar

- [SIEM_FAZ1_SPIKE.md](./SIEM_FAZ1_SPIKE.md)
- [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md)
- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- [workflow/DEVAM §P4](../workflow/DEVAM.md)
