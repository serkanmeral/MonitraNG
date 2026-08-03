# SIEM / Monitoring — Oturum Handoff

**Son güncelleme:** 3 Ağustos 2026 (host agent cutover · RDP · prod sabitleme)  
**Canlı SIEM durum:** [../siem/current_status.md](../siem/current_status.md) · [../siem/HOST_TELEMETRY_CUTOVER.md](../siem/HOST_TELEMETRY_CUTOVER.md)  
**Ana DEVAM (eski özet):** [DEVAM.md](./DEVAM.md)  
**Platform UI (ayrı chat):** [../PLATFORM_HANDOFF.md](../PLATFORM_HANDOFF.md)

---

## 1. Tek cümlede durum

**Host telemetrisi = MngLogs agent → LogCollector → OpenSearch** · NXLog/Linux syslog Engine/Reactor kapalı · FortiGate syslog açık · Lokal UI + agent varsayılanı **prod `192.168.20.8`** · RDP `rdp.*` normalize + SIEM filtre · **Sıradaki:** prod SIEM doğrulama · park Analytics L3 / Discovery / G4 alarm köprü

---

## 2. Platform durumu (güncel)

| Konu | Durum |
|------|--------|
| MngReactor Odak | ✅ `mngreactor` (OS read + NXLog/Linux guards) |
| MngEngine syslog | ✅ FortiGate `:541/:542` · NXLog/Linux host UDP **kapalı** |
| Host yolu | ✅ Agent → Collector `:5091` → OpenSearch |
| RDP LocalSessionManager | ✅ normalize `rdp.logon/logoff/disconnect/reconnect` |
| Lokal Mng.Ui / agent | ✅ varsayılan **prod** (test ayrı script) |
| SIEM `sec_events` (legacy NXLog Mongo) | ⚠️ tarihsel; canlı host OS |
| sec_events → observation → alarm | ✅ (firewall / kurallar; host agent zinciri doğrulanacak) |
| LogAlarm / 5651 (Faz 5) | ⬜ ertelendi |
| SIEM UI | ✅ `/apps/siem-center` · Events intent filtreleri |

> Eski NXLog/WEC/Linux rsyslog satırları tarihsel referans için [DEVAM.md](./DEVAM.md) ve aşağıdaki arşiv bölümlerinde kalır; **operasyonel host yolu agent’tır.**

---

## 2b. Arşiv — önceki platform tablosu (Haziran 2026)

| Konu | Durum (o dönem) |
|------|--------|
| MngReactor Odak | ✅ `mngreactor:latest` |
| MngEngine syslog | ✅ Çoklu UDP: `:5514` linux · `:1514/1541/1542` windows-nxlog · `:541/542` fortigate |
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
| Odak deploy (6 Haz) | ✅ `mngreactor` + `mngengine` + `mngui` — `-NoCache` |
| Engine config (6 Haz) | ⚠️ `setup-mngengine-odak.ps1 -ApplyConfig` → **LICENSE_EXPIRED** (DataGateway PUT); Reactor `config-string` → Engine `/api/Config` ile düzeltildi |
| **Windows NxLog (IT merkezi)** | ⛔ **retired 2026-08** — yerini MngLogs agent aldı |
| **FortiGate syslog (IT merkezi)** | ✅ `firewall.vendor.v1` · `:541` · U4 alarm + workflow E2E |
| NxLog şablon / script | Tarihsel — host ingest kapalı |
| **Engine çoklu port** | FortiGate portları aktif; NXLog/Linux host portları compose’dan çıkarıldı |
| **NxLog parser** | Kod kalabilir; ingest guard drop |

---

## 3. Kanıtlanmış zincirler (Odak)

