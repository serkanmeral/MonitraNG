<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import { ocListDataset } from '@/services/operationCoreService';
import { resolveOcCoreFieldType } from '@/utils/ocFormFieldLabels';
import {
  coerceBoolValue,
  coerceNumberValue,
  isOcPersonsUserPickerField,
  isMultiCardinality,
  recordToDatasetItems,
  resolveRelationDataset,
} from '@/utils/ocDynamicFormField';

const props = withDefaults(
  defineProps<{
    fieldKey: string;
    fieldType?: string;
    relationDataset?: string | null;
    cardinality?: string;
    workspaceId?: string;
    typeItems?: { value: string; title: string }[];
    priorityItems?: { value: string; title: string }[];
    stateItems?: { value: string; title: string }[];
    boardItems?: { value: string; title: string }[];
    /** v-select / v-text-field yoğunluğu — politika şart satırında comfortable kullanın */
    density?: 'default' | 'comfortable' | 'compact';
  controlVariant?: 'outlined' | 'plain' | 'solo';
    /** v-select ile aynı hizada yüzen etiket (politika şart satırı) */
    fieldLabel?: string;
  }>(),
  { density: 'compact', controlVariant: 'outlined' }
);

const fieldVariant = computed(() => props.controlVariant);

const model = defineModel<unknown>();

const relationItems = ref<{ title: string; value: string }[]>([]);
const relationLoading = ref(false);

const meta = computed(() => ({
  fieldType: props.fieldType,
  cardinality: props.cardinality,
}));

const resolvedType = computed(() =>
  (props.fieldType ?? resolveOcCoreFieldType(props.fieldKey)).toLowerCase()
);

const widget = computed(() => {
  if (props.fieldKey === 'typeId') return 'typeSelect';
  if (props.fieldKey === 'priorityId') return 'prioritySelect';
  if (props.fieldKey === 'stateId') return 'stateSelect';
  if (props.fieldKey === 'boardId') return 'boardSelect';
  if (isOcPersonsUserPickerField(props.fieldKey, meta.value)) {
    return isMultiCardinality(props.fieldKey, meta.value) ? 'personsMulti' : 'persons';
  }
  const ft = resolvedType.value;
  if (ft === 'relation') return isMultiCardinality(props.fieldKey, meta.value) ? 'relationSelectMulti' : 'relationSelect';
  if (ft === 'bool' || ft === 'boolean') return 'bool';
  if (ft === 'number') return 'number';
  if (ft === 'date') return 'date';
  if (ft === 'datetime') return 'datetime';
  return 'text';
});

const isPersonWidget = computed(() => widget.value === 'persons' || widget.value === 'personsMulti');

const selectMultiple = computed(
  () => widget.value === 'personsMulti' || widget.value === 'relationSelectMulti'
);

const selectModelValue = computed(() => {
  if (!selectMultiple.value) return model.value ?? null;
  if (Array.isArray(model.value)) return model.value;
  if (model.value != null && model.value !== '') return [model.value];
  return [];
});

async function loadRelationItems() {
  const dataset =
    props.relationDataset?.trim() ||
    resolveRelationDataset(props.fieldKey, { fieldType: props.fieldType });
  if (!dataset) {
    relationItems.value = [];
    return;
  }
  relationLoading.value = true;
  try {
    const rows = await ocListDataset(dataset, { limit: 500 });
    relationItems.value = recordToDatasetItems(rows);
  } catch {
    relationItems.value = [];
  } finally {
    relationLoading.value = false;
  }
}

watch(
  () => [props.fieldKey, props.relationDataset, props.fieldType],
  () => {
    if (widget.value === 'relationSelect' || widget.value === 'relationSelectMulti') {
      void loadRelationItems();
    }
  },
  { immediate: true }
);

function onSelectUpdate(value: unknown) {
  model.value = value;
}
</script>

<template>
  <div class="oc-form-policy-default-value-input w-100">
  <v-checkbox
    v-if="widget === 'bool'"
    :model-value="coerceBoolValue(model)"
    :label="fieldLabel"
    :density="density"
    hide-details
    @update:model-value="(v) => (model = !!v)"
  />
  <v-text-field
    v-else-if="widget === 'number'"
    :model-value="coerceNumberValue(model)"
    type="number"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="(v) => (model = coerceNumberValue(v))"
  />
  <v-text-field
    v-else-if="widget === 'date'"
    :model-value="String(model ?? '')"
    type="date"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="(v) => (model = v)"
  />
  <v-text-field
    v-else-if="widget === 'datetime'"
    :model-value="String(model ?? '')"
    type="datetime-local"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="(v) => (model = v)"
  />
  <v-select
    v-else-if="widget === 'typeSelect'"
    :model-value="model ?? null"
    :items="typeItems ?? []"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="onSelectUpdate"
  />
  <v-select
    v-else-if="widget === 'prioritySelect'"
    :model-value="model ?? null"
    :items="priorityItems ?? []"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="onSelectUpdate"
  />
  <v-select
    v-else-if="widget === 'stateSelect'"
    :model-value="model ?? null"
    :items="stateItems ?? []"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="onSelectUpdate"
  />
  <v-select
    v-else-if="widget === 'boardSelect'"
    :model-value="model ?? null"
    :items="boardItems ?? []"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="onSelectUpdate"
  />
  <v-select
    v-else-if="widget === 'relationSelect' || widget === 'relationSelectMulti'"
    :model-value="selectModelValue"
    :items="relationItems"
    item-title="title"
    item-value="value"
    :multiple="selectMultiple"
    :chips="selectMultiple"
    :loading="relationLoading"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="onSelectUpdate"
  />
  <MngDirectoryPickerField
    v-else-if="isPersonWidget"
    v-model="model"
    entity="user"
    :multiple="selectMultiple"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
  />
  <v-text-field
    v-else
    :model-value="String(model ?? '')"
    :label="fieldLabel"
    :density="density"
    :variant="fieldVariant"
    hide-details
    clearable
    @update:model-value="(v) => (model = v)"
  />
  </div>
</template>
