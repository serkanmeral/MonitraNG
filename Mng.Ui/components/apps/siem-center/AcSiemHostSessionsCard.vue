<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { secEventGet } from '@/services/secEventService';
import type { SecEventListItem } from '@/types/apps/secEvent';
import type {
  HostAnalyticsRange,
  HostSessionHistoryItem,
  HostSessionHistoryKind,
} from '@/composables/useSiemHostAnalytics';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';
import {
  isInteractiveLogonType,
  isWindowsMachineAccount,
  parseWindowsRdpSessionMessage,
  parseWindowsSecurityLogonMessage,
  eventLogDetailFieldsJson,
  eventLogDetailMessageText,
  securityMessageFromEventFields,
  SESSION_HISTORY_RDP_EVENT_IDS,
} from '@/utils/windowsSecurityLogonParse';
import { copyTextToClipboard } from '@/utils/clipboard';

const props = defineProps<{
  host: SiemDiscoveryHost;
  sessionHistory: HostSessionHistoryItem[];
  /** Dashboard-selected analytics range (same as top picker). */
  range?: HostAnalyticsRange | null;
  loading?: boolean;
  eventsHref?: string;
}>();

const { t, locale } = useAppI18n();
const dateLocale = computed(() => (locale.value === 'tr' ? 'tr-TR' : 'en-GB'));

const agent = computed(() => props.host.agent ?? null);
const sessions = computed(() => agent.value?.sessions ?? []);
const isLinux = computed(
  () => (props.host.osFamily || '').toString().trim().toLowerCase() === 'linux',
);

/** Default: human-oriented sessions; hide service/machine noise (e.g. type 5 + HOST$). */
const historyFilter = ref<'users' | 'all'>('users');
const historyPage = ref(1);
const historyItemsPerPage = ref(10);
const HISTORY_PAGE_SIZE_OPTIONS = [5, 10, 25, 50];

function isUserFacingSessionEvent(row: HostSessionHistoryItem): boolean {
  if (
    row.kind === 'ssh_logon'
    || row.kind === 'ssh_failed'
    || row.kind === 'sudo'
    || row.kind === 'failed'
  ) {
    return true;
  }

  // RDP LocalSessionManager events are always user-oriented
  if (
    row.kind === 'rdp_logon'
    || row.kind === 'rdp_logoff'
    || row.kind === 'rdp_disconnect'
    || row.kind === 'rdp_reconnect'
  ) {
    const account = row.user || row.subjectUser;
    if (isWindowsMachineAccount(account)) return false;
    return true;
  }

  const account = row.user || row.subjectUser;
  if (isWindowsMachineAccount(account)) return false;

  if (row.kind === 'logon') {
    if (row.logonType) return isInteractiveLogonType(row.logonType);
    return true;
  }

  if (row.kind === 'logoff') return true;
  return true;
}

const historyStats = computed(() => {
  const all = props.sessionHistory;
  let users = 0;
  for (const row of all) {
    if (isUserFacingSessionEvent(row)) users += 1;
  }
  return { total: all.length, users, noise: all.length - users };
});

const history = computed(() => {
  if (historyFilter.value === 'users') {
    return props.sessionHistory.filter(isUserFacingSessionEvent);
  }
  return props.sessionHistory;
});

const rangeLabel = computed(() => {
  const range = props.range;
  if (!range) return '';
  if (range.timeRange !== 'custom') {
    const keyMap: Record<string, string> = {
      '1h': 'range1h',
      '6h': 'range6h',
      '24h': 'range24h',
      '7d': 'range7d',
    };
    const key = keyMap[range.timeRange];
    return key ? t(`siemCenter.hostDashboard.${key}`) : range.timeRange;
  }
  try {
    const fmt = new Intl.DateTimeFormat(dateLocale.value, {
      dateStyle: 'short',
      timeStyle: 'short',
    });
    return `${fmt.format(new Date(range.fromMs))} – ${fmt.format(new Date(range.toMs))}`;
  } catch {
    return `${range.from} – ${range.to || ''}`;
  }
});

const historyHeaders = computed(() => [
  { title: t('siemCenter.hostDashboard.colTime'), key: 'at', sortable: true },
  { title: t('siemCenter.hostDashboard.colKind'), key: 'kind', sortable: true },
  {
    title: isLinux.value
      ? t('siemCenter.hostDashboard.colAction')
      : t('siemCenter.hostDashboard.colEventId'),
    key: 'eventId',
    sortable: true,
  },
  {
    title: isLinux.value
      ? t('siemCenter.hostDashboard.colSourceAddress')
      : t('siemCenter.hostDashboard.colLogonType'),
    key: 'logonType',
    sortable: true,
  },
  { title: t('siemCenter.hostDashboard.colUser'), key: 'user', sortable: true },
  {
    title: t('siemCenter.hostDashboard.colActions'),
    key: 'actions',
    sortable: false,
    align: 'end' as const,
  },
]);