```
U1: sec_events → observation → correlation → alarm.raised → Workflow → (approval) → block.ip
U1 (Linux): linux.auth.v1 sshd → login_failed → alarm.raised → Workflow → approval → block.ip
U1 (NxLog): windows.nxlog-json.v1 → UDP :1514 → login_failed → alarm.raised → Workflow → approval → block.ip
U4: firewall syslog → sec_events → observation → correlation → alarm.raised → Workflow
U4 (FortiGate): firewall.vendor.v1 → UDP :541 → denied_flow → alarm.raised → Workflow
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
| **Son commit** | `3a8a2ac` (5 Haz — sec_events veri yönetimi + Linux pilot) |
| **Commitlenmemiş (6 Haz oturum)** | NxLog şablonları · IT relay şablonu · Engine multi-port · `windows.nxlog-json.v1` parser · E2E scriptleri (`test-siem-fortigate-*`, `test-siem-nxlog-json-*`, `test-nxlog-json-*`) |
| Odak deploy | ✅ **`mngreactor` + `mngui` + `mngengine`** (6 Haz 2026) |
| Odak lab veri | 2× U1 alarm · pilot kullanıcıları — reset ile temizlenebilir |

**Engine config (deploy sonrası — bilinen sorun):**

`setup-mngengine-odak.ps1 -ApplyConfig` DataGateway’de `LICENSE_EXPIRED` verir. Geçici çözüm:

```powershell
# Reactor config-string al → Engine'e uygula (token + engineId)
# Bkz. setup-mngengine-odak.ps1 veya manuel POST http://192.168.20.20:5037/api/Config
pwsh scripts/odak/test-nxlog-wec-template-e2e.ps1   # ingest smoke
```

---

## 6. Odak operasyon (hatırlatma)

| Konu | Değer |
|------|--------|
| Gateway | `http://192.168.20.20:5040` |
| Engine | `http://192.168.20.20:5037` · syslog UDP çoklu port (aşağı) |
| Engine UDP dinleyiciler | `:5514` linux-syslog · `:1514/1541/1542` windows-nxlog · `:541/542` fortigate |
| Domain / kullanıcı | `odak` · `odak_admin` / `Admin123!` |
| Linux test host | `192.168.20.20` (`monitrang`) · rsyslog → `127.0.0.1:5514` |
| Linux prod host | `192.168.20.8` (`monitrang-prod`) · rsyslog → `192.168.20.20:5514` |
| Windows (IT) | `192.168.20.13` TERMINAL (+ DC) · NxLog ham JSON UDP → `192.168.20.20:1514` |
| FortiGate (IT) | syslog → `192.168.20.20:541` (542 yedek) |
| Windows pilot (eski plan) | `TERMINAL.odak.local` · wec-batch yolu lab smoke için hâlâ geçerli |

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

## 8. Oturum notu (6 Haz 2026 — IT merkezi port + Engine çoklu UDP)

### IT topolojisi (doğrulandı)

| Kaynak | Kaynak IP | Hedef | Port | Format |
|--------|-----------|-------|------|--------|
| TERMINAL (+ DC) | `192.168.20.13` | `192.168.20.20` | **1514** (1541/1542) | NxLog **ham JSON UDP** (Sysmon + Security) |
| FortiGate | — | `192.168.20.20` | **541/542** | FortiGate key=value syslog |
| Linux pilot | — | `:5514` | rsyslog sshd (mevcut) |

**Karar:** IT port değiştiremiyor → Engine **çoklu UDP dinleyici** (kalıcı). Geçici **rsyslog relay** (`1514/541` → `5514`) kuruldu, multi-port deploy sonrası **kapatıldı** (port çakışması).

### Yapılanlar

| Konu | Sonuç |
|------|--------|
| Parser `windows.nxlog-json.v1` | ✅ Security 4624/4625/4672/4720… · Sysmon → `DropUnknownEvents` |
| Engine `ClassifySource` + multi-port | ✅ 6 listener · docker-compose.odak.yml port map |
| Relay (geçici) | ✅ kuruldu → multi-port sonrası silindi |
| NxLog ingest smoke | ✅ `test-nxlog-json-syslog-ingest.ps1` |
| Canlı TERMINAL | ✅ IT → :1514 · 4624/4625/4672 SIEM'de |
| U1 alarm (NxLog) | ✅ `test-siem-nxlog-json-u1-alarm-e2e.ps1` |
| U1 → Workflow (NxLog) | ✅ `test-siem-nxlog-json-u1-workflow-e2e.ps1` |
| U1 → approval → block.ip (NxLog) | ✅ `test-siem-nxlog-json-u1-approval-block-e2e.ps1` |
| FortiGate ingest | ✅ `test-siem-fortigate-syslog-udp-ingest.ps1` |
| U4 alarm (FortiGate) | ✅ `test-siem-fortigate-u4-alarm-e2e.ps1` |
| U4 → Workflow (FortiGate) | ✅ `test-siem-fortigate-u4-workflow-e2e.ps1` |
| Engine config | ⚠️ deploy sonrası Reactor `config-string` → `/api/Config` (LICENSE_EXPIRED workaround) |

