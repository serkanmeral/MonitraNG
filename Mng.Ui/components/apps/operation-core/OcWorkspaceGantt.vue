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

const ITEM_LIMIT = 500;

interface GanttItem {
  id: string;
  key: string;
  title: string;
  stateId: string | null;
  assigneeId: string | null;
  start: string | null;
  end: string | null;
}

const { t, locale } = useAppI18n();
const users = useUserStore();

const loading = ref(false);
const errorLocal = ref<string | null>(null);
const items = ref<GanttItem[]>([]);
const total = ref(0);
const sprints = ref<OcSprint[]>([]);
const stateName = ref<Record<string, string>>({});
const stateColor = ref<Record<string, string>>({});
const personName = ref<Record<string, string>>({});
const cursor = ref(startOfMonth(new Date()));
let loadSeq = 0;

const monthTitle = computed(() =>
  new Intl.DateTimeFormat(locale().startsWith('en') ? 'en-US' : 'tr-TR', {
    month: 'long',
    year: 'numeric',
  }).format(cursor.value)
);

const monthDays = computed(() => daysOfMonth(cursor.value));
const monthStart = computed(() => monthDays.value[0]?.key ?? '');
const monthEnd = computed(() => monthDays.value[monthDays.value.length - 1]?.key ?? '');
const todayKey = computed(() => ymd(new Date()));

const bars = computed(() =>
  items.value
    .filter((item) => item.start && item.end && item.end >= monthStart.value && item.start <= monthEnd.value)
    .slice()
    .sort((a, b) => (a.start! < b.start! ? -1 : a.start! > b.start! ? 1 : a.key.localeCompare(b.key, 'tr')))
);

const undated = computed(() => items.value.filter((item) => !item.start || !item.end).sort((a, b) => a.key.localeCompare(b.key, 'tr')));

const monthSprints = computed(() =>
  sprints.value.filter((sprint) => {
    const start = sprint.startDate.slice(0, 10);
    const end = sprint.endDate.slice(0, 10);
    return start && end && end >= monthStart.value && start <= monthEnd.value;
  })
);

