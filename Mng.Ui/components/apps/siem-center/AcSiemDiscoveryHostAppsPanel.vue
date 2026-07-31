<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  DISCOVERY_APPS_STALE_MS,
  fetchDiscoveryHostApps,
  fetchDiscoveryHostWatchActivity,
  hostWatchActivityEventsLink,
  hostWatchEventsLink,
  watchActivityTone,
  watchHealthTone,
  type DiscoveryHostAppsSnapshot,
  type DiscoveryWatchActivitySnapshot,
  type DiscoveryWatchTarget,
} from '@/composables/useSiemDiscoveryHostApps';

const props = defineProps<{
  hostname: string;
  staleMs?: number;
}>();

const { t, locale } = useAppI18n();

const innerTab = ref<'status' | 'activity'>('status');
const loading = ref(false);
const loadingActivity = ref(false);
const error = ref<string | null>(null);
const activityError = ref<string | null>(null);
const snap = ref<DiscoveryHostAppsSnapshot | null>(null);
const activity = ref<DiscoveryWatchActivitySnapshot | null>(null);
const loadedFor = ref<string | null>(null);
const activityLoadedFor = ref<string | null>(null);
const kindFilter = ref<'all' | 'application' | 'service'>('all');
const activityKindFilter = ref<'all' | 'application' | 'service'>('all');
const activityPage = ref(1);
const activityItemsPerPage = ref(10);
const activitySortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([
  { key: 'at', order: 'desc' },
]);

const activityHeaders = computed(() => [
  {
    title: t('siemCenter.discovery.hostDetail.appsColTime'),
    key: 'at',
    sortable: true,
  },
  {
    title: t('siemCenter.discovery.hostDetail.appsColKind'),
    key: 'watchKind',
    sortable: true,
  },
  {
    title: t('siemCenter.discovery.hostDetail.appsColName'),
    key: 'name',
    sortable: true,
  },
  {
    title: t('siemCenter.discovery.hostDetail.appsColAction'),
    key: 'action',
    sortable: true,
  },
  {
    title: t('siemCenter.discovery.hostDetail.appsColStatus'),
    key: 'detail',
    sortable: true,
  },
]);

const ACTIVITY_PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

const staleThreshold = computed(() =>
  props.staleMs != null && props.staleMs > 0 ? props.staleMs : DISCOVERY_APPS_STALE_MS,
);

const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const eventsHref = computed(() =>
  innerTab.value === 'activity'
    ? hostWatchActivityEventsLink(props.hostname)
    : hostWatchEventsLink(props.hostname),
);

const isStale = computed(() => {
  const at = snap.value?.at;
  if (at == null) return false;
  return Date.now() - at > staleThreshold.value;
});

const filteredTargets = computed(() => {
  const list = snap.value?.targets ?? [];
  if (kindFilter.value === 'all') return list;
  return list.filter((x) => x.kind === kindFilter.value);
});

const filteredActivity = computed(() => {
  const list = activity.value?.items ?? [];
  if (activityKindFilter.value === 'all') return list;
  return list.filter((x) => x.watchKind === activityKindFilter.value);
});

watch(activityKindFilter, () => {
  activityPage.value = 1;
});

watch(
  () => activity.value?.items.length,
  () => {
    activityPage.value = 1;
  },
);

const hasAny = computed(() => (snap.value?.targets.length ?? 0) > 0);
const hasActivity = computed(() => (activity.value?.items.length ?? 0) > 0);

function formatTs(value: string | number | null | undefined): string {
  if (value == null || value === '') return '—';
  const ms = typeof value === 'number' ? value : Date.parse(value);
  if (!Number.isFinite(ms)) return String(value);
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(ms));
  } catch {
    return String(value);
  }
}

function ageLabel(ms: number | null | undefined): string | null {
  if (ms == null) return null;
  const ageSec = Math.max(0, Math.round((Date.now() - ms) / 1000));
  if (ageSec < 60) return t('siemCenter.discovery.hostDetail.ageSeconds', { n: ageSec });
  const ageMin = Math.round(ageSec / 60);
  return t('siemCenter.discovery.hostDetail.ageMinutes', { n: ageMin });
}

