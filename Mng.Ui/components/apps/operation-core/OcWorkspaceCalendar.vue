<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { ocListDatasetPage, ocListStatesForWorkspace } from '@/services/operationCoreService';
import { ocListSprints, type OcSprint } from '@/services/operationCore/sprints';
import { useUserStore } from '@/stores/apps/user';
import { buildWorkItemProfilePath } from '@/utils/ocWorkItemProfileNav';
import { tmStatusBarColors } from '@/utils/taskManagerStatusColor';

const props = defineProps<{
  workspaceId: string;
  showSprints?: boolean;
}>();

const DAY_LIMIT = 3;
const ITEM_LIMIT = 500;
const WEEKDAYS = ['mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'] as const;

interface CalItem {
  id: string;
  key: string;
  title: string;
  stateId: string | null;
  assigneeId: string | null;
  due: string | null;
  sprintId: string | null;
}

interface DayCell {
  key: string;
  number: number;
  inMonth: boolean;
  today: boolean;
  items: CalItem[];
}

interface BandSeg {
  sprint: OcSprint;
  col: number;
  span: number;
}

interface WeekRow {
  id: string;
  lanes: BandSeg[][];
  days: DayCell[];
}

const { t, locale } = useAppI18n();
const users = useUserStore();

const loading = ref(false);
const errorLocal = ref<string | null>(null);
const items = ref<CalItem[]>([]);
const total = ref(0);
const sprints = ref<OcSprint[]>([]);
const stateName = ref<Record<string, string>>({});
const stateColor = ref<Record<string, string>>({});
const personName = ref<Record<string, string>>({});
const cursor = ref(startOfMonth(new Date()));
let loadSeq = 0;
const selection = ref<null | { kind: 'sprint'; id: string } | { kind: 'day'; day: string }>(null);

const monthTitle = computed(() =>
  new Intl.DateTimeFormat(locale().startsWith('en') ? 'en-US' : 'tr-TR', {
    month: 'long',
    year: 'numeric',
  }).format(cursor.value)
);

const monthKey = computed(() => ymd(cursor.value).slice(0, 7));

const datedThisMonth = computed(() => items.value.filter((item) => item.due?.startsWith(monthKey.value)).length);

const undated = computed(() => items.value.filter((item) => !item.due).sort(byKey));

const weeks = computed<WeekRow[]>(() => buildWeeks(cursor.value, items.value, sprints.value));

const selectedSprint = computed(() =>
  selection.value?.kind === 'sprint' ? sprints.value.find((row) => row.id === selection.value?.id) ?? null : null
);

const selectionItems = computed(() => {
  const current = selection.value;
  if (!current) return [];
  if (current.kind === 'sprint') {
    return items.value.filter((item) => item.sprintId === current.id).sort(byKey);
  }
  return items.value.filter((item) => item.due === current.day).sort(byKey);
});

const selectionTitle = computed(() => {
  const current = selection.value;
  if (!current) return '';
  if (current.kind === 'sprint') {
    return t('operationCore.calendar.sprintItems', { name: selectedSprint.value?.name || '' });
  }
  return t('operationCore.calendar.dayItems', { day: formatDay(current.day) });
});

watch(
  () => [props.workspaceId, props.showSprints] as const,
  ([id], previous) => {
    const workspaceChanged = !previous || previous[0] !== id;
    if (workspaceChanged) {
      cursor.value = startOfMonth(new Date());
      selection.value = null;
    }
    void load(id);
  },
  { immediate: true }
);

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