### Bilinen operasyon notları

- Relay + Engine multi-port **aynı anda 1514'te çakışır** — relay kapalı kalmalı
- FortiGate `source.host` bazen syslog hostname yerine `time=…` (parse/alarm çalışıyor; iyileştirme opsiyonel)
- E2E geçici alarm kuralları: `purge-siem-e2e-alarm-rules.ps1 -Apply`

### Eski oturum (6 Haz sabah — bootstrap hatası)

IT ilk kurulumda tüm `nxlog.conf` yerine yazmıştı → bootstrap/Moduledir yok → `to_json()` hatası. IT merkezi syslog modeline geçildi; endpoint bootstrap şablonları referans olarak duruyor:

- [templates/nxlog.conf.bootstrap](./templates/nxlog.conf.bootstrap)
- [templates/nxlog-endpoint-monitrang-siem.conf](./templates/nxlog-endpoint-monitrang-siem.conf)
- `scripts/odak/apply-nxlog-endpoint-config.ps1`

### Commitlenmemiş dosyalar

**Kod:** `WindowsNxlogJsonParser` · Engine multi-port · `docker-compose.odak.yml`  
**Şablon/script:** `nxlog*.conf` · `rsyslog-it-relay-to-engine.conf` · `install-it-syslog-relay-odak.ps1`  
**E2E:** `test-nxlog-json-syslog-ingest.ps1` · `test-siem-nxlog-json-*.ps1` · `test-siem-fortigate-*.ps1`  
**Fixture:** `tests/fixtures/siem/nxlog_terminal_4625.json.txt` · `nxlog_terminal_sysmon_process.json.txt`

---

## 8b. Oturum notu (6 Haz sabah — Odak deploy + bootstrap hatası, arşiv)

### Yapılanlar

| Konu | Sonuç |
|------|--------|
| Odak sync + deploy | ✅ `mngreactor`, `mngengine`, `mngui` (`-NoCache`) |
| Engine config | ⚠️ `setup-mngengine-odak.ps1 -ApplyConfig` → `LICENSE_EXPIRED` (mon_engines PUT); Reactor `config-string` + `/api/Config` ile düzeltildi |
| Ingest smoke | ✅ `test-nxlog-wec-template-e2e.ps1` PASS |
| Windows pilot makine | `TERMINAL.odak.local` · kullanıcı `odak\monitra` · **yerel admin yok** |
| IT NxLog kurulumu | ✅ NXLog-CE 3.2.2329 · servis Running |
| IT config | ❌ MonitraNG bloğu **tüm `nxlog.conf` yerine** yazılmış → `Moduledir`/bootstrap yok → `to_json()` parse hatası |
| SIEM olay akışı | ❌ `sourceHost=TERMINAL.odak.local` → **0 olay** |

### IT düzeltme (doğru yapı)

1. **`C:\Program Files\nxlog\conf\nxlog.conf`** ← [templates/nxlog.conf.bootstrap](./templates/nxlog.conf.bootstrap) (Moduledir + `include nxlog.d\*.conf`)
2. **`C:\Program Files\nxlog\conf\nxlog.d\monitrang-siem.conf`** ← [templates/nxlog-endpoint-monitrang-siem.conf](./templates/nxlog-endpoint-monitrang-siem.conf)
3. `Restart-Service nxlog`
4. Doğrula: `& "C:\Program Files\nxlog\nxlog.exe" -v -f "C:\Program Files\nxlog\conf\nxlog.conf"` (ERROR yok)
5. Test: başarısız oturum → SIEM'de `login_failed` / `TERMINAL.odak.local`

**Script (yönetici):** `scripts/odak/apply-nxlog-endpoint-config.ps1 -Apply`

