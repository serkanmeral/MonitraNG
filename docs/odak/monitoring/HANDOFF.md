# SIEM / Monitoring — Oturum Handoff

**Son güncelleme:** 4 Haziran 2026  
**Ana DEVAM:** [DEVAM.md](./DEVAM.md)  
**Platform UI (ayrı chat):** [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md)

---

## 1. Tek cümlede durum (4 Haz 2026)

**SIEM-hafif MVP + post-MVP ✅** · Linux auth U1 tam zincir (alarm → workflow → block.ip) ✅

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
| WEF forwarder şablonu (B2) | ✅ [SIEM_WEF_WEC_FORWARDER.md](./SIEM_WEF_WEC_FORWARDER.md) · Engine batch/retry · Odak deploy |
| B1 `linux.auth.v1` | ✅ U1 alarm + workflow + block.ip E2E |
| B3 hazır kural paketi | ✅ `siem-mvp-v1` · MITRE/ISO · `seed-siem-alarm-rule-pack.ps1` |
| B1 `firewall.vendor.v1` | ✅ FortiGate pilot · `test-siem-firewall-vendor-ingest.ps1` |
| Odak sync upload | ✅ `Send-OdakRemoteFile` (SCP → SFTP fallback) · `2091029` |
| Güvenlik olay arama UI | ✅ `/apps/siem-center/events` · menü: **Güvenlik Merkezi** |
| LogAlarm feature-parite | ⬜ Ayrı hedef — [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) |

---

## 3. Kanıtlanmış zincirler (Odak)

```
U1: sec_events → observation → correlation → alarm.raised → Workflow → (approval) → block.ip
U1 (Linux): linux.auth.v1 sshd → login_failed → alarm.raised → Workflow → approval → block.ip
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
- `purge-workflow-queues.ps1 -Apply` (birleşik workflow + observation)
- `purge-alarm-observation-queue.ps1` (observation + worker restart)

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
| 10 | ~~**A3 olay arama UX**~~ ✅ | URL sync · U1–U7 presets · new_flow badge |
| 11 | ~~**E2E S4.1/S5 switch fix**~~ ✅ | suite `-VerifyOdakMongo` doğru geçiriliyor |
| 12 | ~~**B1 `linux.auth.v1`**~~ ✅ | sshd/sudo parser + ingest smoke |
| 13 | ~~**B2 WEF forwarder şablonu**~~ ✅ | GPO/WEC ops · NxLog · PS forwarder · Engine batch/retry |
| 14 | ~~**Odak sync SFTP fallback**~~ ✅ | `Send-OdakRemoteFile` · `2091029` |
| **15** | ~~**B1 devamı — `firewall.vendor.v1`**~~ | ✅ FortiGate pilot · U4/U6 alanları · [SIEM_PARSER_PLAN.md](./SIEM_PARSER_PLAN.md) |
| **16** | ~~**B3 — hazır kural paketi**~~ | ✅ `siem-mvp-v1` · [SIEM_ALARM_RULE_PACK.md](./SIEM_ALARM_RULE_PACK.md) |
| **18** | ~~**A4 — özelleştirilebilir dashboard**~~ | ✅ Widget düzeni · localStorage |
| **19** | ~~**Linux auth → U1 alarm/workflow/block.ip**~~ | ✅ tam zincir |
| **20** | ~~**Perf: hub health + MQ tuning**~~ | ✅ `cb95426` |
| **21** | ~~**alarm.updated publish throttle**~~ | ✅ `UpdatedPublishMinIntervalSeconds` |
| — | LogAlarm parite (genel) | [SIEM_LOGALARM_PARITY_ROADMAP.md](./SIEM_LOGALARM_PARITY_ROADMAP.md) |

---

## 5. Git

| Alan | Değer |
|------|--------|
| Branch | `main` (origin ile senkron) |
| Son SIEM commit | (bu commit) — Linux block.ip E2E · alarm.updated throttle · suite purge |
| Önceki | `cb95426` — hub health + MQ worker tuning |

---

## 6. Odak operasyon (hatırlatma)

| Konu | Değer |
|------|--------|
| Gateway | `http://192.168.20.20:5040` |
| Engine | `http://192.168.20.20:5037` · syslog UDP `:5514` |
| Domain / kullanıcı | `odak` · `odak_admin` / `Admin123!` |

