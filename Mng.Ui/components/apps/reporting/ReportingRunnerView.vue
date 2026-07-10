<script setup lang="ts">
/**
 * Salt okunur rapor çalıştırıcı — katalog tanımından tablo + filtre + expand.
 */
import ReportingExpandPanel from '@/components/apps/reporting/ReportingExpandPanel.vue';
import ReportingParametersPanel from '@/components/apps/reporting/ReportingParametersPanel.vue';
import ReportingSummaryCards from '@/components/apps/reporting/ReportingSummaryCards.vue';
import ReportingSummaryFooter from '@/components/apps/reporting/ReportingSummaryFooter.vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useReportingColumnAccess } from '@/composables/useReportingColumnAccess';
import {
  ReportingCatalogService,
  reportingDomainKey,
} from '@/services/reportingCatalogService';
import { fetchReportingPreview } from '@/services/reportingService';
import { useAuthStore } from '@/stores/auth';
import { useDatasetStore, type FieldDefinition } from '@/stores/apps/dataset';
import { draftFromReportDefinition } from '@/utils/reportingCatalogStorage';
import { canViewReportingReport } from '@/utils/reportingReportAccess';
import { cloneAfListFilters } from '@/utils/reportingDefaultFilters';
import { exportReportingRowsToCsv } from '@/utils/reportingExport';
import { ODAK_DATA_TABLE_EXPAND_COLUMN } from '@/utils/odakSiparisConfig';
import {
  getListColumnCellStyle,
  isListColumnTextTruncated,
} from '@/utils/afListColumnFormat';
import type { AfListFilter } from '@/utils/afListFilters';
import {
  buildReportingFilterColumns,
  buildReportingListHeaders,
  columnConfigByField,
  isReportingBoolField,
  normalizeReportingListConfig,
  parseReportingBoolValue,
  readReportingColumnValue,
  reportingCellRawForColumn,
  reportingDataTableRow,
  visibleReportingColumnKeys,
} from '@/utils/reportingListConfig';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { reportingRowId, defaultReportingExpandConfigFromFields } from '@/utils/reportingExpandLayout';
import {
  emptyReportingSummaryConfig,
  fetchReportingSummary,
  reportingSummaryShowCards,
  reportingSummaryShowFooter,
  type ReportingSummaryValues,
} from '@/utils/reportingSummary';
import type { ReportingSummaryConfig } from '@/types/apps/reporting';
import { ensureOdakEgitimParticipantsExpandTab } from '@/utils/reportingOdakEgitimExpandMigrations';
import {
  buildReportingRuntimeQuery,
  defaultReportingParameterValues,
  reportingParameterSearchText,
  reportingParametersReady,
} from '@/utils/reportingParameters';
import type { ReportingParameterValues } from '@/utils/reportingParameterValueKeys';
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { reportingCellDisplayValue } from '@/utils/reportingCellDisplay';
import { computed, onMounted, ref, watch } from 'vue';
import { ArrowLeftIcon, DownloadIcon, RefreshIcon, PencilIcon } from 'vue-tabler-icons';

const props = defineProps<{
  reportId: string;
}>();

const route = useRoute();
const router = useRouter();
const authStore = useAuthStore();
const datasetStore = useDatasetStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: Record<string, unknown>) => {
  if (i18n?.t) return i18n.t(key, params);
  if (i18n?.global?.t) return i18n.global.t(key, params);
  return key;
};

const catalogDomainKey = computed(() =>
  reportingDomainKey(authStore.userInfo?.domain_id, authStore.userInfo?.domain_name)
);

const catalogService = computed(() => new ReportingCatalogService(catalogDomainKey.value));

const report = computed(() => catalogService.value.getReport(props.reportId));
const accessDenied = ref(false);
const notFound = ref(false);