const sessionsHint = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.sessionsHintLinux')
    : t('siemCenter.hostDashboard.sessionsHint'),
);

const sessionsHistoryHint = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.sessionsHistoryHintLinux')
    : t('siemCenter.hostDashboard.sessionsHistoryHint'),
);

const sessionsHistoryEmpty = computed(() =>
  isLinux.value
    ? t('siemCenter.hostDashboard.sessionsHistoryEmptyLinux')
    : t('siemCenter.hostDashboard.sessionsHistoryEmpty'),
);

watch(historyFilter, () => {
  historyPage.value = 1;
});

watch(
  () => props.sessionHistory,
  () => {
    historyPage.value = 1;
  },
);

const detailOpen = ref(false);
const detailLoading = ref(false);
const detailError = ref<string | null>(null);
const selected = ref<HostSessionHistoryItem | null>(null);
const detailFull = ref<SecEventListItem | null>(null);

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

function formatUptimeSeconds(sec?: number | null): string {
  if (sec == null || Number.isNaN(sec) || sec < 0) return '—';
  const s = Math.floor(sec);
  if (s < 60) return t('siemCenter.discovery.hostDetail.uptimeSeconds', { n: s });
  const min = Math.floor(s / 60);
  if (min < 60) return t('siemCenter.discovery.hostDetail.uptimeMinutes', { n: min });
  const hr = Math.floor(min / 60);
  const remMin = min % 60;
  if (hr < 48) {
    return remMin
      ? t('siemCenter.discovery.hostDetail.uptimeHoursMinutes', { h: hr, m: remMin })
      : t('siemCenter.discovery.hostDetail.uptimeHours', { n: hr });
  }
  const day = Math.floor(hr / 24);
  return t('siemCenter.discovery.hostDetail.uptimeDays', { n: day });
}

function kindLabel(kind: HostSessionHistoryKind): string {
  return t(`siemCenter.hostDashboard.sessionKind.${kind}`);
}

function kindColor(kind: HostSessionHistoryKind): string {
  if (kind === 'logon' || kind === 'rdp_logon' || kind === 'rdp_reconnect' || kind === 'ssh_logon') {
    return 'success';
  }
  if (kind === 'logoff' || kind === 'rdp_logoff') return 'info';
  if (kind === 'rdp_disconnect' || kind === 'sudo') return 'warning';
  if (kind === 'failed' || kind === 'ssh_failed') return 'error';
  return 'primary';
}

function logonTypeLabel(code: string | null | undefined): string | null {
  if (!code) return null;
  const key = `siemCenter.hostDashboard.logonType.${code}`;
  const translated = t(key);
  return translated !== key ? `${code} — ${translated}` : code;
}

function sessionTypeCell(row: HostSessionHistoryItem): string {
  if (row.sourceAddress) return row.sourceAddress;
  if (row.logonType) return logonTypeLabel(row.logonType) || row.logonType;
  if (row.kind.startsWith('rdp_')) return 'RDP';
  if (row.kind === 'ssh_logon' || row.kind === 'ssh_failed') return 'SSH';
  if (row.kind === 'sudo') return 'sudo';
  return '—';
}

const detailMessageBlob = computed(() => {
  const full = detailFull.value;
  const fromFields = securityMessageFromEventFields(
    full?.fields,
    full?.raw,
    full?.rawPreview || selected.value?.preview,
    full?.eventAction,
  );
  return fromFields || selected.value?.preview || '';
});

const detailIsRdp = computed(() => {
  const id = selected.value?.eventId || '';
  return SESSION_HISTORY_RDP_EVENT_IDS.has(id)
    || (selected.value?.kind || '').startsWith('rdp_');
});

const detailParsed = computed(() => {
  if (detailIsRdp.value) {
    return { targetAccount: null, subjectAccount: null, logonType: null, displayUser: null };
  }
  return parseWindowsSecurityLogonMessage(detailMessageBlob.value);
});

const detailRdp = computed(() => parseWindowsRdpSessionMessage(detailMessageBlob.value));

const detailUser = computed(() => {
  if (detailIsRdp.value) {
    return detailRdp.value.user || selected.value?.user || null;
  }
  return (
    detailParsed.value.targetAccount
    || detailParsed.value.displayUser
    || selected.value?.user
    || null
  );
});

