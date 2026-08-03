<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
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
  isValidCustomRange,
  SEC_EVENT_DEFAULT_PAGE_SIZE,
  SEC_EVENT_PAGE_SIZE_OPTIONS,
  secEventActionI18nKey,
  sourceTypeLabelKey,
  toDatetimeLocalInput,
} from '@/composables/useSecEventList';
import {
  clearSecEventSmartField,
  hasSecEventSmartFilters,
  type SecEventSmartSearchFieldKey,
  type SecEventSmartSearchFields,
} from '@/utils/secEventSmartSearch';
import {
  getSecEventFilterIntent,
  resolveSecEventIntentActions,
  rowMatchesActionConstraint,
  type SecEventFilterBuilderResult,
} from '@/utils/secEventFilterIntents';
import AcSecEventDetailPanel from '@/components/apps/siem-center/AcSecEventDetailPanel.vue';
import AcSecEventFilterBuilderDialog from '@/components/apps/siem-center/AcSecEventFilterBuilderDialog.vue';

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
const listReady = ref(false);
const suppressPageWatch = ref(false);

const page = ref(1);
const itemsPerPage = ref(SEC_EVENT_DEFAULT_PAGE_SIZE);

/** Full-text search draft (applied → free-text `search` chip). */
const searchDraft = ref('');
/** Structured + free-text applied filters. */
const smartFilters = ref<SecEventSmartSearchFields>({});

const sourceType = ref<string | null>(null);
const eventAction = ref<string | null>(null);
const eventActions = ref<string | null>(null);
const eventActionPrefix = ref<string | null>(null);
const eventOutcome = ref<string | null>(null);
const dstPort = ref<string | null>(null);
const intentId = ref<string | null>(null);
const filterBuilderOpen = ref(false);
const rangeMode = ref<SecEventRangeMode>('preset');
const timeRange = ref<SecEventTimeRange>('24h');
const customFromLocal = ref('');
const customToLocal = ref('');
/** Log explorer: include unknown by default (all ingested events). */
const showUnknown = ref(true);

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

const filterBuilderInitialValues = computed(() => ({
  actorUser: smartFilters.value.actorUser ?? null,
  srcIp: smartFilters.value.srcIp ?? null,
  dstIp: smartFilters.value.dstIp ?? null,
  dstPort: dstPort.value,
  sourceHost: smartFilters.value.sourceHost ?? null,
  eventCode: smartFilters.value.eventCode ?? null,
  eventOutcome: eventOutcome.value,
  eventAction: eventAction.value,
  sourceType: sourceType.value,
  search: smartFilters.value.search ?? null,
}));

type FilterChip = {
  key: string;
  label: string;
  color?: string;
  remove: () => void;
};

