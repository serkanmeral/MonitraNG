<script setup lang="ts">
import { computed, ref, toRef, watch } from 'vue';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type {
  ReportingDocumentBinding,
  ReportingExpandChildListConfig,
} from '@/types/apps/reporting';
import { useAppI18n } from '@/composables/useAppI18n';
import { useReportingColumnAccess } from '@/composables/useReportingColumnAccess';
import { useAuthStore } from '@/stores/auth';
import { useDatasetStore } from '@/stores/apps/dataset';
import ReportingSummaryCards from '@/components/apps/reporting/ReportingSummaryCards.vue';
import ReportingSummaryFooter from '@/components/apps/reporting/ReportingSummaryFooter.vue';
import { buildReportingChildListFilters, fetchReportingChildList } from '@/utils/reportingChildList';
import { reportingCellDisplayValue } from '@/utils/reportingCellDisplay';
import { reportingRowId } from '@/utils/reportingExpandLayout';
import {
  emptyOdakFieldPoliciesBlob,
  type OdakFieldPoliciesBlob,
} from '@/utils/odakSiparisFieldPolicies';
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
import { generateReportingChildRowDocument } from '@/utils/reportingChildRowDocument';
import { buildDiResourceUrl } from '@/utils/diResourceLink';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { FileTextIcon } from 'vue-tabler-icons';

const props = withDefaults(
  defineProps<{
    parentRow: Record<string, unknown>;
    childList: ReportingExpandChildListConfig;
    active: boolean;
    fieldPolicies?: OdakFieldPoliciesBlob;
    tabId?: string | null;
    reportId?: string | null;
    reportTitle?: string | null;
    parentListConfig?: OdakHubListConfig | null;
    documentBindings?: ReportingDocumentBinding[];
    enableChildRowDocuments?: boolean;
  }>(),
  {
    fieldPolicies: () => emptyOdakFieldPoliciesBlob(),
    tabId: null,
    reportId: null,
    reportTitle: null,
    parentListConfig: null,
    documentBindings: () => [],
    enableChildRowDocuments: false,
  }
);

const { t } = useAppI18n();
const authStore = useAuthStore();
const datasetStore = useDatasetStore();
const fieldPoliciesRef = toRef(props, 'fieldPolicies');
const { canViewColumn } = useReportingColumnAccess(fieldPoliciesRef);

const loading = ref(false);
const errorMessage = ref('');
const rows = ref<Record<string, unknown>[]>([]);
const totalCount = ref(0);
const loaded = ref(false);
const schemaFields = ref<FieldDefinition[]>([]);
const summaryValues = ref<ReportingSummaryValues>({});
const summaryLoading = ref(false);
const docGeneratingKey = ref<string | null>(null);
const docMessage = ref('');
const docError = ref('');
const lastDocResourceId = ref<string | null>(null);

const summaryConfig = computed(
  () => props.childList.summary ?? emptyReportingSummaryConfig()
);

const childRowBindings = computed(() => {
  if (!props.enableChildRowDocuments || !props.reportId) return [];
  return (props.documentBindings ?? []).filter((b) => {
    if (b.contextType !== 'childRow') return false;
    if (!b.childTabId) return true;
    return b.childTabId === props.tabId;
  });
});

const showChildDocs = computed(() => childRowBindings.value.length > 0 && !!props.parentListConfig);

const fieldMap = computed(() => new Map(schemaFields.value.map((f) => [f.name, f])));

const visibleColumns = computed(() =>
  visibleReportingColumnKeys(props.childList.listConfig, (field) => canViewColumn(field))
);

const headers = computed(() => {
  const cols = visibleColumns.value.map((listKey) => {
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
  });
  if (showChildDocs.value) {
    cols.push({
      title: t('reporting.runner.historyColActions'),
      key: '__docs',
      width: 180,
      sortable: false,
    });
  }
  return cols;
});

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
        canViewColumn: (field) => canViewColumn(field),
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

function openLastDocInDi() {
  if (!lastDocResourceId.value) return;
  void navigateTo(buildDiResourceUrl(lastDocResourceId.value));
}

function genKey(binding: ReportingDocumentBinding, item: Record<string, unknown>) {
  return `${binding.id}:${reportingRowId(reportingDataTableRow(item))}`;
}

async function generateCert(binding: ReportingDocumentBinding, item: Record<string, unknown>) {
  if (!props.reportId || !props.parentListConfig) return;
  const row = reportingDataTableRow(item);
  docGeneratingKey.value = genKey(binding, item);
  docError.value = '';
  docMessage.value = '';
  lastDocResourceId.value = null;
  try {
    await authStore.ensureValidToken();
    const result = await generateReportingChildRowDocument({
      reportId: props.reportId,
      reportTitle: props.reportTitle ?? '',
      binding,
      parentRow: props.parentRow,
      parentListConfig: props.parentListConfig,
      childRow: row,
      childListConfig: props.childList.listConfig,
    });
    lastDocResourceId.value = result.resourceId || null;
    docMessage.value = t('reporting.runner.generateSuccess', {
      fileName: result.fileName || binding.label,
    });
  } catch (e: unknown) {
    docError.value =
      e instanceof Error ? e.message : t('reporting.runner.generateFailed');
  } finally {
    docGeneratingKey.value = null;
  }
}
</script>

<template>
  <div class="reporting-child-list-panel">
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>
    <v-alert v-if="docError" type="error" variant="tonal" density="compact" class="mb-3">
      {{ docError }}
    </v-alert>
    <v-alert v-if="docMessage" type="success" variant="tonal" density="compact" class="mb-3">
      {{ docMessage }}
      <v-btn
        v-if="lastDocResourceId"
        class="ml-2"
        size="small"
        variant="text"
        @click="openLastDocInDi"
      >
        {{ t('reporting.runner.openInDi') }}
      </v-btn>
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
        <template v-if="!canViewColumn(col, item)">
          <span class="text-medium-emphasis">—</span>
        </template>
        <template v-else-if="isBoolColumn(col)">
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

      <template v-if="showChildDocs" #item.__docs="{ item }">
        <div class="d-flex flex-wrap ga-1 justify-end">
          <v-btn
            v-for="b in childRowBindings"
            :key="b.id"
            size="x-small"
            variant="tonal"
            color="secondary"
            class="text-none"
            :loading="docGeneratingKey === genKey(b, item)"
            :disabled="!!docGeneratingKey"
            @click="generateCert(b, item)"
          >
            <FileTextIcon size="12" class="mr-1" />
            {{ b.label }}
          </v-btn>
        </div>
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
