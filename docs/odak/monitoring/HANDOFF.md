# SIEM / Monitoring — Oturum Handoff

**Son güncelleme:** 4 Haziran 2026 (mola checkpoint)  
**Ana DEVAM:** [DEVAM.md](./DEVAM.md)  
**Platform UI (ayrı chat):** [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md)

---

## 1. Tek cümlede durum (mola — 4 Haz 2026)

**Kendi SIEM yol haritası Faz 1–4 ✅ kapalı** · U1–U10 + bastion · WEF/NxLog/Linux rsyslog toplama şablonları · Odak quick regression PASS · Faz 5 (LogAlarm/5651) **ertelendi**

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
| B1 `bastion.generic.v1` | ✅ ingest smoke |
| B1 extended Windows (4722/4726) | ✅ fixture + unit |
| U8–U10 AD alarm + UX presets | ✅ E2E + dashboard |
| NxLog WEC şablon lab smoke | ✅ `test-nxlog-wec-template-e2e.ps1` |
| Linux rsyslog şablon + Engine classify | ✅ [SIEM_LINUX_RSYSLOG_FORWARDER.md](./SIEM_LINUX_RSYSLOG_FORWARDER.md) |
| SIEM CI yerel kapı | ✅ `run-siem-local-gate.ps1` · benchmark JSON verify |
| Quick regression (`-Quick`) | ✅ ~6 dk PASS |
| LogAlarm / 5651 (Faz 5) | ⬜ **ertelendi** — [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) |

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
U8: group_member_added (4728) → correlation alarm
U9: account_created (4720) → correlation alarm
U10: directory_object_modified (5136) → correlation alarm
B1 bastion: source.type=bastion → linux.auth benzeri sshd auth
```

**Regresyon:** `run-siem-quick-regression.ps1` · yerel: `run-siem-local-gate.ps1`

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
| **22** | ~~**bastion.generic.v1**~~ | ✅ source.type=bastion · ingest smoke script |
| **23** | ~~**U10 directory_object_modified**~~ | ✅ 5136 · E2E + UX preset |
| **24** | ~~**Extended Windows 4722/4726**~~ | ✅ fixture + unit |
| **25** | ~~**Quick regression wrapper**~~ | ✅ `run-siem-quick-regression.ps1` |
| **26** | ~~**NxLog prod doğrulama**~~ | ✅ lab smoke · `test-nxlog-wec-template-e2e.ps1` |
| **27** | ~~**Quick regression (-Quick) post-deploy**~~ | ✅ ~6 dk PASS |
| **28** | ~~**Linux rsyslog hardening (Faz 2.5)**~~ | ✅ şablon · Engine classify · lab smoke |
| **29** | ~~**Faz 4 UX kapanış**~~ | ✅ U1–U10 doc · dashboard/events UI |
| **30** | ~~**Benchmark baseline CI (Faz 3.5)**~~ | ✅ `run-siem-local-gate.ps1` |
| — | LogAlarm parite (genel) | [SIEM_LOGALARM_PARITY_ROADMAP.md](./SIEM_LOGALARM_PARITY_ROADMAP.md) — **Faz 5 ertelendi** |

---

## 5. Git

| Alan | Değer |
|------|--------|
| Branch | `main` (origin ile senkron) |
| **Mola checkpoint** | `62567c3` |
| Son commitler | `62567c3` Faz 3.5 CI · `6d53346` Linux rsyslog · `a809593` bastion/U10 · `b7ea29b` Cisco ASA |
| Odak deploy (4 Haz) | ✅ `mngreactor` + `mngui` + `mngengine` · son smoke PASS |
| Deploy gerekli mi? | **Hayır** — son commit yalnızca CI script + doküman |

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

**Upload / sync:** Aynı PowerShell oturumunda `. .\scripts\odak\OdakSshCommon.ps1; Initialize-OdakSshEnvironment` sonrası script çalıştırın (nested `pwsh -File` SCP DNS hatası verebilir).

**Hızlı doğrulama (mola öncesi):**

```powershell
pwsh scripts/ci/run-siem-local-gate.ps1
pwsh scripts/odak/run-siem-quick-regression.ps1 -SkipUnitGate   # Odak ~6 dk
pwsh scripts/odak/test-linux-rsyslog-auth-e2e.ps1
pwsh scripts/odak/test-nxlog-wec-template-e2e.ps1
```

**Not:** Yoğun benchmark/E2E sonrası P0 kapısı geçici düşebilir; kuyruk purge scriptleri `test-siem-e2e-suite.ps1 -Quick` içinde otomatik.

---

## 7. SIEM chat prompt'u (mola sonrası — kopyala-yapıştır)

```markdown
# MonitraNG — SIEM handoff (mola sonrası devam)

Yanıtlar **Türkçe**. Commit/push yalnızca açıkça istediğimde.

## Bağlam
- **HANDOFF:** docs/odak/monitoring/HANDOFF.md
- **DEVAM:** docs/odak/monitoring/DEVAM.md
- **Yol haritası:** docs/odak/monitoring/SIEM_ROADMAP.md
- **Toplama:** SIEM_WEF_WEC_FORWARDER.md · SIEM_LINUX_RSYSLOG_FORWARDER.md
- **Parser:** SIEM_PARSER_PLAN.md

## Mola checkpoint (4 Haz 2026)
- Git `main` @ **62567c3** (origin senkron)
- **Faz 1–4 ✅** · Faz 5 LogAlarm/5651 **ertelendi**
- Odak: mngreactor + mngui + mngengine deploy ✅ · quick regression PASS
- U1–U10 alarm E2E · bastion · NxLog · Linux rsyslog lab smoke ✅
- CI: `run-siem-local-gate.ps1` (unit + benchmark JSON)

## Odak
- Gateway http://192.168.20.20:5040 · Engine :5037 · syslog UDP :5514
- Engine recreate sonrası: `setup-mngengine-odak.ps1 -ApplyConfig -WaitHealthy`
- Sync/deploy: `. .\scripts\odak\OdakSshCommon.ps1; Initialize-OdakSshEnvironment` sonra script

## Kilitli kararlar
Hibrit toplama · Alarm engine korelasyon · Workflow onaylı müdahale · LogAlarm en sona · AI ⏸️

## Sıradaki adaylar (öncelik sen belirle)
1. **Müşteri prod ops** — NxLog / rsyslog şablon saha kurulumu
2. **Perf tuning** — P2 ~93→150 evt/s lab
3. **Gerçek firewall API** — block.ip mock → vendor API (SIEM_WORKFLOW_SEAM.md)
4. **Extended Windows** — 5137 vb. parser genişletme
5. **Faz 5** — LogAlarm/5651 (bilinçli ertelendi; kod yok)

## Bu oturumda ne yapmak istiyorum?
Önerdiğin sırayla devam et.
```

---

## 8. Mola sonrası ilk komutlar (opsiyonel)

```powershell
# Yerel
pwsh scripts/ci/run-siem-local-gate.ps1

# Odak sağlık (aynı oturumda SSH env yükle)
. .\scripts\odak\OdakSshCommon.ps1; Initialize-OdakSshEnvironment
Invoke-WebRequest http://192.168.20.20:5040/health -UseBasicParsing
pwsh scripts/odak/run-siem-quick-regression.ps1 -SkipUnitGate
```

---

## 9. Referanslar

- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- [SIEM_ROADMAP.md](./SIEM_ROADMAP.md)
- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md)
- [benchmarks/README.md](./benchmarks/README.md)
- [workflow/DEVAM.md](../workflow/DEVAM.md)
- [alarm/DEVAM.md](../alarm/DEVAM.md)
