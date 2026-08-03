<script setup lang="ts">
import { computed, ref, shallowRef, watch } from 'vue';
import { useTheme } from 'vuetify';
import { useAppI18n } from '@/composables/useAppI18n';
import { secEventGet } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import {
  channelFilterKey,
  eventLogLevelTone,
  type DiscoveryHostEventLogItem,
} from '@/composables/useSiemDiscoveryHostEventLogs';
import type {
  HostAnalyticsChannelCount,
  HostAnalyticsLevelCount,
} from '@/composables/useSiemHostAnalytics';
import {
  eventLogDetailFieldsJson,
  eventLogDetailMessageText,
} from '@/utils/windowsSecurityLogonParse';
import { copyTextToClipboard } from '@/utils/clipboard';
import type { SiemOsFamily } from '@/types/apps/siemDiscovery';

const props = defineProps<{
  channelCounts: HostAnalyticsChannelCount[];
  levelCounts: HostAnalyticsLevelCount[];
  /** Full Event Log sample for the dashboard range. */
  events: DiscoveryHostEventLogItem[];
  loading?: boolean;
  eventsHref?: string;
  osFamily?: SiemOsFamily | string | null;
}>();

const { t, locale } = useAppI18n();
const theme = useTheme();
const getPrimary = computed(() => theme.current.value.colors.primary || '#5D87FF');
const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const isLinux = computed(
  () => (props.osFamily || '').toString().trim().toLowerCase() === 'linux',
);
const eventLogSourceType = computed(() =>
  isLinux.value ? 'linux-journal' : 'windows-eventlog',
);

const selectedChannel = ref<string | null>(null);
const page = ref(1);
const itemsPerPage = ref(10);
const sortBy = ref<{ key: string; order: 'asc' | 'desc' }[]>([{ key: 'at', order: 'desc' }]);
const PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

const detailOpen = ref(false);
const detailLoading = ref(false);
const detailError = ref<string | null>(null);
const selected = ref<DiscoveryHostEventLogItem | null>(null);
const detailFull = ref<SecEventListItem | null>(null);

const hasDonut = computed(() => props.channelCounts.some((c) => c.count > 0));

const filteredEvents = computed(() => {
  const all = props.events ?? [];
  if (!selectedChannel.value) return all;
  const want = selectedChannel.value;
  return all.filter((row) => channelFilterKey(row.channel, row.packageName) === want);
});

const headers = computed(() => [
  { title: t('siemCenter.hostDashboard.colTime'), key: 'at', sortable: true },
  {
    title: isLinux.value
      ? t('siemCenter.hostDashboard.colUnit')
      : t('siemCenter.hostDashboard.colChannel'),
    key: 'channel',
    sortable: true,
  },
  { title: t('siemCenter.hostDashboard.colLevel'), key: 'level', sortable: true },
  {
    title: isLinux.value
      ? t('siemCenter.hostDashboard.colAction')
      : t('siemCenter.hostDashboard.colEventId'),
    key: 'eventId',
    sortable: true,
  },
  { title: t('siemCenter.hostDashboard.colMessage'), key: 'message', sortable: false },
  {
    title: t('siemCenter.hostDashboard.colActions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
  },
]);

const eventLogTitle = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.eventLogTitleJournal')
    : t('siemCenter.hostDashboard.eventLogTitle'),
);

const eventLogHint = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.eventLogHintJournal')
    : t('siemCenter.hostDashboard.eventLogHint'),
);

const eventLogEmpty = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.eventLogEmptyJournal')
    : t('siemCenter.hostDashboard.eventLogEmpty'),
);

const eventLogDetailTitle = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.eventLogDetailTitleJournal')
    : t('siemCenter.hostDashboard.eventLogDetailTitle'),
);

watch(selectedChannel, () => {
  page.value = 1;
});

watch(
  () => props.events,
  () => {
    page.value = 1;
  },
);

function selectChannel(label: string | null | undefined) {
  if (!label) return;
  selectedChannel.value = selectedChannel.value === label ? null : label;
}

function clearChannelFilter() {
  selectedChannel.value = null;
}

