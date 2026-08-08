# Agent observation köprüsü ve Flow Lab işletimi

**Son güncelleme:** 8 Ağustos 2026  
**Ortam:** Odak production (`192.168.20.8`)  
**Servisler:** `MngLogCollector` · `MngAlarm` (+ worker) · `Mng.Ui` Flow Lab

Agent EventLog olaylarının Alarm motoruna nasıl girdiği, observation key modeli ve Flow Lab’da alarm birleştirme / düzenleme kuralları.

---

## 1. Akış

```text
Agent (TERMINAL)  →  MngLogCollector ingest
                         │
                         ├─ OpenSearch / SIEM Events
                         └─ monitra.observations  (topic)
                                routing: {domain}.event.{key}
                                         örn. odak.event.powershell-engine
                                         örn. odak.event.rdp.logon
                                ↓
                         MngAlarm queue bind: *.event.#
                                ↓
                         Scenario graph (Flow) → alarm.raised / updated
```

**Kritik bind:** Noktalı key’ler (`rdp.logon`) dört routing kelimesi üretir. Queue pattern `*.event.*` bunları kaçırır; **`*.event.#`** gerekir. Deploy: `mngalarm` + `mngalarm-worker`.

---

## 2. Observation key — paket, Event ID değil

**Karar (8 Ağu 2026):** Her Event ID için semantik key **zorunlu değildir**. Ölçeklenmez; müşteri paketlerine yeni ID eklendiğinde collector’ı güncellemeyi gerektirmez.

| Katman | Ne taşır | Örnek |
|--------|----------|--------|
| Observation **key** | Paket id (veya isteğe bağlı semantik) | `powershell-engine`, `rdp.logon` |
| `dimensions.eventCode` | Windows Event ID | `400`, `21` |
| `dimensions.sourceHost` | Kısa hostname (DNS suffix yok) | `TERMINAL` — **IP değil** |
| `dimensions.sourceType` | Kaynak tipi | `windows-eventlog` |
| `dimensions.sourceProduct` | Paket id (stamp) | `powershell-engine` |

### 2.1 Collector çözümü (`AgentSecEventActionNormalizer.ResolveObservationKey`)

1. Semantik map varsa kullan (bugün yalnızca RDP: `21/23/24/25` → `rdp.logon` …).
2. Yoksa **paket id** (`SourceProduct`) key olur.
3. Paket de yoksa ve Event ID varsa fallback: `windows.eventlog`.
4. Map yok diye olay **düşmez**.

Allowlist: `ObservationPublish.SourceProducts`. **`*`** veya boş liste = tüm damgalı paketler. Odak compose: `SourceProducts__0=*`.

Kod:

- `MngLogCollector/.../AgentObservationMapper.cs`
- `MngLogCollector/.../AgentSecEventActionNormalizer.cs`
- `MngLogCollector/.../AgentObservationPublisher.cs`

### 2.2 Flow Lab eşlemesi (UI)

Yeni flow’larda Event ID seçilince `matchKey` artık `unknown` olmamalı; kanal → paket key (`eventCatalog.observationKeyForEvent` / `packageKeyForChannel`). Event ID filtresi managed `__scope-eventcode` node’unda kalır.

**Mevcut yayınlı flow’lar otomatik düzelmez** — Kapat → Düzenle → `matchKey`/`matchKeys` + host → Yayınla → Aç.

### 2.3 Host filtresi

Agent `sourceHost` = kısa hostname (`TERMINAL.odak.local` → `TERMINAL`).  
Flow host filtresine **IP yazmak eşleşmez**. Discovery / hostname kullanın.

---

## 3. Alarm `in` filtresi (Event ID listesi)

`ObservationValueNormalizer` JSON dizisini string’e çevirmemeli. Aksi halde `eventCode in ["21","23",…]` `no_match` olur.

Düzeltme: diziler liste olarak kalır; legacy JSON-array string hâlâ `ScenarioCompiler.EnumerateInStringValues` ile parse edilir.

---

## 4. Flow Lab işletim sözlüğü

