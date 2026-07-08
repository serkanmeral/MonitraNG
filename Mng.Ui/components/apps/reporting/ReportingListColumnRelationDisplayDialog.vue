<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useDatasetStore } from '@/stores/apps/dataset';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';
import { reportingFieldLabel } from '@/utils/reportingListConfig';

const props = defineProps<{
  modelValue: boolean;
  column: OdakHubListColumnConfig | null;
  sourceField: FieldDefinition | null;
  schemaFields: FieldDefinition[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  save: [relationDisplayField: string | undefined];
}>();

const { t } = useAppI18n();
const datasetStore = useDatasetStore();

const draft = ref('');
const loading = ref(false);
const targetFields = ref<FieldDefinition[]>([]);

const sourceLabel = computed(() =>
  props.column ? reportingFieldLabel(props.sourceField ?? undefined, props.column.fieldName) : ''
);

const fieldOptions = computed(() =>
  targetFields.value
    .filter((f) => f.fieldType !== 'file' && f.fieldType !== 'object' && f.fieldType !== 'relation')
    .map((f) => ({ value: f.name, title: reportingFieldLabel(f, f.name) }))
);

watch(
  () => [props.modelValue, props.column?.fieldName, props.sourceField?.relationDataset] as const,
  async ([open]) => {
    if (!open || !props.column || !props.sourceField?.relationDataset) return;
    draft.value = props.column.relationDisplayField ?? '';
    loading.value = true;
    try {
      const ds = await datasetStore.fetchDatasetByName(props.sourceField.relationDataset);
      targetFields.value = ds?.fields ?? [];
      if (!draft.value) {
        const preferred = ['ad', 'name', 'title', 'label', 'kod', 'displayName'];
        draft.value = preferred.find((p) => targetFields.value.some((f) => f.name === p)) ?? '';
      }
    } finally {
      loading.value = false;
    }
  }
);

function close() {
  emit('update:modelValue', false);
}

function save() {
  const v = draft.value.trim();
  emit('save', v || undefined);
  close();
}

function clearDisplayField() {
  emit('save', undefined);
  close();
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="480" @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="text-h6 font-weight-bold">
        {{ t('reporting.columns.relationDisplayTitle', { column: sourceLabel }) }}
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-4">
        <p class="text-body-2 text-medium-emphasis mb-4">
          {{ t('reporting.columns.relationDisplayHint', { dataset: sourceField?.relationDataset ?? '' }) }}
        </p>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
        <v-select
          v-model="draft"
          :items="fieldOptions"
          item-title="title"
          item-value="value"
          :label="t('reporting.columns.relationDisplayField')"
          variant="outlined"
          density="comfortable"
          hide-details
          clearable
          :disabled="loading || !fieldOptions.length"
        />
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-btn variant="text" color="error" @click="clearDisplayField">
          {{ t('reporting.columns.relationDisplayClear') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" @click="close">{{ t('reporting.actions.cancel') }}</v-btn>
        <v-btn color="primary" variant="flat" @click="save">{{ t('reporting.actions.applyFormat') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
