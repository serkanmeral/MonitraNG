<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import AcSiemDiscoveryMetricSparkline from '@/components/apps/siem-center/AcSiemDiscoveryMetricSparkline.vue';
import AcSiemDiscoveryMetricDonut from '@/components/apps/siem-center/AcSiemDiscoveryMetricDonut.vue';
import AcSiemDiscoveryMetricGauge from '@/components/apps/siem-center/AcSiemDiscoveryMetricGauge.vue';
import {
  DISCOVERY_METRICS_STALE_MS,
  diskUsedBytes,
  diskUsedPercent,
  fetchDiscoveryHostMetrics,
  formatBytes,
  hostMetricsEventsLink,
  primaryDisk,
  type DiscoveryDiskMetric,
  type DiscoveryHostMetricsSnapshot,
} from '@/composables/useSiemDiscoveryHostMetrics';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';

const props = defineProps<{
  hostname: string;
  /** Full host (IP + agent) so scan-IP cards resolve to MachineName metrics. */
  host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null;
  staleMs?: number;
}>();

const { t, locale } = useAppI18n();

const loading = ref(false);
const error = ref<string | null>(null);
const snap = ref<DiscoveryHostMetricsSnapshot | null>(null);
const loadedFor = ref<string | null>(null);
const innerTab = ref('overview');
const processMode = ref<'cpu' | 'memory'>('cpu');

const staleThreshold = computed(() =>
  props.staleMs != null && props.staleMs > 0 ? props.staleMs : DISCOVERY_METRICS_STALE_MS,
);

const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const hasAny = computed(() => {
  const s = snap.value;
  if (!s) return false;
  return (
    s.cpuPercent != null
    || s.memoryUsedPercent != null
    || s.memoryAvailableBytes != null
    || s.disks.some((d) => diskUsedPercent(d) != null)
    || s.topCpu.length > 0
    || s.topMemory.length > 0
    || s.cpuSeries.length > 0
  );
});

const isStale = computed(() => {
  const at = snap.value?.freshestAt;
  if (at == null) return false;
  return Date.now() - at > staleThreshold.value;
});

const hostHints = computed(() => props.host ?? {
  hostname: props.hostname,
  ip: props.hostname,
  agent: null,
});

const metricsEventsHref = computed(() => hostMetricsEventsLink(props.hostname, hostHints.value));

const mainDisk = computed(() => (snap.value ? primaryDisk(snap.value.disks) : null));

const mainDiskUsed = computed(() =>
  mainDisk.value ? diskUsedPercent(mainDisk.value) : null,
);

const cpuValues = computed(() => snap.value?.cpuSeries.map((p) => p.value) ?? []);
const memoryValues = computed(() => {
  const used = snap.value?.memoryUsedSeries ?? [];
  if (used.length) return used.map((p) => p.value);
  return [];
});

const diskSparkByVolume = computed(() => {
  const map = new Map<string, number[]>();
  for (const s of snap.value?.diskSeries ?? []) {
    map.set(s.volume, s.series.map((p) => p.value));
  }
  return map;
});

const cpuWarn = computed(() => (snap.value?.cpuPercent ?? 0) >= 80);
const memoryWarn = computed(() => (snap.value?.memoryUsedPercent ?? 0) >= 90);
const diskWarn = computed(() => (mainDiskUsed.value ?? 0) >= 90);

const disksWithUsage = computed(() =>
  (snap.value?.disks ?? []).filter((d) => diskUsedPercent(d) != null),
);

function formatTs(ms: number | null | undefined): string {
  if (ms == null || !Number.isFinite(ms)) return '—';
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(ms));
  } catch {
    return new Date(ms).toISOString();
  }
}

function ageLabel(ms: number | null | undefined): string | null {
  if (ms == null) return null;
  const ageSec = Math.max(0, Math.round((Date.now() - ms) / 1000));
  if (ageSec < 60) return t('siemCenter.discovery.hostDetail.ageSeconds', { n: ageSec });
  const ageMin = Math.round(ageSec / 60);
  return t('siemCenter.discovery.hostDetail.ageMinutes', { n: ageMin });
}

