<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  watchActivityTone,
  watchHealthTone,
  type DiscoveryHostAppsSnapshot,
  type DiscoveryWatchActivitySnapshot,
  type DiscoveryWatchTarget,
} from '@/composables/useSiemDiscoveryHostApps';

const props = defineProps<{
  apps: DiscoveryHostAppsSnapshot;
  activity: DiscoveryWatchActivitySnapshot;
  loading?: boolean;
  eventsHref?: string;
}>();

const { t, locale } = useAppI18n();
const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const targetsPage = ref(1);
const targetsItemsPerPage = ref(10);
const TARGETS_PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

const activityPage = ref(1);
const activityItemsPerPage = ref(8);
const ACTIVITY_PAGE_SIZE_OPTIONS = [5, 8, 15, 25];

function isUnhealthy(row: DiscoveryWatchTarget): boolean {
  const h = (row.health || '').toLowerCase();
  return !!(h && h !== 'running' && h !== 'healthy');
}

/** Defined watch targets — unhealthy first, then kind/name. */
const targetsSorted = computed(() => {
  const list = [...(props.apps.targets ?? [])].map((row, i) => ({
    ...row,
    _rowKey: `${row.kind}:${row.name}:${i}`,
  }));
  list.sort((a, b) => {
    const ua = isUnhealthy(a) ? 0 : 1;
    const ub = isUnhealthy(b) ? 0 : 1;
    if (ua !== ub) return ua - ub;
    const ka = (a.kind || '').localeCompare(b.kind || '');
    if (ka !== 0) return ka;
    return (a.name || '').localeCompare(b.name || '');
  });
  return list;
});

const recentActivity = computed(() => props.activity.items ?? []);

const targetHeaders = computed(() => [
  { title: t('siemCenter.hostDashboard.colKind'), key: 'kind', sortable: true },
  { title: t('siemCenter.hostDashboard.colName'), key: 'name', sortable: true },
  { title: t('siemCenter.hostDashboard.colHealth'), key: 'health', sortable: true },
  { title: t('siemCenter.hostDashboard.colStatusDetail'), key: 'statusText', sortable: false },
]);

const activityHeaders = computed(() => [
  { title: t('siemCenter.hostDashboard.colTime'), key: 'at', sortable: true },
  { title: t('siemCenter.hostDashboard.colName'), key: 'name', sortable: true },
  { title: t('siemCenter.hostDashboard.colAction'), key: 'action', sortable: true },
]);

watch(
  () => props.apps.targets,
  () => {
    targetsPage.value = 1;
  },
);

