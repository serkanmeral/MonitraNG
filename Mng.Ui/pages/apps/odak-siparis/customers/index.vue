<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import OdakSiparisCustomerContactsPanel from '@/components/apps/odak-siparis/OdakSiparisCustomerContactsPanel.vue';
import OdakSiparisCustomerQualityReqPanel from '@/components/apps/odak-siparis/OdakSiparisCustomerQualityReqPanel.vue';
import OdakSiparisCustomerDialog from '@/components/apps/odak-siparis/OdakSiparisCustomerDialog.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';
import { ODAK_DATA_TABLE_EXPAND_COLUMN, ODAK_SIPARIS_CONFIG, type OdakCustomerRow } from '@/utils/odakSiparisConfig';
import {
  customerSektorLabel,
  fetchOdakCustomersPage,
  packagesByCustomerRoute,
  type OdakCustomerDialogMode,
  type OdakCustomerListSort,
} from '@/utils/odakSiparisCustomerService';
import { invalidateOdakSiparisCustomerCache, packageDataId } from '@/utils/odakSiparisService';
import { EditIcon, PlusIcon, RefreshIcon, TrashIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

type AktifTab = 'active' | 'inactive' | 'all';

const aktifTab = ref<AktifTab>('active');
const searchQuery = ref('');
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakCustomerRow[]>([]);
const activeListFilters = ref<AfListFilter[]>([]);
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);
const tableItemsPerPageOptions = [10, 20, 50, 100];
const tableSortBy = ref<OdakCustomerListSort[]>([{ key: 'unvan', order: 'asc' }]);
const expandedIds = ref<string[]>([]);

const dialogOpen = ref(false);
const dialogMode = ref<OdakCustomerDialogMode>('create');
const dialogCustomerId = ref<string | undefined>();
const dialogSeed = ref<OdakCustomerRow | null>(null);

const deleteDialog = ref(false);
const itemToDelete = ref<OdakCustomerRow | null>(null);
const deleting = ref(false);

const page = computed(() => ({ title: t('odakSiparis.customers.title') }));
const breadcrumbs = computed(() => [
  { text: t('breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.customers.title'), disabled: true, href: '#' },
]);

const aktifTabs = computed(() => [
  { value: 'active' as const, label: t('odakSiparis.customers.tabs.active') },
  { value: 'inactive' as const, label: t('odakSiparis.customers.tabs.inactive') },
  { value: 'all' as const, label: t('odakSiparis.customers.tabs.all') },
]);

const filterColumns = computed<AfFilterColumn[]>(() => [
  { key: 'kod', label: t('odakSiparis.customers.fields.kod'), kind: 'text' },
  { key: 'unvan', label: t('odakSiparis.customers.fields.unvan'), kind: 'text' },
  { key: 'sektor', label: t('odakSiparis.customers.fields.sektor'), kind: 'text' },
  { key: 'ulke', label: t('odakSiparis.customers.fields.ulke'), kind: 'text' },
]);

