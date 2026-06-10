# Mevcut Durum — Dinamik Form Oluşturma

**Son güncelleme:** 10 Haziran 2026  
**Amaç:** Uygulamada halihazırda var olan dinamik form çalışmalarının envanteri. Planlama için başlangıç noktası.

**Aktif POC:** [TEDARIKCILER_POC.md](./TEDARIKCILER_POC.md) · **Devam:** [DEVAM.md](./DEVAM.md)

---

## 1. Özet

MonitraNG'de dinamik formlar **iki bağımsız hat** olarak uygulanmış:

| Hat | Amaç | Veri modeli | Runtime | Durum |
|-----|------|-------------|---------|-------|
| **Automated Forms (AF)** | Herhangi bir dataset için CRUD formları | `@automated_forms` + hedef dataset | `DynamicFormField` | ✅ Temel özellikler tamam |
| **Operation Core Forms (OC)** | İş kaydı (work item) oluşturma/düzenleme | `op_forms` + `op_fields` | `OcDynamicForm` | ✅ v1 uygulandı |

**Kod paylaşımı yok** — iki hat paralel widget ve builder implementasyonları kullanıyor. Birleşik platform form motoru henüz yalnızca planlama aşamasında ([DEVAM.md](./DEVAM.md)).

---

## 2. Automated Forms (AF)

### Route'lar

| Route | Sayfa | Açıklama |
|-------|-------|----------|
| `/apps/automated-forms` | `Mng.Ui/pages/apps/automated-forms/index.vue` | Form listesi, arama, silme |
| `/apps/automated-forms/create` | `create.vue` | Yeni form tanımı |
| `/apps/automated-forms/edit/[formCode]` | `edit/[formCode].vue` | Form tanımı düzenleme |
| `/apps/automated-forms/view/[formCode]` | `view/[formCode].vue` | Runtime: liste + create/edit/delete |

### Bileşenler ve store

| Dosya | Rol |
|-------|-----|
| `components/apps/automated-forms/AutomatedFormForm.vue` | Form **builder** UI; alan ayarları modalı (textWidget / choiceWidget, liste sütunları) |
| `components/apps/automated-forms/DynamicFormField.vue` | Runtime tek alan widget'ı (text, textarea, richtext, select, autocomplete, relation, …) |
| `components/apps/automated-forms/FileUploadField.vue` | Dosya yükleme |
| `components/apps/automated-forms/AfListFilters.vue` | Liste gelişmiş filtre paneli (alan + operatör + değer) |
| `utils/afFormFieldPresentation.ts` | Alan sunumu çözümleme (`textWidget`, `choiceWidget`, static select items) |
| `utils/afListFilters.ts` | Liste filtre modeli ve DG sorgu dönüşümü |
| `composables/useAfRelationPicker.ts` | Relation autocomplete (debounced arama, sayfalama, label cache) |
| `stores/apps/automatedForms.ts` | Pinia store — `@automated_forms` CRUD; `formConfig.fieldLayout`, `readonlyOnEditFields` |
| `stores/apps/dataset.ts` | Dataset şema; `FieldType` içinde `select`, `file` |
| `composables/useFieldLabel.ts` | AF alan etiketi i18n |
| `composables/usePagePermissions.ts` | Sayfa yetkileri — Tedarikçiler view’da CRUD/export butonları |

### Backend

Özel C# servisi **yok**. Tüm işlemler **MngDataGateway** generic dataset CRUD:

- `@automated_forms` — form metadata
- `{datasetName}` — runtime hedef dataset CRUD

Kurulum: `scripts/tests/MngDataGateway/automated-forms/create-automated-forms-dataset.ps1`

### Desteklenen alan tipleri

`text`, `number`, `bool`, `datetime`, `object`, `relation`, `persons`, `personGroups`, `incremental`, `file`, **`select`** — array relation desteği dahil.

**`select` (9 Haz 2026):** DG şemada `options.lookup.staticItems`; runtime’da string saklama; staticItems doğrulaması henüz yok. UI: `fieldLayout.choiceWidget: select` veya dataset tipi `select`.

