<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmRule, AlarmStatus, AlarmSummary } from '@/types/apps/alarm';
import {
  alarmAcknowledge,
  alarmGet,
  alarmListOpen,
  alarmResolve,
  alarmRuleGet,
  alarmSuppress,
} from '@/services/alarmService';
import {
  ALARM_DEFAULT_PAGE_SIZE,
  ALARM_PAGE_SIZE_OPTIONS,
  buildAlarmHistoryRange,
  computeAlarmListStats,
  computeHistoryRange,
  formatAlarmRelativeTime,
  formatAlarmScenarioLabel,
  formatAlarmSummary,
  getScenarioIdForAlarm,
  initAlarmCustomRangeFromDays,
  resolveAlarmListStatus,
  severityColor,
  statusColor,
  statusLabel,
  type AlarmHistoryRangeMode,
  type AlarmListView,
  type AlarmStatusFilter,
} from '@/composables/useAlarmList';
import { fromDatetimeLocalInput, toDatetimeLocalInput } from '@/composables/useSecEventList';
import {
  loadAlarmsRefreshIntervalSec,
  saveAlarmsRefreshIntervalSec,
  ALARMS_REFRESH_INTERVALS_SEC,
  type AlarmsRefreshIntervalSec,
} from '@/composables/useAlarmsRefresh';
import AcAlarmDetailPanel from '@/components/apps/alarm-center/AcAlarmDetailPanel.vue';

const { t, locale } = useAppI18n();
const route = useRoute();
const router = useRouter();
const { lgAndUp } = useDisplay();

const loading = ref(true);
const errorLocal = ref<string | null>(null);
const rows = ref<AlarmSummary[]>([]);
const total = ref(0);
const selectedId = ref<string | null>(null);
const selected = ref<AlarmSummary | null>(null);
const selectedRule = ref<AlarmRule | null>(null);
const selectedRuleName = ref<string | null>(null);
const drawerOpen = ref(false);
const detailLoading = ref(false);
const actionLoading = ref(false);
const lastRefreshedAt = ref<number | null>(null);
const listReady = ref(false);
const suppressPageWatch = ref(false);

const listView = ref<AlarmListView>('inbox');
const historyDays = ref(7);
const historyRangeMode = ref<AlarmHistoryRangeMode>('preset');
const customHistoryFromLocal = ref('');
const customHistoryToLocal = ref('');

const page = ref(1);
const itemsPerPage = ref(ALARM_DEFAULT_PAGE_SIZE);

const search = ref('');
const ruleIdFilter = ref<string | null>(null);
const statusFilter = ref<AlarmStatusFilter>('open');
const minSeverity = ref<number | null>(null);
const pendingAlarmId = ref<string | null>(null);

const autoRefreshIntervalSec = ref<AlarmsRefreshIntervalSec>(loadAlarmsRefreshIntervalSec());
let autoRefreshTimer: ReturnType<typeof setInterval> | null = null;

const skip = computed(() => (page.value - 1) * itemsPerPage.value);
const listStats = computed(() => computeAlarmListStats(rows.value, total.value, skip.value));

