<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventListItem, SecEventRangeMode, SecEventTimeRange } from '@/types/apps/secEvent';
import { secEventQuery, secEventGet } from '@/services/secEventService';
import {
  actionColor,
  buildSecEventQueryRange,
  computeSecEventListStats,
  computePresetRangeFrom,
  formatActiveRangeLabel,
  formatRelativeTime,
  fromDatetimeLocalInput,
  getScenarioIdForAction,
  isValidCustomRange,
  SEC_EVENT_ACTION_OPTIONS,
  SEC_EVENT_DEFAULT_PAGE_SIZE,
  SEC_EVENT_FILTER_PRESETS,
  SEC_EVENT_PAGE_SIZE_OPTIONS,
  sourceTypeLabelKey,
  toDatetimeLocalInput,
} from '@/composables/useSecEventList';
import {
  loadSecEventsRefreshIntervalSec,
  saveSecEventsRefreshIntervalSec,
  SEC_EVENTS_REFRESH_INTERVALS_SEC,
  type SecEventsRefreshIntervalSec,
} from '@/composables/useSecEventsRefresh';
import AcSecEventDetailPanel from '@/components/apps/siem-center/AcSecEventDetailPanel.vue';

const { t, locale } = useAppI18n();
const route = useRoute();
const router = useRouter();
const { lgAndUp } = useDisplay();

const loading = ref(true);
const errorLocal = ref<string | null>(null);
const rows = ref<SecEventListItem[]>([]);
const total = ref(0);
const selectedId = ref<string | null>(null);
const selected = ref<SecEventListItem | null>(null);
const drawerOpen = ref(false);
const detailLoading = ref(false);
const lastRefreshedAt = ref<number | null>(null);
const listReady = ref(false);
const suppressPageWatch = ref(false);

const page = ref(1);
const itemsPerPage = ref(SEC_EVENT_DEFAULT_PAGE_SIZE);

const search = ref('');
const sourceType = ref<string | null>(null);
const eventAction = ref<string | null>(null);
const rangeMode = ref<SecEventRangeMode>('preset');
const timeRange = ref<SecEventTimeRange>('24h');
const customFromLocal = ref('');
const customToLocal = ref('');
const showUnknown = ref(false);

const autoRefreshIntervalSec = ref<SecEventsRefreshIntervalSec>(loadSecEventsRefreshIntervalSec());
let autoRefreshTimer: ReturnType<typeof setInterval> | null = null;

const VALID_TIME_RANGES: SecEventTimeRange[] = ['1h', '24h', '7d'];

const skip = computed(() => (page.value - 1) * itemsPerPage.value);

const listStats = computed(() => computeSecEventListStats(rows.value, total.value, skip.value));

const activeRangeLabel = computed(() =>
  formatActiveRangeLabel(
    rangeMode.value,
    timeRange.value,
    fromDatetimeLocalInput(customFromLocal.value),
    fromDatetimeLocalInput(customToLocal.value),
    locale.value,
    t,
  ),
);

const autoRefreshOptions = computed(() =>
  SEC_EVENTS_REFRESH_INTERVALS_SEC.map((sec) => ({
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
    const time = new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    }).format(new Date(lastRefreshedAt.value));
    return t('siemCenter.dashboard.lastRefreshed', { time });
  } catch {
    return '';
  }
});

const sourceTypeItems = computed(() => [
  { title: t('siemCenter.events.filterAll'), value: null },
  { title: t('siemCenter.events.sourceFirewall'), value: 'firewall' },
  { title: t('siemCenter.events.sourceAd'), value: 'ad' },
  { title: t('siemCenter.events.sourceEndpoint'), value: 'endpoint' },
  { title: t('siemCenter.events.sourceMetric'), value: 'metric' },
  { title: t('siemCenter.events.sourceWindowsEventLog'), value: 'windows-eventlog' },
]);

const eventActionItems = computed(() => [
  { title: t('siemCenter.events.filterAll'), value: null },
  ...SEC_EVENT_ACTION_OPTIONS.map((opt) => ({
    title: t(opt.labelKey),
    value: opt.value,
  })),
]);