### formConfig genişlemeleri (9 Haz 2026)

| Alan | Açıklama |
|------|----------|
| `fieldLayout[field].textWidget` | `text` \| `textarea` \| `richtext` |
| `fieldLayout[field].choiceWidget` | `select` \| `autocomplete` (relation/persons/select) |
| `readonlyOnEditFields` | Yalnızca edit modunda salt okunur (ör. `kod`) |

Referans form: `tedarikciler-form` — [TEDARIKCILER_POC.md](./TEDARIKCILER_POC.md)

### Side menu

Side Menu Manager formları `/apps/automated-forms/view/{formCode}` path'ine bağlar. Prod örneği: TCDD GIS formları.

### Dokümantasyon

| Belge | Konum |
|-------|-------|
| Tam spec + checklist | [AUTOMATED_FORMS_PLANNING.md](../../content/Mng.Ui/support/specs/AUTOMATED_FORMS_PLANNING.md) |
| Güncel durum (Ocak 2026) | [current_status.md](../../content/Mng.Ui/support/guides/current_status.md) |
| Kullanım kılavuzu | [AUTOMATED_FORMS_USAGE.md](../../content/Mng.Ui/support/guides/AUTOMATED_FORMS_USAGE.md) |
| Chatbot rehberi | [automated-forms.md](../../content/Mng.Ui/guides/chatbot/automated-forms/automated-forms.md) |

### Tamamlanan / eksik (AF)

**Tamamlanan:** Builder, runtime CRUD, liste (pagination/sort/filter), gelişmiş filtre (`AfListFilters`), relation config + autocomplete picker, relation liste etiketi (`displayField`), field layout, field grupları, alan sunumu modalı (textarea/richtext/select), `readonlyOnEditFields`, side menu + permission, i18n, array field, `select` tipi (DG + UI), liste formatlama (`color`, `conditional-color`, `text-transform`), action sütunu sticky, `options` BsonDocument sync fix.

