<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import OdakSiparisGeneralShipmentDialog from '@/components/apps/odak-siparis/OdakSiparisGeneralShipmentDialog.vue';
import OdakSiparisShipmentExpandPanel from '@/components/apps/odak-siparis/OdakSiparisShipmentExpandPanel.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useOdakFieldAccess } from '@/composables/useOdakFieldAccess';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { useAuthStore } from '@/stores/auth';
import { useOdakSiparisHubSettingsStore } from '@/stores/apps/odakSiparisHubSettings';
import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';
import {
  applyListColumnFormatting,
  getListColumnCellStyle,
} from '@/utils/afListColumnFormat';
import {
  ODAK_DATA_TABLE_EXPAND_COLUMN,
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_QCF_STATUS_OPTIONS,
  ODAK_RECORD_SCOPE_OPTIONS,
  ODAK_SHIPMENT_STATUS_OPTIONS,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import {
  buildGlobalShipmentListHeaders,
  buildGlobalShipmentListSort,
  fieldNameFromGlobalShipmentListKey,
  globalShipmentContentFull,
  globalShipmentListCellRaw,
  listSortKeyFromGlobalShipmentField,
  ODAK_GLOBAL_SHIPMENT_LIST_KEY_TO_FIELD,
  type GlobalShipmentListSort,
} from '@/utils/odakSiparisGlobalShipmentListSettings';
import { loadOdakShipmentFieldPoliciesOnly } from '@/utils/odakSiparisHubSettingsService';
import { odakGlobalShipmentSettingsFieldLabelTr } from '@/utils/odakSiparisSettingsLabels';
import {
  fetchCustomerLabelMap,
  fetchCustomerRelationOptions,
  fetchPackageCustomerLabelMap,
  fetchPackageRelationOptions,
  resolveDataTableRow,
} from '@/utils/odakSiparisService';
import {
  deleteGeneralOdakShipment,
  fetchOdakShipmentsPage,
  fetchShipmentLineQtyMap,
  normalizeRecordScope,
  ODAK_GLOBAL_SHIPMENTS_DEFAULT_FILTERS,
  shipmentDataId,
  type OdakShipmentDialogMode,
} from '@/utils/odakSiparisShipmentService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, SettingsIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const auth = useAuthStore();
const hubStore = useOdakSiparisHubSettingsStore();
const route = useRoute();
const panelError = usePanelErrorNotify('errors.dg.generic');

const searchQuery = ref('');
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakShipmentRow[]>([]);
const lineQtyByShipment = ref<Map<string, number>>(new Map());
const customerLabels = ref<Record<string, string>>({});
const packageCustomerLabels = ref<Record<string, string>>({});
const activeListFilters = ref<AfListFilter[]>([...ODAK_GLOBAL_SHIPMENTS_DEFAULT_FILTERS]);
const relationFilterOptions = ref<Record<string, { value: string; title: string }[]>>({});
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);
const tableSortBy = ref<GlobalShipmentListSort[]>([{ key: 'shipmentDate', order: 'desc' }]);
const hubSortInitialized = ref(false);
/** İlk yükleme tamamlanana kadar tablo/filtre event'lerinin çift fetch tetiklemesini engeller. */
const pageDataInitialized = ref(false);

const listConfig = computed(() => hubStore.globalShipmentsListConfig);
const fieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });
const { canViewListColumn } = useOdakFieldAccess(fieldPolicies, ODAK_GLOBAL_SHIPMENT_LIST_KEY_TO_FIELD);

const dialogOpen = ref(false);
const dialogMode = ref<OdakShipmentDialogMode>('view');
const dialogId = ref<string | undefined>();
const dialogSeed = ref<OdakShipmentRow | null>(null);

const deleteDialog = ref(false);
const rowToDelete = ref<OdakShipmentRow | null>(null);
const deleting = ref(false);

const expandedIds = ref<string[]>([]);
const expandRefreshToken = ref(0);

