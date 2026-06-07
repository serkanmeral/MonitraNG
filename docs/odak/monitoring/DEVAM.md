# DEVAM — SIEM-Hafif + Alarm Merkezi (Kaldığımız Yer)

**Son güncelleme:** 6 Haziran 2026 (oturum sonu — **mola**, diğer modüllere geçiş)  
**Durum:** ✅ **SIEM Faz 1–4 + Alarm Merkezi operatör UI** · Odak deploy ✅ · Git `main` @ **`969b57b`**

**Handoff (yeni chat):** aşağıdaki [§ Mola checkpoint](#mola-checkpoint-6-haz-2026--siem--alarm-merkezi-ui-kapandi) · [HANDOFF.md](./HANDOFF.md)

> **⭐ KALDIĞIMIZ YER:** Temel SIEM-hafif MVP **yeterli** kabul edildi; kullanıcı **diğer modüllere** (OC, workflow vb.) geçiyor. Bu chat’ten devam: aşağıdaki checkpoint + “Yeni chat promptu”. Alarm motor detayı: [../alarm/DEVAM.md](../alarm/DEVAM.md)

---

## 1. Tek cümlede durum

**SIEM ingest → sec_events → kural (threshold/correlation/scheduled/sequence) → alarm → operatör UI** hattı Odak’ta çalışır durumda. **Güvenlik Yönetimi** menüsü altında **Alarm Merkezi** (alarmlar + kurallar) ve **SIEM Güvenlik Paneli** (dashboard + olay arama) canlı.

---

## Mola checkpoint (6 Haz 2026 — SIEM + Alarm Merkezi UI kapandı)

### Ne tamamlandı (bu oturum serisi)

| Alan | Durum | Not |
|------|--------|-----|
| Açık alarmlar UI | ✅ | Server pagination, filtreler, auto-refresh, detay paneli |
| Alarm lifecycle API + UI | ✅ | acknowledge / suppress / resolve · context timeline |
| Alarm geçmişi | ✅ | `from`/`to`, durum filtresi “Hepsi” |
| SIEM olay arama | ✅ | Pagination, tarih aralığı, auto-refresh, detay |
| Menü | ✅ | **Güvenlik Yönetimi** → Alarm Merkezi · SIEM Güvenlik Paneli |
| Kurallar UI | ✅ | Alarmlar \| Kurallar sekmeleri; kurallar menüden ayrıldı |
| Sequence kural formu | ✅ | U2 preset · create `sequenceSteps` · düzenlemede adımlar salt okunur |
| Smoke | ✅ | `test-siem-alarm-ui-smoke.ps1` · `test-operator-smoke.ps1` |
| Odak deploy | ✅ | `mngui` (+ önceki oturumda `mngalarm`, `mngreactor`) |

### Git

| Commit | Konu |
|--------|------|
| `c68669c` | Alarm Merkezi UI, lifecycle API, Güvenlik Yönetimi menüsü |
| `969b57b` | Sequence kural formu, `test-siem-alarm-ui-smoke.ps1` |

Branch: `main` · push edildi.

### Odak (doğrulama)

| Konu | Değer |
|------|--------|
| Gateway | http://192.168.20.20:5040 |
| UI | http://192.168.20.20:3000 |
| Domain / kullanıcı | `odak` · `odak_admin` / `Admin123!` |
| Alarm Merkezi | `/apps/alarm-center/alarms` · `/apps/alarm-center/rules` |
| SIEM paneli | `/apps/siem-center` · `/apps/siem-center/events` |
| Menü patch | `docs/odak/monitoring/scripts/patch-siem-center-side-menu.ps1` |

Deploy sonrası smoke:

```powershell
.\scripts\odak\test-siem-alarm-ui-smoke.ps1
.\scripts\odak\test-siem-u2-alarm-e2e.ps1      # sequence alarm
.\scripts\odak\test-operator-smoke.ps1
.\scripts\odak\run-siem-quick-regression.ps1   # geniş regresyon (~6 dk)
```

### Önemli dosyalar (UI)

| Alan | Dosyalar |
|------|----------|
| Alarmlar | `Mng.Ui/components/apps/alarm-center/AcAlarmsExplorer.vue`, `AcAlarmDetailPanel.vue`, `useAlarmList.ts` |
| Kurallar | `AcAlarmRulesExplorer.vue`, `AcAlarmRuleFormDialog.vue`, `useAlarmRuleList.ts` |
| SIEM | `AcSecEventsExplorer.vue`, `AcSiemCenterDashboard.vue` |
| Menü / breadcrumb | `sidebarItem.ts`, `useSecurityManagementBreadcrumbs.ts` |
| Alarm API | `MngAlarm/.../AlarmControllers.cs`, `AlarmLifecycleService.cs` |

### Bilinen sınırlar (ertelenmiş — devam ederken)

| Konu | Not |
|------|-----|
| Sequence adım **düzenleme** | Backend `UpdateAlarmRuleRequest` adım içermiyor; UI salt okunur |
| Eski alarmlar lifecycle timeline | Backfill yok; yeni lifecycle kayıtları tam |
| Alarm → **OC ticket** | Workflow Faz 6; ayrı oturum |
| Hub / push bildirim | Yok |
| 5651 / WORM / LogAlarm paritesi | Faz 5 — en sonda |
| Endpoint ölçek (çok PC) | Pilot; NxLog şablonları hazır |

### Sıradaki adımlar (SIEM chat’e dönünce — önerilen sıra)

1. **Alarm → OC iş kaydı** — `alarm.raised` → Workflow `CreateWorkItem` (en yüksek operasyon değeri)
2. **Operatör olgunlaştırma** — hub bildirim, alarm detaydan runbook/deep link, lifecycle backfill
3. **Sequence update API** — backend + formda adım düzenleme
4. **SIEM ops** — FortiGate `source.host`, Sysmon whitelist (HANDOFF’taki opsiyonel maddeler)
5. **Faz 5** — 5651/WORM (regülasyon ihtiyacı doğunca)

### Yeni chat promptu (kopyala-yapıştır)

```
docs/odak/monitoring/DEVAM.md ⭐ mola checkpoint (6 Haz) oku.
SIEM-hafif MVP yeterli kabul edildi; Alarm Merkezi + sequence form deploy edildi (@969b57b).
Odak: gateway :5040, UI :3000, odak_admin.
Devam hedefi: [buraya yaz — örn. "alarm → OC ticket" veya "hub bildirim"]
Commit ancak istersem.
```

### Konumlandırma (müşteri / IT)

MonitraNG **SIEM-hafif**: hedefli senaryolar (U1–U10), olay arama, kural/alarm operasyonu, onaylı workflow aksiyonları. **Tam SIEM / 5651 / binlerce endpoint** değil — bkz. [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md).

---

## 3. MngReactor / MngEngine durumu (4 Haz 2026)

| Konu | Durum |
|------|--------|
| SIEM `sec_events` ingest | ✅ PR-1…PR-6 |
| Odak deploy Engine | ✅ `mngengine:latest` :5037 · çoklu UDP `:5514` `:1514/1541/1542` `:541/542` |
| Odak deploy Reactor | ✅ `mngreactor:latest` |
| Workflow `mqtt/publish` | ✅ P4 E2E `reactor_mqtt` |
| **sec_events → observation** | ✅ `SecEventObservationMapper` + `PublishSecEventAsync` |
| **Alarm U1 correlation** | ✅ Odak E2E |
| **U1 → alarm.raised → Workflow** | ✅ `test-siem-u1-workflow-e2e.ps1` |
| **U1 → approval → block.ip** | ✅ `test-siem-u1-approval-block-e2e.ps1` · NxLog: `test-siem-nxlog-json-u1-approval-block-e2e.ps1` |
| **U4 firewall deny spike** | ✅ `test-siem-u4-alarm-e2e.ps1` |
| **U2 fail→success sequence** | ✅ `test-siem-u2-alarm-e2e.ps1` |
| **P0 benchmark baseline** | ✅ `benchmark-P0-2026-06-04.json` |
| **P0 soak (5dk @ 50 evt/s)** | ✅ `benchmark-soak-2026-06-04.json` |
| **U4 → alarm.raised → Workflow** | ✅ `test-siem-u4-workflow-e2e.ps1` · FortiGate: `test-siem-fortigate-u4-workflow-e2e.ps1` |
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
| **Linux rsyslog auth (Faz 2.5)** | ✅ `test-linux-rsyslog-auth-e2e.ps1` · imjournal sıkı filtre |
| **sec_events veri yönetimi (§9 MVP)** | ✅ `SecEventsSettings` · unknown drop · hot TTL 60g · rawPreview-only |
| **Linux iki-host pilot (U1×2)** | ✅ `run-siem-linux-two-host-pilot.ps1` · monitrang + monitrang-prod |
| **Lab reset script** | ✅ `reset-siem-lab-data.ps1 -Apply` |
| **B3 hazır kural paketi (`siem-mvp-v1`)** | ✅ `seed-siem-alarm-rule-pack.ps1` |
| **B1 `windows.nxlog-json.v1` (IT UDP)** | ✅ Security kanalı · `test-nxlog-json-syslog-ingest.ps1` · U1 alarm/workflow/block E2E |
| **FortiGate UDP ingest (IT :541)** | ✅ `test-siem-fortigate-syslog-udp-ingest.ps1` · U4 alarm E2E |
| **IT geçici relay** | ✅ `rsyslog-it-relay-to-engine.conf` · multi-port sonrası kapatıldı |

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
| 45 | ~~**Faz 4 UX kapanış (U1–U10 doc)**~~ | ✅ events UI + dashboard doc |
| 46 | ~~**Benchmark baseline CI gate (Faz 3.5)**~~ | ✅ `verify-siem-benchmark-baselines.ps1` |

LogAlarm / 5651 / WORM → **Faz 5 (ertelendi)** — [SIEM_ROADMAP.md §6](./SIEM_ROADMAP.md#6-faz-5--ertelenen-logalarm--uyum)

### Mola checkpoint (5 Haz 2026)

| Alan | Değer |
|------|--------|
| Git | `main` @ mola commit (bkz. HANDOFF §5) |
| Odak | mngreactor + mngui + mngengine — **SecEvents deploy gerekli** |
| Lab veri | 2× U1 alarm · 20 login_failed · pilot kullanıcıları (yukarıda) |
| Regresyon | `run-siem-quick-regression.ps1` (deploy sonrası) |
| Handoff | [HANDOFF.md §8](./HANDOFF.md#8-siem-chat-promptu-yeni-chat--kopyala-yapıştır) |

### Mola checkpoint (4 Haz 2026)

| Alan | Değer |
|------|--------|
| Git | `main` @ `62567c3` |
| Odak | mngreactor + mngui + mngengine ✅ |
| Regresyon | `run-siem-quick-regression.ps1` PASS |
| Yerel CI | `run-siem-local-gate.ps1` PASS |
| Handoff | [HANDOFF.md §7](./HANDOFF.md#7-siem-chat-promptu-mola-sonrası--kopyala-yapıştır) |

### Planlama oturumu (5 Haz 2026)

| Konu | Durum |
|------|--------|
| Ürün/SIEM/Alarm/Workflow anlatımı | ✅ HANDOFF §7 |
| Syslog: Engine = listener (push) | ✅ SIEM_PLANNING §5.1 |
| UI: Güvenlik Merkezi + Alarm Merkezi | ✅ SIEM_DASHBOARD · SIEM_EVENTS_UI · `/apps/alarm-center/rules` |
| U1–U10 = iç senaryo kodu (standart değil) | ✅ |
| Odak E2E alarm purge (137 kural) | ✅ `purge-siem-e2e-alarm-rules.ps1 -Apply` |
| Odak P4 workflow test purge (3 kural) | ✅ |
| Odak `siem-mvp-v1` seed U1–U7 | ✅ `seed-siem-alarm-rule-pack.ps1 -Replace` |
| Purge/seed script token düzeltmesi | ✅ commitlendi |
| Kalan benchmark artığı | ⬜ `Bench lag bench-P0-*` · `U1 probe` (isteğe bağlı sil) |

### Veri yönetimi + Linux pilot (5 Haz 2026 — mola)

| Konu | Durum |
|------|--------|
| **Unknown ingest drop** | ✅ `SecEvents:DropUnknownEvents=true` · yanıt `skipped` |
| **Hot TTL** | ✅ Mongo `idx_timestamp_ttl` · `@timestamp` · 60 gün |
| **rawPreview disiplini** | ✅ `PersistFullRaw=false` · BSON'da `raw` yok |
| **UI unknown filtresi** | ✅ `excludeUnknown=true` varsayılan · checkbox |
| **sshd-session parser** | ✅ Debian 13 journal · Engine classify |
| **rsyslog imjournal şablonu** | ✅ yalnızca Failed/Accepted password |
| **DI IT wiki (Linux rsyslog)** | ✅ `guvenlik-merkezi-linux-rsyslog-kurulumu.md` · test+prod seed |
| **Lab temizlik** | ✅ `reset-siem-lab-data.ps1 -Apply` |
| **İki-host pilot** | ✅ test 20.20 + prod 20.8 → Engine · 20 fail + 4 ok · **2× U1 alarm** |

**Pilot kullanıcıları (Odak lab):** `pilot_fail_test20` / `pilot_ok_test20` (monitrang) · `pilot_fail_prod08` / `pilot_ok_prod08` (monitrang-prod)

**Sıradaki (SIEM chat):** mola — bkz. üst [Mola checkpoint](#mola-checkpoint-6-haz-2026--siem--alarm-merkezi-ui-kapandi). Opsiyonel ingest: FortiGate hostname · Sysmon filtresi · Faz 5 en sonda.

### Oturum checkpoint (6 Haz 2026 — IT merkezi port + E2E)

| Konu | Durum |
|------|--------|
| IT topolojisi | TERMINAL `192.168.20.13` → `:1514` JSON · FortiGate → `:541/542` |
| Engine multi-port | ✅ 6 listener · relay kapatıldı |
| Parser `windows.nxlog-json.v1` | ✅ Security · Sysmon drop |
| Canlı TERMINAL | ✅ 4624/4625/4672 |
| NxLog E2E | ✅ ingest · U1 alarm · workflow · approval→block.ip |
| FortiGate E2E | ✅ ingest · U4 alarm · U4 workflow |
| Commit (IT port oturumu) | ✅ sonraki commitlerde |

### Oturum checkpoint (6 Haz 2026 — Alarm Merkezi UI + sequence form)

| Konu | Durum |
|------|--------|
| Alarm Merkezi UI (alarmlar, kurallar, lifecycle) | ✅ commit `c68669c` |
| Sequence kural formu + U2 preset | ✅ commit `969b57b` |
| Smoke `test-siem-alarm-ui-smoke.ps1` | ✅ |
| Odak deploy | ✅ `mngui` `-NoCache` |
| Karar | Temel SIEM-hafif **yeterli** → diğer modüllere mola |

### Oturum checkpoint (6 Haz 2026 — Odak deploy + bootstrap hatası, arşiv)

| Konu | Durum |
|------|--------|
| Odak deploy | ✅ sync + `mngreactor,mngengine,mngui` `-NoCache` |
| Engine config | ⚠️ `LICENSE_EXPIRED` (DataGateway); Reactor `config-string` workaround |
| `test-nxlog-wec-template-e2e.ps1` | ✅ PASS |
| Pilot PC | `TERMINAL.odak.local` · admin yok · IT NxLog CE 3.2.2329 kurdu |
| NxLog config | ❌ IT tüm `nxlog.conf` değiştirdi → bootstrap/Moduledir yok → `to_json()` hatası |
| SIEM TERMINAL olayları | ❌ 0 kayıt |
| Yeni şablon/script (uncommitted) | `nxlog.conf.bootstrap` · `nxlog-endpoint-monitrang-siem.conf` · `apply-nxlog-endpoint-config.ps1` |

**IT düzeltme:** HANDOFF §8 — bootstrap + `nxlog.d\monitrang-siem.conf`

**Operasyon notu:** Benchmark veya yoğun ingest sonrası E2E/workflow testleri önce kuyruk temizliği gerektirebilir:
- `purge-workflow-queues.ps1 -Apply` — `workflow.execution`, `workflow.event.inbound`, `alarm.observation.inbound` (birleşik)
- `purge-alarm-observation-queue.ps1` — yalnızca observation (worker restart ile)

`test-siem-e2e-suite.ps1` alarm + workflow purge adımlarını otomatik çalıştırır.

E2E geçici alarm kuralları birikimini temizlemek: `purge-siem-e2e-alarm-rules.ps1 -Apply` (`siem-mvp-v1` paket kuralları korunur). Ardından operasyonel eşikler: `seed-siem-alarm-rule-pack.ps1 -Replace`. Bkz. [SIEM_ALARM_RULE_PACK.md](./SIEM_ALARM_RULE_PACK.md).

---

## 7. İlgili DEVAM dosyaları

- [alarm/DEVAM.md](../alarm/DEVAM.md) — MngAlarm motor · Alarm Merkezi UI özeti
- [workflow/DEVAM.md](../workflow/DEVAM.md) — P4 engine.command · alarm→OC backlog
- [SEC_EVENT_OBSERVATION_MAP.md](./SEC_EVENT_OBSERVATION_MAP.md) — ✅ implementasyon
