<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmScenarioRollup, AlarmSummary } from '@/types/apps/alarm';
import {
  fetchSiemDashboardPayload,
  invalidateSiemDashboardCache,
} from '@/composables/useSiemDashboardData';
import { getPrimary, getSecondary } from '@/utils/UpdateColors';
import {
  loadSiemDashboardLayout,
  saveSiemDashboardLayout,
  resetSiemDashboardLayout,
  type SiemDashboardLayout,
  type SiemDashboardWidgetId,
  type SiemStatCardId,
} from '@/composables/useSiemDashboardLayout';
import {
  SIEM_SCENARIO_CATALOG,
  scenarioEventsLink,
  type SiemScenarioDef,
} from '@/composables/useSiemScenarioCatalog';
import {
  loadSiemDashboardRefreshIntervalSec,
  saveSiemDashboardRefreshIntervalSec,
  SIEM_DASHBOARD_REFRESH_INTERVALS_SEC,
  type SiemDashboardRefreshIntervalSec,
} from '@/composables/useSiemDashboardRefresh';

const { t, locale } = useAppI18n();

const loading = ref(true);
const errorLocal = ref<string | null>(null);
const lastRefreshedAt = ref<number | null>(null);
const autoRefreshIntervalSec = ref<SiemDashboardRefreshIntervalSec>(loadSiemDashboardRefreshIntervalSec());

let autoRefreshTimer: ReturnType<typeof setInterval> | null = null;
const customizeOpen = ref(false);
const layoutDraft = ref<SiemDashboardLayout>(loadSiemDashboardLayout());
const layout = ref<SiemDashboardLayout>(loadSiemDashboardLayout());

const stats = ref({
  eventsTotal: 0,
  loginFailed: 0,
  deniedFlow: 0,
  newFlow: 0,
  openAlarms: 0,
});

const recentAlarms = ref<AlarmSummary[]>([]);

interface HourlyBucket {
  label: string;
  count: number;
  pct: number;
}

interface ScenarioCard {
  def: SiemScenarioDef;
  lastSeenAt: string | null;
  severity: number | null;
  open: boolean;
  totalAlarms: number;
  openCount: number;
}

type ScenarioStripState = 'open' | 'seen' | 'clean';

const hourlyBuckets = ref<HourlyBucket[]>([]);
const scenarioCards = ref<ScenarioCard[]>([]);

const timeRangeLabel = computed(() => t('siemCenter.dashboard.range24h'));

const autoRefreshOptions = computed(() =>
  SIEM_DASHBOARD_REFRESH_INTERVALS_SEC.map((sec) => ({
    title:
      sec === 0
        ? t('siemCenter.dashboard.autoRefreshOff')
        : t('siemCenter.dashboard.autoRefreshMinutes', { n: sec / 60 }),
    value: sec,
  })),
);

const lastRefreshedLabel = computed(() => {
  if (!lastRefreshedAt.value) return '';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    }).format(new Date(lastRefreshedAt.value));
  } catch {
    return '';
  }
});

const visibleWidgets = computed(() =>
  layout.value.widgetOrder.filter((id) => !layout.value.hiddenWidgets.includes(id)),
);

const showChartsRow = computed(
  () =>
    visibleWidgets.value.includes('eventTimeline') ||
    visibleWidgets.value.includes('breakdown'),
);

const showEventTimeline = computed(
  () =>
    visibleWidgets.value.includes('eventTimeline') &&
    !layout.value.hiddenWidgets.includes('eventTimeline'),
);

const showBreakdown = computed(
  () =>
    visibleWidgets.value.includes('breakdown') &&
    !layout.value.hiddenWidgets.includes('breakdown'),
);

const showRecentAlarms = computed(
  () =>
    visibleWidgets.value.includes('recentAlarms') &&
    !layout.value.hiddenWidgets.includes('recentAlarms'),
);

function formatHourLabel(iso: string): string {
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(iso));
  } catch {
    return iso;
  }
}

function buildHourlyBucketsFromSummary(
  hourly: { hourStart: string; count: number }[],
): HourlyBucket[] {
  const max = Math.max(...hourly.map((b) => b.count), 1);
  return hourly.map((bucket) => ({
    label: formatHourLabel(bucket.hourStart),
    count: bucket.count,
    pct: Math.round((bucket.count / max) * 100),
  }));
}

function formatDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(value));
  } catch {
    return value;
  }
}

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