const headers = computed(() => [
  { ...ODAK_DATA_TABLE_EXPAND_COLUMN },
  { title: t('odakSiparis.customers.fields.kod'), key: 'kod', sortable: true },
  { title: t('odakSiparis.customers.fields.unvan'), key: 'unvan', sortable: true },
  { title: t('odakSiparis.customers.fields.sektor'), key: 'sektor', sortable: true },
  { title: t('odakSiparis.customers.fields.ulke'), key: 'ulke', sortable: true },
  { title: t('odakSiparis.customers.fields.aktif'), key: 'aktifLabel', sortable: true },
  {
    title: t('odakSiparis.packages.columns.actions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: 120,
  },
]);

const paginationLabel = computed(() =>
  t('odakSiparis.packages.paginationSummary', {
    from: totalCount.value === 0 ? 0 : (tablePage.value - 1) * tableItemsPerPage.value + 1,
    to: Math.min(tablePage.value * tableItemsPerPage.value, totalCount.value),
    total: totalCount.value,
  })
);

function openDialog(mode: OdakCustomerDialogMode, item?: OdakCustomerRow) {
  dialogMode.value = mode;
  dialogCustomerId.value = item ? packageDataId(item) : undefined;
  dialogSeed.value = item ?? null;
  dialogOpen.value = true;
}

async function fetchCustomers() {
  loading.value = true;
  errorMessage.value = '';
  try {
    const resp = await fetchOdakCustomersPage({
      aktifTab: aktifTab.value,
      skip: (tablePage.value - 1) * tableItemsPerPage.value,
      limit: tableItemsPerPage.value,
      search: searchQuery.value.trim() || undefined,
      advancedFilters: activeListFilters.value,
      sortBy: tableSortBy.value,
    });
    items.value = resp.items;
    totalCount.value = resp.total;
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
    totalCount.value = 0;
  } finally {
    loading.value = false;
  }
}

function onListFiltersUpdate(filters: AfListFilter[]) {
  activeListFilters.value = filters;
  if (tablePage.value !== 1) tablePage.value = 1;
  else void fetchCustomers();
}

function confirmDelete(item: OdakCustomerRow) {
  itemToDelete.value = item;
  deleteDialog.value = true;
}

async function doDelete() {
  const item = itemToDelete.value;
  if (!item) return;
  const id = packageDataId(item);
  if (!id) return;
  deleting.value = true;
  errorMessage.value = '';
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.customersDataset, id);
    invalidateOdakSiparisCustomerCache();
    deleteDialog.value = false;
    itemToDelete.value = null;
    await fetchCustomers();
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
}

async function onCustomerSaved() {
  invalidateOdakSiparisCustomerCache();
  await fetchCustomers();
  const edit = route.query.edit;
  if (typeof edit === 'string' && edit.trim()) {
    void router.replace({ path: route.path, query: {} });
  }
}

type TableOptions = {
  page: number;
  itemsPerPage: number;
  sortBy?: OdakCustomerListSort[];
};

function onTableOptions(options: TableOptions) {
  const nextSort = Array.isArray(options.sortBy) && options.sortBy.length
    ? options.sortBy
    : [{ key: 'unvan', order: 'asc' as const }];
  const sortChanged = JSON.stringify(nextSort) !== JSON.stringify(tableSortBy.value);
  const nextSize = options.itemsPerPage;
  const sizeChanged = nextSize !== tableItemsPerPage.value;
  let nextPage = options.page;
  if (sortChanged || sizeChanged) nextPage = 1;
  const pageChanged = nextPage !== tablePage.value;
  if (!sortChanged && !pageChanged && !sizeChanged) return;
  tableSortBy.value = nextSort;
  tablePage.value = nextPage;
  tableItemsPerPage.value = nextSize;
  void fetchCustomers();
}

let searchTimer: ReturnType<typeof setTimeout> | null = null;
function scheduleFetch() {
  if (tablePage.value !== 1) tablePage.value = 1;
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void fetchCustomers(), 400);
}

watch(searchQuery, scheduleFetch);
watch(aktifTab, () => {
  if (tablePage.value !== 1) tablePage.value = 1;
  else void fetchCustomers();
});

watch(expandedIds, (ids) => {
  if (ids.length > 1) expandedIds.value = [ids[ids.length - 1]!];
});

function openPackagesFor(item: OdakCustomerRow) {
  const id = packageDataId(item);
  if (!id) return;
  void router.push(packagesByCustomerRoute(id));
}