const autoRefreshOptions = computed(() =>
  ALARMS_REFRESH_INTERVALS_SEC.map((sec) => ({
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

const statusFilterItems = computed(() => {
  const items: { title: string; value: AlarmStatusFilter }[] = [];
  if (listView.value === 'inbox') {
    items.push({ title: t('alarmCenter.alarms.statusFilterOpen'), value: 'open' });
  }
  items.push(
    { title: t('alarmCenter.alarms.statusFilterAll'), value: 'all' },
    { title: t('alarmCenter.alarms.statusActive'), value: 'Active' },
    { title: t('alarmCenter.alarms.statusAcknowledged'), value: 'Acknowledged' },
    { title: t('alarmCenter.alarms.statusResolved'), value: 'Resolved' },
    { title: t('alarmCenter.alarms.statusSuppressed'), value: 'Suppressed' },
  );
  return items;
});

const minSeverityItems = computed(() => [
  { title: t('alarmCenter.alarms.severityAll'), value: null },
  { title: t('alarmCenter.alarms.severityMin5'), value: 5 },
  { title: t('alarmCenter.alarms.severityMin7'), value: 7 },
  { title: t('alarmCenter.alarms.severityMin8'), value: 8 },
]);

const headers = computed(() => [
  { title: t('alarmCenter.alarms.colSeverity'), key: 'severity', sortable: false },
  { title: t('siemCenter.dashboard.alarmColScenario'), key: 'scenario', sortable: false },
  { title: t('alarmCenter.alarms.colStatus'), key: 'status', sortable: false },
  { title: t('siemCenter.dashboard.alarmColSummary'), key: 'summary', sortable: false },
  { title: t('alarmCenter.alarms.colCount'), key: 'count', sortable: false },
  { title: t('alarmCenter.alarms.colLastSeen'), key: 'lastSeenAt', sortable: false },
]);

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
  return formatAlarmRelativeTime(value, locale.value, t);
}

function resolveHistoryRange(): { from: string; to: string } | null {
  return buildAlarmHistoryRange(
    historyRangeMode.value,
    historyDays.value,
    customHistoryFromLocal.value,
    customHistoryToLocal.value,
  );
}

function buildListQuery() {
  const isHistory = listView.value === 'history';
  const { openOnly, status } = resolveAlarmListStatus(statusFilter.value, isHistory);
  const historyRange = isHistory ? resolveHistoryRange() : null;

  return {
    openOnly: isHistory ? false : openOnly,
    status,
    minSeverity: minSeverity.value ?? undefined,
    ruleId: ruleIdFilter.value ?? undefined,
    search: search.value.trim() || undefined,
    from: historyRange?.from,
    to: historyRange?.to,
    skip: skip.value,
    limit: itemsPerPage.value,
  };
}

function switchListView(view: AlarmListView) {
  if (listView.value === view) return;
  listView.value = view;
  page.value = 1;
  if (view === 'history') {
    statusFilter.value = 'all';
    if (autoRefreshIntervalSec.value > 0) {
      autoRefreshIntervalSec.value = 0;
      saveAlarmsRefreshIntervalSec(0);
    }
  } else {
    statusFilter.value = 'open';
    historyRangeMode.value = 'preset';
    historyDays.value = 7;
    customHistoryFromLocal.value = '';
    customHistoryToLocal.value = '';
  }
  void loadRows({ syncUrl: true, resetSelection: true });
}

function syncQueryToUrl() {
  const query: Record<string, string> = {};
  if (listView.value === 'history') query.view = 'history';
  if (listView.value === 'history' && historyRangeMode.value === 'preset' && historyDays.value !== 7) {
    query.historyDays = String(historyDays.value);
  }
  if (listView.value === 'history' && historyRangeMode.value === 'custom') {
    const from = fromDatetimeLocalInput(customHistoryFromLocal.value);
    const to = fromDatetimeLocalInput(customHistoryToLocal.value);
    if (from) query.from = from;
    if (to) query.to = to;
  }
  if (search.value.trim()) query.search = search.value.trim();
  if (ruleIdFilter.value) query.ruleId = ruleIdFilter.value;
  if (selectedId.value) query.alarmId = selectedId.value;
  const defaultStatus = listView.value === 'history' ? 'all' : 'open';
  if (statusFilter.value !== defaultStatus) query.status = statusFilter.value;
  if (minSeverity.value != null) query.minSeverity = String(minSeverity.value);
  void router.replace({ query });
}

function applyFromRoute() {
  const q = route.query;
  listView.value = q.view === 'history' ? 'history' : 'inbox';

  if (typeof q.from === 'string' && q.from.trim()) {
    historyRangeMode.value = 'custom';
    customHistoryFromLocal.value = toDatetimeLocalInput(q.from.trim());
    customHistoryToLocal.value =
      typeof q.to === 'string' && q.to.trim() ? toDatetimeLocalInput(q.to.trim()) : toDatetimeLocalInput(new Date().toISOString());
  } else {
    historyRangeMode.value = 'preset';
    customHistoryFromLocal.value = '';
    customHistoryToLocal.value = '';
    if (typeof q.historyDays === 'string') {
      const parsed = Number.parseInt(q.historyDays, 10);
      historyDays.value = [7, 30, 90].includes(parsed) ? parsed : 7;
    } else {
      historyDays.value = 7;
    }
  }

  search.value = typeof q.search === 'string' ? q.search : '';
  ruleIdFilter.value = typeof q.ruleId === 'string' && q.ruleId.trim() ? q.ruleId.trim() : null;

  const defaultStatus = listView.value === 'history' ? 'all' : 'open';
  const st = typeof q.status === 'string' ? q.status : defaultStatus;
  const validStatuses: AlarmStatusFilter[] = ['open', 'all', 'Active', 'Acknowledged', 'Resolved', 'Suppressed'];
  statusFilter.value = validStatuses.includes(st as AlarmStatusFilter)
    ? (st as AlarmStatusFilter)
    : defaultStatus;

  if (typeof q.minSeverity === 'string') {
    const parsed = Number.parseInt(q.minSeverity, 10);
    minSeverity.value = Number.isFinite(parsed) ? parsed : null;
  } else {
    minSeverity.value = null;
  }

  pendingAlarmId.value = typeof q.alarmId === 'string' && q.alarmId.trim() ? q.alarmId.trim() : null;
}

async function tryOpenPendingAlarm() {
  const id = pendingAlarmId.value;
  if (!id) return;
  pendingAlarmId.value = null;

  const inRows = rows.value.find((r) => r.id === id);
  if (inRows) {
    openDetail(inRows);
    return;
  }

  try {
    const alarm = await alarmGet(id);
    openDetail(alarm);
  } catch {
    /* alarm listede yok veya erişilemiyor */
  }
}

function clearFilters() {
  search.value = '';
  ruleIdFilter.value = null;
  statusFilter.value = listView.value === 'history' ? 'all' : 'open';
  minSeverity.value = null;
  historyDays.value = 7;
  historyRangeMode.value = 'preset';
  customHistoryFromLocal.value = '';
  customHistoryToLocal.value = '';
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function hasActiveFilters(): boolean {
  const defaultStatus = listView.value === 'history' ? 'all' : 'open';
  return !!(
    search.value.trim() ||
    ruleIdFilter.value ||
    statusFilter.value !== defaultStatus ||
    minSeverity.value != null ||
    (listView.value === 'history' &&
      (historyRangeMode.value === 'custom' || historyDays.value !== 7))
  );
}

function setHistoryDays(days: number) {
  historyRangeMode.value = 'preset';
  historyDays.value = days;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: false });
}

function enableCustomHistoryRange() {
  historyRangeMode.value = 'custom';
  if (!customHistoryFromLocal.value || !customHistoryToLocal.value) {
    const seeded = initAlarmCustomRangeFromDays(historyDays.value);
    customHistoryFromLocal.value = seeded.fromLocal;
    customHistoryToLocal.value = seeded.toLocal;
  }
}

async function loadRows(options: { syncUrl?: boolean; resetSelection?: boolean; silent?: boolean } = {}) {
  const { syncUrl = false, resetSelection = false, silent = false } = options;

  if (listView.value === 'history' && !resolveHistoryRange()) {
    if (!silent) {
      errorLocal.value = t('siemCenter.events.invalidDateRange');
      loading.value = false;
    }
    return;
  }

  if (!silent) loading.value = true;
  errorLocal.value = null;
  if (syncUrl) syncQueryToUrl();

  try {
    const res = await alarmListOpen(buildListQuery());
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
        selectedRuleName.value = null;
      }
      return;
    }

    if (resetSelection) {
      if (pendingAlarmId.value) {
        await tryOpenPendingAlarm();
        return;
      }
      const keepId =
        selectedId.value && rows.value.some((r) => r.id === selectedId.value)
          ? selectedId.value
          : rows.value[0].id;
      const item = rows.value.find((r) => r.id === keepId)!;
      openDetail(item);
    }
  } catch (e: unknown) {
    if (!silent) {
      errorLocal.value = e instanceof Error ? e.message : t('alarmCenter.alarms.loadError');
    }
    rows.value = [];
    total.value = 0;
    if (resetSelection) {
      selectedId.value = null;
      selected.value = null;
      selectedRuleName.value = null;
    }
  } finally {
    if (!silent) loading.value = false;
  }
}