const statCardDefs = computed(() => ({
  eventsTotal: {
    key: 'eventsTotal' as const,
    label: t('siemCenter.dashboard.statEvents'),
    value: stats.value.eventsTotal,
    color: 'primary',
    icon: 'mdi-shield-search',
    to: '/apps/siem-center/events',
  },
  openAlarms: {
    key: 'openAlarms' as const,
    label: t('siemCenter.dashboard.statOpenAlarms'),
    value: stats.value.openAlarms,
    color: 'error',
    icon: 'mdi-bell-alert',
    to: '/apps/alarm-center/alarms',
  },
  loginFailed: {
    key: 'loginFailed' as const,
    label: t('siemCenter.dashboard.statLoginFailed'),
    value: stats.value.loginFailed,
    color: 'warning',
    icon: 'mdi-account-lock',
    to: '/apps/siem-center/events?eventAction=login_failed',
  },
  deniedFlow: {
    key: 'deniedFlow' as const,
    label: t('siemCenter.dashboard.statDeniedFlow'),
    value: stats.value.deniedFlow,
    color: 'deep-orange',
    icon: 'mdi-firewall',
    to: '/apps/siem-center/events?eventAction=denied_flow',
  },
  newFlow: {
    key: 'newFlow' as const,
    label: t('siemCenter.dashboard.statNewFlow'),
    value: stats.value.newFlow,
    color: 'info',
    icon: 'mdi-transit-connection-variant',
    to: '/apps/siem-center/events?eventAction=new_flow',
  },
}));

const statCards = computed(() =>
  layout.value.statCardOrder
    .filter((id) => !layout.value.hiddenStatCards.includes(id))
    .map((id) => statCardDefs.value[id]),
);

const actionBreakdown = computed(() => {
  const s = stats.value;
  const items = [
    { key: 'login_failed', label: 'login_failed', count: s.loginFailed, color: 'warning' },
    { key: 'denied_flow', label: 'denied_flow', count: s.deniedFlow, color: 'deep-orange' },
    { key: 'new_flow', label: 'new_flow', count: s.newFlow, color: 'info' },
  ];
  const max = Math.max(...items.map((i) => i.count), 1);
  return items.map((i) => ({ ...i, pct: Math.round((i.count / max) * 100) }));
});

function contextKey(alarm: AlarmSummary): string | null {
  const key = alarm.context?.key;
  return typeof key === 'string' ? key : null;
}

function formatAlarmSummary(alarm: AlarmSummary): string {
  const ctx = alarm.context ?? {};
  const parts: string[] = [];
  for (const field of ['userId', 'srcIp', 'dstIp', 'windowCount'] as const) {
    const val = ctx[field];
    if (val != null && String(val).trim()) parts.push(String(val));
  }
  if (parts.length > 0) return parts.join(' · ');
  const dk = alarm.dedupKey ?? '';
  return dk.length > 72 ? `${dk.slice(0, 72)}…` : dk;
}

function formatAlarmScenario(alarm: AlarmSummary): string {
  const key = contextKey(alarm);
  if (!key) return '—';
  const def = SIEM_SCENARIO_CATALOG.find((s) => s.matchKey === key);
  if (def) return `${def.id} · ${scenarioTitle(def)}`;
  return key;
}

function formatAlarmStatus(status: AlarmSummary['status']): string {
  if (status === 'Active' || status === 0) return t('alarmCenter.alarms.statusActive');
  if (status === 'Acknowledged' || status === 1) return t('alarmCenter.alarms.statusAcknowledged');
  if (status === 'Resolved') return t('alarmCenter.alarms.statusResolved');
  if (status === 'Suppressed') return t('alarmCenter.alarms.statusSuppressed');
  return String(status);
}

function alarmStatusColor(status: AlarmSummary['status']): string {
  if (status === 'Active' || status === 0) return 'error';
  if (status === 'Acknowledged' || status === 1) return 'warning';
  if (status === 'Resolved') return 'success';
  return 'default';
}

function alarmDetailLink(alarm: AlarmSummary): string {
  return `/apps/alarm-center/alarms?alarmId=${encodeURIComponent(alarm.id)}`;
}

function scenarioTitle(def: SiemScenarioDef): string {
  const key = `siemCenter.scenarios.${def.id}.title`;
  const translated = t(key);
  return translated !== key ? translated : def.id;
}

function scenarioDescription(def: SiemScenarioDef): string {
  const key = `siemCenter.scenarios.${def.id}.desc`;
  const translated = t(key);
  return translated !== key ? translated : def.matchKey;
}

const timelineChartOptions = computed(() => ({
  chart: {
    type: 'bar',
    height: 240,
    fontFamily: 'inherit',
    foreColor: 'rgba(var(--v-theme-on-surface), 0.55)',
    toolbar: { show: false },
    sparkline: { enabled: false },
  },
  colors: [getPrimary.value],
  plotOptions: {
    bar: {
      borderRadius: 4,
      columnWidth: '72%',
    },
  },
  dataLabels: { enabled: false },
  grid: {
    borderColor: 'rgba(var(--v-border-color), 0.35)',
    strokeDashArray: 4,
    xaxis: { lines: { show: false } },
    yaxis: { lines: { show: true } },
    padding: { left: 8, right: 8 },
  },
  xaxis: {
    categories: hourlyBuckets.value.map((b) => b.label),
    axisBorder: { show: false },
    axisTicks: { show: false },
    labels: {
      rotate: -45,
      rotateAlways: false,
      hideOverlappingLabels: true,
      style: { fontSize: '10px' },
    },
  },
  yaxis: {
    min: 0,
    forceNiceScale: true,
    labels: {
      formatter: (v: number) => Math.round(v).toString(),
    },
  },
  tooltip: {
    theme: 'dark',
    y: {
      formatter: (v: number) => `${Math.round(v)} ${t('siemCenter.dashboard.tooltipEvents')}`,
    },
  },
}));