function channelIndexFromConfig(config: {
  dataPointIndex?: number;
  seriesIndex?: number;
}): number {
  const idx = config?.dataPointIndex;
  if (typeof idx === 'number' && idx >= 0) return idx;
  const seriesIdx = config?.seriesIndex;
  if (typeof seriesIdx === 'number' && seriesIdx >= 0) return seriesIdx;
  return -1;
}

function onDonutPointSelect(
  _event: unknown,
  _chartContext: unknown,
  config: { dataPointIndex?: number; seriesIndex?: number },
) {
  const idx = channelIndexFromConfig(config);
  if (idx < 0) return;
  selectChannel(props.channelCounts[idx]?.channel);
}

function onDonutLegendClick(
  _chartContext: unknown,
  seriesIndex: number,
) {
  if (typeof seriesIndex !== 'number' || seriesIndex < 0) return;
  selectChannel(props.channelCounts[seriesIndex]?.channel);
  // Prevent ApexCharts default legend toggle (hides series)
  return false;
}

const donutSeries = computed(() => props.channelCounts.map((c) => c.count));

/** Stable options — reactive computed recreates handlers and breaks Apex events. */
const donutOptions = shallowRef<Record<string, unknown>>({});

function rebuildDonutOptions() {
  donutOptions.value = {
    chart: {
      type: 'donut',
      fontFamily: 'inherit',
      foreColor: 'rgba(var(--v-theme-on-surface), 0.55)',
      events: {
        dataPointSelection: onDonutPointSelect,
        legendClick: onDonutLegendClick,
      },
    },
    labels: props.channelCounts.map((c) => c.channel),
    colors: [getPrimary.value, '#49BEFF', '#FA896B', '#13DEB9', '#FFAE1F', '#5D87FF'],
    legend: {
      position: 'bottom',
      onItemClick: { toggleDataSeries: false },
    },
    dataLabels: { enabled: false },
    tooltip: { theme: 'dark' },
    plotOptions: {
      pie: {
        donut: { size: '68%' },
        expandOnClick: true,
      },
    },
    states: {
      active: {
        allowMultipleDataPointsSelection: false,
        filter: { type: 'none' },
      },
    },
  };
}

watch(
  () => [props.channelCounts, getPrimary.value] as const,
  () => rebuildDonutOptions(),
  { immediate: true, deep: true },
);

function formatTs(ms: number): string {
  try {
    return new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(ms));
  } catch {
    return new Date(ms).toISOString();
  }
}

function levelLabel(level: string | null | undefined): string {
  if (!level) return '—';
  const key = `siemCenter.discovery.hostDetail.eventLogLevel.${level}`;
  const translated = t(key);
  return translated !== key ? translated : level;
}

function truncate(text: string | null | undefined, max = 100): string {
  const s = (text || '').trim();
  if (!s) return '—';
  return s.length > max ? `${s.slice(0, max)}…` : s;
}

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

