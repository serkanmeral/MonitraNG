<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import OdakSiparisCustomerDialog from '@/components/apps/odak-siparis/OdakSiparisCustomerDialog.vue';
import OdakSiparisCustomerDrawer from '@/components/apps/odak-siparis/OdakSiparisCustomerDrawer.vue';
import OdakSiparisPackageDialog from '@/components/apps/odak-siparis/OdakSiparisPackageDialog.vue';
import OdakSiparisPackageExpandPanel from '@/components/apps/odak-siparis/OdakSiparisPackageExpandPanel.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';
import { ODAK_SIPARIS_CONFIG, ODAK_DATA_TABLE_EXPAND_COLUMN, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerIdFromRow,
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchCustomerRelationOptions,
  fetchOdakPackagesPage,
  fetchPackageLineStatsMap,
  formatOdakDate,
  formatOdakNumber,
  filterPackagesByLineAdv,
  invalidateOdakSiparisCustomerCache,
  packageDataId,
  packageDisplayNo,
  packageStatusLabel,
  type OdakPackageLineStats,
  type OdakPackageListSort,
} from '@/utils/odakSiparisService';
import type { OdakCustomerDialogMode } from '@/utils/odakSiparisCustomerService';
import type { OdakPackageDialogMode } from '@/utils/odakSiparisPackageService';
import { exportOdakPackagesToCsv, ODAK_PACKAGE_EXPORT_MAX } from '@/utils/odakSiparisPackageExport';
import { CertificateIcon, DownloadIcon, EditIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

type StatusTab = 'open' | 'closed' | 'all';
type ExpandTab = 'summary' | 'lines' | 'quality';

const statusTab = ref<StatusTab>('open');
const searchQuery = ref('');

function searchText(): string {
  const q = searchQuery.value;
  return typeof q === 'string' ? q.trim() : '';
}
const lineSearchPanelOpen = ref<number | undefined>(undefined);
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakPackageRow[]>([]);
const lineStats = ref<Map<string, OdakPackageLineStats>>(new Map());
const customerLabels = ref<Record<string, string>>({});
const relationFilterOptions = ref<Record<string, { value: string; title: string }[]>>({});
const activeListFilters = ref<AfListFilter[]>([]);
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);
const tableItemsPerPageOptions = [10, 20, 50, 100];
const tableSortBy = ref<OdakPackageListSort[]>([{ key: 'displayNo', order: 'desc' }]);
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

/** Kalem dataset alanlari — sunucu filtresi yok; line stats ile client filtre. */
const lineAdv = ref({
  customerPo: '',
  customerProjectNo: '',
  customerPoItem: '',
  productDesc: '',
});

const page = computed(() => ({ title: t('odakSiparis.packages.title') }));
const breadcrumbs = computed(() => [
  { text: t('operationCore.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.title'), disabled: true, href: '#' },
]);

const statusTabs = computed(() => [
  { value: 'open' as const, label: t('odakSiparis.packages.tabs.open') },
  { value: 'closed' as const, label: t('odakSiparis.packages.tabs.closed') },
  { value: 'all' as const, label: t('odakSiparis.packages.tabs.all') },
]);

/** odak-is-paketleri-form listConfig.filterable ile hizali (status sekmelerde). */
const packageFilterColumns = computed<AfFilterColumn[]>(() => [
  { key: 'packageNo', label: t('odakSiparis.packages.searchFields.packageNo'), kind: 'text' },
  { key: 'name', label: t('odakSiparis.packages.searchFields.packageName'), kind: 'text' },
  { key: 'customerId', label: t('odakSiparis.packages.searchFields.customerName'), kind: 'relation' },
  { key: 'beginDate', label: t('odakSiparis.packages.columns.beginDate'), kind: 'date' },
  { key: 'deliveryDate', label: t('odakSiparis.packages.columns.deliveryDate'), kind: 'date' },
  { key: 'partCount', label: t('odakSiparis.packages.columns.partCount'), kind: 'number' },
  { key: 'stockCount', label: t('odakSiparis.packages.columns.stockCount'), kind: 'number' },
]);

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  { title: t('odakSiparis.packages.columns.packageNo'), key: 'displayNo', sortable: true },
  { title: t('odakSiparis.packages.columns.name'), key: 'name', sortable: true },
  { title: t('odakSiparis.packages.columns.customer'), key: 'customer', sortable: true },
  { title: t('odakSiparis.packages.columns.customerPo'), key: 'customerPo', sortable: false },
  { title: t('odakSiparis.packages.columns.projectNo'), key: 'projectNo', sortable: false },
  { title: t('odakSiparis.packages.columns.partCount'), key: 'partCount', sortable: true, width: 88 },
  { title: t('odakSiparis.packages.columns.stockCount'), key: 'stockCount', sortable: true, width: 88 },
  { title: t('odakSiparis.packages.columns.lineCount'), key: 'lineCount', sortable: true, width: 72 },
  { title: t('odakSiparis.packages.columns.status'), key: 'statusLabel', sortable: true },
  { title: t('odakSiparis.packages.columns.beginDate'), key: 'beginDate', sortable: true },
  { title: t('odakSiparis.packages.columns.deliveryDate'), key: 'deliveryDate', sortable: true },
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