/** Tablo hücresinde gösterilecek içerik özeti uzunluğu; tam metin expand panelde. */
const CONTENT_SUMMARY_MAX = 80;

function toggleExpand(item: { raw: OdakShipmentRow; __dataId?: string }) {
  const row = resolveDataTableRow(item);
  const id = shipmentDataId(row);
  if (!id) return;
  if (expandedIds.value.includes(id)) {
    expandedIds.value = [];
  } else {
    expandedIds.value = [id];
    expandRefreshToken.value += 1;
  }
}

const page = computed(() => ({ title: t('odakSiparis.globalShipments.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.globalShipments.title'), disabled: true, href: '#' },
]);

const shipmentFilterColumns = computed<AfFilterColumn[]>(() => [
  {
    key: 'parentPackageId',
    label: t('odakSiparis.globalShipments.filters.parentPackage'),
    kind: 'relation',
  },
  {
    key: 'customerId',
    label: t('odakSiparis.packages.columns.customer'),
    kind: 'relation',
  },
  {
    key: 'waybillNo',
    label: t('odakSiparis.shipments.columns.waybillNo'),
    kind: 'text',
  },
  {
    key: 'headerDescription',
    label: t('odakSiparis.globalShipments.columns.content'),
    kind: 'text',
  },
  {
    key: 'shipmentDate',
    label: t('odakSiparis.shipments.columns.shipmentDate'),
    kind: 'date',
  },
  {
    key: 'recordScope',
    label: t('odakSiparis.globalShipments.columns.scope'),
    kind: 'select',
    selectItems: ODAK_RECORD_SCOPE_OPTIONS.map((o) => ({ value: o.value, title: o.title })),
  },
  {
    key: 'status',
    label: t('odakSiparis.shipments.columns.status'),
    kind: 'select',
    selectItems: ODAK_SHIPMENT_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title })),
  },
  {
    key: 'controlType',
    label: t('odakSiparis.shipments.columns.controlType'),
    kind: 'text',
  },
  {
    key: 'qcfStatus',
    label: t('odakSiparis.shipments.columns.qcfStatus'),
    kind: 'select',
    selectItems: ODAK_QCF_STATUS_OPTIONS.map((o) => ({ value: o.value, title: o.title })),
  },
  {
    key: 'qcfReferenceNo',
    label: t('odakSiparis.shipments.fields.qcfReferenceNo'),
    kind: 'text',
  },
  {
    key: 'notes',
    label: t('odakSiparis.shipments.fields.notes'),
    kind: 'text',
  },
]);

function onListFiltersUpdate(filters: AfListFilter[]) {
  activeListFilters.value = filters;
  if (!pageDataInitialized.value) return;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void loadItems();
}

const showCustomerScopeHint = computed(() => {
  const filters = activeListFilters.value;
  return filters.some((f) => f.field === 'customerId' && f.value?.trim());
});

async function loadFilterRelationOptions() {
  const [customers, packages] = await Promise.all([
    fetchCustomerRelationOptions(),
    fetchPackageRelationOptions(),
  ]);
  relationFilterOptions.value = {
    customerId: customers,
    parentPackageId: packages,
  };
}

function columnTitle(fieldName: string, _listKey: string): string {
  return odakGlobalShipmentSettingsFieldLabelTr(fieldName);
}

function listCellContext() {
  return {
    lineQtyByShipmentId: lineQtyByShipment.value,
    customerLabels: customerLabels.value,
    packageCustomerLabels: packageCustomerLabels.value,
    contentSummaryMax: CONTENT_SUMMARY_MAX,
  };
}

function columnConfigForListKey(listKey: string) {
  const fieldName = fieldNameFromGlobalShipmentListKey(listKey);
  return listConfig.value.columns.find((c) => c.fieldName === fieldName);
}

function cellDisplayValue(raw: string, listKey: string, row: OdakShipmentRow): string {
  const col = columnConfigForListKey(listKey);
  return applyListColumnFormatting(raw, col?.format);
}

