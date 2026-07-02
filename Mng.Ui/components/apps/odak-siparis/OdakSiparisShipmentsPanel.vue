<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisShipmentDialog from '@/components/apps/odak-siparis/OdakSiparisShipmentDialog.vue';
import OdakSiparisShipmentExpandPanel from '@/components/apps/odak-siparis/OdakSiparisShipmentExpandPanel.vue';
import OdakSiparisSubListScroll from '@/components/apps/odak-siparis/OdakSiparisSubListScroll.vue';
import OdakSiparisSubListToolbar from '@/components/apps/odak-siparis/OdakSiparisSubListToolbar.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ODAK_DATA_TABLE_EXPAND_COLUMN,
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import { hubListCellDisplayValue, hubListCellStyle } from '@/utils/odakSiparisHubListCellFormat';
import {
  loadOdakLineFieldPoliciesOnly,
  loadOdakShipmentFieldPoliciesOnly,
  loadOdakShipmentListConfigOnly,
} from '@/utils/odakSiparisHubSettingsService';
import { odakShipmentSettingsFieldLabelTr } from '@/utils/odakSiparisSettingsLabels';
import {
  buildShipmentListHeaders,
  defaultOdakShipmentListConfig,
  ODAK_SHIPMENT_LIST_KEY_TO_FIELD,
  shipmentListCellRaw,
  type OdakShipmentListConfig,
} from '@/utils/odakSiparisShipmentListSettings';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { useOdakFieldAccess } from '@/composables/useOdakFieldAccess';
import { listLinesForPackage } from '@/utils/odakSiparisLineService';
import {
  aggregateLineQuantities,
  buildParentLineMap,
  buildShipmentQtySummaryMap,
  countCompletedShipments,
  deleteOdakShipment,
  listShipmentLinesForPackage,
  listShipmentsForPackage,
  shipmentDataId,
  type OdakLineQuantityAggregate,
  type OdakShipmentQtySummary,
  type OdakShipmentDialogMode,
} from '@/utils/odakSiparisShipmentService';
import { formatOdakNumber } from '@/utils/odakSiparisService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const emit = defineEmits<{
  saved: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakShipmentRow[]>([]);
const qtySummaryByShipment = ref<Map<string, OdakShipmentQtySummary>>(new Map());
const packageQtyTotals = ref<OdakLineQuantityAggregate>({
  totalQuantity: 0,
  totalShipped: 0,
  totalRemaining: 0,
});
const expandedShipmentIds = ref<string[]>([]);
const expandRefreshToken = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(10);

const tableItemsPerPageOptions = [
  { value: 10, title: '10' },
  { value: 25, title: '25' },
  { value: 50, title: '50' },
];

const deleteDialog = ref(false);
const rowToDelete = ref<OdakShipmentRow | null>(null);
const deleting = ref(false);

const dialogOpen = ref(false);
const dialogMode = ref<OdakShipmentDialogMode>('view');
const dialogId = ref<string | undefined>();
const dialogSeed = ref<OdakShipmentRow | null>(null);

const completedCount = computed(() => countCompletedShipments(items.value));

const listConfig = ref<OdakShipmentListConfig>(defaultOdakShipmentListConfig());
const fieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });
const lineFieldPolicies = ref<OdakFieldPoliciesBlob>({ policiesByField: {} });
const { canViewListColumn: canViewShipmentColumn } = useOdakFieldAccess(
  fieldPolicies,
  ODAK_SHIPMENT_LIST_KEY_TO_FIELD
);
const { canViewField: canViewLineField } = useOdakFieldAccess(lineFieldPolicies);

function canViewListColumn(listKey: string): boolean {
  if (listKey === 'orderQty' || listKey === 'remainingQty') {
    return canViewLineField('quantity');
  }
  if (listKey === 'lineQty') {
    return canViewLineField('shippedQuantity');
  }
  return canViewShipmentColumn(listKey);
}

function columnTitle(fieldName: string, listKey: string): string {
  void listKey;
  return odakShipmentSettingsFieldLabelTr(fieldName);
}