async function openDetail(row: DiscoveryHostEventLogItem) {
  selected.value = row;
  detailOpen.value = true;
  detailBodyTab.value = 'message';
  detailCopyHint.value = null;
  detailError.value = null;
  detailLoading.value = true;
  // Seed from list row immediately (works even when GetById fails on slash-ids)
  detailFull.value = {
    id: row.id,
    timestamp: row.timestamp,
    ingestedAt: row.timestamp,
    sourceType: eventLogSourceType.value,
    sourceProduct: row.packageName,
    sourceHost: row.sourceHost ?? null,
    eventAction: row.eventAction || row.action || '',
    eventOutcome: row.level,
    eventCode: row.eventId,
    actorUser: null,
    networkSrcIp: null,
    networkDstIp: null,
    parserId: null,
    rawPreview: row.rawPreview || row.message,
    raw: null,
    baselineNewFlowPair: false,
    fields: row.fields ?? {
      channel: row.channel,
      eventId: row.eventId,
      provider: row.provider,
      message: row.message,
    },
  };
  try {
    detailFull.value = await secEventGet(row.id);
    detailError.value = null;
  } catch (e: unknown) {
    // Keep list seed; only show warning if we truly have little text
    const hasBody = !!(row.message || row.rawPreview || row.eventAction);
    if (!hasBody) {
      detailError.value = e instanceof Error ? e.message : String(e);
    } else {
      detailError.value = null;
    }
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
</script>

<template>
  <v-card variant="outlined" class="rounded-lg pa-4">
    <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-3">
      <div>
        <h3 class="text-subtitle-1 font-weight-bold mb-0">
          {{ eventLogTitle }}
        </h3>
        <p class="text-caption text-medium-emphasis mb-0">
          {{ eventLogHint }}
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

    <v-skeleton-loader v-if="loading" type="image, table" />
    <template v-else>
      <v-row dense class="mb-2">
        <v-col cols="12" md="4">
          <div v-if="!hasDonut" class="host-chart-empty">
            {{ eventLogEmpty }}
          </div>
          <ClientOnly v-else>
            <apexchart
              type="donut"
              height="220"
              :options="donutOptions"
              :series="donutSeries"
            />
          </ClientOnly>
          <p class="text-caption text-medium-emphasis text-center mb-2">
            {{ t('siemCenter.hostDashboard.eventLogPieHint') }}
          </p>
          <div class="d-flex flex-wrap ga-1 justify-center mb-2">
            <v-chip
              v-for="ch in channelCounts"
              :key="ch.channel"
              size="small"
              :color="selectedChannel === ch.channel ? 'primary' : undefined"
              :variant="selectedChannel === ch.channel ? 'flat' : 'tonal'"
              class="cursor-pointer"
              @click="selectChannel(ch.channel)"
            >
              {{ ch.channel }}: {{ ch.count }}
            </v-chip>
            <v-chip
              v-if="selectedChannel"
              size="small"
              variant="outlined"
              class="cursor-pointer"
              @click="clearChannelFilter"
            >
              {{ t('siemCenter.hostDashboard.eventLogFilterClear') }}
            </v-chip>
          </div>
          <div v-if="levelCounts.length" class="d-flex flex-wrap ga-1 mt-1 justify-center">
            <v-chip
              v-for="lv in levelCounts.slice(0, 6)"
              :key="lv.level"
              size="x-small"
              variant="text"
            >
              {{ levelLabel(lv.level) }}: {{ lv.count }}
            </v-chip>
          </div>
        </v-col>
        <v-col cols="12" md="8">
          <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-2">
            <div class="text-subtitle-2 mb-0">
              {{ t('siemCenter.hostDashboard.eventLogTableTitle') }}
              <span class="text-caption text-medium-emphasis font-weight-regular ms-1">
                ({{ filteredEvents.length }})
              </span>
            </div>
            <v-chip
              v-if="selectedChannel"
              size="small"
              color="primary"
              variant="tonal"
              closable
              @click:close="clearChannelFilter"
            >
              {{ t('siemCenter.hostDashboard.eventLogFilterChannel', { channel: selectedChannel }) }}
            </v-chip>
          </div>

          <v-data-table
            v-model:page="page"
            v-model:items-per-page="itemsPerPage"
            v-model:sort-by="sortBy"
            :headers="headers"
            :items="filteredEvents"
            item-value="id"
            density="compact"
            class="host-elog-table"
            :items-per-page-options="PAGE_SIZE_OPTIONS"
            :no-data-text="
              selectedChannel
                ? t('siemCenter.hostDashboard.eventLogEmptyFiltered')
                : t('siemCenter.hostDashboard.eventLogEmpty')
            "
          >
            <template #item.at="{ item }">
              <span class="text-no-wrap">{{ formatTs(item.at) }}</span>
            </template>
            <template #item.channel="{ item }">
              <span class="font-mono text-truncate d-inline-block" style="max-width: 9rem" :title="item.channel">
                {{ item.channel }}
              </span>
            </template>
            <template #item.level="{ item }">
              <v-chip size="x-small" :color="eventLogLevelTone(item.level)" variant="tonal">
                {{ levelLabel(item.level) }}
              </v-chip>
            </template>
            <template #item.eventId="{ item }">
              <span class="font-mono">{{
                isLinux
                  ? (item.eventAction || item.action || item.eventId || '—')
                  : (item.eventId || '—')
              }}</span>
            </template>
            <template #item.message="{ item }">
              <span
                class="text-caption text-medium-emphasis text-truncate d-inline-block"
                style="max-width: 16rem"
                :title="item.message || item.action || undefined"
              >
                {{ truncate(item.message || item.action) }}
              </span>
            </template>
            <template #item.actions="{ item }">
              <v-tooltip :text="t('siemCenter.hostDashboard.eventLogDetail')" location="top">
                <template #activator="{ props: tip }">
                  <v-btn
                    v-bind="tip"
                    icon="mdi-eye-outline"
                    size="small"
                    variant="text"
                    @click="openDetail(item)"
                  />
                </template>
              </v-tooltip>
            </template>
          </v-data-table>
        </v-col>
      </v-row>
    </template>

    <v-dialog
      :model-value="detailOpen"
      max-width="720"
      scrollable
      @update:model-value="(v: boolean) => { if (!v) closeDetail(); }"
    >
      <v-card v-if="selected">
        <v-card-title class="d-flex align-center flex-wrap ga-2 pe-2">
          <span class="text-subtitle-1">
            {{ eventLogDetailTitle }}
          </span>
          <v-chip
            v-if="selected.eventId"
            size="small"
            variant="tonal"
            class="font-mono"
          >
            {{ selected.eventId }}
          </v-chip>
          <v-chip
            size="small"
            :color="eventLogLevelTone(selected.level)"
            variant="tonal"
          >
            {{ levelLabel(selected.level) }}
          </v-chip>
          <v-spacer />
          <v-btn icon="mdi-close" variant="text" size="small" @click="closeDetail" />
        </v-card-title>
        <v-divider />
        <v-card-text class="pt-4">
          <v-alert v-if="detailError" type="warning" variant="tonal" density="compact" class="mb-3">
            {{ detailError }}
            <div class="text-caption mt-1">
              {{ t('siemCenter.hostDashboard.eventLogDetailPartial') }}
            </div>
          </v-alert>

          <v-skeleton-loader v-if="detailLoading" type="article" class="mb-3" />

          <v-table density="compact" class="mb-4 eventlog-detail-meta">
            <tbody>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colTime') }}</td>
                <td>{{ formatTs(selected.at) }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{
                  isLinux
                    ? t('siemCenter.hostDashboard.colUnit')
                    : t('siemCenter.hostDashboard.colChannel')
                }}</td>
                <td class="font-mono text-break">{{ selected.channel }}</td>
              </tr>
              <tr v-if="selected.provider">
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColProvider') }}</td>
                <td class="font-mono text-break">{{ selected.provider }}</td>
              </tr>
              <tr v-if="selected.packageName">
                <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.eventLogColPackage') }}</td>
                <td>{{ selected.packageName }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colLevel') }}</td>
                <td>
                  <v-chip size="x-small" variant="flat" :color="eventLogLevelTone(selected.level)">
                    {{ levelLabel(selected.level) }}
                  </v-chip>
                </td>
              </tr>
              <tr v-if="selected.action && selected.action.trim() !== detailMessage.trim()">
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colAction') }}</td>
                <td class="font-mono text-break">{{ selected.action }}</td>
              </tr>
              <tr v-if="detailFull?.sourceHost">
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colHost') }}</td>
                <td class="font-mono">{{ detailFull.sourceHost }}</td>
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
  </v-card>
</template>

<style scoped>
.host-elog-table :deep(td),
.host-elog-table :deep(th) {
  font-size: 0.75rem;
  vertical-align: middle;
}
.host-chart-empty {
  min-height: 180px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.875rem;
  text-align: center;
  padding: 1rem;
}
.cursor-pointer {
  cursor: pointer;
}
.eventlog-detail-meta :deep(td:first-child) {
  width: 8rem;
  white-space: nowrap;
}
.eventlog-detail-body pre {
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 280px;
  overflow: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.75rem;
}
</style>
