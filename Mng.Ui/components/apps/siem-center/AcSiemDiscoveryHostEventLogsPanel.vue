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
  isLinuxHostEventLog,
  resolveHostEventLogSourceType,
  type DiscoveryHostEventLogItem,
  type DiscoveryHostEventLogSnapshot,
} from '@/composables/useSiemDiscoveryHostEventLogs';
import {
  eventLogDetailFieldsJson,
  eventLogDetailMessageText,
} from '@/utils/windowsSecurityLogonParse';
import { copyTextToClipboard } from '@/utils/clipboard';
import type { SiemOsFamily } from '@/types/apps/siemDiscovery';

const props = defineProps<{
  hostname: string;
  /** windows | linux — selects Event Log vs journal source. */
  osFamily?: SiemOsFamily | string | null;
  /** Full host hints (IP / agent machine) for scan-IP cards. */
  host?: Pick<SiemDiscoveryHost, 'hostname' | 'ip' | 'agent'> | null;
  /** When true, optional package assignment panel loads. */
  active?: boolean;
  staleMs?: number;
}>();

const { t, locale } = useAppI18n();

const isLinux = computed(() => isLinuxHostEventLog(props.osFamily));
const sourceType = computed(() => resolveHostEventLogSourceType(props.osFamily));

const innerTab = ref<'events' | 'packages'>('events');
const loading = ref(false);
const error = ref<string | null>(null);
const snap = ref<DiscoveryHostEventLogSnapshot | null>(null);
/** Cache key: hostname + sourceType */
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

const hostHints = computed(() => props.host ?? {
  hostname: props.hostname,
  ip: props.hostname,
  agent: null,
});

const eventsHref = computed(() =>
  hostEventLogEventsLink(props.hostname, props.osFamily, hostHints.value),
);

const hintText = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogHintJournal')
    : t('siemCenter.discovery.hostDetail.eventLogHint'),
);

const emptyText = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogEmptyJournal')
    : t('siemCenter.discovery.hostDetail.eventLogEmpty'),
);

const staleText = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogStaleJournal')
    : t('siemCenter.discovery.hostDetail.eventLogStale'),
);

const openEventsLabel = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogOpenEventsJournal')
    : t('siemCenter.discovery.hostDetail.eventLogOpenEvents'),
);

const detailTitle = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogDetailTitleJournal')
    : t('siemCenter.discovery.hostDetail.eventLogDetailTitle'),
);

const channelColLabel = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogColUnit')
    : t('siemCenter.discovery.hostDetail.eventLogColChannel'),
);

const eventIdColLabel = computed(() =>
  isLinux.value
    ? t('siemCenter.discovery.hostDetail.eventLogColAction')
    : t('siemCenter.discovery.hostDetail.eventLogColEventId'),
);

const isStale = computed(() => {
  const at = snap.value?.at;
  if (at == null) return false;
  return Date.now() - at > staleThreshold.value;
});