watch(
  () => props.activity.items,
  () => {
    activityPage.value = 1;
  },
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
  if (ms == null || !Number.isFinite(ms)) return null;
  const ageSec = Math.max(0, Math.round((Date.now() - ms) / 1000));
  if (ageSec < 60) return t('siemCenter.hostDashboard.ageSeconds', { n: ageSec });
  const ageMin = Math.round(ageSec / 60);
  if (ageMin < 60) return t('siemCenter.hostDashboard.ageMinutes', { n: ageMin });
  const ageHr = Math.round(ageMin / 60);
  return t('siemCenter.hostDashboard.ageHours', { n: ageHr });
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
</script>

<template>
  <v-card variant="outlined" class="rounded-lg pa-4 h-100">
    <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-3">
      <div>
        <h3 class="text-subtitle-1 font-weight-bold mb-0">
          {{ t('siemCenter.hostDashboard.watchTitle') }}
        </h3>
        <p class="text-caption text-medium-emphasis mb-0">
          {{ t('siemCenter.hostDashboard.watchHint') }}
        </p>
      </div>
      <v-btn
        v-if="eventsHref"
        size="small"
        variant="text"
        :to="eventsHref"
        target="_blank"
        rel="noopener noreferrer"
        prepend-icon="mdi-open-in-new"
      >
        {{ t('siemCenter.hostDashboard.openEvents') }}
      </v-btn>
    </div>

    <v-skeleton-loader v-if="loading" type="list-item@4" />
    <template v-else>
      <div class="d-flex flex-wrap ga-2 mb-2 align-center">
        <v-chip size="small" color="success" variant="tonal">
          {{ t('siemCenter.hostDashboard.watchHealthy', { n: apps.healthyCount ?? 0 }) }}
        </v-chip>
        <v-chip
          size="small"
          :color="(apps.unhealthyCount ?? 0) > 0 ? 'warning' : 'default'"
          variant="tonal"
        >
          {{ t('siemCenter.hostDashboard.watchUnhealthy', { n: apps.unhealthyCount ?? 0 }) }}
        </v-chip>
        <span v-if="apps.at" class="text-caption text-medium-emphasis">
          {{ t('siemCenter.hostDashboard.watchInventoryAt') }}:
          {{ formatTs(apps.at) }}
          <template v-if="ageLabel(apps.at)"> ({{ ageLabel(apps.at) }})</template>
        </span>
      </div>

      <div class="text-subtitle-2 mb-1">
        {{ t('siemCenter.hostDashboard.watchTargetsTitle') }}
      </div>
      <p class="text-caption text-medium-emphasis mb-2">
        {{ t('siemCenter.hostDashboard.watchTargetsHint') }}
      </p>

      <div v-if="!targetsSorted.length" class="text-body-2 text-medium-emphasis mb-3">
        {{ t('siemCenter.hostDashboard.watchEmpty') }}
      </div>
      <v-data-table
        v-else
        v-model:page="targetsPage"
        v-model:items-per-page="targetsItemsPerPage"
        :headers="targetHeaders"
        :items="targetsSorted"
        item-value="_rowKey"
        density="compact"
        class="host-watch-table mb-3"
        :items-per-page-options="TARGETS_PAGE_SIZE_OPTIONS"
      >
        <template #item.kind="{ item }">
          <v-chip
            size="x-small"
            variant="tonal"
            :color="item.kind === 'application' ? 'primary' : 'default'"
          >
            {{ kindLabel(item.kind) }}
          </v-chip>
        </template>
        <template #item.name="{ item }">
          <div class="text-body-2 text-truncate" style="max-width: 12rem" :title="item.name">
            {{ item.name }}
          </div>
          <div
            v-if="item.displayName && item.displayName !== item.name"
            class="text-caption text-medium-emphasis text-truncate"
            style="max-width: 12rem"
          >
            {{ item.displayName }}
          </div>
        </template>
        <template #item.health="{ item }">
          <v-chip size="x-small" variant="flat" :color="watchHealthTone(item.health)">
            {{ healthLabel(item.health) }}
          </v-chip>
        </template>
        <template #item.statusText="{ item }">
          <span class="text-caption text-medium-emphasis text-truncate d-inline-block" style="max-width: 10rem">
            {{ item.statusText || '—' }}
          </span>
        </template>
      </v-data-table>

      <v-divider class="mb-3" />

      <div class="text-subtitle-2 mb-1">
        {{ t('siemCenter.hostDashboard.watchActivity') }}
      </div>
      <p class="text-caption text-medium-emphasis mb-2">
        {{ t('siemCenter.hostDashboard.watchActivityHint') }}
      </p>
      <div v-if="!recentActivity.length" class="text-body-2 text-medium-emphasis">
        {{ t('siemCenter.hostDashboard.watchActivityEmpty') }}
      </div>
      <v-data-table
        v-else
        v-model:page="activityPage"
        v-model:items-per-page="activityItemsPerPage"
        :headers="activityHeaders"
        :items="recentActivity"
        item-value="id"
        density="compact"
        class="host-watch-table"
        :items-per-page-options="ACTIVITY_PAGE_SIZE_OPTIONS"
      >
        <template #item.at="{ item }">
          <span class="text-no-wrap">{{ formatTs(item.at) }}</span>
        </template>
        <template #item.name="{ item }">
          <span class="text-truncate d-inline-block" style="max-width: 140px">
            {{ item.displayName || item.name }}
          </span>
        </template>
        <template #item.action="{ item }">
          <v-chip size="x-small" :color="watchActivityTone(item.action)" variant="tonal">
            {{ actionLabel(item.action) }}
          </v-chip>
        </template>
      </v-data-table>
    </template>
  </v-card>
</template>

<style scoped>
.host-watch-table :deep(td),
.host-watch-table :deep(th) {
  font-size: 0.75rem;
  vertical-align: middle;
}
</style>
