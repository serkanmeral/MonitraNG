<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisCustomerContactDialog from '@/components/apps/odak-siparis/OdakSiparisCustomerContactDialog.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerContactRow, OdakCustomerRow } from '@/utils/odakSiparisConfig';
import {
  contactDataId,
  deleteOdakCustomerContact,
  listContactsForCustomer,
  type OdakCustomerContactDialogMode,
} from '@/utils/odakSiparisCustomerContactService';
import { packageDataId } from '@/utils/odakSiparisService';
import { EditIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  customerRow: OdakCustomerRow;
}>();

const { t } = useAppI18n();

const loading = ref(false);
const errorMessage = ref('');
const contacts = ref<OdakCustomerContactRow[]>([]);

const deleteDialog = ref(false);
const contactToDelete = ref<OdakCustomerContactRow | null>(null);
const deleting = ref(false);

const dialogOpen = ref(false);
const dialogMode = ref<OdakCustomerContactDialogMode>('create');
const dialogContactId = ref<string | undefined>();
const dialogSeed = ref<OdakCustomerContactRow | null>(null);

const customerId = computed(() => packageDataId(props.customerRow));
const customerLabel = computed(() => props.customerRow.unvan || props.customerRow.kod || '—');

const headers = computed(() => [
  { title: t('odakSiparis.customers.contacts.fields.ad'), key: 'ad', minWidth: 140 },
  { title: t('odakSiparis.customers.contacts.fields.email'), key: 'email', minWidth: 180 },
  { title: t('odakSiparis.customers.contacts.fields.telefon'), key: 'telefon', width: 120 },
  { title: t('odakSiparis.customers.contacts.fields.gorevUnvani'), key: 'gorevUnvani', minWidth: 140 },
  { title: t('odakSiparis.customers.contacts.fields.birincilKisi'), key: 'birincilKisi', width: 100 },
  { title: t('odakSiparis.customers.contacts.fields.aktif'), key: 'aktifLabel', width: 88 },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: 96,
  },
]);

async function loadContacts() {
  const id = customerId.value;
  if (!id) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    contacts.value = await listContactsForCustomer(id);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    contacts.value = [];
  } finally {
    loading.value = false;
  }
}

function openDialog(mode: OdakCustomerContactDialogMode, row?: OdakCustomerContactRow) {
  dialogMode.value = mode;
  dialogContactId.value = row ? contactDataId(row) : undefined;
  dialogSeed.value = row ?? null;
  dialogOpen.value = true;
}

function confirmDelete(row: OdakCustomerContactRow) {
  contactToDelete.value = row;
  deleteDialog.value = true;
}

async function doDelete() {
  const row = contactToDelete.value;
  if (!row) return;
  const id = contactDataId(row);
  if (!id) return;
  deleting.value = true;
  try {
    await deleteOdakCustomerContact(id);
    deleteDialog.value = false;
    contactToDelete.value = null;
    await loadContacts();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

watch(
  () => customerId.value,
  () => {
    void loadContacts();
  }
);

onMounted(() => {
  void loadContacts();
});
</script>

<template>
  <div class="odak-customer-contacts-panel pa-3 px-4">
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <div>
        <div class="text-subtitle-2 font-weight-medium">
          {{ t('odakSiparis.customers.contacts.title') }}
        </div>
        <div class="text-caption text-medium-emphasis">{{ customerLabel }}</div>
      </div>
      <v-chip v-if="contacts.length" size="small" variant="tonal" color="primary">
        {{ contacts.length }}
      </v-chip>
      <v-spacer />
      <v-btn icon variant="outlined" size="small" :loading="loading" @click="loadContacts">
        <RefreshIcon size="18" />
      </v-btn>
      <v-btn color="primary" variant="flat" size="small" @click="openDialog('create')">
        <PlusIcon class="mr-1" size="16" />
        {{ t('odakSiparis.customers.contacts.add') }}
      </v-btn>
    </div>

    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-3">
      {{ errorMessage }}
    </v-alert>

    <v-data-table
      :headers="headers"
      :items="contacts"
      :loading="loading"
      item-value="__dataId"
      density="compact"
      class="border rounded-md"
    >
      <template #item.birincilKisi="{ item }">
        <v-chip v-if="item.birincilKisi" size="x-small" color="primary" variant="tonal">
          {{ t('odakSiparis.customers.contacts.primaryYes') }}
        </v-chip>
        <span v-else class="text-medium-emphasis">—</span>
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
          {{ t('odakSiparis.customers.contacts.empty') }}
        </div>
      </template>
    </v-data-table>

    <OdakSiparisCustomerContactDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :customer-id="customerId"
      :contact-id="dialogContactId"
      :seed-row="dialogSeed"
      @saved="loadContacts"
    />

    <v-dialog v-model="deleteDialog" max-width="420">
      <v-card>
        <v-card-title>{{ t('odakSiparis.customers.contacts.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.customers.contacts.deleteConfirm') }}</v-card-text>
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
.odak-customer-contacts-panel {
  background: rgba(var(--v-theme-on-surface), 0.02);
}
</style>