**Eksik / devam eden (spec + POC):**
- Liste yatay scroll (çok sütunlu tabloda — devam ediyor)
- Form önizleme (builder)
- Koşullu alan / alan politikası
- Form builder varsayılan değer (POC #5)
- Relation `dependsOn` (OC seviyesi — henüz yok)
- Kullanıcı bazlı sütun ayarları (`ColumnSelector`, localStorage)
- Server-side export

---

## 3. Operation Core Forms (OC)

### Form tanım editörü (builder)

Ayrı route değil; workspace admin içinde sekme:

| Route | Bileşen | Açıklama |
|-------|---------|----------|
| `/apps/operation-core/admin/workspace-definitions` | `OcWorkspaceDefinitionsFormsTab` | `op_forms` CRUD + layout/policy editörü |

**Editör alt bileşenleri:**

| Dosya | Rol |
|-------|-----|
| `OcWorkspaceFormLayoutEditor.vue` | Sürükle-bırak layout (bölüm + alan sırası, grid cols) |
| `OcWorkspaceFormFieldPolicyEditor.vue` | `fieldBehaviors` + `defaultValues` |
| `OcFormPolicyDefaultValueInput.vue` | Politika varsayılan değer |
| `OcWorkspaceFormTransitionRequirements.vue` | Geçiş zorunlu alan özeti |
| `OcWorkspaceFormRulesPanel.vue` | Form kuralları paneli |
| `OcFormPreviewDialog.vue` | Taslak önizleme |

### Runtime sayfaları

| Route | Kullanım |
|-------|----------|
| `/apps/operation-core/work-items/new` | Yeni iş — `OcDynamicForm` |
| `/apps/operation-core/work-items/[id]/profile` | Profil görüntüleme + yerinde düzenleme |
| `/apps/operation-core/boards/[boardId]` | Board modal — `OcWorkItemFormDialog` |

### Runtime bileşenleri

| Dosya | Rol |
|-------|-----|
| `OcDynamicForm.vue` | Layout bölümleri + grid render |
| `OcDynamicFormField.vue` | Tek alan widget seçimi |
| `OcTransitionRequiredFields.vue` | Geçiş sırasında zorunlu alan toplama |
| `OcWorkItemFileField.vue`, `OcTagSelector.vue`, `OcPersonPickerAutocomplete.vue` | Özel widget'lar |
| `utils/ocDynamicFormField.ts` | Widget kind çözümleme |
| `utils/ocFormLayout.ts` | `op_forms.layout` parse/build |
| `composables/useOcDynamicFormLookups.ts` | Select/person/relation lookup |

### Backend (MngOperations)

| API | Açıklama |
|-----|----------|
| `GET /api/v1/runtime/work-items/form?workspaceId=&formId=&mode=create` | Create form context |
| `GET /api/v1/runtime/work-items/{id}/form?mode=edit` | Edit form context |
| DataGateway `op_forms` | Form metadata CRUD |

**C# kaynakları:** `RuntimeController.cs`, `RuntimeContextService.Form.cs`, `FormRuntimeContext.cs`, `FormLayoutHelper.cs`, `FormRuntimeBuilder.cs`

### Dokümantasyon

| Belge | Konum |
|-------|-------|
| Form tanımları & runtime | [OC_UI_FORM_DEFINITIONS.md](../operationcore/ui/OC_UI_FORM_DEFINITIONS.md) |
| Alan politikası | [OC_UI_FIELD_POLICY.md](../operationcore/ui/OC_UI_FIELD_POLICY.md) |
| Lookup alanları | [OC_UI_LOOKUP_FIELDS.md](../operationcore/ui/OC_UI_LOOKUP_FIELDS.md) |
| Backend layout | [FORM_LAYOUT_AND_EXTRA_FIELDS.md](../operationcore/mngoperations/FORM_LAYOUT_AND_EXTRA_FIELDS.md) |

### Bilinçli ertelenen (OC)

- TM tarzı tam ekran form designer → Faz 2
- `op_forms`: modal, systemFields, panels, visibilityRules → admin UI yok
- Koşullu kurallar → Workspace politikaları sekmesi

---

## 4. Karşılaştırma (AF ↔ OC)

| Boyut | Automated Forms | Operation Core |
|-------|-----------------|----------------|
| Veri deposu | `@automated_forms` | `op_forms` (workspace-scoped) |
| Hedef veri | Kullanıcı seçtiği dataset | `op_work_items` (+ `extraFields`) |
| Alan şeması | Hedef dataset field definitions | `op_fields` + core alanlar |
| Layout | `formConfig.fieldOrder`, `fieldLayout` | `layout.sections[]`, `fieldBehaviors` |
| Runtime motor | Doğrudan DG CRUD | MO `FormRuntimeContext` → `OcDynamicForm` |
| Builder UI | `/apps/automated-forms/create\|edit` | Workspace Tanımları → Forms sekmesi |
| Runtime URL | `/apps/automated-forms/view/{formCode}` | Board modal / work-items / profil |

---

## 5. İlgili ama farklı kapsam

| Route / alan | Not |
|--------------|-----|
| `/forms/*` | Vuetify **tema demosu** — dinamik form sistemi değil |
| `/apps/automation-center/workflows/[workflowId]` | Workflow **graf editörü** — veri giriş formu değil |

---

## 6. Hızlı dosya indeksi

```
Mng.Ui/pages/apps/automated-forms/          ← AF sayfaları
Mng.Ui/components/apps/automated-forms/     ← AF builder + runtime field
Mng.Ui/stores/apps/automatedForms.ts

Mng.Ui/pages/apps/operation-core/admin/workspace-definitions/
Mng.Ui/components/apps/operation-core/OcDynamicForm*.vue
Mng.Ui/components/apps/operation-core/workspace-definitions/OcWorkspace*Form*.vue
Mng.Ui/services/operationCoreService.ts

MngOperations/.../RuntimeController.cs
MngOperations/.../RuntimeContextService.Form.cs
```