function cellStyle(listKey: string, raw: string, row: OdakShipmentRow): Record<string, string> {
  const col = columnConfigForListKey(listKey);
  const fieldName = fieldNameFromGlobalShipmentListKey(listKey);
  return getListColumnCellStyle(raw, fieldName, col?.format, row as Record<string, unknown>);
}

const configurableHeaders = computed(() =>
  buildGlobalShipmentListHeaders(listConfig.value, columnTitle, (listKey) => canViewListColumn(listKey))
);

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  ...configurableHeaders.value,
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    width: 120,
    minWidth: 120,
    sortable: false,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

const SPECIAL_LIST_KEYS = new Set([
  'waybillNo',
  'scopeLabel',
  'customerLabel',
  'headerDescription',
  'lineQty',
]);

const genericListColumns = computed(() =>
  configurableHeaders.value.filter((h) => !SPECIAL_LIST_KEYS.has(h.key))
);

const tableItems = computed(() =>
  items.value.map((row) => ({
    raw: row,
    __dataId: shipmentDataId(row),
  }))
);

async function loadLineQuantities(rows: OdakShipmentRow[]) {
  const ids = rows.map((row) => shipmentDataId(row)).filter(Boolean);
  if (!ids.length) {
    lineQtyByShipment.value = new Map();
    return;
  }
  try {
    lineQtyByShipment.value = await fetchShipmentLineQtyMap(ids);
  } catch {
    lineQtyByShipment.value = new Map();
  }
}

async function loadItems() {
  loading.value = true;
  errorMessage.value = '';
  lineQtyByShipment.value = new Map();
  let rows: OdakShipmentRow[] = [];
  try {
    const [labels, resp] = await Promise.all([
      fetchCustomerLabelMap(),
      fetchOdakShipmentsPage({
        search: searchQuery.value.trim(),
        page: tablePage.value,
        limit: tableItemsPerPage.value,
        sort: buildGlobalShipmentListSort(tableSortBy.value),
        advancedFilters: activeListFilters.value,
      }),
    ]);
    customerLabels.value = labels;
    packageCustomerLabels.value = await fetchPackageCustomerLabelMap(labels);
    rows = resp.items;
    items.value = rows;
    totalCount.value = resp.total;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
    items.value = [];
    totalCount.value = 0;
  } finally {
    loading.value = false;
  }
  void loadLineQuantities(rows);
}

