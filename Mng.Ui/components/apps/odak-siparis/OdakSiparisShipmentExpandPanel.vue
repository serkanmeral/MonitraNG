<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import type { OdakLineRow, OdakPackageRow, OdakShipmentRow } from '@/utils/odakSiparisConfig';
import { listLinesForPackage } from '@/utils/odakSiparisLineService';
import { formatOdakNumber } from '@/utils/odakSiparisService';
import {
  fetchOdakPackageById,
  packageDataId,
  packageDisplayNo,
  shipmentCustomerLabel,
} from '@/utils/odakSiparisService';
import {
  buildParentLineMap,
  buildShipmentLineQtyViews,
  fetchOdakShipmentById,
  formatShipmentDate,
  listShipmentLinesForShipment,
  normalizeRecordScope,
  qcfStatusLabel,
  recordScopeLabel,
  shipmentDataId,
  shipmentStatusLabel,
  sumShipmentLineQuantities,
  type OdakShipmentLineQtyView,
} from '@/utils/odakSiparisShipmentService';

const props = withDefaults(
  defineProps<{
    shipmentRow: OdakShipmentRow;
    customerLabels: Record<string, string>;
    refreshToken?: number;
    /** İş paketi expand panelinde — paket kartını tekrar gösterme */
    embeddedInPackage?: boolean;
    /** Sipariş kalemi miktarları için parent line lookup */
    packageId?: string;
  }>(),
  {
    embeddedInPackage: false,
    packageId: '',
  }
);

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const errorMessage = ref('');
const header = ref<OdakShipmentRow | null>(null);
const packageRow = ref<OdakPackageRow | null>(null);
const lineViews = ref<OdakShipmentLineQtyView[]>([]);

const panelKey = computed(() => `${shipmentDataId(props.shipmentRow)}|${props.refreshToken ?? 0}`);

const displayHeader = computed(() => header.value ?? props.shipmentRow);

const contentText = computed(() => {
  const row = displayHeader.value;
  return (row.headerDescription || row.notes || '').trim() || '—';
});

const customerLabel = computed(() =>
  shipmentCustomerLabel(displayHeader.value, packageRow.value, props.customerLabels)
);

const resolvedPackageId = computed(() => {
  if (props.packageId?.trim()) return props.packageId.trim();
  const raw = displayHeader.value.parentPackageId;
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
  }
  return String(raw).trim();
});

const packageLabel = computed(() => {
  if (normalizeRecordScope(displayHeader.value.recordScope) !== 'Paketli') {
    return t('odakSiparis.globalShipments.expand.noPackage');
  }
  const pkg = packageRow.value;
  if (!pkg) return resolvedPackageId.value ? '…' : '—';
  const no = packageDisplayNo(pkg);
  const name = pkg.name?.trim();
  return name ? `${no} — ${name}` : no;
});

const packageRoute = computed(() => {
  const id = resolvedPackageId.value;
  if (!id) return undefined;
  return `/apps/odak-siparis/packages?expand=${encodeURIComponent(id)}`;
});

const lineQtyTotal = computed(() => sumShipmentLineQuantities(lineViews.value.map((v) => v.line)));

const orderQtyTotal = computed(() =>
  lineViews.value.reduce((sum, v) => sum + (v.orderQty ?? 0), 0)
);

const remainingQtyTotal = computed(() =>
  lineViews.value.reduce((sum, v) => sum + (v.remainingQty ?? 0), 0)
);

const detailFields = computed(() => {
  const row = displayHeader.value;
  return [
    { label: t('odakSiparis.globalShipments.columns.scope'), value: recordScopeLabel(row.recordScope) },
    { label: t('odakSiparis.shipments.columns.waybillNo'), value: row.waybillNo || '—' },
    { label: t('odakSiparis.shipments.columns.shipmentDate'), value: formatShipmentDate(row.shipmentDate) },
    { label: t('odakSiparis.shipments.columns.status'), value: shipmentStatusLabel(row.status) },
    { label: t('odakSiparis.packages.columns.customer'), value: customerLabel.value },
    { label: t('odakSiparis.shipments.columns.controlType'), value: row.controlType?.trim() || '—' },
    { label: t('odakSiparis.shipments.fields.shipmentAddress'), value: row.shipmentAddress?.trim() || '—' },
    { label: t('odakSiparis.shipments.columns.qcfStatus'), value: qcfStatusLabel(row.qcfStatus) },
    { label: t('odakSiparis.shipments.fields.qcfReferenceNo'), value: row.qcfReferenceNo?.trim() || '—' },
    { label: t('odakSiparis.shipments.fields.notes'), value: row.notes?.trim() || '—' },
    { label: t('odakSiparis.shipments.fields.qcfNotes'), value: row.qcfNotes?.trim() || '—' },
  ];
});

function formatQty(value: number | null | undefined): string {
  if (value == null) return '—';
  return formatOdakNumber(value);
}

function lineRowKey(view: OdakShipmentLineQtyView): string {
  return packageDataId(view.line) || `${view.line.lineNo}-${view.line.lineDescription}`;
}

