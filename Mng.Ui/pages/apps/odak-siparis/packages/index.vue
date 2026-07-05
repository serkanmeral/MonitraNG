<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import OdakSiparisCustomerDialog from '@/components/apps/odak-siparis/OdakSiparisCustomerDialog.vue';
import OdakSiparisCustomerDrawer from '@/components/apps/odak-siparis/OdakSiparisCustomerDrawer.vue';
import OdakSiparisPackageDialog from '@/components/apps/odak-siparis/OdakSiparisPackageDialog.vue';
import OdakSiparisPackageExpandPanel from '@/components/apps/odak-siparis/OdakSiparisPackageExpandPanel.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useOdakPackageFieldAccess } from '@/composables/useOdakPackageFieldAccess';
import { useAppI18n } from '@/composables/useAppI18n';
import { useAuthStore } from '@/stores/auth';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';
import { ocDelete } from '@/services/operationCoreService';
import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';
import {
  applyListColumnFormatting,
  getListColumnCellStyle,
} from '@/utils/afListColumnFormat';
import { ODAK_SIPARIS_CONFIG, ODAK_DATA_TABLE_EXPAND_COLUMN, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import {
  buildPackageFilterColumns,
  buildPackageListHeaders,
  fieldNameFromListSortKey,
  listSortKeyFromField,
} from '@/utils/odakSiparisPackageListSettings';
import {
  customerIdFromRow,
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchCustomerRelationOptions,
  fetchOdakPackagesPage,
  fetchPackageLineStatsMap,
  formatOdakDate,
  formatOdakNumber,
  invalidateOdakSiparisCustomerCache,
  packageDataId,
  packageDisplayNo,
  packageListCellRaw,
  packageStatusLabel,
  type OdakPackageLineStats,
  type OdakPackageListSort,
} from '@/utils/odakSiparisService';
import {
  collectPersonIdsForPackageLabelResolution,
  fetchPersonLabelMap,
} from '@/utils/odakSiparisPackagePersonnel';
import type { OdakCustomerDialogMode } from '@/utils/odakSiparisCustomerService';
import type { OdakPackageDialogMode } from '@/utils/odakSiparisPackageService';
import { exportOdakPackagesToCsv, ODAK_PACKAGE_EXPORT_MAX } from '@/utils/odakSiparisPackageExport';
import { odakPackageSettingsFieldLabelTr } from '@/utils/odakSiparisSettingsLabels';
import { CertificateIcon, DownloadIcon, EditIcon, PlusIcon, RefreshIcon, SettingsIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const hubStore = useOdakSiparisHubSettingsStore();
const route = useRoute();
const router = useRouter();

type StatusTab = 'open' | 'closed' | 'all';
type ExpandTab = 'summary' | 'dashboard' | 'lines' | 'shipments' | 'quality' | 'documents';

const statusTab = ref<StatusTab>('open');
const searchQuery = ref('');

function searchText(): string {
  const q = searchQuery.value;
  return typeof q === 'string' ? q.trim() : '';
}
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakPackageRow[]>([]);
const lineStats = ref<Map<string, OdakPackageLineStats>>(new Map());
const listConfig = computed(() => hubStore.packageListConfig);
const fieldPolicies = computed(() => hubStore.packageFieldPolicies as OdakFieldPoliciesBlob);
const { canViewListColumn } = useOdakPackageFieldAccess(fieldPolicies);
const customerLabels = ref<Record<string, string>>({});
const personLabels = ref<Record<string, string>>({});
const relationFilterOptions = ref<Record<string, { value: string; title: string }[]>>({});
const activeListFilters = ref<AfListFilter[]>([]);
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);
const tableItemsPerPageOptions = [10, 20, 50, 100];
const tableSortBy = ref<OdakPackageListSort[]>([{ key: 'displayNo', order: 'desc' }]);
const hubSortInitialized = ref(false);
const expandedIds = ref<string[]>([]);
const expandActiveTab = ref<ExpandTab>('summary');
const expandRefreshToken = ref(0);
const packageDialogOpen = ref(false);
const packageDialogMode = ref<OdakPackageDialogMode>('create');
const packageDialogId = ref<string | undefined>();
const packageDialogSeed = ref<OdakPackageRow | null>(null);
const customerDrawerOpen = ref(false);
const customerDrawerId = ref<string | undefined>();
const customerDialogOpen = ref(false);
const customerDialogMode = ref<OdakCustomerDialogMode>('edit');
const customerDialogId = ref<string | undefined>();
const pendingCustomerFilterId = ref<string | null>(null);

const paginationLabel = computed(() =>
  t('odakSiparis.packages.paginationSummary', {
    from: totalCount.value === 0 ? 0 : (tablePage.value - 1) * tableItemsPerPage.value + 1,
    to: Math.min(tablePage.value * tableItemsPerPage.value, totalCount.value),
    total: totalCount.value,
  })
);
const deleteDialog = ref(false);
const itemToDelete = ref<OdakPackageRow | null>(null);
const deleting = ref(false);
const exporting = ref(false);
const exportMessage = ref('');

function columnTitle(fieldName: string, _listKey: string): string {
  return odakPackageSettingsFieldLabelTr(fieldName);
}

function listCellContext() {
  return { customerLabels: customerLabels.value, personLabels: personLabels.value, lineStats: lineStats.value };
}

const genericListColumns = computed(() =>
  configurableHeaders.value.filter((h) => h.key !== 'displayNo' && h.key !== 'customer')
);

function filterFieldLabel(fieldName: string): string {
  return odakPackageSettingsFieldLabelTr(fieldName);
}

function columnConfigForListKey(listKey: string) {
  const fieldName = fieldNameFromListSortKey(listKey);
  return listConfig.value.columns.find((c) => c.fieldName === fieldName);
}

function cellDisplayValue(raw: string, listKey: string, item: OdakPackageRow): string {
  const col = columnConfigForListKey(listKey);
  return applyListColumnFormatting(raw, col?.format);
}

function cellStyle(listKey: string, raw: string, item: OdakPackageRow): Record<string, string> {
  const col = columnConfigForListKey(listKey);
  const fieldName = fieldNameFromListSortKey(listKey);
  return getListColumnCellStyle(raw, fieldName, col?.format, item as Record<string, unknown>);
}

const page = computed(() => ({ title: t('odakSiparis.packages.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.title'), disabled: true, href: '#' },
]);

const statusTabs = computed(() => [
  { value: 'open' as const, label: t('odakSiparis.packages.tabs.open') },
  { value: 'closed' as const, label: t('odakSiparis.packages.tabs.closed') },
  { value: 'all' as const, label: t('odakSiparis.packages.tabs.all') },
]);

/** Hub ortak listConfig.filterable ile hizali. */
const packageFilterColumns = computed<AfFilterColumn[]>(() =>
  buildPackageFilterColumns(listConfig.value, filterFieldLabel)
);

const configurableHeaders = computed(() =>
  buildPackageListHeaders(listConfig.value, columnTitle, (listKey) =>
    canViewListColumn(listKey)
  )
);

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  ...configurableHeaders.value,
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: 120,
  },
]);

