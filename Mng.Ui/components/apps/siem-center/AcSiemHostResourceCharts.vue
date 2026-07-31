<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { formatBytes, type DiscoveryHostMetricsSnapshot } from '@/composables/useSiemDiscoveryHostMetrics';
import { getPrimary } from '@/utils/UpdateColors';

const props = defineProps<{
  metrics: DiscoveryHostMetricsSnapshot;
  loading?: boolean;
}>();

const { t, locale } = useAppI18n();
const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

/** High-contrast burnt orange — theme secondary is often too light on white charts. */
const MEMORY_SERIES_COLOR = '#C2410C';

const DISK_SERIES_COLORS = ['#5D87FF', '#C2410C', '#0F766E', '#B45309', '#7C3AED'];

function formatLabel(at: number): string {
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      hour: '2-digit',
      minute: '2-digit',
      day: '2-digit',
      month: '2-digit',
    }).format(new Date(at));
  } catch {
    return new Date(at).toISOString();
  }
}

const hasCpuMem = computed(
  () => props.metrics.cpuSeries.length >= 2 || props.metrics.memorySeries.length >= 2,
);

const hasDisk = computed(() => props.metrics.diskSeries.some((d) => d.series.length >= 2));

const cpuMemCategories = computed(() => {
  const times = new Set<number>();
  for (const p of props.metrics.cpuSeries) times.add(p.at);
  for (const p of props.metrics.memorySeries) times.add(p.at);
  return [...times].sort((a, b) => a - b);
});

const cpuMemChartOptions = computed(() => ({
  chart: {
    type: 'line',
    height: 280,
    fontFamily: 'inherit',
    foreColor: 'rgba(var(--v-theme-on-surface), 0.55)',
    toolbar: { show: false },
    zoom: { enabled: false },
  },
  colors: [getPrimary.value, MEMORY_SERIES_COLOR],
  dataLabels: { enabled: false },
  stroke: { curve: 'smooth', width: [2, 3] },
  fill: {
    type: ['gradient', 'solid'],
    opacity: [0.28, 0],
    gradient: { shadeIntensity: 0.4, opacityFrom: 0.35, opacityTo: 0.05 },
  },
  markers: {
    size: [0, 3],
    strokeWidth: 0,
    hover: { sizeOffset: 2 },
  },
  grid: {
    borderColor: 'rgba(var(--v-border-color), 0.35)',
    strokeDashArray: 4,
  },
  xaxis: {
    categories: cpuMemCategories.value.map(formatLabel),
    labels: { rotate: -35, hideOverlappingLabels: true, style: { fontSize: '10px' } },
    axisBorder: { show: false },
    axisTicks: { show: false },
  },
  yaxis: [
    {
      seriesName: t('siemCenter.hostDashboard.chartCpuSeries'),
      title: { text: t('siemCenter.hostDashboard.chartCpuAxis') },
      min: 0,
      max: 100,
      labels: { formatter: (v: number) => `${Math.round(v)}%` },
    },
    {
      seriesName: t('siemCenter.hostDashboard.chartMemSeries'),
      opposite: true,
      title: {
        text: t('siemCenter.hostDashboard.chartMemAxis'),
        style: { color: MEMORY_SERIES_COLOR },
      },
      labels: {
        formatter: (v: number) => formatBytes(v, dateLocale.value),
        style: { colors: [MEMORY_SERIES_COLOR] },
      },
    },
  ],
  legend: { position: 'top' },
  tooltip: { theme: 'dark', shared: true },
}));

const cpuMemSeries = computed(() => {
  const cats = cpuMemCategories.value;
  const cpuMap = new Map(props.metrics.cpuSeries.map((p) => [p.at, p.value]));
  const memMap = new Map(props.metrics.memorySeries.map((p) => [p.at, p.value]));
  return [
    {
      name: t('siemCenter.hostDashboard.chartCpuSeries'),
      type: 'area',
      data: cats.map((at) => cpuMap.get(at) ?? null),
    },
    {
      name: t('siemCenter.hostDashboard.chartMemSeries'),
      type: 'line',
      data: cats.map((at) => memMap.get(at) ?? null),
    },
  ];
});

