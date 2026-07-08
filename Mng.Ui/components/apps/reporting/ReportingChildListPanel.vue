<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { ReportingExpandChildListConfig } from '@/types/apps/reporting';
import { useAppI18n } from '@/composables/useAppI18n';
import { useDatasetStore } from '@/stores/apps/dataset';
import { fetchReportingChildList } from '@/utils/reportingChildList';
import { reportingCellDisplayValue } from '@/utils/reportingCellDisplay';
import {
  isReportingBoolField,
  normalizeReportingListColumn,
  parseReportingBoolValue,
  readReportingColumnValue,
  reportingColumnListKey,
  visibleReportingColumnKeys,
} from '@/utils/reportingListConfig';
import type { OdakHubListColumnConfig } from '@/utils/odakSiparisHubListConfig';

const props = defineProps<{
  parentRow: Record<string, unknown>;
  childList: ReportingExpandChildListConfig;
  active: boolean;
}>();

const { t } = useAppI18n();
const datasetStore = useDatasetStore();

const loading = ref(false);
const errorMessage = ref('');
const rows = ref<Record<string, unknown>[]>([]);
const totalCount = ref(0);
const loaded = ref(false);
const schemaFields = ref<FieldDefinition[]>([]);

const visibleColumns = computed((): OdakHubListColumnConfig[] => {
  const listConfig = props.childList.listConfig;
  const keys = new Set(visibleReportingColumnKeys(listConfig));
  return [...listConfig.columns]
    .map((c) => normalizeReportingListColumn({ ...c }))
    .filter((c) => c.visible && keys.has(reportingColumnListKey(c)))
    .sort((a, b) => a.order - b.order);
});

const headers = computed(() =>
  visibleColumns.value.map((col) => ({
    title: col.title ?? col.fieldName,
    key: reportingColumnListKey(col),
    width: col.width,
    sortable: false,
  }))
);

const tableItems = computed(() =>
  rows.value.map((row, index) => ({
    raw: row,
    _key: String(row.__dataId ?? row.dataId ?? index),
  }))
);

const emptyText = computed(
  () => props.childList.emptyMessage?.trim() || t('reporting.expand.childListEmpty')
);

async function loadSchema() {
  const name = props.childList.datasetName?.trim();
  if (!name || schemaFields.value.length) return;
  try {
    const ds = await datasetStore.fetchDatasetByName(name);
    schemaFields.value = ds?.fields ?? [];
  } catch {
    schemaFields.value = [];
  }
}

async function loadRows() {
  if (!props.active) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    await loadSchema();
    const result = await fetchReportingChildList({
      parentRow: props.parentRow,
      childList: props.childList,
    });
    rows.value = result.rows;
    totalCount.value = result.totalCount;
    loaded.value = true;
  } catch (e: unknown) {
    rows.value = [];
    totalCount.value = 0;
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.active, props.parentRow.__dataId, props.parentRow.dataId] as const,
  () => {
    if (props.active && !loaded.value) void loadRows();
  },
  { immediate: true }
);

watch(
  () => props.parentRow,
  () => {
    loaded.value = false;
    if (props.active) void loadRows();
  }
);

function cellRaw(row: Record<string, unknown>, col: OdakHubListColumnConfig): string {
  const val = readReportingColumnValue(row, col);
  if (val == null) return '';
  if (typeof val === 'object') return '';
  return String(val);
}

function cellDisplay(row: Record<string, unknown>, col: OdakHubListColumnConfig): string {
  const raw = cellRaw(row, col);
  return reportingCellDisplayValue(raw, col);
}

function isBoolColumn(col: OdakHubListColumnConfig): boolean {
  const key = reportingColumnListKey(col);
  return isReportingBoolField(schemaFields.value, key);
}

function boolCellValue(row: Record<string, unknown>, col: OdakHubListColumnConfig): boolean | null {
  const raw = col.relationDisplayField
    ? readReportingColumnValue(row, col)
    : row[col.fieldName];
  return parseReportingBoolValue(raw);
}
</script>

<template>
  <div class="reporting-child-list-panel">
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="tableItems"
      :loading="loading"
      item-value="_key"
      density="compact"
      class="border rounded-md bg-surface"
      :items-per-page="-1"
      hide-default-footer
    >
      <template v-for="col in visibleColumns" :key="reportingColumnListKey(col)" #[`item.${reportingColumnListKey(col)}`]="{ item }">
        <template v-if="isBoolColumn(col)">
          <v-icon
            v-if="boolCellValue(item.raw, col) === true"
            icon="mdi-check-circle"
            color="success"
            size="20"
          />
          <v-icon
            v-else-if="boolCellValue(item.raw, col) === false"
            icon="mdi-close-circle-outline"
            color="error"
            size="20"
          />
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <span v-else>{{ cellDisplay(item.raw, col) || '—' }}</span>
      </template>

      <template #no-data>
        <div class="text-body-2 text-medium-emphasis pa-4 text-center">
          {{ loading ? t('reporting.expand.childListLoading') : emptyText }}
        </div>
      </template>
    </v-data-table>

    <p v-if="!loading && totalCount > 0" class="text-caption text-medium-emphasis mt-2 mb-0">
      {{ t('reporting.expand.childListCount', { count: totalCount }) }}
    </p>
  </div>
</template>

<style scoped>
.reporting-child-list-panel {
  min-height: 80px;
}
</style>