async function loadPanel() {
  const id = shipmentDataId(props.shipmentRow);
  if (!id) return;
  loading.value = true;
  errorMessage.value = '';
  header.value = null;
  packageRow.value = null;
  lineViews.value = [];
  try {
    const [loadedHeader, loadedLines] = await Promise.all([
      fetchOdakShipmentById(id),
      listShipmentLinesForShipment(id),
    ]);
    header.value = loadedHeader ?? props.shipmentRow;

    const pkgId = resolvedPackageId.value;
    let parentLineMap = new Map<string, OdakLineRow>();
    if (pkgId) {
      const [pkg, packageLines] = await Promise.all([
        fetchOdakPackageById(pkgId),
        listLinesForPackage(pkgId),
      ]);
      packageRow.value = pkg ?? null;
      parentLineMap = buildParentLineMap(packageLines);
    }

    lineViews.value = buildShipmentLineQtyViews(loadedLines, parentLineMap);
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
    header.value = props.shipmentRow;
  } finally {
    loading.value = false;
  }
}

watch(
  () => panelKey.value,
  () => {
    void loadPanel();
  },
  { immediate: true }
);
</script>

<template>
  <div class="odak-shipment-expand-panel pa-4">
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <div
      v-if="!embeddedInPackage && normalizeRecordScope(displayHeader.recordScope) === 'Paketli'"
      class="mb-4"
    >
      <div class="text-caption text-medium-emphasis mb-1">
        {{ t('odakSiparis.globalShipments.expand.packageTitle') }}
      </div>
      <div class="d-flex flex-wrap align-center ga-2 mb-1">
        <span class="text-body-2 font-weight-medium">{{ packageLabel }}</span>
        <v-btn
          v-if="packageRoute"
          :to="packageRoute"
          size="x-small"
          variant="tonal"
          color="primary"
        >
          {{ t('odakSiparis.globalShipments.expand.openPackage') }}
        </v-btn>
      </div>
      <div v-if="customerLabel !== '—'" class="text-body-2">
        <span class="text-caption text-medium-emphasis">{{ t('odakSiparis.packages.columns.customer') }}:</span>
        <span class="font-weight-medium ms-1">{{ customerLabel }}</span>
      </div>
    </div>

    <div v-if="!embeddedInPackage" class="mb-4">
      <div class="text-caption text-medium-emphasis mb-1">
        {{ t('odakSiparis.globalShipments.expand.fullContent') }}
      </div>
      <div class="text-body-2 odak-shipment-expand-content">
        {{ contentText }}
      </div>
    </div>

    <v-row dense class="mb-2">
      <v-col v-for="field in detailFields" :key="field.label" cols="12" sm="6" md="4" lg="3">
        <div class="text-caption text-medium-emphasis">{{ field.label }}</div>
        <div class="text-body-2 text-break">{{ field.value }}</div>
      </v-col>
    </v-row>

    <v-row dense class="mb-3">
      <v-col cols="12">
        <div class="text-caption text-medium-emphasis odak-shipment-expand-qty-hint">
          {{ t('odakSiparis.shipments.expand.qtyHint') }}
        </div>
      </v-col>
      <v-col cols="6" sm="4" md="3">
        <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.shipments.columns.orderQty') }}</div>
        <div class="text-body-2 tabular-nums font-weight-medium">{{ formatQty(orderQtyTotal) }}</div>
      </v-col>
      <v-col cols="6" sm="4" md="3">
        <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.shipments.columns.lineQty') }}</div>
        <div class="text-body-2 tabular-nums font-weight-medium">{{ formatQty(lineQtyTotal) }}</div>
      </v-col>
      <v-col cols="6" sm="4" md="3">
        <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.shipments.columns.remainingQty') }}</div>
        <div class="text-body-2 tabular-nums font-weight-medium">{{ formatQty(remainingQtyTotal) }}</div>
      </v-col>
    </v-row>

    <v-divider class="my-3" />

    <div class="text-subtitle-2 mb-2">{{ t('odakSiparis.shipments.expand.linesTitle') }}</div>
    <v-table v-if="lineViews.length" density="compact" class="odak-shipment-expand-lines border rounded-md">
      <thead>
        <tr>
          <th class="text-left" style="width: 56px">#</th>
          <th class="text-left">{{ t('odakSiparis.globalShipments.fields.lineDescription') }}</th>
          <th class="text-end" style="width: 96px">{{ t('odakSiparis.shipments.columns.orderQty') }}</th>
          <th class="text-end" style="width: 96px">{{ t('odakSiparis.shipments.columns.lineQty') }}</th>
          <th class="text-end" style="width: 96px">{{ t('odakSiparis.shipments.columns.remainingQty') }}</th>
          <th class="text-center" style="width: 72px">{{ t('odakSiparis.lines.fields.unit') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="view in lineViews" :key="lineRowKey(view)">
          <td>{{ view.line.lineNo ?? '—' }}</td>
          <td class="text-break">{{ view.line.lineDescription?.trim() || '—' }}</td>
          <td class="text-end tabular-nums">{{ formatQty(view.orderQty) }}</td>
          <td class="text-end tabular-nums">{{ formatQty(view.shippedQty) }}</td>
          <td class="text-end tabular-nums">{{ formatQty(view.remainingQty) }}</td>
          <td class="text-center">{{ view.unit || '—' }}</td>
        </tr>
      </tbody>
    </v-table>
    <div v-else class="text-body-2 text-medium-emphasis">
      {{ t('odakSiparis.shipments.expand.noLines') }}
    </div>
  </div>
</template>

<style scoped>
.odak-shipment-expand-panel {
  background: rgba(var(--v-theme-surface-variant), 0.25);
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.odak-shipment-expand-content {
  white-space: pre-wrap;
  word-break: break-word;
}

.odak-shipment-expand-qty-hint {
  line-height: 1.35;
}

.odak-shipment-expand-lines :deep(th),
.odak-shipment-expand-lines :deep(td) {
  font-size: 0.8125rem;
}
</style>
