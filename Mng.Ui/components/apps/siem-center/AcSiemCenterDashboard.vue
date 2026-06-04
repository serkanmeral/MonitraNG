<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmSummary } from '@/types/apps/alarm';
import { alarmListOpen } from '@/services/alarmService';
import { secEventQuery } from '@/services/secEventService';
import {
  loadSiemDashboardLayout,
  saveSiemDashboardLayout,
  resetSiemDashboardLayout,
  type SiemDashboardLayout,
  type SiemDashboardWidgetId,
  type SiemStatCardId,
} from '@/composables/useSiemDashboardLayout';
import {
  SIEM_SCENARIO_CATALOG,
  scenarioEventsLink,
  type SiemScenarioDef,
} from '@/composables/useSiemScenarioCatalog';

const { t, locale } = useAppI18n();

const loading = ref(true);
const errorLocal = ref<string | null>(null);
const customizeOpen = ref(false);
const layoutDraft = ref<SiemDashboardLayout>(loadSiemDashboardLayout());
const layout = ref<SiemDashboardLayout>(loadSiemDashboardLayout());

const stats = ref({
  eventsTotal: 0,
  loginFailed: 0,
  deniedFlow: 0,
  newFlow: 0,
  openAlarms: 0,
});

const recentAlarms = ref<AlarmSummary[]>([]);

interface HourlyBucket {
  label: string;
  count: number;
  pct: number;
}

interface ScenarioCard {
  def: SiemScenarioDef;
  lastSeenAt: string | null;
  severity: number | null;
  open: boolean;
}

const hourlyBuckets = ref<HourlyBucket[]>([]);
const scenarioCards = ref<ScenarioCard[]>([]);

const timeRangeLabel = computed(() => t('siemCenter.dashboard.range24h'));

const visibleWidgets = computed(() =>
  layout.value.widgetOrder.filter((id) => !layout.value.hiddenWidgets.includes(id)),
);

const mainWidgets = computed(() =>
  visibleWidgets.value.filter((id) => id !== 'recentAlarms' && id !== 'quickLinks'),
);

const showRecentAlarms = computed(
  () =>
    visibleWidgets.value.includes('recentAlarms') &&
    !layout.value.hiddenWidgets.includes('recentAlarms'),
);

const showQuickLinks = computed(
  () =>
    visibleWidgets.value.includes('quickLinks') &&
    !layout.value.hiddenWidgets.includes('quickLinks'),
);

const showBottomRow = computed(() => showRecentAlarms.value || showQuickLinks.value);

const bottomRowOrder = computed(() =>
  layout.value.widgetOrder.filter(
    (id) => (id === 'recentAlarms' || id === 'quickLinks') && visibleWidgets.value.includes(id),
  ),
);

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

const statCardDefs = computed(() => ({
  eventsTotal: {
    key: 'eventsTotal' as const,
    label: t('siemCenter.dashboard.statEvents'),
    value: stats.value.eventsTotal,
    color: 'primary',
    icon: 'mdi-shield-search',
    to: '/apps/siem-center/events',
  },
  openAlarms: {
    key: 'openAlarms' as const,
    label: t('siemCenter.dashboard.statOpenAlarms'),
    value: stats.value.openAlarms,
    color: 'error',
    icon: 'mdi-bell-alert',
    to: '/apps/alarm-center/alarms',
  },
  loginFailed: {
    key: 'loginFailed' as const,
    label: t('siemCenter.dashboard.statLoginFailed'),
    value: stats.value.loginFailed,
    color: 'warning',
    icon: 'mdi-account-lock',
    to: '/apps/siem-center/events?eventAction=login_failed',
  },
  deniedFlow: {
    key: 'deniedFlow' as const,
    label: t('siemCenter.dashboard.statDeniedFlow'),
    value: stats.value.deniedFlow,
    color: 'deep-orange',
    icon: 'mdi-firewall',
    to: '/apps/siem-center/events?eventAction=denied_flow',
  },
  newFlow: {
    key: 'newFlow' as const,
    label: t('siemCenter.dashboard.statNewFlow'),
    value: stats.value.newFlow,
    color: 'info',
    icon: 'mdi-transit-connection-variant',
    to: '/apps/siem-center/events?eventAction=new_flow',
  },
}));

