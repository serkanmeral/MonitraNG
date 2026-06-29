<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcPersonPickerApi } from '@/composables/useOcPersonPicker';
import type { OcDatasetPickerApi } from '@/composables/useOcDatasetPicker';
import type { OcFieldBehaviorDto, OcFormFieldRuntimeDto } from '@/types/apps/operationCore';
import type { OcSelectItem } from '@/composables/useOcDynamicFormLookups';
import {
  buildOcSelectMenuProps,
  coerceBoolValue,
  coerceNumberValue,
  isMultiCardinality,
  resolveOcDynamicFieldWidget,
} from '@/utils/ocDynamicFormField';
import { resolveOcFormFieldType } from '@/utils/ocFormFieldLabels';
import { parseOcLookupFromFieldOptions } from '@/utils/ocLookupFieldOptions';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';
import OcLookupDatasetPickerField from '@/components/apps/operation-core/OcLookupDatasetPickerField.client.vue';
import OcTagSelector from '@/components/apps/operation-core/OcTagSelector.vue';
import OcRichTextEditor from '@/components/apps/operation-core/OcRichTextEditor.client.vue';
import OcRichTextContent from '@/components/apps/operation-core/OcRichTextContent.client.vue';
import OcWorkItemFileField from '@/components/apps/operation-core/OcWorkItemFileField.vue';

const props = defineProps<{
  fieldKey: string;
  meta?: OcFormFieldRuntimeDto | null;
  behavior: OcFieldBehaviorDto;
  /** Aktif workspace (tags alanı etiket kataloğunu bu workspace'ten okur/yaratır). */
  workspaceId?: string | null;
  selectItems?: OcSelectItem[];
  selectLoading?: boolean;
  /** dropdown → v-select; autocomplete → v-autocomplete; picker → modal tablo */
  selectPresentation?: 'dropdown' | 'autocomplete' | 'picker';
  /** dependsOn üst alanı boşken devre dışı */
  selectDependsOnBlocked?: boolean;
  personPicker?: OcPersonPickerApi;
  datasetPicker?: OcDatasetPickerApi;
  /** Grup id → ad (readonly grup alanlarında ham id yerine ad göstermek için; profil sağlar). */
  groupNames?: Record<string, string>;
  /** MO'da çözülmüş görünen metin (relation/person/katalog) — readonly'de lookup yerine gösterilir. */
  fieldDisplay?: string | null;
  readonly?: boolean;
  preview?: boolean;
  errorMessage?: string | null;
}>();

const model = defineModel<unknown>({ required: true });

const { t } = useAppI18n();

const label = computed(() => props.meta?.label?.trim() || props.fieldKey);
const widget = computed(() =>
  resolveOcDynamicFieldWidget(props.fieldKey, props.meta, { masked: props.behavior.masked })
);
const formReadonly = computed(() => props.readonly === true);
const fieldLocked = computed(() => props.behavior.readonly === true);
const fieldDisabled = computed(() => formReadonly.value || fieldLocked.value);
const isRichTextWidget = computed(() => widget.value === 'richtext');
/** Form düzenlenebilirken core açıklama editörü; MO havuz editGroups artefaktını UI'da yoksay. */
const richTextEditing = computed(() => {
  if (!isRichTextWidget.value || formReadonly.value) return false;
  if (props.fieldKey.toLowerCase() === 'description') return true;
  return !fieldLocked.value;
});

const isMulti = computed(() => isMultiCardinality(props.fieldKey, props.meta));
const isPersonsWidget = computed(() => widget.value === 'persons' || widget.value === 'personsMulti');
const isTagsWidget = computed(() => widget.value === 'tags');
const isFileWidget = computed(() => widget.value === 'file');

// Grup alanları (personGroups/group): readonly görünümde ham id yerine grup adını göster.
const fieldType = computed(() => resolveOcFormFieldType(props.fieldKey, props.meta).toLowerCase());
const isGroupField = computed(() =>
  ['persongroups', 'persongroup', 'group'].includes(fieldType.value)
);

function collectGroupIds(value: unknown): string[] {
  if (value === null || value === undefined || value === '') return [];
  if (Array.isArray(value)) return value.flatMap((v) => collectGroupIds(v));
  if (typeof value === 'object') {
    const o = value as Record<string, unknown>;
    const id = o.__dataId ?? o.id ?? o.groupId;
    return id ? [String(id).trim()] : [];
  }
  const s = String(value).trim();
  return s ? [s] : [];
}

const groupReadonlyText = computed(() => {
  const ids = collectGroupIds(model.value);
  if (!ids.length) return '—';
  const map = props.groupNames ?? {};
  const names = ids.map((id) => map[id]?.trim() || id).filter(Boolean);
  return names.length ? names.join(', ') : '—';
});

