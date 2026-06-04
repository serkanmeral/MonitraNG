# DEVAM — SIEM-Hafif Planlama (Kaldığımız Yer)

**Son güncelleme:** 4 Haziran 2026  
**Durum:** ✅ **SIEM MVP + post-MVP tamam** · Linux U1 tam zincir (alarm/workflow/block.ip) ✅

**Handoff (yeni chat):** [HANDOFF.md](./HANDOFF.md)

---

## 1. Tek cümlede durum

**SIEM MVP (U1–U7) + B1/B2/B3 + A4 ✅.** Linux auth U1 E2E tamamlandı.

---

## 3. MngReactor / MngEngine durumu (4 Haz 2026)

| Konu | Durum |
|------|--------|
| SIEM `sec_events` ingest | ✅ PR-1…PR-6 |
| Odak deploy Engine | ✅ `mngengine:latest` :5037, syslog :5514 |
| Odak deploy Reactor | ✅ `mngreactor:latest` |
| Workflow `mqtt/publish` | ✅ P4 E2E `reactor_mqtt` |
| **sec_events → observation** | ✅ `SecEventObservationMapper` + `PublishSecEventAsync` |
| **Alarm U1 correlation** | ✅ Odak E2E |
| **U1 → alarm.raised → Workflow** | ✅ `test-siem-u1-workflow-e2e.ps1` |
| **U1 → approval → block.ip** | ✅ `test-siem-u1-approval-block-e2e.ps1` |
| **U4 firewall deny spike** | ✅ `test-siem-u4-alarm-e2e.ps1` |
| **U2 fail→success sequence** | ✅ `test-siem-u2-alarm-e2e.ps1` |
| **P0 benchmark baseline** | ✅ `benchmark-P0-2026-06-04.json` |
| **P0 soak (5dk @ 50 evt/s)** | ✅ `benchmark-soak-2026-06-04.json` |
| **U4 → alarm.raised → Workflow** | ✅ `test-siem-u4-workflow-e2e.ps1` |
| **P1 benchmark (120s @ 100 evt/s)** | ✅ `benchmark-P1-2026-06-04.json` (~78 evt/s) |
| **Engine syslog UDP benchmark** | ✅ `benchmark-engine-syslog-2026-06-04.json` |
| **SIEM E2E suite (`-Quick`)** | ✅ `test-siem-e2e-suite.ps1` |
| **Engine queue_depth under load** | ✅ `benchmark-engine-queue-depth-2026-06-04.json` (max=107, gate=4000) |
| **WEF→WEC Engine batch ingest** | ✅ `test-engine-wec-ingest-e2e.ps1` · [SIEM_WEF_WEC_INGEST.md](./SIEM_WEF_WEC_INGEST.md) |
| **WEF forwarder şablonu (B2)** | ✅ [SIEM_WEF_WEC_FORWARDER.md](./SIEM_WEF_WEC_FORWARDER.md) |
| **Güvenlik olay arama UI** | ✅ `/apps/siem-center/events` · [SIEM_EVENTS_UI.md](./SIEM_EVENTS_UI.md) |
| **U6 firewall rule_change** | ✅ `test-siem-u6-alarm-e2e.ps1` |
| **U5 firewall traffic spike** | ✅ `test-siem-u5-alarm-e2e.ps1` |
| **U3 privileged outside maintenance window** | ✅ `test-siem-u3-alarm-e2e.ps1` |
| **U7 new src→dst (baseline / new_flow)** | ✅ `test-siem-u7-alarm-e2e.ps1` |
| **SIEM dashboard MVP** | ✅ `/apps/siem-center` · [SIEM_DASHBOARD.md](./SIEM_DASHBOARD.md) |
| **U3 bakım penceresi (appsettings)** | ✅ `SecEventMaintenanceWindow:*` |
| **B1 `linux.auth.v1`** | ✅ U1 alarm + workflow E2E |
| **B1 `firewall.vendor.v1` (FortiGate + PAN-OS + Cisco ASA)** | ✅ `test-siem-firewall-vendor-ingest.ps1` |
| **SIEM unit CI gate** | ✅ `scripts/ci/test-siem-unit-gate.ps1` · `ci.yml` |
| **P2 soak benchmark** | ✅ ~93 evt/s · `benchmark-P2-2026-06-04.json` |
| **B1 `windows.security.extended.v1` (4720/4728/5136)** | ✅ `test-siem-windows-extended-ingest.ps1` |
| **U8 AD group_member_added alarm** | ✅ `test-siem-u8-alarm-e2e.ps1` |
| **U9 AD account_created alarm** | ✅ `test-siem-u9-alarm-e2e.ps1` |
| **B1 `bastion.generic.v1`** | ✅ `test-siem-bastion-ingest.ps1` |
| **U10 AD directory_object_modified alarm** | ✅ `test-siem-u10-alarm-e2e.ps1` |
| **Quick regression wrapper** | ✅ `run-siem-quick-regression.ps1` |
| **Linux rsyslog auth (Faz 2.5)** | ✅ `test-linux-rsyslog-auth-e2e.ps1` |
| **B3 hazır kural paketi (`siem-mvp-v1`)** | ✅ `seed-siem-alarm-rule-pack.ps1` |
| **Aktif yol haritası** | [SIEM_ROADMAP.md](./SIEM_ROADMAP.md) · LogAlarm ertelendi |