const statCards = computed(() =>
  layout.value.statCardOrder
    .filter((id) => !layout.value.hiddenStatCards.includes(id))
    .map((id) => statCardDefs.value[id]),
);

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

function buildHourlyBuckets(totals: number[]): HourlyBucket[] {
  const max = Math.max(...totals, 1);
  const now = Date.now();
  const hourMs = 60 * 60 * 1000;
  return totals.map((count, idx) => {
    const hourStart = new Date(now - (23 - idx) * hourMs);
    const label = new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
    }).format(hourStart);
    return { label, count, pct: Math.round((count / max) * 100) };
  });
}

function contextKey(alarm: AlarmSummary): string | null {
  const key = alarm.context?.key;
  return typeof key === 'string' ? key : null;
}

function isOpenAlarm(status: AlarmSummary['status']): boolean {
  return status === 'Active' || status === 'Acknowledged' || status === 0 || status === 1;
}

function buildScenarioCards(alarms: AlarmSummary[]): ScenarioCard[] {
  return SIEM_SCENARIO_CATALOG.map((def) => {
    const matches = alarms.filter((a) => contextKey(a) === def.matchKey);
    const latest = matches.sort(
      (a, b) => new Date(b.lastSeenAt).getTime() - new Date(a.lastSeenAt).getTime(),
    )[0];
    return {
      def,
      lastSeenAt: latest?.lastSeenAt ?? null,
      severity: latest?.severity ?? null,
      open: latest ? isOpenAlarm(latest.status) : false,
    };
  });
}

function widgetLabel(id: SiemDashboardWidgetId): string {
  return t(`siemCenter.dashboard.widgets.${id}`);
}

function statCardLabel(id: SiemStatCardId): string {
  return t(`siemCenter.dashboard.statCards.${id}`);
}

function openCustomize() {
  layoutDraft.value = JSON.parse(JSON.stringify(layout.value)) as SiemDashboardLayout;
  customizeOpen.value = true;
}

function isWidgetVisible(id: SiemDashboardWidgetId): boolean {
  return !layoutDraft.value.hiddenWidgets.includes(id);
}

function isStatVisible(id: SiemStatCardId): boolean {
  return !layoutDraft.value.hiddenStatCards.includes(id);
}

function toggleWidget(id: SiemDashboardWidgetId, visible: boolean | null) {
  const hidden = layoutDraft.value.hiddenWidgets.filter((x) => x !== id);
  if (!visible) hidden.push(id);
  layoutDraft.value.hiddenWidgets = hidden;
}

function toggleStat(id: SiemStatCardId, visible: boolean | null) {
  const hidden = layoutDraft.value.hiddenStatCards.filter((x) => x !== id);
  if (!visible) hidden.push(id);
  layoutDraft.value.hiddenStatCards = hidden;
}

function moveInList<T extends string>(list: T[], id: T, delta: number): T[] {
  const idx = list.indexOf(id);
  if (idx < 0) return list;
  const next = idx + delta;
  if (next < 0 || next >= list.length) return list;
  const copy = [...list];
  [copy[idx], copy[next]] = [copy[next], copy[idx]];
  return copy;
}

function moveWidget(id: SiemDashboardWidgetId, delta: number) {
  layoutDraft.value.widgetOrder = moveInList(layoutDraft.value.widgetOrder, id, delta);
}

function moveStat(id: SiemStatCardId, delta: number) {
  layoutDraft.value.statCardOrder = moveInList(layoutDraft.value.statCardOrder, id, delta);
}

function saveLayout() {
  layout.value = JSON.parse(JSON.stringify(layoutDraft.value)) as SiemDashboardLayout;
  saveSiemDashboardLayout(layout.value);
  customizeOpen.value = false;
}

function restoreDefaultLayout() {
  layoutDraft.value = resetSiemDashboardLayout();
  layout.value = JSON.parse(JSON.stringify(layoutDraft.value)) as SiemDashboardLayout;
  customizeOpen.value = false;
}

