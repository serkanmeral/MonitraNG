<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import OdakSiparisDashboardStatCard from '@/components/apps/odak-siparis/OdakSiparisDashboardStatCard.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakPackageRow } from '@/utils/odakSiparisConfig';
import {
  fetchPackageDashboardMetrics,
  type OdakPackageDashboardMetrics,
} from '@/utils/odakSiparisDashboardService';
import { formatOdakDate } from '@/utils/odakSiparisService';

const props = defineProps<{
  packageId: string;
  packageRow: OdakPackageRow;
  customerLabels: Record<string, string>;
}>();

const emit = defineEmits<{
  navigate: [tab: 'lines' | 'shipments' | 'quality' | 'documents'];
}>();

const { t } = useAppI18n();
const loading = ref(false);
const metrics = ref<OdakPackageDashboardMetrics | null>(null);

const urgencyColor = computed(() => {
  const u = metrics.value?.deliveryUrgency;
  if (u === 'overdue') return 'error';
  if (u === 'soon') return 'warning';
  if (u === 'ok') return 'success';
  return 'secondary';
});

const urgencyLabel = computed(() => {
  const m = metrics.value;
  if (!m || m.deliveryUrgency === 'none') return t('odakSiparis.dashboard.package.noDeliveryDate');
  if (m.daysToDelivery == null) return '—';
  if (m.daysToDelivery < 0) {
    return t('odakSiparis.dashboard.package.overdueDays', { days: Math.abs(m.daysToDelivery) });
  }
  if (m.daysToDelivery === 0) return t('odakSiparis.dashboard.package.dueToday');
  return t('odakSiparis.dashboard.package.daysLeft', { days: m.daysToDelivery });
});

const fulfillmentColor = computed(() => {
  const p = metrics.value?.fulfillmentPct;
  if (p == null) return 'primary';
  if (p >= 90) return 'success';
  if (p >= 50) return 'info';
  if (p >= 25) return 'warning';
  return 'error';
});

async function loadMetrics() {
  if (!props.packageId) return;
  loading.value = true;
  try {
    metrics.value = await fetchPackageDashboardMetrics(
      props.packageId,
      props.packageRow,
      props.customerLabels
    );
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadMetrics();
});

watch(
  () => props.packageId,
  () => {
    void loadMetrics();
  }
);
</script>

