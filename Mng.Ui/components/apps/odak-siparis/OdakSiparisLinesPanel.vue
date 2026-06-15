<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { ocListDatasetPage, ocDelete } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG } from '@/utils/odakSiparisConfig';
import { PlusIcon, EditIcon, TrashIcon, RefreshIcon } from 'vue-tabler-icons';

const props = defineProps<{
  packageId: string;
  packageNo?: string;
}>();

const router = useRouter();
const loading = ref(false);
const errorMessage = ref('');
const lines = ref<Record<string, unknown>[]>([]);
const deleteDialog = ref(false);
const lineToDelete = ref<Record<string, unknown> | null>(null);
const deleting = ref(false);

const headers = [
  { title: 'Kalem No', key: 'lineNo', width: 90 },
  { title: 'Musteri PO No', key: 'customerPoNo' },
  { title: 'PO Kalem', key: 'customerPoItemNo', width: 90 },
  { title: 'Tanim', key: 'description' },
  { title: 'Miktar', key: 'quantity', width: 90 },
  { title: 'Birim', key: 'unit', width: 80 },
  { title: 'Sevk T.', key: 'shipmentDate', width: 110 },
  { title: 'Eylem', key: 'actions', sortable: false, align: 'center' as const, width: 120 },
];

const filterLabel = computed(() =>
  props.packageNo ? `${props.packageNo} kalemleri` : 'Siparis kalemleri'
);

function lineId(row: Record<string, unknown>): string {
  return String(row.__dataId ?? row.dataId ?? '');
}

function formatDate(v: unknown): string {
  if (!v) return '—';
  try {
    return new Date(String(v)).toLocaleDateString('tr-TR');
  } catch {
    return String(v);
  }
}

async function loadLines() {
  if (!props.packageId) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    const filter = `parentPackageId eq '${props.packageId}'`;
    const resp = await ocListDatasetPage(ODAK_SIPARIS_CONFIG.linesDataset, {
      filter,
      sort: 'lineNo:asc',
      limit: 500,
    });
    lines.value = (resp.items ?? []) as Record<string, unknown>[];
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    lines.value = [];
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  const returnTo = `/apps/odak-siparis/packages/${encodeURIComponent(props.packageId)}?tab=lines`;
  router.push({
    path: `/apps/automated-forms/view/${ODAK_SIPARIS_CONFIG.linesFormCode}`,
    query: {
      parentPackageId: props.packageId,
      mode: 'create',
      returnTo,
    },
  });
}

function openEdit(row: Record<string, unknown>) {
  const id = lineId(row);
  if (!id) return;
  const returnTo = `/apps/odak-siparis/packages/${encodeURIComponent(props.packageId)}?tab=lines`;
  router.push({
    path: `/apps/automated-forms/view/${ODAK_SIPARIS_CONFIG.linesFormCode}`,
    query: {
      parentPackageId: props.packageId,
      editId: id,
      returnTo,
    },
  });
}

function confirmDelete(row: Record<string, unknown>) {
  lineToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = lineToDelete.value;
  if (!row) return;
  const id = lineId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.linesDataset, id);
    deleteDialog.value = false;
    lineToDelete.value = null;
    await loadLines();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

watch(
  () => props.packageId,
  () => {
    void loadLines();
  }
);

onMounted(() => {
  void loadLines();
});
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-4">
      <span class="text-subtitle-1 font-weight-medium">{{ filterLabel }}</span>
      <v-spacer />
      <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadLines">
        <RefreshIcon size="18" />
      </v-btn>
      <v-btn color="primary" variant="flat" size="small" @click="openCreate">
        <PlusIcon class="mr-1" size="16" />
        Kalem Ekle
      </v-btn>
    </div>

    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="lines"
      :loading="loading"
      item-value="__dataId"
      density="comfortable"
      class="border rounded-md"
    >
      <template #item.shipmentDate="{ item }">
        {{ formatDate(item.shipmentDate) }}
      </template>
      <template #item.actions="{ item }">
        <v-btn icon size="x-small" variant="text" @click="openEdit(item)">
          <EditIcon size="18" />
        </v-btn>
        <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(item)">
          <TrashIcon size="18" />
        </v-btn>
      </template>
    </v-data-table>

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>Kalemi sil</v-card-title>
        <v-card-text>Bu kalemi silmek istediginize emin misiniz?</v-card-text>
        <v-card-actions>
          <v-spacer />
          <v-btn variant="text" @click="deleteDialog = false">Iptal</v-btn>
          <v-btn color="error" variant="flat" :loading="deleting" @click="doDelete">Sil</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