const channelOptions = computed(() => {
  const set = new Set<string>();
  for (const x of snap.value?.items ?? []) {
    const key = channelFilterKey(x.channel, x.packageName);
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
    list = list.filter(
      (x) => channelFilterKey(x.channel, x.packageName) === filterChannel.value,
    );
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
  { title: channelColLabel.value, key: 'channel', sortable: true },
  { title: eventIdColLabel.value, key: 'eventId', sortable: true },
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

const detailBodyTab = ref<'message' | 'fields'>('message');
const detailCopyHint = ref<string | null>(null);

/** Mesaj = düz metin; Alanlar = fields JSON (birbirinin kopyası değil). */
const detailMessage = computed(() => {
  const full = detailFull.value;
  return eventLogDetailMessageText(
    full?.fields ?? selected.value?.fields,
    full?.raw,
    full?.rawPreview || selected.value?.rawPreview,
    selected.value?.message,
  );
});

const detailFieldsJson = computed(() => {
  const full = detailFull.value;
  return eventLogDetailFieldsJson(
    full?.fields ?? selected.value?.fields,
    full?.raw,
    full?.rawPreview || selected.value?.rawPreview,
  );
});

async function copyDetailTab(kind: 'message' | 'fields') {
  const label = kind === 'message'
    ? t('siemCenter.discovery.hostDetail.eventLogDetailTabMessage')
    : t('siemCenter.discovery.hostDetail.eventLogDetailTabFields');
  const value = kind === 'message' ? detailMessage.value : detailFieldsJson.value;
  if (!value?.trim()) return;
  const ok = await copyTextToClipboard(value);
  detailCopyHint.value = ok
    ? t('siemCenter.discovery.hostDetail.eventLogDetailCopied', { label })
    : t('siemCenter.discovery.hostDetail.eventLogDetailCopyFailed');
  window.setTimeout(() => {
    detailCopyHint.value = null;
  }, 2000);
}

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
  detailBodyTab.value = 'message';
  detailCopyHint.value = null;
  detailError.value = null;
  detailLoading.value = true;
  detailFull.value = {
    id: item.id,
    timestamp: item.timestamp,
    ingestedAt: item.timestamp,
    sourceType: sourceType.value,
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
      package: item.packageName,
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
  detailBodyTab.value = 'message';
  detailCopyHint.value = null;
}

function cacheKey(host: string): string {
  return `${host}|${sourceType.value}`;
}

async function load(force = false) {
  const host = props.hostname.trim();
  if (!host) return;
  const key = cacheKey(host);
  if (!force && loadedFor.value === key && snap.value) return;

  loading.value = true;
  error.value = null;
  try {
    snap.value = await fetchDiscoveryHostEventLogs(host, {
      osFamily: props.osFamily,
      host: hostHints.value,
    });
    loadedFor.value = key;
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    snap.value = null;
    loadedFor.value = null;
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.hostname, sourceType.value] as const,
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

watch(isLinux, (linux) => {
  if (linux && innerTab.value === 'packages') innerTab.value = 'events';
});

const packagesActive = computed(
  () =>
    !isLinux.value
    && props.active !== false
    && innerTab.value === 'packages',
);
</script>

<template>
  <div class="host-eventlog-panel">
    <div class="d-flex align-center flex-wrap ga-2 px-4 pt-3 pb-1">
      <v-tabs v-model="innerTab" density="compact" color="primary" class="flex-grow-1">
        <v-tab value="events">{{ t('siemCenter.discovery.hostDetail.eventLogTabEvents') }}</v-tab>
        <v-tab v-if="!isLinux" value="packages">
          {{ t('siemCenter.discovery.hostDetail.eventLogTabPackages') }}
        </v-tab>
      </v-tabs>
      <template v-if="innerTab === 'events'">
        <span class="text-caption text-medium-emphasis d-none d-sm-inline">
          {{ hintText }}
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
          {{ openEventsLabel }}
        </v-btn>
      </template>
    </div>

    <v-tabs-window v-model="innerTab">
      <v-tabs-window-item v-if="!isLinux" value="packages">
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
              {{ emptyText }}
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
              {{ staleText }}
              <span v-if="snap?.at" class="ms-1">
                ({{ formatTs(snap.at) }}
                <span v-if="ageLabel(snap.at)"> · {{ ageLabel(snap.at) }}</span>)
              </span>
            </v-alert>

            <div class="d-flex flex-wrap ga-2 mb-3 align-center">
              <v-select
                v-model="filterChannel"
                :items="channelOptions"
                :label="channelColLabel"
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
                :label="eventIdColLabel"
                density="compact"
                clearable
                hide-details
                style="max-width: 10rem"
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
            {{ detailTitle }}
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
              <tr v-if="selected.action && selected.action.trim() !== detailMessage.trim()">
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.appsColAction') }}</td>
                <td class="font-mono text-break">{{ selected.action }}</td>
              </tr>
            </tbody>
          </v-table>

          <v-alert
            v-if="detailCopyHint"
            type="success"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ detailCopyHint }}
          </v-alert>

          <div class="d-flex align-center flex-wrap ga-2 mb-2">
            <v-tabs v-model="detailBodyTab" density="compact" color="primary" class="flex-grow-1">
              <v-tab value="message">
                {{ t('siemCenter.discovery.hostDetail.eventLogDetailTabMessage') }}
              </v-tab>
              <v-tab value="fields">
                {{ t('siemCenter.discovery.hostDetail.eventLogDetailTabFields') }}
              </v-tab>
            </v-tabs>
            <v-btn
              size="small"
              variant="tonal"
              prepend-icon="mdi-content-copy"
              :disabled="detailBodyTab === 'message' ? !detailMessage.trim() : !detailFieldsJson.trim()"
              @click="copyDetailTab(detailBodyTab)"
            >
              {{ t('siemCenter.discovery.hostDetail.eventLogDetailCopy') }}
            </v-btn>
          </div>

          <v-tabs-window v-model="detailBodyTab">
            <v-tabs-window-item value="message">
              <v-sheet border rounded class="pa-3 eventlog-detail-body">
                <pre v-if="detailMessage.trim()" class="ma-0 text-body-2">{{ detailMessage }}</pre>
                <div v-else class="text-body-2 text-medium-emphasis">
                  {{ t('siemCenter.discovery.hostDetail.eventLogDetailNoMessage') }}
                </div>
              </v-sheet>
            </v-tabs-window-item>
            <v-tabs-window-item value="fields">
              <v-sheet border rounded class="pa-3 eventlog-detail-body">
                <pre v-if="detailFieldsJson.trim()" class="ma-0 text-body-2">{{ detailFieldsJson }}</pre>
                <div v-else class="text-body-2 text-medium-emphasis">
                  {{ t('siemCenter.discovery.hostDetail.eventLogDetailNoFields') }}
                </div>
              </v-sheet>
            </v-tabs-window-item>
          </v-tabs-window>
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
