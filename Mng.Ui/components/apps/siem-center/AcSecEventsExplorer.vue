<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventListItem, SecEventTimeRange } from '@/types/apps/secEvent';
import { secEventQuery, secEventGet } from '@/services/secEventService';

const { t, locale } = useAppI18n();
const route = useRoute();
const router = useRouter();

const loading = ref(true);
const errorLocal = ref<string | null>(null);
const rows = ref<SecEventListItem[]>([]);
const total = ref(0);
const selected = ref<SecEventListItem | null>(null);
const drawerOpen = ref(false);
const detailLoading = ref(false);

const search = ref('');
const sourceType = ref<string | null>(null);
const eventAction = ref<string | null>(null);
const timeRange = ref<SecEventTimeRange>('24h');

const VALID_TIME_RANGES: SecEventTimeRange[] = ['1h', '24h', '7d'];

const sourceTypeItems = computed(() => [
  { title: t('siemCenter.events.filterAll'), value: null },
  { title: t('siemCenter.events.sourceFirewall'), value: 'firewall' },
  { title: t('siemCenter.events.sourceAd'), value: 'ad' },
  { title: t('siemCenter.events.sourceEndpoint'), value: 'endpoint' },
]);

const eventActionItems = computed(() => [
  { title: t('siemCenter.events.filterAll'), value: null },
  { title: 'login_failed', value: 'login_failed' },
  { title: 'login_success', value: 'login_success' },
  { title: 'denied_flow', value: 'denied_flow' },
  { title: 'rule_change', value: 'rule_change' },
  { title: 'allowed_flow', value: 'allowed_flow' },
  { title: 'privileged_login_outside_window', value: 'privileged_login_outside_window' },
  { title: 'privilege_denied', value: 'privilege_denied' },
  { title: 'new_flow', value: 'new_flow' },
]);

const filterPresets = computed(() => [
  { key: 'u1', label: 'U1', eventAction: 'login_failed' as const },
  { key: 'u4', label: 'U4', eventAction: 'denied_flow' as const },
  { key: 'u5', label: 'U5', eventAction: 'allowed_flow' as const },
  { key: 'u3', label: 'U3', eventAction: 'privileged_login_outside_window' as const },
  { key: 'u6', label: 'U6', eventAction: 'rule_change' as const },
  { key: 'u7', label: 'U7', eventAction: 'new_flow' as const },
]);

const timeRangeItems = computed(() => [
  { title: t('siemCenter.events.range1h'), value: '1h' as SecEventTimeRange },
  { title: t('siemCenter.events.range24h'), value: '24h' as SecEventTimeRange },
  { title: t('siemCenter.events.range7d'), value: '7d' as SecEventTimeRange },
]);

