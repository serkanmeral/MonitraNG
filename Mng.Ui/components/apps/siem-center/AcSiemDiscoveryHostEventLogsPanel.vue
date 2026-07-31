<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { secEventGet } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import AcSiemDiscoveryHostPackagesPanel from '@/components/apps/siem-center/AcSiemDiscoveryHostPackagesPanel.vue';
import {
  DISCOVERY_EVENTLOG_STALE_MS,
  channelFilterKey,
  eventLogLevelTone,
  fetchDiscoveryHostEventLogs,
  hostEventLogEventsLink,
  type DiscoveryHostEventLogItem,
  type DiscoveryHostEventLogSnapshot,
} from '@/composables/useSiemDiscoveryHostEventLogs';

const props = defineProps<{
  hostname: string;
  /** When true, optional package assignment panel loads. */
  active?: boolean;
  staleMs?: number;
}>();

const { t, locale } = useAppI18n();

const innerTab = ref<'events' | 'packages'>('events');
const loading = ref(false);
const error = ref<string | null>(null);
const snap = ref<DiscoveryHostEventLogSnapshot | null>(null);
const loadedFor = ref<string | null>(null);
const filterChannel = ref<string | null>(null);
const filterPackage = ref<string | null>(null);
const filterEventId = ref('');
const page = ref(1);
const itemsPerPage = ref(10);
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([
  { key: 'at', order: 'desc' },
]);

const detailOpen = ref(false);
const selected = ref<DiscoveryHostEventLogItem | null>(null);
const detailLoading = ref(false);
const detailError = ref<string | null>(null);
const detailFull = ref<SecEventListItem | null>(null);

const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

const staleThreshold = computed(() =>
  props.staleMs != null && props.staleMs > 0 ? props.staleMs : DISCOVERY_EVENTLOG_STALE_MS,
);

const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const eventsHref = computed(() => hostEventLogEventsLink(props.hostname));

const isStale = computed(() => {
  const at = snap.value?.at;
  if (at == null) return false;
  return Date.now() - at > staleThreshold.value;
});

const channelOptions = computed(() => {
  const set = new Set<string>();
  for (const x of snap.value?.items ?? []) {
    const key = channelFilterKey(x.channel);
    if (key) set.add(key);
  }
  return [...set].sort((a, b) => a.localeCompare(b)).map((v) => ({ title: v, value: v }));
});

const packageOptions = computed(() => {
  const set = new Set<string>();
  for (const x of snap.value?.items ?? []) {
    const p = (x.packageName || '').trim();
    if (p) set.add(p);
  }
  return [...set].sort((a, b) => a.localeCompare(b)).map((v) => ({ title: v, value: v }));
});

const filteredItems = computed(() => {
  let list = snap.value?.items ?? [];
  if (filterChannel.value) {
    list = list.filter((x) => channelFilterKey(x.channel) === filterChannel.value);
  }
  if (filterPackage.value) {
    list = list.filter(
      (x) => (x.packageName || '').toLowerCase() === filterPackage.value!.toLowerCase(),
    );
  }
  const idQ = filterEventId.value.trim();
  if (idQ) {
    list = list.filter((x) => String(x.eventId ?? '').includes(idQ));
  }
  return list;
});

const hasAny = computed(() => (snap.value?.items.length ?? 0) > 0);

const headers = computed(() => [
  { title: t('siemCenter.discovery.hostDetail.eventLogColTime'), key: 'at', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.eventLogColChannel'), key: 'channel', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.eventLogColEventId'), key: 'eventId', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.eventLogColPackage'), key: 'packageName', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.eventLogColLevel'), key: 'level', sortable: true },
  { title: t('siemCenter.discovery.hostDetail.eventLogColPreview'), key: 'message', sortable: false },
  {
    title: t('siemCenter.discovery.hostDetail.eventLogColActions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
    width: '72px',
  },
]);

const detailMessage = computed(() => {
  const full = detailFull.value;
  if (full?.raw) return full.raw;
  if (full?.rawPreview) return full.rawPreview;
  return selected.value?.message || '—';
});

