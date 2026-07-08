<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  DiDocumentDataSourceDetail,
  DiDocumentDataSourceSummary,
  DiTemplateDocBinding,
  DiTemplateParameter,
} from '@/types/apps/documentIntelligence';

type TableColumn = { sourceField: string; header?: string | null; format?: string | null };

const props = withDefaults(
  defineProps<{
    parameter: DiTemplateParameter;
    dataSource: DiDocumentDataSourceDetail | null;
    dataSources: DiDocumentDataSourceSummary[];
    dataSourcesLoading?: boolean;
    dataSourceLoading?: boolean;
    section?: 'all' | 'data' | 'binding';
  }>(),
  {
    section: 'all',
  }
);

const emit = defineEmits<{
  'update:label': [value: string];
  'update:dataSourceRef': [value: string | null];
  'update:docBinding': [field: keyof DiTemplateDocBinding, value: unknown];
  'update:columns': [columns: TableColumn[]];
}>();

const { t } = useAppI18n();

const showData = computed(() => props.section === 'all' || props.section === 'data');
const showBinding = computed(() => props.section === 'all' || props.section === 'binding');

const binding = computed<DiTemplateDocBinding>(() => ({
  regionKind: 'table',
  paragraphIndex: 0,
  ...props.parameter.docBinding,
}));

const dataSourceOptions = computed(() =>
  props.dataSources.map((ds) => ({
    title: `${ds.displayName} (${ds.code})`,
    value: ds.code,
  }))
);

const regionKindOptions = computed(() => [
  { title: t('documentIntelligence.designer.parameterStudio.regionTable'), value: 'table' },
  { title: t('documentIntelligence.designer.parameterStudio.regionSheet'), value: 'sheet' },
]);

const effectiveColumns = computed<TableColumn[]>(() => {
  const fromParam = props.parameter.valueSource?.columns;
  if (fromParam?.length) return fromParam.map((col) => ({ ...col }));
  return (props.dataSource?.columns ?? []).map((col) => ({ ...col }));
});

function formatJson(value: unknown): string {
  if (value == null) return '—';
  try {
    return JSON.stringify(value, null, 2);
  } catch {
    return String(value);
  }
}

function onColumnChange(index: number, field: keyof TableColumn, value: string) {
  const next = effectiveColumns.value.map((col, i) =>
    i === index ? { ...col, [field]: value || null } : col
  );
  emit('update:columns', next);
}

function addColumn() {
  emit('update:columns', [...effectiveColumns.value, { sourceField: '', header: '', format: '' }]);
}

function removeColumn(index: number) {
  emit(
    'update:columns',
    effectiveColumns.value.filter((_, i) => i !== index)
  );
}

function syncColumnsFromCatalog() {
  if (!props.dataSource?.columns.length) return;
  emit(
    'update:columns',
    props.dataSource.columns.map((col) => ({ ...col }))
  );
}
</script>

