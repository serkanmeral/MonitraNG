<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisCustomerQualityReqCopyDialog from '@/components/apps/odak-siparis/OdakSiparisCustomerQualityReqCopyDialog.vue';
import OdakSiparisCustomerQualityReqDialog from '@/components/apps/odak-siparis/OdakSiparisCustomerQualityReqDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerQualityReqRow, OdakCustomerRow } from '@/utils/odakSiparisConfig';
import {
  deleteOdakCustomerQualityReq,
  listQualityReqsForCustomer,
  qualityReqDataId,
  type OdakCustomerQualityReqDialogMode,
} from '@/utils/odakSiparisCustomerQualityReqService';
import { packageDataId } from '@/utils/odakSiparisService';
import { CopyIcon, EditIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  customerRow: OdakCustomerRow;
}>();

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const reqs = ref<OdakCustomerQualityReqRow[]>([]);

const deleteDialog = ref(false);
const reqToDelete = ref<OdakCustomerQualityReqRow | null>(null);
const deleting = ref(false);

const dialogOpen = ref(false);
const dialogMode = ref<OdakCustomerQualityReqDialogMode>('create');
const dialogReqId = ref<string | undefined>();
const dialogSeed = ref<OdakCustomerQualityReqRow | null>(null);

const copyDialogOpen = ref(false);
const copyMode = ref<'template' | 'customer'>('template');

const customerId = computed(() => packageDataId(props.customerRow));
const customerLabel = computed(() => props.customerRow.unvan || props.customerRow.kod || '—');

const headers = computed(() => [
  { title: t('odakSiparis.customers.qualityReqs.fields.kod'), key: 'kod', minWidth: 100 },
  { title: t('odakSiparis.customers.qualityReqs.fields.ad'), key: 'ad', minWidth: 160 },
  { title: t('odakSiparis.customers.qualityReqs.fields.faiUygulanacak'), key: 'faiLabel', width: 88 },
  { title: t('odakSiparis.customers.qualityReqs.fields.aktif'), key: 'aktifLabel', width: 88 },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: 96,
  },
]);

async function loadReqs() {
  const id = customerId.value;
  if (!id) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    reqs.value = await listQualityReqsForCustomer(id);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    reqs.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakCustomerQualityReqDialogMode, row?: OdakCustomerQualityReqRow) {
  dialogMode.value = mode;
  dialogReqId.value = row ? qualityReqDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

function openCopyDialog(mode: 'template' | 'customer') {
  copyMode.value = mode;
  copyDialogOpen.value = true;
}

function confirmDelete(row: OdakCustomerQualityReqRow) {
  reqToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = reqToDelete.value;
  if (!row) return;
  const id = qualityReqDataId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await deleteOdakCustomerQualityReq(id);
    deleteDialog.value = false;
    reqToDelete.value = null;
    await loadReqs();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

watch(
  () => customerId.value,
  () => {
    void loadReqs();
  }
);

onMounted(() => {
  void loadReqs();
});
</script>

<template>
  <div class="odak-customer-quality-reqs-panel pa-3 px-4">
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <div>
        <div class="text-subtitle-2 font-weight-medium">
          {{ t('odakSiparis.customers.qualityReqs.title') }}
        </div>
        <div class="text-caption text-medium-emphasis">{{ customerLabel }}</div>
      </div>
      <v-chip v-if="reqs.length" size="small" variant="tonal" color="primary">
        {{ reqs.length }}
      </v-chip>
      <v-spacer />
      <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadReqs">
        <RefreshIcon size="18" />
      </v-btn>
      <v-btn variant="tonal" size="small" @click="openCopyDialog('template')">
        <CopyIcon class="mr-1" size="16" />
        {{ t('odakSiparis.customers.qualityReqs.copyFromTemplate') }}
      </v-btn>
      <v-btn variant="tonal" size="small" @click="openCopyDialog('customer')">
        <CopyIcon class="mr-1" size="16" />
        {{ t('odakSiparis.customers.qualityReqs.copyFromCustomer') }}
      </v-btn>
      <v-btn color="primary" variant="flat" size="small" @click="openDialog('create')">
        <PlusIcon class="mr-1" size="16" />
        {{ t('odakSiparis.customers.qualityReqs.add') }}
      </v-btn>
    </div>

    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="reqs"
      :loading="loading"
      item-value="__dataId"
      density="compact"
      class="border rounded-md"
    >
      <template #item.faiLabel="{ item }">
        {{
          item.faiUygulanacak
            ? t('odakSiparis.customers.qualityReqs.faiYes')
            : t('odakSiparis.customers.qualityReqs.faiNo')
        }}
      </template>
      <template #item.aktifLabel="{ item }">
        <v-chip size="x-small" :color="item.aktif !== false ? 'success' : 'error'" variant="tonal">
          {{ item.aktif !== false ? t('odakSiparis.customers.activeYes') : t('odakSiparis.customers.activeNo') }}
        </v-chip>
      </template>
      <template #item.actions="{ item }">
        <div class="d-inline-flex align-center justify-end ga-1">
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
          {{ t('odakSiparis.customers.qualityReqs.empty') }}
        </div>
      </template>
    </v-data-table>

    <OdakSiparisCustomerQualityReqDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :customer-id="customerId"
      :req-id="dialogReqId"
      :seed-row="dialogSeed"
      @saved="loadReqs"
    />

    <OdakSiparisCustomerQualityReqCopyDialog
      v-model="copyDialogOpen"
      :mode="copyMode"
      :customer-row="customerRow"
      @copied="loadReqs"
    />

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('odakSiparis.customers.qualityReqs.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.customers.qualityReqs.deleteConfirm') }}</v-card-text>
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
.odak-customer-quality-reqs-panel {
  background: rgba(var(--v-theme-on-surface), 0.02);
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
