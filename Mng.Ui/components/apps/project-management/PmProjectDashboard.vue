<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePmDate } from '@/composables/usePmDate';
import { getPrimary, getSecondary } from '@/utils/UpdateColors';
import type { PmProjectPulse } from '@/types/apps/projectManagement';

const props = defineProps<{
  pulse: PmProjectPulse | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  navigate: [tab: string];
}>();

const { t } = useAppI18n();
const { formatPmDateRange } = usePmDate();

const COLOR = {
  success: '#13DEB9',
  error: '#FA896B',
  warning: '#FFAE1F',
  info: '#539BFF',
  muted: '#a1aab2',
};

const healthColor = computed(() => {
  const health = props.pulse?.health;
  if (health === 'alert') return 'error';
  if (health === 'watch') return 'warning';
  return 'success';
});

const healthLabel = computed(() => {
  const health = props.pulse?.health || 'ok';
  const key = `projectManagement.dashboard.health.${health}`;
  const label = t(key);
  return label === key ? health : label;
});

const dateRange = computed(() =>
  formatPmDateRange(props.pulse?.plannedStart, props.pulse?.plannedFinish),
);

const daysHint = computed(() => {
  const days = props.pulse?.daysToFinish;
  if (days == null) return t('projectManagement.dashboard.noFinishDate');
  if (days < 0) return t('projectManagement.dashboard.overdueDays', { n: Math.abs(days) });
  if (days === 0) return t('projectManagement.dashboard.dueToday');
  return t('projectManagement.dashboard.daysLeft', { n: days });
});

const nextGateTitle = computed(() => {
  const gate = props.pulse?.nextGate;
  if (!gate) return t('projectManagement.dashboard.noOpenGate');
  const statusKey = `projectManagement.stageGate.status.${gate.status || 'open'}`;
  const status = t(statusKey);
  return `${gate.name} · ${status === statusKey ? gate.status : status}`;
});

const delayedCount = computed(() => props.pulse?.counts?.delayed ?? 0);

const wbsSeries = computed(() => {
  const w = props.pulse?.charts?.wbs;
  return [
    w?.done ?? 0,
    w?.inProgress ?? 0,
    w?.delayed ?? 0,
    w?.unbound ?? 0,
    w?.notStarted ?? 0,
  ];
});

const hasWbsChart = computed(() => wbsSeries.value.some((n) => n > 0));

const wbsLabels = computed(() => [
  t('projectManagement.dashboard.wbs.done'),
  t('projectManagement.dashboard.wbs.inProgress'),
  t('projectManagement.dashboard.wbs.delayed'),
  t('projectManagement.dashboard.wbs.unbound'),
  t('projectManagement.dashboard.wbs.notStarted'),
]);

const gateBuckets = computed(() => props.pulse?.charts?.gates ?? []);
const hasGateChart = computed(() => gateBuckets.value.some((b) => (b.count ?? 0) > 0));
const raidBuckets = computed(() => props.pulse?.charts?.raid ?? []);
const hasRaidChart = computed(() => raidBuckets.value.some((b) => (b.count ?? 0) > 0));
const budget = computed(() => props.pulse?.charts?.budget);
const hasBudgetChart = computed(
  () => (budget.value?.planned ?? 0) > 0 || (budget.value?.actual ?? 0) > 0,
);

function gateLabel(key: string) {
  const i18nKey = `projectManagement.stageGate.status.${key}`;
  const label = t(i18nKey);
  return label === i18nKey ? key : label;
}

function raidLabel(key: string) {
  const i18nKey = `projectManagement.raid.kind.${key}`;
  const label = t(i18nKey);
  return label === i18nKey ? key : label;
}

const chartBase = computed(() => ({
  fontFamily: 'inherit',
  foreColor: COLOR.muted,
  toolbar: { show: false },
}));

const percentOptions = computed(() => ({
  chart: { ...chartBase.value, type: 'radialBar', sparkline: { enabled: true } },
  colors: [props.pulse?.health === 'alert' ? COLOR.error : getPrimary.value],
  plotOptions: {
    radialBar: {
      hollow: { size: '62%' },
      dataLabels: {
        name: { show: false },
        value: {
          fontSize: '18px',
          fontWeight: 700,
          offsetY: 6,
          formatter: (v: number) => `${Math.round(v)}%`,
        },
      },
    },
  },
  stroke: { lineCap: 'round' },
}));

const percentSeries = computed(() => [Math.round(props.pulse?.percentComplete || 0)]);