function kindLabel(kind: string): string {
  if (kind === 'application') return t('siemCenter.discovery.hostDetail.appsKindApp');
  if (kind === 'service') return t('siemCenter.discovery.hostDetail.appsKindService');
  return kind;
}

function healthLabel(h?: string | null): string {
  if (!h) return '—';
  const key = `siemCenter.discovery.hostDetail.appsHealth.${h}`;
  const translated = t(key);
  return translated === key ? h : translated;
}

function actionLabel(action: string): string {
  const key = `siemCenter.discovery.hostDetail.appsAction.${action}`;
  const translated = t(key);
  return translated === key ? action : translated;
}

function restartSummary(row: DiscoveryWatchTarget): string {
  if (!row.restartAllowed) return t('siemCenter.discovery.hostDetail.appsRestartOff');
  if (row.lastRestartOk === true) return t('siemCenter.discovery.hostDetail.appsRestartOk');
  if (row.lastRestartOk === false) return t('siemCenter.discovery.hostDetail.appsRestartFail');
  return t('siemCenter.discovery.hostDetail.appsRestartOn');
}

async function loadStatus(force = false) {
  const host = props.hostname.trim();
  if (!host) return;
  if (!force && loadedFor.value === host && snap.value) return;

  loading.value = true;
  error.value = null;
  try {
    snap.value = await fetchDiscoveryHostApps(host);
    loadedFor.value = host;
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    snap.value = null;
    loadedFor.value = null;
  } finally {
    loading.value = false;
  }
}

async function loadActivity(force = false) {
  const host = props.hostname.trim();
  if (!host) return;
  if (!force && activityLoadedFor.value === host && activity.value) return;

  loadingActivity.value = true;
  activityError.value = null;
  try {
    activity.value = await fetchDiscoveryHostWatchActivity(host);
    activityLoadedFor.value = host;
  } catch (e: unknown) {
    activityError.value = e instanceof Error ? e.message : String(e);
    activity.value = null;
    activityLoadedFor.value = null;
  } finally {
    loadingActivity.value = false;
  }
}

async function refresh() {
  if (innerTab.value === 'activity') {
    await loadActivity(true);
  } else {
    await loadStatus(true);
  }
}

watch(
  () => props.hostname,
  () => {
    snap.value = null;
    activity.value = null;
    loadedFor.value = null;
    activityLoadedFor.value = null;
    kindFilter.value = 'all';
    activityKindFilter.value = 'all';
    activityPage.value = 1;
    activitySortBy.value = [{ key: 'at', order: 'desc' }];
    innerTab.value = 'status';
    void loadStatus(true);
  },
  { immediate: true },
);

watch(innerTab, (tab) => {
  if (tab === 'activity') void loadActivity(false);
});
</script>

