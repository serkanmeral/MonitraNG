# Mng.Ui — Workspace kuralları (`op_rules`) Faz 1 tamamlama

**Son güncelleme:** 28 Mayıs 2026  
**Durum:** R-UI ✅ · **R-Core** (explorer + dialog + edit) ✅ · **R-UX** (admin kullanıcı deneyimi) ✅ — R-Plus backlog  
**MO referans:** [RULE_ENGINE.md](../mngoperations/RULE_ENGINE.md) · **Plan:** [OC_UI_ADMIN_FAZ1_PLAN.md](./OC_UI_ADMIN_FAZ1_PLAN.md)

---

## 1. «Eksiksiz» tanımı

**Eksiksiz (Faz 1 admin)** = UI, MO runtime’ın Faz 1’de desteklediği `op_rules` alanlarını **yönetebilir**; create/sil-only ve tek `empty` koşulu döneminden çıkılır.

**Eksiksiz değil (Faz 1.1+):** `validFrom`/`validTo`, webhook/script, karmaşık OR/ifade ağaçları, kural test/simülasyon sandbox.

---

## 2. Mevcut vs hedef

| Konu | UI bugün | MO runtime | Hedef UI |
|------|----------|------------|----------|
| CRUD | Create + sil | — | **+ Edit** (`ocUpdateRule`) |
| `typeId` scope | Payload’da var, formda **gizli** | ✅ | Select (workspace tipleri) |
| `boardId` scope | ❌ | ✅ | Opsiyonel select |
| `stateId` / `fromStateId` / `toStateId` | ❌ | ✅ | State select (enabled) |
| `transitionKey` | Metin, yalnızca transition trigger | ✅ | Akış geçiş dropdown |
| `isActive` | Her zaman `true` create | ✅ | Toggle + liste chip |
| `priority` | Sabit 100 | ✅ | Number input |
| `description` | ❌ | persist | Textarea |
| `applyMode` | Sabit `pre` (validation) | ✅ pre/post | Select |
| Koşul | Tek alan + `empty` | ✅ eq/ne/empty/notEmpty/in/gt/lt, AND | Koşul listesi editörü |
| Default actions | setField, setAssignee | + setAssignmentGroups | Tip-aware form |
| Automation | ❌ | ✅ side-effects | Basit action picker (R6) |
| Liste | Düz tablo | — | Filtre: tip, trigger, aktif |
| Özet | Kısmi metin | — | Katalog adları + koşul özeti |

---

## 3. Uygulama fazları

### R-Core (Epic A1 — öncelik P0, bugün)

| ID | İş | Dosya / not |
|----|-----|-------------|
| **R1** | Edit dialog; `ocUpdateRule(dataset, id, body)` | `operationCoreService.ts`, panel |
| **R2** | Scope alanları form + persist | `OcWorkspaceFormRulesPanel.vue`, yeni `OcWorkspaceRuleScopeFields.vue` (öneri) |
| **R3** | Koşul editörü: AND + leaf (field, cmp, value) | `ocFormRuleSummary.ts` → build/parse helpers |
| **R4** | `isActive`, `priority`, `description`, `applyMode` | Liste + dialog |
| **R5** | Özet: tip/board/state adları, `formatOcRuleConditionSummary` genişlet | `ocGetDatasetRecordTitle` |

**DoD R-Core:** Demo workspace’te validation + default kuralı oluştur → düzenle → pasifleştir → sil; scope + koşul MO smoke ile uyumlu.

### R-Plus (Epic A1 — P2, zaman kalırsa / yarın)

| ID | İş |
|----|-----|
| **R6** | `ruleType: automation` + actions: `addWatcher`, `createNotification`, `sendEmailViaMngNotifiers`, `createActivity` |
| **R6-CDR** | `createDatasetRows` aksiyon editörü — [OC_UI_CREATE_DATASET_ROWS.md](./OC_UI_CREATE_DATASET_ROWS.md) · MO: [CREATE_DATASET_ROWS_ACTION_SPEC.md](../mngoperations/CREATE_DATASET_ROWS_ACTION_SPEC.md) |
| **R7** | Liste filtreleri + sıralama (priority, ad) |
| **R8** | Form «Alan politikaları» → alan bazlı ilgili kurallar chip (F7 backlog ile birleşik) |

### R-Future (Faz 1.1)

| ID | İş |
|----|-----|
| **R9** | `validFrom` / `validTo` |
| **R10** | OR grupları, nested conditions |
| **R11** | Kural dry-run / test (MO endpoint gerekir) |

---

## 4. UI yerleşim (Kurallar sekmesi — güncel)

```text
[Kurallar sekmesi — OcWorkspaceRulesExplorer]
├─ Hero: başlık + alt metin (olay motoru; Politikalar’dan farkı)
├─ «Nasıl çalışır?» — 3 kart: tetikleyici → koşul → etki
├─ İstatistik chip’leri: toplam kural, aktif, tetikleyici dağılımı
├─ Filtre şeridi: tetikleyici · iş tipi · yalnızca aktif
├─ Tablo: ad · tip · scope özeti · tetik · koşul · aksiyon · aktif · işlem
├─ Boş durum: yönlendirici metin + «Kural ekle» CTA
└─ Dialog (create/edit — OcWorkspaceRuleDialog, ~960px):
     ├─ Sol: 4 numaralı adım (Genel → Scope → Koşullar → Etki)
     └─ Sağ: canlı önizleme kartı (scope / when / then özeti)
```