---

## 6. Sıradaki adımlar

1. ~~SIEM PR-1…PR-6~~ ✅
2. ~~Engine Odak + syslog S3~~ ✅
3. ~~Reactor mqtt/publish + P4 E2E~~ ✅
4. ~~Faz 2: sec_events observation map + U1 alarm~~ ✅
5. ~~U1 → `alarm.raised` → Workflow~~ ✅
6. ~~U1 → approval → `block.ip`~~ ✅
7. ~~U4 firewall deny spike~~ ✅
8. ~~U2 sequence~~ ✅
9. ~~Benchmark baseline (P0)~~ ✅
10. ~~P0 5 dk soak (50 evt/s kapı)~~ ✅
11. ~~U4 → workflow~~ ✅
12. ~~P1 profil benchmark · Engine syslog UDP benchmark · SIEM E2E suite~~ ✅

### SIEM-hafif sonrası (ayrı hedefler)

MVP tamamlandı. **LogAlarm / tam SIEM paritesi** (5651, WORM, sertifikasyon) **en sona ertelendi** — önce [SIEM_ROADMAP.md](./SIEM_ROADMAP.md).

- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) — referans kıyaslama (kod yok)
- [SIEM_WORM_5651_SPIKE.md](./SIEM_WORM_5651_SPIKE.md) — planlama notu (implementasyon yok)

Kısa vadeli teknik devam (MVP sonrası bakım):

1. ~~`sec_event.queue_depth` under load ölçümü~~ ✅
2. ~~`HANDOFF.md` güncelleme (SIEM MVP durumu)~~ ✅
3. ~~WEF→WEC ingest (Engine `wec-batch` + S5 E2E)~~ ✅
4. ~~WEF→WEC Engine batch ingest~~ ✅
5. ~~Güvenlik olay arama UI (MVP)~~ ✅ — [SIEM_EVENTS_UI.md](./SIEM_EVENTS_UI.md)
6. ~~Side menü: Güvenlik Merkezi header~~ ✅ — `patch-siem-center-side-menu.ps1`

### Post-MVP (önerilen)

1. ~~UI Faz 2 — tam `raw` metin (detay paneli)~~ ✅
2. ~~U6 firewall `rule_change` senaryosu~~ ✅ — parser + `test-siem-u6-alarm-e2e.ps1`
3. ~~U3 / U5 korelasyon senaryoları~~ ✅ — `test-siem-u3-alarm-e2e.ps1` · `test-siem-u5-alarm-e2e.ps1`
4. ~~Tam E2E suite regression~~ ✅
5. ~~Yerel commit + push~~ ✅ — `a685688`
6. ~~**U7 baseline → `new_flow`**~~ ✅ — çift observation · `test-siem-u7-alarm-e2e.ps1`
7. ~~**Tam E2E suite (-Quick)**~~ ✅
8. ~~**SIEM dashboard MVP**~~ ✅ — [SIEM_DASHBOARD.md](./SIEM_DASHBOARD.md)
9. ~~**U3 bakım penceresi yapılandırılabilir**~~ ✅ — `SecEventMaintenanceWindow` appsettings
10. ~~**A3** — olay arama URL sync + U1–U7 presets~~ ✅
11. ~~**Dashboard P2** — 24s olay dağılımı~~ ✅
12. ~~**E2E suite S4.1/S5** argüman düzeltmesi~~ ✅
13. ~~**B1 `linux.auth.v1`**~~ ✅ — sshd/sudo · `test-siem-linux-auth-ingest.ps1`
14. ~~**B2 WEF forwarder şablonu**~~ ✅ — [SIEM_WEF_WEC_FORWARDER.md](./SIEM_WEF_WEC_FORWARDER.md) · Engine `MaxWecBatchItems`/retry
15. ~~**Odak sync SFTP fallback**~~ ✅ — `Send-OdakRemoteFile` · `2091029`

### Mola sonrası (önerilen)