const deleteLineCount = computed(() => {
  const item = itemToDelete.value;
  if (!item) return 0;
  const id = packageDataId(item);
  return lineStats.value.get(id)?.lineCount ?? item.lineCount ?? 0;
});

function onListFiltersUpdate(filters: AfListFilter[]) {
  activeListFilters.value = filters;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void fetchPackages();
}

async function ensureCustomerLabels() {
  if (Object.keys(customerLabels.value).length) return;
  customerLabels.value = await fetchCustomerLabelMap();
}

async function loadFilterRelationOptions() {
  relationFilterOptions.value = {
    customerId: await fetchCustomerRelationOptions(),
  };
}

async function exportPackages() {
  exporting.value = true;
  exportMessage.value = '';
  errorMessage.value = '';
  try {
    const columnLabels = {
      packageNo: t('odakSiparis.packages.columns.packageNo'),
      name: t('odakSiparis.packages.columns.name'),
      customer: t('odakSiparis.packages.columns.customer'),
      customerPo: t('odakSiparis.packages.columns.customerPo'),
      projectNo: t('odakSiparis.packages.columns.projectNo'),
      partCount: t('odakSiparis.packages.columns.partCount'),
      stockCount: t('odakSiparis.packages.columns.stockCount'),
      lineCount: t('odakSiparis.packages.columns.lineCount'),
      status: t('odakSiparis.packages.columns.status'),
      beginDate: t('odakSiparis.packages.columns.beginDate'),
      deliveryDate: t('odakSiparis.packages.columns.deliveryDate'),
      poVersion: t('odakSiparis.packages.fields.poVersion'),
    };
    const result = await exportOdakPackagesToCsv(
      {
        statusTab: statusTab.value,
        search: searchText() || undefined,
        advancedFilters: activeListFilters.value,
        sortBy: tableSortBy.value,
        visibleExportKeys: configurableHeaders.value.map((h) => h.key),
      },
      columnLabels
    );
    exportMessage.value = result.truncated
      ? t('odakSiparis.packages.exportTruncated', { count: result.rowCount, max: ODAK_PACKAGE_EXPORT_MAX })
      : t('odakSiparis.packages.exportSuccess', { count: result.rowCount });
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    exporting.value = false;
  }
}

