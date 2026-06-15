<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OdakSiparisLinesPanel from '@/components/apps/odak-siparis/OdakSiparisLinesPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchOdakPackageById,
  packageDisplayNo,
  packageStatusLabel,
} from '@/utils/odakSiparisService';
import { EditIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

const packageId = computed(() => String(route.params.id ?? ''));
const activeTab = ref<'summary' | 'lines'>(route.query.tab === 'lines' ? 'lines' : 'summary');
const loading = ref(false);
const errorMessage = ref('');
const pkg = ref<OdakPackageRow | null>(null);
const customerLabels = ref<Record<string, string>>({});

const pageTitle = computed(() => {
  if (!pkg.value) return 'Is Paketi';
  return packageDisplayNo(pkg.value);
});

const breadcrumbs = computed(() => [
  { text: t('operationCore.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: 'Odak Siparis', disabled: false, href: '/apps/odak-siparis/packages' },
  { text: 'Is Paketleri', disabled: false, href: '/apps/odak-siparis/packages' },
  { text: pageTitle.value, disabled: true, href: '#' },
]);

const summaryRows = computed(() => {
  const p = pkg.value;
  if (!p) return [];
  return [
    { label: 'Is Paketi No', value: p.packageNo ?? '—' },
    { label: 'Is Paketi Ismi', value: p.name ?? '—' },
    { label: 'Durum', value: packageStatusLabel(p.status) },
    { label: 'Musteri', value: customerLabelFromRow(p, customerLabels.value) },
    { label: 'Baslangic', value: formatDate(p.beginDate) },
    { label: 'Termin', value: formatDate(p.deliveryDate) },
    { label: 'Teslimat adresi', value: p.deliveryAddress ?? '—' },
    { label: 'Odeme bilgisi', value: p.paymentDetail ?? '—' },
    { label: 'Notlar', value: p.notes ?? '—' },
    { label: 'Kalem sayisi', value: p.lineCount != null ? String(p.lineCount) : '—' },
    ...(p.workItemKey ? [{ label: 'MO kayit', value: p.workItemKey }] : []),
  ];
});

function formatDate(v: unknown): string {
  if (!v) return '—';
  try {
    return new Date(String(v)).toLocaleDateString('tr-TR');
  } catch {
    return String(v);
  }
}

async function loadPackage() {
  if (!packageId.value) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    if (!Object.keys(customerLabels.value).length) {
      customerLabels.value = await fetchCustomerLabelMap();
    }
    pkg.value = await fetchOdakPackageById(packageId.value);
    if (!pkg.value) {
      errorMessage.value = 'Is paketi bulunamadi.';
    }
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    pkg.value = null;
  } finally {
    loading.value = false;
  }
}

function openEdit() {
  router.push({
    path: `/apps/automated-forms/view/${ODAK_SIPARIS_CONFIG.packagesFormCode}`,
    query: {
      editId: packageId.value,
      returnTo: `/apps/odak-siparis/packages/${encodeURIComponent(packageId.value)}`,
    },
  });
}

function openMoProfile() {
  if (!pkg.value?.workItemId) return;
  router.push({
    path: `/apps/operation-core/work-items/${encodeURIComponent(pkg.value.workItemId)}/profile`,
    query: { from: 'odak-siparis' },
  });
}

onMounted(() => {
  void loadPackage();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="pageTitle" :breadcrumbs="breadcrumbs" />

    <v-card elevation="10">
      <v-card-title class="d-flex flex-wrap align-center py-4">
        <div>
          <div class="text-h6">{{ pageTitle }}</div>
          <div v-if="pkg?.name" class="text-body-2 text-medium-emphasis">
            {{ pkg.name }}
          </div>
        </div>
        <v-spacer />
        <v-btn variant="outlined" size="small" class="mr-2" @click="openEdit">
          <EditIcon class="mr-1" size="16" />
          Duzenle
        </v-btn>
        <v-btn
          v-if="pkg?.workItemId"
          variant="text"
          size="small"
          @click="openMoProfile"
        >
          MO Profil
        </v-btn>
      </v-card-title>

      <v-tabs v-model="activeTab" color="primary" class="px-4">
        <v-tab value="summary">Ozet</v-tab>
        <v-tab value="lines">Kalemler</v-tab>
      </v-tabs>

      <v-card-text>
        <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">
          {{ errorMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

        <div v-show="activeTab === 'summary'">
          <v-table density="comfortable" class="border rounded-md">
            <tbody>
              <tr v-for="row in summaryRows" :key="row.label">
                <td class="font-weight-medium" width="220">{{ row.label }}</td>
                <td>{{ row.value }}</td>
              </tr>
            </tbody>
          </v-table>
        </div>

        <div v-show="activeTab === 'lines'">
          <OdakSiparisLinesPanel
            :package-id="packageId"
            :package-no="pkg?.packageNo"
          />
        </div>
      </v-card-text>
    </v-card>
  </div>
</template>