const headers = computed(() => [
  { title: t('siemCenter.events.colTime'), key: 'timestamp', sortable: false },
  { title: t('siemCenter.events.colAction'), key: 'eventAction', sortable: false },
  { title: t('siemCenter.events.colSource'), key: 'sourceType', sortable: false },
  { title: t('siemCenter.events.colHost'), key: 'sourceHost', sortable: false },
  { title: t('siemCenter.events.colUser'), key: 'actorUser', sortable: false },
  { title: t('siemCenter.events.colSrcIp'), key: 'networkSrcIp', sortable: false },
  { title: t('siemCenter.events.colDstIp'), key: 'networkDstIp', sortable: false },
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

function actionColor(action: string): string {
  if (action.includes('fail') || action.includes('denied')) return 'error';
  if (action.includes('success')) return 'success';
  if (action.includes('new_flow') || action.includes('privileged')) return 'warning';
  return 'info';
}

function displayAction(item: SecEventListItem): string {
  if (item.baselineNewFlowPair) {
    return `${item.eventAction} + new_flow`;
  }
  return item.eventAction;
}

function computeFrom(): string {
  const now = Date.now();
  const hours = timeRange.value === '1h' ? 1 : timeRange.value === '7d' ? 168 : 24;
  return new Date(now - hours * 3600_000).toISOString();
}

function syncQueryToUrl() {
  const query: Record<string, string> = {};
  if (search.value.trim()) query.search = search.value.trim();
  if (sourceType.value) query.sourceType = sourceType.value;
  if (eventAction.value) query.eventAction = eventAction.value;
  if (timeRange.value !== '24h') query.timeRange = timeRange.value;
  void router.replace({ query });
}

function applyFromRoute() {
  const q = route.query;
  search.value = typeof q.search === 'string' ? q.search : '';
  sourceType.value = typeof q.sourceType === 'string' ? q.sourceType : null;
  eventAction.value = typeof q.eventAction === 'string' ? q.eventAction : null;
  const tr = typeof q.timeRange === 'string' ? q.timeRange : '24h';
  timeRange.value = VALID_TIME_RANGES.includes(tr as SecEventTimeRange)
    ? (tr as SecEventTimeRange)
    : '24h';
}

function applyPreset(preset: { eventAction: string }) {
  eventAction.value = preset.eventAction;
  void loadRows(true);
}

async function loadRows(syncUrl = false) {
  loading.value = true;
  errorLocal.value = null;
  if (syncUrl) syncQueryToUrl();
  try {
    const res = await secEventQuery({
      from: computeFrom(),
      sourceType: sourceType.value ?? undefined,
      eventAction: eventAction.value ?? undefined,
      search: search.value.trim() || undefined,
      limit: 100,
    });
    rows.value = res.items;
    total.value = res.total;
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('siemCenter.events.loadError');
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function openDetail(item: SecEventListItem) {
  selected.value = item;
  drawerOpen.value = true;
  detailLoading.value = true;
  void secEventGet(item.id)
    .then((detail) => {
      if (selected.value?.id === item.id) {
        selected.value = detail;
      }
    })
    .catch(() => {
      // Liste satırındaki rawPreview ile devam et
    })
    .finally(() => {
      detailLoading.value = false;
    });
}

const displayRaw = computed(() => selected.value?.raw ?? selected.value?.rawPreview ?? '');

onMounted(() => {
  applyFromRoute();
  void loadRows(false);
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center gap-2 mb-3">
      <span class="text-caption text-medium-emphasis">{{ t('siemCenter.events.presets') }}</span>
      <v-chip
        v-for="preset in filterPresets"
        :key="preset.key"
        size="small"
        variant="tonal"
        :color="eventAction === preset.eventAction ? 'primary' : undefined"
        @click="applyPreset(preset)"
      >
        {{ preset.label }}
      </v-chip>
    </div>

    <v-row class="mb-4" dense>
      <v-col cols="12" md="3">
        <v-text-field
          v-model="search"
          :label="t('siemCenter.events.search')"
          prepend-inner-icon="mdi-magnify"
          density="comfortable"
          hide-details
          clearable
          @keyup.enter="loadRows(true)"
        />
      </v-col>
      <v-col cols="12" sm="6" md="2">
        <v-select
          v-model="timeRange"
          :items="timeRangeItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.timeRange')"
          density="comfortable"
          hide-details
        />
      </v-col>
      <v-col cols="12" sm="6" md="2">
        <v-select
          v-model="sourceType"
          :items="sourceTypeItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.colSource')"
          density="comfortable"
          hide-details
          clearable
        />
      </v-col>
      <v-col cols="12" sm="6" md="2">
        <v-select
          v-model="eventAction"
          :items="eventActionItems"
          item-title="title"
          item-value="value"
          :label="t('siemCenter.events.colAction')"
          density="comfortable"
          hide-details
          clearable
        />
      </v-col>
      <v-col cols="12" sm="6" md="3" class="d-flex align-center gap-2">
        <v-btn color="primary" prepend-icon="mdi-filter" :loading="loading" @click="loadRows(true)">
          {{ t('siemCenter.events.apply') }}
        </v-btn>
        <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadRows(false)">
          {{ t('siemCenter.events.refresh') }}
        </v-btn>
      </v-col>
    </v-row>

    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-chip variant="tonal" color="primary">
        {{ t('siemCenter.events.statTotal', { shown: rows.length, total }) }}
      </v-chip>
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      class="rounded-lg"
      density="comfortable"
      hover
      @click:row="(_: unknown, ctx: { item: SecEventListItem }) => openDetail(ctx.item)"
    >
      <template #item.timestamp="{ item }">
        {{ formatDate(item.timestamp) }}
      </template>
      <template #item.eventAction="{ item }">
        <div class="d-flex flex-wrap align-center gap-1">
          <v-chip size="small" :color="actionColor(item.eventAction)" variant="tonal">
            {{ item.eventAction }}
          </v-chip>
          <v-chip
            v-if="item.baselineNewFlowPair"
            size="x-small"
            color="info"
            variant="flat"
          >
            new_flow
          </v-chip>
        </div>
      </template>
      <template #item.sourceType="{ item }">
        <span class="text-body-2">{{ item.sourceType || '—' }}</span>
      </template>
      <template #item.sourceHost="{ item }">
        <span class="text-body-2">{{ item.sourceHost || '—' }}</span>
      </template>
      <template #item.actorUser="{ item }">
        <span class="text-body-2">{{ item.actorUser || '—' }}</span>
      </template>
      <template #item.networkSrcIp="{ item }">
        <span class="text-body-2 font-weight-medium">{{ item.networkSrcIp || '—' }}</span>
      </template>
      <template #item.networkDstIp="{ item }">
        <span class="text-body-2">{{ item.networkDstIp || '—' }}</span>
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">
          {{ t('siemCenter.events.empty') }}
        </div>
      </template>
    </v-data-table>

    <v-navigation-drawer v-model="drawerOpen" location="right" width="520" temporary>
      <v-card flat v-if="selected">
        <v-card-title class="d-flex align-center">
          {{ t('siemCenter.events.detailTitle') }}
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" @click="drawerOpen = false" />
        </v-card-title>
        <v-divider />
        <v-card-text>
          <v-list density="compact">
            <v-list-item :title="t('siemCenter.events.colTime')" :subtitle="formatDate(selected.timestamp)" />
            <v-list-item :title="t('siemCenter.events.colAction')" :subtitle="displayAction(selected)" />
            <v-list-item
              v-if="selected.baselineNewFlowPair"
              :title="t('siemCenter.events.newFlowFlag')"
              subtitle="baseline.newFlowPair"
            />
            <v-list-item :title="t('siemCenter.events.colSource')" :subtitle="`${selected.sourceType || '—'} / ${selected.sourceProduct || '—'}`" />
            <v-list-item :title="t('siemCenter.events.colHost')" :subtitle="selected.sourceHost || '—'" />
            <v-list-item :title="t('siemCenter.events.colUser')" :subtitle="selected.actorUser || '—'" />
            <v-list-item :title="t('siemCenter.events.colSrcIp')" :subtitle="selected.networkSrcIp || '—'" />
            <v-list-item :title="t('siemCenter.events.colDstIp')" :subtitle="selected.networkDstIp || '—'" />
            <v-list-item v-if="selected.eventCode" :title="t('siemCenter.events.colCode')" :subtitle="selected.eventCode" />
            <v-list-item v-if="selected.parserId" :title="t('siemCenter.events.colParser')" :subtitle="selected.parserId" />
          </v-list>
          <div v-if="detailLoading" class="d-flex justify-center py-6">
            <v-progress-circular indeterminate color="primary" size="28" />
          </div>
          <div v-else-if="displayRaw" class="mt-4">
            <div class="text-caption text-medium-emphasis mb-1">{{ t('siemCenter.events.rawFull') }}</div>
            <pre class="text-body-2 pa-3 rounded bg-grey-lighten-4 overflow-auto" style="max-height: 360px; white-space: pre-wrap;">{{ displayRaw }}</pre>
          </div>
          <div v-else class="mt-4 text-medium-emphasis text-body-2">
            {{ t('siemCenter.events.rawUnavailable') }}
          </div>
        </v-card-text>
      </v-card>
    </v-navigation-drawer>
  </div>
</template>
