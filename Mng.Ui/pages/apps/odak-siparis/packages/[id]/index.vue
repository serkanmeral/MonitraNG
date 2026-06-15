<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OdakSiparisLinesPanel from '@/components/apps/odak-siparis/OdakSiparisLinesPanel.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocDelete } from '@/services/operationCoreService';
import { ODAK_SIPARIS_CONFIG, type OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  customerFormRoute,
  customerIdFromRow,
  customerLabelFromRow,
  fetchCustomerLabelMap,
  fetchOdakPackageById,
  formatOdakDate,
  formatOdakNumber,
  packageDisplayNo,
  packageStatusLabel,
} from '@/utils/odakSiparisService';
import { EditIcon, TrashIcon } from 'vue-tabler-icons';

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
const deleteDialog = ref(false);
const deleting = ref(false);

const pageTitle = computed(() => {
  if (!pkg.value) return t('odakSiparis.detail.title');
  return packageDisplayNo(pkg.value);
});

const breadcrumbs = computed(() => [
  { text: t('operationCore.breadcrumbs.home'), disabled: false, href: '/dashboards/analytical' },
  { text: t('odakSiparis.module'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: t('odakSiparis.packages.title'), disabled: false, href: '/apps/odak-siparis/packages' },
  { text: pageTitle.value, disabled: true, href: '#' },
]);

const customerId = computed(() => (pkg.value ? customerIdFromRow(pkg.value) : ''));
const customerLabel = computed(() =>
  pkg.value ? customerLabelFromRow(pkg.value, customerLabels.value) : '—'
);

type SummaryRow = { label: string; value: string; link?: string };

function optionalRow(labelKey: string, value: unknown): SummaryRow | null {
  if (value == null || value === '') return null;
  return { label: t(labelKey), value: String(value) };
}

const summaryRows = computed((): SummaryRow[] => {
  const p = pkg.value;
  if (!p) return [];

  const rows: SummaryRow[] = [
    { label: t('odakSiparis.detail.fields.packageNo'), value: p.packageNo ?? '—' },
    { label: t('odakSiparis.detail.fields.name'), value: p.name ?? '—' },
    { label: t('odakSiparis.detail.fields.status'), value: packageStatusLabel(p.status) },
    {
      label: t('odakSiparis.detail.fields.customer'),
      value: customerLabel.value,
      link: customerId.value ? customerFormRoute(customerId.value) : undefined,
    },
    { label: t('odakSiparis.detail.fields.partCount'), value: formatOdakNumber(p.partCount) },
    { label: t('odakSiparis.detail.fields.stockCount'), value: formatOdakNumber(p.stockCount) },
    { label: t('odakSiparis.detail.fields.shippedCount'), value: formatOdakNumber(p.shippedCount) },
    { label: t('odakSiparis.detail.fields.lineCount'), value: formatOdakNumber(p.lineCount) },
    { label: t('odakSiparis.detail.fields.beginDate'), value: formatOdakDate(p.beginDate) },
    { label: t('odakSiparis.detail.fields.deliveryDate'), value: formatOdakDate(p.deliveryDate) },
    { label: t('odakSiparis.detail.fields.deliveryAddress'), value: p.deliveryAddress ?? '—' },
    { label: t('odakSiparis.detail.fields.paymentDetail'), value: p.paymentDetail ?? '—' },
    { label: t('odakSiparis.detail.fields.notes'), value: p.notes ?? '—' },
  ];

  const legacyRows = [
    optionalRow('odakSiparis.detail.fields.customerContact', p.legacyContactId),
    optionalRow('odakSiparis.detail.fields.packageResponsible', p.legacyResponsibleId),
    optionalRow('odakSiparis.detail.fields.designResponsible', p.legacyDesignResponsibleId),
    optionalRow('odakSiparis.detail.fields.manufactureResponsible', p.legacyManufactureResponsibleId),
  ].filter(Boolean) as SummaryRow[];
  rows.push(...legacyRows);

  const createdAt = p.__createdAt ?? p.legacyCreatedAt;
  const updatedAt = p.__updatedAt ?? p.legacyUpdatedAt;
  const createdBy = p.__createdBy ?? p.legacyCreatedBy;
  const updatedBy = p.__updatedBy ?? p.legacyUpdatedBy;

  if (createdAt) {
    rows.push({ label: t('odakSiparis.detail.fields.createdAt'), value: formatOdakDate(createdAt) });
  }
  if (updatedAt) {
    rows.push({ label: t('odakSiparis.detail.fields.updatedAt'), value: formatOdakDate(updatedAt) });
  }
  if (createdBy) {
    rows.push({ label: t('odakSiparis.detail.fields.createdBy'), value: String(createdBy) });
  }
  if (updatedBy) {
    rows.push({ label: t('odakSiparis.detail.fields.updatedBy'), value: String(updatedBy) });
  }
  if (p.workItemKey) {
    rows.push({ label: t('odakSiparis.detail.fields.workItemKey'), value: p.workItemKey });
  }

  return rows;
});

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
      errorMessage.value = t('odakSiparis.packages.notFound');
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

async function doDelete() {
  if (!packageId.value) return;
  deleting.value = true;
  errorMessage.value = '';
  try {
    await ocDelete(ODAK_SIPARIS_CONFIG.packagesDataset, packageId.value);
    deleteDialog.value = false;
    await router.push('/apps/odak-siparis/packages');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
  } finally {
    deleting.value = false;
  }
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
          {{ t('odakSiparis.detail.edit') }}
        </v-btn>
        <v-btn variant="outlined" size="small" color="error" class="mr-2" @click="deleteDialog = true">
          <TrashIcon class="mr-1" size="16" />
          {{ t('odakSiparis.detail.delete') }}
        </v-btn>
        <v-btn
          v-if="pkg?.workItemId"
          variant="text"
          size="small"
          @click="openMoProfile"
        >
          {{ t('odakSiparis.detail.moProfile') }}
        </v-btn>
      </v-card-title>

      <v-tabs v-model="activeTab" color="primary" class="px-4">
        <v-tab value="summary">{{ t('odakSiparis.detail.tabs.summary') }}</v-tab>
        <v-tab value="lines">{{ t('odakSiparis.detail.tabs.lines') }}</v-tab>
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
                <td class="font-weight-medium" width="240">{{ row.label }}</td>
                <td>
                  <NuxtLink
                    v-if="row.link"
                    :to="row.link"
                    class="text-primary text-decoration-none"
                  >
                    {{ row.value }}
                  </NuxtLink>
                  <span v-else>{{ row.value }}</span>
                </td>
              </tr>
            </tbody>
          </v-table>
        </div>

        <div v-if="activeTab === 'lines'">
          <OdakSiparisLinesPanel
            :package-id="packageId"
            :package-no="pkg?.packageNo"
          />
        </div>
      </v-card-text>
    </v-card>

    <v-dialog v-model="deleteDialog" max-width="460">
      <v-card>
        <v-card-title>{{ t('odakSiparis.detail.deleteTitle') }}</v-card-title>
        <v-card-text>{{ t('odakSiparis.detail.deleteConfirm') }}</v-card-text>
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
