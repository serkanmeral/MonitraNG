<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type { ReportingExpandChildListConfig } from '@/types/apps/reporting';
import { useAppI18n } from '@/composables/useAppI18n';
import { useDatasetStore } from '@/stores/apps/dataset';
import ReportingSummaryCards from '@/components/apps/reporting/ReportingSummaryCards.vue';
import ReportingSummaryFooter from '@/components/apps/reporting/ReportingSummaryFooter.vue';
import { buildReportingChildListFilters, fetchReportingChildList } from '@/utils/reportingChildList';
import { reportingCellDisplayValue } from '@/utils/reportingCellDisplay';
import { reportingRowId } from '@/utils/reportingExpandLayout';
import {
  columnConfigByField,
  isReportingBoolField,
  parseReportingBoolValue,
  readReportingColumnValue,
  reportingCellRawForColumn,
  reportingDataTableRow,
  reportingFieldLabel,
  visibleReportingColumnKeys,
} from '@/utils/reportingListConfig';
import {
  emptyReportingSummaryConfig,
  fetchReportingSummary,
  reportingSummaryShowCards,
  reportingSummaryShowFooter,
  type ReportingSummaryValues,
} from '@/utils/reportingSummary';

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
const summaryValues = ref<ReportingSummaryValues>({});
const summaryLoading = ref(false);

const summaryConfig = computed(
  () => props.childList.summary ?? emptyReportingSummaryConfig()
);

const fieldMap = computed(() => new Map(schemaFields.value.map((f) => [f.name, f])));

const visibleColumns = computed(() => visibleReportingColumnKeys(props.childList.listConfig));

const headers = computed(() =>
  visibleColumns.value.map((listKey) => {
    const col = columnConfigByField(props.childList.listConfig, listKey);
    const title =
      col?.title?.trim() ||
      reportingFieldLabel(fieldMap.value.get(col?.fieldName ?? listKey), listKey);
    return {
      title,
      key: listKey,
      width: col?.width,
      sortable: false,
    };
  })
);

const tableItems = computed(() =>
  rows.value.map((row, index) => ({
    ...row,
    __dataId: reportingRowId(row) || `child-row-${index}`,
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

async function loadSummary() {
  const cfg = summaryConfig.value;
  if (!cfg.metrics.length || cfg.placement === 'none') {
    summaryValues.value = {};
    return;
  }
  const filters = buildReportingChildListFilters(props.parentRow, props.childList);
  if (!filters.length) {
    summaryValues.value = {};
    return;
  }
  summaryLoading.value = true;
  try {
    summaryValues.value = await fetchReportingSummary({
      datasetName: props.childList.datasetName,
      metrics: cfg.metrics,
      filters,
    });
  } catch {
    summaryValues.value = {};
  } finally {
    summaryLoading.value = false;
  }
}

async function loadRows() {
  if (!props.active) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    await loadSchema();
    const [result] = await Promise.all([
      fetchReportingChildList({
        parentRow: props.parentRow,
        childList: props.childList,
      }),
      loadSummary(),
    ]);
    rows.value = result.rows;
    totalCount.value = result.totalCount;
    loaded.value = true;
  } catch (e: unknown) {
    rows.value = [];
    totalCount.value = 0;
    summaryValues.value = {};
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

function cellRaw(item: Record<string, unknown>, listKey: string): string {
  const row = reportingDataTableRow(item);
  const col = columnConfigByField(props.childList.listConfig, listKey);
  if (col) return reportingCellRawForColumn(row, col);
  return reportingCellRawForColumn(row, {
    fieldName: listKey,
    visible: true,
    order: 0,
    sortable: false,
    filterable: false,
  });
}

function cellDisplay(item: Record<string, unknown>, listKey: string): string {
  const col = columnConfigByField(props.childList.listConfig, listKey);
  return reportingCellDisplayValue(cellRaw(item, listKey), col);
}

function isBoolColumn(listKey: string): boolean {
  const col = columnConfigByField(props.childList.listConfig, listKey);
  const fieldName = col?.fieldName ?? listKey;
  return isReportingBoolField(schemaFields.value, fieldName);
}

function boolCellValue(item: Record<string, unknown>, listKey: string): boolean | null {
  const row = reportingDataTableRow(item);
  const col = columnConfigByField(props.childList.listConfig, listKey);
  const raw = col ? readReportingColumnValue(row, col) : row[listKey];
  return parseReportingBoolValue(raw);
}
</script>

<template>
  <div class="reporting-child-list-panel">
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <ReportingSummaryCards
      v-if="reportingSummaryShowCards(summaryConfig)"
      :config="summaryConfig"
      :values="summaryValues"
      :loading="summaryLoading"
    />

    <v-data-table
      :headers="headers"
      :items="tableItems"
      :loading="loading"
      item-value="__dataId"
      density="compact"
      class="border rounded-md bg-surface"
      :items-per-page="-1"
      hide-default-footer
    >
      <template v-for="col in visibleColumns" :key="col" #[`item.${col}`]="{ item }">
        <template v-if="isBoolColumn(col)">
          <v-icon
            v-if="boolCellValue(item, col) === true"
            icon="mdi-check-circle"
            color="success"
            size="20"
          />
          <v-icon
            v-else-if="boolCellValue(item, col) === false"
            icon="mdi-close-circle-outline"
            color="error"
            size="20"
          />
          <span v-else class="text-medium-emphasis">—</span>
        </template>
        <span v-else>{{ cellDisplay(item, col) || '—' }}</span>
      </template>

      <template #no-data>
        <div class="text-body-2 text-medium-emphasis pa-4 text-center">
          {{ loading ? t('reporting.expand.childListLoading') : emptyText }}
        </div>
      </template>
    </v-data-table>

    <ReportingSummaryFooter
      v-if="reportingSummaryShowFooter(summaryConfig)"
      :config="summaryConfig"
      :values="summaryValues"
      :loading="summaryLoading"
    />

    <p
      v-else-if="!loading && totalCount > 0 && !reportingSummaryShowFooter(summaryConfig)"
      class="text-caption text-medium-emphasis mt-2 mb-0"
    >
      {{ t('reporting.expand.childListCount', { count: totalCount }) }}
    </p>
  </div>
</template>

<style scoped>
.reporting-child-list-panel {
  min-height: 80px;
}
</style>