const title = ref('');
const description = ref('');
const datasetName = ref('');
const listConfig = ref(report.value?.listConfig ?? { columns: [] });
const expandConfig = ref(defaultReportingExpandConfigFromFields([]));
const summaryConfig = ref<ReportingSummaryConfig>(emptyReportingSummaryConfig());
const summaryValues = ref<ReportingSummaryValues>({});
const summaryLoading = ref(false);
const fieldPolicies = ref(emptyOdakFieldPoliciesBlob());
const defaultFilters = ref<AfListFilter[]>([]);
const reportParameters = ref(report.value?.parameters ?? []);
const parameterValues = ref<ReportingParameterValues>({});
const advancedFilters = ref<AfListFilter[]>([]);
const runtimeFiltersKey = ref(0);

const { canViewColumn } = useReportingColumnAccess(fieldPolicies);

const schemaFields = ref<FieldDefinition[]>([]);
const schemaLoading = ref(false);
const runLoading = ref(false);
const runRows = ref<Record<string, unknown>[]>([]);
const runTotal = ref(0);
const runError = ref<string | null>(null);
const dgQuery = ref<unknown>(null);
const dgRequestUrl = ref<string | null>(null);
/** When true, next run requests showQuery=true and shows the pipeline panel. */
const showDgQueryPanel = ref(false);
const dgQueryExpanded = ref<string | undefined>(undefined);
const exporting = ref(false);
const tablePage = ref(1);
const itemsPerPage = ref(50);
const itemsPerPageOptions = [25, 50, 100];
const tableSortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([]);
const sortInitialized = ref(false);
const expandedIds = ref<string[]>([]);

const page = computed(() => ({ title: title.value || t('reporting.runner.title') }));
const breadcrumbs = computed(() => [
  { text: t('reporting.breadcrumbs.home'), disabled: false, href: '/' },
  { text: t('reporting.breadcrumbs.reporting'), disabled: false, href: '/apps/reporting' },
  { text: title.value || t('reporting.runner.title'), disabled: true, href: '#' },
]);

const filterColumns = computed(() =>
  buildReportingFilterColumns(listConfig.value, schemaFields.value ?? [], (field) => canViewColumn(field))
);

/** Gelişmiş filtre paneli — rapor parametrelerinden bağımsız; filtrelenebilir sütun varsa gösterilir. */
const showAdvancedFilterPanel = computed(() => filterColumns.value.length > 0);

const tableHeaders = computed(() => {
  const headers = buildReportingListHeaders(listConfig.value, schemaFields.value ?? [], (field) =>
    canViewColumn(field)
  );
  if (expandConfig.value.enabled) {
    return [{ ...ODAK_DATA_TABLE_EXPAND_COLUMN }, ...headers];
  }
  return headers;
});

const tableItems = computed(() =>
  runRows.value.map((row, index) => ({
    ...row,
    __dataId: reportingRowId(row) || `run-row-${index}`,
  }))
);

const visibleColumns = computed(() =>
  visibleReportingColumnKeys(listConfig.value, (field) => canViewColumn(field))
);

function cellRaw(item: Record<string, unknown>, listKey: string): string {
  const col = columnConfigByField(listConfig.value, listKey);
  if (col) return reportingCellRawForColumn(item, col);
  return reportingCellRawForColumn(item, { fieldName: listKey, visible: true, order: 0, sortable: false, filterable: false });
}

const columnTitleMap = computed(() => {
  const map: Record<string, string> = {};
  for (const h of tableHeaders.value) {
    if (h.key !== ODAK_DATA_TABLE_EXPAND_COLUMN.key) {
      map[h.key] = h.title;
    }
  }
  return map;
});