const hasLineSearch = computed(
  () =>
    Boolean(
      lineAdv.value.customerProjectNo.trim() ||
        lineAdv.value.customerPoItem.trim() ||
        lineAdv.value.productDesc.trim()
    )
);

const needsLineStats = computed(() => Boolean(lineAdv.value.customerPo.trim() || hasLineSearch.value));

const hasLineFilter = computed(() => needsLineStats.value);

function applyLineFilters(list: OdakPackageRow[]): OdakPackageRow[] {
  return filterPackagesByLineAdv(list, lineAdv.value, lineStats.value);
}

function compareText(a: string, b: string): number {
  return a.localeCompare(b, undefined, { numeric: true, sensitivity: 'base' });
}

function compareNumber(a: number | null | undefined, b: number | null | undefined): number {
  const na = a ?? Number.NEGATIVE_INFINITY;
  const nb = b ?? Number.NEGATIVE_INFINITY;
  return na === nb ? 0 : na < nb ? -1 : 1;
}

function sortPackagesClient(list: OdakPackageRow[], sortBy: OdakPackageListSort[]): OdakPackageRow[] {
  const primary = sortBy[0];
  if (!primary) return list;
  const dir = primary.order === 'desc' ? -1 : 1;
  const key = primary.key;
  return [...list].sort((a, b) => {
    let cmp = 0;
    switch (key) {
      case 'displayNo':
        cmp = compareText(packageDisplayNo(a), packageDisplayNo(b));
        break;
      case 'name':
        cmp = compareText(String(a.name ?? ''), String(b.name ?? ''));
        break;
      case 'customer':
        cmp = compareText(
          customerLabelFromRow(a, customerLabels.value),
          customerLabelFromRow(b, customerLabels.value)
        );
        break;
      case 'statusLabel':
        cmp = compareText(String(a.status ?? ''), String(b.status ?? ''));
        break;
      case 'beginDate':
        cmp = compareText(String(a.beginDate ?? ''), String(b.beginDate ?? ''));
        break;
      case 'deliveryDate':
        cmp = compareText(String(a.deliveryDate ?? ''), String(b.deliveryDate ?? ''));
        break;
      case 'partCount':
        cmp = compareNumber(a.partCount, b.partCount);
        break;
      case 'stockCount':
        cmp = compareNumber(a.stockCount, b.stockCount);
        break;
      case 'lineCount':
        cmp = compareNumber(a.lineCount ?? lineStats.value.get(packageDataId(a))?.lineCount, b.lineCount ?? lineStats.value.get(packageDataId(b))?.lineCount);
        break;
      default:
        cmp = 0;
    }
    return cmp * dir;
  });
}

function lineCountFor(item: OdakPackageRow): string {
  if (item.lineCount != null && item.lineCount >= 0) return String(item.lineCount);
  const fromStats = lineStats.value.get(packageDataId(item))?.lineCount;
  if (fromStats != null && fromStats > 0) return String(fromStats);
  return '—';
}

function rowPo(item: OdakPackageRow): string {
  return lineStats.value.get(packageDataId(item))?.customerPoNos || '—';
}

