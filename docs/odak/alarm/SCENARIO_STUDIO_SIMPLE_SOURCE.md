# SIEM Scenario Studio — Basit Olay Kaynağı (Flow Lab)

**Son güncelleme:** 5 Ağustos 2026  
**Kapsam:** `Mng.Ui` Alarm Merkezi → Scenario Studio / Flow Lab (`/apps/alarm-center/flow-lab`)  
**Backend:** `MngAlarm` ScenarioDefinition **v3** (graph) + `matchKeys`

---

## 1. Amaç

Kullanıcının generic/teknik alanlarla uğraşmadan **basit flow** kurması.  
Gelişmiş (kind / observationKind / ham matchKey) alanlar inspector’da **Gelişmiş** altında salt okunur; ileride ayrı “gelişmiş kaynak” node’una taşınabilir.

---

## 2. UI bileşenleri

| Bileşen | Rol |
|---------|-----|
| `AcFlowLab.vue` | Vue Flow canvas, katalog, palette, managed scope sync |
| `AcScenarioSourceInspector.vue` | Basit kaynak formu (platform / kanal / olay / host / metrik) |
| `AcEventSelectorField.vue` | Olay seçici modal (Windows Event ID · Linux journal) |
| `AcScenarioCatalogTree.vue` | Senaryo katalog ağacı (klasör, yeni senaryo) |

Yardımcılar:

- `utils/alarm/scenarioSimpleSource.ts` — basit state, managed filtre id’leri, metrik karşılaştırma
- `utils/alarm/eventCatalog.ts` — Windows channel dictionary + özel Event ID
- `utils/alarm/linuxJournalCatalog.ts` — Linux paket + `event.action` kataloğu

---

## 3. Platform × kanal matrisi

| Platform | Kanal | Olay seçimi | Otomatik devam node’ları |
|----------|-------|-------------|---------------------------|
| Windows | EventLog | Modal tablo (channel dictionary + özel Event ID) | OS tipi filtresi · EventCode `in` · Host |
| Linux | EventLog | Modal tablo (journal paket + action + özel) | `sourceType=linux-journal` · Host · `matchKeys` |
| * | Metrik | Metrik + operatör + eşik | Host · **condition** `value {op} {threshold}` |
| * | Uygulama/Servis | Preset combobox (şimdilik) | Host (OS filtresi yok) |
| Other | EventLog | Preset combobox (firewall vb.) | Host |

### 3.1 Windows Event seçici

- Kaynak: LogCollector `GET .../eventlog-packages/channels` → `EventLogChannelDictionary` (statik curated liste).
- Seçim birimi: `kanal::eventId`.
- Sözlük dışı: modalda kanal + Event ID + isteğe bağlı ad (ör. Application · **65002** LogAlarm).
- Motor: bilinen ID → `matchKeys` (action); filtre: `dimensions.eventCode`.

### 3.2 Linux Event seçici

- Event ID yok; birim **paket + `event.action`** (`linux::sshd::login_failed`).
- Katalog: agent `BuiltinJournalPackages` + parse-rule seed aksiyonları (sshd / sudo / unit-fail).
- Özel: paket + action (ör. `cron` · `job_failed`).
- Motor: `matchKeys`; EventCode filtresi yok.

### 3.3 Metrik

- Alanlar: metrik anahtarı · operatör (`gt/gte/lt/lte/eq/neq`) · eşik.
- Varsayılan: CPU/bellek `gte 90`; `disk_free_percent` `lt 10`.
- Managed node: **condition** (`value {op} {eşik}`), kesikli kenarlık, salt okunur.

### 3.4 Host

- Discovery host listesi (`/discovery/hosts`), platform `osHint` ile süzülür.
- Multiselect + **Hepsi** (filtre yok).
- 1 host → `eq`; çoklu → `in` (`dimensions.sourceHost`).

---

## 4. Backend notları (`MngAlarm`)

- `ScenarioSource.MatchKeys` — observation.key bu listeden herhangi biriyle eşleşebilir.
- `ScenarioCompiler.SourceMatches` / `EffectiveMatchKeys` · V3 aday sorgu `$or` (matchKey | matchKeys).
- Condition `in` operatörü: dizi, JSON array, virgüllü string.

---

## 5. Managed node id’leri

| Id soneki | Anlam |
|-----------|--------|
| `__scope-os` | `dimensions.sourceType` |
| `__scope-eventcode` | `dimensions.eventCode` |
| `__scope-host` | `dimensions.sourceHost` |
| `__scope-metric` | `value` karşılaştırma (condition) |

Kaynak silinince bu node’lar birlikte silinir. Inspector’da managed node düzenlenemez; kaynak üzerinden güncellenir.

---

## 6. Deploy durumu (bu dilim)

| Bileşen | Durum |
|---------|--------|
| `MngAlarm` API/Worker (v3 graph, matchKeys, due eval) | Kod lokal; prod deploy bu dilimde yapılmadı (önceki v3 dilimleri prod’da olabilir) |
| `Mng.Ui` Flow Lab / basit kaynak | Lokal hazır · **prod deploy yok** |

---

## 7. Sıradaki adımlar (öneri)

1. **Uygulama / Servis** kanalı için basit UX (sinyal + isteğe bağlı staleness).
2. Basit **Alarm çıktısı** / **Durdur** node inspector’ları.
3. Condition / Aggregation için basit sadeleştirme (gelişmiş’e ayırma).
4. Linux kataloğunu parse-rule API’den canlı besleme (opsiyonel).
5. Kontrollü `Mng.Ui` (+ gerekirse `MngAlarm`) prod deploy.
