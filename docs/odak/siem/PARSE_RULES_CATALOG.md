# SIEM — Parse Rules kataloğu (P5 spec)

**Durum:** Dilím 0–5 ✅ (spec + API + Settings UI + sihirbazlar + ingest motor + builtin seed + docs)  
**Son güncelleme:** 3 Ağustos 2026  

**İlgili:** [SIEM_PARSER_PLAN.md](../monitoring/SIEM_PARSER_PLAN.md) · [POLICY_EVENTLOG_PACKAGES.md](./mnglogs/POLICY_EVENTLOG_PACKAGES.md) · [SIEM_PLANNING.md](../monitoring/SIEM_PLANNING.md) §4 (`sec_events`) · [current_status.md](./current_status.md)

---

## 1. Amaç

Ham güvenlik olaylarından (Windows Event Log, Linux journal/syslog, firewall syslog, …) **anlamlı alanlar** çıkarıp `sec_events` ortak şemasına yazmak.

| Katman | Sorumluluk | Ürün yüzü |
|--------|------------|-----------|
| **Toplama** | Ne gelsin (kanal, Event ID, journal unit…) | Settings → **Event Log → Paket kataloğu** |
| **Parse** | Gelen olaydan hangi alanlar üretilsin | Settings → **Event Log → Parse kuralları** (bu spec) |
| **Alanlar** | Ortak hedef şema (`actor.user`, `custom.*`…) | Settings → **Event Log → Alan kataloğu** |
| **Tespit / rapor** | Normalize alanlara kural ve UI | Alarm pack, Host Analytics, arama |

**İlkeler:**

- Parse kuralları **kod deploy’u olmadan** yönetilebilir (CRUD + Yayınla).
- Motor **generic**; Windows / Linux / FW aynı iskelet, farklı `match` / `extract`.
- Ajan mümkün olduğunca sadece toplar; parse **sunucu ağırlıklı** (Reactor).
- `raw` her zaman saklanır; parse fail → `event.action = unknown`.
- Mevcut C# `ISecEventParser` implementasyonları **builtin seed / fallback**; uzun vadede katalog birincil.
- **MITRE** (`threat.*`) bu fazda yazılmaz; alanlar `sec_events` şemasında rezerv (Faz 2 zenginleştirme).

---

## 2. Kararlar (kilit)

| Konu | Karar |
|------|--------|
| Kural modeli | **B** — ayrı Parse Rules kataloğu; Event Log paketinden bağımsız |
| Bağlantı | `match.sourceProduct` (+ channel / eventId / pattern / when); paket “ne topla”, kural “nasıl çıkar” |
| UI | SIEM Settings → **Event Log** alt sekme `parsers`; sihirbaz: Windows + Linux (manuel «Kural oluştur» kaldırıldı) |
| Alan kataloğu | Core + `custom.<slug>`; kural kaydında auto-register |
| Motor konumu | `MngReactor` normalizer hattı |
| Çakışma | `priority` desc; `onConflict: first_wins` (v1) |

---

## 3. Veri modeli

### 3.1 Mongo

| Koleksiyon | Rol |
|------------|-----|
| `sec_event_parse_rules` | Kural belgeleri (draft + yayınlanan kopya ayrımı uygulama diliminde netleşir; Event Log paketleriyle aynı operasyon dili) |
| `sec_event_parse_catalog` | Meta: `version`, `publishedAt`, `BuiltinSeedRevision`, … |
| `sec_event_custom_fields` | Tenant özel hedef alan tanımları |

**DB:** Tenant DB (`mng_{domain}` / Odak: `mng_odak`) — Event Log katalog DB politikasıyla hizalı tutulur.

### 3.2 Kural belgesi

| Alan | Tip | Açıklama |
|------|-----|----------|
| `id` / `ruleId` | string | Kalıcı kimlik (örn. `windows.logon.4625`) — `parser.id` olarak olaya yazılır |
| `name` | string | UI başlığı |
| `description` | string? | Opsiyonel |
| `enabled` | bool | |
| `priority` | int | Yüksek = önce (varsayılan 100) |
| `builtin` | bool | Seed / ürün paketi |
| `version` | int | Kural gövde sürümü (düzenleme ile artabilir) |
| `match` | object | Ne zaman uygulanır |
| `extract` | array | Alan çıkarma adımları |
| `onConflict` | string | v1: `first_wins` |