const timeRangeItems = computed(() => [
  { title: t('siemCenter.events.range1h'), value: '1h' as SecEventTimeRange },
  { title: t('siemCenter.events.range24h'), value: '24h' as SecEventTimeRange },
  { title: t('siemCenter.events.range7d'), value: '7d' as SecEventTimeRange },
  { title: t('siemCenter.events.rangeCustom'), value: 'custom' as const },
]);

const headers = computed(() => [
  { title: t('siemCenter.events.colTime'), key: 'timestamp', sortable: false },
  { title: t('siemCenter.events.colEvent'), key: 'event', sortable: false },
  { title: t('siemCenter.events.colActorNet'), key: 'actorNet', sortable: false },
  { title: t('siemCenter.events.colSource'), key: 'source', sortable: false },
]);

const activePresetKey = computed(() => {
  if (!eventAction.value) return null;
  return SEC_EVENT_FILTER_PRESETS.find((p) => p.eventAction === eventAction.value)?.key ?? null;
});

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

function relativeTime(value?: string | null): string {
  return formatRelativeTime(value, locale.value, t);
}

function actionLabel(action: string): string {
  const key = `siemCenter.events.actions.${action}`;
  const translated = t(key);
  return translated !== key ? translated : action;
}

function initCustomRangeFromPreset() {
  const fromIso = computePresetRangeFrom(timeRange.value);
  const toIso = new Date().toISOString();
  customFromLocal.value = toDatetimeLocalInput(fromIso);
  customToLocal.value = toDatetimeLocalInput(toIso);
}

function resolveQueryRange(): { from: string; to?: string } | null {
  if (rangeMode.value === 'custom') {
    const from = fromDatetimeLocalInput(customFromLocal.value);
    const to = fromDatetimeLocalInput(customToLocal.value) ?? new Date().toISOString();
    if (!from) {
      errorLocal.value = t('siemCenter.events.invalidDateRange');
      return null;
    }
    if (!isValidCustomRange(from, to)) {
      errorLocal.value = t('siemCenter.events.invalidDateRange');
      return null;
    }
    return { from, to };
  }
  return buildSecEventQueryRange('preset', timeRange.value);
}

function onTimeRangeSelect(value: SecEventTimeRange | 'custom') {
  if (value === 'custom') {
    rangeMode.value = 'custom';
    if (!customFromLocal.value || !customToLocal.value) initCustomRangeFromPreset();
    return;
  }
  rangeMode.value = 'preset';
  timeRange.value = value;
}

function syncQueryToUrl() {
  const query: Record<string, string> = {};
  const searchText = (search.value ?? '').trim();
  if (searchText) query.search = searchText;
  if (sourceType.value) query.sourceType = sourceType.value;
  if (eventAction.value) query.eventAction = eventAction.value;
  if (showUnknown.value) query.showUnknown = '1';

  if (rangeMode.value === 'custom') {
    const from = fromDatetimeLocalInput(customFromLocal.value);
    const to = fromDatetimeLocalInput(customToLocal.value);
    if (from) query.from = from;
    if (to) query.to = to;
  } else if (timeRange.value !== '24h') {
    query.timeRange = timeRange.value;
  }

  void router.replace({ query });
}

function applyFromRoute() {
  const q = route.query;
  search.value = typeof q.search === 'string' ? q.search : '';
  sourceType.value = typeof q.sourceType === 'string' ? q.sourceType : null;
  eventAction.value = typeof q.eventAction === 'string' ? q.eventAction : null;
  showUnknown.value = q.showUnknown === '1' || q.showUnknown === 'true';

  const routeFrom = typeof q.from === 'string' ? q.from : null;
  const routeTo = typeof q.to === 'string' ? q.to : null;
  if (routeFrom && routeTo && isValidCustomRange(routeFrom, routeTo)) {
    rangeMode.value = 'custom';
    customFromLocal.value = toDatetimeLocalInput(routeFrom);
    customToLocal.value = toDatetimeLocalInput(routeTo);
    return;
  }

  rangeMode.value = 'preset';
  const tr = typeof q.timeRange === 'string' ? q.timeRange : '24h';
  timeRange.value = VALID_TIME_RANGES.includes(tr as SecEventTimeRange)
    ? (tr as SecEventTimeRange)
    : '24h';
}

