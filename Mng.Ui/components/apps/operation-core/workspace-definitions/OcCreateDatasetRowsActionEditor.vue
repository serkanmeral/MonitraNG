<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  newCreateDatasetRowsMapping,
  type OcCreateDatasetRowsDraft,
  type OcCreateDatasetRowsFieldMapping,
} from '@/utils/ocWorkspaceRules';

const props = defineProps<{
  modelValue: OcCreateDatasetRowsDraft;
  conditionFieldItems: { key: string; label: string }[];
}>();

const emit = defineEmits<{
  'update:modelValue': [OcCreateDatasetRowsDraft];
}>();

const { t } = useAppI18n();

const draft = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
});

function patch(partial: Partial<OcCreateDatasetRowsDraft>) {
  emit('update:modelValue', { ...props.modelValue, ...partial });
}

function patchMapping(id: string, partial: Partial<OcCreateDatasetRowsFieldMapping>) {
  patch({
    fieldMappings: props.modelValue.fieldMappings.map((m) =>
      m.id === id ? { ...m, ...partial } : m
    ),
  });
}

function addMapping() {
  patch({
    fieldMappings: [...props.modelValue.fieldMappings, newCreateDatasetRowsMapping()],
  });
}

function removeMapping(id: string) {
  patch({
    fieldMappings: props.modelValue.fieldMappings.filter((m) => m.id !== id),
  });
}

const cardinalityItems = computed(() => [
  {
    value: 'single',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.cardinalitySingle'),
  },
  {
    value: 'count',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.cardinalityCount'),
  },
  {
    value: 'expand',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.cardinalityExpand'),
  },
]);

const idempotencyItems = computed(() => [
  {
    value: 'one_per_source',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.idempotencyOne'),
  },
  {
    value: 'none',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.idempotencyNone'),
  },
]);

const onErrorItems = computed(() => [
  {
    value: 'failTransition',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.onErrorFail'),
  },
  {
    value: 'continue',
    title: t('operationCore.workspaceDefinitions.rules.createDatasetRows.onErrorContinue'),
  },
]);

const mappingSourceItems = computed(() => [
  { value: 'field', title: 'field' },
  { value: 'static', title: 'static' },
  { value: 'token', title: 'token' },
  { value: 'item', title: 'item' },
  { value: 'sequence', title: 'sequence' },
]);

const pathSuggestions = computed(() =>
  props.conditionFieldItems.map((f) => ({
    value: f.key.startsWith('fields.') || ['key', 'id', 'assignee'].includes(f.key)
      ? f.key
      : `fields.${f.key}`,
    title: f.label,
  }))
);
</script>

