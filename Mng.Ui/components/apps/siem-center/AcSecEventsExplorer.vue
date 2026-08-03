<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useDisplay } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventListItem, SecEventRangeMode, SecEventTimeRange } from '@/types/apps/secEvent';
import type { SecEventSavedFilter } from '@/types/apps/secEventFilterCatalog';
import { secEventQuery, secEventGet } from '@/services/secEventService';
import { fetchDiscoveryHosts } from '@/services/siemDiscoveryService';
import {
  findFilterById,
  loadSecEventFilterCatalog,
} from '@/services/secEventFilterCatalogService';
import {
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
  actionColor,
} from '@/composables/useSecEventList';
import {
  createEmptyActiveFilter,
  mapSecEventSavedFilterToQuery,
} from '@/utils/secEventFilterQueryMap';
import { rowMatchesActionConstraint } from '@/utils/secEventFilterIntents';
import AcSecEventDetailPanel from '@/components/apps/siem-center/AcSecEventDetailPanel.vue';
import AcSecEventFilterCatalogDialog from '@/components/apps/siem-center/AcSecEventFilterCatalogDialog.vue';

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

const searchDraft = ref('');
const activeFilter = ref<SecEventSavedFilter>(createEmptyActiveFilter());
const selectedFilterId = ref<string | null>(null);
const filterDialogOpen = ref(false);
const hostOptions = ref<string[]>([]);

const rangeMode = ref<SecEventRangeMode>('preset');
const timeRange = ref<SecEventTimeRange>('24h');
const customFromLocal = ref('');
const customToLocal = ref('');
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

type FilterChip = {
  key: string;
  label: string;
  color?: string;
  remove: () => void;
};

