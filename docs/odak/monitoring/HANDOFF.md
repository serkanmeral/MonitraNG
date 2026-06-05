# SIEM / Monitoring — Oturum Handoff

**Son güncelleme:** 5 Haziran 2026 (mola — veri yaşam döngüsü + Linux iki-host pilot)  
**Ana DEVAM:** [DEVAM.md](./DEVAM.md)  
**Platform UI (ayrı chat):** [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md)

---

## 1. Tek cümlede durum

**SIEM Faz 1–4 ✅** · **sec_events veri yönetimi MVP ✅** (unknown drop, hot TTL, rawPreview) · Odak **2× Linux U1 pilot** (`monitrang` + `monitrang-prod`) · Faz 5 ertelendi · **Sıradaki:** FortiGate/Windows pilot + Odak `mngreactor`/`mngui` deploy

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
| Linux rsyslog şablon + Engine classify | ✅ [SIEM_LINUX_RSYSLOG_FORWARDER.md](./SIEM_LINUX_RSYSLOG_FORWARDER.md) · imjournal sıkı filtre |
| **sec_events veri yönetimi** | ✅ `SecEventsSettings` · drop unknown · TTL 60g · rawPreview-only · UI `excludeUnknown` |
| **Linux iki-host pilot** | ✅ `run-siem-linux-two-host-pilot.ps1` · 2× U1 alarm |
| **Lab reset** | ✅ `reset-siem-lab-data.ps1 -Apply` |
| **DI IT wiki (Linux rsyslog)** | ✅ [guvenlik-merkezi-linux-rsyslog-kurulumu.md](../document_intelligence/tutorials/guvenlik-merkezi-linux-rsyslog-kurulumu.md) |
| SIEM CI yerel kapı | ✅ `run-siem-local-gate.ps1` · benchmark JSON verify |
| Quick regression (`-Quick`) | ✅ ~6 dk PASS |
| LogAlarm / 5651 (Faz 5) | ⬜ **ertelendi** — [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md) |
| Odak alarm paketi (`siem-mvp-v1`) | ✅ U1–U7 seed · [SIEM_ALARM_RULE_PACK.md](./SIEM_ALARM_RULE_PACK.md) |
| Alarm kural UI | ✅ `/apps/alarm-center/rules` — SIEM = korelasyon kuralları (ayrı U CRUD yok) |
| SIEM UI | ✅ `/apps/siem-center` · `/apps/siem-center/events` — [SIEM_DASHBOARD.md](./SIEM_DASHBOARD.md) · [SIEM_EVENTS_UI.md](./SIEM_EVENTS_UI.md) |

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

**Operasyon:** Benchmark/E2E öncesi veya sonrası gerekirse:
- `purge-workflow-queues.ps1 -Apply` (birleşik workflow + observation)
- `purge-alarm-observation-queue.ps1` (observation + worker restart)
- **`purge-siem-e2e-alarm-rules.ps1 -Apply`** — E2E + P4 workflow test kuralları (paket korunur)
- **`seed-siem-alarm-rule-pack.ps1 -Replace`** — `siem-mvp-v1` U1–U7 operasyonel eşikler
- **`reset-siem-lab-data.ps1 -Apply`** — sec_events + alarmlar + kuyruk + paket yeniden seed (lab sıfır)
- **`run-siem-linux-two-host-pilot.ps1`** — test 20.20 + prod 20.8 auth syslog + U1 doğrulama

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
| Branch | `main` |
| **Mola checkpoint** | Bu commit (5 Haz 2026) — bkz. `git log -1` |
| Önceki checkpoint | `ed4c7bd` / `62567c3` |
| Odak deploy | ⬜ **`mngreactor` + `mngui` + `mngengine`** (SecEvents + sshd-session + UI) |
| Odak lab veri | 2× U1 alarm · pilot kullanıcıları — reset ile temizlenebilir |

**Deploy (mola sonrası ilk iş):**

```powershell
pwsh scripts/odak/sync-odak-source.ps1 -Paths @('MngReactor','MngEngine','Mng.Ui','ApplicationResources/mng_apps')
pwsh scripts/odak/deploy-odak-apps.ps1 -Services mngreactor,mngengine,mngui -NoCache
pwsh scripts/odak/setup-mngengine-odak.ps1 -ApplyConfig -WaitHealthy
```

---

## 6. Odak operasyon (hatırlatma)

| Konu | Değer |
|------|--------|
| Gateway | `http://192.168.20.20:5040` |
| Engine | `http://192.168.20.20:5037` · syslog UDP `:5514` |
| Domain / kullanıcı | `odak` · `odak_admin` / `Admin123!` |
| Linux test host | `192.168.20.20` (`monitrang`) · rsyslog → `127.0.0.1:5514` |
| Linux prod host | `192.168.20.8` (`monitrang-prod`) · rsyslog → `192.168.20.20:5514` |

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

**Not:** Yoğun benchmark/E2E sonrası P0 kapısı geçici düşebilir; kuyruk purge scriptleri `test-siem-e2e-suite.ps1 -Quick` içinde otomatik. **E2E sonrası alarm listesi şişer** — yukarıdaki purge + seed çalıştırın.

