# SIEM / Monitoring — Oturum Handoff

**Son güncelleme:** 4 Haziran 2026  
**Ana DEVAM:** [DEVAM.md](./DEVAM.md)  
**Platform UI (ayrı chat):** [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md)

---

## 1. Tek cümlede durum (4 Haz 2026)

**SIEM-hafif MVP tamam ✅** — `sec_events` ingest, U1/U2/U4 korelasyon, onaylı workflow müdahale, P0/P1 benchmark ve E2E suite Odak'ta doğrulandı. LogAlarm paritesi **ayrı uzun vadeli hedef** — bkz. [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md).

---

## 2. Platform durumu (güncel)

| Konu | Durum |
|------|--------|
| MngReactor Odak | ✅ `mngreactor:latest` |
| MngEngine syslog | ✅ UDP :5514, `SecEvents/queue` + flush |
| SIEM `sec_events` ingest | ✅ PR-1…PR-6 |
| sec_events → observation → alarm | ✅ U1/U2/U4 E2E |
| alarm.raised → Workflow | ✅ U1/U4 workflow E2E |
| U1 approval → block.ip | ✅ `reactor_mqtt` |
| P0 soak (~41 evt/s) | ✅ |
| P1 benchmark (~78 evt/s) | ✅ |
| SIEM E2E suite (`-Quick`) | ✅ |
| Engine queue_depth under load | ✅ max=107 / gate=4000 |
| WEF→WEC Engine batch | ✅ `POST /api/SecEvents/wec-batch` · S5 E2E |
| Güvenlik olay arama UI | ✅ `/apps/siem-center/events` · menü: **Güvenlik Merkezi** |
| LogAlarm feature-parite | ⬜ Ayrı hedef — [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) |

---

## 3. Kanıtlanmış zincirler (Odak)

```
U1: sec_events → observation → correlation → alarm.raised → Workflow → (approval) → block.ip
U4: firewall syslog → sec_events → observation → correlation → alarm.raised → Workflow
U2: login_failed×N → login_success → sequence alarm
U5: allowed_flow×N (dstIp/dstPort) → traffic spike alarm
U6: rule_change → correlation alarm
U3: privileged_login_outside_window (LogonType 10, bakım dışı) → correlation alarm
U7: baseline sonrası yeni src→dst → new_flow → correlation alarm
```

**Scriptler:** `scripts/odak/test-siem-e2e-suite.ps1 -Quick` (kuyruk purge dahil)

**Benchmark:** `scripts/odak/benchmark-siem-p0-baseline.ps1` · `-Soak` · `-P1` · `benchmark-siem-engine-syslog.ps1`

**Operasyon:** Benchmark/E2E öncesi gerekirse:
- `purge-alarm-observation-queue.ps1`
- `purge-workflow-event-inbound-queue.ps1`
- `purge-workflow-execution-queue.ps1`

---

## 4. Sıradaki adımlar (SIEM chat)

### Kısa vadeli (MVP sonrası bakım)

1. ~~P1 · syslog · E2E suite~~ ✅
2. ~~`sec_event.queue_depth` under load~~ ✅
3. ~~WEF→WEC Engine batch ingest~~ ✅ — [SIEM_WEF_WEC_INGEST.md](./SIEM_WEF_WEC_INGEST.md)
4. ~~Güvenlik olay arama UI (MVP)~~ ✅ — [SIEM_EVENTS_UI.md](./SIEM_EVENTS_UI.md)

### Post-MVP (önerilen sıra)

| # | İş | Not |
|---|-----|-----|
| 1 | ~~**UI Faz 2** — olay detayında tam `raw` metni~~ ✅ | Ingest `raw` (8 KB) + GET by id + drawer |
| 2 | ~~**U6** — firewall kural/config değişikliği~~ ✅ | Parser `rule_change` + E2E |
| 3 | ~~**U3 / U5** — bakım penceresi dışı erişim · trafik sıçraması~~ ✅ | U3 parser + U5 `allowed_flow` spike · E2E |
| 4 | ~~**Tam E2E regression**~~ ✅ | `test-siem-e2e-suite.ps1` |
| 5 | ~~**Yerel commit**~~ ✅ | `a685688` pushed |
| 6 | ~~**U7** — yeni/bilinmeyen src→dst (baseline)~~ ✅ | `sec_flow_baseline` + çift observation · E2E |
| 7 | ~~**Tam E2E regression (-Quick)**~~ ✅ | U1–U7 + workflow suite |
| 8 | ~~**SIEM dashboard MVP**~~ ✅ | `/apps/siem-center` |
| 9 | ~~**U3 bakım penceresi yapılandırılabilir**~~ ✅ | `SecEventMaintenanceWindow` appsettings |
| — | LogAlarm parite | [SIEM_LOGALARM_PARITY_ROADMAP.md](./SIEM_LOGALARM_PARITY_ROADMAP.md) |

---

## 5. Git

| Alan | Değer |
|------|--------|
| Branch | `main` |
| Son SIEM commit | `a685688` — sec-events UI, WEC ingest, U6, raw detail |

---

## 6. SIEM chat prompt'u

```markdown
# MonitraNG — SIEM handoff (MVP sonrası)

Yanıtlar **Türkçe**. Commit/push yalnızca açıkça istediğimde.

## Bağlam
- **DEVAM:** docs/odak/monitoring/DEVAM.md
- **LogAlarm kıyaslama:** docs/odak/monitoring/SIEM_LOGALARM_COMPARISON.md
- **Ana plan:** docs/odak/monitoring/SIEM_PLANNING.md

## Mevcut durum
- SIEM-hafif MVP ✅ (U1/U2/U4 + workflow müdahale)
- Odak E2E suite PASS
- LogAlarm parite ayrı hedef

## Kilitli kararlar
Hibrit toplama · Alarm engine tespit · Workflow onaylı müdahale · AI ⏸️

## Bu oturumda ne yapmak istiyorum?
[Kendi cümleni buraya yaz]
```

---

## 7. Referanslar

- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md)
- [benchmarks/README.md](./benchmarks/README.md)
- [workflow/DEVAM.md](../workflow/DEVAM.md)
- [alarm/DEVAM.md](../alarm/DEVAM.md)