async function fetchPackages() {
  loading.value = true;
  errorMessage.value = '';
  try {
    await ensureCustomerLabels();

    const skip = (tablePage.value - 1) * tableItemsPerPage.value;
    const limit = tableItemsPerPage.value;

    const resp = await fetchOdakPackagesPage({
      statusTab: statusTab.value,
      skip,
      limit,
      search: searchText() || undefined,
      advancedFilters: activeListFilters.value,
      sortBy: tableSortBy.value,
    });

    items.value = [...resp.items];
    totalCount.value = resp.total ?? items.value.length;
    const personIds = collectPersonIdsForPackageLabelResolution(items.value as Record<string, unknown>[]);
    if (personIds.length) {
      personLabels.value = { ...personLabels.value, ...(await fetchPersonLabelMap(personIds)) };
    }
    const pageIds = items.value.map((x) => packageDataId(x)).filter(Boolean);
    const needsPoCols = configurableHeaders.value.some(
      (h) => h.key === 'customerPo' || h.key === 'projectNo'
    );
    lineStats.value =
      needsPoCols && pageIds.length ? await fetchPackageLineStatsMap(pageIds) : new Map();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
    totalCount.value = 0;
    lineStats.value = new Map();
  } finally {
    loading.value = false;
  }
}

function openCustomerDrawer(customerId: string) {
  if (!customerId) return;
  customerDrawerId.value = customerId;
  customerDrawerOpen.value = true;
}

function openCustomerEdit(customerId: string) {
  customerDrawerOpen.value = false;
  customerDialogMode.value = 'edit';
  customerDialogId.value = customerId;
  customerDialogOpen.value = true;
}

async function onCustomerSaved() {
  invalidateOdakSiparisCustomerCache();
  customerLabels.value = await fetchCustomerLabelMap(true);
  await fetchPackages();
}

function parseExpandTabFromQuery(): ExpandTab {
  const tab = route.query.tab;
  if (tab === 'dashboard') return 'dashboard';
  if (tab === 'lines') return 'lines';
  if (tab === 'shipments') return 'shipments';
  if (tab === 'quality') return 'quality';
  if (tab === 'documents' || tab === 'coc') return 'documents';
  return 'summary';
}

function onExpandNavigate(tab: 'lines' | 'shipments' | 'quality' | 'documents') {
  expandActiveTab.value = tab;
  syncExpandRoute();
}

function openGlobalDashboard() {
  void router.push('/dashboards/odak-siparis');
}

