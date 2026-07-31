<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  hostLocalUiLink,
  loadHostDashboardHost,
  shortHostKey,
} from '@/composables/useSiemDiscoveryData';
import {
  hostAnalyticsEventsLink,
  loadHostAnalytics,
  parseHostAnalyticsTimeRange,
  resolveHostAnalyticsRange,
  type HostAnalyticsBundle,
  type HostAnalyticsTimeRange,
  type HostRoleChip,
} from '@/composables/useSiemHostAnalytics';
import { coverageColor } from '@/composables/useSiemDiscoveryMock';
import AcSiemHostKpiStrip from '@/components/apps/siem-center/AcSiemHostKpiStrip.vue';
import AcSiemHostResourceCharts from '@/components/apps/siem-center/AcSiemHostResourceCharts.vue';
import AcSiemHostSessionsCard from '@/components/apps/siem-center/AcSiemHostSessionsCard.vue';
import AcSiemHostWatchSummary from '@/components/apps/siem-center/AcSiemHostWatchSummary.vue';
import AcSiemHostEventLogSummary from '@/components/apps/siem-center/AcSiemHostEventLogSummary.vue';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';

const props = defineProps<{
  hostname: string;
}>();

const { t } = useAppI18n();
const route = useRoute();
const router = useRouter();

const hostLoading = ref(true);
const analyticsLoading = ref(true);
const error = ref<string | null>(null);
const host = ref<SiemDiscoveryHost | null>(null);
const bundle = ref<HostAnalyticsBundle | null>(null);

const timeRange = ref<HostAnalyticsTimeRange>('24h');
const customFromLocal = ref('');
const customToLocal = ref('');
const rangeMode = ref<'preset' | 'custom'>('preset');

const shortName = computed(() => shortHostKey(props.hostname) || props.hostname.trim().toLowerCase());

const coverageLabel = computed(() => {
  if (!host.value) return '';
  return t(`siemCenter.discovery.coverage.${host.value.coverage}`);
});

const coverageChipColor = computed(() =>
  host.value ? coverageColor(host.value.coverage) : 'grey',
);

const agent = computed(() => host.value?.agent ?? null);

const displayIp = computed(() => {
  const h = host.value;
  if (!h) return '—';
  return agent.value?.primaryIp || (h.ip && h.ip !== '—' ? h.ip : '—');
});

const displayUser = computed(() => {
  const a = agent.value;
  if (!a) return null;
  if (a.consoleUser) return a.consoleUser;
  if (a.loggedOnUsers?.length) return a.loggedOnUsers.join(', ');
  return null;
});

const localUiHref = computed(() =>
  host.value ? hostLocalUiLink(host.value) : null,
);

const notInAd = computed(() =>
  !!host.value && host.value.coverage === 'discoveredUnmanaged',
);

const roles = computed(() => bundle.value?.roles ?? []);

const eventsHref = computed(() => {
  if (!bundle.value) return `/apps/siem-center/events?search=${encodeURIComponent(shortName.value)}`;
  return hostAnalyticsEventsLink(shortName.value, bundle.value.range);
});

const watchEventsHref = computed(() => {
  if (!bundle.value) return eventsHref.value;
  return hostAnalyticsEventsLink(shortName.value, bundle.value.range, {
    sourceType: 'metric',
    eventAction: 'watch.inventory',
  });
});

const eventLogEventsHref = computed(() => {
  if (!bundle.value) return eventsHref.value;
  return hostAnalyticsEventsLink(shortName.value, bundle.value.range, {
    sourceType: 'windows-eventlog',
  });
});

const rangePresets = computed(() => [
  { title: t('siemCenter.hostDashboard.range1h'), value: '1h' as const },
  { title: t('siemCenter.hostDashboard.range6h'), value: '6h' as const },
  { title: t('siemCenter.hostDashboard.range24h'), value: '24h' as const },
  { title: t('siemCenter.hostDashboard.range7d'), value: '7d' as const },
  { title: t('siemCenter.hostDashboard.rangeCustom'), value: 'custom' as const },
]);

function roleLabel(role: HostRoleChip): string {
  return t(`siemCenter.hostDashboard.role.${role}`);
}

