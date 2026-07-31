<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SiemDiscoveryHost } from '@/types/apps/siemDiscovery';

const props = defineProps<{
  host: SiemDiscoveryHost;
  staleMs: number;
}>();

const { t, locale } = useAppI18n();

const agent = computed(() => props.host.agent ?? null);

const coverageLabel = computed(() =>
  t(`siemCenter.discovery.coverage.${props.host.coverage}`),
);

const displayIp = computed(() => {
  const h = props.host;
  return agent.value?.primaryIp || (h.ip && h.ip !== '—' ? h.ip : '—');
});

const displayUser = computed(() => {
  const a = agent.value;
  if (!a) return null;
  if (a.consoleUser) return a.consoleUser;
  if (a.loggedOnUsers?.length) return a.loggedOnUsers.join(', ');
  return null;
});

const staleMinutes = computed(() => Math.round(props.staleMs / 60000));

const lastSeenAgeLabel = computed(() => {
  const at = props.host.lastSeenAt;
  if (at == null) return null;
  const ageSec = Math.max(0, Math.round((Date.now() - at) / 1000));
  if (ageSec < 60) return t('siemCenter.discovery.hostDetail.ageSeconds', { n: ageSec });
  const ageMin = Math.round(ageSec / 60);
  return t('siemCenter.discovery.hostDetail.ageMinutes', { n: ageMin });
});

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
</script>

<template>
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
      <v-table density="compact" class="mb-4 host-status-table">
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
        <v-table density="compact" class="host-status-table">
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
</template>

<style scoped>
.host-status-table :deep(td),
.host-status-table :deep(th) {
  font-size: 0.8125rem;
}
</style>
