<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmSummary } from '@/types/apps/alarm';
import { alarmListOpen } from '@/services/alarmService';
import { secEventQuery } from '@/services/secEventService';

const { t, locale } = useAppI18n();

const loading = ref(true);
const errorLocal = ref<string | null>(null);

const stats = ref({
  eventsTotal: 0,
  loginFailed: 0,
  deniedFlow: 0,
  newFlow: 0,
  openAlarms: 0,
});

const recentAlarms = ref<AlarmSummary[]>([]);

const timeRangeLabel = computed(() => t('siemCenter.dashboard.range24h'));

function isoRange24h(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
  return { from: from.toISOString(), to: to.toISOString() };
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

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

const statCards = computed(() => [
  {
    key: 'eventsTotal',
    label: t('siemCenter.dashboard.statEvents'),
    value: stats.value.eventsTotal,
    color: 'primary',
    icon: 'mdi-shield-search',
    to: '/apps/siem-center/events',
  },
  {
    key: 'openAlarms',
    label: t('siemCenter.dashboard.statOpenAlarms'),
    value: stats.value.openAlarms,
    color: 'error',
    icon: 'mdi-bell-alert',
    to: '/apps/alarm-center/alarms',
  },
  {
    key: 'loginFailed',
    label: t('siemCenter.dashboard.statLoginFailed'),
    value: stats.value.loginFailed,
    color: 'warning',
    icon: 'mdi-account-lock',
    to: '/apps/siem-center/events?eventAction=login_failed',
  },
  {
    key: 'deniedFlow',
    label: t('siemCenter.dashboard.statDeniedFlow'),
    value: stats.value.deniedFlow,
    color: 'deep-orange',
    icon: 'mdi-firewall',
    to: '/apps/siem-center/events?eventAction=denied_flow',
  },
  {
    key: 'newFlow',
    label: t('siemCenter.dashboard.statNewFlow'),
    value: stats.value.newFlow,
    color: 'info',
    icon: 'mdi-transit-connection-variant',
    to: '/apps/siem-center/events?eventAction=new_flow',
  },
]);

const actionBreakdown = computed(() => {
  const s = stats.value;
  const items = [
    { key: 'login_failed', label: 'login_failed', count: s.loginFailed, color: 'warning' },
    { key: 'denied_flow', label: 'denied_flow', count: s.deniedFlow, color: 'deep-orange' },
    { key: 'new_flow', label: 'new_flow', count: s.newFlow, color: 'info' },
  ];
  const max = Math.max(...items.map((i) => i.count), 1);
  return items.map((i) => ({ ...i, pct: Math.round((i.count / max) * 100) }));
});

async function loadDashboard() {
  loading.value = true;
  errorLocal.value = null;
  const range = isoRange24h();

  try {
    const [allEvents, loginFailed, deniedFlow, newFlow, alarms] = await Promise.all([
      secEventQuery({ ...range, limit: 1 }),
      secEventQuery({ ...range, eventAction: 'login_failed', limit: 1 }),
      secEventQuery({ ...range, eventAction: 'denied_flow', limit: 1 }),
      secEventQuery({ ...range, eventAction: 'new_flow', limit: 1 }),
      alarmListOpen({ openOnly: true, minSeverity: 6, limit: 8 }),
    ]);

    stats.value = {
      eventsTotal: allEvents.total,
      loginFailed: loginFailed.total,
      deniedFlow: deniedFlow.total,
      newFlow: newFlow.total,
      openAlarms: alarms.total,
    };
    recentAlarms.value = alarms.items.slice(0, 8);
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('siemCenter.dashboard.loadError');
    stats.value = { eventsTotal: 0, loginFailed: 0, deniedFlow: 0, newFlow: 0, openAlarms: 0 };
    recentAlarms.value = [];
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadDashboard();
});
</script>

<template>
  <div>
    <v-alert
      v-if="errorLocal"
      type="error"
      variant="tonal"
      class="mb-4"
      closable
      @click:close="errorLocal = null"
    >
      {{ errorLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-chip variant="tonal" color="primary">
        {{ timeRangeLabel }}
      </v-chip>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadDashboard">
        {{ t('siemCenter.dashboard.refresh') }}
      </v-btn>
    </div>

    <v-row dense class="mb-4">
      <v-col v-for="card in statCards" :key="card.key" cols="12" sm="6" md="4" lg="2">
        <v-skeleton-loader v-if="loading" type="card" />
        <v-card
          v-else
          variant="outlined"
          class="pa-3 stat-card h-100"
          :to="card.to"
          link
        >
          <div class="d-flex align-center gap-3">
            <v-avatar :color="card.color" variant="tonal" size="48" rounded>
              <v-icon :icon="card.icon" />
            </v-avatar>
            <div>
              <div class="text-caption text-medium-emphasis">{{ card.label }}</div>
              <div class="text-h5 font-weight-bold">{{ card.value.toLocaleString() }}</div>
            </div>
          </div>
        </v-card>
      </v-col>
    </v-row>

    <v-card variant="outlined" class="rounded-lg pa-4 mb-4">
      <h2 class="text-h6 font-weight-bold mb-3">
        {{ t('siemCenter.dashboard.breakdownTitle') }}
      </h2>
      <v-skeleton-loader v-if="loading" type="list-item@3" />
      <div v-else-if="stats.eventsTotal === 0" class="text-medium-emphasis text-body-2 py-2">
        {{ t('siemCenter.dashboard.breakdownEmpty') }}
      </div>
      <div v-else class="d-flex flex-column gap-3">
        <div v-for="row in actionBreakdown" :key="row.key">
          <div class="d-flex justify-space-between text-body-2 mb-1">
            <router-link
              :to="`/apps/siem-center/events?eventAction=${row.key}`"
              class="text-decoration-none"
            >
              {{ row.label }}
            </router-link>
            <span class="font-weight-medium">{{ row.count.toLocaleString() }}</span>
          </div>
          <v-progress-linear
            :model-value="row.pct"
            :color="row.color"
            height="8"
            rounded
          />
        </div>
      </div>
    </v-card>

    <v-row>
      <v-col cols="12" lg="8">
        <v-card variant="outlined" class="rounded-lg pa-4">
          <div class="d-flex align-center mb-3">
            <h2 class="text-h6 font-weight-bold">
              {{ t('siemCenter.dashboard.recentAlarmsTitle') }}
            </h2>
            <v-spacer />
            <v-btn
              variant="text"
              size="small"
              to="/apps/alarm-center/alarms"
            >
              {{ t('siemCenter.dashboard.viewAllAlarms') }}
            </v-btn>
          </div>

          <v-skeleton-loader v-if="loading" type="table-row@5" />
          <v-table v-else density="comfortable">
            <thead>
              <tr>
                <th>{{ t('alarmCenter.alarms.colSeverity') }}</th>
                <th>{{ t('alarmCenter.alarms.colDedupKey') }}</th>
                <th>{{ t('alarmCenter.alarms.colLastSeen') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="alarm in recentAlarms" :key="alarm.id">
                <td>
                  <v-chip size="small" :color="severityColor(alarm.severity)" variant="flat">
                    {{ alarm.severity }}
                  </v-chip>
                </td>
                <td class="text-body-2">{{ alarm.dedupKey }}</td>
                <td>{{ formatDate(alarm.lastSeenAt) }}</td>
              </tr>
              <tr v-if="recentAlarms.length === 0">
                <td colspan="3" class="text-center text-medium-emphasis py-6">
                  {{ t('siemCenter.dashboard.noAlarms') }}
                </td>
              </tr>
            </tbody>
          </v-table>
        </v-card>
      </v-col>

      <v-col cols="12" lg="4">
        <v-card variant="outlined" class="rounded-lg pa-4 h-100">
          <h2 class="text-h6 font-weight-bold mb-3">
            {{ t('siemCenter.dashboard.quickLinksTitle') }}
          </h2>
          <v-list density="comfortable" nav>
            <v-list-item
              prepend-icon="mdi-format-list-bulleted"
              :title="t('siemCenter.events.menuTitle')"
              to="/apps/siem-center/events"
            />
            <v-list-item
              prepend-icon="mdi-bell-alert"
              :title="t('alarmCenter.alarms.menuTitle')"
              to="/apps/alarm-center/alarms"
            />
            <v-list-item
              prepend-icon="mdi-tune"
              :title="t('alarmCenter.rules.menuTitle')"
              to="/apps/alarm-center/rules"
            />
          </v-list>
        </v-card>
      </v-col>
    </v-row>
  </div>
</template>
