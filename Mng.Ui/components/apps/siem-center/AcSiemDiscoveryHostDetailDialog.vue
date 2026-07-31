<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { hostEventsLink, hostLocalUiLink } from '@/composables/useSiemDiscoveryData';
import { coverageColor } from '@/composables/useSiemDiscoveryMock';
import AcSiemDiscoveryHostMetricsPanel from '@/components/apps/siem-center/AcSiemDiscoveryHostMetricsPanel.vue';
import AcSiemDiscoveryHostAppsPanel from '@/components/apps/siem-center/AcSiemDiscoveryHostAppsPanel.vue';
import AcSiemDiscoveryHostEventLogsPanel from '@/components/apps/siem-center/AcSiemDiscoveryHostEventLogsPanel.vue';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';
import {
  LayoutSidebarLeftCollapseIcon,
  LayoutSidebarLeftExpandIcon,
} from 'vue-tabler-icons';

const open = defineModel<boolean>('open', { default: false });

const props = defineProps<{
  host: SiemDiscoveryHost | null;
  staleMs: number;
}>();

const { t, locale } = useAppI18n();
const activeTab = ref('status');
const sideCollapsed = ref(false);

watch(open, (v) => {
  if (v) {
    activeTab.value = 'status';
    sideCollapsed.value = false;
  }
});

function toggleSideCollapse() {
  sideCollapsed.value = !sideCollapsed.value;
}

const title = computed(() => props.host?.hostname || t('siemCenter.discovery.hostDetail.title'));

const coverageLabel = computed(() => {
  if (!props.host) return '';
  return t(`siemCenter.discovery.coverage.${props.host.coverage}`);
});

const coverageChipColor = computed(() =>
  props.host ? coverageColor(props.host.coverage) : 'grey',
);

const agent = computed(() => props.host?.agent ?? null);

const displayIp = computed(() => {
  const h = props.host;
  if (!h) return '—';
  return agent.value?.primaryIp || (h.ip && h.ip !== '—' ? h.ip : '—');
});

const displayUser = computed(() => {
  const a = agent.value;
  if (!a) return null;
  if (a.consoleUser) return a.consoleUser;
  if (a.loggedOnUsers?.length) return a.loggedOnUsers.join(', ');
  return null;
});