const activeFilterChips = computed((): FilterChip[] => {
  const chips: FilterChip[] = [];
  const f = activeFilter.value;
  const mapped = mapSecEventSavedFilterToQuery(f);

  if (selectedFilterId.value) {
    const catalog = loadSecEventFilterCatalog();
    const saved = findFilterById(catalog, selectedFilterId.value);
    chips.push({
      key: 'savedFilter',
      label: saved?.name
        || f.name
        || t('siemCenter.events.filterCatalog.activeFilter'),
      color: 'primary',
      remove: () => clearFilters(),
    });
  }

  if (mapped.sourceType) {
    chips.push({
      key: 'sourceType',
      label: `${t('siemCenter.events.filterCatalog.type')}: ${t(sourceTypeLabelKey(mapped.sourceType))}`,
      remove: () => {
        activeFilter.value = {
          ...activeFilter.value,
          scope: { ...activeFilter.value.scope, type: null },
        };
        selectedFilterId.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }
  if (mapped.sourceProduct) {
    chips.push({
      key: 'sourceProduct',
      label: `${t('siemCenter.events.filterCatalog.product')}: ${mapped.sourceProduct}`,
      remove: () => {
        activeFilter.value = {
          ...activeFilter.value,
          scope: { ...activeFilter.value.scope, product: null },
        };
        selectedFilterId.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }
  const hosts = f.scope?.hosts ?? [];
  if (hosts.length) {
    chips.push({
      key: 'hosts',
      label: `${t('siemCenter.events.filterCatalog.host')}: ${hosts.join(', ')}`,
      remove: () => {
        activeFilter.value = {
          ...activeFilter.value,
          scope: { ...activeFilter.value.scope, hosts: [] },
        };
        selectedFilterId.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }

  for (const clause of f.fields) {
    if (!clause.value?.trim()) continue;
    chips.push({
      key: `field-${clause.field}`,
      label: `${clause.field}: ${clause.value}`,
      color: 'secondary',
      remove: () => {
        activeFilter.value = {
          ...activeFilter.value,
          fields: activeFilter.value.fields.filter((x) => x.field !== clause.field),
        };
        selectedFilterId.value = null;
        page.value = 1;
        void loadRows({ syncUrl: true, resetSelection: true });
      },
    });
  }

  if (searchDraft.value.trim() && !f.fields.some((x) => x.field === 'search' && x.value === searchDraft.value.trim())) {
    chips.push({
      key: 'search',
      label: `${t('siemCenter.events.chipText')}: ${searchDraft.value.trim()}`,
      color: 'secondary',
      remove: () => {
        searchDraft.value = '';
        applySearch();
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

function hasActiveFilters(): boolean {
  const f = activeFilter.value;
  return !!(
    selectedFilterId.value
    || f.scope?.type
    || f.scope?.product
    || (f.scope?.hosts?.length ?? 0) > 0
    || f.fields.some((x) => x.value?.trim())
    || searchDraft.value.trim()
    || rangeMode.value !== 'preset'
    || timeRange.value !== '24h'
    || !showUnknown.value
  );
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

function relativeTime(value?: string | null): string {
  return formatRelativeTime(value, locale.value, t);
}

function actionLabel(action: string): string {
  const key = secEventActionI18nKey(action);
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
    if (!from || !isValidCustomRange(from, to)) {
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

function applyFilterDefinition(filter: SecEventSavedFilter, filterId: string | null) {
  activeFilter.value = {
    ...filter,
    scope: {
      type: filter.scope?.type ?? null,
      product: filter.scope?.product ?? null,
      hosts: [...(filter.scope?.hosts ?? [])],
    },
    fields: filter.fields.map((x) => ({ ...x })),
  };
  selectedFilterId.value = filterId;
  const searchField = filter.fields.find((x) => x.field === 'search');
  if (searchField?.value) searchDraft.value = searchField.value;
}

function onFilterDialogApply(payload: { filter: SecEventSavedFilter; filterId: string | null }) {
  applyFilterDefinition(payload.filter, payload.filterId);
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function clearFilters() {
  searchDraft.value = '';
  applyFilterDefinition(createEmptyActiveFilter(), null);
  rangeMode.value = 'preset';
  timeRange.value = '24h';
  customFromLocal.value = '';
  customToLocal.value = '';
  showUnknown.value = true;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function applySearch() {
  const draft = (searchDraft.value ?? '').trim();
  const fields = activeFilter.value.fields.filter((x) => x.field !== 'search');
  if (draft) fields.push({ field: 'search', op: 'contains', value: draft });
  activeFilter.value = { ...activeFilter.value, fields };
  selectedFilterId.value = null;
  page.value = 1;
  void loadRows({ syncUrl: true, resetSelection: true });
}

function syncQueryToUrl() {
  const query: Record<string, string> = {};
  const mapped = mapSecEventSavedFilterToQuery(activeFilter.value);
  if (selectedFilterId.value) query.filterId = selectedFilterId.value;
  if (mapped.sourceType) query.sourceType = mapped.sourceType;
  if (mapped.sourceProduct) query.sourceProduct = mapped.sourceProduct;
  if (mapped.sourceHost) query.sourceHost = mapped.sourceHost;
  if (mapped.sourceHosts) query.sourceHosts = mapped.sourceHosts;
  if (mapped.eventAction) query.eventAction = mapped.eventAction;
  if (mapped.eventActions) query.eventActions = mapped.eventActions;
  if (mapped.eventActionPrefix) query.eventActionPrefix = mapped.eventActionPrefix;
  if (mapped.eventOutcome) query.eventOutcome = mapped.eventOutcome;
  if (mapped.eventCode) query.eventCode = mapped.eventCode;
  if (mapped.eventCodes) query.eventCodes = mapped.eventCodes;
  if (mapped.actorUser) query.actorUser = mapped.actorUser;
  if (mapped.srcIp) query.srcIp = mapped.srcIp;
  if (mapped.dstIp) query.dstIp = mapped.dstIp;
  if (mapped.dstPort) query.dstPort = mapped.dstPort;
  if (mapped.search) query.search = mapped.search;
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
  if (typeof q.filterId === 'string' && q.filterId.trim()) {
    const catalog = loadSecEventFilterCatalog();
    const found = findFilterById(catalog, q.filterId.trim());
    if (found) applyFilterDefinition(found, found.id);
  } else {
    const draft = createEmptyActiveFilter();
    if (typeof q.sourceType === 'string') draft.scope.type = q.sourceType;
    if (typeof q.sourceProduct === 'string') draft.scope.product = q.sourceProduct;
    if (typeof q.sourceHost === 'string') draft.scope.hosts = [q.sourceHost];
    else if (typeof q.sourceHosts === 'string') {
      draft.scope.hosts = q.sourceHosts.split(',').map((s) => s.trim()).filter(Boolean);
    }
    const fields: SecEventSavedFilter['fields'] = [];
    if (typeof q.eventCode === 'string') fields.push({ field: 'event.code', op: 'eq', value: q.eventCode });
    else if (typeof q.eventCodes === 'string') fields.push({ field: 'event.code', op: 'in', value: q.eventCodes });
    if (typeof q.eventOutcome === 'string') fields.push({ field: 'event.outcome', op: 'eq', value: q.eventOutcome });
    if (typeof q.eventAction === 'string') fields.push({ field: 'event.action', op: 'eq', value: q.eventAction });
    if (typeof q.eventActionPrefix === 'string') {
      fields.push({ field: 'event.actionPrefix', op: 'eq', value: q.eventActionPrefix });
    }
    if (typeof q.actorUser === 'string') fields.push({ field: 'actor.user', op: 'eq', value: q.actorUser });
    if (typeof q.srcIp === 'string') fields.push({ field: 'network.srcIp', op: 'eq', value: q.srcIp });
    if (typeof q.dstIp === 'string') fields.push({ field: 'network.dstIp', op: 'eq', value: q.dstIp });
    if (typeof q.dstPort === 'string') fields.push({ field: 'network.dstPort', op: 'eq', value: q.dstPort });
    if (typeof q.search === 'string') {
      fields.push({ field: 'search', op: 'contains', value: q.search });
      searchDraft.value = q.search;
    }
    draft.fields = fields;
    applyFilterDefinition(draft, null);
  }

  if (q.hideUnknown === '1' || q.hideUnknown === 'true') showUnknown.value = false;
  else showUnknown.value = true;

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

async function loadRows(options: { syncUrl?: boolean; resetSelection?: boolean } = {}) {
  const { syncUrl = false, resetSelection = false } = options;
  const range = resolveQueryRange();
  if (!range) return;

  loading.value = true;
  errorLocal.value = null;
  if (syncUrl) syncQueryToUrl();

  const mapped = mapSecEventSavedFilterToQuery(activeFilter.value);
  try {
    const res = await secEventQuery({
      from: range.from,
      to: range.to,
      ...mapped,
      excludeUnknown: !showUnknown.value,
      skip: skip.value,
      limit: itemsPerPage.value,
    });

    let items = res.items;
    let totalCount = res.total;

    const codeFilter = mapped.eventCode || mapped.eventCodes;
    if (codeFilter) {
      const allowed = new Set(codeFilter.split(',').map((s) => s.trim()).filter(Boolean));
      const matchedCodes = items.filter((item) => allowed.has((item.eventCode ?? '').trim()));
      if (matchedCodes.length !== items.length) {
        items = matchedCodes;
        if (matchedCodes.length === 0) totalCount = 0;
      }
    }

    const product = mapped.sourceProduct?.trim().toLowerCase();
    if (product) {
      const matchedProduct = items.filter(
        (item) => (item.sourceProduct ?? '').trim().toLowerCase() === product,
      );
      if (matchedProduct.length !== items.length) {
        items = matchedProduct;
        if (matchedProduct.length === 0) totalCount = 0;
      }
    }

    const hasActionConstraint = !!(
      mapped.eventAction
      || mapped.eventActions
      || mapped.eventActionPrefix
    );
    if (hasActionConstraint) {
      const matched = items.filter((item) =>
        rowMatchesActionConstraint(
          item.eventAction,
          {
            eventAction: mapped.eventAction,
            eventActions: mapped.eventActions,
            eventActionPrefix: mapped.eventActionPrefix,
          },
          {
            eventCode: item.eventCode,
            sourceProduct: item.sourceProduct,
          },
        ),
      );
      rows.value = matched.length === 0 ? [] : matched;
      total.value = matched.length === 0 ? 0 : totalCount;
    } else {
      rows.value = items;
      total.value = totalCount;
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

async function loadHostOptions() {
  try {
    const res = await fetchDiscoveryHosts({ limit: 1000 });
    const names = res.items
      .map((h) => (h.hostname || h.samAccountName || h.ip || '').trim())
      .filter(Boolean);
    hostOptions.value = Array.from(new Set(names)).sort((a, b) =>
      String(a).localeCompare(String(b)),
    ) as string[];
  } catch {
    hostOptions.value = [];
  }
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
  void loadHostOptions();
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
          <v-btn variant="tonal" prepend-icon="mdi-filter-plus" @click="filterDialogOpen = true">
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
              <div v-if="item.sourceProduct" class="text-caption text-medium-emphasis">
                {{ item.sourceProduct }}
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

    <AcSecEventFilterCatalogDialog
      v-model="filterDialogOpen"
      :initial-filter="activeFilter"
      :initial-filter-id="selectedFilterId"
      :host-options="hostOptions"
      @apply="onFilterDialogApply"
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