const configurableHeaders = computed(() =>
  buildShipmentListHeaders(listConfig.value, columnTitle, canViewListColumn)
);

/** Sütun toplam genişliği — yatay scroll tetiklemek için tablo viewport'tan geniş tutulur. */
const tableMinWidthPx = computed(() => {
  let total = 48 + 132;
  for (const h of configurableHeaders.value) {
    total += Number(h.width ?? h.minWidth ?? 112);
  }
  return total;
});

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  ...configurableHeaders.value,
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    width: 132,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

const numericListKeys = new Set(['orderQty', 'lineQty', 'remainingQty']);

function qtyColumnHint(listKey: 'orderQty' | 'lineQty' | 'remainingQty'): string {
  return t(`odakSiparis.shipments.columnHints.${listKey}`);
}

const showQtyFooter = computed(
  () =>
    items.value.length > 0 &&
    (canViewListColumn('orderQty') ||
      canViewListColumn('lineQty') ||
      canViewListColumn('remainingQty'))
);

const footerQtyCells = computed(() => {
  const cells: Array<{ key: string; label: string; value: string }> = [];
  if (canViewListColumn('orderQty')) {
    cells.push({
      key: 'orderQty',
      label: t('odakSiparis.shipments.listFooter.orderQty'),
      value: formatOdakNumber(packageQtyTotals.value.totalQuantity),
    });
  }
  if (canViewListColumn('lineQty')) {
    cells.push({
      key: 'lineQty',
      label: t('odakSiparis.shipments.listFooter.shippedQty'),
      value: formatOdakNumber(packageQtyTotals.value.totalShipped),
    });
  }
  if (canViewListColumn('remainingQty')) {
    cells.push({
      key: 'remainingQty',
      label: t('odakSiparis.shipments.listFooter.remainingQty'),
      value: formatOdakNumber(packageQtyTotals.value.totalRemaining),
    });
  }
  return cells;
});

function shipmentCellContext() {
  return { qtySummaryByShipmentId: qtySummaryByShipment.value };
}

function cellDisplayValue(raw: string, listKey: string, item: OdakShipmentRow): string {
  return hubListCellDisplayValue(raw, listKey, listConfig.value, ODAK_SHIPMENT_LIST_KEY_TO_FIELD, item);
}

function cellStyle(listKey: string, raw: string, item: OdakShipmentRow): Record<string, string> {
  return hubListCellStyle(listKey, raw, listConfig.value, ODAK_SHIPMENT_LIST_KEY_TO_FIELD, item);
}

function toggleExpand(row: OdakShipmentRow) {
  const id = shipmentDataId(row);
  if (!id) return;
  if (expandedShipmentIds.value.includes(id)) {
    expandedShipmentIds.value = [];
  } else {
    expandedShipmentIds.value = [id];
  }
}