const timelineChartSeries = computed(() => [
  {
    name: t('siemCenter.dashboard.timelineSeries'),
    data: hourlyBuckets.value.map((b) => b.count),
  },
]);

const breakdownChartOptions = computed(() => {
  const items = actionBreakdown.value.filter((i) => i.count > 0);
  return {
    chart: {
      type: 'donut',
      height: 240,
      fontFamily: 'inherit',
      foreColor: 'rgba(var(--v-theme-on-surface), 0.55)',
    },
    labels: items.map((i) => i.label),
    colors: items.map((i) => {
      if (i.color === 'warning') return '#FB8C00';
      if (i.color === 'deep-orange') return '#FF5722';
      return getSecondary.value;
    }),
    legend: {
      position: 'bottom',
      fontSize: '12px',
      markers: { size: 6 },
    },
    dataLabels: {
      enabled: true,
      formatter: (_val: number, opts: { w: { config: { series: number[] } }; seriesIndex: number }) => {
        const count = opts.w.config.series[opts.seriesIndex] ?? 0;
        return count > 0 ? String(count) : '';
      },
    },
    plotOptions: {
      pie: {
        donut: {
          size: '62%',
          labels: {
            show: true,
            total: {
              show: true,
              label: t('siemCenter.dashboard.breakdownTotal'),
              formatter: () => stats.value.eventsTotal.toLocaleString(),
            },
          },
        },
      },
    },
    stroke: { width: 0 },
  };
});

const breakdownChartSeries = computed(() =>
  actionBreakdown.value.filter((i) => i.count > 0).map((i) => i.count),
);

const hasBreakdownChart = computed(() => breakdownChartSeries.value.some((n) => n > 0));

function buildScenarioCardsFromRollup(rollups: AlarmScenarioRollup[]): ScenarioCard[] {
  const byKey = new Map(rollups.map((r) => [r.matchKey, r]));
  return SIEM_SCENARIO_CATALOG.map((def) => {
    const rollup = byKey.get(def.matchKey);
    return {
      def,
      lastSeenAt: rollup?.lastSeenAt ?? null,
      severity: rollup?.maxSeverity ?? null,
      open: (rollup?.openCount ?? 0) > 0,
      totalAlarms: rollup?.totalInRange ?? 0,
      openCount: rollup?.openCount ?? 0,
    };
  });
}

function scenarioStripState(card: ScenarioCard): ScenarioStripState {
  if (card.open) return 'open';
  if (card.totalAlarms > 0 || card.lastSeenAt) return 'seen';
  return 'clean';
}

function scenarioStatusLabel(card: ScenarioCard): string {
  if (card.open) return t('siemCenter.dashboard.scenarioOpen');
  if (card.totalAlarms > 0) return t('siemCenter.dashboard.scenarioSeen');
  return t('siemCenter.dashboard.scenarioClean');
}

function scenarioStatusColor(card: ScenarioCard): string {
  if (card.open) return 'error';
  if (card.totalAlarms > 0) return 'warning';
  return 'default';
}

const openScenarioCards = computed(() => scenarioCards.value.filter((c) => c.open));

const otherScenarioCards = computed(() => scenarioCards.value.filter((c) => !c.open));

const hasScenarioChart = computed(() =>
  scenarioCards.value.some((c) => c.totalAlarms > 0 || c.open),
);

const scenarioChartHeight = computed(() =>
  Math.max(280, scenarioCards.value.length * 30 + 48),
);

const scenarioChartLabels = computed(() =>
  scenarioCards.value.map((c) => `${c.def.id} · ${scenarioTitle(c.def)}`),
);

const scenarioChartOptions = computed(() => ({
  chart: {
    type: 'bar',
    height: scenarioChartHeight.value,
    fontFamily: 'inherit',
    foreColor: 'rgba(var(--v-theme-on-surface), 0.55)',
    toolbar: { show: false },
    events: {
      dataPointSelection: (
        _event: unknown,
        _chartContext: unknown,
        config: { dataPointIndex: number },
      ) => {
        const card = scenarioCards.value[config.dataPointIndex];
        if (card) void navigateTo(scenarioEventsLink(card.def));
      },
    },
  },
  colors: scenarioCards.value.map((c) => {
    if (c.openCount > 0) return '#FF5252';
    if (c.totalAlarms > 0) return '#FB8C00';
    return 'rgba(var(--v-theme-on-surface), 0.18)';
  }),
  plotOptions: {
    bar: {
      horizontal: true,
      borderRadius: 4,
      barHeight: '68%',
      distributed: true,
    },
  },
  dataLabels: {
    enabled: true,
    formatter: (val: number) => (val > 0 ? String(Math.round(val)) : ''),
    style: { fontSize: '11px', fontWeight: 600 },
  },
  grid: {
    borderColor: 'rgba(var(--v-border-color), 0.35)',
    strokeDashArray: 4,
    xaxis: { lines: { show: true } },
    yaxis: { lines: { show: false } },
    padding: { left: 8, right: 16 },
  },
  xaxis: {
    categories: scenarioChartLabels.value,
    min: 0,
    forceNiceScale: true,
    labels: {
      formatter: (v: number) => Math.round(v).toString(),
    },
  },
  yaxis: {
    labels: {
      style: { fontSize: '11px' },
      maxWidth: 220,
    },
  },
  legend: { show: false },
  tooltip: {
    theme: 'dark',
    y: {
      formatter: (v: number, opts: { dataPointIndex: number }) => {
        const card = scenarioCards.value[opts.dataPointIndex];
        if (!card) return `${Math.round(v)}`;
        if (card.openCount > 0) {
          return t('siemCenter.dashboard.scenarioTooltipOpen', {
            open: card.openCount,
            total: card.totalAlarms,
          });
        }
        return t('siemCenter.dashboard.scenarioTooltipTotal', { n: Math.round(v) });
      },
    },
  },
}));