function ymd(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

function dateOnly(value: unknown): string | null {
  if (value == null || value === '') return null;
  const text = String(value).trim();
  const match = text.match(/^(\d{4}-\d{2}-\d{2})/);
  return match ? match[1] : null;
}

function relationId(value: unknown): string | null {
  if (value == null || value === '') return null;
  if (typeof value === 'string') return value.trim() || null;
  if (typeof value === 'object') {
    const row = value as Record<string, unknown>;
    const id = row.__dataId ?? row.id ?? row.userId;
    return id != null && String(id).trim() ? String(id).trim() : null;
  }
  const text = String(value).trim();
  return text || null;
}

function byKey(a: CalItem, b: CalItem): number {
  return a.key.localeCompare(b.key, 'tr');
}

function mapItem(raw: unknown): CalItem | null {
  if (!raw || typeof raw !== 'object') return null;
  const row = raw as Record<string, unknown>;
  const id = relationId(row.__dataId ?? row.dataId);
  if (!id) return null;
  const sprintId = relationId(row.sprintId);
  return {
    id,
    key: String(row.key ?? '').trim() || id,
    title: String(row.title ?? '').trim(),
    stateId: relationId(row.stateId),
    assigneeId: relationId(row.assignee),
    due: dateOnly(row.dueDate),
    sprintId: sprintId || null,
  };
}

function profileTo(id: string): string {
  return buildWorkItemProfilePath(id, {
    workspaceId: props.workspaceId,
    from: 'workspace',
    view: 'calendar',
  });
}

function dotBackground(stateId: string | null): string {
  return tmStatusBarColors(stateId ? stateColor.value[stateId] : null).background;
}

function stateLabel(item: CalItem): string {
  if (!item.stateId) return '';
  return stateName.value[item.stateId] || '';
}

function assigneeLabel(item: CalItem): string {
  if (!item.assigneeId) return '';
  return personName.value[item.assigneeId] || '';
}

function itemTitle(item: CalItem): string {
  return [item.key, item.title, stateLabel(item), assigneeLabel(item)].filter(Boolean).join(' · ');
}

function formatDay(day: string): string {
  const [year, month, date] = day.split('-').map(Number);
  if (!year || !month || !date) return day;
  return new Intl.DateTimeFormat(locale().startsWith('en') ? 'en-US' : 'tr-TR', {
    day: 'numeric',
    month: 'long',
  }).format(new Date(year, month - 1, date));
}

function shiftMonth(delta: number) {
  const current = cursor.value;
  cursor.value = new Date(current.getFullYear(), current.getMonth() + delta, 1);
  selection.value = null;
}

function goToday() {
  cursor.value = startOfMonth(new Date());
  selection.value = null;
}

function toggleSprint(id: string) {
  selection.value = selection.value?.kind === 'sprint' && selection.value.id === id ? null : { kind: 'sprint', id };
}

function openDay(day: string) {
  selection.value = selection.value?.kind === 'day' && selection.value.day === day ? null : { kind: 'day', day };
}

function laneOf(segments: BandSeg[]): BandSeg[][] {
  const sorted = [...segments].sort((a, b) => a.col - b.col || a.span - b.span);
  const lanes: BandSeg[][] = [];
  for (const segment of sorted) {
    const end = segment.col + segment.span - 1;
    const lane = lanes.find((row) => {
      const last = row[row.length - 1];
      return last.col + last.span - 1 < segment.col;
    });
    if (lane) lane.push(segment);
    else lanes.push([segment]);
  }
  return lanes;
}

function weekBands(dayKeys: string[], rows: OcSprint[]): BandSeg[][] {
  const segments: BandSeg[] = [];
  for (const sprint of rows) {
    const start = sprint.startDate.slice(0, 10);
    const end = sprint.endDate.slice(0, 10);
    if (!start || !end) continue;
    let first = -1;
    let last = -1;
    dayKeys.forEach((day, index) => {
      if (day >= start && day <= end) {
        if (first < 0) first = index;
        last = index;
      }
    });
    if (first < 0) continue;
    segments.push({ sprint, col: first + 1, span: last - first + 1 });
  }
  return laneOf(segments);
}

function buildWeeks(month: Date, source: CalItem[], sprintRows: OcSprint[]): WeekRow[] {
  const year = month.getFullYear();
  const monthIndex = month.getMonth();
  const offset = (month.getDay() + 6) % 7;
  const start = new Date(year, monthIndex, 1 - offset);
  const today = ymd(new Date());
  const byDue = new Map<string, CalItem[]>();
  for (const item of source) {
    if (!item.due) continue;
    const list = byDue.get(item.due) ?? [];
    list.push(item);
    byDue.set(item.due, list);
  }
  const result: WeekRow[] = [];
  for (let week = 0; week < 6; week += 1) {
    const days: DayCell[] = [];
    for (let index = 0; index < 7; index += 1) {
      const date = new Date(start);
      date.setDate(start.getDate() + week * 7 + index);
      const key = ymd(date);
      days.push({
        key,
        number: date.getDate(),
        inMonth: date.getMonth() === monthIndex,
        today: key === today,
        items: (byDue.get(key) ?? []).slice().sort(byKey),
      });
    }
    if (days.every((day) => !day.inMonth)) continue;
    result.push({
      id: days[0].key,
      lanes: weekBands(days.map((day) => day.key), sprintRows),
      days,
    });
  }
  return result;
}

async function load(workspaceId: string) {
  const id = workspaceId.trim();
  const seq = ++loadSeq;
  errorLocal.value = null;
  if (!id) {
    items.value = [];
    sprints.value = [];
    total.value = 0;
    return;
  }
  loading.value = true;
  try {
    const [page, sprintRows, states] = await Promise.all([
      ocListDatasetPage('op_work_items', {
        filter: `workspaceId:eq:${id}`,
        sort: 'key:asc',
        limit: ITEM_LIMIT,
        expand: false,
      }),
      props.showSprints ? ocListSprints(id).catch(() => [] as OcSprint[]) : Promise.resolve([] as OcSprint[]),
      ocListStatesForWorkspace(id).catch(() => []),
    ]);
    if (seq !== loadSeq) return;
    items.value = page.items.map(mapItem).filter((row): row is CalItem => !!row);
    total.value = page.total;
    sprints.value = sprintRows;
    const names: Record<string, string> = {};
    const colors: Record<string, string> = {};
    for (const state of states) {
      names[state.__dataId] = state.name;
      if (state.color) colors[state.__dataId] = state.color;
    }
    stateName.value = names;
    stateColor.value = colors;
    const ids = [...new Set(items.value.map((item) => item.assigneeId).filter((value): value is string => !!value))];
    personName.value = {};
    if (ids.length) {
      try {
        const people = await users.fetchUsersByIds(ids);
        if (seq !== loadSeq) return;
        const labels: Record<string, string> = {};
        for (const person of people) {
          const label = `${person.firstName || ''} ${person.lastName || ''}`.trim() || person.username;
          if (!label) continue;
          if (person.id) labels[person.id] = label;
          if (person.userId) labels[person.userId] = label;
        }
        personName.value = labels;
      } catch {
        if (seq === loadSeq) personName.value = {};
      }
    }
  } catch {
    if (seq === loadSeq) errorLocal.value = t('operationCore.calendar.loadError');
  } finally {
    if (seq === loadSeq) loading.value = false;
  }
}
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center gap-2 mb-3">
      <v-btn icon variant="text" size="small" :aria-label="t('operationCore.calendar.prev')" @click="shiftMonth(-1)">
        <v-icon icon="mdi-chevron-left" />
      </v-btn>
      <div class="text-subtitle-1 font-weight-bold text-capitalize">{{ monthTitle }}</div>
      <v-btn icon variant="text" size="small" :aria-label="t('operationCore.calendar.next')" @click="shiftMonth(1)">
        <v-icon icon="mdi-chevron-right" />
      </v-btn>
      <v-btn size="small" variant="tonal" class="text-none" @click="goToday">
        {{ t('operationCore.calendar.today') }}
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
    <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg">
      {{ errorLocal }}
    </v-alert>

    <div class="oc-cal">
      <div class="oc-cal-head">
        <div v-for="day in WEEKDAYS" :key="day" class="oc-cal-head__cell">
          {{ t(`operationCore.calendar.weekday.${day}`) }}
        </div>
      </div>
      <div v-for="week in weeks" :key="week.id" class="oc-cal-week">
        <div v-for="(lane, laneIndex) in week.lanes" :key="`${week.id}-lane-${laneIndex}`" class="oc-cal-lane">
          <button
            v-for="segment in lane"
            :key="segment.sprint.id"
            type="button"
            class="oc-cal-band text-truncate"
            :class="`oc-cal-band--${segment.sprint.state}`"
            :style="{ gridColumn: `${segment.col} / span ${segment.span}` }"
            :title="`${segment.sprint.name} · ${t(`operationCore.sprints.state.${segment.sprint.state}`)}`"
            @click="toggleSprint(segment.sprint.id)"
          >
            {{ segment.sprint.name }}
          </button>
        </div>
        <div class="oc-cal-days">
          <div
            v-for="day in week.days"
            :key="day.key"
            class="oc-cal-day"
            :class="{ 'oc-cal-day--out': !day.inMonth, 'oc-cal-day--today': day.today }"
          >
            <div class="oc-cal-day__num" :class="{ 'oc-cal-day__num--today': day.today }">{{ day.number }}</div>
            <NuxtLink
              v-for="item in day.items.slice(0, DAY_LIMIT)"
              :key="item.id"
              :to="profileTo(item.id)"
              class="oc-cal-item"
              :title="itemTitle(item)"
            >
              <span v-if="item.stateId" class="oc-cal-dot" :style="{ background: dotBackground(item.stateId) }" />
              <span class="oc-cal-item__text">
                <span class="d-block text-truncate">{{ item.key }} {{ item.title }}</span>
                <span v-if="stateLabel(item) || assigneeLabel(item)" class="d-block text-truncate text-medium-emphasis">
                  {{ stateLabel(item) }}<template v-if="stateLabel(item) && assigneeLabel(item)"> · </template>{{ assigneeLabel(item) }}
                </span>
              </span>
            </NuxtLink>
            <button
              v-if="day.items.length > DAY_LIMIT"
              type="button"
              class="oc-cal-more"
              @click="openDay(day.key)"
            >
              {{ t('operationCore.calendar.more', { count: day.items.length - DAY_LIMIT }) }}
            </button>
          </div>
        </div>
      </div>
    </div>

    <p v-if="!loading && datedThisMonth === 0" class="text-caption text-medium-emphasis mt-3 mb-0">
      {{ t('operationCore.calendar.noDueThisMonth') }}
    </p>
    <p v-if="total > items.length" class="text-caption text-medium-emphasis mt-2 mb-0">
      {{ t('operationCore.calendar.truncated', { count: items.length }) }}
    </p>

    <div v-if="selection" class="mt-4">
      <div class="d-flex align-center mb-2">
        <div class="text-subtitle-2 font-weight-bold">{{ selectionTitle }}</div>
        <v-spacer />
        <v-btn size="small" variant="text" class="text-none" @click="selection = null">
          {{ t('operationCore.calendar.closeList') }}
        </v-btn>
      </div>
      <v-list v-if="selectionItems.length" density="compact" class="py-0 rounded-lg border">
        <v-list-item v-for="item in selectionItems" :key="item.id" :to="profileTo(item.id)" rounded="lg">
          <v-list-item-title class="text-body-2">
            {{ item.key }} · {{ item.title }}
          </v-list-item-title>
          <v-list-item-subtitle>
            <span v-if="stateLabel(item)">{{ stateLabel(item) }}</span>
            <span v-if="stateLabel(item) && assigneeLabel(item)"> · </span>
            <span v-if="assigneeLabel(item)">{{ assigneeLabel(item) }}</span>
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </div>

    <div class="mt-5">
      <div class="text-subtitle-2 font-weight-bold mb-2">
        {{ t('operationCore.calendar.undated') }}
        <span class="text-medium-emphasis font-weight-regular">({{ undated.length }})</span>
      </div>
      <p v-if="!undated.length" class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.calendar.undatedEmpty') }}
      </p>
      <v-list v-else density="compact" class="py-0" max-height="320" style="overflow: auto">
        <v-list-item v-for="item in undated" :key="item.id" :to="profileTo(item.id)" rounded="lg">
          <template #prepend>
            <span
              v-if="item.stateId"
              class="oc-cal-dot mr-2"
              :style="{ background: dotBackground(item.stateId) }"
            />
          </template>
          <v-list-item-title class="text-body-2">{{ item.key }} · {{ item.title }}</v-list-item-title>
          <v-list-item-subtitle>
            <span v-if="stateLabel(item)">{{ stateLabel(item) }}</span>
            <span v-if="stateLabel(item) && assigneeLabel(item)"> · </span>
            <span v-if="assigneeLabel(item)">{{ assigneeLabel(item) }}</span>
          </v-list-item-subtitle>
        </v-list-item>
      </v-list>
    </div>
  </div>
</template>

<style scoped>
.oc-cal {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  overflow: hidden;
}
.oc-cal-head,
.oc-cal-lane,
.oc-cal-days {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
}
.oc-cal-head__cell {
  padding: 8px;
  font-size: 0.75rem;
  font-weight: 600;
  text-align: center;
  color: rgba(var(--v-theme-on-surface), 0.6);
}
.oc-cal-week + .oc-cal-week {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.oc-cal-lane {
  padding: 2px 4px 0;
  gap: 4px;
}
.oc-cal-band {
  border: 0;
  border-radius: 6px;
  padding: 2px 8px;
  font-size: 0.75rem;
  text-align: left;
  cursor: pointer;
}
.oc-cal-band--active {
  background: rgba(var(--v-theme-primary), 0.18);
  color: rgb(var(--v-theme-primary));
}
.oc-cal-band--planned {
  background: rgba(var(--v-theme-info), 0.16);
  color: rgb(var(--v-theme-info));
}
.oc-cal-band--closed {
  background: rgba(var(--v-theme-on-surface), 0.08);
  color: rgba(var(--v-theme-on-surface), 0.55);
}
.oc-cal-day {
  min-height: 92px;
  padding: 4px;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.oc-cal-day + .oc-cal-day,
.oc-cal-head__cell + .oc-cal-head__cell {
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.oc-cal-day--out {
  opacity: 0.45;
}
.oc-cal-day__num {
  font-size: 0.75rem;
  width: 1.5rem;
  height: 1.5rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 2px;
}
.oc-cal-day__num--today {
  border-radius: 999px;
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
}
.oc-cal-item,
.oc-cal-more {
  display: flex;
  align-items: flex-start;
  gap: 4px;
  width: 100%;
  border: 0;
  background: transparent;
  text-align: left;
  font-size: 0.7rem;
  line-height: 1.3;
  padding: 1px 2px;
  border-radius: 4px;
  color: inherit;
  text-decoration: none;
  cursor: pointer;
}
.oc-cal-item:hover,
.oc-cal-more:hover {
  background: rgba(var(--v-theme-primary), 0.08);
}
.oc-cal-item__text {
  min-width: 0;
  flex: 1 1 auto;
}
.oc-cal-dot {
  width: 8px;
  height: 8px;
  margin-top: 3px;
  border-radius: 999px;
  flex: 0 0 auto;
}
</style>
