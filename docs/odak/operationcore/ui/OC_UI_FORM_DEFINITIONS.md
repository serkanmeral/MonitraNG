# Mng.Ui — Operation Core form tanımlama ve dinamik form

**Son güncelleme:** 9 Haziran 2026  
**Backend:** [FORM_LAYOUT_AND_EXTRA_FIELDS.md](../mngoperations/FORM_LAYOUT_AND_EXTRA_FIELDS.md)  
**Alan politikası (tek el):** [OC_UI_FIELD_POLICY.md](./OC_UI_FIELD_POLICY.md) · Handoff: [DEVAM.md](../mngoperations/DEVAM.md)

---

## 1. Kapsam (bu oturumda tamamlanan)

Workspace tanımları → **Forms** sekmesi: `op_forms` CRUD (DataGateway), layout tabanlı create formu, taslak önizleme, «Yeni iş» sayfası (MO create API).

| Alan | Durum |
|------|--------|
| Workspace tanımları — Genel / Yerleşim / **Alan politikaları** (3 sekme) | ✅ v1 |
| Layout: bölüm sıra, alan sıra (sürükle-bırak), `fieldCols`, `sectionCols` | ✅ |
| Layout’a eklenebilir core alanlar | `OC_FORM_LAYOUT_CORE_FIELD_KEYS` (watchers, reporter, dueDate, …; `key`/state hariç) |
| `dialogMaxWidth` (modal / sayfa genişliği) | ✅ |
| `formHeading` / `formIntro` | ✅ |
| `helpMarkdown` — Formlar → **Yardım** sekmesi; Yeni iş modalında Yardım butonu (`OcFormHelpDialog`) | ✅ F1 (9 Haz 2026) |
| `fieldBehaviors` + `defaultValues` | ✅ |
| Boards sekmesi (`op_boards`) temel CRUD | ✅ |
| `OcDynamicForm` — alan tipi widget’ları | ✅ |
| `OcFormPreviewDialog` — taslak önizleme (MO cache’siz) | ✅ |
| `work-items/new` — MO `FormRuntimeContext` + create | ✅ |
| Core + pool alan etiketleri (i18n + `op_fields.label`) | ✅ |

---

## 2. Önemli dosyalar

| Dosya | Rol |
|-------|-----|
| `Mng.Ui/components/.../OcWorkspaceDefinitionsFormsTab.vue` | Form listesi + editör (hedef: 3 sekme) |
| `Mng.Ui/components/.../OcWorkspaceFormFieldPolicyEditor.vue` | Alan politikaları tablosu |
| `Mng.Ui/components/.../OcWorkspaceFormLayoutEditor.vue` | Layout editörü (vue-draggable-next) |
| `Mng.Ui/components/.../OcFormPreviewDialog.vue` | Önizleme modal kabuğu |
| `Mng.Ui/components/.../OcDynamicForm.vue` | Runtime / taslak form render |
| `Mng.Ui/components/.../OcDynamicFormField.vue` | Tek alan widget seçimi |
| `Mng.Ui/utils/ocFormLayout.ts` | layout parse/build, grid, `dialogMaxWidth` |
| `Mng.Ui/utils/ocFormFieldLabels.ts` | Etiket + `enrichFormRuntimeFields` |
| `Mng.Ui/utils/ocDynamicFormField.ts` | Widget kind çözümleme |
| `Mng.Ui/utils/ocFileFieldOptions.ts` | `op_fields.options` — `maxSizeBytes`, `allowedExtensions` parse/build |
| `Mng.Ui/utils/ocWorkItemFileFields.ts` | Form model → `attachments` birleştirme |
| `Mng.Ui/components/.../OcWorkItemFileField.vue` | Dosya seçici widget |
| `Mng.Ui/components/.../OcWorkspaceDefinitionsFieldsTab.vue` | Alan tanımı: file tipinde MB + uzantı combobox |
| `Mng.Ui/composables/useOcDynamicFormLookups.ts` | priority/state/board/relation listeleri |
| `Mng.Ui/composables/useOcPersonPicker.ts` | Keeper kullanıcı arama + sayfalama |
| `Mng.Ui/utils/ocPersonPicker.ts` | Kullanıcı satır eşlemesi, form model id toplama |
| `Mng.Ui/services/operationCoreService.ts` | DG CRUD, MO runtime, `ocCreateWorkItem`, taslak preview builder |
| `Mng.Ui/pages/.../work-items/new/index.vue` | Yeni iş oluşturma |
| `Mng.Ui/pages/.../workspace-definitions/index.vue` | Workspace tanım sayfası |

**TM referans (kopyalanmadı, ilham):** `ProjectIssueCreateLayoutEditor.vue`, `TmNewIssueFormFields.vue`

---

## 3. Form editör sekmeleri

| Sekme | İçerik |
|-------|--------|
| **Genel** | Ad, üst metin, `dialogMaxWidth`, varsayılan tip/akış/state/öncelik |
| **Yerleşim** | Bölümler, alan sırası, grid |
| **Alan politikaları** | Statik `fieldBehaviors` + `defaultValues`; geçici `op_rules` / geçiş özeti |

Koşullu kurallar (rol, state): **formda değil** → [OC_UI_WORKSPACE_POLICIES.md](./OC_UI_WORKSPACE_POLICIES.md).  
Backlog: [OC_UI_FIELD_POLICY.md §10](./OC_UI_FIELD_POLICY.md).

Kayıt: `operationCoreService` → `ocCreateForm` / `ocUpdateForm` → DG `op_forms`.

---

## 4. Önizleme davranışı