const diskChartOptions = computed(() => {
  const volumes = props.metrics.diskSeries.filter((d) => d.series.length >= 2);
  const allTimes = new Set<number>();
  for (const d of volumes) for (const p of d.series) allTimes.add(p.at);
  const cats = [...allTimes].sort((a, b) => a - b);
  return {
    chart: {
      type: 'line',
      height: 280,
      fontFamily: 'inherit',
      foreColor: 'rgba(var(--v-theme-on-surface), 0.55)',
      toolbar: { show: false },
      zoom: { enabled: false },
    },
    colors: DISK_SERIES_COLORS,
    dataLabels: { enabled: false },
    stroke: { curve: 'smooth', width: 2 },
    grid: {
      borderColor: 'rgba(var(--v-border-color), 0.35)',
      strokeDashArray: 4,
    },
    xaxis: {
      categories: cats.map(formatLabel),
      labels: { rotate: -35, hideOverlappingLabels: true, style: { fontSize: '10px' } },
      axisBorder: { show: false },
      axisTicks: { show: false },
    },
    yaxis: {
      min: 0,
      max: 100,
      title: { text: t('siemCenter.hostDashboard.chartDiskAxis') },
      labels: { formatter: (v: number) => `${Math.round(v)}%` },
    },
    legend: { position: 'top' },
    tooltip: { theme: 'dark', shared: true },
    _cats: cats,
    _volumes: volumes,
  };
});

const diskSeries = computed(() => {
  const opts = diskChartOptions.value as {
    _cats: number[];
    _volumes: DiscoveryHostMetricsSnapshot['diskSeries'];
  };
  return opts._volumes.map((d) => {
    const map = new Map(d.series.map((p) => [p.at, p.value]));
    return {
      name: d.volume,
      data: opts._cats.map((at) => map.get(at) ?? null),
    };
  });
});
</script>

<template>
  <v-row dense>
    <v-col cols="12" lg="7">
      <v-card variant="outlined" class="rounded-lg pa-4 h-100">
        <h3 class="text-subtitle-1 font-weight-bold mb-1">
          {{ t('siemCenter.hostDashboard.chartCpuMemTitle') }}
        </h3>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ t('siemCenter.hostDashboard.chartCpuMemHint') }}
        </p>
        <v-skeleton-loader v-if="loading" type="image" height="280" />
        <div v-else-if="!hasCpuMem" class="host-chart-empty">
          {{ t('siemCenter.hostDashboard.chartEmpty') }}
        </div>
        <ClientOnly v-else>
          <apexchart
            type="line"
            height="280"
            :options="cpuMemChartOptions"
            :series="cpuMemSeries"
          />
        </ClientOnly>
      </v-card>
    </v-col>
    <v-col cols="12" lg="5">
      <v-card variant="outlined" class="rounded-lg pa-4 h-100">
        <h3 class="text-subtitle-1 font-weight-bold mb-1">
          {{ t('siemCenter.hostDashboard.chartDiskTitle') }}
        </h3>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ t('siemCenter.hostDashboard.chartDiskHint') }}
        </p>
        <v-skeleton-loader v-if="loading" type="image" height="280" />
        <div v-else-if="!hasDisk" class="host-chart-empty">
          {{ t('siemCenter.hostDashboard.chartEmpty') }}
        </div>
        <ClientOnly v-else>
          <apexchart
            type="line"
            height="280"
            :options="diskChartOptions"
            :series="diskSeries"
          />
        </ClientOnly>
      </v-card>
    </v-col>
  </v-row>
</template>

<style scoped>
.host-chart-empty {
  min-height: 280px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.875rem;
  text-align: center;
  padding: 1rem;
}
</style>