const detailSubject = computed(() => {
  return detailParsed.value.subjectAccount || selected.value?.subjectUser || null;
});

const detailLogonType = computed(() => {
  return detailParsed.value.logonType || selected.value?.logonType || null;
});

const detailSourceAddress = computed(() => {
  return detailRdp.value.sourceAddress || selected.value?.sourceAddress || null;
});

/** Mesaj tab = human text; parsing still uses detailMessageBlob (may include eventAction). */
const detailMessage = computed(() => {
  const full = detailFull.value;
  return eventLogDetailMessageText(
    full?.fields,
    full?.raw,
    full?.rawPreview || selected.value?.preview,
    selected.value?.preview,
  );
});

const detailBodyTab = ref<'message' | 'fields'>('message');
const detailCopyHint = ref<string | null>(null);

const detailFieldsJson = computed(() => {
  const full = detailFull.value;
  return eventLogDetailFieldsJson(full?.fields, full?.raw, full?.rawPreview);
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

async function openDetail(row: HostSessionHistoryItem) {
  selected.value = row;
  detailOpen.value = true;
  detailBodyTab.value = 'message';
  detailCopyHint.value = null;
  detailFull.value = null;
  detailError.value = null;
  detailLoading.value = true;
  try {
    detailFull.value = await secEventGet(row.id);
  } catch (e: unknown) {
    detailError.value = e instanceof Error ? e.message : String(e);
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
  <v-card variant="outlined" class="rounded-lg pa-4 h-100">
    <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-3">
      <div>
        <h3 class="text-subtitle-1 font-weight-bold mb-0">
          {{ t('siemCenter.hostDashboard.sessionsTitle') }}
        </h3>
        <p class="text-caption text-medium-emphasis mb-0">
          {{ sessionsHint }}
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

    <v-skeleton-loader v-if="loading" type="list-item@5" />
    <template v-else>
      <div class="text-body-2 mb-2">
        <span class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.lastHeartbeat') }}:</span>
        {{ formatTs(host.lastSeenAt) }}
      </div>
      <div class="text-body-2 mb-2">
        <span class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.uptime') }}:</span>
        {{ formatUptimeSeconds(agent?.uptimeSeconds) }}
      </div>
      <div class="text-body-2 mb-3">
        <span class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.agentVersion') }}:</span>
        <span class="font-mono">{{ agent?.agentVersion || '—' }}</span>
      </div>

      <template v-if="!isLinux">
        <div class="text-subtitle-2 mb-2">
          {{ t('siemCenter.hostDashboard.sessionsActiveTitle') }}
        </div>
        <div v-if="!sessions.length" class="text-body-2 text-medium-emphasis mb-4">
          {{ t('siemCenter.hostDashboard.sessionsEmpty') }}
        </div>
        <v-table v-else density="compact" class="mb-4 host-sessions-table">
          <thead>
            <tr>
              <th>{{ t('siemCenter.discovery.hostDetail.sessionUser') }}</th>
              <th>{{ t('siemCenter.discovery.hostDetail.sessionType') }}</th>
              <th>{{ t('siemCenter.discovery.hostDetail.sessionState') }}</th>
              <th>{{ t('siemCenter.discovery.hostDetail.sessionDuration') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="(s, i) in sessions" :key="`${s.sessionId ?? i}-${s.user}`">
              <td class="font-mono text-break">{{ s.user }}</td>
              <td>{{ s.clientProtocol || '—' }}</td>
              <td>{{ s.state || '—' }}</td>
              <td>{{ formatUptimeSeconds(s.durationSeconds) }}</td>
            </tr>
          </tbody>
        </v-table>
        <v-divider class="mb-3" />
      </template>

      <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-1">
        <div class="text-subtitle-2 mb-0">
          {{ t('siemCenter.hostDashboard.sessionsHistoryTitle') }}
        </div>
        <v-btn-toggle
          v-if="!isLinux"
          v-model="historyFilter"
          mandatory
          density="compact"
          color="primary"
          variant="outlined"
          divided
        >
          <v-btn value="users" size="x-small">
            {{ t('siemCenter.hostDashboard.sessionsFilterUsers') }}
          </v-btn>
          <v-btn value="all" size="x-small">
            {{ t('siemCenter.hostDashboard.sessionsFilterAll') }}
          </v-btn>
        </v-btn-toggle>
      </div>
      <p class="text-caption text-medium-emphasis mb-1">
        {{ sessionsHistoryHint }}
      </p>
      <p v-if="rangeLabel" class="text-caption mb-1">
        <v-chip size="x-small" variant="tonal" color="primary" class="me-1">
          {{ t('siemCenter.hostDashboard.sessionsHistoryRange', { range: rangeLabel }) }}
        </v-chip>
      </p>
      <p
        v-if="!isLinux && historyFilter === 'users' && historyStats.noise > 0"
        class="text-caption text-medium-emphasis mb-2"
      >
        {{
          t('siemCenter.hostDashboard.sessionsFilterHidden', {
            n: historyStats.noise,
            shown: historyStats.users,
          })
        }}
      </p>
      <v-data-table
        v-model:page="historyPage"
        v-model:items-per-page="historyItemsPerPage"
        :headers="historyHeaders"
        :items="history"
        item-value="id"
        density="compact"
        class="host-sessions-table host-history-table"
        :items-per-page-options="HISTORY_PAGE_SIZE_OPTIONS"
        :no-data-text="
          !isLinux && historyFilter === 'users' && historyStats.total > 0
            ? t('siemCenter.hostDashboard.sessionsHistoryEmptyFiltered')
            : sessionsHistoryEmpty
        "
      >
        <template #item.at="{ item }">
          <span class="text-no-wrap">{{ formatTs(item.at) }}</span>
        </template>
        <template #item.kind="{ item }">
          <v-chip size="x-small" :color="kindColor(item.kind)" variant="tonal">
            {{ kindLabel(item.kind) }}
          </v-chip>
        </template>
        <template #item.eventId="{ item }">
          <span class="font-mono">{{ item.eventId }}</span>
        </template>
        <template #item.logonType="{ item }">
          <span class="text-caption">
            {{ sessionTypeCell(item) }}
          </span>
        </template>
        <template #item.user="{ item }">
          <span
            class="font-mono text-truncate d-inline-block"
            style="max-width: 8rem"
            :title="item.user || undefined"
          >
            {{ item.user || '—' }}
          </span>
        </template>
        <template #item.actions="{ item }">
          <v-tooltip :text="t('siemCenter.hostDashboard.sessionDetail')" location="top">
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
            {{ t('siemCenter.hostDashboard.sessionDetailTitle') }}
          </span>
          <v-chip size="small" :color="kindColor(selected.kind)" variant="tonal">
            {{ kindLabel(selected.kind) }}
          </v-chip>
          <v-chip size="small" variant="tonal" class="font-mono">
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
              {{ t('siemCenter.hostDashboard.sessionDetailPartial') }}
            </div>
          </v-alert>

          <v-skeleton-loader v-if="detailLoading" type="article" class="mb-3" />

          <v-table density="compact" class="mb-4 session-detail-meta">
            <tbody>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colTime') }}</td>
                <td>{{ formatTs(selected.at) }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colKind') }}</td>
                <td>{{ kindLabel(selected.kind) }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colEventId') }}</td>
                <td class="font-mono">{{ selected.eventId }}</td>
              </tr>
              <tr>
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colUser') }}</td>
                <td class="font-mono text-break">
                  {{ detailUser || t('siemCenter.hostDashboard.sessionUserUnknown') }}
                </td>
              </tr>
              <tr v-if="detailSubject && detailSubject !== detailUser">
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colSubject') }}</td>
                <td class="font-mono text-break">{{ detailSubject }}</td>
              </tr>
              <tr v-if="detailLogonType">
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colLogonType') }}</td>
                <td>{{ logonTypeLabel(detailLogonType) }}</td>
              </tr>
              <tr v-if="detailSourceAddress">
                <td class="text-medium-emphasis">{{ t('siemCenter.hostDashboard.colSourceAddress') }}</td>
                <td class="font-mono">{{ detailSourceAddress }}</td>
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
              <v-sheet border rounded class="pa-3 session-detail-body">
                <pre v-if="detailMessage.trim()" class="ma-0 text-body-2">{{ detailMessage }}</pre>
                <div v-else class="text-body-2 text-medium-emphasis">
                  {{ t('siemCenter.discovery.hostDetail.eventLogDetailNoMessage') }}
                </div>
              </v-sheet>
            </v-tabs-window-item>
            <v-tabs-window-item value="fields">
              <v-sheet border rounded class="pa-3 session-detail-body">
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
.host-sessions-table :deep(td),
.host-sessions-table :deep(th) {
  font-size: 0.75rem;
  vertical-align: middle;
}
.host-history-table {
  width: 100%;
}
.host-history-table :deep(th),
.host-history-table :deep(td) {
  font-size: 0.75rem;
}
.session-detail-meta :deep(td:first-child) {
  width: 8rem;
  white-space: nowrap;
}
.session-detail-body pre {
  white-space: pre-wrap;
  word-break: break-word;
  max-height: 280px;
  overflow: auto;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
  font-size: 0.75rem;
}
</style>
