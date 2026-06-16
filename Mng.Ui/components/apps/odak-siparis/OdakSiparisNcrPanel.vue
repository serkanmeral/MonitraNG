<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisNcrDialog from '@/components/apps/odak-siparis/OdakSiparisNcrDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakNcrRow } from '@/utils/odakSiparisConfig';
import {
  countOpenNcrs,
  formatNcrDate,
  listNcrsForPackage,
  ncrDataId,
  ncrDisplayNo,
  ncrStatusLabel,
  type OdakNcrDialogMode,
} from '@/utils/odakSiparisNcrService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakNcrRow[]>([]);

const deleteDialog = ref(false);
const rowToDelete = ref<OdakNcrRow | null>(null);
const deleting = ref(false);

const dialogOpen = ref(false);
const dialogMode = ref<OdakNcrDialogMode>('view');
const dialogId = ref<string | undefined>();
const dialogSeed = ref<OdakNcrRow | null>(null);

const openCount = computed(() => countOpenNcrs(items.value));

const headers = computed(() => [
  { title: t('odakSiparis.quality.ncr.columns.ncrNo'), key: 'ncrNo', width: 110 },
  { title: t('odakSiparis.quality.ncr.columns.descriptor'), key: 'descriptor', minWidth: 160 },
  { title: t('odakSiparis.quality.ncr.columns.ncStatus'), key: 'ncStatus', width: 160 },
  { title: t('odakSiparis.quality.ncr.columns.controlType'), key: 'controlType', width: 120 },
  { title: t('odakSiparis.quality.ncr.columns.partCount'), key: 'partCount', width: 88 },
  { title: t('odakSiparis.quality.ncr.columns.createdAt'), key: 'createdAt', width: 100 },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: 132,
  },
]);

async function loadItems() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    items.value = await listNcrsForPackage(props.packageId);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakNcrDialogMode, row?: OdakNcrRow) {
  dialogMode.value = mode;
  dialogId.value = row ? ncrDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

function confirmDelete(row: OdakNcrRow) {
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = rowToDelete.value;
  if (!row) return;
  const id = ncrDataId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.ncrDataset, id);
    deleteDialog.value = false;
    rowToDelete.value = null;
    await loadItems();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

watch(
  () => props.packageId,
  () => {
    void loadItems();
  }
);

onMounted(() => {
  void loadItems();
});

defineExpose({ reload: loadItems });
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <div class="text-body-2 text-medium-emphasis">
        {{ t('odakSiparis.quality.ncr.summary', { count: items.length, open: openCount }) }}
      </div>
      <v-spacer />
      <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadItems">
        <RefreshIcon size="18" />
      </v-btn>
      <v-btn color="primary" variant="flat" size="small" @click="openDialog('create')">
        <PlusIcon class="mr-1" size="16" />
        {{ t('odakSiparis.quality.ncr.add') }}
      </v-btn>
    </div>

    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="items"
      :loading="loading"
      item-value="__dataId"
      density="compact"
      class="border rounded-md bg-surface"
      :items-per-page="10"
      hide-default-footer
    >
      <template #item.ncrNo="{ item }">
        <span class="font-weight-medium">{{ ncrDisplayNo(item) }}</span>
      </template>
      <template #item.ncStatus="{ item }">
        {{ ncrStatusLabel(item.ncStatus) }}
      </template>
      <template #item.controlType="{ item }">
        {{ item.controlType || '—' }}
      </template>
      <template #item.partCount="{ item }">
        {{ item.partCount ?? '—' }}
      </template>
      <template #item.createdAt="{ item }">
        {{ formatNcrDate(item.__createdAt) }}
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
          {{ t('odakSiparis.quality.ncr.empty') }}
        </div>
      </template>
    </v-data-table>

    <OdakSiparisNcrDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :package-id="packageId"
      :package-no="packageNo"
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
          <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">
            {{ t('odakSiparis.packages.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