function fmtBytes(n: number | null | undefined): string {
  return formatBytes(n, dateLocale.value);
}

function fmtCpu(n: number | null | undefined): string {
  if (n == null || !Number.isFinite(n)) return '—';
  return `${n.toLocaleString(dateLocale.value, { maximumFractionDigits: 1 })}%`;
}

function fmtPct(n: number | null | undefined): string {
  if (n == null || !Number.isFinite(n)) return '—';
  return `${n.toLocaleString(dateLocale.value, { maximumFractionDigits: 1 })}%`;
}

function usedOfTotalLabel(used: number | null | undefined, total: number | null | undefined): string {
  if (used == null || total == null) return '—';
  return t('siemCenter.discovery.hostDetail.metricsUsedOfTotal', {
    used: fmtBytes(used),
    total: fmtBytes(total),
  });
}

function diskUsedOfTotal(d: DiscoveryDiskMetric): string {
  return usedOfTotalLabel(diskUsedBytes(d), d.totalBytes);
}

async function load(force = false) {
  const host = props.hostname.trim();
  if (!host) return;
  if (!force && loadedFor.value === host && snap.value) return;

  loading.value = true;
  error.value = null;
  try {
    snap.value = await fetchDiscoveryHostMetrics(host, { host: hostHints.value });
    loadedFor.value = host;
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    snap.value = null;
    loadedFor.value = null;
  } finally {
    loading.value = false;
  }
}

watch(
  () => [
    props.hostname,
    props.host?.agent?.machine,
    props.host?.agent?.primaryIp,
    props.host?.ip,
  ] as const,
  () => {
    snap.value = null;
    loadedFor.value = null;
    innerTab.value = 'overview';
    processMode.value = 'cpu';
    void load(true);
  },
  { immediate: true },
);
</script>