const activeFilterChips = computed((): FilterChip[] => {
  const chips: FilterChip[] = [];
  const f = smartFilters.value;

  if (intentId.value) {
    const intent = getSecEventFilterIntent(intentId.value);
    chips.push({
      key: 'intent',
      label: intent
        ? t(intent.titleKey)
        : `${t('siemCenter.events.filterBuilder.intentChip')}: ${intentId.value}`,
      color: intent?.color || 'primary',
      remove: () => {
        intentId.value = null;
        eventActions.value = null;
        eventActionPrefix.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }

  if (f.search?.trim()) {
    chips.push({
      key: 'search',
      label: `${t('siemCenter.events.chipText')}: ${f.search.trim()}`,
      color: 'secondary',
      remove: () => removeSmartChip('search'),
    });
  }
  if (f.actorUser?.trim()) {
    chips.push({
      key: 'actorUser',
      label: `${t('siemCenter.events.chipUser')}: ${f.actorUser.trim()}`,
      color: 'primary',
      remove: () => removeSmartChip('actorUser'),
    });
  }
  if (f.srcIp?.trim()) {
    chips.push({
      key: 'srcIp',
      label: `${t('siemCenter.events.chipSrcIp')}: ${f.srcIp.trim()}`,
      color: 'primary',
      remove: () => removeSmartChip('srcIp'),
    });
  }
  if (f.dstIp?.trim()) {
    chips.push({
      key: 'dstIp',
      label: `${t('siemCenter.events.chipDstIp')}: ${f.dstIp.trim()}`,
      color: 'primary',
      remove: () => removeSmartChip('dstIp'),
    });
  }
  if (dstPort.value?.trim()) {
    chips.push({
      key: 'dstPort',
      label: `${t('siemCenter.events.chipDstPort')}: ${dstPort.value.trim()}`,
      color: 'primary',
      remove: () => {
        dstPort.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }
  if (f.sourceHost?.trim()) {
    chips.push({
      key: 'sourceHost',
      label: `${t('siemCenter.events.chipHost')}: ${f.sourceHost.trim()}`,
      color: 'primary',
      remove: () => removeSmartChip('sourceHost'),
    });
  }
  if (f.eventCode?.trim()) {
    chips.push({
      key: 'eventCode',
      label: `${t('siemCenter.events.chipEventCode')}: ${f.eventCode.trim()}`,
      color: 'primary',
      remove: () => removeSmartChip('eventCode'),
    });
  }
  if (eventOutcome.value?.trim()) {
    chips.push({
      key: 'eventOutcome',
      label: `${t('siemCenter.events.chipOutcome')}: ${outcomeLabel(eventOutcome.value)}`,
      remove: () => {
        eventOutcome.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }
  if (sourceType.value) {
    chips.push({
      key: 'sourceType',
      label: `${t('siemCenter.events.colSource')}: ${t(sourceTypeLabelKey(sourceType.value))}`,
      remove: () => {
        sourceType.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }
  if (eventAction.value) {
    chips.push({
      key: 'eventAction',
      label: `${t('siemCenter.events.colAction')}: ${actionLabel(eventAction.value)}`,
      color: actionColor(eventAction.value),
      remove: () => {
        eventAction.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  } else if (eventActions.value?.trim()) {
    const intent = intentId.value ? getSecEventFilterIntent(intentId.value) : null;
    chips.push({
      key: 'eventActions',
      label: intent
        ? t('siemCenter.events.filterBuilder.familyActionsChip', { name: t(intent.titleKey) })
        : `${t('siemCenter.events.colAction')}: ${eventActions.value.split(',').length}`,
      color: 'info',
      remove: () => {
        eventActions.value = null;
        eventActionPrefix.value = null;
        intentId.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }
  if (!showUnknown.value) {
    chips.push({
      key: 'hideUnknown',
      label: t('siemCenter.events.hideUnknown'),
      remove: () => {
        showUnknown.value = true;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }

  return chips;
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
  const key = secEventActionI18nKey(action);
  const translated = t(key);
  return translated !== key ? translated : action;
}

function outcomeLabel(outcome: string): string {
  const key = `siemCenter.events.filterBuilder.outcomes.${outcome}`;
  const translated = t(key);
  return translated !== key ? translated : outcome;
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
  if (listReady.value) {
    page.value = 1;
    void loadRows({ syncUrl: true, resetSelection: true });
  }
}

function syncQueryToUrl() {
  const query: Record<string, string> = {};
  const f = smartFilters.value;
  const actionQuery = resolveSecEventIntentActions(
    intentId.value,
    eventAction.value,
    eventActions.value,
    eventActionPrefix.value,
  );
  if (f.search?.trim()) query.search = f.search.trim();
  if (f.actorUser?.trim()) query.actorUser = f.actorUser.trim();
  if (f.srcIp?.trim()) query.srcIp = f.srcIp.trim();
  if (f.dstIp?.trim()) query.dstIp = f.dstIp.trim();
  if (f.sourceHost?.trim()) query.sourceHost = f.sourceHost.trim();
  if (f.eventCode?.trim()) query.eventCode = f.eventCode.trim();
  if (dstPort.value?.trim()) query.dstPort = dstPort.value.trim();
  if (eventOutcome.value?.trim()) query.eventOutcome = eventOutcome.value.trim();
  if (sourceType.value) query.sourceType = sourceType.value;
  if (actionQuery.eventAction) query.eventAction = actionQuery.eventAction;
  else if (actionQuery.eventActionPrefix) query.eventActionPrefix = actionQuery.eventActionPrefix;
  else if (actionQuery.eventActions) query.eventActions = actionQuery.eventActions;
  if (intentId.value) query.intent = intentId.value;
  // Default is show unknown; only persist when hidden
  if (!showUnknown.value) query.hideUnknown = '1';

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
  const fields: SecEventSmartSearchFields = {};
  if (typeof q.search === 'string' && q.search.trim()) fields.search = q.search.trim();
  if (typeof q.actorUser === 'string' && q.actorUser.trim()) fields.actorUser = q.actorUser.trim();
  if (typeof q.srcIp === 'string' && q.srcIp.trim()) fields.srcIp = q.srcIp.trim();
  if (typeof q.dstIp === 'string' && q.dstIp.trim()) fields.dstIp = q.dstIp.trim();
  if (typeof q.sourceHost === 'string' && q.sourceHost.trim()) fields.sourceHost = q.sourceHost.trim();
  if (typeof q.eventCode === 'string' && q.eventCode.trim()) fields.eventCode = q.eventCode.trim();
  smartFilters.value = fields;
  searchDraft.value = fields.search ?? '';

  sourceType.value = typeof q.sourceType === 'string' ? q.sourceType : null;
  eventAction.value = typeof q.eventAction === 'string' ? q.eventAction : null;
  eventActions.value =
    !eventAction.value && typeof q.eventActions === 'string' && q.eventActions.trim()
      ? q.eventActions.trim()
      : null;
  eventActionPrefix.value =
    typeof q.eventActionPrefix === 'string' && q.eventActionPrefix.trim()
      ? q.eventActionPrefix.trim()
      : null;
  intentId.value = typeof q.intent === 'string' ? q.intent : null;
  // Restore prefix/family from intent when URL only has intent=
  if (!eventAction.value && !eventActions.value && !eventActionPrefix.value && intentId.value) {
    const intent = getSecEventFilterIntent(intentId.value);
    if (intent?.eventActionPrefix) eventActionPrefix.value = intent.eventActionPrefix;
    else if (intent?.eventActions?.length) eventActions.value = intent.eventActions.join(',');
  }
  eventOutcome.value = typeof q.eventOutcome === 'string' ? q.eventOutcome : null;
  dstPort.value = typeof q.dstPort === 'string' ? q.dstPort : null;

  // Legacy showUnknown=1 kept; new default is include unknown unless hideUnknown=1
  if (q.hideUnknown === '1' || q.hideUnknown === 'true') {
    showUnknown.value = false;
  } else if (q.showUnknown === '0' || q.showUnknown === 'false') {
    showUnknown.value = false;
  } else {
    showUnknown.value = true;
  }

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

function applyFilterBuilder(result: SecEventFilterBuilderResult) {
  const intent = getSecEventFilterIntent(result.intentId);
  intentId.value = result.intentId;

  const refined = result.eventAction?.trim() || null;
  eventAction.value = refined;
  if (refined) {
    eventActions.value = null;
    eventActionPrefix.value = null;
  } else if (intent?.eventActionPrefix || result.eventActionPrefix) {
    eventActionPrefix.value = (result.eventActionPrefix || intent?.eventActionPrefix || null);
    eventActions.value = intent?.eventActions?.length ? intent.eventActions.join(',') : null;
  } else if (intent?.eventActions?.length) {
    eventActionPrefix.value = null;
    eventActions.value = intent.eventActions.join(',');
  } else if (result.eventActions?.length) {
    eventActionPrefix.value = null;
    eventActions.value = result.eventActions.join(',');
  } else {
    eventActionPrefix.value = null;
    eventActions.value = null;
  }

  eventOutcome.value = result.eventOutcome ?? null;
  dstPort.value = result.dstPort ?? null;
  sourceType.value = result.sourceType ?? null;
  // Intent filter replaces free-text unless the builder explicitly set message/search.
  smartFilters.value = {
    actorUser: result.actorUser ?? undefined,
    srcIp: result.srcIp ?? undefined,
    dstIp: result.dstIp ?? undefined,
    sourceHost: result.sourceHost ?? undefined,
    eventCode: result.eventCode ?? undefined,
    search: result.search ?? undefined,
  };
  searchDraft.value = smartFilters.value.search ?? '';
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function removeSmartChip(key: SecEventSmartSearchFieldKey) {
  smartFilters.value = clearSecEventSmartField(smartFilters.value, key);
  if (key === 'search') searchDraft.value = '';
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function clearFilters() {
  searchDraft.value = '';
  smartFilters.value = {};
  sourceType.value = null;
  eventAction.value = null;
  eventActions.value = null;
  eventActionPrefix.value = null;
  eventOutcome.value = null;
  dstPort.value = null;
  intentId.value = null;
  rangeMode.value = 'preset';
  timeRange.value = '24h';
  customFromLocal.value = '';
  customToLocal.value = '';
  showUnknown.value = true;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function hasActiveFilters(): boolean {
  return !!(
    hasSecEventSmartFilters(smartFilters.value)
    || (searchDraft.value ?? '').trim()
    || sourceType.value
    || eventAction.value
    || eventActions.value
    || eventActionPrefix.value
    || eventOutcome.value
    || dstPort.value
    || intentId.value
    || rangeMode.value !== 'preset'
    || timeRange.value !== '24h'
    || !showUnknown.value
  );
}

async function loadRows(options: { syncUrl?: boolean; resetSelection?: boolean } = {}) {
  const { syncUrl = false, resetSelection = false } = options;
  const range = resolveQueryRange();
  if (!range) return;

  loading.value = true;
  errorLocal.value = null;
  if (syncUrl) syncQueryToUrl();

  const f = smartFilters.value;
  const actionQuery = resolveSecEventIntentActions(
    intentId.value,
    eventAction.value,
    eventActions.value,
    eventActionPrefix.value,
  );
  try {
    const res = await secEventQuery({
      from: range.from,
      to: range.to,
      sourceType: sourceType.value ?? undefined,
      eventAction: actionQuery.eventAction,
      eventActions: actionQuery.eventActions,
      eventActionPrefix: actionQuery.eventActionPrefix,
      eventOutcome: eventOutcome.value ?? undefined,
      actorUser: f.actorUser?.trim() || undefined,
      srcIp: f.srcIp?.trim() || undefined,
      dstIp: f.dstIp?.trim() || undefined,
      dstPort: dstPort.value?.trim() || undefined,
      sourceHost: f.sourceHost?.trim() || undefined,
      eventCode: f.eventCode?.trim() || undefined,
      search: f.search?.trim() || undefined,
      excludeUnknown: !showUnknown.value,
      skip: skip.value,
      limit: itemsPerPage.value,
    });

    // Guard: if API ignored action constraints, do not show unrelated rows.
    const hasActionConstraint = !!(
      actionQuery.eventAction
      || actionQuery.eventActions
      || actionQuery.eventActionPrefix
    );
    if (hasActionConstraint) {
      const matched = res.items.filter((item) =>
        rowMatchesActionConstraint(item.eventAction, actionQuery, {
          eventCode: item.eventCode,
          sourceProduct: item.sourceProduct,
        }),
      );
      if (matched.length === 0) {
        rows.value = [];
        total.value = 0;
      } else {
        rows.value = matched;
        total.value = res.total;
      }
    } else {
      rows.value = res.items;
      total.value = res.total;
    }

    const maxPage = Math.max(1, Math.ceil(total.value / itemsPerPage.value));
    if (page.value > maxPage) {
      suppressPageWatch.value = true;
      page.value = maxPage;
      suppressPageWatch.value = false;
      if (total.value > 0) {
        await loadRows({ syncUrl, resetSelection });
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
    errorLocal.value = e instanceof Error ? e.message : t('siemCenter.events.loadError');
    rows.value = [];
    total.value = 0;
    if (resetSelection) {
      selectedId.value = null;
      selected.value = null;
    }
  } finally {
    loading.value = false;
  }
}

/** Apply full-text search (does not clear structured filters). */
function applySearch() {
  const draft = (searchDraft.value ?? '').trim();
  smartFilters.value = {
    ...smartFilters.value,
    search: draft || undefined,
  };
  if (!draft) {
    const next = { ...smartFilters.value };
    delete next.search;
    smartFilters.value = next;
  }
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
      /* keep list row */
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

watch(lgAndUp, (wide) => {
  if (wide) drawerOpen.value = false;
});

watch([page, itemsPerPage], () => {
  if (!listReady.value || suppressPageWatch.value) return;
  void loadRows({ resetSelection: false });
});

onMounted(() => {
  applyFromRoute();
  listReady.value = true;
  void loadRows({ resetSelection: true });
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <v-card variant="outlined" class="rounded-lg pa-3 pa-md-4 mb-4">
      <v-row dense align="center">
        <v-col cols="12" md="6">
          <v-text-field
            v-model="searchDraft"
            :label="t('siemCenter.events.fullTextSearch')"
            :placeholder="t('siemCenter.events.fullTextSearchPlaceholder')"
            prepend-inner-icon="mdi-magnify"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            @keyup.enter="applySearch"
            @click:clear="applySearch"
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
        <v-col cols="12" md="4" class="d-flex flex-wrap align-center ga-2">
          <v-btn color="primary" prepend-icon="mdi-magnify" :loading="loading" @click="applySearch">
            {{ t('siemCenter.events.search') }}
          </v-btn>
          <v-btn variant="tonal" prepend-icon="mdi-filter-plus" @click="filterBuilderOpen = true">
            {{ t('siemCenter.events.filterBuilder.open') }}
          </v-btn>
          <v-btn variant="text" prepend-icon="mdi-refresh" :loading="loading" @click="refreshList">
            {{ t('siemCenter.events.refresh') }}
          </v-btn>
          <v-btn v-if="hasActiveFilters()" variant="text" prepend-icon="mdi-filter-off" @click="clearFilters">
            {{ t('siemCenter.events.clearFilters') }}
          </v-btn>
        </v-col>

        <v-col v-if="activeFilterChips.length" cols="12">
          <div class="d-flex flex-wrap align-center ga-2">
            <span class="text-caption text-medium-emphasis">{{ t('siemCenter.events.activeFilters') }}</span>
            <v-chip
              v-for="chip in activeFilterChips"
              :key="chip.key"
              size="small"
              variant="tonal"
              :color="chip.color"
              closable
              @click:close="chip.remove()"
            >
              {{ chip.label }}
            </v-chip>
          </div>
        </v-col>

        <v-col cols="12" class="d-flex align-center">
          <span class="text-caption text-medium-emphasis">
            {{ t('siemCenter.events.statTotal', { shown: listStats.shown, total: listStats.total }) }}
            · {{ activeRangeLabel }}
          </span>
        </v-col>
      </v-row>
    </v-card>

    <v-card v-if="!loading && rows.length === 0" variant="outlined" class="rounded-lg pa-8 text-center">
      <v-icon icon="mdi-shield-off-outline" size="48" color="primary" class="mb-3 opacity-60" />
      <div class="text-h6 font-weight-bold mb-2">{{ t('siemCenter.events.empty') }}</div>
      <p class="text-body-2 text-medium-emphasis mb-0">{{ t('siemCenter.events.emptyHint') }}</p>
    </v-card>

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
            <div class="d-flex flex-wrap align-center ga-1">
              <v-chip size="small" :color="actionColor(item.eventAction)" variant="tonal">
                {{ actionLabel(item.eventAction) }}
              </v-chip>
              <v-chip v-if="item.eventCode" size="x-small" variant="outlined">
                {{ item.eventCode }}
              </v-chip>
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
        <AcSecEventDetailPanel :event="selected" :loading="detailLoading" />
      </v-col>
    </v-row>

    <v-navigation-drawer v-if="!lgAndUp" v-model="drawerOpen" location="right" width="100%" temporary class="ac-events-drawer">
      <AcSecEventDetailPanel
        v-if="selected"
        :event="selected"
        :loading="detailLoading"
        @close="drawerOpen = false"
      />
    </v-navigation-drawer>

    <AcSecEventFilterBuilderDialog
      v-model="filterBuilderOpen"
      :initial-intent-id="intentId"
      :initial-values="filterBuilderInitialValues"
      @apply="applyFilterBuilder"
    />
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