async function loadDashboard() {
  loading.value = true;
  errorLocal.value = null;
  const range = isoRange24h();
  const now = Date.now();
  const hourMs = 60 * 60 * 1000;
  const hourQueries = Array.from({ length: 24 }, (_, idx) => {
    const bucketEnd = new Date(now - (23 - idx) * hourMs);
    const bucketStart = new Date(bucketEnd.getTime() - hourMs);
    return secEventQuery({
      from: bucketStart.toISOString(),
      to: bucketEnd.toISOString(),
      limit: 1,
    });
  });

  try {
    const [allEvents, loginFailed, deniedFlow, newFlow, alarms, recentAll, ...hourResults] =
      await Promise.all([
        secEventQuery({ ...range, limit: 1 }),
        secEventQuery({ ...range, eventAction: 'login_failed', limit: 1 }),
        secEventQuery({ ...range, eventAction: 'denied_flow', limit: 1 }),
        secEventQuery({ ...range, eventAction: 'new_flow', limit: 1 }),
        alarmListOpen({ openOnly: true, minSeverity: 6, limit: 8 }),
        alarmListOpen({ openOnly: false, limit: 150 }),
        ...hourQueries,
      ]);

    stats.value = {
      eventsTotal: allEvents.total,
      loginFailed: loginFailed.total,
      deniedFlow: deniedFlow.total,
      newFlow: newFlow.total,
      openAlarms: alarms.total,
    };
    recentAlarms.value = alarms.items.slice(0, 8);
    hourlyBuckets.value = buildHourlyBuckets(hourResults.map((r) => r.total));
    scenarioCards.value = buildScenarioCards(recentAll.items);
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('siemCenter.dashboard.loadError');
    stats.value = { eventsTotal: 0, loginFailed: 0, deniedFlow: 0, newFlow: 0, openAlarms: 0 };
    recentAlarms.value = [];
    hourlyBuckets.value = [];
    scenarioCards.value = buildScenarioCards([]);
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
      <v-btn variant="tonal" prepend-icon="mdi-view-dashboard-edit" @click="openCustomize">
        {{ t('siemCenter.dashboard.customize') }}
      </v-btn>
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadDashboard">
        {{ t('siemCenter.dashboard.refresh') }}
      </v-btn>
    </div>

    <template v-for="widgetId in mainWidgets" :key="widgetId">
      <v-row v-if="widgetId === 'stats'" dense class="mb-4">
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

      <v-card v-else-if="widgetId === 'eventTimeline'" variant="outlined" class="rounded-lg pa-4 mb-4">
        <h2 class="text-h6 font-weight-bold mb-3">
          {{ t('siemCenter.dashboard.timelineTitle') }}
        </h2>
        <v-skeleton-loader v-if="loading" type="list-item@6" />
        <div v-else-if="hourlyBuckets.every((b) => b.count === 0)" class="text-medium-emphasis text-body-2 py-2">
          {{ t('siemCenter.dashboard.timelineEmpty') }}
        </div>
        <div v-else class="d-flex flex-column gap-1">
          <div v-for="row in hourlyBuckets" :key="row.label" class="d-flex align-center gap-2">
            <span class="text-caption text-medium-emphasis timeline-hour">{{ row.label }}</span>
            <v-progress-linear
              :model-value="row.pct"
              color="primary"
              height="10"
              rounded
              class="flex-grow-1"
            />
            <span class="text-caption font-weight-medium timeline-count">{{ row.count }}</span>
          </div>
        </div>
      </v-card>

      <v-card v-else-if="widgetId === 'scenarios'" variant="outlined" class="rounded-lg pa-4 mb-4">
        <h2 class="text-h6 font-weight-bold mb-3">
          {{ t('siemCenter.dashboard.scenariosTitle') }}
        </h2>
        <v-skeleton-loader v-if="loading" type="table-row@4" />
        <v-row v-else dense>
          <v-col v-for="card in scenarioCards" :key="card.def.id" cols="12" sm="6" md="4" lg="3">
            <v-card variant="tonal" :to="scenarioEventsLink(card.def)" link class="pa-3 h-100">
              <div class="d-flex align-center justify-space-between mb-1">
                <span class="text-subtitle-2 font-weight-bold">{{ card.def.id }}</span>
                <v-chip v-if="card.open" size="x-small" color="error" variant="flat">
                  {{ t('siemCenter.dashboard.scenarioOpen') }}
                </v-chip>
              </div>
              <div class="text-caption text-medium-emphasis text-truncate">{{ card.def.matchKey }}</div>
              <div class="text-body-2 mt-2">
                {{
                  card.lastSeenAt
                    ? formatDate(card.lastSeenAt)
                    : t('siemCenter.dashboard.scenarioNever')
                }}
              </div>
              <div v-if="card.severity != null" class="mt-1">
                <v-chip size="x-small" :color="severityColor(card.severity)" variant="tonal">
                  {{ t('siemCenter.dashboard.scenarioSeverity', { n: card.severity }) }}
                </v-chip>
              </div>
            </v-card>
          </v-col>
        </v-row>
      </v-card>

      <v-card v-else-if="widgetId === 'breakdown'" variant="outlined" class="rounded-lg pa-4 mb-4">
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
    </template>

    <v-row v-if="showBottomRow">
      <template v-for="widgetId in bottomRowOrder" :key="widgetId">
        <v-col v-if="widgetId === 'recentAlarms'" cols="12" :lg="showQuickLinks ? 8 : 12">
          <v-card variant="outlined" class="rounded-lg pa-4">
            <div class="d-flex align-center mb-3">
              <h2 class="text-h6 font-weight-bold">
                {{ t('siemCenter.dashboard.recentAlarmsTitle') }}
              </h2>
              <v-spacer />
              <v-btn variant="text" size="small" to="/apps/alarm-center/alarms">
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

        <v-col v-else-if="widgetId === 'quickLinks'" cols="12" :lg="showRecentAlarms ? 4 : 12">
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
      </template>
    </v-row>

    <v-dialog v-model="customizeOpen" max-width="560">
      <v-card>
        <v-card-title>{{ t('siemCenter.dashboard.customizeTitle') }}</v-card-title>
        <v-card-text>
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('siemCenter.dashboard.customizeHint') }}
          </p>

          <div class="text-subtitle-2 mb-2">{{ t('siemCenter.dashboard.customizeSections') }}</div>
          <v-list density="compact" class="mb-4">
            <v-list-item
              v-for="id in layoutDraft.widgetOrder"
              :key="id"
              :title="widgetLabel(id)"
            >
              <template #prepend>
                <v-checkbox
                  :model-value="isWidgetVisible(id)"
                  hide-details
                  density="compact"
                  @update:model-value="toggleWidget(id, $event)"
                />
              </template>
              <template #append>
                <v-btn icon="mdi-chevron-up" variant="text" size="x-small" @click="moveWidget(id, -1)" />
                <v-btn icon="mdi-chevron-down" variant="text" size="x-small" @click="moveWidget(id, 1)" />
              </template>
            </v-list-item>
          </v-list>

          <div class="text-subtitle-2 mb-2">{{ t('siemCenter.dashboard.customizeStatCards') }}</div>
          <v-list density="compact">
            <v-list-item
              v-for="id in layoutDraft.statCardOrder"
              :key="id"
              :title="statCardLabel(id)"
            >
              <template #prepend>
                <v-checkbox
                  :model-value="isStatVisible(id)"
                  hide-details
                  density="compact"
                  @update:model-value="toggleStat(id, $event)"
                />
              </template>
              <template #append>
                <v-btn icon="mdi-chevron-up" variant="text" size="x-small" @click="moveStat(id, -1)" />
                <v-btn icon="mdi-chevron-down" variant="text" size="x-small" @click="moveStat(id, 1)" />
              </template>
            </v-list-item>
          </v-list>
        </v-card-text>
        <v-card-actions>
          <v-btn variant="text" @click="restoreDefaultLayout">
            {{ t('siemCenter.dashboard.customizeReset') }}
          </v-btn>
          <v-spacer />
          <v-btn variant="text" @click="customizeOpen = false">
            {{ t('siemCenter.dashboard.customizeCancel') }}
          </v-btn>
          <v-btn color="primary" variant="flat" @click="saveLayout">
            {{ t('siemCenter.dashboard.customizeSave') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.timeline-hour {
  width: 3.5rem;
  flex-shrink: 0;
}
.timeline-count {
  width: 2.5rem;
  text-align: right;
  flex-shrink: 0;
}
</style>
