<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import OdakSiparisGeneralShipmentDialog from '@/components/apps/odak-siparis/OdakSiparisGeneralShipmentDialog.vue';
import OdakSiparisShipmentExpandPanel from '@/components/apps/odak-siparis/OdakSiparisShipmentExpandPanel.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';
import {
  ODAK_DATA_TABLE_EXPAND_COLUMN,
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_QCF_STATUS_OPTIONS,
  ODAK_RECORD_SCOPE_OPTIONS,
  ODAK_SHIPMENT_STATUS_OPTIONS,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import {
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchCustomerRelationOptions,
  fetchPackageRelationOptions,
  resolveDataTableRow,
} from '@/utils/odakSiparisService';
import {
  deleteGeneralOdakShipment,
  fetchOdakShipmentsPage,
  fetchShipmentLineQtyMap,
  formatShipmentDate,
  normalizeRecordScope,
  recordScopeLabel,
  shipmentDataId,
  shipmentStatusLabel,
  type OdakShipmentDialogMode,
  type OdakShipmentScopeTab,
  type OdakShipmentStatusTab,
} from '@/utils/odakSiparisShipmentService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const scopeTab = ref<OdakShipmentScopeTab>('all');
const statusTab = ref<OdakShipmentStatusTab>('all');
const searchQuery = ref('');
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakShipmentRow[]>([]);
const lineQtyByShipment = ref<Map<string, number>>(new Map());
const customerLabels = ref<Record<string, string>>({});
const activeListFilters = ref<AfListFilter[]>([]);
const relationFilterOptions = ref<Record<string, { value: string; title: string }[]>>({});
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);

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

function truncateText(text: string | null | undefined, maxLength = CONTENT_SUMMARY_MAX): string {
  const value = String(text ?? '').trim();
  if (!value) return '—';
  if (value.length <= maxLength) return value;
  return `${value.slice(0, maxLength)}…`;
}

function contentSummaryFromRow(row: OdakShipmentRow): string {
  return truncateText(row.headerDescription || row.notes);
}

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

const scopeTabs = computed(() => [
  { value: 'all' as const, label: t('odakSiparis.globalShipments.tabs.all') },
  { value: 'package' as const, label: t('odakSiparis.globalShipments.tabs.package') },
  { value: 'general' as const, label: t('odakSiparis.globalShipments.tabs.general') },
]);

const statusTabs = computed(() => [
  { value: 'all' as const, label: t('odakSiparis.globalShipments.tabs.statusAll') },
  { value: 'completed' as const, label: t('odakSiparis.globalShipments.tabs.completed') },
  { value: 'planned' as const, label: t('odakSiparis.globalShipments.tabs.planned') },
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
  if (tablePage.value !== 1) tablePage.value = 1;
  else void loadItems();
}

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

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  { title: t('odakSiparis.shipments.columns.waybillNo'), key: 'waybillNo', width: 118, minWidth: 118 },
  { title: t('odakSiparis.globalShipments.columns.scope'), key: 'scopeLabel', width: 156, minWidth: 156 },
  {
    title: t('odakSiparis.globalShipments.columns.content'),
    key: 'headerDescription',
    minWidth: 220,
  },
  {
    title: t('odakSiparis.packages.columns.customer'),
    key: 'customerLabel',
    width: 140,
    minWidth: 120,
  },
  { title: t('odakSiparis.shipments.columns.shipmentDate'), key: 'shipmentDate', width: 112, minWidth: 112 },
  { title: t('odakSiparis.shipments.columns.status'), key: 'status', width: 118, minWidth: 118 },
  { title: t('odakSiparis.shipments.columns.lineQty'), key: 'lineQty', width: 88, minWidth: 88, align: 'end' as const },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    width: 120,
    minWidth: 120,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

const tableItems = computed(() =>
  items.value.map((row) => {
    const id = shipmentDataId(row);
    return {
      raw: row,
      __dataId: id,
      waybillNo: row.waybillNo || '—',
      scopeLabel: recordScopeLabel(row.recordScope),
      headerDescription: row.headerDescription || row.notes || '—',
      headerDescriptionSummary: contentSummaryFromRow(row),
      headerDescriptionFull: (row.headerDescription || row.notes || '').trim(),
      customerLabel: customerLabelFromRow(row, customerLabels.value),
      shipmentDate: formatShipmentDate(row.shipmentDate),
      status: shipmentStatusLabel(row.status),
      lineQty: id ? lineQtyByShipment.value.get(id) ?? '—' : '—',
    };
  })
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
        scopeTab: scopeTab.value,
        statusTab: statusTab.value,
        search: searchQuery.value.trim(),
        page: tablePage.value,
        limit: tableItemsPerPage.value,
        advancedFilters: activeListFilters.value,
      }),
    ]);
    customerLabels.value = labels;
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

watch([scopeTab, statusTab, tablePage, tableItemsPerPage], () => void loadItems());
watch(searchQuery, () => {
  tablePage.value = 1;
  void loadItems();
});
watch(expandedIds, (ids) => {
  if (ids.length > 1) {
    expandedIds.value = [ids[ids.length - 1]!];
  }
});

onMounted(() => void loadItems());
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  <v-card elevation="10">
    <v-card-text>
      <div class="d-flex flex-wrap align-center ga-3 mb-4">
        <v-btn-toggle v-model="scopeTab" mandatory density="compact" color="primary" variant="outlined">
          <v-btn v-for="tab in scopeTabs" :key="tab.value" :value="tab.value" size="small">
            {{ tab.label }}
          </v-btn>
        </v-btn-toggle>
        <v-btn-toggle v-model="statusTab" mandatory density="compact" color="primary" variant="outlined">
          <v-btn v-for="tab in statusTabs" :key="tab.value" :value="tab.value" size="small">
            {{ tab.label }}
          </v-btn>
        </v-btn-toggle>
        <v-spacer />
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
        @update:filters="onListFiltersUpdate"
        @advanced-open="loadFilterRelationOptions"
      />
      <p class="text-caption text-medium-emphasis mb-4">
        {{ t('odakSiparis.globalShipments.filters.customerHint') }}
      </p>

      <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
        {{ errorMessage }}
      </v-alert>

      <div class="odak-shipments-list-scroll">
        <v-data-table-server
          v-model:page="tablePage"
          v-model:items-per-page="tableItemsPerPage"
          v-model:expanded="expandedIds"
          :headers="headers"
          :items="tableItems"
          :items-length="totalCount"
          :loading="loading"
          item-value="__dataId"
          show-expand
          :expand-on-click="false"
          density="compact"
          class="border rounded-md odak-shipments-list-table"
          :class="ODAK_SUB_LIST_TABLE_CLASS"
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
              :title="String(item.waybillNo)"
              @click.prevent="toggleExpand(item)"
            >
              {{ item.waybillNo }}
            </a>
          </template>
          <template #item.scopeLabel="{ item }">
            <span class="odak-shipments-cell-ellipsis" :title="String(item.scopeLabel)">
              {{ item.scopeLabel }}
            </span>
          </template>
          <template #item.headerDescription="{ item }">
            <span
              class="odak-shipments-cell-ellipsis"
              :title="item.headerDescriptionFull || undefined"
            >
              {{ item.headerDescriptionSummary }}
            </span>
          </template>
          <template #item.customerLabel="{ item }">
            <span class="odak-shipments-cell-ellipsis" :title="String(item.customerLabel)">
              {{ item.customerLabel }}
            </span>
          </template>
          <template #item.lineQty="{ item }">
            <span class="d-block text-end tabular-nums">{{ item.lineQty }}</span>
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