| # | İş | Not |
|---|-----|-----|
| 16 | ~~**`firewall.vendor.v1` parser**~~ | ✅ FortiGate pilot · `test-siem-firewall-vendor-ingest.ps1` |
| 17 | ~~**B3 hazır kural paketi**~~ | ✅ MITRE/ISO · `siem-mvp-v1` |
| 18 | ~~**A4 özelleştirilebilir dashboard**~~ | ✅ localStorage widget düzeni |
| 19 | ~~**Linux auth → U1 alarm E2E**~~ | ✅ `test-siem-linux-auth-u1-alarm-e2e.ps1` |
| 20 | ~~**Linux auth → U1 workflow E2E**~~ | ✅ `test-siem-linux-auth-u1-workflow-e2e.ps1` |
| 21 | ~~**Backend perf check + hub/MQ fix**~~ | ✅ `cb95426` · `diagnostic-mq-backlog.ps1` |
| 22 | ~~**Linux auth → U1 approval → block.ip**~~ | ✅ `test-siem-linux-auth-u1-approval-block-e2e.ps1` |
| 23 | ~~**B1 Palo Alto PAN-OS**~~ | ✅ CEF/CSV · `panw_*` fixtures |
| 24 | ~~**Dashboard P2**~~ | ✅ saatlik timeline + U1–U7 senaryo kartları |
| 25 | ~~**workflow.deadletter triage**~~ | ✅ `diagnostic-workflow-deadletter.ps1` (eski E2E artefakt) |
| 26 | ~~**E2E suite B1 ingest**~~ | ✅ firewall vendor + windows extended |
| 27 | ~~**B1 `windows.security.extended.v1`**~~ | ✅ 4720/4728/5136 · unit + ingest smoke |
| 28 | ~~**Odak mngreactor deploy**~~ | ✅ extended parser smoke PASS |
| 29 | ~~**DLQ purge**~~ | ✅ 7 eski mesaj · `-IncludeDeadletter` |
| 30 | ~~**Faz C spike (5651/WORM)**~~ | ✅ [SIEM_WORM_5651_SPIKE.md](./SIEM_WORM_5651_SPIKE.md) |
| 31 | ~~**U8 AD group_member_added alarm**~~ | ✅ `test-siem-u8-alarm-e2e.ps1` |

### Aktif yol haritası (LogAlarm hariç)

→ [SIEM_ROADMAP.md](./SIEM_ROADMAP.md)

| # | İş | Not |
|---|-----|-----|
| 32 | ~~**U9 `account_created` alarm**~~ | ✅ `test-siem-u9-alarm-e2e.ps1` |
| 33 | ~~**Dashboard/arama U8–U9**~~ | ✅ senaryo kataloğu + presets |
| 34 | ~~**WEF forwarder extended fixture**~~ | ✅ `Forward-WecEventsToEngine.ps1` |
| 35 | ~~**SIEM unit CI gate**~~ | ✅ `ci.yml` · `test-siem-unit-gate.ps1` |
| 36 | ~~**Cisco ASA FW vendor**~~ | ✅ parser + ingest smoke |
| 37 | ~~**P2 soak benchmark**~~ | ✅ ~93 evt/s · `benchmark-P2-2026-06-04.json` |
| 38 | ~~**mngui deploy (U8/U9 UX)**~~ | ✅ Odak |
| 39 | ~~**bastion.generic.v1 parser**~~ | ✅ sshd auth · ingest smoke |
| 40 | ~~**U10 directory_object_modified alarm**~~ | ✅ + UX preset |
| 41 | ~~**Extended Windows 4722/4726**~~ | ✅ fixture + unit |
| 42 | ~~**Quick regression wrapper**~~ | ✅ `run-siem-quick-regression.ps1` |
| 43 | ~~**NxLog prod şablonu doğrulama**~~ | ✅ lab smoke · müşteri ops checklist §6.1 |
| 44 | ~~**Linux rsyslog hardening (Faz 2.5)**~~ | ✅ şablon + Engine classify + lab smoke |

LogAlarm / 5651 / WORM → **Faz 5 (ertelendi)** — [SIEM_ROADMAP.md §6](./SIEM_ROADMAP.md#6-faz-5--ertelenen-logalarm--uyum)

**Operasyon notu:** Benchmark veya yoğun ingest sonrası E2E/workflow testleri önce kuyruk temizliği gerektirebilir:
- `purge-workflow-queues.ps1 -Apply` — `workflow.execution`, `workflow.event.inbound`, `alarm.observation.inbound` (birleşik)
- `purge-alarm-observation-queue.ps1` — yalnızca observation (worker restart ile)

`test-siem-e2e-suite.ps1` alarm + workflow purge adımlarını otomatik çalıştırır.

E2E geçici alarm kuralları birikimini temizlemek: `purge-siem-e2e-alarm-rules.ps1 -Apply` (`siem-mvp-v1` paket kuralları korunur).

---

## 7. İlgili DEVAM dosyaları

- [workflow/DEVAM.md](../workflow/DEVAM.md) — P4 engine.command
- [alarm/DEVAM.md](../alarm/DEVAM.md) — correlation + observation consumer
- [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) — ✅ implementasyon
