<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  istanbulYmd,
  listOrgMeetingActionRows,
  meetingTopicColorHex,
  type OrgMeeting,
  type OrgMeetingAction,
  type OrgMeetingTopic,
} from '@/services/meetingsService';

const props = defineProps<{
  meetings: OrgMeeting[];
  topics: OrgMeetingTopic[];
}>();

const emit = defineEmits<{ open: [id: string] }>();

const DAY = 86400000;
const UPCOMING_DAYS = 14;
const MINUTES_DAYS = 30;
const NO_TOPIC_COLOR = '#546e7a';
const COLOR = { info: '#539BFF', error: '#FA896B', muted: '#a1aab2' };

type Focus = 'upcoming' | 'missing' | 'actions' | 'overdue';
type Rows = { upcoming: OrgMeeting[]; missing: OrgMeeting[]; actions: OrgMeetingAction[] };
type Group = Rows & { id: string; name: string; color: string };

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const actions = ref<OrgMeetingAction[]>([]);
const loading = ref(false);
const now = ref(Date.now());
const focus = ref<Focus | null>(null);
const focusDay = ref<string | null>(null);
const listsEl = ref<HTMLElement | null>(null);

onMounted(async () => {
  loading.value = true;
  now.value = Date.now();
  try {
    actions.value = await listOrgMeetingActionRows();
  } catch (error) {
    panelError(error, 'meetings.summary.loadFailed');
  } finally {
    loading.value = false;
  }
});

function endMillis(meeting: OrgMeeting): number {
  const end = Date.parse(meeting.endAt || '');
  if (!Number.isNaN(end)) return end;
  return Date.parse(meeting.startAt);
}

function formatWhen(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) return value;
  return new Date(parsed).toLocaleString();
}

function addDaysYmd(value: string, days: number): string {
  const [year, month, day] = value.split('-').map(Number);
  const next = new Date(Date.UTC(year, month - 1, day) + days * 86400000);
  const monthText = String(next.getUTCMonth() + 1).padStart(2, '0');
  const dayText = String(next.getUTCDate()).padStart(2, '0');
  return `${next.getUTCFullYear()}-${monthText}-${dayText}`;
}

function dayLabel(value: string): string {
  const [, month, day] = value.split('-');
  return `${Number(day)}.${Number(month)}`;
}

function overdue(action: OrgMeetingAction): boolean {
  if (!action.dueAt) return false;
  const due = istanbulYmd(action.dueAt);
  const today = istanbulYmd(new Date(now.value));
  return Boolean(due && today && due < today);
}

function meetingTitle(action: OrgMeetingAction): string {
  return props.meetings.find((meeting) => meeting.id === action.meetingId)?.title || action.title;
}

const clustered = computed<Group[]>(() => {
  const instant = now.value;
  const byId = new Map(props.meetings.map((meeting) => [meeting.id, meeting]));
  const upcomingUntil = instant + UPCOMING_DAYS * DAY;
  const minutesSince = instant - MINUTES_DAYS * DAY;
  const buckets = new Map<string, Rows>();
  const bucket = (topicId: string | null) => {
    const key = topicId || '';
    const found = buckets.get(key);
    if (found) return found;
    const created: Rows = { upcoming: [], missing: [], actions: [] };
    buckets.set(key, created);
    return created;
  };
  for (const meeting of props.meetings) {
    if (meeting.status === 'cancelled') continue;
    const start = Date.parse(meeting.startAt);
    const end = endMillis(meeting);
    if (!Number.isNaN(start) && start >= instant && start < upcomingUntil) bucket(meeting.topicId).upcoming.push(meeting);
    if (!Number.isNaN(end) && end < instant && end >= minutesSince && !meeting.minutesResourceId) bucket(meeting.topicId).missing.push(meeting);
  }
  for (const action of actions.value) {
    if (action.status !== 'open') continue;
    const meeting = byId.get(action.meetingId);
    if (!meeting || meeting.status === 'cancelled') continue;
    bucket(meeting.topicId).actions.push(action);
  }
  const named = props.topics
    .slice()
    .sort((left, right) => left.name.localeCompare(right.name, 'tr'))
    .flatMap((topic) => {
      const rows = buckets.get(topic.id);
      if (!rows || !(rows.upcoming.length || rows.missing.length || rows.actions.length)) return [];
      return [{ id: topic.id, name: topic.name, color: meetingTopicColorHex(topic.color), ...rows }];
    });
  const loose = buckets.get('');
  if (loose && (loose.upcoming.length || loose.missing.length || loose.actions.length)) {
    named.push({ id: '', name: t('meetings.summary.noTopic'), color: NO_TOPIC_COLOR, ...loose });
  }
  return named;
});