const scenarioChartSeries = computed(() => [
  {
    name: t('siemCenter.dashboard.scenarioChartSeries'),
    data: scenarioCards.value.map((c) => Math.max(c.totalAlarms, c.open ? c.openCount : 0)),
  },
]);

function widgetLabel(id: SiemDashboardWidgetId): string {
  return t(`siemCenter.dashboard.widgets.${id}`);
}

function statCardLabel(id: SiemStatCardId): string {
  return t(`siemCenter.dashboard.statCards.${id}`);
}

function openCustomize() {
  layoutDraft.value = JSON.parse(JSON.stringify(layout.value)) as SiemDashboardLayout;
  customizeOpen.value = true;
}

function isWidgetVisible(id: SiemDashboardWidgetId): boolean {
  return !layoutDraft.value.hiddenWidgets.includes(id);
}

function isStatVisible(id: SiemStatCardId): boolean {
  return !layoutDraft.value.hiddenStatCards.includes(id);
}

function toggleWidget(id: SiemDashboardWidgetId, visible: boolean | null) {
  const hidden = layoutDraft.value.hiddenWidgets.filter((x) => x !== id);
  if (!visible) hidden.push(id);
  layoutDraft.value.hiddenWidgets = hidden;
}

function toggleStat(id: SiemStatCardId, visible: boolean | null) {
  const hidden = layoutDraft.value.hiddenStatCards.filter((x) => x !== id);
  if (!visible) hidden.push(id);
  layoutDraft.value.hiddenStatCards = hidden;
}

function moveInList<T extends string>(list: T[], id: T, delta: number): T[] {
  const idx = list.indexOf(id);
  if (idx < 0) return list;
  const next = idx + delta;
  if (next < 0 || next >= list.length) return list;
  const copy = [...list];
  [copy[idx], copy[next]] = [copy[next], copy[idx]];
  return copy;
}

function moveWidget(id: SiemDashboardWidgetId, delta: number) {
  layoutDraft.value.widgetOrder = moveInList(layoutDraft.value.widgetOrder, id, delta);
}

function moveStat(id: SiemStatCardId, delta: number) {
  layoutDraft.value.statCardOrder = moveInList(layoutDraft.value.statCardOrder, id, delta);
}

function saveLayout() {
  layout.value = JSON.parse(JSON.stringify(layoutDraft.value)) as SiemDashboardLayout;
  saveSiemDashboardLayout(layout.value);
  customizeOpen.value = false;
}

function restoreDefaultLayout() {
  layoutDraft.value = resetSiemDashboardLayout();
  layout.value = JSON.parse(JSON.stringify(layoutDraft.value)) as SiemDashboardLayout;
  customizeOpen.value = false;
}

async function loadDashboard(options?: { force?: boolean; silent?: boolean }) {
  const force = options?.force ?? false;
  const silent = options?.silent ?? false;

  if (!silent) {
    loading.value = true;
    errorLocal.value = null;
  }

  try {
    const { events, alarms } = await fetchSiemDashboardPayload({ force });
    stats.value = {
      eventsTotal: events.eventsTotal,
      loginFailed: events.byAction.login_failed ?? 0,
      deniedFlow: events.byAction.denied_flow ?? 0,
      newFlow: events.byAction.new_flow ?? 0,
      openAlarms: alarms.openTotal,
    };
    recentAlarms.value = alarms.openAlarms;
    hourlyBuckets.value = buildHourlyBucketsFromSummary(events.hourly);
    scenarioCards.value = buildScenarioCardsFromRollup(alarms.scenarioRollup);
    lastRefreshedAt.value = Date.now();
  } catch (e: unknown) {
    if (!silent) {
      errorLocal.value = e instanceof Error ? e.message : t('siemCenter.dashboard.loadError');
      stats.value = { eventsTotal: 0, loginFailed: 0, deniedFlow: 0, newFlow: 0, openAlarms: 0 };
      recentAlarms.value = [];
      hourlyBuckets.value = [];
      scenarioCards.value = buildScenarioCardsFromRollup([]);
    }
  } finally {
    if (!silent) loading.value = false;
  }
}