const detailFieldsJson = computed(() => {
  const fields = detailFull.value?.fields ?? null;
  if (!fields || typeof fields !== 'object') return null;
  try {
    return JSON.stringify(fields, null, 2);
  } catch {
    return null;
  }
});

watch([filterChannel, filterPackage, filterEventId], () => {
  page.value = 1;
});

watch(
  () => snap.value?.items.length,
  () => {
    page.value = 1;
  },
);

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

function levelLabel(level?: string | null): string {
  if (!level) return '—';
  const key = `siemCenter.discovery.hostDetail.eventLogLevel.${level}`;
  const translated = t(key);
  return translated === key ? level : translated;
}

function truncate(text: string | null | undefined, max = 48): string {
  if (!text) return '—';
  const t0 = text.replace(/\s+/g, ' ').trim();
  if (t0.length <= max) return t0;
  return `${t0.slice(0, max)}…`;
}

function clearFilters() {
  filterChannel.value = null;
  filterPackage.value = null;
  filterEventId.value = '';
}

async function openDetail(item: DiscoveryHostEventLogItem) {
  selected.value = item;
  detailOpen.value = true;
  detailError.value = null;
  detailLoading.value = true;
  detailFull.value = {
    id: item.id,
    timestamp: item.timestamp,
    ingestedAt: item.timestamp,
    sourceType: 'windows-eventlog',
    sourceProduct: item.packageName,
    sourceHost: item.sourceHost ?? null,
    eventAction: item.eventAction || item.action || '',
    eventOutcome: item.level,
    eventCode: item.eventId,
    actorUser: null,
    networkSrcIp: null,
    networkDstIp: null,
    parserId: null,
    rawPreview: item.rawPreview || item.message,
    raw: null,
    baselineNewFlowPair: false,
    fields: item.fields ?? {
      channel: item.channel,
      eventId: item.eventId,
      provider: item.provider,
      message: item.message,
    },
  };
  try {
    detailFull.value = await secEventGet(item.id);
    detailError.value = null;
  } catch (e: unknown) {
    const hasBody = !!(item.message || item.rawPreview || item.eventAction);
    detailError.value = hasBody ? null : (e instanceof Error ? e.message : String(e));
  } finally {
    detailLoading.value = false;
  }
}

function closeDetail() {
  detailOpen.value = false;
  selected.value = null;
  detailFull.value = null;
  detailError.value = null;
}