const isSelectWidget = computed(() =>
  [
    'typeSelect',
    'prioritySelect',
    'boardSelect',
    'stateSelect',
    'staticSelect',
    'staticSelectMulti',
    'relationSelect',
    'relationSelectMulti',
    'groupSelect',
    'groupSelectMulti',
  ].includes(widget.value)
);

const useDropdownSelect = computed(
  () => props.selectPresentation === 'dropdown' && isSelectWidget.value
);

const lookupConfig = computed(() => {
  const ft = resolveOcFormFieldType(props.fieldKey, props.meta);
  return parseOcLookupFromFieldOptions(props.meta?.options, ft);
});

const isDatasetPickerWidget = computed(
  () =>
    isSelectWidget.value &&
    props.selectPresentation === 'picker' &&
    props.datasetPicker != null
);

// Readonly (profil) görünümde lookup yapılmadığından select/person/grup alanları
// MO'dan gelen çözülmüş metinle (fieldDisplay) ya da grup adıyla gösterilir.
const hasFieldDisplay = computed(
  () => props.fieldDisplay != null && String(props.fieldDisplay).trim() !== ''
);
const useReadonlyDisplay = computed(
  () =>
    fieldDisabled.value &&
    !isRichTextWidget.value &&
    // tags HARİÇ: tags readonly'de de OcTagSelector ile renkli chip gösterir (düz metin değil).
    (isGroupField.value ||
      ((isSelectWidget.value || isPersonsWidget.value) && hasFieldDisplay.value) ||
      widget.value === 'date' ||
      widget.value === 'datetime')
);
const readonlyDisplayText = computed(() => {
  if (isGroupField.value) return groupReadonlyText.value;
  if (widget.value === 'date' || widget.value === 'datetime') {
    const raw = model.value;
    if (raw == null || raw === '') return '—';
    const d = new Date(String(raw));
    if (Number.isNaN(d.getTime())) return String(raw);
    return widget.value === 'date'
      ? d.toLocaleDateString('tr-TR')
      : d.toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
  }
  return String(props.fieldDisplay ?? '').trim() || '—';
});

const selectMultiple = computed(
  () =>
    isMulti.value ||
    widget.value === 'relationSelectMulti' ||
    widget.value === 'staticSelectMulti' ||
    widget.value === 'personsMulti' ||
    widget.value === 'groupSelectMulti'
);

const selectModelValue = computed(() => {
  if (!isSelectWidget.value) return model.value;
  if (selectMultiple.value) {
    if (Array.isArray(model.value)) return model.value;
    if (model.value != null && model.value !== '') return [model.value];
    return [];
  }
  return model.value ?? null;
});

const selectMenuProps = computed(() =>
  buildOcSelectMenuProps(props.preview ? 'dialog' : 'default')
);

const autocompleteItems = computed(() => props.selectItems ?? []);

const selectControlDisabled = computed(
  () => fieldDisabled.value || props.selectDependsOnBlocked === true
);

const fieldClass = computed(() => (props.preview ? 'oc-dynamic-form__field--preview' : ''));

const showFieldError = computed(() => !!props.errorMessage?.trim());

const fieldErrorMessages = computed(() =>
  showFieldError.value && props.errorMessage ? [props.errorMessage] : undefined
);

function update(value: unknown) {
  if (isRichTextWidget.value) {
    if (!richTextEditing.value) return;
  } else if (fieldDisabled.value) {
    return;
  }
  model.value = value;
}

const autocompleteLoading = computed(() => props.selectLoading ?? false);

function isAllowedSelectValue(value: unknown): boolean {
  const allowed = new Set(autocompleteItems.value.map((i) => i.value));
  if (selectMultiple.value && Array.isArray(value)) {
    return value.every((v) => allowed.has(String(v)));
  }
  return value != null && value !== '' && allowed.has(String(value));
}

function onSelectUpdate(value: unknown) {
  if (selectControlDisabled.value) return;
  if (value === null || value === '' || (Array.isArray(value) && value.length === 0)) {
    update(selectMultiple.value ? [] : null);
    return;
  }
  if (isAllowedSelectValue(value)) {
    update(value);
  }
}
</script>