- **Taslak önizleme:** `buildFormPreviewContextFromDraft()` — editördeki kaydedilmemiş state; MO metadata cache **kullanılmaz**.
- Bilgi kutusu: «Taslak önizleme» chip’i; modal genişliği `layout.dialogMaxWidth`.
- Kayıtlı formun MO runtime karşılığı: «Yeni iş» sayfası (`ocGetFormCreateContext`) — cache gecikmesi olabilir.
- **Metadata cache:** Workspace Tanımları → Genel → «Runtime önbelleğini yenile» veya MO `POST .../metadata-cache/reload` (bkz. [FORM_LAYOUT §5](../mngoperations/FORM_LAYOUT_AND_EXTRA_FIELDS.md)).

---

## 4.1 Pool `file` alanı — `op_fields.options`

Değerler → Alanlar sekmesinde `fieldType: file` için:

| UI alanı | DG `options` |
|----------|----------------|
| Max boyut (MB) | `maxSizeBytes` (UI MB → byte) |
| İzinli uzantılar | `allowedExtensions`: `[".pdf", ".png", …]` (boş = tüm tipler, yalnız max boyut) |

Örnek:

```json
{ "maxSizeBytes": 5242880, "allowedExtensions": [".pdf", ".png"] }
```

**Dosya alanları:** bkz. §4.1. **Lookup / seçim alanları:** [OC_UI_LOOKUP_FIELDS.md](./OC_UI_LOOKUP_FIELDS.md).

Runtime: MO `FormRuntimeContext.fields[key].options` → `enrichFormRuntimeFields`. Kayıt sonrası gecikme varsa metadata cache reload.

---

## 5. OcDynamicForm widget eşlemesi

| `op_fields.fieldType` / core key | UI |
|----------------------------------|-----|
| `typeId` | Select (runtime `types`) |
| `priorityId`, `boardId`, `stateId` | Select (DG listeleri) |
| `relation` (+ `relationDatasetName`) | Autocomplete / dropdown — `options.lookup` + `ocListDataset` |
| `select` | Statik liste — `options.lookup.staticItems` |
| `number` | `type="number"` |
| `bool` | Checkbox |
| `date` / `datetime` | Native date / datetime-local |
| `text` (pool) | Tek satır metin |
| `description` | Textarea |
| `persons` | `v-autocomplete` — Keeper arama (debounce), alt satır (e-posta/@user), sayfalama («Daha fazla»); `watchers` çoklu |
| `personGroups` | Metin (grup seçici sonraki faz) |
| `file` | `OcWorkItemFileField` — sürükle-bırak / seç; `op_fields.options` ile max MB + izinli uzantılar |
| `richtext` | `OcRichTextEditor` / `OcRichTextContent` (TipTap tabanlı HTML) |

Create gönderimi: `buildCreateWorkItemRequest` — core üst seviye + diğerleri `fields` → MO `extraFields` ayrımı. Formdaki **pool `file` alanları** create sırasında base64 payload olarak **`fields.attachments`** (core, `isArray`) dizisine birleştirilir; profil **Ekler** sekmesi aynı alanı okur (ayrı MO upload ucu gerekmez).

---

## 6. i18n

`Mng.Ui/utils/locales/tr.json` / `en.json` → `operationCore.fieldLabels.*`, `operationCore.workspaceDefinitions.forms.*`, `operationCore.formUi.*`, `operationCore.create.*`

---

## 7. Odak URL’leri (geliştirme)

| Servis | Örnek |
|--------|--------|
| UI | `npm run dev` (Mng.Ui) |
| Gateway | `http://192.168.20.20:5040` |
| MO (gateway) | `/operations/api/v1/...` |
| MO doğrudan | `http://192.168.20.20:5086` |

Proxy: `Mng.Ui` → `/api/operations/...`, `/api/v1/data/...` (DataGateway)

---

## 8. Bilinçli ertelenen (form tanımı)

| Madde | Not |
|-------|-----|
| `op_forms`: modal, systemFields, panels, visibilityRules | Spec’te var; admin UI yok |
| TM tarzı tam ekran form designer | Faz 2 |
| ~~Kullanıcı select~~ | ✅ `useOcPersonPicker` (arama + sayfa); diğer select’ler `v-autocomplete` (client filtre) |
| ~~Alan politikaları v1~~ | ✅ — backlog §10 |
| ~~Zorunlu yıldız~~ | ✅ önizleme + create |
| `op_rules` | Geçici form altı → [Workspace politikaları](./OC_UI_WORKSPACE_POLICIES.md) W0 |
| Koşullu kurallar | [Workspace politikaları](./OC_UI_WORKSPACE_POLICIES.md) |
| MO runtime vs taslak karşılaştırma | Opsiyonel |
| ~~Dosya upload widget~~ | ✅ create + profil düzenle + Ekler (`attachments`) |
| Form kaydı sonrası otomatik cache reload | Opsiyonel (manuel buton yeterli v1) |

---

## 9. Yeni chat için test checklist

1. Workspace tanımları → Forms → düzenle → Yerleşim değiştir → **Önizleme** (kaydetmeden).
2. Kaydet → **Önizleme** → Board → **Yeni iş** (layout + etiketler).
3. Pool alan `fieldType: number` → sayı kutusu görünmeli.
4. `dialogMaxWidth` 720 → önizleme modalı daralmalı.
5. Değerler → Alanlar → `file` alanı: max MB + uzantı kaydet → forma ekle → cache reload → board «Yeni iş» dosya yükle → kaydet → profil **Ekler**.

---

*Güncelleme: form/profile/board runtime ekranları ilerledikçe bu dosyayı genişletin.*