const wbsOptions = computed(() => ({
  chart: {
    ...chartBase.value,
    type: 'donut',
    events: {
      dataPointSelection: (_e: unknown, _ctx: unknown, cfg: { dataPointIndex: number }) => {
        const tabs = ['wbs', 'wbs', 'status', 'wbs', 'wbs'];
        const tab = tabs[cfg.dataPointIndex];
        if (tab) emit('navigate', tab);
      },
    },
  },
  labels: wbsLabels.value,
  colors: [COLOR.success, getPrimary.value, COLOR.error, COLOR.warning, COLOR.muted],
  legend: { position: 'bottom', fontSize: '12px' },
  dataLabels: { enabled: false },
  stroke: { width: 0 },
  plotOptions: { pie: { donut: { size: '68%' } } },
  tooltip: { theme: 'dark', y: { formatter: (v: number) => String(v) } },
}));

const gateOptions = computed(() => ({
  chart: {
    ...chartBase.value,
    type: 'bar',
    events: {
      dataPointSelection: () => emit('navigate', 'gates'),
    },
  },
  plotOptions: { bar: { horizontal: true, distributed: true, borderRadius: 6, barHeight: '58%' } },
  colors: [COLOR.info, COLOR.success, COLOR.error, COLOR.warning],
  xaxis: { categories: gateBuckets.value.map((b) => gateLabel(b.key)) },
  dataLabels: { enabled: true },
  grid: { borderColor: 'rgba(var(--v-border-color), 0.35)', strokeDashArray: 4 },
  tooltip: { theme: 'dark' },
  legend: { show: false },
}));

const gateSeries = computed(() => [
  {
    name: t('projectManagement.dashboard.gatesTitle'),
    data: gateBuckets.value.map((b) => b.count ?? 0),
  },
]);

const raidOptions = computed(() => ({
  chart: {
    ...chartBase.value,
    type: 'donut',
    events: {
      dataPointSelection: () => emit('navigate', 'raid'),
    },
  },
  labels: raidBuckets.value.map((b) => raidLabel(b.key)),
  colors: [COLOR.error, COLOR.warning, getSecondary.value, getPrimary.value],
  legend: { position: 'bottom', fontSize: '12px' },
  dataLabels: { enabled: false },
  stroke: { width: 0 },
  plotOptions: { pie: { donut: { size: '68%' } } },
  tooltip: { theme: 'dark', y: { formatter: (v: number) => String(v) } },
}));

const raidSeries = computed(() => raidBuckets.value.map((b) => b.count ?? 0));

const budgetOptions = computed(() => ({
  chart: {
    ...chartBase.value,
    type: 'bar',
    events: {
      dataPointSelection: () => emit('navigate', 'budget'),
    },
  },
  plotOptions: { bar: { distributed: true, columnWidth: '42%', borderRadius: 6 } },
  colors: [getPrimary.value, COLOR.warning],
  xaxis: {
    categories: [
      t('projectManagement.dashboard.budget.planned'),
      t('projectManagement.dashboard.budget.actual'),
    ],
  },
  dataLabels: { enabled: false },
  grid: { borderColor: 'rgba(var(--v-border-color), 0.35)', strokeDashArray: 4 },
  tooltip: {
    theme: 'dark',
    y: {
      formatter: (v: number) =>
        `${v.toLocaleString()} ${budget.value?.currency || 'TRY'}`,
    },
  },
  legend: { show: false },
}));

const budgetSeries = computed(() => [
  {
    name: budget.value?.currency || 'TRY',
    data: [budget.value?.planned ?? 0, budget.value?.actual ?? 0],
  },
]);

function go(tab: string) {
  emit('navigate', tab);
}
</script>