function syncExpandRoute() {
  const id = expandedIds.value[0];
  const nextQuery: Record<string, string | string[] | undefined> = { ...route.query };
  if (id) {
    nextQuery.expand = id;
    if (expandActiveTab.value !== 'summary') {
      nextQuery.tab = expandActiveTab.value;
    } else {
      delete nextQuery.tab;
    }
  } else {
    delete nextQuery.expand;
    delete nextQuery.tab;
  }
  const currentExpand = typeof route.query.expand === 'string' ? route.query.expand : '';
  const currentTab = typeof route.query.tab === 'string' ? route.query.tab : '';
  const nextTab = typeof nextQuery.tab === 'string' ? nextQuery.tab : '';
  if (currentExpand === (id ?? '') && currentTab === nextTab) return;
  void router.replace({ query: nextQuery });
}

function expandPackage(item: OdakPackageRow, tab: ExpandTab = 'summary') {
  const id = packageDataId(item);
  if (!id) return;
  expandActiveTab.value = tab;
  expandedIds.value = [id];
  expandRefreshToken.value += 1;
  syncExpandRoute();
}

function toggleExpand(item: OdakPackageRow) {
  const id = packageDataId(item);
  if (!id) return;
  if (expandedIds.value.includes(id)) {
    expandedIds.value = [];
    expandActiveTab.value = 'summary';
  } else {
    expandActiveTab.value = 'summary';
    expandedIds.value = [id];
    expandRefreshToken.value += 1;
  }
  syncExpandRoute();
}

function openQuality(item: OdakPackageRow) {
  expandPackage(item, 'quality');
  if (statusTab.value !== 'all') {
    statusTab.value = 'all';
  }
}

function openPackageDialog(mode: OdakPackageDialogMode, item?: OdakPackageRow) {
  packageDialogMode.value = mode;
  packageDialogId.value = item ? packageDataId(item) : undefined;
  packageDialogSeed.value = item ?? null;
  packageDialogOpen.value = true;
}

async function onPackageSaved(packageId?: string) {
  await fetchPackages();
  if (packageId) {
    expandActiveTab.value = 'summary';
    expandedIds.value = [packageId];
    expandRefreshToken.value += 1;
    syncExpandRoute();
  }
}

watch(expandedIds, (ids) => {
  if (ids.length > 1) {
    expandedIds.value = [ids[ids.length - 1]!];
  }
});

watch(expandActiveTab, () => {
  if (expandedIds.value.length === 1) {
    syncExpandRoute();
  }
});

function openEdit(item: OdakPackageRow) {
  openPackageDialog('edit', item);
}

function confirmDelete(item: OdakPackageRow) {
  itemToDelete.value = item;
  deleteDialog.value = true;
}

async function doDelete() {
  const item = itemToDelete.value;
  if (!item) return;
  const id = packageDataId(item);
  if (!id) return;
  deleting.value = true;
  errorMessage.value = '';
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.packagesDataset, id);
    deleteDialog.value = false;
    itemToDelete.value = null;
    await fetchPackages();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

function createPackage() {
  openPackageDialog('create');
}

watch(statusTab, () => {
  expandedIds.value = [];
  expandActiveTab.value = 'summary';
  syncExpandRoute();
  if (tablePage.value !== 1) {
    tablePage.value = 1;
  } else {
    void fetchPackages();
  }
});

type TableOptions = {
  page: number;
  itemsPerPage: number;
  sortBy?: OdakPackageListSort[];
};

function onTableOptions(options: TableOptions) {
  const nextSort = Array.isArray(options.sortBy) && options.sortBy.length
    ? options.sortBy
    : [{ key: 'displayNo', order: 'desc' as const }];
  const sortChanged = JSON.stringify(nextSort) !== JSON.stringify(tableSortBy.value);
  const nextSize = options.itemsPerPage;
  const sizeChanged = nextSize !== tableItemsPerPage.value;
  let nextPage = options.page;
  if (sortChanged || sizeChanged) nextPage = 1;
  const pageChanged = nextPage !== tablePage.value;
  if (!sortChanged && !pageChanged && !sizeChanged) return;
  expandedIds.value = [];
  tableSortBy.value = nextSort;
  tablePage.value = nextPage;
  tableItemsPerPage.value = nextSize;
  void fetchPackages();
}