const groups = computed(() => {
  const day = focusDay.value;
  const byMeeting = new Map(props.meetings.map((meeting) => [meeting.id, meeting]));
  return clustered.value.map((group) => ({
    ...group,
    upcoming: group.upcoming.slice().sort((left, right) => {
      const leftHit = day && istanbulYmd(left.startAt) === day ? 0 : 1;
      const rightHit = day && istanbulYmd(right.startAt) === day ? 0 : 1;
      if (leftHit !== rightHit) return leftHit - rightHit;
      return Date.parse(left.startAt) - Date.parse(right.startAt);
    }),
    missing: group.missing.slice().sort((left, right) => endMillis(right) - endMillis(left)),
    actions: group.actions.slice().sort((left, right) => {
      const leftLate = overdue(left);
      const rightLate = overdue(right);
      if (leftLate !== rightLate) return leftLate ? -1 : 1;
      const leftDue = left.dueAt ? Date.parse(left.dueAt) : Number.POSITIVE_INFINITY;
      const rightDue = right.dueAt ? Date.parse(right.dueAt) : Number.POSITIVE_INFINITY;
      if (leftDue !== rightDue) return leftDue - rightDue;
      const leftTitle = byMeeting.get(left.meetingId)?.title || left.title;
      const rightTitle = byMeeting.get(right.meetingId)?.title || right.title;
      return leftTitle.localeCompare(rightTitle, 'tr');
    }),
  }));
});

const totals = computed(() => {
  const rows = clustered.value;
  const open = rows.flatMap((group) => group.actions);
  return {
    upcoming: rows.reduce((count, group) => count + group.upcoming.length, 0),
    missing: rows.reduce((count, group) => count + group.missing.length, 0),
    open: open.length,
    overdue: open.filter(overdue).length,
  };
});

const dayKeys = computed(() => {
  const today = istanbulYmd(new Date(now.value));
  const end = istanbulYmd(new Date(now.value + UPCOMING_DAYS * DAY - 1));
  const keys: string[] = [];
  if (!today || !end) return keys;
  let cursor = today;
  while (cursor <= end && keys.length < UPCOMING_DAYS + 1) {
    keys.push(cursor);
    cursor = addDaysYmd(cursor, 1);
  }
  return keys;
});

const daySeries = computed(() =>
  clustered.value
    .map((group) => ({
      name: group.name,
      color: group.color || NO_TOPIC_COLOR,
      data: dayKeys.value.map((day) => group.upcoming.filter((meeting) => istanbulYmd(meeting.startAt) === day).length),
    }))
    .filter((series) => series.data.some((count) => count > 0)),
);

const actionTopics = computed(() => clustered.value.filter((group) => group.actions.length));

const chartBase = {
  fontFamily: 'inherit',
  foreColor: COLOR.muted,
  toolbar: { show: false },
};