**Sync + deploy (Engine örneği):**

```powershell
pwsh scripts/odak/sync-odak-source.ps1 -Paths @('MngEngine','ApplicationResources/mng_apps')
pwsh scripts/odak/deploy-odak-apps.ps1 -Services mngengine
# Container recreate sonrası Reactor config gerekebilir:
pwsh scripts/odak/setup-mngengine-odak.ps1 -ApplyConfig -WaitHealthy
```

**Upload:** `sync-odak-source.ps1` → `Send-OdakRemoteFile` (SCP başarısız olursa SFTP).

**Hızlı doğrulama:**

```powershell
pwsh scripts/odak/test-siem-e2e-suite.ps1 -Quick
pwsh scripts/odak/test-engine-wec-ingest-e2e.ps1 -EngineUrl http://192.168.20.20:5037
pwsh scripts/wef/Forward-WecEventsToEngine.ps1 -EngineUrl http://192.168.20.20:5037 -Source Fixture
```

**Not:** Yoğun benchmark/E2E sonrası P0 kapısı geçici düşebilir; kuyruk purge scriptleri `test-siem-e2e-suite.ps1 -Quick` içinde otomatik.

---

## 7. SIEM chat prompt'u (yeni oturum — kopyala-yapıştır)

```markdown
# MonitraNG — SIEM handoff (mola sonrası devam)

Yanıtlar **Türkçe**. Commit/push yalnızca açıkça istediğimde.

## Bağlam
- **HANDOFF:** docs/odak/monitoring/HANDOFF.md
- **DEVAM:** docs/odak/monitoring/DEVAM.md
- **Parite yol haritası:** docs/odak/monitoring/SIEM_LOGALARM_PARITY_ROADMAP.md
- **Parser planı:** docs/odak/monitoring/SIEM_PARSER_PLAN.md
- **WEF/B2:** docs/odak/monitoring/SIEM_WEF_WEC_FORWARDER.md

## Mevcut durum (4 Haz 2026)
- SIEM-hafif MVP + post-MVP ✅: U1–U7 korelasyon, workflow müdahale, dashboard, A3 events UX
- B1 ✅ `linux.auth.v1` (sshd/sudo) · B2 ✅ WEF forwarder şablonu + Engine wec-batch batch/retry
- Odak E2E: `test-siem-e2e-suite.ps1 -Quick` PASS
- Git main @ `2091029` (sync SFTP fallback)

## Odak
- Gateway http://192.168.20.20:5040 · Engine :5037 · syslog :5514
- Engine recreate sonrası: `setup-mngengine-odak.ps1 -ApplyConfig`

## Kilitli kararlar
Hibrit toplama (syslog + WEF→WEC + agent) · Alarm engine tespit · Workflow onaylı müdahale · AI ⏸️

## Önerilen sıradaki iş (sen seç veya onayla)
1. **B1 devamı:** `firewall.vendor.v1` parser (pilot FW markası)
2. **B3:** Hazır kural paketi (MITRE/ISO)
3. **A4:** Özelleştirilebilir SIEM dashboard widget düzeni
4. Linux auth → U1 alarm E2E (linux sshd brute-force zinciri)

## Bu oturumda ne yapmak istiyorum?
Önerdiğin sırayla devam et: B1 `firewall.vendor.v1` ile başla (commit/push deploy sonunda).
```

---

## 8. Referanslar

- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md)
- [benchmarks/README.md](./benchmarks/README.md)
- [workflow/DEVAM.md](../workflow/DEVAM.md)
- [alarm/DEVAM.md](../alarm/DEVAM.md)