async function load(force = false) {
  const host = props.hostname.trim();
  if (!host) return;
  if (!force && loadedFor.value === host && snap.value) return;

  loading.value = true;
  error.value = null;
  try {
    snap.value = await fetchDiscoveryHostEventLogs(host);
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
  () => props.hostname,
  () => {
    snap.value = null;
    loadedFor.value = null;
    innerTab.value = 'events';
    clearFilters();
    page.value = 1;
    sortBy.value = [{ key: 'at', order: 'desc' }];
    closeDetail();
    void load(true);
  },
  { immediate: true },
);

const packagesActive = computed(
  () => props.active !== false && innerTab.value === 'packages',
);
</script>

<template>
  <div class="host-eventlog-panel">
    <div class="d-flex align-center flex-wrap ga-2 px-4 pt-3 pb-1">
      <v-tabs v-model="innerTab" density="compact" color="primary" class="flex-grow-1">
        <v-tab value="events">{{ t('siemCenter.discovery.hostDetail.eventLogTabEvents') }}</v-tab>
        <v-tab value="packages">{{ t('siemCenter.discovery.hostDetail.eventLogTabPackages') }}</v-tab>
      </v-tabs>
      <template v-if="innerTab === 'events'">
        <span class="text-caption text-medium-emphasis d-none d-sm-inline">
          {{ t('siemCenter.discovery.hostDetail.eventLogHint') }}
        </span>
        <v-btn
          size="small"
          variant="text"
          prepend-icon="mdi-refresh"
          :loading="loading"
          @click="load(true)"
        >
          {{ t('siemCenter.discovery.hostDetail.eventLogRefresh') }}
        </v-btn>
        <v-btn
          size="small"
          variant="text"
          prepend-icon="mdi-timeline-text-outline"
          :to="eventsHref"
          target="_blank"
          rel="noopener noreferrer"
        >
          {{ t('siemCenter.discovery.hostDetail.eventLogOpenEvents') }}
        </v-btn>
      </template>
    </div>

    <v-tabs-window v-model="innerTab">
      <v-tabs-window-item value="packages">
        <div class="pa-4 pt-2">
          <AcSiemDiscoveryHostPackagesPanel
            :hostname="hostname"
            :active="packagesActive"
          />
        </div>
      </v-tabs-window-item>

      <v-tabs-window-item value="events">
        <div class="pa-4 pt-2">
          <v-alert v-if="error" type="warning" variant="tonal" density="compact" class="mb-3">
            {{ error }}
          </v-alert>

          <v-skeleton-loader v-if="loading && !snap" type="table" />

          <template v-else-if="!hasAny">
            <v-sheet border rounded class="pa-3 text-medium-emphasis text-body-2">
              {{ t('siemCenter.discovery.hostDetail.eventLogEmpty') }}
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
              {{ t('siemCenter.discovery.hostDetail.eventLogStale') }}
              <span v-if="snap?.at" class="ms-1">
                ({{ formatTs(snap.at) }}
                <span v-if="ageLabel(snap.at)"> · {{ ageLabel(snap.at) }}</span>)
              </span>
            </v-alert>

            <div class="d-flex flex-wrap ga-2 mb-3 align-center">
              <v-select
                v-model="filterChannel"
                :items="channelOptions"
                :label="t('siemCenter.discovery.hostDetail.eventLogColChannel')"
                density="compact"
                clearable
                hide-details
                style="max-width: 11rem"
              />
              <v-select
                v-model="filterPackage"
                :items="packageOptions"
                :label="t('siemCenter.discovery.hostDetail.eventLogColPackage')"
                density="compact"
                clearable
                hide-details
                style="max-width: 12rem"
              />
              <v-text-field
                v-model="filterEventId"
                :label="t('siemCenter.discovery.hostDetail.eventLogColEventId')"
                density="compact"
                clearable
                hide-details
                style="max-width: 8rem"
              />
              <v-btn size="small" variant="text" @click="clearFilters">
                {{ t('siemCenter.discovery.hostDetail.eventLogClearFilters') }}
              </v-btn>
              <v-spacer />
              <span class="text-caption text-medium-emphasis">
                {{ t('siemCenter.discovery.hostDetail.eventLogCount', { n: filteredItems.length }) }}
                <template v-if="snap?.at">
                  · {{ t('siemCenter.discovery.hostDetail.appsLastSample') }}:
                  {{ formatTs(snap.at) }}
                  <span v-if="ageLabel(snap.at)"> ({{ ageLabel(snap.at) }})</span>
                </template>
              </span>
            </div>

            <v-data-table
              v-model:page="page"
              v-model:items-per-page="itemsPerPage"
              v-model:sort-by="sortBy"
              :headers="headers"
              :items="filteredItems"
              :loading="loading"
              item-value="id"
              density="compact"
              class="eventlog-table rounded-lg"
              :items-per-page-options="PAGE_SIZE_OPTIONS"
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
              <template #item.channel="{ item }">
                <div class="font-mono text-body-2 text-truncate eventlog-channel">{{ item.channel }}</div>
              </template>
              <template #item.eventId="{ item }">
                <span class="font-mono">{{ item.eventId || '—' }}</span>
              </template>
              <template #item.packageName="{ item }">
                <v-chip v-if="item.packageName" size="x-small" variant="tonal">
                  {{ item.packageName }}
                </v-chip>
                <span v-else>—</span>
              </template>
              <template #item.level="{ item }">
                <v-chip size="x-small" variant="flat" :color="eventLogLevelTone(item.level)">
                  {{ levelLabel(item.level) }}
                </v-chip>
              </template>
              <template #item.message="{ item }">
                <span class="text-body-2 text-medium-emphasis text-truncate d-inline-block eventlog-preview">
                  {{ truncate(item.message || item.action) }}
                </span>
              </template>
              <template #item.actions="{ item }">
                <v-tooltip :text="t('siemCenter.discovery.hostDetail.eventLogDetail')" location="top">
                  <template #activator="{ props: tip }">
                    <v-btn
                      v-bind="tip"
                      icon="mdi-eye-outline"
                      size="small"
                      variant="text"
                      @click.stop="openDetail(item)"
                    />
                  </template>
                </v-tooltip>
              </template>
            </v-data-table>
          </template>
        </div>
      </v-tabs-window-item>
    </v-tabs-window>

    <v-dialog
      :model-value="detailOpen"
      max-width="720"
      scrollable
      @update:model-value="(v: boolean) => { if (!v) closeDetail(); }"
    >
      <v-card v-if="selected">
        <v-card-title class="d-flex align-center flex-wrap ga-2 pe-2">
          <span class="text-subtitle-1">
            {{ t('siemCenter.discovery.hostDetail.eventLogDetailTitle') }}
          </span>
          <v-chip
            v-if="selected.eventId"
            size="small"
            variant="tonal"
            class="font-mono"
          >
            {{ selected.eventId }}
          </v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeDetail" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <v-alert v-if="detailError" type="warning" variant="tonal" density="compact" class="mb-3">
            {{ detailError }}
            <div class="text-caption mt-1">
              {{ t('siemCenter.discovery.hostDetail.eventLogDetailPartial') }}
            </div>
          </v-alert>

          <v-skeleton-loader v-if="detailLoading" type="article" class="mb-3" />

          <v-table density="compact" class="mb-4 eventlog-detail-meta">
            <tbody>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColTime') }}</td>
                <td>{{ formatTs(selected.at) }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColChannel') }}</td>
                <td class="font-mono text-break">{{ selected.channel }}</td>
              </tr>
              <tr v-if="selected.provider">
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColProvider') }}</td>
                <td class="font-mono text-break">{{ selected.provider }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColPackage') }}</td>
                <td>{{ selected.packageName || '—' }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColLevel') }}</td>
                <td>
                  <v-chip size="x-small" variant="flat" :color="eventLogLevelTone(selected.level)">
                    {{ levelLabel(selected.level) }}
                  </v-chip>
                </td>
              </tr>
              <tr v-if="selected.action">
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.appsColAction') }}</td>
                <td class="font-mono text-break">{{ selected.action }}</td>
              </tr>
            </tbody>
          </v-table>

          <div class="text-subtitle-2 mb-2">
            {{ t('siemCenter.discovery.hostDetail.eventLogColMessage') }}
          </div>
          <v-sheet border rounded class="pa-3 mb-4 eventlog-detail-body">
            <pre class="ma-0 text-body-2">{{ detailMessage }}</pre>
          </v-sheet>

          <template v-if="detailFieldsJson">
            <div class="text-subtitle-2 mb-2">
              {{ t('siemCenter.discovery.hostDetail.eventLogDetailFields') }}
            </div>
            <v-sheet border rounded class="pa-3 eventlog-detail-body">
              <pre class="ma-0 text-body-2">{{ detailFieldsJson }}</pre>
            </v-sheet>
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-3">
          <v-spacer />
          <v-btn variant="text" @click="closeDetail">
            {{ t('siemCenter.discovery.hostDetail.close') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.eventlog-table :deep(td) {
  vertical-align: middle;
}

.eventlog-preview {
  max-width: 12rem;
}

.eventlog-channel {
  max-width: 9rem;
}

.eventlog-detail-meta :deep(td) {
  padding-block: 6px !important;
  vertical-align: top;
}

.eventlog-detail-meta :deep(td:first-child) {
  width: 7.5rem;
  white-space: nowrap;
}

.eventlog-detail-body pre {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  max-height: 20rem;
  overflow: auto;
}
</style>
