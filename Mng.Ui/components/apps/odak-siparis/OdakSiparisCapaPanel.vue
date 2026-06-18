<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisCapaDialog from '@/components/apps/odak-siparis/OdakSiparisCapaDialog.vue';
import OdakSiparisSubListScroll from '@/components/apps/odak-siparis/OdakSiparisSubListScroll.vue';
import OdakSiparisSubListToolbar from '@/components/apps/odak-siparis/OdakSiparisSubListToolbar.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import {
  ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  ODAK_SIPARIS_CONFIG,
  ODAK_SUB_LIST_TABLE_CLASS,
  type OdakCapaRow,
} from '@/utils/odakSiparisConfig';
import {
  capaDataId,
  capaDisplayNo,
  capaStatusLabel,
  countOpenCapas,
  formatCapaDate,
  listCapasForPackage,
  ncrLabelFromRow,
  type OdakCapaDialogMode,
} from '@/utils/odakSiparisCapaService';
import { EditIcon, EyeIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakCapaRow[]>([]);

const deleteDialog = ref(false);
const rowToDelete = ref<OdakCapaRow | null>(null);
const deleting = ref(false);

const dialogOpen = ref(false);
const dialogMode = ref<OdakCapaDialogMode>('view');
const dialogId = ref<string | undefined>();
const dialogSeed = ref<OdakCapaRow | null>(null);

const openCount = computed(() => countOpenCapas(items.value));

const headers = computed(() => [
  { title: t('odakSiparis.quality.capa.columns.capaNo'), key: 'capaNo', width: 120 },
  { title: t('odakSiparis.quality.capa.columns.description'), key: 'description', minWidth: 160 },
  { title: t('odakSiparis.quality.capa.columns.capaStatus'), key: 'capaStatus', width: 110 },
  { title: t('odakSiparis.quality.capa.columns.parentNcr'), key: 'parentNcr', width: 120 },
  { title: t('odakSiparis.quality.capa.columns.cpaDate'), key: 'cpaDate', width: 100 },
  { title: t('odakSiparis.quality.capa.columns.createdAt'), key: 'createdAt', width: 100 },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    width: 132,
    ...ODAK_DATA_TABLE_STICKY_ACTIONS_HEADER,
  },
]);

async function loadItems() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    items.value = await listCapasForPackage(props.packageId);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakCapaDialogMode, row?: OdakCapaRow) {
  dialogMode.value = mode;
  dialogId.value = row ? capaDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

function confirmDelete(row: OdakCapaRow) {
  rowToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = rowToDelete.value;
  if (!row) return;
  const id = capaDataId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.capaDataset, id);
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
    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <OdakSiparisSubListScroll>
      <template #toolbar>
        <OdakSiparisSubListToolbar>
          <template #info>
            <div class="text-body-2 text-medium-emphasis">
              {{ t('odakSiparis.quality.capa.summary', { count: items.length, open: openCount }) }}
            </div>
          </template>
          <template #actions>
            <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadItems">
              <RefreshIcon size="18" />
            </v-btn>
            <v-btn color="primary" variant="flat" size="small" @click="openDialog('create')">
              <PlusIcon class="mr-1" size="16" />
              {{ t('odakSiparis.quality.capa.add') }}
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
      <template #item.capaNo="{ item }">
        <span class="font-weight-medium">{{ capaDisplayNo(item) }}</span>
      </template>
      <template #item.capaStatus="{ item }">
        {{ capaStatusLabel(item.capaStatus) }}
      </template>
      <template #item.parentNcr="{ item }">
        {{ ncrLabelFromRow(item.parentNcrId) }}
      </template>
      <template #item.cpaDate="{ item }">
        {{ formatCapaDate(item.cpaDate) }}
      </template>
      <template #item.createdAt="{ item }">
        {{ formatCapaDate(item.__createdAt) }}
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
          {{ t('odakSiparis.quality.capa.empty') }}
        </div>
      </template>
    </v-data-table>
    </OdakSiparisSubListScroll>

    <OdakSiparisCapaDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :package-id="packageId"
      :package-no="packageNo"
      :capa-id="dialogId"
      :seed-row="dialogSeed"
      @saved="loadItems"
    />

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('odakSiparis.quality.capa.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.quality.capa.deleteConfirm') }}</v-card-text>
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