watch(
  () => [props.workspaceId, props.showSprints] as const,
  ([id], previous) => {
    if (!previous || previous[0] !== id) cursor.value = startOfMonth(new Date());
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
  const match = String(value).trim().match(/^(\d{4}-\d{2}-\d{2})/);
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
  return String(value).trim() || null;
}

function spanOf(row: Record<string, unknown>): { start: string | null; end: string | null } {
  const extra = row.extraFields && typeof row.extraFields === 'object' ? (row.extraFields as Record<string, unknown>) : {};
  let start = dateOnly(extra.startDate);
  let end = dateOnly(row.dueDate);
  if (start && end && start > end) {
    const swap = start;
    start = end;
    end = swap;
  }
  if (!start && end) start = end;
  if (start && !end) end = start;
  return { start, end };
}

function mapItem(raw: unknown): GanttItem | null {
  if (!raw || typeof raw !== 'object') return null;
  const row = raw as Record<string, unknown>;
  const id = relationId(row.__dataId ?? row.dataId);
  if (!id) return null;
  const span = spanOf(row);
  return {
    id,
    key: String(row.key ?? '').trim() || id,
    title: String(row.title ?? '').trim(),
    stateId: relationId(row.stateId),
    assigneeId: relationId(row.assignee),
    start: span.start,
    end: span.end,
  };
}

function daysOfMonth(month: Date): { key: string; number: number; today: boolean }[] {
  const today = ymd(new Date());
  const count = new Date(month.getFullYear(), month.getMonth() + 1, 0).getDate();
  return Array.from({ length: count }, (_, index) => {
    const date = new Date(month.getFullYear(), month.getMonth(), index + 1);
    const key = ymd(date);
    return { key, number: index + 1, today: key === today };
  });
}

function barStyle(item: GanttItem): Record<string, string> {
  const count = monthDays.value.length || 1;
  const start = item.start! < monthStart.value ? monthStart.value : item.start!;
  const end = item.end! > monthEnd.value ? monthEnd.value : item.end!;
  const startIndex = Math.max(0, monthDays.value.findIndex((day) => day.key === start));
  const endIndex = Math.max(startIndex, monthDays.value.findIndex((day) => day.key === end));
  const paint = tmStatusBarColors(item.stateId ? stateColor.value[item.stateId] : null);
  return {
    left: `${(startIndex / count) * 100}%`,
    width: `${((endIndex - startIndex + 1) / count) * 100}%`,
    background: paint.background,
    color: paint.color,
  };
}

function sprintStyle(sprint: OcSprint): Record<string, string> {
  const count = monthDays.value.length || 1;
  const startKey = sprint.startDate.slice(0, 10);
  const endKey = sprint.endDate.slice(0, 10);
  const start = startKey < monthStart.value ? monthStart.value : startKey;
  const end = endKey > monthEnd.value ? monthEnd.value : endKey;
  const startIndex = Math.max(0, monthDays.value.findIndex((day) => day.key === start));
  const endIndex = Math.max(startIndex, monthDays.value.findIndex((day) => day.key === end));
  return {
    left: `${(startIndex / count) * 100}%`,
    width: `${((endIndex - startIndex + 1) / count) * 100}%`,
  };
}

function profileTo(id: string): string {
  return buildWorkItemProfilePath(id, {
    workspaceId: props.workspaceId,
    from: 'workspace',
    view: 'gantt',
  });
}

function stateLabel(item: GanttItem): string {
  return item.stateId ? stateName.value[item.stateId] || '' : '';
}

function assigneeLabel(item: GanttItem): string {
  return item.assigneeId ? personName.value[item.assigneeId] || '' : '';
}

function barTitle(item: GanttItem): string {
  return [item.key, item.title, `${item.start} – ${item.end}`, stateLabel(item), assigneeLabel(item)]
    .filter(Boolean)
    .join(' · ');
}

function shiftMonth(delta: number) {
  const current = cursor.value;
  cursor.value = new Date(current.getFullYear(), current.getMonth() + delta, 1);
}

function goToday() {
  cursor.value = startOfMonth(new Date());
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
    items.value = page.items.map(mapItem).filter((row): row is GanttItem => !!row);
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
    if (seq === loadSeq) errorLocal.value = t('operationCore.gantt.loadError');
  } finally {
    if (seq === loadSeq) loading.value = false;
  }
}
</script>

<template>
  <div class="oc-gantt">
    <div class="d-flex align-center ga-2 mb-3">
      <v-btn icon variant="text" size="small" :aria-label="t('operationCore.gantt.prev')" @click="shiftMonth(-1)">
        <v-icon icon="mdi-chevron-left" />
      </v-btn>
      <div class="text-subtitle-1 font-weight-bold text-capitalize">{{ monthTitle }}</div>
      <v-btn icon variant="text" size="small" :aria-label="t('operationCore.gantt.next')" @click="shiftMonth(1)">
        <v-icon icon="mdi-chevron-right" />
      </v-btn>
      <v-btn size="small" variant="tonal" class="text-none" @click="goToday">
        {{ t('operationCore.gantt.today') }}
      </v-btn>
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />
    <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-3 rounded-lg">
      {{ errorLocal }}
    </v-alert>

    <div class="oc-gantt-scroll">
      <div class="oc-gantt-grid" :style="{ '--days': monthDays.length }">
        <div class="oc-gantt-label oc-gantt-label--head" />
        <div class="oc-gantt-track oc-gantt-track--head">
          <div
            v-for="day in monthDays"
            :key="day.key"
            class="oc-gantt-day"
            :class="{ 'oc-gantt-day--today': day.today }"
          >
            {{ day.number }}
          </div>
        </div>

        <template v-if="showSprints && monthSprints.length">
          <div class="oc-gantt-label text-caption text-medium-emphasis">{{ t('operationCore.sprints.title') }}</div>
          <div class="oc-gantt-track oc-gantt-track--bands">
            <div
              v-for="sprint in monthSprints"
              :key="sprint.id"
              class="oc-gantt-band text-truncate"
              :class="`oc-gantt-band--${sprint.state}`"
              :style="sprintStyle(sprint)"
              :title="sprint.name"
            >
              {{ sprint.name }}
            </div>
          </div>
        </template>

        <template v-for="item in bars" :key="item.id">
          <NuxtLink :to="profileTo(item.id)" class="oc-gantt-label text-truncate" :title="barTitle(item)">
            <span class="font-weight-medium">{{ item.key }}</span>
            <span class="text-medium-emphasis"> {{ item.title }}</span>
          </NuxtLink>
          <div class="oc-gantt-track">
            <div
              v-for="day in monthDays"
              :key="`${item.id}-${day.key}`"
              class="oc-gantt-day oc-gantt-day--cell"
              :class="{ 'oc-gantt-day--today': day.key === todayKey }"
            />
            <NuxtLink :to="profileTo(item.id)" class="oc-gantt-bar" :style="barStyle(item)" :title="barTitle(item)">
              <span class="oc-gantt-bar__text">{{ item.key }}</span>
            </NuxtLink>
          </div>
        </template>
      </div>
    </div>

    <p v-if="!loading && bars.length === 0" class="text-caption text-medium-emphasis mt-3 mb-0">
      {{ t('operationCore.gantt.emptyMonth') }}
    </p>
    <p v-if="total > items.length" class="text-caption text-medium-emphasis mt-2 mb-0">
      {{ t('operationCore.gantt.truncated', { count: items.length }) }}
    </p>

    <div class="mt-5">
      <div class="text-subtitle-2 font-weight-bold mb-2">
        {{ t('operationCore.gantt.undated') }}
        <span class="text-medium-emphasis font-weight-regular">({{ undated.length }})</span>
      </div>
      <p v-if="!undated.length" class="text-caption text-medium-emphasis mb-0">
        {{ t('operationCore.gantt.undatedEmpty') }}
      </p>
      <div v-else class="d-flex flex-column ga-1">
        <NuxtLink v-for="item in undated" :key="item.id" :to="profileTo(item.id)" class="oc-gantt-undated text-truncate">
          <span class="font-weight-medium">{{ item.key }}</span>
          {{ item.title }}
          <span v-if="stateLabel(item)" class="text-medium-emphasis"> · {{ stateLabel(item) }}</span>
        </NuxtLink>
      </div>
    </div>
  </div>