function rowProjectNo(item: OdakPackageRow): string {
  return lineStats.value.get(packageDataId(item))?.customerProjectNos || '—';
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

function onListFiltersUpdate(filters: AfListFilter[]) {
  activeListFilters.value = filters;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void fetchPackages();
}

function clearLineSearch() {
  lineAdv.value = {
    customerPo: '',
    customerProjectNo: '',
    customerPoItem: '',
    productDesc: '',
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
        lineAdv: lineAdv.value,
        sortBy: tableSortBy.value,
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

    const skip = hasLineFilter.value ? 0 : (tablePage.value - 1) * tableItemsPerPage.value;
    const limit = hasLineFilter.value ? 500 : tableItemsPerPage.value;

    const resp = await fetchOdakPackagesPage({
      statusTab: statusTab.value,
      skip,
      limit,
      search: searchText() || undefined,
      advancedFilters: activeListFilters.value,
      sortBy: tableSortBy.value,
    });

    let filtered = [...resp.items];

    if (needsLineStats.value) {
      const stats = await fetchPackageLineStatsMap(filtered.map((x) => packageDataId(x)));
      lineStats.value = stats;
      filtered = applyLineFilters(filtered);
    }

    if (hasLineFilter.value) {
      filtered = sortPackagesClient(filtered, tableSortBy.value);
      const start = (tablePage.value - 1) * tableItemsPerPage.value;
      totalCount.value = filtered.length;
      items.value = filtered.slice(start, start + tableItemsPerPage.value);
    } else {
      items.value = filtered;
      totalCount.value = resp.total ?? filtered.length;
      // B: gorunen sayfa icin PO/Proje ozeti (chunk basina ~3 paralel DG cagrisi / 20 paket).
      const pageIds = items.value.map((x) => packageDataId(x)).filter(Boolean);
      lineStats.value = pageIds.length ? await fetchPackageLineStatsMap(pageIds) : new Map();
    }
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
  if (tab === 'lines') return 'lines';
  if (tab === 'quality') return 'quality';
  return 'summary';
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
watch(lineAdv, scheduleFetch, { deep: true });

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
  void initPackagesPage();
});

async function initPackagesPage() {
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
        <v-btn icon variant="outlined" size="small" :loading="loading" @click="fetchPackages">
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

        <v-expansion-panels v-model="lineSearchPanelOpen" class="pb-2">
          <v-expansion-panel>
            <v-expansion-panel-title>{{ t('odakSiparis.packages.lineSearchPanel') }}</v-expansion-panel-title>
            <v-expansion-panel-text>
              <v-alert type="info" variant="tonal" density="compact" class="mb-3">
                {{ t('odakSiparis.packages.lineSearchHint') }}
              </v-alert>
              <v-row dense>
                <v-col cols="12" sm="6" md="3">
                  <v-text-field
                    v-model="lineAdv.customerPo"
                    :label="t('odakSiparis.packages.searchFields.customerPo')"
                    density="compact"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="6" md="3">
                  <v-text-field
                    v-model="lineAdv.customerProjectNo"
                    :label="t('odakSiparis.packages.searchFields.customerProjectNo')"
                    density="compact"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="6" md="3">
                  <v-text-field
                    v-model="lineAdv.customerPoItem"
                    :label="t('odakSiparis.packages.searchFields.customerPoItem')"
                    density="compact"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="6" md="3">
                  <v-text-field
                    v-model="lineAdv.productDesc"
                    :label="t('odakSiparis.packages.searchFields.productDesc')"
                    density="compact"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" class="d-flex ga-2">
                  <v-btn size="small" variant="tonal" @click="scheduleFetch">
                    {{ t('odakSiparis.packages.search') }}
                  </v-btn>
                  <v-btn size="small" variant="text" @click="clearLineSearch">
                    {{ t('odakSiparis.packages.clear') }}
                  </v-btn>
                </v-col>
              </v-row>
            </v-expansion-panel-text>
          </v-expansion-panel>
        </v-expansion-panels>
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
        <v-alert v-if="hasLineFilter" type="info" variant="tonal" density="compact" class="mb-4">
          {{ t('odakSiparis.packages.clientFilterHint') }}
        </v-alert>

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
                <OdakSiparisPackageExpandPanel
                  :key="`${packageDataId(item)}-${expandRefreshToken}`"
                  :package-row="item"
                  :customer-labels="customerLabels"
                  :initial-tab="expandActiveTab"
                  @open-customer="openCustomerDrawer"
                  @update:active-tab="expandActiveTab = $event"
                />
              </td>
            </tr>
          </template>
          <template #item.displayNo="{ item }">
            <a
              href="#"
              class="text-primary text-decoration-none font-weight-medium"
              @click.prevent="toggleExpand(item)"
            >
              {{ packageDisplayNo(item) }}
            </a>
          </template>
          <template #item.customer="{ item }">
            <a
              v-if="customerIdFromRow(item)"
              href="#"
              class="text-primary text-decoration-none"
              @click.prevent="openCustomerDrawer(customerIdFromRow(item))"
            >
              {{ customerLabelFromRow(item, customerLabels) }}
            </a>
            <span v-else>{{ customerLabelFromRow(item, customerLabels) }}</span>
          </template>
          <template #item.customerPo="{ item }">
            {{ rowPo(item) }}
          </template>
          <template #item.projectNo="{ item }">
            {{ rowProjectNo(item) }}
          </template>
          <template #item.partCount="{ item }">
            {{ formatOdakNumber(item.partCount) }}
          </template>
          <template #item.stockCount="{ item }">
            {{ formatOdakNumber(item.stockCount) }}
          </template>
          <template #item.lineCount="{ item }">
            {{ lineCountFor(item) }}
          </template>
          <template #item.statusLabel="{ item }">
            {{ packageStatusLabel(item.status) }}
          </template>
          <template #item.beginDate="{ item }">
            {{ formatOdakDate(item.beginDate) }}
          </template>
          <template #item.deliveryDate="{ item }">
            {{ formatOdakDate(item.deliveryDate) }}
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