function formatTs(value: string | number | null | undefined): string {
  if (value == null || value === '') return '—';
  const ms = typeof value === 'number' ? value : Date.parse(value);
  if (!Number.isFinite(ms)) return String(value);
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
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

const lastSeenAgeLabel = computed(() => {
  const at = props.host?.lastSeenAt;
  if (at == null) return null;
  const ageSec = Math.max(0, Math.round((Date.now() - at) / 1000));
  if (ageSec < 60) return t('siemCenter.discovery.hostDetail.ageSeconds', { n: ageSec });
  const ageMin = Math.round(ageSec / 60);
  return t('siemCenter.discovery.hostDetail.ageMinutes', { n: ageMin });
});

const staleMinutes = computed(() => Math.round(props.staleMs / 60000));

const sourcesLabel = computed(() => {
  const s = props.host?.sources;
  if (!s?.length) return '—';
  return s.join(', ');
});

const eventsHref = computed(() =>
  props.host ? hostEventsLink(props.host) : '/apps/siem-center/events',
);

const localUiHref = computed(() =>
  props.host ? hostLocalUiLink(props.host) : null,
);

const hasAgentSnapshot = computed(() => {
  const a = agent.value;
  if (!a) return false;
  return !!(
    a.primaryIp
    || a.consoleUser
    || a.loggedOnUsers?.length
    || a.bootTimeUtc
    || a.uptimeSeconds != null
    || a.sessions?.length
    || a.agentVersion
  );
});

function close() {
  open.value = false;
}
</script>

<template>
  <v-dialog v-model="open" max-width="920" scrollable>
    <v-card v-if="host" class="host-detail-card">
      <v-card-title class="d-flex align-start justify-space-between ga-3 flex-wrap py-4 px-4">
        <div class="min-w-0">
          <div class="text-h6 font-weight-bold text-truncate">{{ title }}</div>
          <div class="text-caption text-medium-emphasis">
            {{ t('siemCenter.discovery.hostDetail.subtitle') }}
          </div>
        </div>
        <v-chip size="small" :color="coverageChipColor" variant="flat">
          {{ coverageLabel }}
        </v-chip>
      </v-card-title>

      <v-divider />

      <div class="host-detail-body" :class="{ 'host-detail-body--side-collapsed': sideCollapsed }">
        <aside v-if="!sideCollapsed" class="host-detail-side pa-4">
          <div class="d-flex align-center justify-space-between ga-2 mb-3">
            <div class="text-subtitle-2">
              {{ t('siemCenter.discovery.hostDetail.sectionInfo') }}
            </div>
            <v-btn
              icon
              size="x-small"
              variant="text"
              :title="t('siemCenter.discovery.hostDetail.collapseSide')"
              @click="toggleSideCollapse"
            >
              <LayoutSidebarLeftCollapseIcon size="18" />
            </v-btn>
          </div>
          <dl class="host-side-dl">
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.hostname') }}</dt>
              <dd class="font-weight-medium text-break">{{ host.hostname }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.ip') }}</dt>
              <dd class="font-mono">{{ displayIp }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.activeUser') }}</dt>
              <dd class="font-mono text-break">{{ displayUser || '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.uptime') }}</dt>
              <dd>{{ formatUptimeSeconds(agent?.uptimeSeconds) }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.os') }}</dt>
              <dd>{{ host.osHint || '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.sam') }}</dt>
              <dd class="text-break">{{ host.samAccountName || '—' }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.sources') }}</dt>
              <dd>{{ sourcesLabel }}</dd>
            </div>
            <div>
              <dt>{{ t('siemCenter.discovery.hostDetail.lastSeenAd') }}</dt>
              <dd>{{ formatTs(host.lastSeenFromAd) }}</dd>
            </div>
            <div v-if="agent?.agentVersion">
              <dt>{{ t('siemCenter.discovery.hostDetail.agentVersion') }}</dt>
              <dd class="font-mono">{{ agent.agentVersion }}</dd>
            </div>
            <div v-if="agent?.localUiPort">
              <dt>{{ t('siemCenter.discovery.hostDetail.localUi') }}</dt>
              <dd class="font-mono">{{ localUiHref || (`:${agent.localUiPort}`) }}</dd>
            </div>
          </dl>
        </aside>

        <div class="host-detail-main">
          <div class="d-flex align-center">
            <v-btn
              v-if="sideCollapsed"
              icon
              size="x-small"
              variant="text"
              class="ms-2 flex-shrink-0"
              :title="t('siemCenter.discovery.hostDetail.expandSide')"
              @click="toggleSideCollapse"
            >
              <LayoutSidebarLeftExpandIcon size="18" />
            </v-btn>
            <v-tabs v-model="activeTab" density="compact" color="primary" class="px-2 flex-grow-1">
              <v-tab value="status">{{ t('siemCenter.discovery.hostDetail.tabStatus') }}</v-tab>
              <v-tab value="metrics">{{ t('siemCenter.discovery.hostDetail.tabMetrics') }}</v-tab>
              <v-tab value="apps">{{ t('siemCenter.discovery.hostDetail.tabApps') }}</v-tab>
              <v-tab value="eventlog">{{ t('siemCenter.discovery.hostDetail.tabEventLog') }}</v-tab>
            </v-tabs>
          </div>
          <v-divider />
          <v-tabs-window v-model="activeTab">
            <v-tabs-window-item value="status">
              <div class="pa-4">
                <v-alert
                  :type="host.coverage === 'managedOnline' ? 'success' : host.coverage === 'managedOffline' ? 'warning' : 'info'"
                  variant="tonal"
                  density="comfortable"
                  class="mb-4"
                >
                  <div class="font-weight-medium mb-1">{{ coverageLabel }}</div>
                  <div class="text-body-2">
                    {{
                      t('siemCenter.discovery.hostDetail.statusHint', {
                        m: staleMinutes,
                      })
                    }}
                  </div>
                  <div v-if="host.lastSeenAt" class="text-body-2 mt-2">
                    <span class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.lastHeartbeat') }}:</span>
                    {{ formatTs(host.lastSeenAt) }}
                    <span v-if="lastSeenAgeLabel" class="text-medium-emphasis"> ({{ lastSeenAgeLabel }})</span>
                  </div>
                  <div v-else class="text-body-2 mt-2 text-medium-emphasis">
                    {{ t('siemCenter.discovery.hostDetail.noHeartbeat') }}
                  </div>
                </v-alert>

                <template v-if="hasAgentSnapshot">
                  <div class="text-subtitle-2 mb-2">
                    {{ t('siemCenter.discovery.hostDetail.sectionAgent') }}
                  </div>
                  <v-table density="compact" class="mb-4 host-detail-table">
                    <tbody>
                      <tr>
                        <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.ip') }}</td>
                        <td class="font-mono">{{ displayIp }}</td>
                      </tr>
                      <tr>
                        <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.activeUser') }}</td>
                        <td class="font-mono">{{ displayUser || '—' }}</td>
                      </tr>
                      <tr>
                        <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.bootTime') }}</td>
                        <td>{{ formatTs(agent?.bootTimeUtc) }}</td>
                      </tr>
                      <tr>
                        <td class="text-medium-emphasis">{{ t('siemCenter.discovery.hostDetail.uptime') }}</td>
                        <td>{{ formatUptimeSeconds(agent?.uptimeSeconds) }}</td>
                      </tr>
                    </tbody>
                  </v-table>

                  <div v-if="agent?.sessions?.length" class="mb-2">
                    <div class="text-subtitle-2 mb-2">
                      {{ t('siemCenter.discovery.hostDetail.sectionSessions') }}
                    </div>
                    <v-table density="compact" class="host-detail-table">
                      <thead>
                        <tr>
                          <th class="text-left">{{ t('siemCenter.discovery.hostDetail.sessionUser') }}</th>
                          <th class="text-left">{{ t('siemCenter.discovery.hostDetail.sessionType') }}</th>
                          <th class="text-left">{{ t('siemCenter.discovery.hostDetail.sessionState') }}</th>
                          <th class="text-left">{{ t('siemCenter.discovery.hostDetail.sessionDuration') }}</th>
                        </tr>
                      </thead>
                      <tbody>
                        <tr v-for="(s, i) in agent.sessions" :key="`${s.sessionId ?? i}-${s.user}`">
                          <td class="font-mono text-break">{{ s.user }}</td>
                          <td>{{ s.clientProtocol || '—' }}</td>
                          <td>{{ s.state || '—' }}</td>
                          <td>{{ formatUptimeSeconds(s.durationSeconds) }}</td>
                        </tr>
                      </tbody>
                    </v-table>
                  </div>
                </template>
                <v-sheet
                  v-else-if="host.lastSeenAt"
                  border
                  rounded
                  class="pa-3 text-medium-emphasis text-body-2"
                >
                  {{ t('siemCenter.discovery.hostDetail.agentFieldsPending') }}
                </v-sheet>
              </div>
            </v-tabs-window-item>

            <v-tabs-window-item value="metrics">
              <AcSiemDiscoveryHostMetricsPanel
                v-if="activeTab === 'metrics' && host"
                :hostname="host.hostname"
              />
            </v-tabs-window-item>

            <v-tabs-window-item value="apps">
              <AcSiemDiscoveryHostAppsPanel
                v-if="activeTab === 'apps' && host"
                :hostname="host.hostname"
              />
            </v-tabs-window-item>

            <v-tabs-window-item value="eventlog">
              <AcSiemDiscoveryHostEventLogsPanel
                v-if="activeTab === 'eventlog' && host"
                :hostname="host.hostname"
              />
            </v-tabs-window-item>
          </v-tabs-window>
        </div>
      </div>

      <v-divider />

      <v-card-actions class="pa-4 flex-wrap ga-2">
        <v-btn variant="text" :to="eventsHref" prepend-icon="mdi-timeline-text-outline">
          {{ t('siemCenter.discovery.hostDetail.openEvents') }}
        </v-btn>
        <v-tooltip :disabled="!!localUiHref" location="top">
          <template #activator="{ props: tip }">
            <span v-bind="tip" class="d-inline-flex">
              <v-btn
                variant="text"
                prepend-icon="mdi-open-in-new"
                :href="localUiHref || undefined"
                :disabled="!localUiHref"
                target="_blank"
                rel="noopener noreferrer"
              >
                {{ t('siemCenter.discovery.hostDetail.openLocalUi') }}
              </v-btn>
            </span>
          </template>
          <span>{{ t('siemCenter.discovery.hostDetail.openLocalUiDisabled') }}</span>
        </v-tooltip>
        <v-spacer />
        <v-btn color="primary" variant="flat" @click="close">
          {{ t('siemCenter.discovery.hostDetail.close') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.host-detail-body {
  display: grid;
  grid-template-columns: minmax(240px, 300px) 1fr;
  min-height: 360px;
}
.host-detail-body--side-collapsed {
  grid-template-columns: 1fr;
}
.host-detail-side {
  border-right: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-surface-variant), 0.12);
  overflow: auto;
}
.host-side-dl {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin: 0;
}
.host-side-dl dt {
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.6);
  margin-bottom: 2px;
}
.host-side-dl dd {
  margin: 0;
  font-size: 0.875rem;
}
.host-detail-main {
  min-width: 0;
}
.host-detail-table :deep(td),
.host-detail-table :deep(th) {
  border-bottom: thin solid rgba(var(--v-border-color), var(--v-border-opacity)) !important;
  padding-block: 8px !important;
  vertical-align: top;
}
.host-detail-table :deep(td:first-child) {
  width: 34%;
  white-space: nowrap;
}
@media (max-width: 800px) {
  .host-detail-body {
    grid-template-columns: 1fr;
  }
  .host-detail-side {
    border-right: none;
    border-bottom: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
  }
}
</style>
