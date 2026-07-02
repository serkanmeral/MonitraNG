<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisShipmentDialog from '@/components/apps/odak-siparis/OdakSiparisShipmentDialog.vue';
import OdakSiparisSubListScroll from '@/components/apps/odak-siparis/OdakSiparisSubListScroll.vue';
import OdakSiparisSubListToolbar from '@/components/apps/odak-siparis/OdakSiparisSubListToolbar.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakShipmentRow,
} from '@/utils/odakSiparisConfig';
import { hubListCellDisplayValue, hubListCellStyle } from '@/utils/odakSiparisHubListCellFormat';
import {
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
import {
  countCompletedShipments,
  deleteOdakShipment,
  fetchShipmentLineQtyMap,
  listShipmentsForPackage,
  shipmentDataId,
  type OdakShipmentDialogMode,
} from '@/utils/odakSiparisShipmentService';
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
const lineQtyByShipment = ref<Map<string, number>>(new Map());

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
const { canViewListColumn } = useOdakFieldAccess(fieldPolicies, ODAK_SHIPMENT_LIST_KEY_TO_FIELD);

function columnTitle(fieldName: string, listKey: string): string {
  void listKey;
  return odakShipmentSettingsFieldLabelTr(fieldName);
}

const configurableHeaders = computed(() =>
  buildShipmentListHeaders(listConfig.value, columnTitle, (listKey) => canViewListColumn(listKey))
);

const headers = computed(() => [
  ...configurableHeaders.value,
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    width: 132,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

function shipmentCellContext() {
  return { lineQtyByShipmentId: lineQtyByShipment.value };
}

function cellDisplayValue(raw: string, listKey: string, item: OdakShipmentRow): string {
  return hubListCellDisplayValue(raw, listKey, listConfig.value, ODAK_SHIPMENT_LIST_KEY_TO_FIELD, item);
}

function cellStyle(listKey: string, raw: string, item: OdakShipmentRow): Record<string, string> {
  return hubListCellStyle(listKey, raw, listConfig.value, ODAK_SHIPMENT_LIST_KEY_TO_FIELD, item);
}

async function loadItems() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  lineQtyByShipment.value = new Map();
  let rows: OdakShipmentRow[] = [];
  try {
    rows = await listShipmentsForPackage(props.packageId);
    items.value = rows;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
    items.value = [];
  } finally {
    loading.value = false;
  }
  const ids = rows.map((row) => shipmentDataId(row)).filter(Boolean);
  if (!ids.length) return;
  try {
    lineQtyByShipment.value = await fetchShipmentLineQtyMap(ids);
  } catch {
    lineQtyByShipment.value = new Map();
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
    emit('saved');
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    deleting.value = false;
  }
}

function onDialogSaved() {
  void loadItems();
  emit('saved');
}

watch(
  () => props.packageId,
  () => {
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
  void loadItems();
});

defineExpose({ reload: loadItems });
</script>

<template>
  <div>
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <OdakSiparisSubListScroll>
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

      <v-data-table
        :headers="headers"
        :items="items"
        :loading="loading"
        item-value="__dataId"
        density="compact"
        :class="['border', 'rounded-md', 'bg-surface', ODAK_SUB_LIST_TABLE_CLASS]"
        :items-per-page="10"
        hide-default-footer
      >
      <template v-for="col in configurableHeaders" :key="col.key" #[`item.${col.key}`]="{ item }">
        <span
          :class="col.key === 'waybillNo' ? 'font-weight-medium' : undefined"
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