**Politikalar ile ayrım (ürün):** Kurallar kaydı **durdurabilir** (validation) veya olay anında **otomasyon** çalıştırır. Politikalar yalnızca form alanı davranışını (görünürlük, salt okunur, varsayılan) etkiler — [OC_UI_WORKSPACE_POLICIES.md §0.5](./OC_UI_WORKSPACE_POLICIES.md).

**Eski panel:** `OcWorkspaceFormRulesPanel.vue` repoda kalır; Kurallar sekmesi artık yalnızca `OcWorkspaceRulesExplorer` kullanır.

---

## 5. Persist örnekleri (MO uyumlu)

**Validation — geçişte açıklama zorunlu:**

```json
{
  "name": "Çözüm notu zorunlu",
  "workspaceId": "<ws>",
  "typeId": "<typeId>",
  "ruleType": "validation",
  "trigger": "WorkItemTransition",
  "transitionKey": "resolve",
  "applyMode": "pre",
  "conditions": {
    "op": "and",
    "items": [
      { "field": "description", "cmp": "empty" }
    ]
  },
  "errorMessage": "Çözüm açıklaması girin.",
  "isActive": true,
  "priority": 100
}
```

**Default — oluşturmada öncelik:**

```json
{
  "name": "Olay → yüksek öncelik",
  "workspaceId": "<ws>",
  "typeId": "<incidentTypeId>",
  "ruleType": "default",
  "trigger": "WorkItemCreated",
  "conditions": {
    "op": "and",
    "items": [
      { "field": "priorityId", "cmp": "empty" }
    ]
  },
  "actions": [{ "type": "setField", "field": "priorityId", "value": "<priorityId>" }],
  "isActive": true,
  "priority": 50
}
```

---

## 6. Test checklist

1. Create validation → MO transition reject → UI form/profil mesajı (operasyonel UI sonrası E2E).
2. Edit scope: kural yalnızca seçili `typeId`’de çalışsın.
3. `isActive=false` → MO eşleşmez.
4. AND koşul: iki leaf — biri fail → kural tetiklenmez.
5. `setAssignee` + person picker — özet kişi adı.
6. Sayfa yenile → persist DG’den geri gelir.

---

## 7. Kod indeksi

| Ne | Dosya |
|----|--------|
| Koşul (paylaşılan) | `utils/ocConditionClauses.ts`, `OcConditionClauseList.vue` |
| Kural modeli | `utils/ocWorkspaceRules.ts` |
| Explorer | `OcWorkspaceRulesExplorer.vue` |
| Dialog | `OcWorkspaceRuleDialog.vue`, `OcRuleScopePanel.vue`, `OcRuleEffectPanel.vue` |
| Sekme | `OcWorkspaceDefinitionsRulesTab.vue` |
| Servis | `operationCoreService.ts` — `ocListRulesForWorkspace`, `ocCreateRule`, **`ocUpdateRule`** |
| Politika (delegasyon) | `OcWorkspacePolicyConditionList.vue` → `OcConditionClauseList` |

---

## 8. Admin UX yenileme (28 Mayıs 2026 — R-UX)

**Amaç:** Workspace admin’de Kurallar ekranını teknik tablo yerine «ne zaman / ne olur» odaklı, Politikalar ile tutarlı bir deneyime taşımak.

| Bileşen | UX öğeleri |
|---------|------------|
| `OcWorkspaceRulesExplorer.vue` | Hero + alt metin; 3 adımlı «Nasıl çalışır?»; istatistik chip’leri; tetikleyici/tip/aktif filtreleri; boş durum; tablo tooltip’leri; Türkçe tetikleyici etiketleri |
| `OcWorkspaceRuleDialog.vue` | Geniş dialog; 4 adımlı form; sağda canlı önizleme (`formatRuleDraft*Summary`); dostane tetikleyici ve kural türü metinleri |
| `OcWorkspaceDefinitionsRulesTab.vue` | Yalnızca explorer kabuğu (katalog alanları yükler) |
| i18n | `operationCore.workspaceDefinitions.rules.*` — TR/EN kullanıcı odaklı metinler |

**Paylaşılan altyapı (Politikalar ile):**

| Ne | Dosya |
|----|--------|
| Koşul modeli / parse | `utils/ocConditionClauses.ts` |
| Koşul satır editörü | `OcConditionClauseList.vue` |
| Kural özet helper’ları | `utils/ocWorkspaceRules.ts` — `formatRuleScopeSummary`, `formatRuleWhenSummary`, `formatRuleThenSummary`, draft özetleri |

**DoD R-UX:** Ürün sahibi onayı — admin «Kurallar vs Politikalar» ayrımını ekrandan anlayabiliyor; kural ekle/düzenle akışı adım adım takip edilebiliyor.

**Backlog (R-Plus / UX):** Liste sıralama; automation action picker metinleri; isteğe bağlı `?tab=rules` çapraz link Politikalar bandından.

---

*Üst plan: [OC_UI_ADMIN_FAZ1_PLAN.md](./OC_UI_ADMIN_FAZ1_PLAN.md). Handoff: [DEVAM.md](../mngoperations/DEVAM.md).*
