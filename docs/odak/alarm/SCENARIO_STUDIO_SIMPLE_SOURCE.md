# SIEM Scenario Studio — Basit Olay Kaynağı & Flow Lab çıktıları

**Son güncelleme:** 8 Ağustos 2026  
**Kapsam:** `Mng.Ui` Alarm Merkezi → Scenario Studio / Flow Lab (`/apps/alarm-center/flow-lab`)  
**Backend:** `MngAlarm` ScenarioDefinition **v3** (graph) + `matchKeys` + `debug-output` + `dedup.mergeEnabled`

---

## 1. Amaç

Kullanıcının generic/teknik alanlarla uğraşmadan **basit flow** kurması.  
Gelişmiş (kind / observationKind / ham matchKey) alanlar inspector’da **Gelişmiş** altında salt okunur; ileride ayrı “gelişmiş kaynak” node’una taşınabilir.

---

## 2. UI bileşenleri

| Bileşen | Rol |
|---------|-----|
| `AcFlowLab.vue` | Vue Flow canvas, katalog, **gruplu palette**, managed scope, simulate + debug paneli |
| `AcScenarioSourceInspector.vue` | Basit kaynak formu (platform / kanal / olay / host / metrik) |
| `AcEventSelectorField.vue` | Olay seçici modal (Windows Event ID · Linux journal) |
| `AcScenarioCatalogTree.vue` | Senaryo katalog ağacı (klasör, yeni senaryo) |

Yardımcılar:

- `utils/alarm/scenarioSimpleSource.ts` — basit state, managed filtre id’leri, metrik karşılaştırma
- `utils/alarm/eventCatalog.ts` — Windows channel dictionary + özel Event ID
- `utils/alarm/linuxJournalCatalog.ts` — Linux paket + `event.action` kataloğu
- `utils/alarm/scenarioFlowMapper.ts` — v2↔v3 / Vue Flow mapping (debug visual dahil)
- `utils/alarm/alarmOutputMerge.ts` — birleştirme kapsamı / `mergeEnabled` / groupBy
- `utils/alarm/scenarioOperationalStatus.ts` — Açık/Kapalı / lifecycle chip
- `AcAlarmOutputInspector.vue` — Alarm node birleştirme + gruplama UI

### 2.1 Palette grupları

| Grup | Node’lar |
|------|----------|
| **Olaylar** | `source` (basit olay kaynağı) |
| **Fonksiyonlar** | condition, filter, aggregation, threshold, sequence, decision |
| **Çıktılar** | alarm-output, stop-output, **debug-output** |

Grup başlıkları collapse/expand (`paletteGroupCollapsed`).

---

## 3. Platform × kanal matrisi

| Platform | Kanal | Olay seçimi | Otomatik devam node’ları |
|----------|-------|-------------|---------------------------|
| Windows | EventLog | Modal tablo (channel dictionary + özel Event ID) | OS tipi filtresi · EventCode `in` · Host |
| Linux | EventLog | Modal tablo (journal paket + action + özel) | `sourceType=linux-journal` · Host · `matchKeys` |
| * | Metrik | Metrik + operatör + eşik | Host · **condition** `value {op} {threshold}` |
| * | Uygulama/Servis | Preset combobox (şimdilik) | Host (OS filtresi yok) |
| Other | EventLog | Preset combobox (firewall vb.) | Host |

### 3.1–3.4

Windows / Linux Event seçici, Metrik, Host — önceki dilimle aynı (detay önceki sürümde).

---

## 4. Backend notları (`MngAlarm`)