<template>
  <div>
    <v-alert
      v-if="showData"
      type="info"
      variant="tonal"
      density="compact"
      class="mb-4 rounded-lg"
    >
      {{ t('documentIntelligence.designer.parameterStudio.tableEditableHint') }}
    </v-alert>

    <template v-if="showData">
      <v-text-field
        :model-value="parameter.label"
        :label="t('documentIntelligence.designer.paramLabel')"
        density="compact"
        variant="outlined"
        hide-details
        class="mb-3"
        @update:model-value="emit('update:label', String($event ?? ''))"
      />

      <v-row dense class="mb-2">
        <v-col cols="12" md="6">
          <div class="text-caption text-medium-emphasis mb-1">
            {{ t('documentIntelligence.designer.parameterStudio.paramKind') }}
          </div>
          <v-chip size="small" color="secondary" variant="tonal">table</v-chip>
        </v-col>
        <v-col cols="12" md="6">
          <div class="text-caption text-medium-emphasis mb-1">
            {{ t('documentIntelligence.designer.parameterStudio.dataSourceRef') }}
          </div>
          <v-autocomplete
            :model-value="parameter.dataSourceRef ?? null"
            :items="dataSourceOptions"
            :loading="dataSourcesLoading"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            @update:model-value="emit('update:dataSourceRef', $event)"
          />
        </v-col>
      </v-row>

      <v-divider class="my-4" />

      <div class="text-subtitle-2 font-weight-bold mb-2">
        {{ t('documentIntelligence.designer.parameterStudio.dataSourceCatalog') }}
        <v-progress-circular v-if="dataSourceLoading" indeterminate size="16" width="2" class="ms-2" />
      </div>

      <template v-if="dataSource">
        <v-row dense>
          <v-col cols="12" md="6">
            <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.dsDisplayName') }}</div>
            <div>{{ dataSource.displayName }}</div>
          </v-col>
          <v-col cols="12" md="3">
            <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.dsMode') }}</div>
            <div>{{ dataSource.mode }}</div>
          </v-col>
          <v-col cols="12" md="3">
            <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.dsProvider') }}</div>
            <div>{{ dataSource.provider }}</div>
          </v-col>
          <v-col cols="12" md="6">
            <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.dsDataset') }}</div>
            <div><code>{{ dataSource.dataset ?? '—' }}</code></div>
          </v-col>
          <v-col cols="12" md="6">
            <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.dsQuery') }}</div>
            <div class="text-body-2">{{ dataSource.query ?? '—' }}</div>
          </v-col>
          <v-col cols="12">
            <div class="text-caption text-medium-emphasis">{{ t('documentIntelligence.designer.parameterStudio.dsMatch') }}</div>
            <pre class="di-json-block">{{ formatJson(dataSource.match) }}</pre>
          </v-col>
        </v-row>
      </template>

      <v-alert
        v-else-if="!dataSourceLoading && parameter.dataSourceRef"
        type="warning"
        variant="tonal"
        density="compact"
        class="rounded-lg"
      >
        {{ t('documentIntelligence.designer.parameterStudio.dataSourceMissing') }}
      </v-alert>

      <div class="d-flex align-center ga-2 mt-4 mb-2 flex-wrap">
        <div class="text-subtitle-2 font-weight-bold">
          {{ t('documentIntelligence.designer.parameterStudio.paramColumns') }}
          <v-chip size="x-small" variant="tonal" class="ms-2">{{ effectiveColumns.length }}</v-chip>
        </div>
        <v-spacer />
        <v-btn
          v-if="dataSource?.columns.length"
          size="x-small"
          variant="tonal"
          class="text-none"
          @click="syncColumnsFromCatalog"
        >
          {{ t('documentIntelligence.designer.parameterStudio.syncColumnsFromCatalog') }}
        </v-btn>
        <v-btn
          size="x-small"
          variant="tonal"
          color="primary"
          class="text-none"
          prepend-icon="mdi-plus"
          @click="addColumn"
        >
          {{ t('documentIntelligence.designer.parameterStudio.addColumn') }}
        </v-btn>
      </div>

      <p class="text-caption text-medium-emphasis mb-2">
        {{ t('documentIntelligence.designer.parameterStudio.paramColumnsHint') }}
      </p>

      <v-table density="compact" class="rounded-lg border">
        <thead>
          <tr>
            <th>{{ t('documentIntelligence.designer.parameterStudio.colSourceField') }}</th>
            <th>{{ t('documentIntelligence.designer.parameterStudio.colHeader') }}</th>
            <th>{{ t('documentIntelligence.designer.parameterStudio.colFormat') }}</th>
            <th style="width: 48px" />
          </tr>
        </thead>
        <tbody>
          <tr v-for="(col, index) in effectiveColumns" :key="`${col.sourceField}-${index}`">
            <td>
              <v-text-field
                :model-value="col.sourceField"
                density="compact"
                variant="plain"
                hide-details
                @update:model-value="onColumnChange(index, 'sourceField', String($event ?? ''))"
              />
            </td>
            <td>
              <v-text-field
                :model-value="col.header ?? ''"
                density="compact"
                variant="plain"
                hide-details
                @update:model-value="onColumnChange(index, 'header', String($event ?? ''))"
              />
            </td>
            <td>
              <v-text-field
                :model-value="col.format ?? ''"
                density="compact"
                variant="plain"
                hide-details
                placeholder="N0"
                @update:model-value="onColumnChange(index, 'format', String($event ?? ''))"
              />
            </td>
            <td>
              <v-btn
                icon="mdi-delete-outline"
                size="x-small"
                variant="text"
                color="error"
                @click="removeColumn(index)"
              />
            </td>
          </tr>
          <tr v-if="!effectiveColumns.length">
            <td colspan="4" class="text-medium-emphasis">
              {{ t('documentIntelligence.designer.parameterStudio.dsNoColumns') }}
            </td>
          </tr>
        </tbody>
      </v-table>
    </template>

    <template v-if="showBinding">
      <v-divider v-if="showData" class="my-4" />

      <div class="text-subtitle-2 font-weight-bold mb-3">
        {{ t('documentIntelligence.designer.parameterStudio.docBindingTitle') }}
      </div>

      <v-row dense>
        <v-col cols="12" md="6">
          <v-select
            :model-value="binding.regionKind"
            :items="regionKindOptions"
            :label="t('documentIntelligence.designer.parameterStudio.bindingRegion')"
            density="compact"
            variant="outlined"
            hide-details
            @update:model-value="emit('update:docBinding', 'regionKind', $event)"
          />
        </v-col>
        <v-col cols="12" md="6">
          <v-text-field
            :model-value="binding.tableIndex ?? 0"
            :label="t('documentIntelligence.designer.parameterStudio.bindingTableIndex')"
            type="number"
            min="0"
            density="compact"
            variant="outlined"
            hide-details
            @update:model-value="emit('update:docBinding', 'tableIndex', Number($event))"
          />
        </v-col>
        <v-col cols="12" md="6">
          <v-text-field
            :model-value="binding.headerRowIndex ?? 0"
            :label="t('documentIntelligence.designer.parameterStudio.bindingHeaderRow')"
            type="number"
            min="0"
            density="compact"
            variant="outlined"
            hide-details
            @update:model-value="emit('update:docBinding', 'headerRowIndex', Number($event))"
          />
        </v-col>
        <v-col cols="12" md="6">
          <v-text-field
            :model-value="binding.templateRowIndex ?? 1"
            :label="t('documentIntelligence.designer.parameterStudio.bindingTemplateRow')"
            type="number"
            min="0"
            density="compact"
            variant="outlined"
            hide-details
            @update:model-value="emit('update:docBinding', 'templateRowIndex', Number($event))"
          />
        </v-col>
      </v-row>
    </template>
  </div>
</template>

<style scoped>
.di-json-block {
  margin: 0;
  padding: 8px 12px;
  border-radius: 8px;
  background: rgba(var(--v-theme-on-surface), 0.04);
  font-size: 12px;
  overflow: auto;
  max-height: 160px;
}

.border {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
