# Spec — `op_rules` aksiyonu: `createDatasetRows`

**Durum:** Implementasyon (CDR-1…5) — MO executor + transition pre-persist + UI editör + zimmet seed  
**Son güncelleme:** 11 Temmuz 2026  
**Karar oturumu:** GIR → demirbaş (AF-1) tartışması; generic A yolu onaylandı  
**İlgili:** [RULE_ENGINE.md](./RULE_ENGINE.md) · [WORKSPACE_AUTOMATION_PLANNING.md](./WORKSPACE_AUTOMATION_PLANNING.md) · [PIPELINES.md](./PIPELINES.md) · UI: [OC_UI_CREATE_DATASET_ROWS.md](../ui/OC_UI_CREATE_DATASET_ROWS.md) · Zimmet: [PLAN.md §8 AF-1](../../zimmet/PLAN.md)

---

## 1. Amaç

`op_rules` **automation** kurallarına generic bir side-effect aksiyonu eklemek:

> Kaynak work item (WI) bağlamından **herhangi bir DG dataset’ine** 1…N satır oluştur.

İlk referans senaryo: Zimmet **GIR** kapanınca `zimmet_demirbaslar` üretimi (**AF-1**). Domain mantığı kodda değil; **kural + mapping seed/config** ile tanımlanır.

---

## 2. Kilitlenen kararlar

| Konu | Karar |
|------|--------|
| Yer | `op_rules` automation (`actions[]`) — zimmet’e özel MO servisi yok |
| Aksiyon adı | `createDatasetRows` |
| Mapping dili | Otomatik işlerle uyumlu: `field` / `static` / `token` / `item` / **`sequence`** |
| Cardinality | `single` \| `count` \| `expand` |
| Idempotency | `one_per_source` (varsayılan öneri) |
| Hata politikası | `onError: failTransition` (geçiş geri alınır / 400 — stok bütünlüğü) |
| Seri ↔ miktar | Önce **validation** kuralı; aksiyon tutarlı veri varsayar |
| UI | Workspace Tanımları → **Kurallar** içinde aksiyon tanımlama ekranı (zorunlu teslimat) |
| MngWorkflow | Bu aksiyon **MngWorkflow designer’da değil**; sınır §3 |
| İleri aile | `updateDatasetRows` / `upsertDatasetRows` (OC-1 / OC-2) — aynı mapping dili |

---

## 3. Sınırlar (üç katman)

```text
op_rules automation
  → aynı istekte yan etki: mail, bildirim, startWorkflow, createDatasetRows  ← BU SPEC

Otomatik işler (op_workspace_automations)
  → yeni WI spawn + fieldMappings (WI hedefi)

MngWorkflow
  → çok adım, async, büyük N, modüller arası orkestrasyon
```

| Bu aksiyon | Değil |
|------------|--------|
| DG dataset satır(lar)ı oluşturur | Yeni OC work item açmaz |
| Senkron, transition/create pipeline içinde | Async kuyruk / Reactor consumer (MVP dışı) |
| Metadata ile yapılandırılır | Hardcoded `if (type == GIR)` |

**Büyük N:** Inline `op_rules` küçük/orta N için. Çok büyük batch → ileride MngWorkflow veya async worker (bu spec’in MVP kapsamı dışı).

---

## 4. Pipeline konumu

Mevcut sıra ([PIPELINES.md](./PIPELINES.md) / [RULE_ENGINE.md](./RULE_ENGINE.md)):

```text
pre-validation → state mutasyonu → default → post-validation
  → inline automation side-effects   ← createDatasetRows burada
```

`onError: failTransition` ise:

1. DG create başarısız veya idempotency/cardinality hatası → **tüm transition komutu fail** (kullanıcıya validation benzeri hata).
2. Başarılı create sonrası WI activity (öneri): özet + oluşturulan satır sayısı / id listesi (kısaltılmış).

> Not: Gerçek “transaction rollback” DG + MO arasında dağıtık olabilir. MVP’de pratik: create **geçiş persist’ten önce** veya fail durumunda telafi politikası netleştirilir (implementasyon notu §11). Tercih: mümkünse **önce satır üret, sonra WI state commit**; üretilemezse state değişmesin.

---

## 5. Aksiyon şeması

```json
{
  "type": "createDatasetRows",
  "dataset": "zimmet_demirbaslar",
  "cardinality": {
    "mode": "count",
    "countFrom": "fields.miktar"
  },
  "idempotency": {
    "mode": "one_per_source",
    "lookupField": "girisRef",
    "lookupFrom": "key"
  },
  "onError": "failTransition",
  "fieldMappings": [
    { "target": "katalogUrunId", "source": "field", "path": "fields.katalogUrunId" },
    { "target": "depoId", "source": "field", "path": "fields.depoId" },
    { "target": "lokasyonId", "source": "field", "path": "fields.lokasyonId" },
    { "target": "durum", "source": "static", "value": "depoda" },
    { "target": "girisTarihi", "source": "field", "path": "fields.girisTarihi" },
    { "target": "girisRef", "source": "field", "path": "key" },
    { "target": "seriNo", "source": "sequence", "template": "{{source.key}}-{000}", "startFrom": 1 }
  ]
}
```