const dayOptions = computed(() => ({
  chart: {
    ...chartBase,
    type: 'bar',
    stacked: true,
    events: {
      dataPointSelection: (_event: unknown, _chart: unknown, config: { dataPointIndex: number }) => {
        const day = dayKeys.value[config.dataPointIndex];
        if (!day) return;
        focus.value = 'upcoming';
        focusDay.value = day;
        scrollLists();
      },
    },
  },
  colors: daySeries.value.map((series) => series.color),
  plotOptions: { bar: { columnWidth: '62%', borderRadius: 3 } },
  dataLabels: { enabled: false },
  legend: { position: 'bottom', fontSize: '12px' },
  grid: { borderColor: 'rgba(var(--v-border-color), 0.35)', strokeDashArray: 4 },
  xaxis: { categories: dayKeys.value.map(dayLabel) },
  yaxis: { labels: { formatter: (value: number) => String(Math.round(value)) }, forceNiceScale: true },
  tooltip: { theme: 'dark', y: { formatter: (value: number) => String(value) } },
}));

const actionChart = computed(() => {
  const rows = actionTopics.value;
  const onTime = rows.map((group) => group.actions.filter((action) => !overdue(action)).length);
  const late = rows.map((group) => group.actions.filter(overdue).length);
  const series: { name: string; data: number[] }[] = [];
  const colors: string[] = [];
  if (onTime.some((count) => count > 0)) {
    series.push({ name: t('meetings.summary.onTime'), data: onTime });
    colors.push(COLOR.info);
  }
  if (late.some((count) => count > 0)) {
    series.push({ name: t('meetings.summary.overdue'), data: late });
    colors.push(COLOR.error);
  }
  return { series, colors, categories: rows.map((group) => group.name) };
});

const actionOptions = computed(() => ({
  chart: {
    ...chartBase,
    type: 'bar',
    stacked: true,
    events: {
      dataPointSelection: (_event: unknown, _chart: unknown, config: { dataPointIndex: number }) => {
        const group = actionTopics.value[config.dataPointIndex];
        if (!group) return;
        focus.value = 'actions';
        focusDay.value = null;
        scrollTopic(group.id);
      },
    },
  },
  colors: actionChart.value.colors,
  plotOptions: { bar: { horizontal: true, barHeight: '58%', borderRadius: 4 } },
  dataLabels: { enabled: true },
  legend: { position: 'bottom', fontSize: '12px' },
  grid: { borderColor: 'rgba(var(--v-border-color), 0.35)', strokeDashArray: 4 },
  xaxis: {
    categories: actionChart.value.categories,
    labels: { formatter: (value: string) => String(Math.round(Number(value))) },
  },
  tooltip: { theme: 'dark', y: { formatter: (value: number) => String(value) } },
}));

const actionChartHeight = computed(() => Math.max(220, actionTopics.value.length * 56));

function columnHot(kind: 'upcoming' | 'missing' | 'actions'): boolean {
  if (kind === 'upcoming') return focus.value === 'upcoming';
  if (kind === 'missing') return focus.value === 'missing';
  return focus.value === 'actions' || focus.value === 'overdue';
}

function dayHot(meeting: OrgMeeting): boolean {
  return Boolean(focusDay.value && istanbulYmd(meeting.startAt) === focusDay.value);
}

