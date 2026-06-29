<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import OdakSiparisDashboardStatCard from '@/components/apps/odak-siparis/OdakSiparisDashboardStatCard.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerRow } from '@/utils/odakSiparisConfig';
import { customerActorRoleChips, packagesByCustomerRoute } from '@/utils/odakSiparisCustomerService';
import {
  fetchCustomerDashboardMetrics,
  type OdakCustomerDashboardMetrics,
} from '@/utils/odakSiparisDashboardService';
import { formatOdakDate, packageDataId } from '@/utils/odakSiparisService';

const props = defineProps<{
  customerRow: OdakCustomerRow;
}>();

const { t } = useAppI18n();
const router = useRouter();

const loading = ref(false);
const errorMessage = ref('');
const metrics = ref<OdakCustomerDashboardMetrics | null>(null);

const customerId = computed(() => packageDataId(props.customerRow));

const roleChips = computed(() => customerActorRoleChips(props.customerRow));

/** Rol veya kimlik değişince metrikleri yeniden yükle (aynı __dataId ile rol güncellemesi dahil). */
const reloadSignature = computed(
  () =>
    [
      customerId.value,
      props.customerRow?.isMusteri,
      props.customerRow?.isTedarikci,
      props.customerRow?.aktif,
      props.customerRow?.kod,
      props.customerRow?.unvan,
    ].join('|')
);

async function loadMetrics() {
  const id = customerId.value;
  errorMessage.value = '';
  if (!id) {
    metrics.value = null;
    return;
  }
  loading.value = true;
  metrics.value = null;
  try {
    metrics.value = await fetchCustomerDashboardMetrics(id, props.customerRow);
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    metrics.value = null;
  } finally {
    loading.value = false;
  }
}

watch(
  reloadSignature,
  () => {
    void loadMetrics();
  },
  { immediate: true }
);

function openAllPackages() {
  const id = customerId.value;
  if (!id) return;
  void router.push(packagesByCustomerRoute(id));
}

function packageLink(packageId: string) {
  return `/apps/odak-siparis/packages?expand=${encodeURIComponent(packageId)}&tab=dashboard`;
}

function urgencyChip(daysLeft: number | null) {
  if (daysLeft == null) return null;
  if (daysLeft < 0) return { color: 'error' as const, text: t('odakSiparis.dashboard.global.overdue') };
  if (daysLeft <= 7) return { color: 'warning' as const, text: t('odakSiparis.dashboard.global.dueSoon') };
  return { color: 'success' as const, text: t('odakSiparis.dashboard.global.onTrack') };
}
</script>