function hydrateFromReport() {
  bootstrapReportingCatalog(catalogDomainKey.value);
  const r = catalogService.value.getReport(props.reportId);
  if (!r) {
    notFound.value = true;
    return;
  }
  if (!canViewReportingReport(r.visibilityPolicies, authStore.userGroups)) {
    accessDenied.value = true;
    return;
  }
  const draft = draftFromReportDefinition(r);
  title.value = draft.title;
  description.value = draft.description ?? '';
  datasetName.value = draft.datasetName;
  listConfig.value = draft.listConfig;
  normalizeReportingListConfig(listConfig.value);

  const expandMigrated = ensureOdakEgitimParticipantsExpandTab(
    JSON.parse(JSON.stringify(draft.expand)) as typeof draft.expand,
    draft.datasetName
  );
  expandConfig.value = expandMigrated.expand;
  if (expandMigrated.changed) {
    catalogService.value.saveReport({
      ...r,
      expand: expandMigrated.expand,
      updatedAt: new Date().toISOString(),
    });
  }

  summaryConfig.value = draft.summary ?? emptyReportingSummaryConfig();
  fieldPolicies.value = draft.fieldPolicies;
  defaultFilters.value = draft.defaultFilters;
  reportParameters.value = draft.parameters ?? [];
  parameterValues.value = defaultReportingParameterValues(reportParameters.value);

  const personFromQuery = route.query.personId;
  if (typeof personFromQuery === 'string' && personFromQuery.trim()) {
    const personParam = reportParameters.value.find((p) => p.type === 'person');
    if (personParam) {
      parameterValues.value = { ...parameterValues.value, [personParam.id]: personFromQuery.trim() };
    }
  }

  applyRuntimeFiltersFromDefaults();
}

function applyRuntimeFiltersFromDefaults() {
  advancedFilters.value = cloneAfListFilters(defaultFilters.value);
  runtimeFiltersKey.value += 1;
}

function applyDefaultSortFromConfig() {
  const field = listConfig.value.defaultSortBy?.trim();
  if (!field) {
    tableSortBy.value = [];
    return;
  }
  const col = listConfig.value.columns.find((c) => c.fieldName === field);
  if (!col?.sortable) {
    tableSortBy.value = [];
    return;
  }
  tableSortBy.value = [
    {
      key: field,
      order: listConfig.value.defaultSortOrder === 'asc' ? 'asc' : 'desc',
    },
  ];
}

function currentSortField(): string {
  const s = tableSortBy.value[0];
  if (!s?.key) return listConfig.value.defaultSortBy ?? '';
  const col = columnConfigByField(listConfig.value, s.key);
  if (col && !col.sortable) return listConfig.value.defaultSortBy ?? s.key;
  return s.key;
}

function currentSortDesc(): boolean {
  const s = tableSortBy.value[0];
  if (!s) return listConfig.value.defaultSortOrder !== 'asc';
  return s.order === 'desc';
}

function cellDisplay(raw: string, fieldName: string): string {
  const col = columnConfigByField(listConfig.value, fieldName);
  return reportingCellDisplayValue(raw, col);
}

function cellTitle(item: Record<string, unknown>, listKey: string): string | undefined {
  const col = columnConfigByField(listConfig.value, listKey);
  if (!col) return undefined;
  const full = cellRaw(item, listKey);
  return isListColumnTextTruncated(full, col?.format) ? full : undefined;
}

function cellStyle(fieldName: string, raw: string, row: Record<string, unknown>): Record<string, string> {
  const col = columnConfigByField(listConfig.value, fieldName);
  return getListColumnCellStyle(raw, fieldName, col?.format, row);
}

function isBoolColumn(fieldName: string): boolean {
  return isReportingBoolField(schemaFields.value ?? [], fieldName);
}

function boolCellValue(item: Record<string, unknown>, listKey: string): boolean | null {
  const col = columnConfigByField(listConfig.value, listKey);
  const raw = col ? readReportingColumnValue(item, col) : item[listKey];
  return parseReportingBoolValue(raw);
}

function runtimeQuery() {
  return buildReportingRuntimeQuery(
    reportParameters.value,
    parameterValues.value,
    defaultFilters.value,
    advancedFilters.value
  );
}

async function loadSchema() {
  const name = datasetName.value?.trim();
  if (!name) return;
  schemaLoading.value = true;
  runError.value = null;
  try {
    await authStore.ensureValidToken();
    const ds = await datasetStore.fetchDatasetByName(name);
    schemaFields.value = ds?.fields ?? [];
    applyDefaultSortFromConfig();
    sortInitialized.value = true;
  } catch (e: unknown) {
    schemaFields.value = [];
    runError.value = e instanceof Error ? e.message : t('reporting.errors.schemaLoad');
  } finally {
    schemaLoading.value = false;
  }
}