function scrollLists() {
  nextTick(() => listsEl.value?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
}

function scrollTopic(id: string) {
  nextTick(() => document.getElementById(`mt-summary-${id || 'none'}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
}

function onKpi(kind: Focus) {
  focus.value = kind;
  focusDay.value = null;
  scrollLists();
}
</script>

<template>
  <div>
    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-4" />

    <v-row dense class="mb-1">
      <v-col cols="12" sm="6" lg="3">
        <v-card class="mt-summary-kpi mt-summary-kpi--primary rounded-lg h-100" variant="flat" role="button" @click="onKpi('upcoming')">
          <v-card-text>
            <div class="text-overline text-medium-emphasis">{{ t('meetings.summary.upcoming') }}</div>
            <div class="text-h4 font-weight-bold mt-1">{{ totals.upcoming }}</div>
            <div class="text-caption text-medium-emphasis mt-1">{{ t('meetings.summary.upcomingHint') }}</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" sm="6" lg="3">
        <v-card
          class="mt-summary-kpi rounded-lg h-100"
          :class="totals.missing ? 'mt-summary-kpi--warning' : 'mt-summary-kpi--success'"
          variant="flat"
          role="button"
          @click="onKpi('missing')"
        >
          <v-card-text>
            <div class="text-overline text-medium-emphasis">{{ t('meetings.summary.missing') }}</div>
            <div class="text-h4 font-weight-bold mt-1">{{ totals.missing }}</div>
            <div class="text-caption text-medium-emphasis mt-1">{{ t('meetings.summary.missingHint') }}</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" sm="6" lg="3">
        <v-card class="mt-summary-kpi mt-summary-kpi--info rounded-lg h-100" variant="flat" role="button" @click="onKpi('actions')">
          <v-card-text>
            <div class="text-overline text-medium-emphasis">{{ t('meetings.summary.actions') }}</div>
            <div class="text-h4 font-weight-bold mt-1">{{ totals.open }}</div>
            <div class="text-caption text-medium-emphasis mt-1">&nbsp;</div>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" sm="6" lg="3">
        <v-card
          class="mt-summary-kpi rounded-lg h-100"
          :class="totals.overdue ? 'mt-summary-kpi--error' : 'mt-summary-kpi--success'"
          variant="flat"
          role="button"
          @click="onKpi('overdue')"
        >
          <v-card-text>
            <div class="text-overline text-medium-emphasis">{{ t('meetings.summary.overdue') }}</div>
            <div class="text-h4 font-weight-bold mt-1">{{ totals.overdue }}</div>
            <div class="text-caption text-medium-emphasis mt-1">{{ t('meetings.summary.overdueHint') }}</div>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <v-row dense class="mb-4">
      <v-col cols="12" md="7">
        <v-card class="rounded-lg h-100" variant="outlined">
          <v-card-text>
            <div class="text-subtitle-2 font-weight-medium mb-2">{{ t('meetings.summary.upcomingHint') }}</div>
            <div v-if="!daySeries.length" class="mt-summary-empty">{{ t('meetings.summary.chartEmpty') }}</div>
            <ClientOnly v-else>
              <apexchart type="bar" height="280" :options="dayOptions" :series="daySeries" />
            </ClientOnly>
          </v-card-text>
        </v-card>
      </v-col>
      <v-col cols="12" md="5">
        <v-card class="rounded-lg h-100" variant="outlined">
          <v-card-text>
            <div class="text-subtitle-2 font-weight-medium mb-2">{{ t('meetings.summary.byTopic') }}</div>
            <div v-if="!actionChart.series.length" class="mt-summary-empty">{{ t('meetings.summary.chartEmpty') }}</div>
            <ClientOnly v-else>
              <apexchart type="bar" :height="actionChartHeight" :options="actionOptions" :series="actionChart.series" />
            </ClientOnly>
          </v-card-text>
        </v-card>
      </v-col>
    </v-row>

    <div ref="listsEl">
      <p v-if="!groups.length" class="text-medium-emphasis">{{ t('meetings.summary.empty') }}</p>
      <div v-else class="d-flex flex-column ga-4">
        <section v-for="group in groups" :id="`mt-summary-${group.id || 'none'}`" :key="group.id || 'none'" class="mt-summary-topic rounded-lg pa-4">
          <div class="d-flex align-center ga-2 mb-3">
            <span class="mt-summary-dot" :style="{ background: group.color || NO_TOPIC_COLOR }" />
            <div class="text-h6">{{ group.name }}</div>
          </div>
          <v-row>
            <v-col cols="12" md="4" :class="{ 'mt-summary-focus': columnHot('upcoming') }">
              <div class="text-subtitle-2">{{ t('meetings.summary.upcoming') }}</div>
              <div class="text-caption text-medium-emphasis mb-2">{{ focusDay ? dayLabel(focusDay) : t('meetings.summary.upcomingHint') }}</div>
              <v-list v-if="group.upcoming.length" density="compact" class="pa-0">
                <v-list-item
                  v-for="meeting in group.upcoming"
                  :key="meeting.id"
                  :class="{ 'mt-summary-hit': dayHot(meeting) }"
                  @click="emit('open', meeting.id)"
                >
                  <v-list-item-title>{{ meeting.title }}</v-list-item-title>
                  <v-list-item-subtitle>{{ formatWhen(meeting.startAt) }}</v-list-item-subtitle>
                </v-list-item>
              </v-list>
              <p v-else class="text-caption text-medium-emphasis">{{ t('meetings.summary.none') }}</p>
            </v-col>
            <v-col cols="12" md="4" :class="{ 'mt-summary-focus': columnHot('missing') }">
              <div class="text-subtitle-2">{{ t('meetings.summary.missing') }}</div>
              <div class="text-caption text-medium-emphasis mb-2">{{ t('meetings.summary.missingHint') }}</div>
              <v-list v-if="group.missing.length" density="compact" class="pa-0">
                <v-list-item v-for="meeting in group.missing" :key="meeting.id" @click="emit('open', meeting.id)">
                  <v-list-item-title>{{ meeting.title }}</v-list-item-title>
                  <v-list-item-subtitle>{{ formatWhen(meeting.endAt || meeting.startAt) }}</v-list-item-subtitle>
                </v-list-item>
              </v-list>
              <p v-else class="text-caption text-medium-emphasis">{{ t('meetings.summary.none') }}</p>
            </v-col>
            <v-col cols="12" md="4" :class="{ 'mt-summary-focus': columnHot('actions') }">
              <div class="text-subtitle-2">{{ t('meetings.summary.actions') }}</div>
              <div class="text-caption text-medium-emphasis mb-2">&nbsp;</div>
              <v-list v-if="group.actions.length" density="compact" class="pa-0">
                <v-list-item
                  v-for="action in group.actions"
                  :key="action.id"
                  :class="{ 'mt-summary-hit': focus === 'overdue' && overdue(action) }"
                  @click="emit('open', action.meetingId)"
                >
                  <v-list-item-title>
                    {{ action.title }}
                    <v-chip v-if="overdue(action)" size="x-small" color="error" variant="tonal" class="ms-1">{{ t('meetings.summary.overdue') }}</v-chip>
                  </v-list-item-title>
                  <v-list-item-subtitle>
                    {{ meetingTitle(action) }}
                    <span v-if="action.dueAt"> · {{ formatWhen(action.dueAt) }}</span>
                  </v-list-item-subtitle>
                </v-list-item>
              </v-list>
              <p v-else class="text-caption text-medium-emphasis">{{ t('meetings.summary.none') }}</p>
            </v-col>
          </v-row>
        </section>
      </div>
    </div>
  </div>
</template>

<style scoped>
.mt-summary-kpi {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  min-height: 118px;
  cursor: pointer;
}
.mt-summary-kpi--primary { border-top: 3px solid rgb(var(--v-theme-primary)); }
.mt-summary-kpi--success { border-top: 3px solid rgb(var(--v-theme-success)); }
.mt-summary-kpi--warning { border-top: 3px solid rgb(var(--v-theme-warning)); }
.mt-summary-kpi--error { border-top: 3px solid rgb(var(--v-theme-error)); }
.mt-summary-kpi--info { border-top: 3px solid rgb(var(--v-theme-info)); }
.mt-summary-empty {
  min-height: 220px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.875rem;
}
.mt-summary-topic {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  scroll-margin-top: 80px;
}
.mt-summary-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  display: inline-block;
}
.mt-summary-focus {
  background: rgba(var(--v-theme-primary), 0.06);
  border-radius: 8px;
}
.mt-summary-hit {
  background: rgba(var(--v-theme-primary), 0.14);
  border-radius: 8px;
}
</style>