**Syslog rolü:** MngEngine **dinleyici/collector** (kaynaklar Engine’e push); müşteri syslog sunucusuna client değiliz — [SIEM_PLANNING.md §5.1](./SIEM_PLANNING.md).

---

## 7. Planlama oturumu özeti (5 Haz 2026)

Konuşulanlar (kod yok, referans):

| Konu | Özet |
|------|------|
| **Ürün anlatımı** | SIEM-hafif; U1–U10 = iç senaryo kodları (LogAlarm standardı değil); on-prem; onaylı müdahale |
| **SIEM / Alarm / Workflow** | SIEM=veri · Alarm=tespit · Workflow=müdahale süreci |
| **UI** | Güvenlik Merkezi (panel + olay arama) · Alarm Merkezi (kurallar + inbox) |
| **Kural CRUD** | `/apps/alarm-center/rules` — U senaryo sihirbazı yok; `siem-mvp-v1` script ile seed |
| **U8–U10 pakette yok** | Parser/alarm E2E var; paket yalnızca U1–U7 |
| **5137/5139** | Parser’da var; tam U senaryosu/E2E/UX preset eksik |

**Önerilen sıra (pilot yoksa):** perf P2 → extended AD → müşteri ops → gerçek FW API · Faz 5 en sonda.

---

## 8. SIEM chat prompt'u (yeni chat — kopyala-yapıştır)

```markdown
# MonitraNG — SIEM handoff (mola sonrası devam)

Yanıtlar **Türkçe**. Commit/push yalnızca açıkça istediğimde.

## Bağlam
- **HANDOFF:** docs/odak/monitoring/HANDOFF.md
- **DEVAM:** docs/odak/monitoring/DEVAM.md
- **Yol haritası:** docs/odak/monitoring/SIEM_ROADMAP.md
- **Performans / saklama:** SIEM_PERFORMANCE_PLAN.md §2.4 · SIEM_PLANNING.md §9
- **Linux toplama:** SIEM_LINUX_RSYSLOG_FORWARDER.md · DI wiki: document_intelligence/tutorials/guvenlik-merkezi-linux-rsyslog-kurulumu.md

## Durum (5 Haz 2026 mola)
- Git `main` — mola commit pushlandı (SecEvents veri yönetimi + Linux pilot scriptleri)
- **Faz 1–4 ✅** · Faz 5 LogAlarm/5651 **ertelendi**
- **Veri yönetimi MVP ✅:** `SecEventsSettings` — DropUnknownEvents, HotTtlDays=60, PersistFullRaw=false
- **UI:** olay listesinde unknown varsayılan gizli · "Bilinmeyen olayları göster" checkbox
- **Odak lab:** `reset-siem-lab-data.ps1 -Apply` + `run-siem-linux-two-host-pilot.ps1` → 20 fail + 4 ok · **2× U1 alarm**
- Pilot kullanıcılar: `pilot_fail_test20` / `pilot_ok_test20` (monitrang) · `pilot_fail_prod08` / `pilot_ok_prod08` (monitrang-prod)

## Odak
- Gateway http://192.168.20.20:5040 · Engine :5037 · syslog UDP :5514
- Test Linux 192.168.20.20 · Prod Linux 192.168.20.8 (syslog → test Engine)
- **Deploy gerekli:** mngreactor + mngengine + mngui (kod commitlendi; Odak güncellenmemiş olabilir)
- Lab sıfır: `reset-siem-lab-data.ps1 -Apply`
- Pilot tekrar: `run-siem-linux-two-host-pilot.ps1`

## Sütun sözlüğü (olay listesi)
- **Kaynak** = `source.type` (endpoint/ad/firewall), IP değil
- **Host** = log üreten cihaz (`source.host`)
- **Kaynak IP** = istemci IP (`network.srcIp`)
- **Hedef** = akış hedefi (`network.dstIp`) — auth loglarında genelde boş (normal)

## Sıradaki adaylar
1. **Odak deploy** — SecEvents ayarları + sshd-session parser + UI
2. **FortiGate pilot** — deny-only syslog · U4 smoke
3. **Windows NxLog/WEC** — dar Event ID · U1/U2
4. **Gerçek FW API** — onaylı block.ip
5. **Faz 5** — 5651/WORM (ertelendi)

## Bu oturumda ne yapmak istiyorum?
[Buraya yaz]
```

---

## 9. İlk komutlar (opsiyonel)

```powershell
# Yerel
pwsh scripts/ci/run-siem-local-gate.ps1

# Odak sağlık (aynı oturumda SSH env yükle)
. .\scripts\odak\OdakSshCommon.ps1; Initialize-OdakSshEnvironment
Invoke-WebRequest http://192.168.20.20:5040/health -UseBasicParsing
pwsh scripts/odak/run-siem-quick-regression.ps1 -SkipUnitGate
```

---

## 10. Referanslar

- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- [SIEM_ROADMAP.md](./SIEM_ROADMAP.md)
- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md)
- [benchmarks/README.md](./benchmarks/README.md)
- [workflow/DEVAM.md](../workflow/DEVAM.md)
- [alarm/DEVAM.md](../alarm/DEVAM.md)