<template>
  <v-text-field
    v-if="useReadonlyDisplay"
    :model-value="readonlyDisplayText"
    readonly
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-text-field>

  <OcPersonPickerAutocomplete
    v-else-if="isPersonsWidget && personPicker"
    v-model="model"
    :multiple="selectMultiple"
    :disabled="fieldDisabled"
    :external-picker="personPicker"
    :menu-context="preview ? 'dialog' : 'default'"
    :label="label"
    :show-required-mark="behavior.required"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    :field-class="fieldClass"
  />

  <client-only v-else-if="isDatasetPickerWidget && datasetPicker">
    <OcLookupDatasetPickerField
      v-model="model"
      :multiple="selectMultiple"
      :disabled="selectControlDisabled"
      :external-picker="datasetPicker"
      :label="label"
      :show-required-mark="behavior.required"
      :error="showFieldError"
      :error-messages="fieldErrorMessages"
      :field-class="fieldClass"
      :label-field-key="lookupConfig?.labelField"
      :search-field-keys="lookupConfig?.searchFields ?? []"
    />
    <template #fallback>
      <v-skeleton-loader type="text" />
    </template>
  </client-only>

  <OcTagSelector
    v-else-if="isTagsWidget"
    v-model="model"
    :workspace-id="workspaceId"
    :multiple="true"
    :disabled="fieldDisabled"
    :label="label"
    :required="behavior.required"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    :preview="preview"
  />

  <v-select
    v-else-if="isSelectWidget && useDropdownSelect"
    :model-value="selectModelValue"
    :items="autocompleteItems"
    item-title="title"
    item-value="value"
    :disabled="selectControlDisabled"
    :loading="autocompleteLoading"
    :menu-props="selectMenuProps"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    :multiple="selectMultiple"
    :chips="selectMultiple"
    :closable-chips="!selectControlDisabled && selectMultiple"
    clearable
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="onSelectUpdate"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-select>

  <v-autocomplete
    v-else-if="isSelectWidget"
    :model-value="selectModelValue"
    :items="autocompleteItems"
    item-title="title"
    item-value="value"
    :disabled="selectControlDisabled"
    :loading="autocompleteLoading"
    :menu-props="selectMenuProps"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    :multiple="selectMultiple"
    :chips="selectMultiple"
    :closable-chips="!selectControlDisabled && selectMultiple"
    clearable
    :auto-select-first="false"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="onSelectUpdate"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-autocomplete>

  <div v-else-if="widget === 'bool'" class="oc-dynamic-form__bool-field">
    <v-checkbox
      :model-value="coerceBoolValue(model)"
      :disabled="fieldDisabled"
      density="comfortable"
      :hide-details="!showFieldError"
      :error="showFieldError"
      :class="fieldClass"
      @update:model-value="(v) => update(!!v)"
    >
      <template #label>
        <span>{{ label }}</span>
        <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
      </template>
    </v-checkbox>
    <div v-if="showFieldError" class="text-caption text-error oc-dynamic-form__bool-error">
      {{ errorMessage }}
    </div>
  </div>

  <v-text-field
    v-else-if="widget === 'number'"
    :model-value="coerceNumberValue(model)"
    type="number"
    :readonly="fieldDisabled"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    clearable
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(coerceNumberValue(v))"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-text-field>

  <v-text-field
    v-else-if="widget === 'date'"
    :model-value="String(model ?? '')"
    type="date"
    :readonly="fieldDisabled"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-text-field>

  <v-text-field
    v-else-if="widget === 'datetime'"
    :model-value="String(model ?? '')"
    type="datetime-local"
    :readonly="fieldDisabled"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-text-field>

  <div v-else-if="isFileWidget" class="oc-dynamic-form__file-field">
    <div class="text-caption text-medium-emphasis mb-1 d-flex align-center ga-1">
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </div>
    <OcWorkItemFileField
      v-model="model"
      :field-key="fieldKey"
      :meta="meta"
      :readonly="fieldDisabled"
      :error-message="errorMessage"
    />
  </div>

  <div v-else-if="isRichTextWidget" class="oc-dynamic-form__richtext-field">
    <div class="text-caption text-medium-emphasis mb-1 d-flex align-center ga-1">
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </div>
    <client-only>
      <OcRichTextContent
        v-if="!richTextEditing"
        :html="String(model ?? '')"
        :class="fieldClass"
      />
      <OcRichTextEditor
        v-else
        :model-value="String(model ?? '')"
        :placeholder="label"
        @update:model-value="(v) => update(v)"
      />
      <template #fallback>
        <v-skeleton-loader type="paragraph" class="rounded-lg" />
      </template>
    </client-only>
    <div v-if="showFieldError" class="text-caption text-error mt-1">
      {{ errorMessage }}
    </div>
  </div>

  <v-textarea
    v-else-if="widget === 'textarea'"
    :model-value="String(model ?? '')"
    :readonly="fieldDisabled"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    rows="3"
    auto-grow
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-textarea>

  <v-text-field
    v-else
    :model-value="String(model ?? '')"
    :type="widget === 'password' ? 'password' : 'text'"
    :readonly="fieldDisabled"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-text-field>
</template>

<style scoped>
.oc-field-required {
  color: rgb(var(--v-theme-error));
  font-weight: 600;
}

.oc-dynamic-form__bool-error {
  margin-left: 40px;
  margin-top: -4px;
}

</style>