async function runReport() {
  if (!datasetName.value) {
    runError.value = t('reporting.errors.pickDataset');
    return;
  }
  if (!visibleColumns.value.length) {
    runError.value = t('reporting.errors.pickColumns');
    return;
  }
  if (!reportingParametersReady(reportParameters.value, parameterValues.value)) {
    runError.value = t('reporting.runner.parametersRequired');
    return;
  }

  runLoading.value = true;
  runError.value = null;
  try {
    await authStore.ensureValidToken();
    const skip = (tablePage.value - 1) * itemsPerPage.value;
    const query = runtimeQuery();
    const summaryPromise =
      summaryConfig.value.metrics.length && summaryConfig.value.placement !== 'none'
        ? (async () => {
            summaryLoading.value = true;
            try {
              summaryValues.value = await fetchReportingSummary({
                datasetName: datasetName.value,
                metrics: summaryConfig.value.metrics,
                filters: query.filters,
              });
            } catch {
              summaryValues.value = {};
            } finally {
              summaryLoading.value = false;
            }
          })()
        : Promise.resolve();

    const [result] = await Promise.all([
      fetchReportingPreview({
        datasetName: datasetName.value,
        listConfig: listConfig.value,
        expandConfig: expandConfig.value,
        canViewColumn: (field) => canViewColumn(field),
        advancedFilters: query.filters,
        mongoMatch: query.mongoMatch,
        search: reportingParameterSearchText(reportParameters.value, parameterValues.value),
        sortField: currentSortField(),
        sortDesc: currentSortDesc(),
        skip,
        limit: itemsPerPage.value,
        expand: true,
        showQuery: showDgQueryPanel.value,
      }),
      summaryPromise,
    ]);
    runRows.value = result.rows;
    runTotal.value = result.totalCount;
    dgQuery.value = showDgQueryPanel.value ? (result.dgQuery ?? null) : null;
    dgRequestUrl.value = showDgQueryPanel.value ? (result.requestUrl ?? null) : null;
    expandedIds.value = [];
  } catch (e: unknown) {
    runRows.value = [];
    runTotal.value = 0;
    summaryValues.value = {};
    dgQuery.value = null;
    dgRequestUrl.value = null;
    runError.value = e instanceof Error ? e.message : t('reporting.errors.previewFailed');
  } finally {
    runLoading.value = false;
  }
}

async function onShowDgQueryPanelChange(enabled: boolean | null) {
  if (!enabled) {
    dgQuery.value = null;
    dgRequestUrl.value = null;
    dgQueryExpanded.value = undefined;
    return;
  }
  dgQueryExpanded.value = 'pipeline';
  if (dgQuery.value != null) return;
  if (
    reportingParametersReady(reportParameters.value, parameterValues.value) &&
    visibleColumns.value.length &&
    datasetName.value
  ) {
    await runReport();
  }
}

function onParametersRun(values: ReportingParameterValues) {
  parameterValues.value = values;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void runReport();
}

function onAdvancedFiltersUpdate(filters: AfListFilter[]) {
  advancedFilters.value = filters;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void runReport();
}

function onTableOptions(options: {
  page: number;
  itemsPerPage: number;
  sortBy: { key: string; order: 'asc' | 'desc' }[];
}) {
  if (!sortInitialized.value) return;

  const pageChanged = options.page !== tablePage.value;
  const sizeChanged = options.itemsPerPage !== itemsPerPage.value;
  const sortChanged = JSON.stringify(options.sortBy) !== JSON.stringify(tableSortBy.value);

  tablePage.value = options.page;
  itemsPerPage.value = options.itemsPerPage;
  tableSortBy.value = options.sortBy ?? [];

  if (pageChanged || sizeChanged || sortChanged) {
    void runReport();
  }
}

function resetRuntimeFiltersToDefaults() {
  applyRuntimeFiltersFromDefaults();
  if (tablePage.value !== 1) tablePage.value = 1;
  else void runReport();
}

function goBackToCatalog() {
  const categoryId = report.value?.categoryId;
  void router.push({
    path: '/apps/reporting',
    query: categoryId ? { categoryId } : undefined,
  });
}

