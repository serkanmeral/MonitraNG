<script setup lang="ts">
import { computed, watch } from 'vue';
import type { PresentationKind } from '@/types/apps/widgetManifest';
import {
  validatePresentationFieldMapping,
  type FieldMappingValidation,
} from '@/utils/widgets/widgetFieldMappingBridge';

const props = defineProps<{
  kind: PresentationKind;
  modelValue: Record<string, unknown>;
  sampleRowKeys?: string[];
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: Record<string, unknown>];
}>();

const lbl = (key: string) => props.t?.(`widgets.designer.fieldMapping.${key}`) ?? key;

const local = computed({
  get: () => props.modelValue ?? {},
  set: (v) => emit('update:modelValue', v),
});

const xAxisField = computed({
  get: () => String((local.value.xAxis as { field?: string })?.field ?? ''),
  set: (v) => {
    local.value = {
      ...local.value,
      xAxis: { ...(local.value.xAxis as object), field: v || undefined },
    };
  },
});

const yAxisField = computed({
  get: () => String((local.value.yAxis as { field?: string })?.field ?? ''),
  set: (v) => {
    local.value = {
      ...local.value,
      yAxis: { ...(local.value.yAxis as object), field: v || undefined },
    };
  },
});

const valueField = computed({
  get: () => String(local.value.valueField ?? 'value'),
  set: (v) => {
    local.value = { ...local.value, valueField: v || 'value' };
  },
});

const fieldOptions = computed(() => {
  const keys = props.sampleRowKeys ?? [];
  return keys.map((k) => ({ title: k, value: k }));
});

const validation = computed((): FieldMappingValidation =>
  validatePresentationFieldMapping(props.kind, local.value),
);

watch(
  () => props.kind,
  () => {
    /* preset değişince parent günceller */
  },
);
</script>

<template>
  <div>
    <div class="text-subtitle-2 mb-2">{{ lbl('title') }}</div>
    <p class="text-caption text-medium-emphasis mb-3">{{ lbl('hint') }}</p>

    <template v-if="kind === 'stat'">
      <v-text-field
        v-model="valueField"
        :label="lbl('valueField')"
        :hint="lbl('valueFieldHint')"
        persistent-hint
        variant="outlined"
        density="compact"
        class="mb-3"
      />
    </template>

    <template v-else-if="kind === 'chart'">
      <v-combobox
        v-model="xAxisField"
        :items="fieldOptions"
        item-title="title"
        item-value="value"
        :label="lbl('xAxisField')"
        :hint="lbl('xAxisFieldHint')"
        persistent-hint
        variant="outlined"
        density="compact"
        class="mb-3"
        clearable
      />
      <v-combobox
        v-model="yAxisField"
        :items="fieldOptions"
        item-title="title"
        item-value="value"
        :label="lbl('yAxisField')"
        :hint="lbl('yAxisFieldHint')"
        persistent-hint
        variant="outlined"
        density="compact"
        class="mb-3"
        clearable
      />
    </template>

    <template v-else-if="kind === 'table' || kind === 'list'">
      <v-alert type="info" variant="tonal" density="compact" class="mb-2">
        {{ lbl('tableColumnsHint') }}
      </v-alert>
      <div v-if="Array.isArray(local.columns) && local.columns.length" class="text-caption">
        <div v-for="(col, idx) in (local.columns as Array<{ key: string; title: string; format?: string }>)" :key="idx">
          {{ col.title || col.key }}
          <span v-if="col.format" class="text-disabled"> · {{ col.format }}</span>
        </div>
      </div>
    </template>

    <v-alert
      v-for="warn in validation.warnings"
      :key="warn"
      type="warning"
      variant="tonal"
      density="compact"
      class="mt-3"
    >
      {{ lbl(warn) }}
    </v-alert>
  </div>
</template>