let searchTimer: ReturnType<typeof setTimeout> | null = null;
function scheduleFetch() {
  if (tablePage.value !== 1) tablePage.value = 1;
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void fetchPackages(), 400);
}

watch(searchQuery, (v) => {
  if (v == null) {
    searchQuery.value = '';
    return;
  }
  scheduleFetch();
});

async function applyHubSortDefaults() {
  if (hubSortInitialized.value) return;
  const sortField = listConfig.value.defaultSortBy ?? 'packageNo';
  const sortKey = listSortKeyFromField(sortField);
  tableSortBy.value = [{ key: sortKey, order: listConfig.value.defaultSortOrder ?? 'desc' }];
  hubSortInitialized.value = true;
}

function onPageShow(event: PageTransitionEvent) {
  if (event.persisted) {
    void hubStore.ensureReady(true).then(() => applyHubSortDefaults());
  }
}

onMounted(() => {
  const expand = route.query.expand;
  if (typeof expand === 'string' && expand.trim()) {
    expandedIds.value = [expand.trim()];
    expandActiveTab.value = parseExpandTabFromQuery();
    statusTab.value = 'all';
  }
  const customerId = route.query.customerId;
  if (typeof customerId === 'string' && customerId.trim()) {
    pendingCustomerFilterId.value = customerId.trim();
    statusTab.value = 'all';
  }
  if (import.meta.client) {
    window.addEventListener('pageshow', onPageShow);
  }
  void initPackagesPage();
});

onBeforeUnmount(() => {
  if (import.meta.client) {
    window.removeEventListener('pageshow', onPageShow);
  }
});

watch(
  () => route.fullPath,
  (path, previousPath) => {
    if (path === '/apps/odak-siparis/packages' && previousPath && previousPath !== path) {
      void hubStore.ensureReady(false).then(() => applyHubSortDefaults());
    }
  }
);

async function refreshPackagesPage() {
  await hubStore.ensureReady(true);
  await applyHubSortDefaults();
  await fetchPackages();
}