function refreshDashboard() {
  invalidateSiemDashboardCache();
  void loadDashboard({ force: true });
}

function stopAutoRefresh() {
  if (autoRefreshTimer) {
    clearInterval(autoRefreshTimer);
    autoRefreshTimer = null;
  }
}

function startAutoRefresh() {
  stopAutoRefresh();
  if (autoRefreshIntervalSec.value <= 0) return;
  if (typeof document !== 'undefined' && document.visibilityState !== 'visible') return;

  autoRefreshTimer = setInterval(() => {
    invalidateSiemDashboardCache();
    void loadDashboard({ force: true, silent: true });
  }, autoRefreshIntervalSec.value * 1000);
}

function onVisibilityChange() {
  if (typeof document === 'undefined') return;
  if (document.visibilityState === 'visible' && autoRefreshIntervalSec.value > 0) {
    invalidateSiemDashboardCache();
    void loadDashboard({ force: true, silent: true });
    startAutoRefresh();
  } else {
    stopAutoRefresh();
  }
}

watch(autoRefreshIntervalSec, (sec) => {
  saveSiemDashboardRefreshIntervalSec(sec);
  startAutoRefresh();
});

onMounted(() => {
  void loadDashboard();
  startAutoRefresh();
  if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', onVisibilityChange);
  }
});

onUnmounted(() => {
  stopAutoRefresh();
  if (typeof document !== 'undefined') {
    document.removeEventListener('visibilitychange', onVisibilityChange);
  }
});
</script>