> Üretici seri listesi için alternatif: `cardinality.mode=expand` + `seriNo` ← `item` — bkz. `zimmet-rule-gir-create-demirbas-expand.json`.

### 5.1 Alanlar

| Alan | Zorunlu | Açıklama |
|------|---------|----------|
| `type` | evet | `createDatasetRows` |
| `dataset` | evet | DG dataset adı |
| `cardinality` | evet | §6 |
| `idempotency` | evet | §7 |
| `onError` | hayır | Varsayılan: `failTransition`. Alternatif: `continue` (activity + log; stok için önerilmez) |
| `fieldMappings` | evet | §8 — satır şablonu |

---

## 6. Cardinality

| `mode` | Davranış |
|--------|----------|
| `single` | Tek satır; `countFrom` / `itemsFrom` yok sayılır |
| `count` | `countFrom` path’inden sayı (N); N kez aynı şablon (item yok) |
| `expand` | `itemsFrom` dizi ise her eleman bir satır; `itemAs` ile mapping’te `item` kaynağı. `countFrom` varsa uzunluk **eşit olmalı** (değilse fail) |

**Kurallar:**

- N ≤ 0 veya path çözülemez → `failTransition` (veya `onError`).
- `expand` + boş dizi + `countFrom` > 0 → fail (validation’ın yakalaması beklenir; aksiyon da korur).
- Üst sınır (ör. `maxRows: 100`) opsiyonel güvenlik; aşımda fail. Varsayılan öneri: **100** (config/constant).

---

## 7. Idempotency

| `mode` | Davranış |
|--------|----------|
| `none` | Her tetiklemede yeniden create (genelde istenmez) |
| `one_per_source` | Hedef dataset’te `lookupField == kaynak değeri` olan kayıt varsa **yeni satır üretme**; geçiş **başarılı** sayılır (no-op) |

| Alan | Açıklama |
|------|----------|
| `lookupField` | Hedef dataset alanı (ör. `girisRef`) |
| `lookupFrom` | Kaynak WI path veya özel: `id`, `key`, veya `fields.*` |

**Zimmet:** Mevcut alan `girisRef` (text). İleride relation `kaynakWorkItemId` eklenebilir; mapping/idempotency güncellenir.

**Kısmi başarı:** İlk çalıştırmada 3/5 yazıldı, istek fail oldu → ikinci denemede `one_per_source` tümünü atlar ama eksik satırlar kalır. MVP notu: ya **atomik batch** (hepsi veya hiçbiri) ya da lookup + beklenen N karşılaştırması (`expectedCountFrom`). Öneri MVP: DG’ye mümkünse tek batch / transaction; değilse `expectedCountFrom: fields.miktar` ile mevcut sayı < beklenen ise tamamla (faz 1.1). **Faz 1:** strict — mevcut ≥ 1 ise no-op; operatör manuel düzeltir. Açık nokta §12.

---

## 8. Field mapping

Otomatik işler ([WORKSPACE_AUTOMATION_PLANNING.md §4.4](./WORKSPACE_AUTOMATION_PLANNING.md)) ile aynı `source` türleri + `item`:

| source | Alanlar | Anlam |
|--------|---------|--------|
| `field` | `path` | WI görünümünden değer (`fields.x`, `key`, `id`, `assignee`, …) |
| `static` | `value` | Sabit |
| `token` | `template` | `{{source.key}}`, `{{source.fields.miktar}}`, … |
| `item` | `path` | Cardinality `expand` satır bağlamı (`itemAs` kökü; ör. `serial`) |
| `sequence` | `template`, `startFrom?`, `startFromPath?` | Satır indeksinden artımlı değer (§8.1) |
| `relation` | (opsiyonel faz) | Şimdilik gerekmez |

- Hedef alan adı = DG field `name`.
- Incremental alanlar (`demirbasNo`) mapping’te **olmamalı** — DG üretir.
- Unique çakışma (ör. `seriNo`) → create fail → `failTransition`.

### 8.1 `sequence` — artımlı şablon

Toplu alımda (100+ adet) elle seri listesi yerine `count` + `sequence` kullanılır.

```json
{ "target": "seriNo", "source": "sequence", "template": "{{source.key}}-{000}", "startFrom": 1 }
```

| Alan | Zorunlu | Anlam |
|------|---------|--------|
| `template` | evet | Önce `{{…}}` token’ları çözülür; sonra `{0}` / `{00}` / `{000}` satır numarası ile değişir |
| `startFrom` | hayır | Başlangıç (varsayılan `1`). Satır değeri = `startFrom + rowIndex` (0-based) |
| `startFromPath` | hayır | Varsa `startFrom` yerine WI path’ten okunur (ör. `fields.seriBaslangic`) |

