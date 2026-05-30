<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcPersonPickerApi } from '@/composables/useOcPersonPicker';
import type { OcFieldBehaviorDto, OcFormFieldRuntimeDto } from '@/types/apps/operationCore';
import type { OcSelectItem } from '@/composables/useOcDynamicFormLookups';
import {
  buildOcSelectMenuProps,
  coerceBoolValue,
  coerceNumberValue,
  isMultiCardinality,
  resolveOcDynamicFieldWidget,
} from '@/utils/ocDynamicFormField';
import { resolveOcCoreFieldType } from '@/utils/ocFormFieldLabels';
import OcPersonPickerAutocomplete from '@/components/apps/operation-core/OcPersonPickerAutocomplete.vue';

const props = defineProps<{
  fieldKey: string;
  meta?: OcFormFieldRuntimeDto | null;
  behavior: OcFieldBehaviorDto;
  selectItems?: OcSelectItem[];
  selectLoading?: boolean;
  personPicker?: OcPersonPickerApi;
  /** Grup id → ad (readonly grup alanlarında ham id yerine ad göstermek için; profil sağlar). */
  groupNames?: Record<string, string>;
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
const fieldDisabled = computed(() => props.readonly || props.behavior.readonly);
const isMulti = computed(() => isMultiCardinality(props.fieldKey, props.meta));

const isPersonsWidget = computed(() => widget.value === 'persons' || widget.value === 'personsMulti');

// Grup alanları (personGroups/group): readonly görünümde ham id yerine grup adını göster.
const fieldType = computed(() =>
  (props.meta?.fieldType ?? resolveOcCoreFieldType(props.fieldKey)).toLowerCase()
);
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
    'relationSelect',
    'relationSelectMulti',
  ].includes(widget.value)
);

const selectMultiple = computed(
  () =>
    isMulti.value ||
    widget.value === 'relationSelectMulti' ||
    widget.value === 'personsMulti'
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

const autocompleteLoading = computed(() => props.selectLoading ?? false);

const fieldClass = computed(() => (props.preview ? 'oc-dynamic-form__field--preview' : ''));

const showFieldError = computed(() => !!props.errorMessage?.trim());

const fieldErrorMessages = computed(() =>
  showFieldError.value && props.errorMessage ? [props.errorMessage] : undefined
);

function update(value: unknown) {
  if (fieldDisabled.value) return;
  model.value = value;
}

function onAutocompleteUpdate(value: unknown) {
  if (fieldDisabled.value) return;
  update(value);
}
</script>

<template>
  <v-text-field
    v-if="fieldDisabled && isGroupField"
    :model-value="groupReadonlyText"
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

  <v-autocomplete
    v-else-if="isSelectWidget"
    :model-value="selectModelValue"
    :items="autocompleteItems"
    item-title="title"
    item-value="value"
    :disabled="fieldDisabled"
    :loading="autocompleteLoading"
    :menu-props="selectMenuProps"
    :error="showFieldError"
    :error-messages="fieldErrorMessages"
    :multiple="selectMultiple"
    :chips="selectMultiple"
    :closable-chips="!fieldDisabled && selectMultiple"
    clearable
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="onAutocompleteUpdate"
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

  <v-text-field
    v-else-if="widget === 'file'"
    :model-value="String(model ?? '')"
    readonly
    :hint="t('operationCore.formUi.fileFieldHint')"
    persistent-hint
    density="comfortable"
    variant="outlined"
    :class="fieldClass"
  >
    <template #label>
      <span>{{ label }}</span>
      <span v-if="behavior.required" class="oc-field-required" aria-hidden="true"> *</span>
    </template>
  </v-text-field>

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