<template>
  <div class="odak-package-dashboard">
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <template v-if="metrics">
      <v-sheet class="odak-package-dashboard__hero pa-4 pa-md-5 mb-4 rounded-lg" elevation="0">
        <div class="d-flex flex-wrap align-center justify-space-between ga-3">
          <div class="min-w-0">
            <div class="text-overline text-medium-emphasis mb-1">
              {{ t('odakSiparis.dashboard.package.heroEyebrow') }}
            </div>
            <div class="text-h6 font-weight-bold text-truncate">
              {{ metrics.packageNo }} · {{ metrics.name }}
            </div>
            <div class="text-body-2 text-medium-emphasis mt-1">
              {{ metrics.customerLabel }}
            </div>
          </div>
          <div class="d-flex flex-wrap align-center ga-2">
            <v-chip size="small" :color="metrics.status === 'closed' ? 'secondary' : 'primary'" variant="tonal">
              {{ metrics.statusLabel }}
            </v-chip>
            <v-chip v-if="metrics.deliveryUrgency !== 'none'" size="small" :color="urgencyColor" variant="flat">
              {{ urgencyLabel }}
            </v-chip>
          </div>
        </div>
      </v-sheet>

      <v-row dense class="mb-4">
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.package.stats.lines')"
            :value="metrics.lineCount"
            icon="mdi-format-list-bulleted"
            color="primary"
          />
        </v-col>
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.package.stats.shipments')"
            :value="`${metrics.shipmentCompleted}/${metrics.shipmentTotal}`"
            :hint="t('odakSiparis.dashboard.package.stats.shipmentsHint')"
            icon="mdi-truck-delivery-outline"
            color="info"
          />
        </v-col>
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.package.stats.openNcr')"
            :value="metrics.openNcrCount"
            icon="mdi-alert-circle-outline"
            :color="metrics.openNcrCount > 0 ? 'warning' : 'success'"
          />
        </v-col>
        <v-col cols="6" sm="3">
          <OdakSiparisDashboardStatCard
            :label="t('odakSiparis.dashboard.package.stats.openCapa')"
            :value="metrics.openCapaCount"
            icon="mdi-clipboard-check-outline"
            :color="metrics.openCapaCount > 0 ? 'error' : 'success'"
          />
        </v-col>
      </v-row>

      <v-row dense>
        <v-col cols="12" md="7">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-3">
              {{ t('odakSiparis.dashboard.package.fulfillmentTitle') }}
            </div>
            <div class="d-flex flex-wrap ga-4 mb-3">
              <div>
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.detail.fields.partCount') }}</div>
                <div class="text-h6 font-weight-bold">{{ metrics.partCount || '—' }}</div>
              </div>
              <div>
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.detail.fields.shippedCount') }}</div>
                <div class="text-h6 font-weight-bold">{{ metrics.shippedCount || '—' }}</div>
              </div>
              <div>
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.detail.fields.stockCount') }}</div>
                <div class="text-h6 font-weight-bold">{{ metrics.stockCount || '—' }}</div>
              </div>
            </div>
            <v-progress-linear
              v-if="metrics.fulfillmentPct != null"
              :model-value="metrics.fulfillmentPct"
              :color="fulfillmentColor"
              height="10"
              rounded
            />
            <div v-if="metrics.fulfillmentPct != null" class="text-caption text-medium-emphasis mt-2">
              {{ t('odakSiparis.dashboard.package.fulfillmentPct', { pct: metrics.fulfillmentPct }) }}
            </div>
            <div v-else class="text-caption text-medium-emphasis">
              {{ t('odakSiparis.dashboard.package.fulfillmentUnavailable') }}
            </div>
          </v-card>
        </v-col>

        <v-col cols="12" md="5">
          <v-card rounded="lg" variant="outlined" class="pa-4 h-100">
            <div class="text-subtitle-2 font-weight-medium mb-3">
              {{ t('odakSiparis.dashboard.package.timelineTitle') }}
            </div>
            <v-timeline side="end" density="compact" truncate-line="both">
              <v-timeline-item dot-color="primary" size="x-small">
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.detail.fields.beginDate') }}</div>
                <div class="text-body-2">{{ formatOdakDate(metrics.beginDate) }}</div>
              </v-timeline-item>
              <v-timeline-item :dot-color="urgencyColor" size="x-small">
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.detail.fields.deliveryDate') }}</div>
                <div class="text-body-2">{{ formatOdakDate(metrics.deliveryDate) }}</div>
              </v-timeline-item>
              <v-timeline-item v-if="metrics.closedAt" dot-color="secondary" size="x-small">
                <div class="text-caption text-medium-emphasis">{{ t('odakSiparis.packages.fieldPolicyFields.closedAt') }}</div>
                <div class="text-body-2">{{ formatOdakDate(metrics.closedAt) }}</div>
              </v-timeline-item>
            </v-timeline>
          </v-card>
        </v-col>
      </v-row>

      <div class="d-flex flex-wrap ga-2 mt-4">
        <v-btn size="small" variant="tonal" color="primary" @click="emit('navigate', 'lines')">
          {{ t('odakSiparis.detail.tabs.lines') }}
        </v-btn>
        <v-btn size="small" variant="tonal" color="info" @click="emit('navigate', 'shipments')">
          {{ t('odakSiparis.detail.tabs.shipments') }}
        </v-btn>
        <v-btn size="small" variant="tonal" color="warning" @click="emit('navigate', 'quality')">
          {{ t('odakSiparis.detail.tabs.quality') }}
        </v-btn>
        <v-btn size="small" variant="tonal" @click="emit('navigate', 'documents')">
          {{ t('odakSiparis.detail.tabs.documents') }}
        </v-btn>
      </div>
    </template>
  </div>
</template>

<style scoped>
.odak-package-dashboard__hero {
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.08) 0%,
    rgba(var(--v-theme-surface-variant), 0.45) 100%
  );
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
