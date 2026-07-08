<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisLineDocumentsCreateDialog from '@/components/apps/odak-siparis/OdakSiparisLineDocumentsCreateDialog.vue';
import OdakSiparisSubListScroll from '@/components/apps/odak-siparis/OdakSiparisSubListScroll.vue';
import OdakSiparisSubListToolbar from '@/components/apps/odak-siparis/OdakSiparisSubListToolbar.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify, useApiErrorNotify } from '@/composables/useApiErrorNotify';
import { useAppToast } from '@/composables/useAppToast';
import {
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakLineRow,
} from '@/utils/odakSiparisConfig';
import {
  diResourceUrl,
  flattenLineDocuments,
  lineDocumentHasParameterWarnings,
  type OdakLineDocumentRow,
} from '@/utils/odakSiparisLineDocumentService';
import {
  generateOdakPackageBrief,
  generateOdakPackageDashboard,
  generateOdakPackageShipmentList,
  packageBriefFromRow,
  packageBriefToGenerateResult,
  packageDashboardFromRow,
  packageDashboardToGenerateResult,
  packageDocumentHasParameterWarnings,
  packageShipmentListFromRow,
  packageShipmentListToGenerateResult,
} from '@/utils/odakSiparisPackageDocumentService';
import type { DiGenerateDocumentResult } from '@/types/apps/documentIntelligence';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import { lineDataId, listLinesForPackage } from '@/utils/odakSiparisLineService';
import { fetchOdakPackageById } from '@/utils/odakSiparisService';
import { ExternalLinkIcon, FileIcon, FileSpreadsheetIcon, PlusIcon, RefreshIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
  packageRow?: OdakPackageRow | null;
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const { notifyApiError } = useApiErrorNotify();
const { push } = useAppToast();

const loading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');
const parameterWarningResult = ref<DiGenerateDocumentResult | null>(null);
const allLines = ref<OdakLineRow[]>([]);
const createDialogOpen = ref(false);
const shipmentListLoading = ref(false);
const shipmentListResult = ref<DiGenerateDocumentResult | null>(null);
const dashboardLoading = ref(false);
const dashboardResult = ref<DiGenerateDocumentResult | null>(null);
const briefLoading = ref(false);
const briefResult = ref<DiGenerateDocumentResult | null>(null);

function syncShipmentListFromPackage(row?: OdakPackageRow | null) {
  const persisted = packageShipmentListFromRow(row);
  shipmentListResult.value = persisted ? packageShipmentListToGenerateResult(persisted) : null;
}

function syncDashboardFromPackage(row?: OdakPackageRow | null) {
  const persisted = packageDashboardFromRow(row);
  dashboardResult.value = persisted ? packageDashboardToGenerateResult(persisted) : null;
}

function syncBriefFromPackage(row?: OdakPackageRow | null) {
  const persisted = packageBriefFromRow(row);
  briefResult.value = persisted ? packageBriefToGenerateResult(persisted) : null;
}

function syncPackageDocumentsFromRow(row?: OdakPackageRow | null) {
  syncShipmentListFromPackage(row);
  syncDashboardFromPackage(row);
  syncBriefFromPackage(row);
}

async function refreshPackageDocuments() {
  const previous = {
    shipment: shipmentListResult.value,
    dashboard: dashboardResult.value,
    brief: briefResult.value,
  };

  if (!props.packageId?.trim()) {
    syncPackageDocumentsFromRow(props.packageRow);
  } else {
    try {
      const row = await fetchOdakPackageById(props.packageId);
      syncPackageDocumentsFromRow(row ?? props.packageRow);
    } catch {
      syncPackageDocumentsFromRow(props.packageRow);
    }
  }

  // Writeback gecikmesi veya eksik alan durumunda oturum içi sonucu koru.
  if (!shipmentListResult.value?.resourceId && previous.shipment?.resourceId) {
    shipmentListResult.value = previous.shipment;
  }
  if (!dashboardResult.value?.resourceId && previous.dashboard?.resourceId) {
    dashboardResult.value = previous.dashboard;
  }
  if (!briefResult.value?.resourceId && previous.brief?.resourceId) {
    briefResult.value = previous.brief;
  }
}

const documentRows = computed(() => flattenLineDocuments(allLines.value, lineDataId));

const headers = computed(() => [
  { title: t('odakSiparis.lineDocuments.columns.line'), key: 'line', sortable: false },
  { title: t('odakSiparis.lineDocuments.columns.documentType'), key: 'documentType', sortable: false },
  { title: t('odakSiparis.lineDocuments.columns.docNo'), key: 'docNo', sortable: false },
  { title: t('odakSiparis.lineDocuments.columns.template'), key: 'template', sortable: false },
  { title: t('odakSiparis.lineDocuments.columns.generatedAt'), key: 'generatedAt', sortable: false },
  {
    title: t('odakSiparis.lineDocuments.columns.actions'),
    key: 'actions',
    width: 72,
    sortable: false,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

function lineLabel(row: OdakLineRow): string {
  const no = row.lineNo ?? '?';
  const desc = row.description?.trim();
  return desc ? `K${no} — ${desc}` : `K${no}`;
}

function formatGeneratedAt(value?: string): string {
  if (!value?.trim()) return '—';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString('tr-TR');
}

function templateDisplay(row: OdakLineDocumentRow): string {
  return row.templateName?.trim() || row.templateCode?.trim() || '—';
}

function documentTypeLabel(row: OdakLineDocumentRow): string {
  return t(`odakSiparis.lineDocuments.documentTypes.${row.kind}`);
}

async function loadLines() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    allLines.value = await listLinesForPackage(props.packageId);
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
    allLines.value = [];
  } finally {
    loading.value = false;
  }
}

function formatParameterKeys(keys: string[]): string {
  return keys.map((k) => `{{${k}}}`).join(', ');
}

const parameterWarningLines = computed(() => {
  const result = parameterWarningResult.value;
  if (!result || !lineDocumentHasParameterWarnings(result)) return [];

  const lines: string[] = [];
  if (result.undefinedParameterKeys.length) {
    lines.push(
      t('odakSiparis.lineDocuments.parameterWarnings.undefined', {
        keys: formatParameterKeys(result.undefinedParameterKeys),
      })
    );
  }
  if (result.unresolvedParameterKeys.length) {
    lines.push(
      t('odakSiparis.lineDocuments.parameterWarnings.unresolved', {
        keys: formatParameterKeys(result.unresolvedParameterKeys),
      })
    );
  }
  return lines;
});

function onCreated(result: DiGenerateDocumentResult | null) {
  successMessage.value = t('odakSiparis.lineDocuments.createSuccess');
  parameterWarningResult.value =
    result && lineDocumentHasParameterWarnings(result) ? result : null;
  void loadLines();
}

async function generateShipmentList() {
  if (!props.packageId?.trim()) return;
  shipmentListLoading.value = true;
  errorMessage.value = '';
  try {
    const result = await generateOdakPackageShipmentList(props.packageId);
    shipmentListResult.value = result;
    successMessage.value = t('odakSiparis.packageDocuments.shipmentListSuccess', {
      fileName: result.fileName?.trim() || result.docNo?.trim() || '—',
    });
    if (packageDocumentHasParameterWarnings(result)) {
      parameterWarningResult.value = result;
    }
    push({
      title: t('odakSiparis.packageDocuments.shipmentListTitle'),
      message: successMessage.value,
      severity: 'success',
    });
    await refreshPackageDocuments();
  } catch (e: unknown) {
    errorMessage.value = notifyApiError(e, {
      fallbackKey: 'odakSiparis.packageDocuments.shipmentListError',
    }).message;
  } finally {
    shipmentListLoading.value = false;
  }
}

async function generateDashboard() {
  if (!props.packageId?.trim()) return;
  dashboardLoading.value = true;
  errorMessage.value = '';
  try {
    const result = await generateOdakPackageDashboard(props.packageId);
    dashboardResult.value = result;
    successMessage.value = t('odakSiparis.packageDocuments.dashboardSuccess', {
      fileName: result.fileName?.trim() || result.docNo?.trim() || '—',
    });
    if (packageDocumentHasParameterWarnings(result)) {
      parameterWarningResult.value = result;
    }
    push({
      title: t('odakSiparis.packageDocuments.dashboardTitle'),
      message: successMessage.value,
      severity: 'success',
    });
    await refreshPackageDocuments();
  } catch (e: unknown) {
    errorMessage.value = notifyApiError(e, {
      fallbackKey: 'odakSiparis.packageDocuments.dashboardError',
    }).message;
  } finally {
    dashboardLoading.value = false;
  }
}

async function generateBrief() {
  if (!props.packageId?.trim()) return;
  briefLoading.value = true;
  errorMessage.value = '';
  try {
    const result = await generateOdakPackageBrief(props.packageId);
    briefResult.value = result;
    successMessage.value = t('odakSiparis.packageDocuments.briefSuccess', {
      fileName: result.fileName?.trim() || result.docNo?.trim() || '—',
    });
    if (packageDocumentHasParameterWarnings(result)) {
      parameterWarningResult.value = result;
    }
    push({
      title: t('odakSiparis.packageDocuments.briefTitle'),
      message: successMessage.value,
      severity: 'success',
    });
    await refreshPackageDocuments();
  } catch (e: unknown) {
    errorMessage.value = notifyApiError(e, {
      fallbackKey: 'odakSiparis.packageDocuments.briefError',
    }).message;
  } finally {
    briefLoading.value = false;
  }
}

function openShipmentListDi() {
  const id = shipmentListResult.value?.resourceId?.trim();
  if (!id) return;
  navigateTo(diResourceUrl(id));
}

function openDashboardDi() {
  const id = dashboardResult.value?.resourceId?.trim();
  if (!id) return;
  navigateTo(diResourceUrl(id));
}

function openBriefDi() {
  const id = briefResult.value?.resourceId?.trim();
  if (!id) return;
  navigateTo(diResourceUrl(id));
}

function openDi(row: OdakLineDocumentRow) {
  const id = row.resourceId?.trim();
  if (!id) return;
  navigateTo(diResourceUrl(id));
}

watch(
  () => props.packageId,
  () => {
    void refreshPackageDocuments();
    void loadLines();
  }
);

watch(
  () => props.packageRow,
  (row) => {
    syncPackageDocumentsFromRow(row);
  },
  { deep: true }
);

onMounted(() => {
  void refreshPackageDocuments();
  void loadLines();
});
</script>

<template>
  <div class="odak-line-documents-panel">
    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-alert
      v-if="successMessage"
      type="success"
      variant="tonal"
      class="mb-3"
      closable
      @click:close="successMessage = ''"
    >
      {{ successMessage }}
    </v-alert>

    <v-alert
      v-if="parameterWarningLines.length"
      type="warning"
      variant="tonal"
      class="mb-3"
      closable
      @click:close="parameterWarningResult = null"
    >
      <div class="font-weight-medium mb-1">
        {{ t('odakSiparis.lineDocuments.parameterWarnings.title') }}
      </div>
      <div class="text-body-2">
        {{ t('odakSiparis.lineDocuments.parameterWarnings.body') }}
      </div>
      <ul class="mt-2 mb-0 pl-4 text-body-2">
        <li v-for="(line, index) in parameterWarningLines" :key="index">
          {{ line }}
        </li>
      </ul>
    </v-alert>

    <v-card variant="outlined" class="mb-4">
      <v-card-text class="d-flex flex-wrap align-center ga-3 py-3">
        <div class="flex-grow-1 min-width-0">
          <div class="text-subtitle-2 font-weight-medium">
            {{ t('odakSiparis.packageDocuments.shipmentListTitle') }}
          </div>
          <div class="text-body-2 text-medium-emphasis">
            {{ t('odakSiparis.packageDocuments.shipmentListHint') }}
          </div>
          <div
            v-if="shipmentListResult?.resourceId"
            class="text-body-2 mt-2 d-flex flex-wrap align-center ga-2"
          >
            <span>{{ shipmentListResult.fileName || shipmentListResult.docNo || '—' }}</span>
            <v-btn
              size="small"
              variant="text"
              color="primary"
              class="px-1"
              @click="openShipmentListDi"
            >
              <ExternalLinkIcon class="mr-1" size="16" />
              {{ t('odakSiparis.lineDocuments.openDi') }}
            </v-btn>
          </div>
        </div>
        <v-btn
          color="primary"
          variant="tonal"
          size="small"
          :loading="shipmentListLoading"
          :disabled="!packageId"
          @click="generateShipmentList"
        >
          <FileSpreadsheetIcon class="mr-1" size="16" />
          {{ t('odakSiparis.packageDocuments.shipmentListAction') }}
        </v-btn>
      </v-card-text>
    </v-card>

    <v-card variant="outlined" class="mb-4">
      <v-card-text class="d-flex flex-wrap align-center ga-3 py-3">
        <div class="flex-grow-1 min-width-0">
          <div class="text-subtitle-2 font-weight-medium">
            {{ t('odakSiparis.packageDocuments.dashboardTitle') }}
          </div>
          <div class="text-body-2 text-medium-emphasis">
            {{ t('odakSiparis.packageDocuments.dashboardHint') }}
          </div>
          <div
            v-if="dashboardResult?.resourceId"
            class="text-body-2 mt-2 d-flex flex-wrap align-center ga-2"
          >
            <span>{{ dashboardResult.fileName || dashboardResult.docNo || '—' }}</span>
            <v-btn size="small" variant="text" color="primary" class="px-1" @click="openDashboardDi">
              <ExternalLinkIcon class="mr-1" size="16" />
              {{ t('odakSiparis.lineDocuments.openDi') }}
            </v-btn>
          </div>
        </div>
        <v-btn
          color="primary"
          variant="tonal"
          size="small"
          :loading="dashboardLoading"
          :disabled="!packageId"
          class="text-none"
          @click="generateDashboard"
        >
          <FileSpreadsheetIcon class="mr-1" size="18" />
          {{ t('odakSiparis.packageDocuments.dashboardAction') }}
        </v-btn>
      </v-card-text>
    </v-card>

    <v-card variant="outlined" class="mb-4">
      <v-card-text class="d-flex flex-wrap align-center ga-3 py-3">
        <div class="flex-grow-1 min-width-0">
          <div class="text-subtitle-2 font-weight-medium">
            {{ t('odakSiparis.packageDocuments.briefTitle') }}
          </div>
          <div class="text-body-2 text-medium-emphasis">
            {{ t('odakSiparis.packageDocuments.briefHint') }}
          </div>
          <div
            v-if="briefResult?.resourceId"
            class="text-body-2 mt-2 d-flex flex-wrap align-center ga-2"
          >
            <span>{{ briefResult.fileName || briefResult.docNo || '—' }}</span>
            <v-btn size="small" variant="text" color="primary" class="px-1" @click="openBriefDi">
              <ExternalLinkIcon class="mr-1" size="16" />
              {{ t('odakSiparis.lineDocuments.openDi') }}
            </v-btn>
          </div>
        </div>
        <v-btn
          color="primary"
          variant="tonal"
          size="small"
          :loading="briefLoading"
          :disabled="!packageId"
          class="text-none"
          @click="generateBrief"
        >
          <FileIcon class="mr-1" size="18" />
          {{ t('odakSiparis.packageDocuments.briefAction') }}
        </v-btn>
      </v-card-text>
    </v-card>

    <div class="text-subtitle-2 font-weight-medium mb-2">
      {{ t('odakSiparis.packageDocuments.lineSectionTitle') }}
    </div>

    <OdakSiparisSubListScroll>
      <template #toolbar>
        <OdakSiparisSubListToolbar>
          <template #info>
            <span v-if="packageNo" class="text-subtitle-1 font-weight-medium">
              {{ packageNo }} · {{ t('odakSiparis.detail.tabs.documents') }}
            </span>
            <v-chip v-if="documentRows.length" size="small" variant="tonal" color="primary">
              {{ documentRows.length }}
            </v-chip>
          </template>
          <template #actions>
            <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadLines">
              <RefreshIcon size="18" />
            </v-btn>
            <v-btn color="primary" variant="flat" size="small" @click="createDialogOpen = true">
              <PlusIcon class="mr-1" size="16" />
              {{ t('odakSiparis.lineDocuments.createAction') }}
            </v-btn>
          </template>
        </OdakSiparisSubListToolbar>
      </template>

      <v-data-table
        :headers="headers"
        :items="documentRows"
        :loading="loading"
        item-value="rowKey"
        density="compact"
        :class="['border', 'rounded-md', ODAK_SUB_LIST_TABLE_CLASS]"
      >
        <template #item.line="{ item }">
          {{ lineLabel(item.line) }}
        </template>
        <template #item.documentType="{ item }">
          {{ documentTypeLabel(item) }}
        </template>
        <template #item.docNo="{ item }">
          {{ item.docNo?.trim() || '—' }}
        </template>
        <template #item.template="{ item }">
          {{ templateDisplay(item) }}
        </template>
        <template #item.generatedAt="{ item }">
          {{ formatGeneratedAt(item.generatedAt) }}
        </template>
        <template #item.actions="{ item }">
          <v-btn
            icon
            size="x-small"
            variant="text"
            color="primary"
            :title="t('odakSiparis.lineDocuments.openDi')"
            @click="openDi(item)"
          >
            <ExternalLinkIcon size="18" />
          </v-btn>
        </template>
        <template #no-data>
          <div class="text-center py-6 text-medium-emphasis">
            {{ t('odakSiparis.lineDocuments.empty') }}
          </div>
        </template>
      </v-data-table>
    </OdakSiparisSubListScroll>

    <OdakSiparisLineDocumentsCreateDialog
      v-model="createDialogOpen"
      :lines="allLines"
      @created="onCreated"
    />
  </div>
</template>
