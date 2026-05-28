# Mng.Ui — Form alan politikaları (tek el, Faz 1)

**Son güncelleme:** 26 Mayıs 2026  
**Durum:** **Faz 1 v1 uygulandı** — aşağıdaki [§10 backlog](#10-form-alan-politikaları--kalan-işler-backlog) tamamlanınca «form tarafı kapalı» sayılır.  
**Workspace (formdan bağımsız):** [OC_UI_WORKSPACE_POLICIES.md](./OC_UI_WORKSPACE_POLICIES.md) — alan politikaları (`fieldPolicies`) + Değerler sekmesi. **`op_rules`** → **Kurallar** sekmesi (R-UI ✅).  
**İlişkili:** [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md) · [operationcore_phase1.md §10–11](../operationcore_phase1.md)

---

## 1. Amaç

Workspace **Forms** editöründe layout (yerleşim) olgunlaştı. Sırada:

- Aynı form için **davranış, varsayılan değer ve kurallar** tek sayfada yönetilsin.
- Admin bir alan için **Davranışlar / Varsayılan değerler / Kurallar** sekmeleri arasında gezinmesin.
- **Layout** ile **politika** kavramsal olarak ayrı kalsın (farklı sorular, farklı metadata).

---

## 2. Kavram ayrımı

**Form (bu belge)** ≠ **Workspace** ([OC_UI_WORKSPACE_POLICIES.md](./OC_UI_WORKSPACE_POLICIES.md)): koşullu kurallar ve `op_rules` uzun vadede workspace **Politikalar** sekmesinde; formda yalnızca şablona özgü statik tablo kalır.

```text
┌─────────────────────────┐       ┌────────────────────────────────┐
│  YERLEŞİM (Layout)      │       │  FORM ALAN POLİTİKASI (bu belge)│
│  op_forms.layout        │       │  fieldBehaviors, defaultValues  │
├─────────────────────────┤       │  (geçici: op_rules alt bölüm) │
│  Hangi alanlar var?     │       ├────────────────────────────────┤
│  Hangi bölümde, sıra?   │       │  Nasıl davranır?              │
│  fieldCols, genişlik    │       │  Varsayılan değer?            │
└─────────────────────────┘       │  Hangi kurallar bu alanı etkiler?│
         │                        └────────────────────────────────┘
         └─ layout’taki field key listesi → politika satırlarının kaynağı
```

| Soru | Katman | Metadata |
|------|--------|----------|
| Formda bu alan var mı? | Layout | `layout.sections[].fields` |
| Görünür / salt okunur / zorunlu / hassas? | Statik politika | `op_forms.fieldBehaviors` |
| Create açılış değeri? | Varsayılan | `op_forms.defaultValues` |
| Geçişte / olayda ne olur? | Kurallar | `op_rules`, transition `requiredFields` |
| Runtime’da gerçek sonuç? | MO merge | `IFieldBehaviorResolver` + rules |

**UI merge yapmaz** — kayıt DG’ye gider; «Yeni iş» MO birleşik `fieldBehaviors` döner.

---

## 3. Form editör — hedef sekme yapısı

| Sekme | İçerik | Durum |
|-------|--------|--------|
| **Genel** | Ad, açıklama, `formHeading` / `formIntro`, `dialogMaxWidth`, varsayılan tip/akış, `isDefault` | Mevcut |
| **Yerleşim** | Bölümler, alan sırası, `fieldCols` / `sectionCols` | Mevcut — ayrı kalır |
| **Alan politikaları** | Tablo + varsayılan + geçici kurallar/özet | **✅ v1** (ince ayar §10) |
| ~~Davranışlar~~ | — | Politika sekmesine taşınır |
| ~~Varsayılan değerler~~ | — | Politika sekmesine taşınır |

**Profil (`op_profiles`):** Aynı UX şablonu Faz 1 sonrası (Sprint profil runtime sonrası).

---

## 4. «Alan politikaları» ekranı — bileşenler

### 4.1 Üst şerit

- Form adı, workspace
- Özet: «Yerleşimde N alan»
- **Önizleme** (mevcut `OcFormPreviewDialog`) — layout + kayıtlı politika (taslak)
- Kaydet → `fieldBehaviors` + `defaultValues`

Bilgi kutusu (kısa):

> Burada kaydettiğiniz değerler forma özgü statik politikadır. Çalışma zamanında yetki, board ve `op_rules` ile birleştirilir (MO). Önizleme taslaktır; tam MO merge önizlemesi Faz 2.

### 4.2 Ana tablo (layout alanlarıyla senkron)

Her satır = `layout.sections` içinde geçen bir `fieldKey` (sıra layout ile uyumlu).

| Sütun | Kaynak | Faz 1 v1 widget |
|-------|--------|-----------------|
| Alan (etiket + key) | `resolveOcFieldEditorLabel` | Salt okunur |
| Görünür | `fieldBehaviors.visible` | Checkbox |
| Salt okunur | `fieldBehaviors.readonly` | Checkbox |
| Zorunlu | `fieldBehaviors.required` | Checkbox (+ create’te yıldız — [#3](../mngoperations/DEVAM.md)) |
| Hassas | `fieldBehaviors.masked` | Checkbox |
| Varsayılan | `defaultValues[key]` | Tip’e göre (`OcDynamicFormField` widget veya kısa input) |
| Kurallar | `op_rules` özeti | Faz 1 v1: «N kural» link |

Layout’ta yeni alan eklenince tabloya satır eklenir (varsayılan: visible=true, diğerleri false). Layout’tan çıkarılınca satır kalkar veya gri «layout’ta yok» (politika verisi isteğe bağlı temizlenir).

### 4.3 Alt bölüm — «Workspace kuralları» (geçici konum)

> **Taşınacak:** `op_rules` UI [Workspace Politikaları](./OC_UI_WORKSPACE_POLICIES.md) sekmesine alınacak; formda yalnızca kısa link kalabilir.

Filtre: `workspaceId`.

| Sütun | Açıklama |
|-------|----------|
| Ad | Kural adı |
| Tetikleyici | `WorkItemCreated`, `WorkItemTransition`, … |
| Tip | `validation` / `default` |
| Özet | Koşul + action kısa metin |
| | Düzenle / sil (Faz 1 basit) |

**Faz 1 v1 — basit kural oluşturma:**

- Validation: trigger + `transitionKey` + tek condition + `errorMessage`
- Default: `setAssignee` veya `setField` (tek action)

Koşullu visible/readonly matrisi → **Faz 2** (`visibilityRules` + condition builder).

### 4.4 Geçiş zorunlulukları (özet)

Salt okunur özet veya link: «Akışlar → X geçişinde zorunlu: …»  
Asıl düzenleme **Akışlar** sekmesinde kalır; bu sayfa çapraz görünürlük sağlar.

---

## 5. Uygulama adımları (onaylı sıra)

| Adım | İş | Kod / belge |
|------|-----|-------------|
| **3a** | Zorunlu alan UI (yıldız + submit özeti) | `OcDynamicForm`, `OcDynamicFormField` |
| **3b** | «Alan politikaları» sekmesi — birleşik tablo | `OcWorkspaceDefinitionsFormsTab.vue`, yeni alt bileşen önerilir: `OcWorkspaceFormFieldPolicyEditor.vue` |
| **3c** | Layout ↔ tablo senkron | `form.sections` watch |
| **3d** | Kurallar alt bölümü — liste + basit CRUD | `op_rules` DG + `operationCoreService` |
| **3e** | (Opsiyonel) Geçiş requiredFields özeti | Akışlar sekmesine link |
| **4+** | Board / profil runtime | [OC_UI_PHASE1_PLAN.md](./OC_UI_PHASE1_PLAN.md) S2–S4 |

Board/profil runtime, form politika sayfasını **bloklamaz**; paralel gidebilir.

---

## 6. Faz 1 v1 kapsam sınırı

| Dahil | Hariç (Faz 2) |
|-------|----------------|
| Statik visible / readonly / required / masked | Koşullu visible/readonly (state, rol) |
| `defaultValues` hücresi | `visibilityRules` editörü |
| Layout alan listesiyle senkron satırlar | Profil politika sekmesi (şablon sonra) |
| `op_rules` liste + basit create | Tam condition builder |
| | Giriş format maskesi (telefon); `masked` = hassas veri |
| | MO runtime vs taslak karşılaştırma (DEVAM #8) |

---

## 7. Önerilen dosyalar (uygulama)

| Dosya | Rol |
|-------|-----|
| `OcWorkspaceDefinitionsFormsTab.vue` | Sekme yapısı: Genel \| Yerleşim \| Alan politikaları |
| `OcWorkspaceFormFieldPolicyEditor.vue` | **Yeni** — tablo + kurallar bölümü |
| `OcWorkspaceFormLayoutEditor.vue` | Değişmez (yalnızca layout) |
| `operationCoreService.ts` | `op_rules` CRUD (yoksa ekle) |
| `utils/ocFieldDefinitions.ts` | `OC_FORM_LAYOUT_CORE_FIELD_KEYS`, `resolveOcCoreFieldCardinality` |

---

## 8. Alan davranışı — plan özeti (referans)

Kullanıcı modeli ile [operationcore_phase1.md](../operationcore_phase1.md) uyumu:

| Kavram | Faz 1 tek el | Not |
|--------|--------------|-----|
| Statik visible/readonly/required | Tablo | `fieldBehaviors` |
| Varsayılan (boş/create) | Tablo — varsayılan sütun | `defaultValues` |
| Geçişte atama değişimi | Kurallar — `setAssignee` | Default rule, ayrı başlık |
| Validation (işlemi durdur) | Kurallar — validation | `op_rules` |
| Koşullu visible (state+rol) | Faz 2 | `visibilityRules` |
| Format maskesi (telefon) | Faz 2 | `masked` ≠ format |

---

## 9. Test checklist (politika sekmesi)

1. Yerleşime `watchers` ekle → Politika sekmesinde satır; varsayılan widget (çoklu alan metin/select).
2. `assignee` zorunlu → **Önizleme**’de yıldız (kaydetmeden).
3. Varsayılan `typeId` → önizleme / kayıtlı form create’te dolu.
4. Layout’tan alan çıkar → politika satırı kalkar (`syncBehaviorsFromSections`).
5. (Backlog sonrası) `op_rules` workspace sekmesinde CRUD.

---

## 10. Form alan politikaları — kalan işler (backlog)

**Sıra:** Bu tablo bitmeden / bilinçli ertelenmeden [Workspace politikaları](./OC_UI_WORKSPACE_POLICIES.md) koduna geçilmez.

| # | Eksik | Öncelik | Not / hedef |
|---|--------|---------|-------------|
| ~~F1~~ | ~~`op_rules` formdan kaldır → workspace hub~~ | ✅ | Politikalar altında geçici; **R-UI:** ayrı sekme planlandı |
| ~~F2~~ | ~~Varsayılan: board select + person autocomplete~~ | ✅ | `OcFormPolicyDefaultValueInput` |
| ~~F3~~ | ~~Varsayılan: relation select~~ | ✅ | `ocListDataset` |
| ~~F4~~ | ~~Kural: `setAssignee`~~ | ✅ | Politikalar sekmesi |
| F5 | Kural düzenleme (edit dialog) | Düşük | Yalnızca create/sil var |
| F6 | Akışlar sekmesi: geçiş `requiredFields` editörü | Orta | Özet salt okunur; düzenleme yok |
| F7 | Tabloda «Kurallar» sütunu (alan başına `op_rules` özeti) | Düşük | İsteğe bağlı; hub sonrası |
| ~~F8~~ | ~~`visible=false` satır vurgusu~~ | ✅ | Gri satır |
| F9 | MO runtime vs taslak karşılaştırma toggle | Opsiyonel | DEVAM #8 |
| — | Koşullu visible/readonly (rol, state) | **Workspace** | Formda yapılmaz → [OC_UI_WORKSPACE_POLICIES.md](./OC_UI_WORKSPACE_POLICIES.md) |

### Uygulandı (v1)

| Madde | Kod |
|-------|-----|
| Birleşik sekme + layout senkron tablo | `OcWorkspaceFormFieldPolicyEditor.vue` |
| Tip bazlı varsayılan (type/priority/state/bool/number/date) | `OcFormPolicyDefaultValueInput.vue` |
| Zorunlu yıldız + submit özeti | `ocFormValidation.ts`, `OcDynamicFormField.vue` |
| Geçiş `requiredFields` özeti + Akışlar linki | `OcWorkspaceFormTransitionRequirements.vue` |
| `op_rules` liste / basit ekle / sil (geçici) | `OcWorkspaceFormRulesPanel.vue` |

---

## 11. Yol haritası (onaylı)

```text
1. Form alan politikaları backlog (§10) — F1–F3 öncelikli, geri kalan bilinçli
2. Workspace politikaları W0–W1 (OC_UI_WORKSPACE_POLICIES.md)
3. Board / profil runtime (DEVAM #4–5) — paralel mümkün
```

---

*Layout: [OC_UI_FORM_DEFINITIONS.md](./OC_UI_FORM_DEFINITIONS.md). Handoff: [DEVAM.md](../mngoperations/DEVAM.md).*