async function initPackagesPage() {
  await hubStore.ensureReady(false);
  await applyHubSortDefaults();
  await ensureCustomerLabels();
  if (pendingCustomerFilterId.value) {
    activeListFilters.value = [
      { field: 'customerId', operator: 'eq', value: pendingCustomerFilterId.value },
    ];
    pendingCustomerFilterId.value = null;
  }
  await fetchPackages();
}
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10">
      <v-card-title class="d-flex flex-wrap align-center ga-3 py-4">
        <span class="text-h6">{{ t('odakSiparis.packages.title') }}</span>
        <v-spacer />
        <v-text-field
          v-model="searchQuery"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          :placeholder="t('odakSiparis.packages.quickSearch')"
          style="max-width: 220px"
        />
        <v-btn
          v-if="auth.isManager"
          variant="outlined"
          size="small"
          to="/apps/odak-siparis/packages/settings"
        >
          <SettingsIcon size="18" class="mr-1" />
          {{ t('odakSiparis.packages.settings.title') }}
        </v-btn>
        <v-btn variant="tonal" color="primary" prepend-icon="mdi-view-dashboard-outline" @click="openGlobalDashboard">
          {{ t('odakSiparis.dashboard.global.openFromPackages') }}
        </v-btn>
        <v-btn icon variant="outlined" size="small" :loading="loading" @click="refreshPackagesPage">
          <RefreshIcon size="18" />
        </v-btn>
        <v-btn
          variant="outlined"
          size="small"
          :loading="exporting"
          :disabled="loading"
          @click="exportPackages"
        >
          <DownloadIcon size="18" class="mr-1" />
          {{ t('odakSiparis.packages.export') }}
        </v-btn>
        <v-btn color="primary" variant="flat" @click="createPackage">
          <PlusIcon class="mr-1" size="18" />
          {{ t('odakSiparis.packages.add') }}
        </v-btn>
      </v-card-title>

      <v-alert v-if="exportMessage" type="success" variant="tonal" density="compact" class="mx-4 mb-2">
        {{ exportMessage }}
      </v-alert>

      <div class="px-4">
        <AfListFilters
          :columns="packageFilterColumns"
          :relation-options-by-key="relationFilterOptions"
          @update:filters="onListFiltersUpdate"
          @advanced-open="loadFilterRelationOptions"
        />

      </div>

      <v-tabs v-model="statusTab" color="primary" class="px-4">
        <v-tab v-for="tab in statusTabs" :key="tab.value" :value="tab.value">
          {{ tab.label }}
        </v-tab>
      </v-tabs>

      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">
          {{ errorMessage }}
        </v-alert>

        <div class="odak-packages-list-scroll">
        <v-data-table-server
          v-model:expanded="expandedIds"
          :headers="headers"
          :items="items"
          :loading="loading"
          :items-per-page="tableItemsPerPage"
          :items-per-page-options="tableItemsPerPageOptions"
          :page="tablePage"
          :items-length="totalCount"
          :sort-by="tableSortBy"
          item-value="__dataId"
          show-expand
          :expand-on-click="false"
          class="border rounded-md odak-packages-list-table"
          @update:options="onTableOptions"
        >
          <template #expanded-row="{ columns, item }">
            <tr>
              <td :colspan="columns.length" class="pa-0">
                <div class="odak-package-expand-viewport">
                <OdakSiparisPackageExpandPanel
                  :key="`${packageDataId(item)}-${expandRefreshToken}`"
                  :package-row="item"
                  :customer-labels="customerLabels"
                  :initial-tab="expandActiveTab"
                  @open-customer="openCustomerDrawer"
                  @update:active-tab="expandActiveTab = $event"
                  @navigate="onExpandNavigate"
                />
                </div>
              </td>
            </tr>
          </template>
          <template #item.displayNo="{ item }">
            <a
              href="#"
              class="text-primary text-decoration-none font-weight-medium"
              :style="cellStyle('displayNo', packageListCellRaw(item, 'displayNo', listCellContext()), item)"
              @click.prevent="toggleExpand(item)"
            >
              {{
                cellDisplayValue(
                  packageListCellRaw(item, 'displayNo', listCellContext()),
                  'displayNo',
                  item
                )
              }}
            </a>
          </template>
          <template #item.customer="{ item }">
            <a
              v-if="customerIdFromRow(item)"
              href="#"
              class="text-primary text-decoration-none"
              :style="cellStyle('customer', packageListCellRaw(item, 'customer', listCellContext()), item)"
              @click.prevent="openCustomerDrawer(customerIdFromRow(item))"
            >
              {{
                cellDisplayValue(
                  packageListCellRaw(item, 'customer', listCellContext()),
                  'customer',
                  item
                )
              }}
            </a>
            <span
              v-else
              :style="cellStyle('customer', packageListCellRaw(item, 'customer', listCellContext()), item)"
            >
              {{
                cellDisplayValue(
                  packageListCellRaw(item, 'customer', listCellContext()),
                  'customer',
                  item
                )
              }}
            </span>
          </template>
          <template v-for="col in genericListColumns" :key="col.key" #[`item.${col.key}`]="{ item }">
            <span
              :style="
                cellStyle(col.key, packageListCellRaw(item, col.key, listCellContext()), item)
              "
            >
              {{
                cellDisplayValue(
                  packageListCellRaw(item, col.key, listCellContext()),
                  col.key,
                  item
                )
              }}
            </span>
          </template>
          <template #item.actions="{ item }">
            <div class="d-inline-flex align-center justify-end ga-1">
              <v-btn
                icon
                size="x-small"
                variant="text"
                color="secondary"
                :title="t('odakSiparis.packages.openQuality')"
                @click="openQuality(item)"
              >
                <CertificateIcon size="18" />
              </v-btn>
              <v-btn icon size="x-small" variant="text" @click="openEdit(item)">
                <EditIcon size="18" />
              </v-btn>
              <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(item)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
        </v-data-table-server>
        </div>
        <div v-if="totalCount > 0" class="text-caption text-medium-emphasis mt-2">
          {{ paginationLabel }}
        </div>
      </v-card-text>
    </v-card>

    <OdakSiparisCustomerDrawer
      v-model="customerDrawerOpen"
      :customer-id="customerDrawerId"
      @edit="openCustomerEdit"
    />
    <OdakSiparisCustomerDialog
      v-model="customerDialogOpen"
      :mode="customerDialogMode"
      :customer-id="customerDialogId"
      @saved="onCustomerSaved"
    />

    <OdakSiparisPackageDialog
      v-model="packageDialogOpen"
      :mode="packageDialogMode"
      :package-id="packageDialogId"
      :seed-row="packageDialogSeed"
      @saved="onPackageSaved"
    />

    <v-dialog v-model="deleteDialog" max-width="460">
      <v-card>
        <v-card-title>{{ t('odakSiparis.packages.deleteTitle') }}</v-card-title>
        <v-card-text>
          <p>{{ t('odakSiparis.packages.deleteConfirm') }}</p>
          <p v-if="deleteLineCount > 0" class="text-medium-emphasis mb-0">
            {{ t('odakSiparis.packages.deleteWithLines', { count: deleteLineCount }) }}
          </p>
        </v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">{{ t('odakSiparis.packages.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">
            {{ t('odakSiparis.packages.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
/*
 * Yatay scroll tek kaynak: .odak-packages-list-scroll
 * Alt liste (expand panel) bu konteynerin içinde; geniş tablo üst tabloyu şişirirken
 * expand viewport container query ile görünür genişliğe sabitlenir.
 */
.odak-packages-list-scroll {
  display: block;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  overflow-x: auto;
  overflow-y: visible;
  -webkit-overflow-scrolling: touch;
  container-type: inline-size;
  container-name: odak-packages-scroll;
  padding-bottom: 2px;
}

.odak-packages-list-table {
  display: block;
  width: fit-content;
  min-width: 100%;
}

.odak-packages-list-table :deep(.v-table),
.odak-packages-list-table :deep(.v-table__wrapper) {
  overflow: visible !important;
}

.odak-packages-list-table :deep(table) {
  width: auto !important;
  table-layout: auto !important;
}

.odak-packages-list-table :deep(.v-data-table__expanded__content > td) {
  overflow: visible;
  padding: 0 !important;
}

/* Expand panel — üst tablo yatay scroll'da görünür alan genişliğinde kalır. */
.odak-package-expand-viewport {
  position: sticky;
  left: 0;
  z-index: 2;
  box-sizing: border-box;
  width: 100%;
  max-width: 100%;
  background: rgb(var(--v-theme-surface));
}

@supports (width: 100cqi) {
  .odak-package-expand-viewport {
    width: 100cqi;
    max-width: 100cqi;
  }
}

/* Expand sütunu (ilk sütun) — yatay scroll'da solda sabit. */
.odak-packages-list-table :deep(table) > thead > tr > th:first-child,
.odak-packages-list-table :deep(table) > tbody > tr:not(.v-data-table__expanded__content) > td:first-child {
  position: sticky;
  left: 0;
  z-index: 3;
  background: rgb(var(--v-theme-surface));
  box-shadow: 6px 0 6px -6px rgba(0, 0, 0, 0.12);
}

/* İşlemler sütunu — yatay scroll'da sağda sabit. */
.odak-packages-list-table :deep(table) > thead > tr > th:last-child,
.odak-packages-list-table :deep(table) > tbody > tr:not(.v-data-table__expanded__content) > td:last-child {
  position: sticky;
  right: 0;
  background: rgb(var(--v-theme-surface));
  box-shadow: -6px 0 6px -6px rgba(0, 0, 0, 0.18);
}

.odak-packages-list-table :deep(table) > tbody > tr:not(.v-data-table__expanded__content) > td:last-child {
  z-index: 1;
}

.odak-packages-list-table :deep(table) > thead > tr > th:last-child {
  z-index: 2;
}
</style>