async function loadItems() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  qtySummaryByShipment.value = new Map();
  packageQtyTotals.value = { totalQuantity: 0, totalShipped: 0, totalRemaining: 0 };
  let rows: OdakShipmentRow[] = [];
  try {
    const [shipments, shipmentLines, packageLines] = await Promise.all([
      listShipmentsForPackage(props.packageId),
      listShipmentLinesForPackage(props.packageId),
      listLinesForPackage(props.packageId),
    ]);
    rows = shipments;
    items.value = rows;
    qtySummaryByShipment.value = buildShipmentQtySummaryMap(
      shipmentLines,
      buildParentLineMap(packageLines)
    );
    packageQtyTotals.value = aggregateLineQuantities(packageLines);
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakShipmentDialogMode, row?: OdakShipmentRow) {
  dialogMode.value = mode;
  dialogId.value = row ? shipmentDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

function confirmDelete(row: OdakShipmentRow) {
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = rowToDelete.value;
  if (!row) return;
  const id = shipmentDataId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await deleteOdakShipment(id, props.packageId);
    deleteDialog.value = false;
    rowToDelete.value = null;
    await loadItems();
    expandRefreshToken.value += 1;
    emit('saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    deleting.value = false;
  }
}

function onDialogSaved() {
  void loadItems();
  expandRefreshToken.value += 1;
  emit('saved');
}

watch(expandedShipmentIds, (ids) => {
  if (ids.length > 1) expandedShipmentIds.value = [ids[ids.length - 1]!];
});

watch(
  () => props.packageId,
  () => {
    expandedShipmentIds.value = [];
    tablePage.value = 1;
    void loadItems();
  }
);

onMounted(() => {
  void loadOdakShipmentListConfigOnly()
    .then((cfg) => {
      listConfig.value = cfg;
    })
    .catch(() => {
      listConfig.value = defaultOdakShipmentListConfig();
    });
  void loadOdakShipmentFieldPoliciesOnly()
    .then((blob) => {
      fieldPolicies.value = blob;
    })
    .catch(() => {
      fieldPolicies.value = { policiesByField: {} };
    });
  void loadOdakLineFieldPoliciesOnly()
    .then((blob) => {
      lineFieldPolicies.value = blob;
    })
    .catch(() => {
      lineFieldPolicies.value = { policiesByField: {} };
    });
  void loadItems();
});

defineExpose({ reload: loadItems });
</script>

<template>
  <div>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <OdakSiparisSubListScroll sticky-expand-column>
      <template #toolbar>
        <OdakSiparisSubListToolbar>
          <template #info>
            <div class="text-body-2 text-medium-emphasis">
              {{ t('odakSiparis.shipments.summary', { count: items.length, completed: completedCount }) }}
            </div>
          </template>
          <template #actions>
            <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadItems">
              <RefreshIcon size="18" />
            </v-btn>
            <v-btn color="primary" variant="flat" size="small" @click="openDialog('create')">
              <PlusIcon class="mr-1" size="16" />
              {{ t('odakSiparis.shipments.add') }}
            </v-btn>
          </template>
        </OdakSiparisSubListToolbar>
      </template>

      <div class="odak-shipments-table-stack" :style="{ minWidth: `${tableMinWidthPx}px` }">
      <v-data-table
        v-model:expanded="expandedShipmentIds"
        v-model:page="tablePage"
        v-model:items-per-page="tableItemsPerPage"
        :headers="headers"
        :items="items"
        :loading="loading"
        item-value="__dataId"
        show-expand
        :expand-on-click="false"
        density="compact"
        :class="[
          'border',
          'rounded-md',
          'bg-surface',
          ODAK_SUB_LIST_TABLE_CLASS,
          showQtyFooter && items.length ? 'odak-shipments-table--with-footer' : '',
        ]"
        :items-per-page-options="tableItemsPerPageOptions"
      >
        <template #header.orderQty="{ column }">
          <span class="d-inline-flex align-center justify-end ga-1 ms-auto">
            <span>{{ column.title }}</span>
            <v-tooltip location="top" max-width="300">
              <template #activator="{ props: tipProps }">
                <v-icon v-bind="tipProps" size="14" class="text-medium-emphasis">mdi-information-outline</v-icon>
              </template>
              {{ qtyColumnHint('orderQty') }}
            </v-tooltip>
          </span>
        </template>

        <template #header.lineQty="{ column }">
          <span class="d-inline-flex align-center justify-end ga-1 ms-auto">
            <span>{{ column.title }}</span>
            <v-tooltip location="top" max-width="300">
              <template #activator="{ props: tipProps }">
                <v-icon v-bind="tipProps" size="14" class="text-medium-emphasis">mdi-information-outline</v-icon>
              </template>
              {{ qtyColumnHint('lineQty') }}
            </v-tooltip>
          </span>
        </template>

        <template #header.remainingQty="{ column }">
          <span class="d-inline-flex align-center justify-end ga-1 ms-auto">
            <span>{{ column.title }}</span>
            <v-tooltip location="top" max-width="300">
              <template #activator="{ props: tipProps }">
                <v-icon v-bind="tipProps" size="14" class="text-medium-emphasis">mdi-information-outline</v-icon>
              </template>
              {{ qtyColumnHint('remainingQty') }}
            </v-tooltip>
          </span>
        </template>

        <template #expanded-row="{ columns, item }">
          <tr>
            <td :colspan="columns.length" class="pa-0">
              <OdakSiparisShipmentExpandPanel
                :key="`${shipmentDataId(item)}-${expandRefreshToken}`"
                :shipment-row="item"
                :customer-labels="{}"
                :refresh-token="expandRefreshToken"
                embedded-in-package
                :package-id="packageId"
              />
            </td>
          </tr>
        </template>

        <template v-for="col in configurableHeaders" :key="col.key" #[`item.${col.key}`]="{ item }">
          <a
            v-if="col.key === 'waybillNo'"
            href="#"
            class="text-primary text-decoration-none font-weight-medium"
            :style="cellStyle(col.key, shipmentListCellRaw(item, col.key, shipmentCellContext()), item)"
            @click.prevent="toggleExpand(item)"
          >
            {{
              cellDisplayValue(
                shipmentListCellRaw(item, col.key, shipmentCellContext()),
                col.key,
                item
              )
            }}
          </a>
          <span
            v-else
            :class="numericListKeys.has(col.key) ? 'd-block text-end tabular-nums' : undefined"
            :style="cellStyle(col.key, shipmentListCellRaw(item, col.key, shipmentCellContext()), item)"
          >
            {{
              cellDisplayValue(
                shipmentListCellRaw(item, col.key, shipmentCellContext()),
                col.key,
                item
              )
            }}
          </span>
        </template>

        <template #item.actions="{ item }">
          <div class="d-inline-flex align-center justify-end ga-1">
            <v-btn icon size="x-small" variant="text" color="primary" @click="openDialog('view', item)">
              <EyeIcon size="18" />
            </v-btn>
            <v-btn icon size="x-small" variant="text" @click="openDialog('edit', item)">
              <EditIcon size="18" />
            </v-btn>
            <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(item)">
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>

        <template #no-data>
          <div class="text-center py-6 text-medium-emphasis">
            {{ t('odakSiparis.shipments.empty') }}
          </div>
        </template>
      </v-data-table>

      <div v-if="showQtyFooter && items.length" class="odak-shipments-list-footer">
        <div class="odak-shipments-list-footer__label">
          <div class="text-body-2 font-weight-medium">
            {{ t('odakSiparis.shipments.listFooter.title') }}
          </div>
          <div class="text-caption text-medium-emphasis odak-shipments-list-footer__hint">
            {{ t('odakSiparis.shipments.listFooter.hint') }}
          </div>
        </div>
        <div class="odak-shipments-list-footer__metrics">
          <div
            v-for="cell in footerQtyCells"
            :key="cell.key"
            class="odak-shipments-list-footer__metric"
          >
            <span class="text-caption text-medium-emphasis">{{ cell.label }}</span>
            <span class="text-body-2 font-weight-bold tabular-nums">{{ cell.value }}</span>
          </div>
        </div>
      </div>
      </div>
    </OdakSiparisSubListScroll>

    <OdakSiparisShipmentDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :package-id="packageId"
      :package-no="packageNo"
      :shipment-id="dialogId"
      :seed-row="dialogSeed"
      @saved="onDialogSaved"
    />

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('odakSiparis.shipments.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.shipments.deleteConfirm') }}</v-card-text>
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
.odak-shipments-table-stack {
  min-width: 100%;
}

.odak-shipments-table--with-footer {
  border-bottom-left-radius: 0 !important;
  border-bottom-right-radius: 0 !important;
}

.odak-shipments-list-footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px 24px;
  width: 100%;
  min-width: 100%;
  padding: 10px 16px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-top: none;
  border-bottom-left-radius: 6px;
  border-bottom-right-radius: 6px;
  background: rgba(var(--v-theme-surface-variant), 0.35);
}

.odak-shipments-list-footer__label {
  max-width: min(100%, 420px);
}

.odak-shipments-list-footer__hint {
  margin-top: 2px;
  line-height: 1.35;
}

.odak-shipments-list-footer__metrics {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: 8px 28px;
  margin-left: auto;
}

.odak-shipments-list-footer__metric {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
  min-width: 72px;
}
</style>