**WEC notu:** Bu makinede Forwarded Events yok; endpoint = yerel **Security** log. WEC senaryosu ayrı sunucu + [nxlog-wec-to-engine.conf](./templates/nxlog-wec-to-engine.conf).

### Commitlenmemiş dosyalar

- `docs/odak/monitoring/templates/nxlog-endpoint-to-engine.conf`
- `docs/odak/monitoring/templates/nxlog-endpoint-monitrang-siem.conf`
- `docs/odak/monitoring/templates/nxlog.conf.bootstrap`
- `scripts/odak/install-nxlog-endpoint.ps1`
- `scripts/odak/apply-nxlog-endpoint-config.ps1`

---

## 9. SIEM chat prompt'u (yeni chat — kopyala-yapıştır)

```markdown
# MonitraNG — SIEM handoff (Windows NxLog pilot devam)

Yanıtlar **Türkçe**. Commit/push yalnızca açıkça istediğimde.

## Bağlam
- **HANDOFF:** docs/odak/monitoring/HANDOFF.md (§8 oturum notu)
- **DEVAM:** docs/odak/monitoring/DEVAM.md
- **Windows toplama:** SIEM_WEF_WEC_FORWARDER.md · templates/nxlog*.conf
- **Linux toplama:** SIEM_LINUX_RSYSLOG_FORWARDER.md

## Durum (6 Haz 2026 — IT merkezi port)
- Git `main` @ `3a8a2ac` · multi-port + parser + E2E scriptleri **commitlenmedi** (kullanıcı talebi bekleniyor)
- **Odak deploy ✅** mngreactor + mngengine + mngui · 6 UDP listener aktif
- **Engine config ⚠️** deploy sonrası Reactor config-string → `/api/Config`
- **Windows NxLog ✅** IT → UDP :1514 · `windows.nxlog-json.v1` · canlı TERMINAL olayları
- **FortiGate ✅** UDP :541 · U4 alarm + workflow E2E
- **Linux pilot ✅** 2× U1 (monitrang + monitrang-prod)

## Odak
- Gateway http://192.168.20.20:5040 · Engine :5037
- UDP: `:5514` linux · `:1514/1541/1542` windows-nxlog · `:541/542` fortigate
- SIEM UI: http://192.168.20.20:3000/apps/siem-center/events

## Sıradaki
1. Commit (kullanıcı talebi)
2. Opsiyonel: FortiGate hostname parse · Sysmon whitelist
3. Faz 5 ertelendi

## Bu oturumda ne yapmak istiyorum?
[Buraya yaz — örn. "IT config sonrası kontrol + U1 E2E"]
```

---

## 10. İlk komutlar (opsiyonel)

---

```powershell
# Engine config (deploy sonrası)
# Reactor config-string → POST http://192.168.20.20:5037/api/Config

# NxLog / FortiGate E2E (Odak)
pwsh scripts/odak/test-nxlog-json-syslog-ingest.ps1
pwsh scripts/odak/test-siem-nxlog-json-u1-alarm-e2e.ps1
pwsh scripts/odak/test-siem-nxlog-json-u1-workflow-e2e.ps1
pwsh scripts/odak/test-siem-nxlog-json-u1-approval-block-e2e.ps1
pwsh scripts/odak/test-siem-fortigate-syslog-udp-ingest.ps1
pwsh scripts/odak/test-siem-fortigate-u4-alarm-e2e.ps1
pwsh scripts/odak/test-siem-fortigate-u4-workflow-e2e.ps1

# Odak sağlık
. .\scripts\odak\OdakSshCommon.ps1; Initialize-OdakSshEnvironment
Invoke-WebRequest http://192.168.20.20:5040/health -UseBasicParsing
```

---

## 11. Referanslar

- [SIEM_PLANNING.md](./SIEM_PLANNING.md)
- [SIEM_ROADMAP.md](./SIEM_ROADMAP.md)
- [SIEM_LOGALARM_COMPARISON.md](./SIEM_LOGALARM_COMPARISON.md)
- [benchmarks/README.md](./benchmarks/README.md)
- [workflow/DEVAM.md](../workflow/DEVAM.md)
- [alarm/DEVAM.md](../alarm/DEVAM.md)