- `ScenarioSource.MatchKeys` — observation.key bu listeden herhangi biriyle eşleşebilir.
- `ScenarioCompiler.SourceMatches` / `EffectiveMatchKeys` · V3 aday sorgu `$or` (matchKey | matchKeys).
- Condition `in` operatörü: dizi, JSON array, virgüllü string. Observation normalizer diziyi string’e çevirmez.
- Observation **matchKey** Windows EventLog’da paket id (`powershell-engine`, `security-auth`, …) veya semantik (`rdp.logon`). Event ID ayrı filtre. Ayrıntı: [AGENT_OBSERVATION_AND_FLOW_LAB.md](./AGENT_OBSERVATION_AND_FLOW_LAB.md).
- `ScenarioDedup.mergeEnabled` — açıkken aynı dedupKey’de adet++; kapalıyken her eşleşme yeni alarm.
- **`debug-output`:** `ScenarioDebug` (`mode`, `path`, `active`); executor `ScenarioDebugHit`; preview `debugLines`. Prod `ObservationProcessor` alarm çıktılarından bağımsız — debug hit’leri alarm üretmez.
- `graph.output.required`: yalnızca `alarm-output` veya `stop-output` (debug sayılmaz).

---

## 5. Managed node id’leri

| Id soneki | Anlam |
|-----------|--------|
| `__scope-os` | `dimensions.sourceType` |
| `__scope-eventcode` | `dimensions.eventCode` |
| `__scope-host` | `dimensions.sourceHost` |
| `__scope-metric` | `value` karşılaştırma (condition) |

Kaynak silinince bu node’lar birlikte silinir.

---

## 6. Debug output (`debug-output`)

| Alan | Değer |
|------|--------|
| Tip | `debug-output` |
| Config | `debug.mode` = `complete` \| `path`; `debug.path`; `debug.active` |
| Complete payload | `{ kind, key, value, timestamp, dimensions }` |
| Path | `value` / `key` / `kind` / `timestamp` / `dimensions.x` (veya düz dimension adı) |
| Ortam | **Simulate/preview only** |
| UI | Simülasyon paneli → kronolojik `debugLines` (node label + sample index + payload JSON) |

---

## 7. Çıktı roadmap (kararlar — 6 Ağu)

| Madde | Karar |
|--------|--------|
| Bağımsızlık | Bildirim / WI, Alarm olmadan da tetiklenebilir |
| Stop vs Debug | Ayrı node’lar |
| Prod Debug | Sim-only (log yok) |
| Bildirim MVP | **Mail** (Telegram / inApp sonra) |
| OC WI | Ayrı dilimde planlanacak |

Sıradaki implement: **notify-output (mail)** → sonra WI.

---

## 8. Deploy durumu

| Bileşen | Durum (8 Ağu 2026, Odak `.8`) |
|---------|--------|
| `mngalarm` + worker | ✅ prod (graph, merge, `*.event.#`, `in` dizi, severity hiza) |
| `mnglogcollector` | ✅ prod (paket observation key, `SourceProducts=*`) |
| `Mng.Ui` Flow Lab / toaster / alarm inspector | Kod hazır; **mngui deploy kullanıcı onayıyla** |

---

## 9. Flow işletim durumu ve sağlık

| Kavram | Değerler | Not |
|--------|----------|-----|
| `operationalStatus` | draft / running / stopped / archived | UI: Taslak / **Açık** / **Kapalı** / Arşiv |
| `enabled` | bool | **Aç/Kapat**; arşiv değil. **Açık flow düzenlenemez** |
| `health` | unknown / healthy / warning / error | İkincil rozet; status’tan bağımsız |
| Yayınla | save + validate + publish | **Kapalı kalır**; Aç ayrı |
| API | `POST .../enabled` | Yalnız published user scenario |
| Persist | `AlarmRuleDocument.runtimeHealth` | Eval hata/success |

Otomatik disable on error: **kapalı** (bilinçli).

### Execution log

| Madde | Değer |
|--------|--------|
| Collection | `@mon_alarm_scenario_executions` |
| Retain | Flow başına son **100** |
| Yazılan | observation (source matched) · due · error |
| Yazılmayan | skipped kaynak · simulate |
| Outcome | matched / no_match / stopped / pending / error |
| API | `GET .../scenarios/{id}/executions?limit=100` |

---

## 10. Sıradaki adımlar

1. `mngui` prod deploy (Flow Lab toaster, birleştirme inspector, host/paket key UX).
2. Semantik Event ID kataloğu (opsiyonel; yayın yolu paket key).
3. Bildirim node — mail MVP (Notifier).
4. App/Servis kanalı basit UX (live / staleness).
5. OC workitem-output (ayrı konuşma).