<template>
  <div class="host-metrics-panel">
    <div class="d-flex align-center flex-wrap ga-2 px-4 pt-3 pb-1">
      <v-spacer class="d-none d-sm-block" />
      <v-btn
        size="small"
        variant="text"
        prepend-icon="mdi-refresh"
        :loading="loading"
        @click="load(true)"
      >
        {{ t('siemCenter.discovery.hostDetail.metricsRefresh') }}
      </v-btn>
      <v-btn
        size="small"
        variant="text"
        prepend-icon="mdi-timeline-text-outline"
        :to="metricsEventsHref"
        target="_blank"
        rel="noopener noreferrer"
      >
        {{ t('siemCenter.discovery.hostDetail.metricsOpenEvents') }}
      </v-btn>
    </div>

    <v-tabs v-model="innerTab" density="compact" color="primary" class="px-2">
      <v-tab value="overview">{{ t('siemCenter.discovery.hostDetail.metricsTabOverview') }}</v-tab>
      <v-tab value="resources">{{ t('siemCenter.discovery.hostDetail.metricsTabResources') }}</v-tab>
      <v-tab value="processes">{{ t('siemCenter.discovery.hostDetail.metricsTabProcesses') }}</v-tab>
    </v-tabs>
    <v-divider />

    <div class="pa-4">
      <v-alert v-if="error" type="warning" variant="tonal" density="compact" class="mb-3">
        {{ error }}
      </v-alert>

      <v-skeleton-loader v-if="loading && !snap" type="card, list-item@3" />

      <template v-else-if="!hasAny">
        <v-sheet border rounded class="pa-3 text-medium-emphasis text-body-2">
          {{ t('siemCenter.discovery.hostDetail.metricsEmpty') }}
        </v-sheet>
      </template>

      <template v-else>
        <v-alert
          v-if="isStale"
          type="warning"
          variant="tonal"
          density="compact"
          class="mb-3"
        >
          {{ t('siemCenter.discovery.hostDetail.metricsStale') }}
          <span v-if="snap?.freshestAt" class="ms-1">
            ({{ formatTs(snap.freshestAt) }}
            <span v-if="ageLabel(snap.freshestAt)"> · {{ ageLabel(snap.freshestAt) }}</span>)
          </span>
        </v-alert>

        <div class="text-caption text-medium-emphasis mb-3">
          {{ t('siemCenter.discovery.hostDetail.metricsLastSample') }}:
          {{ formatTs(snap?.freshestAt) }}
          <span v-if="ageLabel(snap?.freshestAt)"> ({{ ageLabel(snap?.freshestAt) }})</span>
          <span class="ms-1">· {{ t('siemCenter.discovery.hostDetail.metricsWindowHint') }}</span>
        </div>

        <v-tabs-window v-model="innerTab">
          <!-- Overview: usage % primary -->
          <v-tabs-window-item value="overview">
            <v-row dense>
              <v-col cols="12" sm="4">
                <v-sheet
                  border
                  rounded
                  class="pa-3 h-100 d-flex flex-column align-center"
                  :class="{ 'kpi-warn': cpuWarn }"
                >
                  <div class="text-caption text-medium-emphasis align-self-start w-100 mb-1">
                    {{ t('siemCenter.discovery.hostDetail.metricsCpu') }}
                  </div>
                  <AcSiemDiscoveryMetricGauge
                    :value="snap?.cpuPercent ?? null"
                    :color="cpuWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-primary))'"
                    :caption="t('siemCenter.discovery.hostDetail.metricsUsage')"
                  />
                  <AcSiemDiscoveryMetricSparkline
                    class="mt-1 w-100"
                    :values="cpuValues"
                    :height="28"
                    :color="cpuWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-primary))'"
                  />
                </v-sheet>
              </v-col>

              <v-col cols="12" sm="4">
                <v-sheet
                  border
                  rounded
                  class="pa-3 h-100 d-flex flex-column align-center"
                  :class="{ 'kpi-warn': memoryWarn }"
                >
                  <div class="text-caption text-medium-emphasis align-self-start w-100 mb-1">
                    {{ t('siemCenter.discovery.hostDetail.metricsMemory') }}
                  </div>
                  <template v-if="snap?.memoryUsedPercent != null">
                    <AcSiemDiscoveryMetricGauge
                      :value="snap.memoryUsedPercent"
                      :color="memoryWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-secondary))'"
                      :caption="t('siemCenter.discovery.hostDetail.metricsUsage')"
                    />
                    <div class="text-caption text-medium-emphasis font-mono mt-2 text-center">
                      {{ usedOfTotalLabel(snap.memoryUsedBytes, snap.memoryTotalBytes) }}
                    </div>
                    <AcSiemDiscoveryMetricSparkline
                      v-if="memoryValues.length"
                      class="mt-1 w-100"
                      :values="memoryValues"
                      :height="28"
                      :color="memoryWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-secondary))'"
                    />
                  </template>
                  <template v-else>
                    <div class="text-caption text-medium-emphasis mt-2">
                      {{ t('siemCenter.discovery.hostDetail.metricsMemoryAvailOnly') }}
                    </div>
                    <div class="text-h5 font-weight-bold font-mono mt-2">
                      {{ fmtBytes(snap?.memoryAvailableBytes) }}
                    </div>
                  </template>
                </v-sheet>
              </v-col>

              <v-col cols="12" sm="4">
                <v-sheet
                  border
                  rounded
                  class="pa-3 h-100 d-flex flex-column align-center"
                  :class="{ 'kpi-warn': diskWarn }"
                >
                  <div class="text-caption text-medium-emphasis align-self-start w-100 mb-1">
                    {{ t('siemCenter.discovery.hostDetail.metricsDisk') }}
                    <span v-if="mainDisk" class="font-mono"> · {{ mainDisk.volume }}</span>
                  </div>
                  <AcSiemDiscoveryMetricDonut
                    :used-percent="mainDiskUsed"
                    :used-color="diskWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-info))'"
                    :center-label="mainDiskUsed != null ? fmtPct(mainDiskUsed) : '—'"
                    :center-caption="t('siemCenter.discovery.hostDetail.metricsUsage')"
                  />
                  <div v-if="mainDisk && diskUsedBytes(mainDisk) != null" class="text-caption text-medium-emphasis font-mono mt-2 text-center">
                    {{ diskUsedOfTotal(mainDisk) }}
                  </div>
                </v-sheet>
              </v-col>
            </v-row>
          </v-tabs-window-item>

          <!-- Resources -->
          <v-tabs-window-item value="resources">
            <v-sheet border rounded class="pa-3 mb-3" :class="{ 'kpi-warn': cpuWarn }">
              <div class="d-flex align-start flex-wrap ga-4">
                <AcSiemDiscoveryMetricGauge
                  :value="snap?.cpuPercent ?? null"
                  :size="128"
                  :color="cpuWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-primary))'"
                  :caption="t('siemCenter.discovery.hostDetail.metricsUsage')"
                />
                <div class="flex-grow-1" style="min-width: 160px">
                  <div class="text-subtitle-2 mb-2">{{ t('siemCenter.discovery.hostDetail.metricsCpu') }}</div>
                  <AcSiemDiscoveryMetricSparkline
                    :values="cpuValues"
                    :height="56"
                    :color="cpuWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-primary))'"
                  />
                  <div class="text-caption text-medium-emphasis mt-2">
                    {{ t('siemCenter.discovery.hostDetail.metricsSeriesHint') }}
                  </div>
                </div>
              </div>
            </v-sheet>

            <v-sheet border rounded class="pa-3 mb-3" :class="{ 'kpi-warn': memoryWarn }">
              <div class="d-flex align-start flex-wrap ga-4">
                <template v-if="snap?.memoryUsedPercent != null">
                  <AcSiemDiscoveryMetricGauge
                    :value="snap.memoryUsedPercent"
                    :size="128"
                    :color="memoryWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-secondary))'"
                    :caption="t('siemCenter.discovery.hostDetail.metricsUsage')"
                  />
                  <div class="flex-grow-1" style="min-width: 160px">
                    <div class="text-subtitle-2 mb-1">{{ t('siemCenter.discovery.hostDetail.metricsMemory') }}</div>
                    <div class="text-body-2 font-mono mb-2">
                      {{ usedOfTotalLabel(snap.memoryUsedBytes, snap.memoryTotalBytes) }}
                    </div>
                    <AcSiemDiscoveryMetricSparkline
                      v-if="memoryValues.length"
                      :values="memoryValues"
                      :height="56"
                      :color="memoryWarn ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-secondary))'"
                    />
                  </div>
                </template>
                <div v-else class="w-100">
                  <div class="text-subtitle-2 mb-1">{{ t('siemCenter.discovery.hostDetail.metricsMemoryAvailOnly') }}</div>
                  <div class="text-h6 font-weight-bold font-mono">{{ fmtBytes(snap?.memoryAvailableBytes) }}</div>
                </div>
              </div>
            </v-sheet>

            <div class="text-subtitle-2 mb-2">{{ t('siemCenter.discovery.hostDetail.metricsDisk') }}</div>
            <v-sheet v-if="!disksWithUsage.length" border rounded class="pa-3 text-medium-emphasis text-body-2">
              —
            </v-sheet>
            <v-row v-else dense>
              <v-col
                v-for="d in disksWithUsage"
                :key="d.volume"
                cols="12"
                sm="6"
              >
                <v-sheet
                  border
                  rounded
                  class="pa-3 h-100"
                  :class="{ 'kpi-warn': (diskUsedPercent(d) ?? 0) >= 90 }"
                >
                  <div class="d-flex align-center ga-3">
                    <AcSiemDiscoveryMetricDonut
                      :used-percent="diskUsedPercent(d)"
                      :size="88"
                      :thickness="10"
                      :used-color="(diskUsedPercent(d) ?? 0) >= 90 ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-info))'"
                      :center-label="diskUsedPercent(d) != null ? fmtPct(diskUsedPercent(d)) : '—'"
                      :center-caption="t('siemCenter.discovery.hostDetail.metricsUsage')"
                    />
                    <div class="flex-grow-1 min-w-0">
                      <div class="font-weight-medium font-mono mb-1">{{ d.volume }}</div>
                      <div class="text-caption text-medium-emphasis font-mono mb-2">
                        {{ diskUsedOfTotal(d) }}
                      </div>
                      <AcSiemDiscoveryMetricSparkline
                        :values="diskSparkByVolume.get(d.volume) || []"
                        :height="32"
                        :color="(diskUsedPercent(d) ?? 0) >= 90 ? 'rgb(var(--v-theme-warning))' : 'rgb(var(--v-theme-info))'"
                      />
                    </div>
                  </div>
                </v-sheet>
              </v-col>
            </v-row>
          </v-tabs-window-item>

          <!-- Processes -->
          <v-tabs-window-item value="processes">
            <v-btn-toggle
              v-model="processMode"
              mandatory
              density="compact"
              color="primary"
              variant="outlined"
              class="mb-3"
            >
              <v-btn value="cpu" size="small">
                {{ t('siemCenter.discovery.hostDetail.metricsTopCpu') }}
              </v-btn>
              <v-btn value="memory" size="small">
                {{ t('siemCenter.discovery.hostDetail.metricsTopMemory') }}
              </v-btn>
            </v-btn-toggle>

            <template v-if="processMode === 'cpu'">
              <v-sheet v-if="!snap?.topCpu?.length" border rounded class="pa-3 text-medium-emphasis text-body-2">
                —
              </v-sheet>
              <v-table v-else density="compact" class="metrics-table">
                <thead>
                  <tr>
                    <th class="text-left">{{ t('siemCenter.discovery.hostDetail.metricsProcess') }}</th>
                    <th class="text-left">PID</th>
                    <th class="text-right">CPU</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(p, i) in snap.topCpu" :key="`cpu-${p.pid ?? i}-${p.name}`">
                    <td class="font-mono text-break">{{ p.name }}</td>
                    <td class="font-mono">{{ p.pid ?? '—' }}</td>
                    <td class="font-mono text-right">{{ fmtCpu(p.cpuPercent) }}</td>
                  </tr>
                </tbody>
              </v-table>
            </template>

            <template v-else>
              <v-sheet v-if="!snap?.topMemory?.length" border rounded class="pa-3 text-medium-emphasis text-body-2">
                —
              </v-sheet>
              <v-table v-else density="compact" class="metrics-table">
                <thead>
                  <tr>
                    <th class="text-left">{{ t('siemCenter.discovery.hostDetail.metricsProcess') }}</th>
                    <th class="text-left">PID</th>
                    <th class="text-right">RSS</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(p, i) in snap.topMemory" :key="`mem-${p.pid ?? i}-${p.name}`">
                    <td class="font-mono text-break">{{ p.name }}</td>
                    <td class="font-mono">{{ p.pid ?? '—' }}</td>
                    <td class="font-mono text-right">{{ fmtBytes(p.workingSetBytes) }}</td>
                  </tr>
                </tbody>
              </v-table>
            </template>
          </v-tabs-window-item>
        </v-tabs-window>
      </template>
    </div>
  </div>
</template>

<style scoped>
.kpi-warn {
  border-color: rgba(var(--v-theme-warning), 0.55) !important;
}
.metrics-table :deep(td),
.metrics-table :deep(th) {
  border-bottom: thin solid rgba(var(--v-border-color), var(--v-border-opacity)) !important;
  padding-block: 6px !important;
}
</style>
