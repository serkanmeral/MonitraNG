<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisNcrDialog from '@/components/apps/odak-siparis/OdakSiparisNcrDialog.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { ocDelete } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakNcrRow } from '@/utils/odakSiparisConfig';
import {
  countOpenNcrs,
  fetchOdakNcrsPage,
  formatNcrDate,
  ncrDataId,
  ncrDisplayNo,
  ncrRecordScopeLabel,
  ncrStatusLabel,
  normalizeNcrRecordScope,
  type OdakNcrDialogMode,
  type OdakNcrScopeTab,
  type OdakNcrStatusTab,
} from '@/utils/odakSiparisNcrService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const scopeTab = ref<OdakNcrScopeTab>('all');
const statusTab = ref<OdakNcrStatusTab>('open');
const searchQuery = ref('');
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakNcrRow[]>([]);
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);

const dialogOpen = ref(false);
const dialogMode = ref<OdakNcrDialogMode>('view');
const dialogId = ref<string | undefined>();
const dialogSeed = ref<OdakNcrRow | null>(null);
const dialogPackageId = ref<string | undefined>();

const deleteDialog = ref(false);
const rowToDelete = ref<OdakNcrRow | null>(null);
const deleting = ref(false);

const page = computed(() => ({ title: t('odakSiparis.globalNcr.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.globalNcr.title'), disabled: true, href: '#' },
]);

const scopeTabs = computed(() => [
  { value: 'all' as const, label: t('odakSiparis.globalNcr.tabs.all') },
  { value: 'package' as const, label: t('odakSiparis.globalNcr.tabs.package') },
  { value: 'general' as const, label: t('odakSiparis.globalNcr.tabs.general') },
]);

const statusTabs = computed(() => [
  { value: 'open' as const, label: t('odakSiparis.globalNcr.tabs.open') },
  { value: 'all' as const, label: t('odakSiparis.globalNcr.tabs.statusAll') },
]);

const openCount = computed(() => countOpenNcrs(items.value));

const headers = computed(() => [
  { title: t('odakSiparis.quality.ncr.columns.ncrNo'), key: 'ncrNo', width: 110 },
  { title: t('odakSiparis.globalNcr.columns.scope'), key: 'scopeLabel', width: 120 },
  { title: t('odakSiparis.quality.ncr.columns.descriptor'), key: 'descriptor', minWidth: 180 },
  { title: t('odakSiparis.quality.ncr.columns.ncStatus'), key: 'ncStatus', width: 160 },
  { title: t('odakSiparis.quality.ncr.columns.controlType'), key: 'controlType', width: 120 },
  { title: t('odakSiparis.quality.ncr.columns.ncDate'), key: 'ncDate', width: 110 },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    width: 132,
    sortable: false,
    align: 'end' as const,
  },
]);

function resolvePackageId(row: OdakNcrRow): string {
  const raw = row.parentPackageId;
  if (raw == null) return '';
  if (typeof raw === 'string') return raw.trim();
  if (typeof raw === 'object') {
    const o = raw as Record<string, unknown>;
    return String(o.__dataId ?? o.dataId ?? '').trim();
  }
  return '';
}

const tableItems = computed(() =>
  items.value.map((row) => ({
    raw: row,
    __dataId: ncrDataId(row),
    ncrNo: ncrDisplayNo(row),
    scopeLabel: ncrRecordScopeLabel(row.recordScope),
    descriptor: row.descriptor || '—',
    ncStatus: ncrStatusLabel(row.ncStatus),
    controlType: row.controlType || '—',
    ncDate: formatNcrDate(row.ncDate),
  }))
);

async function loadItems() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const resp = await fetchOdakNcrsPage({
      scopeTab: scopeTab.value,
      statusTab: statusTab.value,
      search: searchQuery.value.trim(),
      page: tablePage.value,
      limit: tableItemsPerPage.value,
    });
    items.value = resp.items;
    totalCount.value = resp.total;
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakNcrDialogMode, row?: OdakNcrRow) {
  dialogMode.value = mode;
  dialogId.value = row ? ncrDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  if (row) {
    dialogPackageId.value =
      normalizeNcrRecordScope(row.recordScope) === 'Genel' ? undefined : resolvePackageId(row);
  } else {
    dialogPackageId.value = undefined;
  }
  dialogOpen.value = true;
}

function confirmDelete(row: OdakNcrRow) {
  if (normalizeNcrRecordScope(row.recordScope) !== 'Genel') return;
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function executeDelete() {
  const row = rowToDelete.value;
  const id = row ? ncrDataId(row) : '';
  if (!id) return;
  deleting.value = true;
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.ncrDataset, id);
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

onMounted(() => void loadItems());
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  <v-card elevation="10">
    <v-card-text>
      <div class="d-flex flex-wrap align-center ga-3 mb-2">
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
        <v-chip size="small" color="warning" variant="tonal">
          {{ t('odakSiparis.quality.ncr.summaryOpen', { count: openCount }) }}
        </v-chip>
        <v-spacer />
        <v-text-field
          v-model="searchQuery"
          :label="t('odakSiparis.globalNcr.searchDescriptor')"
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
          {{ t('odakSiparis.globalNcr.addGeneral') }}
        </v-btn>
      </div>

      <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
        {{ errorMessage }}
      </v-alert>

      <v-data-table-server
        v-model:page="tablePage"
        v-model:items-per-page="tableItemsPerPage"
        :headers="headers"
        :items="tableItems"
        :items-length="totalCount"
        :loading="loading"
        item-value="__dataId"
        class="border rounded-md"
      >
        <template #item.actions="{ item }">
          <div class="d-flex justify-end ga-1">
            <v-btn icon variant="text" size="small" @click="openDialog('view', item.raw)">
              <EyeIcon size="18" />
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              :disabled="normalizeNcrRecordScope(item.raw.recordScope) !== 'Genel'"
              @click="openDialog('edit', item.raw)"
            >
              <EditIcon size="18" />
            </v-btn>
            <v-btn
              icon
              variant="text"
              size="small"
              color="error"
              :disabled="normalizeNcrRecordScope(item.raw.recordScope) !== 'Genel'"
              @click="confirmDelete(item.raw)"
            >
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>
      </v-data-table-server>
    </v-card-text>
  </v-card>

  <OdakSiparisNcrDialog
    v-model="dialogOpen"
    :mode="dialogMode"
    :package-id="dialogPackageId"
    :ncr-id="dialogId"
    :seed-row="dialogSeed"
    @saved="loadItems"
  />

  <v-dialog v-model="deleteDialog" max-width="420">
    <v-card>
      <v-card-title>{{ t('odakSiparis.quality.ncr.deleteTitle') }}</v-card-title>
      <v-card-text>{{ t('odakSiparis.quality.ncr.deleteConfirm') }}</v-card-text>
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
