<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import OdakSiparisDashboardStatCard from '@/components/apps/odak-siparis/OdakSiparisDashboardStatCard.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  fetchGlobalDashboardMetrics,
  type OdakGlobalDashboardMetrics,
} from '@/utils/odakSiparisDashboardService';
import { formatOdakDate } from '@/utils/odakSiparisService';

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const loading = ref(false);
const errorMessage = ref('');
const metrics = ref<OdakGlobalDashboardMetrics | null>(null);

const statusTotal = computed(() => {
  const m = metrics.value;
  if (!m) return 0;
  return m.statusBreakdown.open + m.statusBreakdown.closed || 1;
});

const openPct = computed(() =>
  metrics.value ? Math.round((metrics.value.statusBreakdown.open / statusTotal.value) * 100) : 0
);

const closedPct = computed(() =>
  metrics.value ? Math.round((metrics.value.statusBreakdown.closed / statusTotal.value) * 100) : 0
);

const maxCustomerCount = computed(() => {
  const list = metrics.value?.topCustomers ?? [];
  if (!list.length) return 1;
  return Math.max(...list.map((c) => c.count), 1);
});

async function load() {
  loading.value = true;
  errorMessage.value = '';
  try {
    metrics.value = await fetchGlobalDashboardMetrics();
  } catch (e: unknown) {
    errorMessage.value = panelError(e, 'errors.dg.generic');
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void load();
});

function packageLink(packageId: string) {
  return `/apps/odak-siparis/packages?expand=${encodeURIComponent(packageId)}&tab=dashboard`;
}

function urgencyChip(daysLeft: number) {
  if (daysLeft < 0) return { color: 'error' as const, text: t('odakSiparis.dashboard.global.overdue') };
  if (daysLeft <= 7) return { color: 'warning' as const, text: t('odakSiparis.dashboard.global.dueSoon') };
  return { color: 'success' as const, text: t('odakSiparis.dashboard.global.onTrack') };
}
</script>

