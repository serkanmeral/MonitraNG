<script setup lang="ts">
/**
 * Reporting designer — listConfig, filters, column auth, save to local catalog.
 */
import ReportingColumnAuthPanel from '@/components/apps/reporting/ReportingColumnAuthPanel.vue';
import ReportingDocumentBindingsPanel from '@/components/apps/reporting/ReportingDocumentBindingsPanel.vue';
import ReportingParametersDesignerPanel from '@/components/apps/reporting/ReportingParametersDesignerPanel.vue';
import ReportingReportVisibilityPanel from '@/components/apps/reporting/ReportingReportVisibilityPanel.vue';
import { useReportingColumnAccess } from '@/composables/useReportingColumnAccess';
import { canViewReportingReport } from '@/utils/reportingReportAccess';
import {
  ReportingCatalogService,
  reportingDomainKey,
} from '@/services/reportingCatalogService';
import {
  draftFromReportDefinition,
  freshReportConfigFromSchema,
  reportFromDraft,
} from '@/utils/reportingCatalogStorage';
import { ReportingCategoryService } from '@/services/reportingCategoryService';
import { flattenReportingCategoryOptions } from '@/utils/reportingCategoryTree';
import type { OdakFieldVisibilityPolicy } from '@/utils/odakSiparisFieldPolicies';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import {
  cloneAfListFilters,
  sanitizeReportingDefaultFilters,
} from '@/utils/reportingDefaultFilters';
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { useDisplay } from 'vuetify';
import ReportingParametersPanel from '@/components/apps/reporting/ReportingParametersPanel.vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import ReportingListColumnsPanel from '@/components/apps/reporting/ReportingListColumnsPanel.vue';
import ReportingExpandLayoutPanel from '@/components/apps/reporting/ReportingExpandLayoutPanel.vue';
import ReportingExpandPanel from '@/components/apps/reporting/ReportingExpandPanel.vue';
import ReportingSummaryCards from '@/components/apps/reporting/ReportingSummaryCards.vue';
import ReportingSummaryFooter from '@/components/apps/reporting/ReportingSummaryFooter.vue';
import ReportingSummaryDesignerPanel from '@/components/apps/reporting/ReportingSummaryDesignerPanel.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAuthStore } from '@/stores/auth';
import { useDatasetStore, type FieldDefinition } from '@/stores/apps/dataset';
import { fetchReportingPreview } from '@/services/reportingService';
import type { AfListFilter } from '@/utils/afListFilters';
import {
  applyListColumnFormatting,
  getListColumnCellStyle,
  isListColumnTextTruncated,
} from '@/utils/afListColumnFormat';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import { ODAK_DATA_TABLE_EXPAND_COLUMN } from '@/utils/odakSiparisConfig';
import {
  buildReportingFilterColumns,
  buildReportingListHeaders,
  columnConfigByField,
  defaultReportingListConfigFromFields,
  isReportingBoolField,
  normalizeReportingListConfig,
  parseReportingBoolValue,
  readReportingColumnValue,
  reportingCellRawForColumn,
  reportingDataTableRow,
  visibleReportingColumnKeys,
} from '@/utils/reportingListConfig';
import type { ReportingDocumentBinding, ReportingExpandConfig, ReportingReportParameter, ReportingSummaryConfig } from '@/types/apps/reporting';
import {
  emptyReportingSummaryConfig,
  fetchReportingSummary,
  reportingSummarySearchableTextFields,
  reportingSummaryShowCards,
  reportingSummaryShowFooter,
  type ReportingSummaryValues,
} from '@/utils/reportingSummary';
import {
  buildReportingRuntimeQuery,
  defaultReportingParameterValues,
  reportingParameterSearchText,
  reportingParametersReady,
} from '@/utils/reportingParameters';
import type { ReportingParameterValues } from '@/utils/reportingParameterValueKeys';
import {
  defaultReportingExpandConfigFromFields,
  reportingRowId,
} from '@/utils/reportingExpandLayout';
import { ensureOdakEgitimParticipantsExpandTab } from '@/utils/reportingOdakEgitimExpandMigrations';
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { PlayerPlayIcon, RefreshIcon, DeviceFloppyIcon, ArrowLeftIcon } from 'vue-tabler-icons';