function applyFilters() {
  if (listView.value === 'history' && !resolveHistoryRange()) {
    errorLocal.value = t('siemCenter.events.invalidDateRange');
    return;
  }
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function refreshList() {
  void loadRows({ resetSelection: false });
}

function clearRuleFilter() {
  ruleIdFilter.value = null;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

async function openDetail(item: AlarmSummary) {
  selectedId.value = item.id;
  selected.value = item;
  selectedRule.value = null;
  selectedRuleName.value = null;
  if (!lgAndUp.value) drawerOpen.value = true;
  syncQueryToUrl();

  detailLoading.value = true;
  try {
    const detail = await alarmGet(item.id);
    if (selectedId.value === item.id) selected.value = detail;
  } catch {
    /* liste satırı ile devam */
  }

  try {
    const rule = await alarmRuleGet(item.ruleId);
    if (selectedId.value === item.id) {
      selectedRule.value = rule;
      selectedRuleName.value = rule.name;
    }
  } catch {
    selectedRule.value = null;
    selectedRuleName.value = null;
  } finally {
    detailLoading.value = false;
  }
}

async function runLifecycleAction(action: 'acknowledge' | 'suppress' | 'resolve') {
  if (!selected.value) return;
  actionLoading.value = true;
  errorLocal.value = null;
  try {
    const fn =
      action === 'acknowledge' ? alarmAcknowledge : action === 'suppress' ? alarmSuppress : alarmResolve;
    const updated = await fn(selected.value.id);
    selected.value = updated;
    const idx = rows.value.findIndex((r) => r.id === updated.id);
    if (idx >= 0) rows.value[idx] = updated;
    if (
      statusFilter.value === 'open' &&
      (updated.status === 'Resolved' || updated.status === 'Suppressed')
    ) {
      rows.value = rows.value.filter((r) => r.id !== updated.id);
      total.value = Math.max(0, total.value - 1);
    }
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('alarmCenter.alarms.actionError');
  } finally {
    actionLoading.value = false;
  }
}

function onTableRowClick(_event: Event, ctx: { item: AlarmSummary }) {
  openDetail(ctx.item);
}

function tableRowProps(data: { item: AlarmSummary }) {
  return {
    class: data.item.id === selectedId.value ? 'ac-alarms-table__row--selected' : '',
  };
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
  saveAlarmsRefreshIntervalSec(sec);
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

    <v-alert
      v-if="ruleIdFilter"
      type="info"
      variant="tonal"
      density="compact"
      class="mb-4"
      icon="mdi-filter"
    >
      {{ t('alarmCenter.alarms.filteredByRule') }}
      <v-btn variant="text" size="small" class="ml-2" @click="clearRuleFilter">
        {{ t('alarmCenter.alarms.clearRuleFilter') }}
      </v-btn>
    </v-alert>

    <!-- View toggle -->
    <div class="d-flex flex-wrap align-center gap-2 mb-4">
      <v-btn-toggle :model-value="listView" mandatory density="compact" variant="outlined" divided @update:model-value="switchListView($event as AlarmListView)">
        <v-btn value="inbox" prepend-icon="mdi-inbox-outline">
          {{ t('alarmCenter.alarms.viewInbox') }}
        </v-btn>
        <v-btn value="history" prepend-icon="mdi-history">
          {{ t('alarmCenter.alarms.viewHistory') }}
        </v-btn>
      </v-btn-toggle>
      <template v-if="listView === 'history'">
        <v-chip
          v-for="days in [7, 30, 90]"
          :key="days"
          size="small"
          variant="tonal"
          :color="historyRangeMode === 'preset' && historyDays === days ? 'primary' : undefined"
          @click="setHistoryDays(days)"
        >
          {{ t('alarmCenter.alarms.historyDays', { n: days }) }}
        </v-chip>
        <v-chip
          size="small"
          variant="tonal"
          :color="historyRangeMode === 'custom' ? 'primary' : undefined"
          @click="enableCustomHistoryRange"
        >
          {{ t('alarmCenter.alarms.historyRangeCustom') }}
        </v-chip>
      </template>
    </div>

    <!-- Summary strip -->
    <v-row dense class="mb-4">
      <v-col cols="6" sm="3">
        <v-card variant="tonal" color="primary" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">
            <template v-if="listStats.total === 0">0</template>
            <template v-else>{{ listStats.pageFrom }}–{{ listStats.pageTo }}</template>
          </div>
          <div class="text-caption">{{ t('alarmCenter.alarms.statPageRange') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card variant="tonal" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">{{ listStats.total }}</div>
          <div class="text-caption">{{ t('alarmCenter.alarms.statMatching') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card variant="tonal" color="error" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">{{ listStats.highSeverity }}</div>
          <div class="text-caption">{{ t('alarmCenter.alarms.statHighSeverity') }}</div>
        </v-card>
      </v-col>
      <v-col cols="6" sm="3">
        <v-card variant="tonal" color="warning" class="rounded-lg pa-3 text-center">
          <div class="text-h6 font-weight-bold">{{ listStats.activeCount }}</div>
          <div class="text-caption">{{ t('alarmCenter.alarms.statActivePage') }}</div>
        </v-card>
      </v-col>
    </v-row>

    <!-- Filters -->
    <v-card variant="outlined" class="rounded-lg pa-3 pa-md-4 mb-4">
      <v-row dense>
        <v-col cols="12" md="4">
          <v-text-field
            v-model="search"
            :label="t('alarmCenter.alarms.search')"
            :placeholder="t('alarmCenter.alarms.searchPlaceholder')"
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
            v-model="statusFilter"
            :items="statusFilterItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.alarms.colStatus')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="6" md="2">
          <v-select
            v-model="minSeverity"
            :items="minSeverityItems"
            item-title="title"
            item-value="value"
            :label="t('alarmCenter.alarms.colSeverity')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col v-if="listView === 'history' && historyRangeMode === 'custom'" cols="6" md="2">
          <v-text-field
            v-model="customHistoryFromLocal"
            type="datetime-local"
            :label="t('siemCenter.events.dateFrom')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col v-if="listView === 'history' && historyRangeMode === 'custom'" cols="6" md="2">
          <v-text-field
            v-model="customHistoryToLocal"
            type="datetime-local"
            :label="t('siemCenter.events.dateTo')"
            variant="outlined"
            density="compact"
            hide-details
          />
        </v-col>
        <v-col cols="12" class="d-flex flex-wrap align-center gap-2">
          <v-btn color="primary" prepend-icon="mdi-filter" :loading="loading" @click="applyFilters">
            {{ t('alarmCenter.alarms.apply') }}
          </v-btn>
          <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="refreshList">
            {{ t('alarmCenter.alarms.refresh') }}
          </v-btn>
          <v-btn v-if="hasActiveFilters()" variant="text" prepend-icon="mdi-filter-off" @click="clearFilters">
            {{ t('alarmCenter.alarms.clearFilters') }}
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
            {{ t('alarmCenter.alarms.statTotal', { count: listStats.total }) }}
          </v-chip>
        </v-col>
      </v-row>
    </v-card>

    <!-- Empty -->
    <v-card v-if="!loading && rows.length === 0" variant="outlined" class="rounded-lg pa-8 text-center">
      <v-icon icon="mdi-bell-off-outline" size="48" color="primary" class="mb-3 opacity-60" />
      <div class="text-h6 font-weight-bold mb-2">
        {{ ruleIdFilter ? t('alarmCenter.alarms.emptyForRule') : listView === 'history' ? t('alarmCenter.alarms.emptyHistory') : t('alarmCenter.alarms.empty') }}
      </div>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('alarmCenter.alarms.emptyHint') }}</p>
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
          :items-per-page-options="[...ALARM_PAGE_SIZE_OPTIONS]"
          :loading="loading"
          :row-props="tableRowProps"
          item-value="id"
          class="rounded-lg ac-alarms-table"
          density="comfortable"
          hover
          @click:row="onTableRowClick"
        >
          <template #item.severity="{ item }">
            <v-chip size="small" :color="severityColor(item.severity)" variant="flat">
              {{ item.severity }}
            </v-chip>
          </template>
          <template #item.scenario="{ item }">
            <div class="d-flex flex-wrap align-center gap-1">
              <v-chip v-if="getScenarioIdForAlarm(item)" size="x-small" color="primary" variant="flat">
                {{ getScenarioIdForAlarm(item) }}
              </v-chip>
              <span class="text-body-2 text-no-wrap">{{ formatAlarmScenarioLabel(item, t) }}</span>
            </div>
          </template>
          <template #item.status="{ item }">
            <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
              {{ statusLabel(item.status, t) }}
            </v-chip>
          </template>
          <template #item.summary="{ item }">
            <span class="text-body-2">{{ formatAlarmSummary(item) }}</span>
          </template>
          <template #item.count="{ item }">
            <span class="text-medium-emphasis">{{ item.count.toLocaleString() }}</span>
          </template>
          <template #item.lastSeenAt="{ item }">
            <div>
              <div class="text-body-2">{{ formatDate(item.lastSeenAt) }}</div>
              <div class="text-caption text-medium-emphasis">{{ relativeTime(item.lastSeenAt) }}</div>
            </div>
          </template>
        </v-data-table-server>
      </v-col>

      <v-col v-if="selected && lgAndUp" cols="12" lg="4">
        <AcAlarmDetailPanel
          :alarm="selected"
          :rule="selectedRule"
          :rule-name="selectedRuleName"
          :loading="detailLoading"
          :action-loading="actionLoading"
          @acknowledge="runLifecycleAction('acknowledge')"
          @suppress="runLifecycleAction('suppress')"
          @resolve="runLifecycleAction('resolve')"
        />
      </v-col>
    </v-row>

    <!-- Mobile drawer -->
    <v-navigation-drawer
      v-if="!lgAndUp"
      v-model="drawerOpen"
      location="right"
      width="100%"
      temporary
      class="ac-alarms-drawer"
    >
      <AcAlarmDetailPanel
        v-if="selected"
        :alarm="selected"
        :rule="selectedRule"
        :rule-name="selectedRuleName"
        :loading="detailLoading"
        :action-loading="actionLoading"
        @close="drawerOpen = false"
        @acknowledge="runLifecycleAction('acknowledge')"
        @suppress="runLifecycleAction('suppress')"
        @resolve="runLifecycleAction('resolve')"
      />
    </v-navigation-drawer>
  </div>
</template>

<style scoped>
.ac-alarms-table :deep(.ac-alarms-table__row--selected) {
  background: rgba(var(--v-theme-primary), 0.06);
}

.ac-alarms-drawer :deep(.v-navigation-drawer__content) {
  overflow-y: auto;
}
</style>
