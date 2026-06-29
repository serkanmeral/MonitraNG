<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import OdakSiparisDashboardStatCard from '@/components/apps/odak-siparis/OdakSiparisDashboardStatCard.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  fetchGlobalCustomerDashboardMetrics,
  type OdakGlobalCustomerDashboardMetrics,
} from '@/utils/odakSiparisDashboardService';

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const loading = ref(false);
const errorMessage = ref('');
const metrics = ref<OdakGlobalCustomerDashboardMetrics | null>(null);

const maxSectorCount = computed(() => {
  const list = metrics.value?.sectorBreakdown ?? [];
  if (!list.length) return 1;
  return Math.max(...list.map((s) => s.count), 1);
});

const maxOpenCount = computed(() => {
  const list = metrics.value?.topCustomers ?? [];
  if (!list.length) return 1;
  return Math.max(...list.map((c) => c.openCount), 1);
});

async function load() {
  loading.value = true;
  errorMessage.value = '';
  try {
    metrics.value = await fetchGlobalCustomerDashboardMetrics();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void load();
});

function customerPackagesRoute(customerId: string) {
  return `/apps/odak-siparis/packages?customerId=${encodeURIComponent(customerId)}`;
}
</script>

<template>
  <div class="odak-global-customer-dashboard">
    <v-sheet class="odak-global-customer-dashboard__hero pa-6 pa-md-8 mb-6 rounded-xl" elevation="0">
      <div class="d-flex flex-wrap align-end justify-space-between ga-4">
        <div>
          <div class="text-overline mb-2 odak-global-customer-dashboard__hero-eyebrow">
            {{ t('odakSiparis.dashboard.customerGlobal.heroEyebrow') }}
          </div>
          <h1 class="text-h4 text-md-h3 font-weight-bold mb-2">
            {{ t('odakSiparis.dashboard.customerGlobal.heroTitle') }}
          </h1>
          <p class="text-body-1 text-medium-emphasis mb-0 odak-global-customer-dashboard__hero-sub">
            {{ t('odakSiparis.dashboard.customerGlobal.heroSubtitle') }}
          </p>
        </div>
        <v-btn variant="tonal" color="primary" :loading="loading" prepend-icon="mdi-refresh" @click="load">
          {{ t('odakSiparis.dashboard.global.refresh') }}
        </v-btn>
      </div>
    </v-sheet>

    <v-alert v-if="errorMessage" type="error" variant="tonal" class="mb-4">{{ errorMessage }}</v-alert>
    <v-progress-linear v-if="loading && !metrics" indeterminate color="primary" class="mb-4" />

    <template v-if="metrics">
      <v-row dense class="mb-6">
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customerGlobal.stats.activeCustomers')"
            :value="metrics.activeCustomers"
            icon="mdi-account-check-outline"
            color="success"
          />
        </v-col>
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customerGlobal.stats.withOpenPackages')"
            :value="metrics.customersWithOpenPackages"
            icon="mdi-account-group-outline"
            color="primary"
          />
        </v-col>
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customerGlobal.stats.openPackages')"
            :value="metrics.totalOpenPackages"
            icon="mdi-briefcase-clock-outline"
            color="info"
          />
        </v-col>
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.customerGlobal.stats.overduePackages')"
            :value="metrics.totalOverduePackages"
            icon="mdi-alert-outline"
            :color="metrics.totalOverduePackages > 0 ? 'error' : 'success'"
          />
        </v-col>
      </v-row>

      <v-row dense class="mb-6">
        <v-col cols="12" md="5">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-4">
              {{ t('odakSiparis.dashboard.customerGlobal.sectorTitle') }}
            </div>
            <div v-if="!metrics.sectorBreakdown.length" class="text-caption text-medium-emphasis">
              {{ t('odakSiparis.dashboard.global.noData') }}
            </div>
            <div v-else class="d-flex flex-column ga-3">
              <div v-for="row in metrics.sectorBreakdown" :key="row.sector">
                <div class="d-flex justify-space-between text-caption mb-1">
                  <span>{{ row.label }}</span>
                  <span class="font-weight-medium">{{ row.count }}</span>
                </div>
                <v-progress-linear
                  :model-value="(row.count / maxSectorCount) * 100"
                  color="info"
                  height="8"
                  rounded
                />
              </div>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" md="7">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-4">
              {{ t('odakSiparis.dashboard.customerGlobal.topCustomersTitle') }}
            </div>
            <div v-if="!metrics.topCustomers.length" class="text-caption text-medium-emphasis">
              {{ t('odakSiparis.dashboard.global.noData') }}
            </div>
            <div v-else class="d-flex flex-column ga-3">
              <div v-for="row in metrics.topCustomers" :key="row.customerId">
                <div class="d-flex flex-wrap justify-space-between align-center ga-2 text-caption mb-1">
                  <NuxtLink
                    :to="customerPackagesRoute(row.customerId)"
                    class="text-primary text-decoration-none font-weight-medium"
                  >
                    {{ row.label }}
                  </NuxtLink>
                  <span>
                    {{ t('odakSiparis.dashboard.customerGlobal.openCountLabel', { count: row.openCount }) }}
                    <span v-if="row.overdueCount > 0" class="text-error">
                      · {{ t('odakSiparis.dashboard.customerGlobal.overdueCountLabel', { count: row.overdueCount }) }}
                    </span>
                  </span>
                </div>
                <v-progress-linear
                  :model-value="(row.openCount / maxOpenCount) * 100"
                  color="primary"
                  height="8"
                  rounded
                />
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <v-card rounded="lg" variant="outlined">
        <v-card-title class="text-subtitle-1 font-weight-medium py-4 px-4">
          {{ t('odakSiparis.dashboard.customerGlobal.atRiskTitle') }}
        </v-card-title>
        <v-divider />
        <v-table density="comfortable">
          <thead>
            <tr>
              <th>{{ t('odakSiparis.customers.fields.unvan') }}</th>
              <th>{{ t('odakSiparis.dashboard.customerGlobal.stats.openPackages') }}</th>
              <th>{{ t('odakSiparis.dashboard.customerGlobal.stats.overduePackages') }}</th>
              <th>{{ t('odakSiparis.packages.columns.actions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!metrics.atRiskCustomers.length">
              <td colspan="4" class="text-center text-medium-emphasis py-6">
                {{ t('odakSiparis.dashboard.customerGlobal.noAtRisk') }}
              </td>
            </tr>
            <tr v-for="row in metrics.atRiskCustomers" :key="row.customerId">
              <td>{{ row.label }}</td>
              <td>{{ row.openCount }}</td>
              <td>
                <v-chip size="x-small" color="error" variant="tonal">{{ row.overdueCount }}</v-chip>
              </td>
              <td>
                <v-btn
                  size="x-small"
                  variant="tonal"
                  color="primary"
                  :to="customerPackagesRoute(row.customerId)"
                >
                  {{ t('odakSiparis.customers.openPackages') }}
                </v-btn>
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>

      <div class="text-caption text-medium-emphasis mt-4">
        {{ t('odakSiparis.dashboard.customerGlobal.inactiveHint', { count: metrics.inactiveCustomers }) }}
      </div>
    </template>
  </div>
</template>

<style scoped>
.odak-global-customer-dashboard__hero {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-info), 0.12) 0%,
    rgba(var(--v-theme-surface), 1) 50%,
    rgba(var(--v-theme-primary), 0.08) 100%
  );
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.odak-global-customer-dashboard__hero-eyebrow {
  color: rgb(var(--v-theme-info));
  letter-spacing: 0.12em;
}

.odak-global-customer-dashboard__hero-sub {
  max-width: 42rem;
}
</style>