| UI | Anlam |
|----|--------|
| **Taslak** | Henüz yayınlanmamış |
| **Yayında · Kapalı** | Publish edilmiş, `enabled=false` — dinlemiyor |
| **Yayında · Açık** | `enabled=true` — observation dinliyor |
| **Yayınla** | Kaydet + doğrula + publish; **Kapalı kalır** |
| **Aç / Kapat** | `POST .../versions/{v}/enabled` |

### 4.1 Düzenleme kilidi

- **Açık** flow düzenlenemez (canvas + node alanları kilitli). Önce **Kapat**.
- **Kapalı** yayınlı flow açılınca otomatik sonraki taslağa geçilir.
- Product şablonları salt okunur.

### 4.2 Alarm node — birleştirme ve gruplama

`ScenarioDedup.mergeEnabled` (varsayılan `true`, eski kayıtlar birleştirmeyi açık sayar).

| Ayar | Etki |
|------|------|
| Birleştir açık + kapsam “tümü” | Aynı flow için tek açık alarm, adet++ |
| Birleştir açık + host | `groupBy: sourceHost` → host başına alarm |
| Birleştir açık + kullanıcı / host+kullanıcı | `userId` / ikisi |
| Birleştir kapalı | Her eşleşmede yeni alarm (`dedupKey` unique) |
| Gürültü bekleme (cooldown) | Yalnız birleştir açıkken; süre içinde sessiz |

UI: `AcAlarmOutputInspector.vue` · `utils/alarm/alarmOutputMerge.ts`.

Runtime: `ScenarioGraphExecutor` + `ObservationProcessor.ProcessGraphOutputsAsync`.

### 4.3 Severity

Alarm node `config.severity` ile version `severity` senkron (`ScenarioService.AlignAlarmSeverity`, UI `syncAlarmOutputSeverity`). Runtime: `node.Config.Severity ?? rule.Severity`.

### 4.4 Toaster

Flow Lab kart alert yerine global `useAppToast` stack (en fazla 6, yeniler üstte). `AppGlobalToast.vue`.

---

## 5. Örnek: PowerShell Alerts (Odak prod)

| Alan | Değer |
|------|--------|
| ScenarioId | `af627062848c4b06845db89cb64c0138` |
| Yayın | v3, Açık |
| matchKey / matchKeys | `powershell-engine` |
| Event ID filtresi | `400`, `403`, `600` |
| Host | `TERMINAL` |
| Doğrulama | `POST /alarm/api/v1/dev/observations/ingest` → `alarmsRaised: 1` |

---

## 6. Deploy (Odak `.8`)

| Bileşen | Durum (8 Ağu 2026) |
|---------|---------------------|
| `mnglogcollector` | ✅ paket key + `SourceProducts=*` |
| `mngalarm` + `mngalarm-worker` | ✅ `*.event.#`, `in` dizi, merge, severity hizası |
| `mngui` | Flow Lab / toaster / alarm inspector **UI deploy kullanıcı onayıyla**; lokal kod hazır |

Compose: `ApplicationResources/mng_apps/docker-compose.odak.prod.yml`  
`MngLogCollectorSettings__ObservationPublish__Enabled=true`  
`MngLogCollectorSettings__ObservationPublish__SourceProducts__0=*`

---

## 7. Semantik map (sonra)

RDP key’leri (`rdp.logon` …) isteğe bağlı kolaylık olarak duruyor. Genel Event ID → semantik katalog **ayrı iş**; zorunlu yayın yolu paket key + `eventCode`.

---

## 8. İlgili kod / test

| Alan | Dosya |
|------|--------|
| Collector map | `AgentObservationMapperTests` |
| `in` dizi | `ObservationValueNormalizerTests` |
| Graph merge | `ScenarioGraphV3Tests.Alarm_output_merge_disabled_emits_unique_dedup_keys` |
| UI merge scope | `Mng.Ui/utils/alarm/alarmOutputMerge.test.ts` |
| E2E ingest | `POST /alarm/api/v1/dev/observations/ingest` |