<template>
  <div class="oc-create-dataset-rows-editor">
    <v-alert type="info" variant="tonal" density="compact" class="mb-3 rounded-lg">
      {{ t('operationCore.workspaceDefinitions.rules.createDatasetRows.hint') }}
    </v-alert>

    <v-text-field
      :model-value="draft.dataset"
      :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.dataset')"
      :hint="t('operationCore.workspaceDefinitions.rules.createDatasetRows.datasetHint')"
      persistent-hint
      density="comfortable"
      class="mb-3"
      @update:model-value="patch({ dataset: String($event ?? '') })"
    />

    <v-select
      :model-value="draft.cardinalityMode"
      :items="cardinalityItems"
      item-title="title"
      item-value="value"
      :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.cardinality')"
      density="comfortable"
      class="mb-3"
      @update:model-value="patch({ cardinalityMode: $event })"
    />

    <v-text-field
      v-if="draft.cardinalityMode === 'count' || draft.cardinalityMode === 'expand'"
      :model-value="draft.countFrom"
      :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.countFrom')"
      density="comfortable"
      class="mb-3"
      @update:model-value="patch({ countFrom: String($event ?? '') })"
    />

    <template v-if="draft.cardinalityMode === 'expand'">
      <v-text-field
        :model-value="draft.itemsFrom"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.itemsFrom')"
        density="comfortable"
        class="mb-3"
        @update:model-value="patch({ itemsFrom: String($event ?? '') })"
      />
      <v-text-field
        :model-value="draft.itemAs"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.itemAs')"
        density="comfortable"
        class="mb-3"
        @update:model-value="patch({ itemAs: String($event ?? '') })"
      />
    </template>

    <v-select
      :model-value="draft.idempotencyMode"
      :items="idempotencyItems"
      item-title="title"
      item-value="value"
      :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.idempotency')"
      density="comfortable"
      class="mb-3"
      @update:model-value="patch({ idempotencyMode: $event })"
    />

    <template v-if="draft.idempotencyMode === 'one_per_source'">
      <v-text-field
        :model-value="draft.lookupField"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.lookupField')"
        density="comfortable"
        class="mb-3"
        @update:model-value="patch({ lookupField: String($event ?? '') })"
      />
      <v-text-field
        :model-value="draft.lookupFrom"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.lookupFrom')"
        density="comfortable"
        class="mb-3"
        @update:model-value="patch({ lookupFrom: String($event ?? '') })"
      />
    </template>

    <v-select
      :model-value="draft.onError"
      :items="onErrorItems"
      item-title="title"
      item-value="value"
      :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.onError')"
      density="comfortable"
      class="mb-4"
      @update:model-value="patch({ onError: $event })"
    />

    <div class="d-flex align-center justify-space-between mb-2">
      <div class="text-subtitle-2">
        {{ t('operationCore.workspaceDefinitions.rules.createDatasetRows.mappings') }}
      </div>
      <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addMapping">
        {{ t('operationCore.workspaceDefinitions.rules.createDatasetRows.addMapping') }}
      </v-btn>
    </div>

    <div
      v-for="row in draft.fieldMappings"
      :key="row.id"
      class="oc-create-dataset-rows-editor__mapping mb-3 pa-3 rounded-lg"
    >
      <div class="d-flex ga-2 mb-2">
        <v-text-field
          :model-value="row.target"
          :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapTarget')"
          density="compact"
          hide-details
          class="flex-grow-1"
          @update:model-value="patchMapping(row.id, { target: String($event ?? '') })"
        />
        <v-select
          :model-value="row.source"
          :items="mappingSourceItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapSource')"
          density="compact"
          hide-details
          style="max-width: 140px"
          @update:model-value="patchMapping(row.id, { source: $event })"
        />
        <v-btn
          icon="mdi-delete-outline"
          variant="text"
          size="small"
          color="error"
          @click="removeMapping(row.id)"
        />
      </div>
      <v-combobox
        v-if="row.source === 'field' || row.source === 'item'"
        :model-value="row.path"
        :items="pathSuggestions"
        item-title="title"
        item-value="value"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapPath')"
        density="compact"
        hide-details
        @update:model-value="
          patchMapping(row.id, {
            path: typeof $event === 'string' ? $event : String(($event as any)?.value ?? ''),
          })
        "
      />
      <v-text-field
        v-else-if="row.source === 'static'"
        :model-value="row.value"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapValue')"
        density="compact"
        hide-details
        @update:model-value="patchMapping(row.id, { value: String($event ?? '') })"
      />
      <template v-else-if="row.source === 'sequence'">
        <v-text-field
          :model-value="row.template"
          :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapSequenceTemplate')"
          :hint="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapSequenceTemplateHint')"
          persistent-hint
          density="compact"
          class="mb-2"
          @update:model-value="patchMapping(row.id, { template: String($event ?? '') })"
        />
        <div class="d-flex ga-2">
          <v-text-field
            :model-value="row.startFrom"
            :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapSequenceStartFrom')"
            type="number"
            density="compact"
            hide-details
            style="max-width: 140px"
            :disabled="!!(row.startFromPath || '').trim()"
            @update:model-value="patchMapping(row.id, { startFrom: String($event ?? '1') })"
          />
          <v-combobox
            :model-value="row.startFromPath"
            :items="pathSuggestions"
            item-title="title"
            item-value="value"
            :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapSequenceStartFromPath')"
            density="compact"
            hide-details
            clearable
            class="flex-grow-1"
            @update:model-value="
              patchMapping(row.id, {
                startFromPath:
                  typeof $event === 'string' ? $event : String(($event as any)?.value ?? ''),
              })
            "
          />
        </div>
      </template>
      <v-text-field
        v-else
        :model-value="row.template"
        :label="t('operationCore.workspaceDefinitions.rules.createDatasetRows.mapTemplate')"
        density="compact"
        hide-details
        @update:model-value="patchMapping(row.id, { template: String($event ?? '') })"
      />
    </div>

    <p v-if="!draft.fieldMappings.length" class="text-body-2 text-medium-emphasis mb-0">
      {{ t('operationCore.workspaceDefinitions.rules.createDatasetRows.mappingsEmpty') }}
    </p>
  </div>
</template>

<style scoped>
.oc-create-dataset-rows-editor__mapping {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-surface), 1);
}
</style>