onMounted(() => {
  void fetchCustomers();
  const edit = route.query.edit;
  if (typeof edit === 'string' && edit.trim()) {
    dialogMode.value = 'edit';
    dialogCustomerId.value = edit.trim();
    dialogSeed.value = null;
    dialogOpen.value = true;
  }
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10">
      <v-card-title class="d-flex flex-wrap align-center ga-3 py-4">
        <span class="text-h6">{{ t('odakSiparis.customers.title') }}</span>
        <v-spacer />
        <v-text-field
          v-model="searchQuery"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          :placeholder="t('odakSiparis.packages.quickSearch')"
          style="max-width: 220px"
        />
        <v-btn icon variant="outlined" size="small" :loading="loading" @click="fetchCustomers">
          <RefreshIcon size="18" />
        </v-btn>
        <v-btn color="primary" variant="flat" @click="openDialog('create')">
          <PlusIcon class="mr-1" size="18" />
          {{ t('odakSiparis.customers.add') }}
        </v-btn>
      </v-card-title>

      <div class="px-4 pb-2">
        <AfListFilters :columns="filterColumns" @update:filters="onListFiltersUpdate" />
      </div>

      <v-tabs v-model="aktifTab" color="primary" class="px-4">
        <v-tab v-for="tab in aktifTabs" :key="tab.value" :value="tab.value">
          {{ tab.label }}
        </v-tab>
      </v-tabs>

      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">
          {{ errorMessage }}
        </v-alert>

        <v-data-table-server
          v-model:expanded="expandedIds"
          :headers="headers"
          :items="items"
          :loading="loading"
          :items-per-page="tableItemsPerPage"
          :items-per-page-options="tableItemsPerPageOptions"
          :page="tablePage"
          :items-length="totalCount"
          :sort-by="tableSortBy"
          item-value="__dataId"
          show-expand
          :expand-on-click="false"
          class="border rounded-md odak-customers-list-table"
          @update:options="onTableOptions"
        >
          <template #expanded-row="{ columns, item }">
            <tr>
              <td :colspan="columns.length" class="pa-0">
                <OdakSiparisCustomerContactsPanel :customer-row="item" />
                <OdakSiparisCustomerQualityReqPanel :customer-row="item" />
              </td>
            </tr>
          </template>
          <template #item.kod="{ item }">
            <a
              href="#"
              class="text-primary text-decoration-none font-weight-medium"
              @click.prevent="openDialog('edit', item)"
            >
              {{ item.kod ?? '—' }}
            </a>
          </template>
          <template #item.sektor="{ item }">
            {{ customerSektorLabel(item.sektor) }}
          </template>
          <template #item.aktifLabel="{ item }">
            <v-chip size="x-small" :color="item.aktif !== false ? 'success' : 'error'" variant="tonal">
              {{ item.aktif !== false ? t('odakSiparis.customers.activeYes') : t('odakSiparis.customers.activeNo') }}
            </v-chip>
          </template>
          <template #item.actions="{ item }">
            <div class="d-inline-flex align-center justify-end ga-1">
              <v-btn icon size="x-small" variant="text" :title="t('odakSiparis.customers.openPackages')" @click="openPackagesFor(item)">
                <v-icon size="18">mdi-clipboard-list-outline</v-icon>
              </v-btn>
              <v-btn icon size="x-small" variant="text" @click="openDialog('edit', item)">
                <EditIcon size="18" />
              </v-btn>
              <v-btn icon size="x-small" variant="text" color="error" @click="confirmDelete(item)">
                <TrashIcon size="18" />
              </v-btn>
            </div>
          </template>
        </v-data-table-server>
        <div v-if="totalCount > 0" class="text-caption text-medium-emphasis mt-2">
          {{ paginationLabel }}
        </div>
      </v-card-text>
    </v-card>

    <OdakSiparisCustomerDialog
      v-model="dialogOpen"
      :mode="dialogMode"
      :customer-id="dialogCustomerId"
      :seed-row="dialogSeed"
      @saved="onCustomerSaved"
    />

    <v-dialog v-model="deleteDialog" max-width="460">
      <v-card>
        <v-card-title>{{ t('odakSiparis.customers.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.customers.deleteConfirm') }}</v-card-text>
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
.odak-customers-list-table :deep(table) > tbody > tr:not(.v-data-table__expanded__content) > td:first-child {
  width: 48px;
  padding-inline: 8px;
}
</style>