</template>

<style scoped>
.oc-gantt-scroll {
  overflow-x: auto;
}

.oc-gantt-grid {
  display: grid;
  grid-template-columns: 220px minmax(calc(var(--days) * 28px), 1fr);
  min-width: 720px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 12px;
  overflow: hidden;
}

.oc-gantt-label {
  display: flex;
  align-items: center;
  padding: 0 12px;
  min-height: 36px;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
  color: inherit;
  text-decoration: none;
  font-size: 0.8125rem;
}

.oc-gantt-label--head {
  min-height: 32px;
  border-top: 0;
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.oc-gantt-track {
  position: relative;
  display: grid;
  grid-template-columns: repeat(var(--days), minmax(28px, 1fr));
  min-height: 36px;
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-left: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.oc-gantt-track--head {
  min-height: 32px;
  border-top: 0;
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.oc-gantt-track--bands {
  min-height: 28px;
}

.oc-gantt-day {
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 0.75rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
}

.oc-gantt-day--cell {
  border-left: 1px solid rgba(var(--v-border-color), 0.35);
}

.oc-gantt-day--cell:first-child {
  border-left: 0;
}

.oc-gantt-day--today {
  background: rgba(var(--v-theme-primary), 0.08);
  color: rgb(var(--v-theme-primary));
  font-weight: 700;
}

.oc-gantt-bar,
.oc-gantt-band {
  position: absolute;
  top: 7px;
  height: 22px;
  border-radius: 6px;
  color: white;
  font-size: 0.75rem;
  line-height: 22px;
  padding: 0 6px;
  text-decoration: none;
  overflow: hidden;
  white-space: nowrap;
}

.oc-gantt-band {
  top: 4px;
  height: 20px;
  line-height: 20px;
  color: rgb(var(--v-theme-on-surface));
}

.oc-gantt-band--active {
  background: rgba(var(--v-theme-primary), 0.25);
}

.oc-gantt-band--planned {
  background: rgba(var(--v-theme-info), 0.22);
}

.oc-gantt-band--closed {
  background: rgba(var(--v-theme-on-surface), 0.12);
}

.oc-gantt-undated {
  color: inherit;
  text-decoration: none;
  font-size: 0.875rem;
}
</style>