<template>
  <div class="host-apps-panel">
    <div class="d-flex align-center flex-wrap ga-2 px-4 pt-3 pb-1">
      <v-tabs v-model="innerTab" density="compact" color="primary" class="flex-grow-1">
        <v-tab value="status">{{ t('siemCenter.discovery.hostDetail.appsTabStatus') }}</v-tab>
        <v-tab value="activity">{{ t('siemCenter.discovery.hostDetail.appsTabActivity') }}</v-tab>
      </v-tabs>
      <v-btn
        size="small"
        variant="text"
        prepend-icon="mdi-refresh"
        :loading="innerTab === 'activity' ? loadingActivity : loading"
        @click="refresh"
      >
        {{ t('siemCenter.discovery.hostDetail.appsRefresh') }}
      </v-btn>
      <v-btn
        size="small"
        variant="text"
        prepend-icon="mdi-timeline-text-outline"
        :to="eventsHref"
      >
        {{
          innerTab === 'activity'
            ? t('siemCenter.discovery.hostDetail.appsOpenActivityEvents')
            : t('siemCenter.discovery.hostDetail.appsOpenEvents')
        }}
      </v-btn>
    </div>

    <v-tabs-window v-model="innerTab">
      <v-tabs-window-item value="status">
        <div class="d-flex align-center flex-wrap ga-2 px-4 pb-1">
          <v-btn-toggle
            v-model="kindFilter"
            mandatory
            density="compact"
            color="primary"
            variant="outlined"
          >
            <v-btn value="all" size="small">{{ t('siemCenter.discovery.hostDetail.appsFilterAll') }}</v-btn>
            <v-btn value="application" size="small">{{ t('siemCenter.discovery.hostDetail.appsKindApp') }}</v-btn>
            <v-btn value="service" size="small">{{ t('siemCenter.discovery.hostDetail.appsKindService') }}</v-btn>
          </v-btn-toggle>
        </div>

        <div class="pa-4 pt-2">
          <v-alert v-if="error" type="warning" variant="tonal" density="compact" class="mb-3">
            {{ error }}
          </v-alert>

          <v-skeleton-loader v-if="loading && !snap" type="table" />

          <template v-else-if="!hasAny">
            <v-sheet border rounded class="pa-3 text-medium-emphasis text-body-2">
              {{ t('siemCenter.discovery.hostDetail.appsEmpty') }}
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
              {{ t('siemCenter.discovery.hostDetail.appsStale') }}
              <span v-if="snap?.at" class="ms-1">
                ({{ formatTs(snap.at) }}
                <span v-if="ageLabel(snap.at)"> · {{ ageLabel(snap.at) }}</span>)
              </span>
            </v-alert>

            <div class="d-flex flex-wrap ga-3 mb-3 text-caption text-medium-emphasis">
              <span>
                {{ t('siemCenter.discovery.hostDetail.appsLastSample') }}:
                {{ formatTs(snap?.at) }}
                <span v-if="ageLabel(snap?.at)"> ({{ ageLabel(snap?.at) }})</span>
              </span>
              <span v-if="snap?.healthyCount != null">
                · {{ t('siemCenter.discovery.hostDetail.appsHealthy', { n: snap.healthyCount }) }}
              </span>
              <span v-if="snap?.unhealthyCount != null">
                · {{ t('siemCenter.discovery.hostDetail.appsUnhealthy', { n: snap.unhealthyCount }) }}
              </span>
            </div>

            <v-sheet v-if="!filteredTargets.length" border rounded class="pa-3 text-medium-emphasis text-body-2">
              {{ t('siemCenter.discovery.hostDetail.appsFilterEmpty') }}
            </v-sheet>

            <v-table v-else density="compact" class="apps-table">
              <thead>
                <tr>
                  <th class="text-left">{{ t('siemCenter.discovery.hostDetail.appsColKind') }}</th>
                  <th class="text-left">{{ t('siemCenter.discovery.hostDetail.appsColName') }}</th>
                  <th class="text-left">{{ t('siemCenter.discovery.hostDetail.appsColHealth') }}</th>
                  <th class="text-left">{{ t('siemCenter.discovery.hostDetail.appsColStatus') }}</th>
                  <th class="text-left">{{ t('siemCenter.discovery.hostDetail.appsColRestart') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in filteredTargets" :key="`${row.kind}-${row.name}`">
                  <td>
                    <v-chip size="x-small" variant="tonal" :color="row.kind === 'application' ? 'primary' : 'default'">
                      {{ kindLabel(row.kind) }}
                    </v-chip>
                  </td>
                  <td>
                    <div class="font-weight-medium font-mono text-break">{{ row.name }}</div>
                    <div v-if="row.displayName && row.displayName !== row.name" class="text-caption text-medium-emphasis">
                      {{ row.displayName }}
                    </div>
                  </td>
                  <td>
                    <v-chip size="x-small" variant="flat" :color="watchHealthTone(row.health)">
                      {{ healthLabel(row.health) }}
                    </v-chip>
                  </td>
                  <td class="font-mono text-body-2">{{ row.statusText || '—' }}</td>
                  <td class="text-body-2">
                    <div>{{ restartSummary(row) }}</div>
                    <div v-if="row.lastRestartAtUtc" class="text-caption text-medium-emphasis">
                      {{ formatTs(row.lastRestartAtUtc) }}
                      <span v-if="(row.restartAttemptCount || 0) > 0">
                        · {{ row.restartAttemptCount }}x
                      </span>
                    </div>
                  </td>
                </tr>
              </tbody>
            </v-table>
          </template>
        </div>
      </v-tabs-window-item>

      <v-tabs-window-item value="activity">
        <div class="d-flex align-center flex-wrap ga-2 px-4 pb-1">
          <v-btn-toggle
            v-model="activityKindFilter"
            mandatory
            density="compact"
            color="primary"
            variant="outlined"
          >
            <v-btn value="all" size="small">{{ t('siemCenter.discovery.hostDetail.appsFilterAll') }}</v-btn>
            <v-btn value="application" size="small">{{ t('siemCenter.discovery.hostDetail.appsKindApp') }}</v-btn>
            <v-btn value="service" size="small">{{ t('siemCenter.discovery.hostDetail.appsKindService') }}</v-btn>
          </v-btn-toggle>
          <span class="text-caption text-medium-emphasis">
            {{ t('siemCenter.discovery.hostDetail.appsActivityHint') }}
          </span>
        </div>

        <div class="pa-4 pt-2">
          <v-alert v-if="activityError" type="warning" variant="tonal" density="compact" class="mb-3">
            {{ activityError }}
          </v-alert>

          <v-skeleton-loader v-if="loadingActivity && !activity" type="table" />

          <template v-else-if="!hasActivity">
            <v-sheet border rounded class="pa-3 text-medium-emphasis text-body-2">
              {{ t('siemCenter.discovery.hostDetail.appsActivityEmpty') }}
            </v-sheet>
          </template>

          <template v-else>
            <div class="d-flex flex-wrap ga-3 mb-3 text-caption text-medium-emphasis">
              <span>
                {{ t('siemCenter.discovery.hostDetail.appsActivityCount', { n: filteredActivity.length }) }}
              </span>
              <span v-if="activity?.at">
                · {{ t('siemCenter.discovery.hostDetail.appsLastSample') }}:
                {{ formatTs(activity.at) }}
                <span v-if="ageLabel(activity.at)"> ({{ ageLabel(activity.at) }})</span>
              </span>
            </div>

            <v-data-table
              v-model:page="activityPage"
              v-model:items-per-page="activityItemsPerPage"
              v-model:sort-by="activitySortBy"
              :headers="activityHeaders"
              :items="filteredActivity"
              :loading="loadingActivity"
              item-value="id"
              density="compact"
              class="apps-activity-table rounded-lg"
              :items-per-page-options="ACTIVITY_PAGE_SIZE_OPTIONS"
              :no-data-text="t('siemCenter.discovery.hostDetail.appsFilterEmpty')"
            >
              <template #item.at="{ item }">
                <div class="text-body-2 text-no-wrap">
                  <div>{{ formatTs(item.at) }}</div>
                  <div v-if="ageLabel(item.at)" class="text-caption text-medium-emphasis">
                    {{ ageLabel(item.at) }}
                  </div>
                </div>
              </template>
              <template #item.watchKind="{ item }">
                <v-chip
                  size="x-small"
                  variant="tonal"
                  :color="item.watchKind === 'application' ? 'primary' : 'default'"
                >
                  {{ kindLabel(item.watchKind) }}
                </v-chip>
              </template>
              <template #item.name="{ item }">
                <div class="font-weight-medium font-mono text-break">{{ item.name }}</div>
                <div
                  v-if="item.displayName && item.displayName !== item.name"
                  class="text-caption text-medium-emphasis"
                >
                  {{ item.displayName }}
                </div>
              </template>
              <template #item.action="{ item }">
                <v-chip size="x-small" variant="flat" :color="watchActivityTone(item.action)">
                  {{ actionLabel(item.action) }}
                </v-chip>
              </template>
              <template #item.detail="{ item }">
                <span class="text-body-2 text-break">{{ item.detail || '—' }}</span>
              </template>
            </v-data-table>
          </template>
        </div>
      </v-tabs-window-item>
    </v-tabs-window>
  </div>
</template>

<style scoped>
.apps-table :deep(td),
.apps-table :deep(th) {
  border-bottom: thin solid rgba(var(--v-border-color), var(--v-border-opacity)) !important;
  padding-block: 8px !important;
  vertical-align: top;
}

.apps-activity-table :deep(td) {
  vertical-align: top;
}
</style>