<template>
  <div class="pm-dash">
    <p class="text-body-2 text-medium-emphasis mb-4">{{ t('projectManagement.dashboard.hint') }}</p>

    <v-skeleton-loader v-if="loading && !pulse" type="article, image, image" />

    <template v-else-if="pulse">
      <v-row dense class="mb-1">
        <v-col cols="12" sm="6" lg="3">
          <v-card
            class="pm-dash__kpi rounded-lg h-100"
            :class="`pm-dash__kpi--${healthColor}`"
            variant="flat"
          >
            <v-card-text>
              <div class="text-overline text-medium-emphasis">{{ t('projectManagement.dashboard.healthLabel') }}</div>
              <div class="text-h5 font-weight-bold mt-1">{{ healthLabel }}</div>
              <div class="text-caption text-medium-emphasis mt-1">{{ dateRange || '—' }}</div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" lg="3">
          <v-card class="pm-dash__kpi rounded-lg h-100 pm-dash__kpi--primary" variant="flat">
            <v-card-text class="d-flex align-center justify-space-between">
              <div>
                <div class="text-overline text-medium-emphasis">{{ t('projectManagement.dashboard.progress') }}</div>
                <div class="text-caption text-medium-emphasis mt-2">
                  {{ t('projectManagement.portfolio.percent') }}
                </div>
              </div>
              <ClientOnly>
                <apexchart type="radialBar" width="96" height="96" :options="percentOptions" :series="percentSeries" />
              </ClientOnly>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" lg="3">
          <v-card
            class="pm-dash__kpi rounded-lg h-100 cursor-pointer"
            :class="delayedCount ? 'pm-dash__kpi--error' : 'pm-dash__kpi--success'"
            variant="flat"
            role="button"
            @click="go('status')"
          >
            <v-card-text>
              <div class="text-overline text-medium-emphasis">{{ t('projectManagement.statusPack.flag.delayed') }}</div>
              <div class="text-h4 font-weight-bold mt-1">{{ delayedCount }}</div>
              <div class="text-caption text-medium-emphasis mt-1">{{ t('projectManagement.dashboard.clickStatus') }}</div>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" lg="3">
          <v-card
            class="pm-dash__kpi rounded-lg h-100 pm-dash__kpi--info cursor-pointer"
            variant="flat"
            role="button"
            @click="go('gates')"
          >
            <v-card-text>
              <div class="text-overline text-medium-emphasis">{{ t('projectManagement.dashboard.nextGate') }}</div>
              <div class="text-subtitle-1 font-weight-bold mt-1 text-truncate">{{ nextGateTitle }}</div>
              <div class="text-caption text-medium-emphasis mt-1">{{ daysHint }}</div>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <v-row dense>
        <v-col cols="12" md="6">
          <v-card class="rounded-lg h-100 pm-dash__chart" variant="outlined">
            <v-card-text>
              <div class="text-subtitle-2 font-weight-medium mb-2">{{ t('projectManagement.dashboard.wbsTitle') }}</div>
              <div v-if="!hasWbsChart" class="pm-dash__empty">{{ t('projectManagement.dashboard.chartEmpty') }}</div>
              <ClientOnly v-else>
                <apexchart type="donut" height="280" :options="wbsOptions" :series="wbsSeries" />
              </ClientOnly>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card class="rounded-lg h-100 pm-dash__chart" variant="outlined">
            <v-card-text>
              <div class="text-subtitle-2 font-weight-medium mb-2">{{ t('projectManagement.dashboard.gatesTitle') }}</div>
              <div v-if="!hasGateChart" class="pm-dash__empty">{{ t('projectManagement.dashboard.chartEmpty') }}</div>
              <ClientOnly v-else>
                <apexchart type="bar" height="280" :options="gateOptions" :series="gateSeries" />
              </ClientOnly>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card class="rounded-lg h-100 pm-dash__chart" variant="outlined">
            <v-card-text>
              <div class="text-subtitle-2 font-weight-medium mb-2">{{ t('projectManagement.dashboard.raidTitle') }}</div>
              <div v-if="!hasRaidChart" class="pm-dash__empty">{{ t('projectManagement.dashboard.chartEmpty') }}</div>
              <ClientOnly v-else>
                <apexchart type="donut" height="280" :options="raidOptions" :series="raidSeries" />
              </ClientOnly>
            </v-card-text>
          </v-card>
        </v-col>
        <v-col cols="12" md="6">
          <v-card class="rounded-lg h-100 pm-dash__chart" variant="outlined">
            <v-card-text>
              <div class="text-subtitle-2 font-weight-medium mb-2">{{ t('projectManagement.dashboard.budgetTitle') }}</div>
              <div v-if="!hasBudgetChart" class="pm-dash__empty">{{ t('projectManagement.dashboard.budgetEmpty') }}</div>
              <ClientOnly v-else>
                <apexchart type="bar" height="280" :options="budgetOptions" :series="budgetSeries" />
              </ClientOnly>
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>

      <div class="mt-4">
        <v-btn variant="tonal" color="primary" @click="go('status')">
          {{ t('projectManagement.dashboard.openStatus') }}
        </v-btn>
      </div>
    </template>
  </div>
</template>

<style scoped>
.pm-dash__kpi {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  min-height: 118px;
}
.pm-dash__kpi--primary {
  border-top: 3px solid rgb(var(--v-theme-primary));
}
.pm-dash__kpi--success {
  border-top: 3px solid rgb(var(--v-theme-success));
}
.pm-dash__kpi--warning {
  border-top: 3px solid rgb(var(--v-theme-warning));
}
.pm-dash__kpi--error {
  border-top: 3px solid rgb(var(--v-theme-error));
}
.pm-dash__kpi--info {
  border-top: 3px solid rgb(var(--v-theme-info));
}
.pm-dash__chart {
  background: rgb(var(--v-theme-surface));
}
.pm-dash__empty {
  min-height: 220px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.875rem;
}
.cursor-pointer {
  cursor: pointer;
}
</style>