<template>
  <div class="odak-customer-dashboard">
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-alert v-if="errorMessage" type="error" variant="tonal" density="compact" class="mb-4">
      {{ errorMessage }}
    </v-alert>

    <v-alert
      v-else-if="!loading && !metrics && !customerId"
      type="warning"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ t('odakSiparis.dashboard.customer.missingIdHint') }}
    </v-alert>

    <template v-if="metrics">
      <v-sheet class="odak-customer-dashboard__hero pa-4 pa-md-5 mb-4 rounded-lg" elevation="0">
        <div class="d-flex flex-wrap align-center justify-space-between ga-3">
          <div class="min-w-0">
            <div class="text-overline text-medium-emphasis mb-1">
              {{ t('odakSiparis.dashboard.customer.heroEyebrow') }}
            </div>
            <div class="text-h6 font-weight-bold">{{ metrics.kod }} · {{ metrics.unvan }}</div>
            <div class="text-body-2 text-medium-emphasis mt-1">
              {{ metrics.sektorLabel }}
            </div>
          </div>
          <div class="d-flex flex-wrap align-center ga-2">
            <v-chip
              v-for="role in roleChips"
              :key="role"
              size="small"
              :color="role === 'musteri' ? 'primary' : 'warning'"
              variant="tonal"
            >
              {{ role === 'musteri' ? t('odakSiparis.customers.roleMusteri') : t('odakSiparis.customers.roleTedarikci') }}
            </v-chip>
            <v-chip size="small" :color="metrics.isActive ? 'success' : 'error'" variant="flat">
              {{ metrics.isActive ? t('odakSiparis.customers.activeYes') : t('odakSiparis.customers.activeNo') }}
            </v-chip>
          </div>
        </div>
      </v-sheet>

      <v-alert
        v-if="!metrics.isCustomer"
        type="info"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        {{ t('odakSiparis.dashboard.customer.supplierOnlyHint') }}
      </v-alert>

      <v-row v-if="metrics.isCustomer" dense class="mb-4">
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customer.stats.openPackages')"
            :value="metrics.packageOpen"
            icon="mdi-folder-open-outline"
            color="primary"
          />
        </v-col>
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customer.stats.closedPackages')"
            :value="metrics.packageClosed"
            icon="mdi-folder-check-outline"
            color="secondary"
          />
        </v-col>
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customer.stats.dueSoon')"
            :value="metrics.dueSoonCount"
            icon="mdi-calendar-clock"
            :color="metrics.dueSoonCount > 0 ? 'warning' : 'success'"
          />
        </v-col>
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customer.stats.overdue')"
            :value="metrics.overdueCount"
            icon="mdi-alert-outline"
            :color="metrics.overdueCount > 0 ? 'error' : 'success'"
          />
        </v-col>
      </v-row>

      <v-row dense class="mb-4">
        <v-col cols="12" sm="4">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customer.stats.contacts')"
            :value="metrics.contactCount"
            icon="mdi-account-multiple-outline"
            color="info"
          />
        </v-col>
        <v-col cols="12" sm="4">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customer.stats.qualityReqs')"
            :value="metrics.qualityReqCount"
            icon="mdi-clipboard-list-outline"
            color="primary"
          />
        </v-col>
        <template v-if="metrics.isCustomer">
          <v-col cols="6" sm="2">
            <OdakSiparisDashboardStatCard
              :label="t('odakSiparis.dashboard.customer.stats.openNcr')"
              :value="metrics.openNcrCount"
              icon="mdi-alert-circle-outline"
              :color="metrics.openNcrCount > 0 ? 'warning' : 'success'"
            />
          </v-col>
          <v-col cols="6" sm="2">
            <OdakSiparisDashboardStatCard
              :label="t('odakSiparis.dashboard.customer.stats.openCapa')"
              :value="metrics.openCapaCount"
              icon="mdi-clipboard-check-outline"
              :color="metrics.openCapaCount > 0 ? 'error' : 'success'"
            />
          </v-col>
        </template>
      </v-row>

      <v-card v-if="metrics.isCustomer" rounded="lg" variant="outlined" class="mb-4">
        <v-card-title class="d-flex flex-wrap align-center ga-2 py-3 px-4">
          <span class="text-subtitle-2 font-weight-medium">
            {{ t('odakSiparis.dashboard.customer.recentPackagesTitle') }}
          </span>
          <v-spacer />
          <v-btn size="small" variant="tonal" color="primary" @click="openAllPackages">
            {{ t('odakSiparis.customers.openPackages') }}
          </v-btn>
        </v-card-title>
        <v-divider />
        <v-table density="comfortable">
          <thead>
            <tr>
              <th>{{ t('odakSiparis.packages.columns.packageNo') }}</th>
              <th>{{ t('odakSiparis.packages.columns.name') }}</th>
              <th>{{ t('odakSiparis.packages.columns.status') }}</th>
              <th>{{ t('odakSiparis.packages.columns.deliveryDate') }}</th>
              <th>{{ t('odakSiparis.dashboard.global.urgency') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!metrics.recentPackages.length">
              <td colspan="5" class="text-center text-medium-emphasis py-5">
                {{ t('odakSiparis.dashboard.customer.noOpenPackages') }}
              </td>
            </tr>
            <tr v-for="row in metrics.recentPackages" :key="row.packageId">
              <td>
                <NuxtLink :to="packageLink(row.packageId)" class="text-primary text-decoration-none font-weight-medium">
                  {{ row.packageNo }}
                </NuxtLink>
              </td>
              <td>{{ row.name }}</td>
              <td>{{ row.statusLabel }}</td>
              <td>{{ formatOdakDate(row.deliveryDate) }}</td>
              <td>
                <v-chip
                  v-if="urgencyChip(row.daysLeft)"
                  size="x-small"
                  :color="urgencyChip(row.daysLeft)!.color"
                  variant="tonal"
                >
                  {{ urgencyChip(row.daysLeft)!.text }}
                </v-chip>
                <span v-else>—</span>
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>
    </template>
  </div>
</template>

<style scoped>
.odak-customer-dashboard__hero {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-info), 0.1) 0%,
    rgba(var(--v-theme-surface-variant), 0.4) 100%
  );
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