function openDialog(mode: OdakShipmentDialogMode, row?: OdakShipmentRow) {
  if (row && normalizeRecordScope(row.recordScope) === 'Paketli') {
    return;
  }
  dialogMode.value = mode;
  dialogId.value = row ? shipmentDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

function confirmDelete(row: OdakShipmentRow) {
  if (normalizeRecordScope(row.recordScope) === 'Paketli') return;
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function executeDelete() {
  const row = rowToDelete.value;
  const id = row ? shipmentDataId(row) : '';
  if (!id) return;
  deleting.value = true;
  try {
    await deleteGeneralOdakShipment(id);
    deleteDialog.value = false;
    rowToDelete.value = null;
    await loadItems();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    deleting.value = false;
  }
}

async function applyHubSortDefaults() {
  if (hubSortInitialized.value) return;
  const sortField = listConfig.value.defaultSortBy ?? 'shipmentDate';
  const sortKey = listSortKeyFromGlobalShipmentField(sortField);
  tableSortBy.value = [{ key: sortKey, order: listConfig.value.defaultSortOrder ?? 'desc' }];
  hubSortInitialized.value = true;
}

type TableOptions = {
  page: number;
  itemsPerPage: number;
  sortBy?: GlobalShipmentListSort[];
};

function onTableOptions(options: TableOptions) {
  if (!pageDataInitialized.value) return;
  const nextSort =
    Array.isArray(options.sortBy) && options.sortBy.length
      ? options.sortBy
      : [{ key: 'shipmentDate', order: 'desc' as const }];
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
  void loadItems();
}

function onPageShow(event: PageTransitionEvent) {
  if (event.persisted) {
    hubSortInitialized.value = false;
    void hubStore.ensureScopeReady('global_shipments_list', true).then(() => {
      void applyHubSortDefaults().then(() => loadItems());
    });
  }
}

async function initShipmentsPage() {
  pageDataInitialized.value = false;
  await hubStore.ensureScopeReady('global_shipments_list', false);
  await applyHubSortDefaults();
  void loadOdakShipmentFieldPoliciesOnly()
    .then((blob) => {
      fieldPolicies.value = blob;
    })
    .catch(() => {
      fieldPolicies.value = { policiesByField: {} };
    });
  await loadFilterRelationOptions();
  await loadItems();
  pageDataInitialized.value = true;
}

watch(
  () => route.fullPath,
  (path, previousPath) => {
    if (path === '/apps/odak-siparis/shipments' && previousPath?.includes('/shipments/settings')) {
      hubSortInitialized.value = false;
      void hubStore.ensureScopeReady('global_shipments_list', true).then(() => {
        void applyHubSortDefaults().then(() => loadItems());
      });
    }
  }
);

watch(searchQuery, () => {
  if (!pageDataInitialized.value) return;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void loadItems();
});
watch(expandedIds, (ids) => {
  if (ids.length > 1) {
    expandedIds.value = [ids[ids.length - 1]!];
  }
});

onMounted(() => {
  if (import.meta.client) {
    window.addEventListener('pageshow', onPageShow);
  }
  void initShipmentsPage();
});

onBeforeUnmount(() => {
  if (import.meta.client) {
    window.removeEventListener('pageshow', onPageShow);
  }
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  <v-card elevation="10">
    <v-card-text>
      <div class="d-flex flex-wrap align-center ga-3 mb-4">
        <v-spacer />
        <v-btn
          v-if="auth.isManager"
          variant="tonal"
          color="primary"
          size="small"
          to="/apps/odak-siparis/shipments/settings"
        >
          <SettingsIcon size="18" class="mr-1" />
          {{ t('odakSiparis.globalShipments.settings.title') }}
        </v-btn>
        <v-text-field
          v-model="searchQuery"
          :label="t('odakSiparis.globalShipments.searchWaybill')"
          density="compact"
          hide-details
          style="max-width: 220px"
          variant="outlined"
        />
        <v-btn icon variant="text" @click="loadItems">
          <RefreshIcon size="18" />
        </v-btn>
        <v-btn color="primary" variant="flat" @click="openDialog('create')">
          <PlusIcon size="18" class="mr-1" />
          {{ t('odakSiparis.globalShipments.addGeneral') }}
        </v-btn>
      </div>

      <AfListFilters
        class="mb-2"
        :columns="shipmentFilterColumns"
        :relation-options-by-key="relationFilterOptions"
        :initial-filters="ODAK_GLOBAL_SHIPMENTS_DEFAULT_FILTERS"
        :initial-panel-open="true"
        @update:filters="onListFiltersUpdate"
        @advanced-open="loadFilterRelationOptions"
      />
      <div class="mb-4">
        <p class="text-caption text-medium-emphasis mb-1">
          {{ t('odakSiparis.globalShipments.filters.customerHint') }}
        </p>
        <p v-if="showCustomerScopeHint" class="text-caption text-medium-emphasis mb-0">
          {{ t('odakSiparis.globalShipments.filters.customerScopeHint') }}
        </p>
      </div>

      <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
        {{ errorMessage }}
      </v-alert>

      <div class="odak-shipments-list-scroll">
        <v-data-table-server
          v-model:expanded="expandedIds"
          :headers="headers"
          :items="tableItems"
          :items-length="totalCount"
          :loading="loading"
          :page="tablePage"
          :items-per-page="tableItemsPerPage"
          :sort-by="tableSortBy"
          item-value="__dataId"
          show-expand
          :expand-on-click="false"
          density="compact"
          class="border rounded-md odak-shipments-list-table"
          :class="ODAK_SUB_LIST_TABLE_CLASS"
          @update:options="onTableOptions"
        >
          <template #expanded-row="{ columns, item }">
            <tr>
              <td :colspan="columns.length" class="pa-0">
                <div class="odak-shipment-expand-viewport">
                  <OdakSiparisShipmentExpandPanel
                    :key="`${item.__dataId}-${expandRefreshToken}`"
                    :shipment-row="item.raw"
                    :customer-labels="customerLabels"
                    :refresh-token="expandRefreshToken"
                  />
                </div>
              </td>
            </tr>
          </template>
          <template #item.waybillNo="{ item }">
            <a
              href="#"
              class="text-primary text-decoration-none font-weight-medium odak-shipments-cell-ellipsis"
              :style="cellStyle('waybillNo', globalShipmentListCellRaw(item.raw, 'waybillNo', listCellContext()), item.raw)"
              :title="globalShipmentListCellRaw(item.raw, 'waybillNo', listCellContext())"
              @click.prevent="toggleExpand(item)"
            >
              {{
                cellDisplayValue(
                  globalShipmentListCellRaw(item.raw, 'waybillNo', listCellContext()),
                  'waybillNo',
                  item.raw
                )
              }}
            </a>
          </template>
          <template #item.scopeLabel="{ item }">
            <span
              class="odak-shipments-cell-ellipsis"
              :style="cellStyle('scopeLabel', globalShipmentListCellRaw(item.raw, 'scopeLabel', listCellContext()), item.raw)"
              :title="globalShipmentListCellRaw(item.raw, 'scopeLabel', listCellContext())"
            >
              {{
                cellDisplayValue(
                  globalShipmentListCellRaw(item.raw, 'scopeLabel', listCellContext()),
                  'scopeLabel',
                  item.raw
                )
              }}
            </span>
          </template>
          <template #item.headerDescription="{ item }">
            <span
              class="odak-shipments-cell-ellipsis"
              :style="cellStyle('headerDescription', globalShipmentListCellRaw(item.raw, 'headerDescription', listCellContext()), item.raw)"
              :title="globalShipmentContentFull(item.raw) || undefined"
            >
              {{
                cellDisplayValue(
                  globalShipmentListCellRaw(item.raw, 'headerDescription', listCellContext()),
                  'headerDescription',
                  item.raw
                )
              }}
            </span>
          </template>
          <template #item.customerLabel="{ item }">
            <span
              class="odak-shipments-cell-ellipsis"
              :style="cellStyle('customerLabel', globalShipmentListCellRaw(item.raw, 'customerLabel', listCellContext()), item.raw)"
              :title="globalShipmentListCellRaw(item.raw, 'customerLabel', listCellContext())"
            >
              {{
                cellDisplayValue(
                  globalShipmentListCellRaw(item.raw, 'customerLabel', listCellContext()),
                  'customerLabel',
                  item.raw
                )
              }}
            </span>
          </template>
          <template #item.lineQty="{ item }">
            <span
              class="d-block text-end tabular-nums"
              :style="cellStyle('lineQty', globalShipmentListCellRaw(item.raw, 'lineQty', listCellContext()), item.raw)"
            >
              {{
                cellDisplayValue(
                  globalShipmentListCellRaw(item.raw, 'lineQty', listCellContext()),
                  'lineQty',
                  item.raw
                )
              }}
            </span>
          </template>
          <template v-for="col in genericListColumns" :key="col.key" #[`item.${col.key}`]="{ item }">
            <span
              :style="cellStyle(col.key, globalShipmentListCellRaw(item.raw, col.key, listCellContext()), item.raw)"
            >
              {{
                cellDisplayValue(
                  globalShipmentListCellRaw(item.raw, col.key, listCellContext()),
                  col.key,
                  item.raw
                )
              }}
            </span>
          </template>
          <template #item.actions="{ item }">
            <div class="d-inline-flex align-center justify-end ga-1">
              <v-btn
                icon
                variant="text"
                size="x-small"
                :disabled="normalizeRecordScope(item.raw.recordScope) === 'Paketli'"
                @click="openDialog('view', item.raw)"
              >
                <EyeIcon size="18" />
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="x-small"
                :disabled="normalizeRecordScope(item.raw.recordScope) !== 'Genel'"
                @click="openDialog('edit', item.raw)"
              >
                <EditIcon size="18" />
              </v-btn>
              <v-btn
                icon
                variant="text"
                size="x-small"
                color="error"
                :disabled="normalizeRecordScope(item.raw.recordScope) !== 'Genel'"
                @click="confirmDelete(item.raw)"
              >
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
        </v-data-table-server>
      </div>
    </v-card-text>
  </v-card>

  <OdakSiparisGeneralShipmentDialog
    v-model="dialogOpen"
    :mode="dialogMode"
    :shipment-id="dialogId"
    :seed-row="dialogSeed"
    @saved="loadItems"
  />

  <v-dialog v-model="deleteDialog" max-width="420">
    <v-card>
      <v-card-title>{{ t('odakSiparis.shipments.deleteTitle') }}</v-card-title>
      <v-card-text>{{ t('odakSiparis.shipments.deleteConfirm') }}</v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="deleteDialog = false">{{ t('odakSiparis.packages.cancel') }}</v-btn>
        <v-btn color="error" variant="flat" :loading="deleting" @click="executeDelete">
          {{ t('odakSiparis.packages.delete') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  </div>
</template>

<style scoped>
.odak-shipments-list-scroll {
  display: block;
  width: 100%;
  max-width: 100%;
  min-width: 0;
  overflow-x: auto;
  overflow-y: visible;
  -webkit-overflow-scrolling: touch;
  container-type: inline-size;
  container-name: odak-shipments-scroll;
  padding-bottom: 2px;
}

.odak-shipments-list-table {
  display: block;
  width: fit-content;
  min-width: 100%;
}

.odak-shipments-list-table :deep(.v-table),
.odak-shipments-list-table :deep(.v-table__wrapper) {
  overflow: visible !important;
}

.odak-shipments-list-table :deep(table) {
  width: 100% !important;
  min-width: 1020px;
  table-layout: auto !important;
}

.odak-shipment-expand-viewport {
  position: sticky;
  left: 0;
  z-index: 2;
  box-sizing: border-box;
  width: 100%;
  max-width: 100%;
  background: rgb(var(--v-theme-surface));
}

@supports (width: 100cqi) {
  .odak-shipment-expand-viewport {
    width: 100cqi;
    max-width: 100cqi;
  }
}

.odak-shipments-list-table :deep(.v-data-table__expanded__content > td) {
  overflow: visible;
  padding: 0 !important;
}

.odak-shipments-list-table :deep(table > thead > tr > th:first-child),
.odak-shipments-list-table :deep(table > tbody > tr:not(.v-data-table__expanded__content) > td:first-child) {
  position: sticky;
  left: 0;
  z-index: 3;
  background: rgb(var(--v-theme-surface));
  box-shadow: 6px 0 6px -6px rgba(0, 0, 0, 0.12);
}

.odak-shipments-list-table :deep(th.v-data-table__th),
.odak-shipments-list-table :deep(td.v-data-table__td) {
  overflow: hidden;
  vertical-align: middle;
}

.odak-shipments-cell-ellipsis {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  max-width: 100%;
}

/* İşlemler sütunu — yatay scroll'da sağda sabit. */
.odak-shipments-list-table :deep(table > thead > tr > th:last-child),
.odak-shipments-list-table :deep(table > tbody > tr:not(.v-data-table__expanded__content) > td:last-child) {
  position: sticky;
  right: 0;
  overflow: visible;
  background: rgb(var(--v-theme-surface));
  box-shadow: -6px 0 6px -6px rgba(0, 0, 0, 0.18);
}

.odak-shipments-list-table :deep(table > tbody > tr:not(.v-data-table__expanded__content) > td:last-child) {
  z-index: 1;
}

.odak-shipments-list-table :deep(table > thead > tr > th:last-child) {
  z-index: 2;
}
</style>
