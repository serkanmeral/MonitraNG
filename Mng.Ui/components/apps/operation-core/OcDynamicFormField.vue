<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcFieldBehaviorDto, OcFormFieldRuntimeDto } from '@/types/apps/operationCore';
import type { OcSelectItem } from '@/composables/useOcDynamicFormLookups';
import {
  coerceBoolValue,
  coerceNumberValue,
  isMultiCardinality,
  resolveOcDynamicFieldWidget,
} from '@/utils/ocDynamicFormField';

const props = defineProps<{
  fieldKey: string;
  meta?: OcFormFieldRuntimeDto | null;
  behavior: OcFieldBehaviorDto;
  selectItems?: OcSelectItem[];
  selectLoading?: boolean;
  readonly?: boolean;
  preview?: boolean;
}>();

const model = defineModel<unknown>({ required: true });

const { t } = useAppI18n();

const label = computed(() => props.meta?.label?.trim() || props.fieldKey);
const widget = computed(() =>
  resolveOcDynamicFieldWidget(props.fieldKey, props.meta, { masked: props.behavior.masked })
);
const disabled = computed(() => props.readonly || props.behavior.readonly);
const isMulti = computed(() => isMultiCardinality(props.meta));

const fieldClass = computed(() => (props.preview ? 'oc-dynamic-form__field--preview' : ''));

function update(value: unknown) {
  if (disabled.value) return;
  model.value = value;
}

const isPersonsWidget = computed(() => widget.value === 'persons' || widget.value === 'personsMulti');

const usePersonsTextFallback = computed(
  () => isPersonsWidget.value && !(props.selectItems?.length ?? 0)
);

const isSelectWidget = computed(
  () =>
    !usePersonsTextFallback.value &&
    [
      'typeSelect',
      'prioritySelect',
      'boardSelect',
      'stateSelect',
      'relationSelect',
      'relationSelectMulti',
      'persons',
      'personsMulti',
    ].includes(widget.value)
);

const selectMultiple = computed(
  () =>
    isMulti.value ||
    widget.value === 'relationSelectMulti' ||
    widget.value === 'personsMulti'
);
</script>

<template>
  <v-select
    v-if="isSelectWidget"
    :model-value="model"
    :items="selectItems ?? []"
    item-title="title"
    item-value="value"
    :label="label"
    :readonly="disabled"
    :disabled="disabled || selectLoading"
    :loading="selectLoading"
    :required="behavior.required"
    :multiple="selectMultiple"
    :chips="selectMultiple"
    :closable-chips="!disabled && selectMultiple"
    clearable
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="update"
  />

  <v-checkbox
    v-else-if="widget === 'bool'"
    :model-value="coerceBoolValue(model)"
    :label="label"
    :disabled="disabled"
    density="comfortable"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(!!v)"
  />

  <v-text-field
    v-else-if="widget === 'number'"
    :model-value="coerceNumberValue(model)"
    type="number"
    :label="label"
    :readonly="disabled"
    :required="behavior.required"
    clearable
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(coerceNumberValue(v))"
  />

  <v-text-field
    v-else-if="widget === 'date'"
    :model-value="String(model ?? '')"
    type="date"
    :label="label"
    :readonly="disabled"
    :required="behavior.required"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  />

  <v-text-field
    v-else-if="widget === 'datetime'"
    :model-value="String(model ?? '')"
    type="datetime-local"
    :label="label"
    :readonly="disabled"
    :required="behavior.required"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  />

  <v-text-field
    v-else-if="usePersonsTextFallback"
    :model-value="String(model ?? '')"
    :label="label"
    :readonly="disabled"
    :required="behavior.required"
    :hint="t('operationCore.formUi.personsFieldHint')"
    persistent-hint
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  />

  <v-text-field
    v-else-if="widget === 'file'"
    :model-value="String(model ?? '')"
    :label="label"
    readonly
    :hint="t('operationCore.formUi.fileFieldHint')"
    persistent-hint
    density="comfortable"
    variant="outlined"
    :class="fieldClass"
  />

  <v-textarea
    v-else-if="widget === 'textarea'"
    :model-value="String(model ?? '')"
    :label="label"
    :readonly="disabled"
    :required="behavior.required"
    rows="3"
    auto-grow
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  />

  <v-text-field
    v-else
    :model-value="String(model ?? '')"
    :label="label"
    :type="widget === 'password' ? 'password' : 'text'"
    :readonly="disabled"
    :required="behavior.required"
    density="comfortable"
    variant="outlined"
    hide-details="auto"
    :class="fieldClass"
    @update:model-value="(v) => update(v)"
  />
</template>