const props = defineProps<{
  reportId?: string | null;
}>();

const route = useRoute();
const router = useRouter();
const { mdAndUp } = useDisplay();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: Record<string, unknown>) => {
  if (i18n?.t) return i18n.t(key, params);
  if (i18n?.global?.t) return i18n.global.t(key, params);
  return key;
};

const authStore = useAuthStore();
const datasetStore = useDatasetStore();

const contentTabItems = computed(() =>
  (
    [
      'design',
      'view',
      'columns',
      'expand',
      'summary',
      'columnAuth',
      'parameters',
      'documentBindings',
      'reportAuth',
    ] as const
  ).map((key) => ({
    key,
    label: t(`reporting.tabs.${key}`),
  }))
);

const catalogService = computed(() => new ReportingCatalogService(domainKey.value));

const categoryService = computed(() => new ReportingCategoryService(domainKey.value));

const resolvedReportId = computed(() => {
  const raw = props.reportId ?? (route.params.id as string | string[] | undefined);
  const id = Array.isArray(raw) ? raw[0] : raw;
  const trimmed = id != null ? String(id).trim() : '';
  return trimmed || null;
});
const existingReport = computed(() =>
  resolvedReportId.value ? catalogService.value.getReport(resolvedReportId.value) : undefined
);

const page = computed(() => ({
  title: resolvedReportId.value ? t('reporting.designer.editTitle') : t('reporting.designer.newTitle'),
}));
const breadcrumbs = computed(() => [
  { text: t('reporting.breadcrumbs.home'), disabled: false, href: '/' },
  { text: t('reporting.breadcrumbs.reporting'), disabled: false, href: '/apps/reporting' },
  { text: title.value || page.value.title, disabled: true, href: '#' },
]);

const title = ref(t('reporting.draft.defaultTitle'));
const description = ref('');
const categoryId = ref<string | null>(null);
const datasetName = ref<string | null>(null);
const loadedReportDataset = ref<string | null>(null);
const listConfig = ref<OdakHubListConfig>({ columns: [] });
const expandConfig = ref<ReportingExpandConfig>(defaultReportingExpandConfigFromFields([]));
const summaryConfig = ref(emptyReportingSummaryConfig());
const summaryValues = ref<ReportingSummaryValues>({});
const summaryLoading = ref(false);
const fieldPolicies = ref(emptyOdakFieldPoliciesBlob());
const visibilityPolicies = ref<OdakFieldVisibilityPolicy[]>([]);
const defaultFilters = ref<AfListFilter[]>([]);
const reportParameters = ref<ReportingReportParameter[]>([]);
const documentBindings = ref<ReportingDocumentBinding[]>([]);
const parameterValues = ref<ReportingParameterValues>({});
const advancedFilters = ref<AfListFilter[]>([]);
const runtimeFiltersKey = ref(0);
const saving = ref(false);
const saveMessage = ref('');
const saveError = ref('');
const accessDenied = ref(false);
const notFound = ref(false);

const domainKey = computed(() =>
  reportingDomainKey(authStore.userInfo?.domain_id, authStore.userInfo?.domain_name)
);

const { canViewColumn } = useReportingColumnAccess(fieldPolicies);

const schemaFields = ref<FieldDefinition[]>([]);
const schemaLoading = ref(false);
const previewLoading = ref(false);
const previewRows = ref<Record<string, unknown>[]>([]);
const previewTotal = ref(0);
const previewError = ref<string | null>(null);
const dgQuery = ref<unknown>(null);
const dgRequestUrl = ref<string | null>(null);
const showDgQueryPanel = ref(false);
const dgQueryExpanded = ref<string | undefined>(undefined);
const tablePage = ref(1);
const itemsPerPage = ref(50);
const itemsPerPageOptions = [25, 50, 100];
const tableSortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([]);
const sortInitialized = ref(false);
const contentTab = ref<
  | 'design'
  | 'view'
  | 'columns'
  | 'expand'
  | 'summary'
  | 'columnAuth'
  | 'parameters'
  | 'reportAuth'
>('design');
const expandedIds = ref<string[]>([]);

const datasetItems = computed(() =>
  (datasetStore.datasets || [])
    .map((d) => ({
      value: d.name,
      title: d.title ? `${d.title} (${d.name})` : d.name,
    }))
    .sort((a, b) => a.title.localeCompare(b.title, 'tr')),
);