function openDesigner() {
  void router.push(`/apps/reporting/designer/${props.reportId}`);
}

async function exportCsv() {
  if (!runRows.value.length) return;
  exporting.value = true;
  try {
    const safeName = title.value.replace(/[^\w\-]+/g, '_').slice(0, 48) || 'report';
    exportReportingRowsToCsv(runRows.value, listConfig.value, columnTitleMap.value, `${safeName}.csv`);
  } finally {
    exporting.value = false;
  }
}

watch(
  () => catalogDomainKey.value,
  () => {
    hydrateFromReport();
  }
);

watch(
  () => props.reportId,
  () => {
    hydrateFromReport();
    void loadSchema().then(() => {
      if (reportingParametersReady(reportParameters.value, parameterValues.value)) {
        void runReport();
      }
    });
  }
);

onMounted(async () => {
  await authStore.ensureValidToken();
  hydrateFromReport();
  void loadSchema().then(() => {
    if (reportingParametersReady(reportParameters.value, parameterValues.value)) {
      void runReport();
    }
  });
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-alert v-if="notFound" type="warning" variant="tonal" class="mb-4">
      {{ t('reporting.runner.notFound') }}
      <v-btn class="ml-2" size="small" variant="text" @click="goBackToCatalog">
        {{ t('reporting.catalog.backToList') }}
      </v-btn>
    </v-alert>

    <v-alert v-else-if="accessDenied" type="error" variant="tonal" class="mb-4">
      {{ t('reporting.errors.accessDenied') }}
      <v-btn class="ml-2" size="small" variant="text" @click="goBackToCatalog">
        {{ t('reporting.catalog.backToList') }}
      </v-btn>
    </v-alert>

    <template v-else>
      <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-3">
        <v-btn variant="text" size="small" class="text-none px-0" @click="goBackToCatalog">
          <ArrowLeftIcon size="16" class="mr-1" />
          {{ t('reporting.catalog.backToList') }}
        </v-btn>
        <div class="d-flex flex-wrap ga-2">
          <v-btn variant="tonal" size="small" class="text-none" @click="openDesigner">
            <PencilIcon size="16" class="mr-1" />
            {{ t('reporting.runner.editDesign') }}
          </v-btn>
          <v-btn
            variant="tonal"
            size="small"
            class="text-none"
            :loading="exporting"
            :disabled="!runRows.length"
            @click="exportCsv"
          >
            <DownloadIcon size="16" class="mr-1" />
            {{ t('reporting.runner.exportCsv') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            size="small"
            class="text-none"
            :loading="runLoading"
            @click="runReport"
          >
            <RefreshIcon size="16" class="mr-1" />
            {{ t('reporting.actions.run') }}
          </v-btn>
        </div>
      </div>

      <v-alert v-if="description" type="info" variant="tonal" density="comfortable" class="mb-4">
        {{ description }}
      </v-alert>

      <ReportingParametersPanel
        v-model="parameterValues"
        :parameters="reportParameters"
        :disabled="runLoading"
        @run="onParametersRun"
      />

      <v-card elevation="0" class="border">
        <v-card-title class="d-flex align-center flex-wrap ga-2 py-3">
          <span class="text-subtitle-1 font-weight-medium">{{ title }}</span>
          <v-spacer />
        </v-card-title>

        <v-divider />

        <div v-if="showAdvancedFilterPanel" class="px-4 pt-3">
          <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-2">
            <span class="text-subtitle-2 font-weight-medium">
              {{ t('reporting.runner.advancedFiltersTitle') }}
            </span>
            <v-btn
              v-if="defaultFilters.length"
              size="small"
              variant="tonal"
              @click="resetRuntimeFiltersToDefaults"
            >
              {{ t('reporting.defaultFilters.applyToPreview') }}
            </v-btn>
          </div>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ t('reporting.runner.advancedFiltersHint') }}
          </p>
          <AfListFilters
            :key="runtimeFiltersKey"
            :columns="filterColumns"
            :initial-filters="cloneAfListFilters(advancedFilters)"
            :initial-panel-open="advancedFilters.length > 0"
            @update:filters="onAdvancedFiltersUpdate"
          />
        </div>

        <v-card-text>
          <v-progress-linear v-if="schemaLoading" indeterminate color="primary" class="mb-3" />

          <v-alert v-if="runError" type="error" variant="tonal" class="mb-3">
            {{ runError }}
          </v-alert>

          <div
            v-if="reportingParametersReady(reportParameters, parameterValues)"
            class="d-flex flex-wrap align-center ga-3 mb-3"
          >
            <v-switch
              v-model="showDgQueryPanel"
              :label="t('reporting.runner.showDgQuery')"
              color="primary"
              density="compact"
              hide-details
              :disabled="runLoading"
              @update:model-value="onShowDgQueryPanelChange"
            />
          </div>

          <v-expansion-panels
            v-if="showDgQueryPanel && dgQuery != null"
            v-model="dgQueryExpanded"
            class="mb-3"
            variant="accordion"
          >
            <v-expansion-panel value="pipeline">
              <v-expansion-panel-title>{{ t('reporting.runner.dgQueryTitle') }}</v-expansion-panel-title>
              <v-expansion-panel-text>
                <p v-if="dgRequestUrl" class="text-caption text-medium-emphasis mb-2 text-break">
                  {{ dgRequestUrl }}
                </p>
                <pre class="text-caption overflow-auto">{{ JSON.stringify(dgQuery, null, 2) }}</pre>
              </v-expansion-panel-text>
            </v-expansion-panel>
          </v-expansion-panels>

          <v-alert
            v-if="!reportingParametersReady(reportParameters, parameterValues)"
            type="info"
            variant="tonal"
            class="mb-3"
          >
            {{ t('reporting.runner.selectParameters') }}
          </v-alert>

          <div
            v-else-if="!visibleColumns.length && !runLoading"
            class="text-medium-emphasis text-body-2 py-8 text-center"
          >
            {{ t('reporting.hints.emptyPreview') }}
          </div>

          <template v-else-if="reportingParametersReady(reportParameters, parameterValues)">
            <ReportingSummaryCards
              v-if="reportingSummaryShowCards(summaryConfig)"
              :config="summaryConfig"
              :values="summaryValues"
              :loading="summaryLoading"
            />

            <v-data-table-server
              v-model:expanded="expandedIds"
              :headers="tableHeaders"
              :items="tableItems"
              :loading="runLoading"
              :items-per-page="itemsPerPage"
              :items-per-page-options="itemsPerPageOptions"
              :page="tablePage"
              :items-length="runTotal"
              :sort-by="tableSortBy"
              :show-expand="expandConfig.enabled"
              :expand-on-click="false"
              item-value="__dataId"
              density="compact"
              class="border rounded"
              @update:options="onTableOptions"
            >
            <template v-for="col in visibleColumns" #[`item.${col}`]="{ item }">
              <span
                :key="col"
                class="d-inline-flex align-center"
                    :style="isBoolColumn(col) ? undefined : cellStyle(col, cellRaw(item, col), item)"
              >
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
                    <template v-else>
                      <span :title="cellTitle(item, col)">
                        {{ cellDisplay(cellRaw(item, col), col) }}
                      </span>
                    </template>
              </span>
            </template>

            <template v-if="expandConfig.enabled" #expanded-row="{ columns, item }">
              <tr>
                <td :colspan="columns.length" class="pa-0">
                  <ReportingExpandPanel
                    :key="reportingRowId(reportingDataTableRow(item))"
                    :row="reportingDataTableRow(item)"
                    :expand-config="expandConfig"
                    :dataset-name="datasetName"
                    :fields="schemaFields ?? []"
                    :list-config="listConfig"
                    :can-view-field="canViewColumn"
                  />
                </td>
              </tr>
            </template>
          </v-data-table-server>

            <ReportingSummaryFooter
              v-if="reportingSummaryShowFooter(summaryConfig)"
              :config="summaryConfig"
              :values="summaryValues"
              :loading="summaryLoading"
            />
          </template>
        </v-card-text>
      </v-card>
    </template>
  </div>
</template>