**Örnekler** (`miktar=3`, `key=GIR-0005`, `startFrom=1`):

| template | Üretilen |
|----------|----------|
| `SERI-{00}` | `SERI-01`, `SERI-02`, `SERI-03` |
| `{{source.key}}-{000}` | `GIR-0005-001`, `GIR-0005-002`, `GIR-0005-003` |

`{0+}` yoksa numara şablonun sonuna eklenir. `count` / `expand` / `single` tüm modlarda `rowIndex` geçerlidir.

**Ne zaman `sequence`, ne zaman `item`?**

| Senaryo | Cardinality | `seriNo` kaynağı |
|---------|-------------|------------------|
| Üretici / gerçek seri listesi var | `expand` + `itemsFrom` | `item` |
| İç etiket / toplu alım (liste yok) | `count` + `countFrom` | `sequence` |

---

## 9. Validation eşliği (ayrı kural)

Aksiyon varsayar; **ayrı `op_rules` validation** önerilir (aynı transition scope):

Örnek (seri takip):

- `fields.seriNoListesi` notEmpty (veya grup kuralına göre)
- dizi uzunluğu `eq` `fields.miktar` (condition dili yeterli değilse faz 1.1 expression; MVP’de aksiyon içi cardinality check yeterli olabilir)

`trackBySerial` ürün grubundan okuma **MVP dışı** (ek DG lookup); ilk etapta GIR form disiplini + cardinality check.

---

## 10. Referans senaryo — Zimmet AF-1

| | |
|--|--|
| Workspace | Zimmet Depo (`GIR`) |
| Tetik | `WorkItemTransition` → hedef **Kapalı** (veya Stoklandı→Kapalı son geçiş; seed’de netleştirilir) |
| Kural tipi | automation + `createDatasetRows` |
| Dataset | `zimmet_demirbaslar` |
| N | `miktar` (`count`) — varsayılan seed: `sequence` ile `{{source.key}}-{000}` |
| Alternatif | Gerçek üretici serileri → `expand` + `seriNoListesi` + `item` (ayrı kural / JSON örneği) |
| Sonuç | N kayıt, `durum=depoda`, `girisRef` = GIR key |

**OC-1 / OC-2** (sonra): aynı ailede `updateDatasetRows` — ZIM kapanınca `durum` / `zimmetliPersonelId` güncelle.

Seed: `docs/odak/zimmet/seed/` altına kural JSON + setup script adımı (implementasyon fazında).

---

## 11. Implementasyon fazları (geliştirme sırası)

| Faz | İş | DoD |
|-----|-----|-----|
| **CDR-0** | Bu spec + UI spec onayı | Dokümanlar merge |
| **CDR-1** | MO: action parse + mapping resolver + DG create | Unit test; mock DG |
| **CDR-2** | Pipeline: side-effect + `failTransition` | Transition E2E fail/success |
| **CDR-3** | Idempotency `one_per_source` | İkinci transition no-op |
| **CDR-4** | **UI tanımlama ekranı** — [OC_UI_CREATE_DATASET_ROWS.md](../ui/OC_UI_CREATE_DATASET_ROWS.md) | Admin kuralı UI’dan kaydedip çalıştırır |
| **CDR-5** | Zimmet seed AF-1 + Odak test E2E | GIR kapat → N demirbaş |
| **CDR-6** | (Sonra) `updateDatasetRows` spec + OC-1/2 | Ayrı kısa spec |

**Kod dokunuşları (beklenen):**

- `RuleEngineService` / `RuleActionParser` — yeni action tipi
- `WorkItemCommandService.ExecuteAutomationSideEffectsAsync` — executor
- DG client: create (batch tercihen)
- UI: `OcRuleEffectPanel` / automation action editor (CDR-4)

---

## 12. Açık noktalar (geliştirmede netleşir)

1. Transition commit sırası vs DG create atomikliği (kısmi yazım).
2. `seriNo` unique + boş seri (miktar takipli ürün) — sparse unique / null politikası.
3. `expectedCountFrom` ile tamamlayıcı idempotency (faz 1.1?).
4. Activity’de oluşturulan `__dataId` listesi boyutu.
5. Dataset şema cache / alan adı doğrulama (UI’da autocomplete için DG schema).

---

## 13. Bilinçli olarak kapsam dışı

- Webhook / script action
- Reactor event consumer ile demirbaş üretimi
- MngWorkflow adımı olarak `createDatasetRows` (gerekirse ayrı entegrasyon)
- Ürün grubundan runtime `trackBySerial` okuma (ilk seed’de form + validation)

---

*Onay: 11 Temmuz 2026 — A yolu, failTransition, generic mapping, UI tanımlama ekranı zorunlu.*
