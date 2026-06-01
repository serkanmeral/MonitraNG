<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type {
  OcBoardCatalogs,
  OcCatalogDisplayEntry,
  OcDashboardWidget,
  OcPersonDisplay,
} from '@/types/apps/operationCore';

const props = defineProps<{
  widget: OcDashboardWidget;
  catalogs: OcBoardCatalogs;
  people: Record<string, OcPersonDisplay>;
  groups: Record<string, OcPersonDisplay>;
}>();

const { t } = useAppI18n();

const execution = computed(() => props.widget.execution ?? null);
const failed = computed(() => execution.value != null && execution.value.success === false);
const buckets = computed(() => execution.value?.aggregation ?? []);
const title = computed(() => props.widget.title?.trim() || props.widget.key);
const total = computed(() => buckets.value.reduce((s, b) => s + b.count, 0));

const chartType = computed(() => {
  const t = (props.widget.chartType || 'donut').toLowerCase();
  return ['bar', 'pie', 'donut', 'line'].includes(t) ? t : 'donut';
});

function catalogEntry(key: string): OcCatalogDisplayEntry | undefined {
  const g = (props.widget.groupBy || 'stateId').toLowerCase();
  if (g === 'priorityid') return props.catalogs?.priorities?.[key];
  if (g === 'typeid') return props.catalogs?.types?.[key];
  if (g === 'stateid') return props.catalogs?.states?.[key];
  return undefined;
}

function labelFor(key: string | null | undefined): string {
  if (!key) return t('operationCore.dashboards.unassigned');
  const g = (props.widget.groupBy || 'stateId').toLowerCase();
  if (g === 'assignee') return props.people?.[key]?.name?.trim() || key;
  return catalogEntry(key)?.name?.trim() || key;
}

const labels = computed(() => buckets.value.map((b) => labelFor(b.key)));
const values = computed(() => buckets.value.map((b) => b.count));

// Renk: yalnızca hex (#...) renkleri ApexCharts'a verilir; tema adlari (primary vb.) varsayilan palete birakilir.
const colors = computed(() => {
  const out: string[] = [];
  let hasAny = false;
  for (const b of buckets.value) {
    const c = b.key ? catalogEntry(b.key)?.color : null;
    if (c && /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(c)) {
      out.push(c);
      hasAny = true;
    } else {
      out.push('#bdbdbd');
    }
  }
  return hasAny ? out : undefined;
});

const isCircular = computed(() => chartType.value === 'pie' || chartType.value === 'donut');

const chartOptions = computed(() => {
  const base: Record<string, unknown> = {
    chart: { type: chartType.value, fontFamily: 'inherit', toolbar: { show: false } },
    legend: { position: 'bottom' },
    dataLabels: { enabled: isCircular.value },
    tooltip: { theme: 'light' },
  };
  if (colors.value) base.colors = colors.value;

  if (isCircular.value) {
    base.labels = labels.value;
  } else {
    base.xaxis = { categories: labels.value };
    base.plotOptions = { bar: { distributed: true, borderRadius: 4, columnWidth: '55%' } };
    base.legend = { show: false };
  }
  return base;
});

const series = computed(() => {
  if (isCircular.value) return values.value;
  return [{ name: title.value, data: values.value }];
});
</script>

<template>
  <v-card variant="outlined" class="rounded-lg h-100 d-flex flex-column oc-dash-chart">
    <v-card-title class="d-flex align-center py-2 px-4 ga-2">
      <span class="text-subtitle-2 font-weight-medium text-truncate">{{ title }}</span>
      <v-spacer />
      <v-chip size="x-small" variant="tonal" color="primary">{{ total }}</v-chip>
    </v-card-title>
    <v-divider />

    <v-card-text class="flex-grow-1 d-flex align-center justify-center pa-2">
      <div v-if="failed" class="d-flex align-center ga-1 text-error">
        <v-icon icon="mdi-alert-circle-outline" size="18" />
        <span class="text-caption">
          {{ execution?.errorMessage || t('operationCore.dashboards.widgetError') }}
        </span>
      </div>

      <div v-else-if="!buckets.length" class="text-body-2 text-medium-emphasis">
        {{ t('operationCore.dashboards.emptyWidget') }}
      </div>

      <apexchart
        v-else
        width="100%"
        height="240"
        :type="chartType"
        :options="chartOptions"
        :series="series"
      />
    </v-card-text>
  </v-card>
</template>

<style scoped>
.oc-dash-chart {
  min-height: 300px;
}
</style>