<template>
  <div>
    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-chip variant="tonal" color="primary" size="small">
        {{ timeRangeLabel }}
      </v-chip>
      <v-select
        v-model="autoRefreshIntervalSec"
        :items="autoRefreshOptions"
        item-title="title"
        item-value="value"
        density="compact"
        hide-details
        variant="outlined"
        prepend-inner-icon="mdi-clock-sync-outline"
        :label="t('siemCenter.dashboard.autoRefresh')"
        class="siem-auto-refresh-select"
        style="max-width: 11rem"
      />
      <span v-if="lastRefreshedLabel" class="text-caption text-medium-emphasis">
        {{ t('siemCenter.dashboard.lastRefreshed', { time: lastRefreshedLabel }) }}
      </span>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-view-dashboard-edit" @click="openCustomize">
        {{ t('siemCenter.dashboard.customize') }}
      </v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="refreshDashboard">
        {{ t('siemCenter.dashboard.refresh') }}
      </v-btn>
    </div>

    <v-row v-if="visibleWidgets.includes('stats')" dense class="mb-4">
      <v-col v-for="card in statCards" :key="card.key" cols="12" sm="6" md="4" lg="2">
        <v-skeleton-loader v-if="loading" type="card" class="rounded-lg" />
        <v-card
          v-else
          variant="flat"
          class="siem-stat-card h-100"
          :class="`siem-stat-card--${card.color}`"
          :to="card.to"
          link
        >
          <div class="siem-stat-card__accent" />
          <div class="pa-4 d-flex align-center gap-3">
            <v-avatar :color="card.color" variant="tonal" size="44" rounded="lg">
              <v-icon :icon="card.icon" size="22" />
            </v-avatar>
            <div class="min-w-0">
              <div class="text-caption text-medium-emphasis text-truncate">{{ card.label }}</div>
              <div class="text-h5 font-weight-bold lh-sm">{{ card.value.toLocaleString() }}</div>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <v-row v-if="showChartsRow" class="mb-4">
      <v-col v-if="showEventTimeline" cols="12" lg="8">
        <v-card variant="flat" class="siem-panel-card rounded-lg pa-4 h-100">
          <h2 class="text-subtitle-1 font-weight-bold mb-1">
            {{ t('siemCenter.dashboard.timelineTitle') }}
          </h2>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ timeRangeLabel }}
          </p>
          <v-skeleton-loader v-if="loading" type="image" height="240" />
          <div v-else-if="hourlyBuckets.every((b) => b.count === 0)" class="siem-chart-empty">
            {{ t('siemCenter.dashboard.timelineEmpty') }}
          </div>
          <ClientOnly v-else>
            <apexchart
              type="bar"
              height="240"
              :options="timelineChartOptions"
              :series="timelineChartSeries"
            />
          </ClientOnly>
        </v-card>
      </v-col>
      <v-col v-if="showBreakdown" cols="12" lg="4">
        <v-card variant="flat" class="siem-panel-card rounded-lg pa-4 h-100">
          <h2 class="text-subtitle-1 font-weight-bold mb-1">
            {{ t('siemCenter.dashboard.breakdownTitle') }}
          </h2>
          <p class="text-caption text-medium-emphasis mb-3">
            {{ timeRangeLabel }}
          </p>
          <v-skeleton-loader v-if="loading" type="image" height="240" />
          <div v-else-if="!hasBreakdownChart" class="siem-chart-empty">
            {{ t('siemCenter.dashboard.breakdownEmpty') }}
          </div>
          <ClientOnly v-else>
            <apexchart
              type="donut"
              height="240"
              :options="breakdownChartOptions"
              :series="breakdownChartSeries"
            />
          </ClientOnly>
        </v-card>
      </v-col>
    </v-row>

    <v-card
      v-if="visibleWidgets.includes('scenarios')"
      variant="flat"
      class="siem-panel-card rounded-lg pa-4 mb-4"
    >
      <div class="d-flex align-center mb-4">
        <h2 class="text-subtitle-1 font-weight-bold mb-0">
          {{ t('siemCenter.dashboard.scenariosTitle') }}
        </h2>
        <v-spacer />
        <v-btn
          variant="text"
          size="small"
          prepend-icon="mdi-shield-search"
          to="/apps/siem-center/events"
        >
          {{ t('siemCenter.dashboard.openEvents') }}
        </v-btn>
      </div>
      <v-skeleton-loader v-if="loading" type="image" class="mb-4" />
      <template v-else>
        <div class="scenario-status-strip mb-4">
          <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
            <span class="text-caption text-medium-emphasis">
              {{ t('siemCenter.dashboard.scenarioStripHint') }}
            </span>
            <div class="d-flex flex-wrap gap-3 text-caption text-medium-emphasis">
              <span class="d-inline-flex align-center gap-1">
                <span class="scenario-strip-dot scenario-strip-dot--open" />
                {{ t('siemCenter.dashboard.scenarioOpen') }}
              </span>
              <span class="d-inline-flex align-center gap-1">
                <span class="scenario-strip-dot scenario-strip-dot--seen" />
                {{ t('siemCenter.dashboard.scenarioSeen') }}
              </span>
              <span class="d-inline-flex align-center gap-1">
                <span class="scenario-strip-dot scenario-strip-dot--clean" />
                {{ t('siemCenter.dashboard.scenarioClean') }}
              </span>
            </div>
          </div>
          <div class="scenario-status-strip__grid">
            <v-tooltip
              v-for="card in scenarioCards"
              :key="card.def.id"
              location="top"
              :text="`${card.def.id} · ${scenarioTitle(card.def)} — ${scenarioStatusLabel(card)}`"
            >
              <template #activator="{ props: tipProps }">
                <NuxtLink
                  v-bind="tipProps"
                  :to="scenarioEventsLink(card.def)"
                  class="scenario-strip-cell"
                  :class="`scenario-strip-cell--${scenarioStripState(card)}`"
                >
                  {{ card.def.id }}
                </NuxtLink>
              </template>
            </v-tooltip>
          </div>
        </div>

        <div class="mb-4">
          <h3 class="text-body-2 font-weight-bold mb-1">
            {{ t('siemCenter.dashboard.scenarioChartTitle') }}
          </h3>
          <p class="text-caption text-medium-emphasis mb-2">
            {{ timeRangeLabel }}
          </p>
          <div v-if="!hasScenarioChart" class="siem-chart-empty siem-chart-empty--compact">
            {{ t('siemCenter.dashboard.scenarioChartEmpty') }}
          </div>
          <ClientOnly v-else>
            <apexchart
              type="bar"
              :height="scenarioChartHeight"
              :options="scenarioChartOptions"
              :series="scenarioChartSeries"
            />
          </ClientOnly>
        </div>

        <div v-if="openScenarioCards.length > 0" class="mb-4">
          <h3 class="text-body-2 font-weight-bold mb-3">
            {{
              t('siemCenter.dashboard.scenariosOpenTitle', { n: openScenarioCards.length })
            }}
          </h3>
          <v-row dense>
            <v-col
              v-for="card in openScenarioCards"
              :key="card.def.id"
              cols="12"
              sm="6"
              lg="4"
            >
              <v-card
                variant="flat"
                :to="scenarioEventsLink(card.def)"
                link
                class="scenario-card scenario-card--active h-100 pa-3"
              >
                <div class="d-flex align-start justify-space-between gap-2 mb-2">
                  <div class="min-w-0">
                    <div class="text-subtitle-2 font-weight-bold">{{ card.def.id }}</div>
                    <div class="text-caption text-medium-emphasis text-truncate">
                      {{ scenarioTitle(card.def) }}
                    </div>
                  </div>
                  <v-chip size="x-small" color="error" variant="flat" class="flex-shrink-0">
                    {{ t('siemCenter.dashboard.scenarioOpen') }}
                  </v-chip>
                </div>
                <div class="text-caption text-medium-emphasis mb-2 line-clamp-2">
                  {{ scenarioDescription(card.def) }}
                </div>
                <div class="text-body-2 font-weight-medium mb-2">
                  {{ card.lastSeenAt ? formatDate(card.lastSeenAt) : '—' }}
                </div>
                <div class="d-flex flex-wrap gap-2">
                  <v-chip
                    v-if="card.severity != null"
                    size="x-small"
                    :color="severityColor(card.severity)"
                    variant="tonal"
                  >
                    {{ t('siemCenter.dashboard.scenarioSeverity', { n: card.severity }) }}
                  </v-chip>
                  <v-chip size="x-small" variant="tonal" color="error">
                    {{
                      t('siemCenter.dashboard.scenarioOpenCount', {
                        n: card.openCount,
                      })
                    }}
                  </v-chip>
                </div>
              </v-card>
            </v-col>
          </v-row>
        </div>
        <v-alert
          v-else
          type="info"
          variant="tonal"
          density="compact"
          class="mb-4"
        >
          {{ t('siemCenter.dashboard.scenariosNoOpen') }}
        </v-alert>

        <div>
          <h3 class="text-body-2 font-weight-bold mb-3">
            {{ t('siemCenter.dashboard.scenariosOthersTitle') }}
          </h3>
          <v-table density="comfortable" class="siem-scenario-table">
            <thead>
              <tr>
                <th>{{ t('siemCenter.dashboard.scenarioColId') }}</th>
                <th>{{ t('siemCenter.dashboard.scenarioColStatus') }}</th>
                <th class="text-no-wrap">{{ t('siemCenter.dashboard.scenarioColCount') }}</th>
                <th class="text-no-wrap">{{ t('siemCenter.dashboard.scenarioColLastSeen') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="card in otherScenarioCards"
                :key="card.def.id"
                class="siem-scenario-table__row"
                @click="void navigateTo(scenarioEventsLink(card.def))"
              >
                <td>
                  <div class="font-weight-medium">{{ card.def.id }}</div>
                  <div class="text-caption text-medium-emphasis">
                    {{ scenarioTitle(card.def) }}
                  </div>
                </td>
                <td>
                  <v-chip size="x-small" :color="scenarioStatusColor(card)" variant="tonal">
                    {{ scenarioStatusLabel(card) }}
                  </v-chip>
                </td>
                <td class="text-medium-emphasis">{{ card.totalAlarms }}</td>
                <td class="text-no-wrap text-medium-emphasis">
                  {{
                    card.lastSeenAt
                      ? formatDate(card.lastSeenAt)
                      : t('siemCenter.dashboard.scenarioNever')
                  }}
                </td>
              </tr>
            </tbody>
          </v-table>
        </div>
      </template>
    </v-card>

    <v-card
      v-if="showRecentAlarms"
      variant="flat"
      class="siem-panel-card rounded-lg pa-4 mb-4"
    >
      <div class="d-flex flex-wrap align-center gap-2 mb-1">
        <h2 class="text-subtitle-1 font-weight-bold mb-0">
          {{ t('siemCenter.dashboard.recentAlarmsTitle') }}
        </h2>
        <v-chip v-if="!loading && stats.openAlarms > 0" size="small" color="error" variant="tonal">
          {{ t('alarmCenter.alarms.statTotal', { count: stats.openAlarms }) }}
        </v-chip>
      </div>
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('siemCenter.dashboard.recentAlarmsHint') }}
      </p>

      <v-skeleton-loader v-if="loading" type="table-row@8" />
      <v-table v-else density="comfortable" class="siem-alarm-table">
        <thead>
          <tr>
            <th class="text-no-wrap">{{ t('alarmCenter.alarms.colSeverity') }}</th>
            <th>{{ t('siemCenter.dashboard.alarmColScenario') }}</th>
            <th class="text-no-wrap">{{ t('alarmCenter.alarms.colStatus') }}</th>
            <th>{{ t('siemCenter.dashboard.alarmColSummary') }}</th>
            <th class="text-no-wrap">{{ t('alarmCenter.alarms.colCount') }}</th>
            <th class="text-no-wrap">{{ t('alarmCenter.alarms.colFirstSeen') }}</th>
            <th class="text-no-wrap">{{ t('alarmCenter.alarms.colLastSeen') }}</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="alarm in recentAlarms"
            :key="alarm.id"
            class="siem-alarm-table__row"
            @click="void navigateTo(alarmDetailLink(alarm))"
          >
            <td>
              <v-chip size="small" :color="severityColor(alarm.severity)" variant="flat">
                {{ alarm.severity }}
              </v-chip>
            </td>
            <td class="text-body-2 text-no-wrap">{{ formatAlarmScenario(alarm) }}</td>
            <td>
              <v-chip size="x-small" :color="alarmStatusColor(alarm.status)" variant="tonal">
                {{ formatAlarmStatus(alarm.status) }}
              </v-chip>
            </td>
            <td class="text-body-2">{{ formatAlarmSummary(alarm) }}</td>
            <td class="text-medium-emphasis">{{ alarm.count.toLocaleString() }}</td>
            <td class="text-no-wrap text-medium-emphasis">{{ formatDate(alarm.firstSeenAt) }}</td>
            <td class="text-no-wrap text-medium-emphasis">{{ formatDate(alarm.lastSeenAt) }}</td>
          </tr>
          <tr v-if="recentAlarms.length === 0">
            <td colspan="7" class="text-center text-medium-emphasis py-8">
              {{ t('siemCenter.dashboard.noAlarms') }}
            </td>
          </tr>
        </tbody>
      </v-table>
    </v-card>

    <v-dialog v-model="customizeOpen" max-width="560">
      <v-card>
        <v-card-title>{{ t('siemCenter.dashboard.customizeTitle') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('siemCenter.dashboard.customizeHint') }}
          </p>

          <div class="text-subtitle-2 mb-2">{{ t('siemCenter.dashboard.customizeSections') }}</div>
          <v-list density="compact" class="mb-4">
            <v-list-item
              v-for="id in layoutDraft.widgetOrder"
              :key="id"
              :title="widgetLabel(id)"
            >
              <template #prepend>
                <v-checkbox
                  :model-value="isWidgetVisible(id)"
                  hide-details
                  density="compact"
                  @update:model-value="toggleWidget(id, $event)"
                />
              </template>
              <template #append>
                <v-btn icon="mdi-chevron-up" variant="text" size="x-small" @click="moveWidget(id, -1)" />
                <v-btn icon="mdi-chevron-down" variant="text" size="x-small" @click="moveWidget(id, 1)" />
              </template>
            </v-list-item>
          </v-list>

          <div class="text-subtitle-2 mb-2">{{ t('siemCenter.dashboard.customizeStatCards') }}</div>
          <v-list density="compact">
            <v-list-item
              v-for="id in layoutDraft.statCardOrder"
              :key="id"
              :title="statCardLabel(id)"
            >
              <template #prepend>
                <v-checkbox
                  :model-value="isStatVisible(id)"
                  hide-details
                  density="compact"
                  @update:model-value="toggleStat(id, $event)"
                />
              </template>
              <template #append>
                <v-btn icon="mdi-chevron-up" variant="text" size="x-small" @click="moveStat(id, -1)" />
                <v-btn icon="mdi-chevron-down" variant="text" size="x-small" @click="moveStat(id, 1)" />
              </template>
            </v-list-item>
          </v-list>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="restoreDefaultLayout">
            {{ t('siemCenter.dashboard.customizeReset') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="customizeOpen = false">
            {{ t('siemCenter.dashboard.customizeCancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" @click="saveLayout">
            {{ t('siemCenter.dashboard.customizeSave') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.siem-panel-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
}

.siem-stat-card {
  position: relative;
  overflow: hidden;
  border: 1px solid rgba(var(--v-border-color), calc(var(--v-border-opacity) * 0.85));
  background: rgb(var(--v-theme-surface));
  transition: box-shadow 0.2s ease, transform 0.2s ease;
}

.siem-stat-card:hover {
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.08);
  transform: translateY(-1px);
}

.siem-stat-card__accent {
  position: absolute;
  left: 0;
  top: 0;
  bottom: 0;
  width: 3px;
  background: rgb(var(--v-theme-primary));
}

.siem-stat-card--error .siem-stat-card__accent {
  background: rgb(var(--v-theme-error));
}

.siem-stat-card--warning .siem-stat-card__accent {
  background: rgb(var(--v-theme-warning));
}

.siem-stat-card--deep-orange .siem-stat-card__accent {
  background: #ff5722;
}

.siem-stat-card--info .siem-stat-card__accent {
  background: rgb(var(--v-theme-info));
}

.siem-chart-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 240px;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.875rem;
}

.siem-chart-empty--compact {
  min-height: 120px;
}

.scenario-status-strip__grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 8px;
}

@media (min-width: 960px) {
  .scenario-status-strip__grid {
    grid-template-columns: repeat(10, minmax(0, 1fr));
  }
}

.scenario-strip-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 40px;
  border-radius: 8px;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.02em;
  text-decoration: none;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.scenario-strip-cell:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.scenario-strip-cell--open {
  color: rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.12);
  border-color: rgba(var(--v-theme-error), 0.45);
}

.scenario-strip-cell--seen {
  color: rgb(var(--v-theme-warning));
  background: rgba(var(--v-theme-warning), 0.1);
  border-color: rgba(var(--v-theme-warning), 0.35);
}

.scenario-strip-cell--clean {
  color: rgba(var(--v-theme-on-surface), 0.55);
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.scenario-strip-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  flex-shrink: 0;
}

.scenario-strip-dot--open {
  background: rgb(var(--v-theme-error));
}

.scenario-strip-dot--seen {
  background: rgb(var(--v-theme-warning));
}

.scenario-strip-dot--clean {
  background: rgba(var(--v-theme-on-surface), 0.28);
}

.siem-scenario-table :deep(th) {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55) !important;
}

.siem-scenario-table__row {
  cursor: pointer;
}

.siem-scenario-table__row:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.scenario-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-on-surface), 0.02);
  transition: border-color 0.2s ease, box-shadow 0.2s ease;
}

.scenario-card--active {
  border-color: rgba(var(--v-theme-error), 0.45);
  background: rgba(var(--v-theme-error), 0.06);
  box-shadow: inset 0 0 0 1px rgba(var(--v-theme-error), 0.12);
}

.scenario-card:hover {
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.06);
}

.line-clamp-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.siem-alarm-table :deep(th) {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55) !important;
}

.siem-alarm-table :deep(td) {
  vertical-align: middle;
}

.siem-alarm-table__row {
  cursor: pointer;
}

.siem-alarm-table__row:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}
</style>
