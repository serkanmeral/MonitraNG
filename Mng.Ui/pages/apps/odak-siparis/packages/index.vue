<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchOdakPackagesPage,
  fetchPackageLineStatsMap,
  packageDataId,
  packageDisplayNo,
  packageStatusLabel,
  type OdakPackageLineStats,
} from '@/utils/odakSiparisService';
import { PlusIcon, RefreshIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const router = useRouter();

type StatusTab = 'open' | 'closed' | 'all';

const statusTab = ref<StatusTab>('open');
const searchQuery = ref('');
const searchPanelOpen = ref<number | undefined>(undefined);
const loading = ref(false);
const errorMessage = ref('');
const items = ref<OdakPackageRow[]>([]);
const lineStats = ref<Map<string, OdakPackageLineStats>>(new Map());
const customerLabels = ref<Record<string, string>>({});
const totalCount = ref(0);
const tablePage = ref(1);
const tableItemsPerPage = ref(20);

const adv = ref({
  packageNo: '',
  packageName: '',
  customerPo: '',
  customerName: '',
  customerProjectNo: '',
  customerPoItem: '',
  productDesc: '',
});

const page = computed(() => ({ title: 'Is Paketleri' }));
const breadcrumbs = computed(() => [
  { text: t('operationCore.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: 'Odak Siparis', disabled: false, href: '/apps/odak-siparis/packages' },
  { text: 'Is Paketleri', disabled: true, href: '#' },
]);

const statusTabs = [
  { value: 'open' as const, label: 'Acik' },
  { value: 'closed' as const, label: 'Kapali' },
  { value: 'all' as const, label: 'Tumu' },
];

const headers = [
  { title: 'Is Paketi No', key: 'displayNo', sortable: false },
  { title: 'Is Paketi Ismi', key: 'name', sortable: false },
  { title: 'Musteri', key: 'customer', sortable: false },
  { title: 'Musteri PO', key: 'customerPo', sortable: false },
  { title: 'Proje No', key: 'projectNo', sortable: false },
  { title: 'Kalem', key: 'lineCount', sortable: false, width: 72 },
  { title: 'Durum', key: 'statusLabel', sortable: false },
  { title: 'Termin', key: 'deliveryDate', sortable: false },
  { title: 'Eylemler', key: 'actions', sortable: false, align: 'center' as const, width: 120 },
];

const hasLineSearch = computed(
  () =>
    Boolean(
      adv.value.customerProjectNo.trim() ||
        adv.value.customerPoItem.trim() ||
        adv.value.productDesc.trim()
    )
);

const hasClientFilter = computed(
  () =>
    Boolean(
      adv.value.customerName.trim() ||
        adv.value.customerPo.trim() ||
        hasLineSearch.value
    )
);

function buildSearchText(): string | undefined {
  const parts = [searchQuery.value.trim(), adv.value.packageName.trim()].filter(Boolean);
  return parts.length ? parts.join(' ') : undefined;
}

function applyClientFilters(list: OdakPackageRow[]): OdakPackageRow[] {
  let result = list;
  const cust = adv.value.customerName.trim().toLowerCase();
  if (cust) {
    result = result.filter((item) =>
      customerLabelFromRow(item, customerLabels.value).toLowerCase().includes(cust)
    );
  }
  if (hasLineSearch.value) {
    const proj = adv.value.customerProjectNo.trim().toLowerCase();
    const poItem = adv.value.customerPoItem.trim().toLowerCase();
    const desc = adv.value.productDesc.trim().toLowerCase();
    result = result.filter((item) => {
      const id = packageDataId(item);
      const stats = lineStats.value.get(id);
      if (!stats) return false;
      if (proj && !stats.customerProjectNos.toLowerCase().includes(proj)) return false;
      if (poItem && !stats.customerPoNos.toLowerCase().includes(poItem)) return false;
      if (desc && !stats.descriptions.some((d) => d.toLowerCase().includes(desc))) return false;
      return true;
    });
  }
  return result;
}

function rowDeliveryDate(item: OdakPackageRow): string {
  if (!item.deliveryDate) return '—';
  try {
    return new Date(String(item.deliveryDate)).toLocaleDateString('tr-TR');
  } catch {
    return String(item.deliveryDate);
  }
}

function lineCountFor(item: OdakPackageRow): string {
  const id = packageDataId(item);
  const fromStats = lineStats.value.get(id)?.lineCount;
  if (fromStats != null && fromStats > 0) return String(fromStats);
  if (item.lineCount != null && item.lineCount > 0) return String(item.lineCount);
  return '—';
}

function rowPo(item: OdakPackageRow): string {
  const id = packageDataId(item);
  const fromLines = lineStats.value.get(id)?.customerPoNos;
  if (fromLines) return fromLines;
  return '—';
}

function rowProjectNo(item: OdakPackageRow): string {
  const id = packageDataId(item);
  return lineStats.value.get(id)?.customerProjectNos || '—';
}

function clearAdvancedSearch() {
  adv.value = {
    packageNo: '',
    packageName: '',
    customerPo: '',
    customerName: '',
    customerProjectNo: '',
    customerPoItem: '',
    productDesc: '',
  };
  searchQuery.value = '';
}

async function fetchPackages() {
  loading.value = true;
  errorMessage.value = '';
  try {
    if (!Object.keys(customerLabels.value).length) {
      customerLabels.value = await fetchCustomerLabelMap();
    }

    const skip = hasClientFilter.value ? 0 : (tablePage.value - 1) * tableItemsPerPage.value;
    const limit = hasClientFilter.value ? 500 : tableItemsPerPage.value;

    const resp = await fetchOdakPackagesPage({
      statusTab: statusTab.value,
      skip,
      limit,
      search: buildSearchText(),
      packageNo: adv.value.packageNo.trim() || undefined,
    });

    let filtered = [...resp.items];
    const stats = await fetchPackageLineStatsMap(filtered.map((x) => packageDataId(x)));
    lineStats.value = stats;

    if (adv.value.customerPo.trim()) {
      const po = adv.value.customerPo.trim().toLowerCase();
      filtered = filtered.filter((item) => {
        const id = packageDataId(item);
        return (lineStats.value.get(id)?.customerPoNos ?? '').toLowerCase().includes(po);
      });
    }

    filtered = applyClientFilters(filtered);

    if (hasClientFilter.value) {
      const start = (tablePage.value - 1) * tableItemsPerPage.value;
      totalCount.value = filtered.length;
      items.value = filtered.slice(start, start + tableItemsPerPage.value);
    } else {
      items.value = filtered;
      totalCount.value = resp.total ?? filtered.length;
    }
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    items.value = [];
    totalCount.value = 0;
    lineStats.value = new Map();
  } finally {
    loading.value = false;
  }
}

function openDetail(item: OdakPackageRow) {
  router.push(`/apps/odak-siparis/packages/${encodeURIComponent(packageDataId(item))}`);
}

function createPackage() {
  router.push({
    path: `/apps/automated-forms/view/${ODAK_SIPARIS_CONFIG.packagesFormCode}`,
    query: {
      mode: 'create',
      returnTo: '/apps/odak-siparis/packages',
    },
  });
}

watch([statusTab, tablePage, tableItemsPerPage], () => {
  void fetchPackages();
});

let searchTimer: ReturnType<typeof setTimeout> | null = null;
function scheduleFetch() {
  if (tablePage.value !== 1) tablePage.value = 1;
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void fetchPackages(), 400);
}

watch(searchQuery, scheduleFetch);
watch(adv, scheduleFetch, { deep: true });

onMounted(() => {
  void fetchPackages();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10">
      <v-card-title class="d-flex flex-wrap align-center ga-3 py-4">
        <span class="text-h6">Is Paketleri</span>
        <v-spacer />
        <v-text-field
          v-model="searchQuery"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          placeholder="Hizli arama..."
          style="max-width: 220px"
        />
        <v-btn icon variant="outlined" size="small" :loading="loading" @click="fetchPackages">
          <RefreshIcon size="18" />
        </v-btn>
        <v-btn color="primary" variant="flat" @click="createPackage">
          <PlusIcon class="mr-1" size="18" />
          Is Paketi Ekle
        </v-btn>
      </v-card-title>

      <v-expansion-panels v-model="searchPanelOpen" class="px-4 pb-2">
        <v-expansion-panel>
          <v-expansion-panel-title>Is Paketi Arama</v-expansion-panel-title>
          <v-expansion-panel-text>
            <v-row dense>
              <v-col cols="12" sm="6" md="4">
                <v-text-field v-model="adv.packageNo" label="Is Paketi No" density="compact" hide-details />
              </v-col>
              <v-col cols="12" sm="6" md="4">
                <v-text-field v-model="adv.packageName" label="Is Paketi Ismi" density="compact" hide-details />
              </v-col>
              <v-col cols="12" sm="6" md="4">
                <v-text-field v-model="adv.customerPo" label="Musteri PO No" density="compact" hide-details />
              </v-col>
              <v-col cols="12" sm="6" md="4">
                <v-text-field v-model="adv.customerName" label="Musteri" density="compact" hide-details />
              </v-col>
              <v-col cols="12" sm="6" md="4">
                <v-text-field
                  v-model="adv.customerProjectNo"
                  label="Musteri Proje No"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12" sm="6" md="4">
                <v-text-field
                  v-model="adv.customerPoItem"
                  label="Musteri PO Kalem"
                  density="compact"
                  hide-details
                />
              </v-col>
              <v-col cols="12" sm="6" md="4">
                <v-text-field
                  v-model="adv.productDesc"
                  label="Urun / Hizmet Tanimi"
                  density="compact"
                  hide-details
                  hint="Kalem bazli — yuklenen paketler uzerinde"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12" class="d-flex ga-2">
                <v-btn size="small" variant="tonal" @click="scheduleFetch">Ara</v-btn>
                <v-btn size="small" variant="text" @click="clearAdvancedSearch">Temizle</v-btn>
              </v-col>
            </v-row>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>

      <v-tabs v-model="statusTab" color="primary" class="px-4">
        <v-tab v-for="tab in statusTabs" :key="tab.value" :value="tab.value">
          {{ tab.label }}
        </v-tab>
      </v-tabs>

      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">
          {{ errorMessage }}
        </v-alert>
        <v-alert v-if="hasClientFilter" type="info" variant="tonal" density="compact" class="mb-4">
          Musteri / kalem bazli arama aktif — sonuclar yuklenen paketler uzerinde filtrelenir.
        </v-alert>

        <v-data-table
          :headers="headers"
          :items="items"
          :loading="loading"
          :items-per-page="tableItemsPerPage"
          :page="tablePage"
          :items-length="totalCount"
          item-value="__dataId"
          class="border rounded-md"
          @update:page="(p) => (tablePage = p)"
          @update:items-per-page="(n) => (tableItemsPerPage = n)"
        >
          <template #item.displayNo="{ item }">
            <a
              href="#"
              class="text-primary text-decoration-none font-weight-medium"
              @click.prevent="openDetail(item)"
            >
              {{ packageDisplayNo(item) }}
            </a>
          </template>
          <template #item.customer="{ item }">
            {{ customerLabelFromRow(item, customerLabels) }}
          </template>
          <template #item.customerPo="{ item }">
            {{ rowPo(item) }}
          </template>
          <template #item.projectNo="{ item }">
            {{ rowProjectNo(item) }}
          </template>
          <template #item.lineCount="{ item }">
            {{ lineCountFor(item) }}
          </template>
          <template #item.statusLabel="{ item }">
            {{ packageStatusLabel(item.status) }}
          </template>
          <template #item.deliveryDate="{ item }">
            {{ rowDeliveryDate(item) }}
          </template>
          <template #item.actions="{ item }">
            <v-btn size="small" variant="text" color="primary" @click="openDetail(item)">
              Goruntule
            </v-btn>
          </template>
        </v-data-table>
      </v-card-text>
    </v-card>
  </div>
</template>