function toDatetimeLocalValue(isoOrMs: string | number): string {
  const ms = typeof isoOrMs === 'number' ? isoOrMs : Date.parse(isoOrMs);
  if (!Number.isFinite(ms)) return '';
  const d = new Date(ms);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function fromDatetimeLocalInput(local: string): string | null {
  const ms = Date.parse(local);
  return Number.isFinite(ms) ? new Date(ms).toISOString() : null;
}

function applyRangeFromRoute() {
  const q = route.query;
  if (typeof q.from === 'string' && q.from) {
    rangeMode.value = 'custom';
    timeRange.value = 'custom';
    customFromLocal.value = toDatetimeLocalValue(q.from);
    customToLocal.value = typeof q.to === 'string' && q.to
      ? toDatetimeLocalValue(q.to)
      : toDatetimeLocalValue(Date.now());
    return;
  }
  const tr = parseHostAnalyticsTimeRange(q.timeRange);
  rangeMode.value = 'preset';
  timeRange.value = tr === 'custom' ? '24h' : tr;
}

async function syncRangeToUrl() {
  const query: Record<string, string> = {};
  // Drop legacy tab param from modal deep-links
  if (rangeMode.value === 'custom') {
    const from = fromDatetimeLocalInput(customFromLocal.value);
    const to = fromDatetimeLocalInput(customToLocal.value);
    if (from) query.from = from;
    if (to) query.to = to;
  } else if (timeRange.value !== '24h') {
    query.timeRange = timeRange.value;
  }
  await router.replace({ query });
}

async function loadHostMeta() {
  hostLoading.value = true;
  try {
    host.value = await loadHostDashboardHost(props.hostname);
  } catch (e: unknown) {
    host.value = null;
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    hostLoading.value = false;
  }
}

async function loadAnalytics() {
  analyticsLoading.value = true;
  error.value = null;
  try {
    let tr: HostAnalyticsTimeRange = timeRange.value;
    let from: string | null = null;
    let to: string | null = null;
    if (rangeMode.value === 'custom') {
      tr = 'custom';
      from = fromDatetimeLocalInput(customFromLocal.value);
      to = fromDatetimeLocalInput(customToLocal.value);
      if (!from || !to || Date.parse(from) >= Date.parse(to)) {
        error.value = t('siemCenter.hostDashboard.invalidRange');
        bundle.value = null;
        return;
      }
    }
    bundle.value = await loadHostAnalytics({
      hostname: props.hostname,
      host: host.value,
      timeRange: tr,
      from,
      to,
    });
  } catch (e: unknown) {
    bundle.value = null;
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    analyticsLoading.value = false;
  }
}

async function reloadAll() {
  await loadHostMeta();
  await loadAnalytics();
}

async function applyFilters() {
  await syncRangeToUrl();
  await loadAnalytics();
}

function onPresetChange(v: HostAnalyticsTimeRange | null) {
  if (!v) return;
  if (v === 'custom') {
    rangeMode.value = 'custom';
    timeRange.value = 'custom';
    if (!customFromLocal.value || !customToLocal.value) {
      const resolved = resolveHostAnalyticsRange({ timeRange: '24h' });
      customFromLocal.value = toDatetimeLocalValue(resolved.fromMs);
      customToLocal.value = toDatetimeLocalValue(resolved.toMs);
    }
    return;
  }
  rangeMode.value = 'preset';
  timeRange.value = v;
  void applyFilters();
}

watch(
  () => props.hostname,
  () => {
    void reloadAll();
  },
);

watch(
  () => [route.query.timeRange, route.query.from, route.query.to],
  () => {
    applyRangeFromRoute();
  },
);

onMounted(async () => {
  applyRangeFromRoute();
  await reloadAll();
});
</script>

<template>
  <div class="siem-host-dashboard">
    <v-skeleton-loader v-if="hostLoading && !host" type="article" class="mb-4" />

    <v-alert v-else-if="error && !bundle" type="error" variant="tonal" class="mb-4">
      {{ error }}
    </v-alert>

    <template v-if="host">
      <v-alert
        v-if="notInAd"
        type="info"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        {{ t('siemCenter.hostDashboard.notInInventory') }}
      </v-alert>

      <v-card variant="outlined" class="rounded-lg mb-4 host-sticky-bar">
        <div class="pa-4 pa-md-5">
          <div class="d-flex flex-wrap align-start justify-space-between ga-3 mb-4">
            <div class="min-w-0">
              <div class="d-flex flex-wrap align-center ga-2 mb-1">
                <h2 class="text-h5 font-weight-bold text-truncate mb-0">
                  {{ host.hostname }}
                </h2>
                <v-chip size="small" :color="coverageChipColor" variant="flat">
                  {{ coverageLabel }}
                </v-chip>
                <v-chip
                  v-for="role in roles"
                  :key="role"
                  size="small"
                  color="primary"
                  variant="tonal"
                >
                  {{ roleLabel(role) }}
                </v-chip>
              </div>
              <div class="text-body-2 text-medium-emphasis">
                <span class="font-mono">{{ shortName }}</span>
                <span> · {{ displayIp }}</span>
                <span v-if="host.osHint"> · {{ host.osHint }}</span>
                <span v-if="displayUser"> · {{ displayUser }}</span>
              </div>
            </div>
            <div class="d-flex flex-wrap ga-2">
              <v-btn
                variant="outlined"
                color="primary"
                prepend-icon="mdi-map-search-outline"
                to="/apps/siem-center/discovery"
              >
                {{ t('siemCenter.hostDashboard.backDiscovery') }}
              </v-btn>
              <v-btn
                variant="outlined"
                color="primary"
                prepend-icon="mdi-timeline-text-outline"
                :to="eventsHref"
                target="_blank"
                rel="noopener noreferrer"
              >
                {{ t('siemCenter.hostDashboard.openEvents') }}
              </v-btn>
              <v-tooltip :disabled="!!localUiHref" location="top">
                <template #activator="{ props: tip }">
                  <span v-bind="tip" class="d-inline-flex">
                    <v-btn
                      variant="outlined"
                      prepend-icon="mdi-open-in-new"
                      :href="localUiHref || undefined"
                      :disabled="!localUiHref"
                      target="_blank"
                      rel="noopener noreferrer"
                    >
                      {{ t('siemCenter.discovery.hostDetail.openLocalUi') }}
                    </v-btn>
                  </span>
                </template>
                <span>{{ t('siemCenter.discovery.hostDetail.openLocalUiDisabled') }}</span>
              </v-tooltip>
              <v-btn
                variant="text"
                prepend-icon="mdi-refresh"
                :loading="hostLoading || analyticsLoading"
                @click="reloadAll"
              >
                {{ t('siemCenter.hostDashboard.refresh') }}
              </v-btn>
            </div>
          </div>

          <div class="d-flex flex-wrap align-end ga-3">
            <v-btn-toggle
              :model-value="rangeMode === 'custom' ? 'custom' : timeRange"
              mandatory
              density="compact"
              color="primary"
              variant="outlined"
              divided
              @update:model-value="onPresetChange"
            >
              <v-btn
                v-for="p in rangePresets"
                :key="p.value"
                :value="p.value"
                size="small"
              >
                {{ p.title }}
              </v-btn>
            </v-btn-toggle>

            <template v-if="rangeMode === 'custom'">
              <v-text-field
                v-model="customFromLocal"
                type="datetime-local"
                density="compact"
                hide-details
                :label="t('siemCenter.hostDashboard.from')"
                style="max-width: 220px"
              />
              <v-text-field
                v-model="customToLocal"
                type="datetime-local"
                density="compact"
                hide-details
                :label="t('siemCenter.hostDashboard.to')"
                style="max-width: 220px"
              />
              <v-btn color="primary" variant="flat" size="small" @click="applyFilters">
                {{ t('siemCenter.hostDashboard.applyRange') }}
              </v-btn>
            </template>
          </div>
        </div>
      </v-card>

      <v-alert v-if="error && bundle" type="warning" variant="tonal" density="compact" class="mb-4">
        {{ error }}
      </v-alert>

      <AcSiemHostKpiStrip
        v-if="bundle"
        class="mb-4"
        :kpis="bundle.kpis"
        :loading="analyticsLoading"
      />
      <v-skeleton-loader v-else-if="analyticsLoading" type="card@6" class="mb-4" />

      <AcSiemHostResourceCharts
        v-if="bundle"
        class="mb-4"
        :metrics="bundle.metrics"
        :loading="analyticsLoading"
      />

      <v-row dense class="mb-4">
        <v-col cols="12" md="5">
          <AcSiemHostSessionsCard
            v-if="bundle"
            :host="host"
            :session-history="bundle.sessionHistory"
            :range="bundle.range"
            :loading="analyticsLoading"
            :events-href="eventLogEventsHref"
          />
        </v-col>
        <v-col cols="12" md="7">
          <AcSiemHostWatchSummary
            v-if="bundle"
            :apps="bundle.apps"
            :activity="bundle.activity"
            :loading="analyticsLoading"
            :events-href="watchEventsHref"
          />
        </v-col>
      </v-row>

      <AcSiemHostEventLogSummary
        v-if="bundle"
        :channel-counts="bundle.channelCounts"
        :level-counts="bundle.levelCounts"
        :events="bundle.eventLogItems"
        :loading="analyticsLoading"
        :events-href="eventLogEventsHref"
      />
    </template>
  </div>
</template>

<style scoped>
.host-sticky-bar {
  position: sticky;
  top: 0;
  z-index: 2;
  background: rgb(var(--v-theme-surface));
}
</style>