<template>
  <div class="odak-global-dashboard">
    <v-sheet class="odak-global-dashboard__hero pa-6 pa-md-8 mb-6 rounded-xl" elevation="0">
      <div class="d-flex flex-wrap align-end justify-space-between ga-4">
        <div>
          <div class="text-overline mb-2 odak-global-dashboard__hero-eyebrow">
            {{ t('odakSiparis.dashboard.global.heroEyebrow') }}
          </div>
          <h1 class="text-h4 text-md-h3 font-weight-bold mb-2">
            {{ t('odakSiparis.dashboard.global.heroTitle') }}
          </h1>
          <p class="text-body-1 text-medium-emphasis mb-0 odak-global-dashboard__hero-sub">
            {{ t('odakSiparis.dashboard.global.heroSubtitle') }}
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
            :label="t('odakSiparis.dashboard.global.stats.open')"
            :value="metrics.openCount"
            icon="mdi-folder-open-outline"
            color="primary"
          />
        </v-col>
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.global.stats.closed')"
            :value="metrics.closedCount"
            icon="mdi-folder-check-outline"
            color="secondary"
          />
        </v-col>
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.global.stats.dueSoon')"
            :value="metrics.dueSoonCount"
            icon="mdi-calendar-clock"
            :color="metrics.dueSoonCount > 0 ? 'warning' : 'success'"
          />
        </v-col>
        <v-col cols="6" md="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.global.stats.overdue')"
            :value="metrics.overdueCount"
            icon="mdi-alert-outline"
            :color="metrics.overdueCount > 0 ? 'error' : 'success'"
          />
        </v-col>
      </v-row>

      <v-row dense class="mb-6">
        <v-col cols="12" md="4">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-4">
              {{ t('odakSiparis.dashboard.global.statusMixTitle') }}
            </div>
            <div class="odak-global-dashboard__donut-wrap mb-4">
              <div
                class="odak-global-dashboard__donut"
                :style="{
                  background: `conic-gradient(rgb(var(--v-theme-primary)) 0 ${openPct}%, rgb(var(--v-theme-secondary)) ${openPct}% 100%)`,
                }"
              />
              <div class="odak-global-dashboard__donut-center">
                <div class="text-h5 font-weight-bold">{{ metrics.totalCount }}</div>
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.dashboard.global.total') }}</div>
              </div>
            </div>
            <div class="d-flex justify-space-between text-body-2">
              <span><v-icon size="10" color="primary" class="mr-1">mdi-circle</v-icon>{{ t('odakSiparis.packages.tabs.open') }} {{ openPct }}%</span>
              <span><v-icon size="10" color="secondary" class="mr-1">mdi-circle</v-icon>{{ t('odakSiparis.packages.tabs.closed') }} {{ closedPct }}%</span>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" md="4">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-4">
              {{ t('odakSiparis.dashboard.global.qualityTitle') }}
            </div>
            <div class="d-flex flex-column ga-4">
              <div class="d-flex align-center justify-space-between">
                <span class="text-body-2">{{ t('odakSiparis.dashboard.global.stats.openNcr') }}</span>
                <v-chip size="small" :color="metrics.openNcrCount > 0 ? 'warning' : 'success'" variant="tonal">
                  {{ metrics.openNcrCount }}
                </v-chip>
              </div>
              <div class="d-flex align-center justify-space-between">
                <span class="text-body-2">{{ t('odakSiparis.dashboard.global.stats.openCapa') }}</span>
                <v-chip size="small" :color="metrics.openCapaCount > 0 ? 'error' : 'success'" variant="tonal">
                  {{ metrics.openCapaCount }}
                </v-chip>
              </div>
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" md="4">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-4">
              {{ t('odakSiparis.dashboard.global.topCustomersTitle') }}
            </div>
            <div v-if="!metrics.topCustomers.length" class="text-caption text-medium-emphasis">
              {{ t('odakSiparis.dashboard.global.noData') }}
            </div>
            <div v-else class="odak-global-dashboard__bars d-flex flex-column ga-3">
              <div v-for="row in metrics.topCustomers" :key="row.customerId">
                <div class="d-flex justify-space-between text-caption mb-1">
                  <span class="text-truncate mr-2">{{ row.label }}</span>
                  <span class="font-weight-medium">{{ row.count }}</span>
                </div>
                <v-progress-linear
                  :model-value="(row.count / maxCustomerCount) * 100"
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
          {{ t('odakSiparis.dashboard.global.upcomingTitle') }}
        </v-card-title>
        <v-divider />
        <v-table density="comfortable" class="odak-global-dashboard__table">
          <thead>
            <tr>
              <th>{{ t('odakSiparis.packages.columns.packageNo') }}</th>
              <th>{{ t('odakSiparis.packages.columns.name') }}</th>
              <th>{{ t('odakSiparis.packages.columns.customer') }}</th>
              <th>{{ t('odakSiparis.packages.columns.deliveryDate') }}</th>
              <th>{{ t('odakSiparis.dashboard.global.urgency') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="!metrics.upcomingDeliveries.length">
              <td colspan="5" class="text-medium-emphasis text-center py-6">
                {{ t('odakSiparis.dashboard.global.noUpcoming') }}
              </td>
            </tr>
            <tr v-for="row in metrics.upcomingDeliveries" :key="row.packageId">
              <td>
                <NuxtLink :to="packageLink(row.packageId)" class="text-primary text-decoration-none font-weight-medium">
                  {{ row.packageNo }}
                </NuxtLink>
              </td>
              <td>{{ row.name }}</td>
              <td>{{ row.customerLabel }}</td>
              <td>{{ formatOdakDate(row.deliveryDate) }}</td>
              <td>
                <v-chip size="x-small" :color="urgencyChip(row.daysLeft).color" variant="tonal">
                  {{ urgencyChip(row.daysLeft).text }}
                </v-chip>
              </td>
            </tr>
          </tbody>
        </v-table>
      </v-card>
    </template>
  </div>
</template>

<style scoped>
.odak-global-dashboard__hero {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.14) 0%,
    rgba(var(--v-theme-surface), 1) 45%,
    rgba(var(--v-theme-info), 0.08) 100%
  );
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.odak-global-dashboard__hero-eyebrow {
  color: rgb(var(--v-theme-primary));
  letter-spacing: 0.12em;
}

.odak-global-dashboard__hero-sub {
  max-width: 42rem;
}

.odak-global-dashboard__donut-wrap {
  position: relative;
  width: 160px;
  height: 160px;
  margin: 0 auto;
}

.odak-global-dashboard__donut {
  width: 100%;
  height: 100%;
  border-radius: 50%;
}

.odak-global-dashboard__donut-center {
  position: absolute;
  inset: 18%;
  border-radius: 50%;
  background: rgb(var(--v-theme-surface));
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  box-shadow: inset 0 0 0 1px rgba(var(--v-border-color), var(--v-border-opacity));
}

.odak-global-dashboard__table :deep(th) {
  font-size: 0.75rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.65);
}
</style>
