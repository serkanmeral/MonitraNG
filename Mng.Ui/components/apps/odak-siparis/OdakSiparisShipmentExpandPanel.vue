<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import type { OdakPackageRow, OdakShipmentLineRow, OdakShipmentRow } from '@/utils/odakSiparisConfig';
import {
  customerLabelFromRow,
  fetchOdakPackageById,
  packageDataId,
  packageDisplayNo,
} from '@/utils/odakSiparisService';
import {
  fetchOdakShipmentById,
  formatShipmentDate,
  listShipmentLinesForShipment,
  normalizeRecordScope,
  qcfStatusLabel,
  recordScopeLabel,
  shipmentDataId,
  shipmentStatusLabel,
  sumShipmentLineQuantities,
} from '@/utils/odakSiparisShipmentService';

const props = defineProps<{
  shipmentRow: OdakShipmentRow;
  customerLabels: Record<string, string>;
  refreshToken?: number;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const loading = ref(false);
const errorMessage = ref('');
const header = ref<OdakShipmentRow | null>(null);
const packageRow = ref<OdakPackageRow | null>(null);
const lines = ref<OdakShipmentLineRow[]>([]);

const panelKey = computed(() => `${shipmentDataId(props.shipmentRow)}|${props.refreshToken ?? 0}`);

const displayHeader = computed(() => header.value ?? props.shipmentRow);

const contentText = computed(() => {
  const row = displayHeader.value;
  return (row.headerDescription || row.notes || '').trim() || '—';
});

const customerLabel = computed(() => customerLabelFromRow(displayHeader.value, props.customerLabels));

const packageId = computed(() => {
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
  if (!pkg) return packageId.value ? '…' : '—';
  const no = packageDisplayNo(pkg);
  const name = pkg.name?.trim();
  return name ? `${no} — ${name}` : no;
});

const packageRoute = computed(() => {
  const id = packageId.value;
  if (!id) return undefined;
  return `/apps/odak-siparis/packages?expand=${encodeURIComponent(id)}`;
});

const lineQtyTotal = computed(() => sumShipmentLineQuantities(lines.value));

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

async function loadPanel() {
  const id = shipmentDataId(props.shipmentRow);
  if (!id) return;
  loading.value = true;
  errorMessage.value = '';
  header.value = null;
  packageRow.value = null;
  lines.value = [];
  try {
    const [loadedHeader, loadedLines] = await Promise.all([
      fetchOdakShipmentById(id),
      listShipmentLinesForShipment(id),
    ]);
    header.value = loadedHeader ?? props.shipmentRow;
    lines.value = loadedLines;
    const parentRaw = header.value.parentPackageId;
    let pkgId = '';
    if (parentRaw != null) {
      if (typeof parentRaw === 'string') pkgId = parentRaw.trim();
      else if (typeof parentRaw === 'object') {
        const o = parentRaw as Record<string, unknown>;
        pkgId = String(o.__dataId ?? o.dataId ?? o.id ?? '').trim();
      } else {
        pkgId = String(parentRaw).trim();
      }
    }
    if (pkgId) {
      packageRow.value = await fetchOdakPackageById(pkgId);
    }
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

    <div v-if="normalizeRecordScope(displayHeader.recordScope) === 'Paketli'" class="mb-4">
      <div class="text-caption text-medium-emphasis mb-1">
        {{ t('odakSiparis.globalShipments.expand.packageTitle') }}
      </div>
      <div class="d-flex flex-wrap align-center ga-2">
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
    </div>

    <div class="mb-4">
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
      <v-col cols="12" sm="6" md="4" lg="3">
        <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.shipments.columns.lineQty') }}</div>
        <div class="text-body-2 tabular-nums">{{ lineQtyTotal || '—' }}</div>
      </v-col>
    </v-row>

    <v-divider class="my-3" />

    <div class="text-subtitle-2 mb-2">{{ t('odakSiparis.globalShipments.expand.linesTitle') }}</div>
    <v-table v-if="lines.length" density="compact" class="odak-shipment-expand-lines border rounded-md">
      <thead>
        <tr>
          <th class="text-left" style="width: 56px">#</th>
          <th class="text-left">{{ t('odakSiparis.globalShipments.fields.lineDescription') }}</th>
          <th class="text-end" style="width: 120px">{{ t('odakSiparis.shipments.dialog.shippedQty') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="line in lines" :key="packageDataId(line) || `${line.lineNo}-${line.lineDescription}`">
          <td>{{ line.lineNo ?? '—' }}</td>
          <td class="text-break">{{ line.lineDescription?.trim() || '—' }}</td>
          <td class="text-end tabular-nums">{{ line.shippedQuantity ?? '—' }}</td>
        </tr>
      </tbody>
    </v-table>
    <div v-else class="text-body-2 text-medium-emphasis">
      {{ t('odakSiparis.globalShipments.expand.noLines') }}
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

.odak-shipment-expand-lines :deep(th),
.odak-shipment-expand-lines :deep(td) {
  font-size: 0.8125rem;
}
</style>