function applyPreset(preset: (typeof SEC_EVENT_FILTER_PRESETS)[number]) {
  eventAction.value = preset.eventAction;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function clearFilters() {
  search.value = '';
  sourceType.value = null;
  eventAction.value = null;
  rangeMode.value = 'preset';
  timeRange.value = '24h';
  customFromLocal.value = '';
  customToLocal.value = '';
  showUnknown.value = false;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function hasActiveFilters(): boolean {
  return !!(
    (search.value ?? '').trim() ||
    sourceType.value ||
    eventAction.value ||
    rangeMode.value !== 'preset' ||
    timeRange.value !== '24h' ||
    showUnknown.value
  );
}

async function loadRows(options: { syncUrl?: boolean; resetSelection?: boolean; silent?: boolean } = {}) {
  const { syncUrl = false, resetSelection = false, silent = false } = options;
  const range = resolveQueryRange();
  if (!range) return;

  if (!silent) loading.value = true;
  errorLocal.value = null;
  if (syncUrl) syncQueryToUrl();

  try {
    const res = await secEventQuery({
      from: range.from,
      to: range.to,
      sourceType: sourceType.value ?? undefined,
      eventAction: eventAction.value ?? undefined,
      search: (search.value ?? '').trim() || undefined,
      excludeUnknown: !showUnknown.value,
      skip: skip.value,
      limit: itemsPerPage.value,
    });
    rows.value = res.items;
    total.value = res.total;
    lastRefreshedAt.value = Date.now();

    const maxPage = Math.max(1, Math.ceil(total.value / itemsPerPage.value));
    if (page.value > maxPage) {
      suppressPageWatch.value = true;
      page.value = maxPage;
      suppressPageWatch.value = false;
      if (total.value > 0) {
        await loadRows({ syncUrl, resetSelection, silent });
        return;
      }
    }

    if (rows.value.length === 0) {
      if (resetSelection) {
        selectedId.value = null;
        selected.value = null;
      }
      return;
    }

    if (resetSelection) {
      const keepId =
        selectedId.value && rows.value.some((r) => r.id === selectedId.value)
          ? selectedId.value
          : rows.value[0].id;
      const item = rows.value.find((r) => r.id === keepId)!;
      openDetail(item);
    }
  } catch (e: unknown) {
    if (!silent) {
      errorLocal.value = e instanceof Error ? e.message : t('siemCenter.events.loadError');
    }
    rows.value = [];
    total.value = 0;
    if (resetSelection) {
      selectedId.value = null;
      selected.value = null;
    }
  } finally {
    if (!silent) loading.value = false;
  }
}

function applyFilters() {
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function refreshList() {
  void loadRows({ resetSelection: false });
}

function openDetail(item: SecEventListItem) {
  selectedId.value = item.id;
  selected.value = item;
  if (!lgAndUp.value) drawerOpen.value = true;
  detailLoading.value = true;
  void secEventGet(item.id)
    .then((detail) => {
      if (selectedId.value === item.id) selected.value = detail;
    })
    .catch(() => {
      /* liste satırı ile devam */
    })
    .finally(() => {
      detailLoading.value = false;
    });
}

function onTableRowClick(_event: Event, ctx: { item: SecEventListItem }) {
  openDetail(ctx.item);
}

function tableRowProps(data: { item: SecEventListItem }) {
  return {
    class: data.item.id === selectedId.value ? 'ac-events-table__row--selected' : '',
  };
}

function actorNetSummary(item: SecEventListItem): string {
  const user = item.actorUser?.trim();
  const src = item.networkSrcIp?.trim();
  const dst = item.networkDstIp?.trim();
  if (user && src && dst) return `${user} · ${src} → ${dst}`;
  if (user && src) return `${user} · ${src}`;
  if (src && dst) return `${src} → ${dst}`;
  return user || src || dst || '—';
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
    void loadRows({ resetSelection: false, silent: true });
  }, autoRefreshIntervalSec.value * 1000);
}

function onVisibilityChange() {
  if (typeof document === 'undefined') return;
  if (document.visibilityState === 'visible' && autoRefreshIntervalSec.value > 0) {
    void loadRows({ resetSelection: false, silent: true });
    startAutoRefresh();
  } else {
    stopAutoRefresh();
  }
}

watch(lgAndUp, (wide) => {
  if (wide) drawerOpen.value = false;
});

watch(autoRefreshIntervalSec, (sec) => {
  saveSecEventsRefreshIntervalSec(sec);
  startAutoRefresh();
});

watch([page, itemsPerPage], () => {
  if (!listReady.value || suppressPageWatch.value) return;
  void loadRows({ resetSelection: false });
});

onMounted(() => {
  applyFromRoute();
  listReady.value = true;
  void loadRows({ resetSelection: true });
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
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <!-- Summary strip -->
    <v-row dense class="mb-4">
      <v-col cols="6" sm="3">
        <v-card variant="tonal" color="primary" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">
            <template v-if="listStats.total === 0">0</template>
            <template v-else>{{ listStats.pageFrom }}–{{ listStats.pageTo }}</template>
          </div>
          <div class="text-caption">{{ t('siemCenter.events.statPageRange') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card variant="tonal" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">{{ listStats.total }}</div>
          <div class="text-caption">{{ t('siemCenter.events.statMatching') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card variant="tonal" color="error" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">{{ listStats.failureLike }}</div>
          <div class="text-caption">{{ t('siemCenter.events.statFailurePage') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card variant="tonal" color="info" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">{{ listStats.sourceTypes }}</div>
          <div class="text-caption">{{ t('siemCenter.events.statSourcesPage') }}</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Scenario presets -->
    <div class="mb-4">
      <div class="text-caption text-medium-emphasis mb-2">{{ t('siemCenter.events.presets') }}</div>
      <div class="d-flex flex-wrap gap-2">
        <v-chip
          v-for="preset in SEC_EVENT_FILTER_PRESETS"
          :key="preset.key"
          size="small"
          variant="tonal"
          :color="activePresetKey === preset.key ? 'primary' : undefined"
          @click="applyPreset(preset)"
        >
          <span class="font-weight-bold mr-1">{{ preset.scenarioId }}</span>
          <span class="text-medium-emphasis">{{ t(`siemCenter.scenarios.${preset.scenarioId}.title`) }}</span>
        </v-chip>
      </div>
    </div>

    <!-- Filters -->
    <v-card variant="outlined" class="rounded-lg pa-3 pa-md-4 mb-4">
      <v-row dense>
        <v-col cols="12" md="4">
          <v-text-field
            v-model="search"
            :label="t('siemCenter.events.search')"
            :placeholder="t('siemCenter.events.searchPlaceholder')"
            prepend-inner-icon="mdi-magnify"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            @keyup.enter="applyFilters"
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            :model-value="rangeMode === 'custom' ? 'custom' : timeRange"
            :items="timeRangeItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.events.timeRange')"
            variant="outlined"
            density="compact"
            hide-details
            @update:model-value="onTimeRangeSelect"
          />
        </v-col>
        <v-col v-if="rangeMode === 'custom'" cols="6" md="2">
          <v-text-field
            v-model="customFromLocal"
            type="datetime-local"
            :label="t('siemCenter.events.dateFrom')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col v-if="rangeMode === 'custom'" cols="6" md="2">
          <v-text-field
            v-model="customToLocal"
            type="datetime-local"
            :label="t('siemCenter.events.dateTo')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            v-model="sourceType"
            :items="sourceTypeItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.events.colSource')"
            variant="outlined"
            density="compact"
            hide-details
            clearable
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            v-model="eventAction"
            :items="eventActionItems"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.events.colAction')"
            variant="outlined"
            density="compact"
            hide-details
            clearable
          />
        </v-col>
        <v-col cols="6" md="2" class="d-flex align-center gap-1">
          <v-checkbox
            v-model="showUnknown"
            :label="t('siemCenter.events.showUnknown')"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="12" class="d-flex flex-wrap align-center gap-2">
          <v-btn color="primary" prepend-icon="mdi-filter" :loading="loading" @click="applyFilters">
            {{ t('siemCenter.events.apply') }}
          </v-btn>
          <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="refreshList">
            {{ t('siemCenter.events.refresh') }}
          </v-btn>
          <v-btn v-if="hasActiveFilters()" variant="text" prepend-icon="mdi-filter-off" @click="clearFilters">
            {{ t('siemCenter.events.clearFilters') }}
          </v-btn>
          <v-select
            v-model="autoRefreshIntervalSec"
            :items="autoRefreshOptions"
            item-title="title"
            item-value="value"
            :label="t('siemCenter.dashboard.autoRefresh')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 10rem"
            class="ml-sm-2"
          />
          <span v-if="lastRefreshedLabel" class="text-caption text-medium-emphasis">
            {{ lastRefreshedLabel }}
          </span>
          <v-chip variant="tonal" size="small" class="ml-auto align-self-center">
            {{ t('siemCenter.events.statTotal', { shown: listStats.shown, total: listStats.total }) }}
            · {{ activeRangeLabel }}
          </v-chip>
        </v-col>
      </v-row>
    </v-card>

    <!-- Empty -->
    <v-card v-if="!loading && rows.length === 0" variant="outlined" class="rounded-lg pa-8 text-center">
      <v-icon icon="mdi-shield-off-outline" size="48" color="primary" class="mb-3 opacity-60" />
      <div class="text-h6 font-weight-bold mb-2">{{ t('siemCenter.events.empty') }}</div>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('siemCenter.events.emptyHint') }}</p>
    </v-card>

    <!-- Table + detail -->
    <v-row v-else>
      <v-col cols="12" :lg="selected && lgAndUp ? 8 : 12">
        <v-data-table-server
          v-model:page="page"
          v-model:items-per-page="itemsPerPage"
          :headers="headers"
          :items="rows"
          :items-length="total"
          :items-per-page-options="[...SEC_EVENT_PAGE_SIZE_OPTIONS]"
          :loading="loading"
          :row-props="tableRowProps"
          item-value="id"
          class="rounded-lg ac-events-table"
          density="comfortable"
          hover
          @click:row="onTableRowClick"
        >
          <template #item.timestamp="{ item }">
            <div>
              <div class="text-body-2">{{ formatDate(item.timestamp) }}</div>
              <div class="text-caption text-medium-emphasis">{{ relativeTime(item.timestamp) }}</div>
            </div>
          </template>
          <template #item.event="{ item }">
            <div class="d-flex flex-wrap align-center gap-1">
              <v-chip v-if="getScenarioIdForAction(item.eventAction)" size="x-small" color="primary" variant="flat">
                {{ getScenarioIdForAction(item.eventAction) }}
              </v-chip>
              <v-chip size="small" :color="actionColor(item.eventAction)" variant="tonal">
                {{ actionLabel(item.eventAction) }}
              </v-chip>
              <v-chip v-if="item.baselineNewFlowPair" size="x-small" color="info" variant="flat">U7</v-chip>
            </div>
          </template>
          <template #item.actorNet="{ item }">
            <span class="text-body-2">{{ actorNetSummary(item) }}</span>
          </template>
          <template #item.source="{ item }">
            <div>
              <div class="text-body-2">{{ item.sourceType ? t(sourceTypeLabelKey(item.sourceType)) : '—' }}</div>
              <div v-if="item.sourceHost" class="text-caption text-medium-emphasis text-truncate" style="max-width: 12rem">
                {{ item.sourceHost }}
              </div>
            </div>
          </template>
        </v-data-table-server>
      </v-col>

      <v-col v-if="selected && lgAndUp" cols="12" lg="4">
        <AcSecEventDetailPanel
          :event="selected"
          :loading="detailLoading"
        />
      </v-col>
    </v-row>

    <!-- Mobile drawer -->
    <v-navigation-drawer v-if="!lgAndUp" v-model="drawerOpen" location="right" width="100%" temporary class="ac-events-drawer">
      <AcSecEventDetailPanel
        v-if="selected"
        :event="selected"
        :loading="detailLoading"
        @close="drawerOpen = false"
      />
    </v-navigation-drawer>
  </div>
</template>

<style scoped>
.ac-events-table :deep(.ac-events-table__row--selected) {
  background: rgba(var(--v-theme-primary), 0.06);
}

.ac-events-drawer :deep(.v-navigation-drawer__content) {
  overflow-y: auto;
}
</style>