### 3.3 `match` (v1)

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| `sourceProduct` | evet | string[] — örn. `windows`, `linux-journal`, `linux-syslog`, `fortigate` |
| `sourceType` | hayır | string[] — `ad` \| `endpoint` \| `firewall` \| `bastion` … |
| `channel` | hayır | string[] — Windows Event Log kanalı |
| `eventIds` | hayır | int[] |
| `when` | hayır | `eq` / `neq` / `in` / `exists` / **`contains`** |
| `messagePatterns` | hayır | `{ "family": "<builtin_id>" }[]` — whitelist family |

**Not:** Agent journal olaylarında `source.product` çoğu zaman `mnglogs-agent`; motor `source.type=linux-journal` ile `linux-journal` / `linux-syslog` kurallarını eşler.

### 3.4 `extract` (v1)

| `type` | Anlam |
|--------|--------|
| `event_data` | Windows EventData anahtarı → hedef |
| `json_path` | Ham JSON / fields yolu → hedef (Linux journal alanları) |
| `regex` | Kaynak metin (varsayılan `message`) + named groups → hedefler |
| `constant` | Sabit değer (örn. `event.action`) |
| `kv` | key=value satırı |

Hedef: core alan **veya** `custom.<slug>` (bare slug normalize edilir).

---

## 4. UI — sihirbazlar

| Sihirbaz | Örnek API | Alanlar adımı |
|----------|-----------|----------------|
| Windows | `GET …/parse-samples/windows` | **Tanımlı Alanlar** (EventData) \| **Custom Regex** |
| Linux | `GET …/parse-samples/linux` | Journal `json_path` \| **Custom Regex**; family **veya** `message contains` zorunlu; paket → `when.package eq` |

Route: `/apps/siem-center/settings?tab=eventlog&section=parsers`

---

## 5. Builtin seed (`SeedRevision` 5)

| Grup | Kural örnekleri |
|------|-----------------|
| Windows logon/lock | 4624, 4625, 4634, 4648, 4672, 4740 |
| Windows hesap/grup/AD | 4720, 4722, 4726, 4728, 4732, 4738, 5136, 5137, 5139 |
| RDP | 21, 23, 24, 25 |
| Linux | sshd fail/ok, sudo deny/command |

**Bilinçli dışı (katalog):** Application free-text (65002) — şablon/sihirbaz ile özel kural. **Firewall** — hâlâ C# `FirewallVendorParser` (sonraki dilim).

Revision bump’ta seed’de olmayan **builtin** satırlar silinir; `Enabled` korunarak mevcut builtin içerik yenilenir.

---

## 6. Uygulama dilimleri

| Dilim | Çıktı | Kabul |
|-------|--------|--------|
| **0** | Spec | ✅ |
| **1** | Mongo + manage/publish API + validate | ✅ |
| **2** | Settings parse + alan UI + Windows/Linux sihirbaz | ✅ |
| **3** | Katalog motor + cache | ✅ |
| **4** | Builtin seed + sync | ✅ SeedRevision 5 |
| **5** | Docs / current_status | ✅ |

### Kod konumları (özet)

| Parça | Path |
|-------|------|
| Seed / validator / field catalog | `MngReactor/.../Services/SecEvents/` |
| Motor | `SecEventCatalogParseEngine.cs` |
| Sample API | `…/parse-samples/windows`, `…/parse-samples/linux` |
| UI | `AcSiemSettingsParseRulesPanel`, `AcSiemWindowsParseWizardDialog`, `AcSiemLinuxParseWizardDialog`, `AcSiemSettingsFieldCatalogPanel` |

---

## 7. Bilinçli kapsam dışı / sıradaki

- Firewall / bastion katalog seed + family whitelist  
- MITRE / `threat.*` yazımı  
- Serbest grok stüdyosu  
- Ajan içi parse  
- Content pack import/export  

---

## 8. Referanslar

- Parser planı: [SIEM_PARSER_PLAN.md](../monitoring/SIEM_PARSER_PLAN.md)  
- Event Log paketleri: [POLICY_EVENTLOG_PACKAGES.md](./mnglogs/POLICY_EVENTLOG_PACKAGES.md)  
- Durum: [current_status.md](./current_status.md)