const filterColumns = computed(() =>
  buildReportingFilterColumns(listConfig.value, schemaFields.value, (field) => canViewColumn(field))
);

const showAdvancedFilterPanel = computed(() => filterColumns.value.length > 0);

/** Tasarımcı — tüm filtrelenebilir sütunlar (yetki filtresi yok). */
const designerFilterColumns = computed(() =>
  buildReportingFilterColumns(listConfig.value, schemaFields.value)
);

const designerFilterFieldNames = computed(() =>
  designerFilterColumns.value.map((c) => c.key)
);

const categorySelectItems = computed(() =>
  flattenReportingCategoryOptions(categoryService.value.getTree())
);

const tableHeaders = computed(() => {
  const headers = buildReportingListHeaders(listConfig.value, schemaFields.value, (field) =>
    canViewColumn(field)
  );
  if (expandConfig.value.enabled) {
    return [{ ...ODAK_DATA_TABLE_EXPAND_COLUMN }, ...headers];
  }
  return headers;
});

const previewTableItems = computed(() =>
  previewRows.value.map((row, index) => ({
    ...row,
    __dataId: reportingRowId(row) || `preview-row-${index}`,
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
  return applyListColumnFormatting(raw, col?.format);
}

function cellTitle(raw: string, fieldName: string): string | undefined {
  const col = columnConfigByField(listConfig.value, fieldName);
  return isListColumnTextTruncated(raw, col?.format) ? raw : undefined;
}

function cellStyle(fieldName: string, raw: string, row: Record<string, unknown>): Record<string, string> {
  const col = columnConfigByField(listConfig.value, fieldName);
  return getListColumnCellStyle(raw, fieldName, col?.format, row);
}

function isBoolColumn(fieldName: string): boolean {
  return isReportingBoolField(schemaFields.value, fieldName);
}

function boolCellValue(item: Record<string, unknown>, fieldName: string): boolean | null {
  return parseReportingBoolValue(item[fieldName]);
}

async function loadDatasets() {
  await authStore.ensureValidToken();
  await datasetStore.fetchAllDatasets();
}

function sanitizeDefaultFiltersInPlace() {
  defaultFilters.value = sanitizeReportingDefaultFilters(
    defaultFilters.value,
    designerFilterFieldNames.value
  );
}

function applyRuntimeFiltersFromDefaults() {
  advancedFilters.value = cloneAfListFilters(defaultFilters.value);
  runtimeFiltersKey.value += 1;
}

function resetRuntimeFiltersToDefaults() {
  applyRuntimeFiltersFromDefaults();
  if (tablePage.value !== 1) tablePage.value = 1;
  else if (previewRows.value.length || visibleColumns.value.length) void runPreview();
}

function resetReportVisibilityDefaults() {
  visibilityPolicies.value = [];
}

async function hydrateFromReport() {
  notFound.value = false;
  accessDenied.value = false;

  if (!resolvedReportId.value) return;

  await bootstrapReportingCatalog(domainKey.value);
  const report = catalogService.value.getReport(resolvedReportId.value);
  if (!report) {
    notFound.value = true;
    return;
  }
  if (!canViewReportingReport(report.visibilityPolicies, authStore.userGroups)) {
    accessDenied.value = true;
    return;
  }
  const draft = draftFromReportDefinition(report);
  title.value = draft.title;
  description.value = draft.description ?? '';
  categoryId.value = draft.categoryId;
  loadedReportDataset.value = draft.datasetName || null;
  listConfig.value = draft.listConfig;
  normalizeReportingListConfig(listConfig.value);
  const expandMigrated = ensureOdakEgitimParticipantsExpandTab(
    JSON.parse(JSON.stringify(draft.expand)) as typeof draft.expand,
    draft.datasetName
  );
  expandConfig.value = expandMigrated.expand;
  if (expandMigrated.changed) {
    void catalogService.value.saveReport({
      ...report,
      expand: expandMigrated.expand,
      updatedAt: new Date().toISOString(),
    });
  }
  summaryConfig.value = draft.summary ?? emptyReportingSummaryConfig();
  fieldPolicies.value = draft.fieldPolicies;
  defaultFilters.value = draft.defaultFilters;
  reportParameters.value = draft.parameters ?? [];
  documentBindings.value = draft.documentBindings ?? [];
  parameterValues.value = defaultReportingParameterValues(reportParameters.value);
  visibilityPolicies.value = draft.visibilityPolicies;
  datasetName.value = draft.datasetName || null;
  sanitizeDefaultFiltersInPlace();
  applyRuntimeFiltersFromDefaults();
}

async function bootstrapEditMode() {
  if (!resolvedReportId.value) return;
  await authStore.ensureValidToken();
  await hydrateFromReport();
  if (notFound.value || accessDenied.value) return;
  if (datasetName.value) await loadSchemaFieldsOnly(datasetName.value);
}

async function saveReport() {
  saveError.value = '';
  saveMessage.value = '';
  if (!title.value.trim()) {
    saveError.value = t('reporting.errors.titleRequired');
    return;
  }
  if (!categoryId.value) {
    saveError.value = t('reporting.errors.categoryRequired');
    return;
  }
  if (!datasetName.value?.trim()) {
    saveError.value = t('reporting.errors.pickDataset');
    return;
  }
  saving.value = true;
  try {
    const saved = await catalogService.value.saveReport(
      reportFromDraft(existingReport.value ?? null, {
        title: title.value,
        description: description.value,
        categoryId: categoryId.value,
        datasetName: datasetName.value ?? '',
        listConfig: listConfig.value,
        expand: expandConfig.value,
        fieldPolicies: fieldPolicies.value,
        defaultFilters: defaultFilters.value,
        visibilityPolicies: visibilityPolicies.value,
        parameters: reportParameters.value,
        documentBindings: documentBindings.value,
        summary: summaryConfig.value,
      })
    );
    saveMessage.value = t('reporting.designer.saved');
    loadedReportDataset.value = saved.datasetName || null;
    if (!resolvedReportId.value) {
      await router.replace(`/apps/reporting/designer/${saved.id}`);
    }
  } catch (e: unknown) {
    saveError.value = e instanceof Error ? e.message : t('reporting.errors.saveFailed');
  } finally {
    saving.value = false;
  }
}

function goBackToCatalog() {
  void router.push('/apps/reporting');
}

async function loadSchemaFieldsOnly(name: string) {
  schemaLoading.value = true;
  previewError.value = null;
  try {
    await authStore.ensureValidToken();
    const ds = await datasetStore.fetchDatasetByName(name);
    schemaFields.value = ds?.fields || [];
    sanitizeDefaultFiltersInPlace();
    applyRuntimeFiltersFromDefaults();
    applyDefaultSortFromConfig();
    sortInitialized.value = true;
  } catch (e: unknown) {
    schemaFields.value = [];
    previewError.value = e instanceof Error ? e.message : t('reporting.errors.schemaLoad');
  } finally {
    schemaLoading.value = false;
  }
}

async function loadSchemaFresh(name: string) {
  schemaLoading.value = true;
  previewError.value = null;
  sortInitialized.value = false;
  try {
    await authStore.ensureValidToken();
    const ds = await datasetStore.fetchDatasetByName(name);
    schemaFields.value = ds?.fields || [];
    const fresh = freshReportConfigFromSchema(schemaFields.value);
    listConfig.value = fresh.listConfig;
    expandConfig.value = fresh.expand;
    summaryConfig.value = emptyReportingSummaryConfig();
    summaryValues.value = {};
    fieldPolicies.value = fresh.fieldPolicies;
    defaultFilters.value = fresh.defaultFilters;
    loadedReportDataset.value = name;
    expandedIds.value = [];
    sanitizeDefaultFiltersInPlace();
    applyRuntimeFiltersFromDefaults();
    previewRows.value = [];
    previewTotal.value = 0;
    tablePage.value = 1;
    applyDefaultSortFromConfig();
    sortInitialized.value = true;
  } catch (e: unknown) {
    schemaFields.value = [];
    listConfig.value = { columns: [] };
    previewError.value = e instanceof Error ? e.message : t('reporting.errors.schemaLoad');
  } finally {
    schemaLoading.value = false;
  }
}

function resetColumnAuthDefaults() {
  fieldPolicies.value = emptyOdakFieldPoliciesBlob();
}

function resetExpandDefaults() {
  if (!schemaFields.value.length) return;
  const preservedTabs = expandConfig.value.tabs;
  const preservedDefaultTabId = expandConfig.value.defaultTabId;
  const preservedActions = expandConfig.value.actions;
  const wasEnabled = expandConfig.value.enabled;
  expandConfig.value = {
    ...defaultReportingExpandConfigFromFields(schemaFields.value),
    enabled: wasEnabled,
    tabs: preservedTabs,
    defaultTabId: preservedDefaultTabId,
    actions: preservedActions,
  };
}

function resetColumnDefaults() {
  if (!schemaFields.value.length) return;
  listConfig.value = defaultReportingListConfigFromFields(schemaFields.value);
  applyDefaultSortFromConfig();
}

function onSummaryConfigUpdate(value: ReportingSummaryConfig) {
  summaryConfig.value = value;
}

const ADVANCED_FILTERS_DEBOUNCE_MS = 450;
let advancedFiltersPreviewTimer: ReturnType<typeof setTimeout> | null = null;

function scheduleAdvancedFiltersPreview() {
  if (advancedFiltersPreviewTimer) clearTimeout(advancedFiltersPreviewTimer);
  advancedFiltersPreviewTimer = setTimeout(() => {
    advancedFiltersPreviewTimer = null;
    if (tablePage.value !== 1) tablePage.value = 1;
    else if (previewRows.value.length || visibleColumns.value.length) void runPreview();
  }, ADVANCED_FILTERS_DEBOUNCE_MS);
}

function onAdvancedFiltersUpdate(filters: AfListFilter[]) {
  advancedFilters.value = filters;
  scheduleAdvancedFiltersPreview();
}

function previewRuntimeQuery() {
  return buildReportingRuntimeQuery(
    reportParameters.value,
    parameterValues.value,
    [],
    advancedFilters.value
  );
}

async function runPreview() {
  if (!datasetName.value) {
    previewError.value = t('reporting.errors.pickDataset');
    return;
  }
  if (!visibleColumns.value.length) {
    previewError.value = t('reporting.errors.pickColumns');
    return;
  }
  if (!reportingParametersReady(reportParameters.value, parameterValues.value)) {
    previewError.value = t('reporting.runner.parametersRequired');
    return;
  }

  previewLoading.value = true;
  previewError.value = null;
  try {
    await authStore.ensureValidToken();
    const skip = (tablePage.value - 1) * itemsPerPage.value;
    const query = previewRuntimeQuery();
    const searchText = reportingParameterSearchText(reportParameters.value, parameterValues.value);
    const summaryPromise =
      summaryConfig.value.metrics.length && summaryConfig.value.placement !== 'none'
        ? (async () => {
            summaryLoading.value = true;
            try {
              summaryValues.value = await fetchReportingSummary({
                datasetName: datasetName.value!,
                metrics: summaryConfig.value.metrics,
                filters: query.filters,
                mongoMatch: query.mongoMatch,
                search: searchText,
                textFieldNames: reportingSummarySearchableTextFields(schemaFields.value),
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
        search: searchText,
        sortField: currentSortField(),
        sortDesc: currentSortDesc(),
        skip,
        limit: itemsPerPage.value,
        expand: true,
        showQuery: showDgQueryPanel.value,
      }),
      summaryPromise,
    ]);
    previewRows.value = result.rows;
    previewTotal.value = result.totalCount;
    dgQuery.value = showDgQueryPanel.value ? (result.dgQuery ?? null) : null;
    dgRequestUrl.value = showDgQueryPanel.value ? (result.requestUrl ?? null) : null;
    expandedIds.value = [];
  } catch (e: unknown) {
    previewRows.value = [];
    previewTotal.value = 0;
    summaryValues.value = {};
    dgQuery.value = null;
    dgRequestUrl.value = null;
    previewError.value = e instanceof Error ? e.message : t('reporting.errors.previewFailed');
  } finally {
    previewLoading.value = false;
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
    await runPreview();
  }
}

function onParametersRun(values: ReportingParameterValues) {
  parameterValues.value = values;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void runPreview();
}

function onTableOptions(options: {
  page: number;
  itemsPerPage: number;
  sortBy: { key: string; order: 'asc' | 'desc' }[];
}) {
  if (!sortInitialized.value) return;

  const pageChanged = options.page !== tablePage.value;
  const sizeChanged = options.itemsPerPage !== itemsPerPage.value;
  const sortChanged =
    JSON.stringify(options.sortBy) !== JSON.stringify(tableSortBy.value);

  tablePage.value = options.page;
  itemsPerPage.value = options.itemsPerPage;
  tableSortBy.value = options.sortBy ?? [];

  if (pageChanged || sizeChanged || sortChanged) {
    void runPreview();
  }
}

function onReportParametersUpdate(parameters: ReportingReportParameter[]) {
  reportParameters.value = parameters;
  parameterValues.value = defaultReportingParameterValues(parameters);
}

function resetReportParameters() {
  reportParameters.value = [];
  parameterValues.value = {};
}

watch(datasetName, (name) => {
  if (!name) {
    schemaFields.value = [];
    listConfig.value = { columns: [] };
    expandConfig.value = defaultReportingExpandConfigFromFields([]);
    fieldPolicies.value = emptyOdakFieldPoliciesBlob();
    expandedIds.value = [];
    defaultFilters.value = [];
    advancedFilters.value = [];
    runtimeFiltersKey.value += 1;
    previewRows.value = [];
    loadedReportDataset.value = null;
    sortInitialized.value = false;
    return;
  }
  if (loadedReportDataset.value === name && resolvedReportId.value) {
    void loadSchemaFieldsOnly(name);
    return;
  }
  void loadSchemaFresh(name);
});

onMounted(async () => {
  await loadDatasets();
  if (resolvedReportId.value) {
    await bootstrapEditMode();
  } else {
    await bootstrapReportingCatalog(domainKey.value);
    const q = route.query.categoryId;
    if (typeof q === 'string' && q.trim()) {
      categoryId.value = q.trim();
    }
  }
});

onBeforeUnmount(() => {
  if (advancedFiltersPreviewTimer) clearTimeout(advancedFiltersPreviewTimer);
});

watch(
  () => [resolvedReportId.value, domainKey.value] as const,
  (next, prev) => {
    const id = next[0];
    if (!id) return;
    if (prev && next[0] === prev[0] && next[1] === prev[1]) return;
    void bootstrapEditMode();
  }
);
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-alert v-if="notFound" type="warning" variant="tonal" class="mb-4">
      {{ t('reporting.errors.reportNotFound') }}
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
    <v-row>
    <v-col cols="12">
      <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-2">
        <v-btn variant="text" size="small" @click="goBackToCatalog">
          <ArrowLeftIcon size="16" class="mr-1" />
          {{ t('reporting.catalog.backToList') }}
        </v-btn>
        <div class="d-flex flex-wrap ga-2">
          <v-btn
            color="primary"
            variant="flat"
            size="small"
            :loading="saving"
            :disabled="saving"
            @click="saveReport"
          >
            <DeviceFloppyIcon size="16" class="mr-1" />
            {{ t('reporting.actions.save') }}
          </v-btn>
        </div>
      </div>
      <v-alert v-if="saveMessage" type="success" variant="tonal" density="compact" class="mb-2">
        {{ saveMessage }}
      </v-alert>
      <v-alert v-if="saveError" type="error" variant="tonal" density="compact" class="mb-2">
        {{ saveError }}
      </v-alert>
      <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
        {{ t('reporting.introAdvanced') }}
        {{ t('reporting.designer.saveHint') }}
      </v-alert>
    </v-col>

    <!-- İçerik — sekmeli (Tasarım ilk sekme) -->
    <v-col cols="12">
      <v-card elevation="0" class="border">
        <v-card-title class="d-flex align-center justify-space-between flex-wrap ga-2 py-3">
          <span class="text-subtitle-1 font-weight-medium">
            {{ title || t('reporting.preview.title') }}
          </span>
          <div class="d-flex flex-wrap ga-2">
            <v-btn
              color="primary"
              variant="flat"
              size="small"
              :loading="saving"
              :disabled="saving"
              @click="saveReport"
            >
              <DeviceFloppyIcon size="16" class="mr-1" />
              {{ t('reporting.actions.save') }}
            </v-btn>
            <v-btn
              variant="tonal"
              size="small"
              :loading="previewLoading"
              :disabled="!datasetName"
              @click="runPreview"
            >
              <PlayerPlayIcon size="16" class="mr-1" />
              {{ t('reporting.actions.run') }}
            </v-btn>
          </div>
        </v-card-title>

        <div class="d-flex flex-column flex-md-row">
          <v-tabs
            v-model="contentTab"
            :direction="mdAndUp ? 'vertical' : 'horizontal'"
            color="primary"
            class="reporting-side-tabs flex-shrink-0 py-2"
            show-arrows
          >
            <v-tab
              v-for="tab in contentTabItems"
              :key="tab.key"
              :value="tab.key"
              class="text-none justify-start"
            >
              {{ tab.label }}
            </v-tab>
          </v-tabs>
          <v-divider :vertical="mdAndUp" />
          <v-window v-model="contentTab" class="reporting-content-window flex-grow-1" style="min-width: 0">
          <!-- Tasarım (başlık, kategori, dataset) -->
          <v-window-item value="design">
            <v-card-text>
              <v-row dense>
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="title"
                    :label="t('reporting.fields.title')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-3"
                  />
                  <v-textarea
                    v-model="description"
                    :label="t('reporting.fields.description')"
                    density="compact"
                    variant="outlined"
                    rows="2"
                    hide-details
                    class="mb-3"
                  />
                </v-col>
                <v-col cols="12" md="6">
                  <v-select
                    v-model="categoryId"
                    :items="categorySelectItems"
                    item-title="title"
                    item-value="value"
                    :label="t('reporting.fields.category')"
                    density="compact"
                    variant="outlined"
                    hide-details
                    class="mb-3"
                    :hint="t('reporting.fields.categoryHint')"
                    persistent-hint
                  />
                  <v-autocomplete
                    v-model="datasetName"
                    :items="datasetItems"
                    item-title="title"
                    item-value="value"
                    :label="t('reporting.fields.dataset')"
                    :loading="datasetStore.loading"
                    density="compact"
                    variant="outlined"
                    clearable
                    hide-details
                    class="mb-3"
                  />
                  <v-progress-linear v-if="schemaLoading" indeterminate class="mb-3" />
                  <v-btn
                    variant="text"
                    size="small"
                    :loading="datasetStore.loading"
                    @click="loadDatasets"
                  >
                    <RefreshIcon size="16" class="mr-1" />
                    {{ t('reporting.actions.refreshDatasets') }}
                  </v-btn>
                </v-col>
              </v-row>
            </v-card-text>
          </v-window-item>

          <!-- Rapor görünümü -->
          <v-window-item value="view">
            <div class="px-4 pt-3">
              <ReportingParametersPanel
                v-model="parameterValues"
                :parameters="reportParameters"
                :disabled="previewLoading"
                @run="onParametersRun"
              />

              <div v-if="showAdvancedFilterPanel" class="mt-2 mb-1">
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
            </div>
            <v-card-text>
              <v-alert v-if="previewError" type="error" variant="tonal" class="mb-3">
                {{ previewError }}
              </v-alert>

              <div
                v-if="!reportParameters.length || reportingParametersReady(reportParameters, parameterValues)"
                class="d-flex flex-wrap align-center ga-3 mb-3"
              >
                <v-switch
                  v-model="showDgQueryPanel"
                  :label="t('reporting.runner.showDgQuery')"
                  color="primary"
                  density="compact"
                  hide-details
                  :disabled="previewLoading"
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
                v-if="reportParameters.length && !reportingParametersReady(reportParameters, parameterValues)"
                type="info"
                variant="tonal"
                class="mb-3"
              >
                {{ t('reporting.runner.selectParameters') }}
              </v-alert>

              <div
                v-else-if="!visibleColumns.length && !previewLoading"
                class="text-medium-emphasis text-body-2 py-8 text-center"
              >
                {{ t('reporting.hints.emptyPreview') }}
              </div>

              <template
                v-else-if="!reportParameters.length || reportingParametersReady(reportParameters, parameterValues)"
              >
                <ReportingSummaryCards
                  v-if="reportingSummaryShowCards(summaryConfig)"
                  :config="summaryConfig"
                  :values="summaryValues"
                  :loading="summaryLoading"
                />

                <v-data-table-server
                  v-model:expanded="expandedIds"
                  :headers="tableHeaders"
                  :items="previewTableItems"
                  :loading="previewLoading"
                  :items-per-page="itemsPerPage"
                  :items-per-page-options="itemsPerPageOptions"
                  :page="tablePage"
                  :items-length="previewTotal"
                  :sort-by="tableSortBy"
                  :show-expand="expandConfig.enabled"
                  :expand-on-click="false"
                  item-value="__dataId"
                  density="compact"
                  class="border rounded"
                  @update:options="onTableOptions"
                >
                <template
                  v-for="col in visibleColumns"
                  #[`item.${col}`]="{ item }"
                >
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
                        :title="t('reporting.bool.true')"
                      />
                      <v-icon
                        v-else-if="boolCellValue(item, col) === false"
                        icon="mdi-close-circle-outline"
                        color="error"
                        size="20"
                        :title="t('reporting.bool.false')"
                      />
                      <span v-else class="text-medium-emphasis">—</span>
                    </template>
                    <template v-else>
                      <span :title="cellTitle(cellRaw(item, col), col)">
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
                        :dataset-name="datasetName ?? ''"
                        :fields="schemaFields"
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
          </v-window-item>

          <!-- Sütun ayarları -->
          <v-window-item value="columns">
            <v-card-text>
              <ReportingListColumnsPanel
                :list-config="listConfig"
                :fields="schemaFields"
                :disabled="!schemaFields.length"
                :domain-key="domainKey"
                @reset="resetColumnDefaults"
              />
            </v-card-text>
          </v-window-item>

          <!-- Expand düzeni -->
          <v-window-item value="expand">
            <v-card-text>
              <ReportingExpandLayoutPanel
                :expand-config="expandConfig"
                :fields="schemaFields"
                :disabled="!schemaFields.length"
                :domain-key="domainKey"
                @reset="resetExpandDefaults"
              />
            </v-card-text>
          </v-window-item>

          <!-- Özet metrikler -->
          <v-window-item value="summary">
            <v-card-text>
              <ReportingSummaryDesignerPanel
                :summary="summaryConfig"
                :fields="schemaFields"
                :disabled="!schemaFields.length"
                @update:summary="onSummaryConfigUpdate"
              />
            </v-card-text>
          </v-window-item>

          <!-- Sütun yetkilendirme -->
          <v-window-item value="columnAuth">
            <v-card-text>
              <ReportingColumnAuthPanel
                :field-policies="fieldPolicies"
                :fields="schemaFields"
                :disabled="!schemaFields.length"
                @reset="resetColumnAuthDefaults"
              />
            </v-card-text>
          </v-window-item>

          <!-- Rapor parametreleri -->
          <v-window-item value="parameters">
            <v-card-text>
              <ReportingParametersDesignerPanel
                :parameters="reportParameters"
                :fields="schemaFields"
                :disabled="!schemaFields.length"
                @update:parameters="onReportParametersUpdate"
                @reset="resetReportParameters"
              />
            </v-card-text>
          </v-window-item>

          <!-- Belge şablonları -->
          <v-window-item value="documentBindings">
            <v-card-text>
              <ReportingDocumentBindingsPanel
                :bindings="documentBindings"
                :report-id="resolvedReportId"
                :domain-key="domainKey"
                @update:bindings="documentBindings = $event"
              />
            </v-card-text>
          </v-window-item>

          <!-- Rapor yetkilendirme -->
          <v-window-item value="reportAuth">
            <v-card-text>
              <ReportingReportVisibilityPanel
                :visibility-policies="visibilityPolicies"
                @reset="resetReportVisibilityDefaults"
              />
            </v-card-text>
          </v-window-item>
        </v-window>
        </div>
      </v-card>
    </v-col>
    </v-row>
    </template>
  </div>
</template>

<style scoped>
.reporting-side-tabs {
  min-width: 200px;
  max-width: 240px;
}

@media (max-width: 959px) {
  .reporting-side-tabs {
    min-width: 0;
    max-width: none;
  }
}

.reporting-side-tabs :deep(.v-tab) {
  min-height: 40px;
}

.reporting-content-window {
  min-height: 320px;
}
</style>
