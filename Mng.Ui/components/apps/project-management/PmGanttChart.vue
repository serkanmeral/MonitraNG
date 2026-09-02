<script setup lang="ts">
import { computed, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { PmDependency, PmWbsItem } from '@/types/apps/projectManagement';
import {
  pmBuildGanttBars,
  pmBuildGanttLinks,
  pmBuildGanttRange,
  pmFsConnectorPath,
  pmGanttHeaderTicks,
  pmGanttPxPerDay,
  pmGanttTodayDay,
  pmSuggestGanttScale,
  type PmGanttScale,
} from '@/utils/pmGanttLayout';
import { pmDateInput } from '@/services/projectManagementService';

const props = defineProps<{
  items: PmWbsItem[];
  dependencies: PmDependency[];
}>();

const emit = defineEmits<{
  edit: [item: PmWbsItem];
}>();

const { t } = useAppI18n();

const ROW_H = 36;
const HEADER_H = 44;
const LABEL_W = 260;

const hoveredId = ref<string | null>(null);
const scaleOverride = ref<PmGanttScale | null>(null);
const range = computed(() => pmBuildGanttRange(props.items));
const scale = computed<PmGanttScale>({
  get: () => scaleOverride.value ?? pmSuggestGanttScale(range.value),
  set: (value) => {
    scaleOverride.value = value;
  },
});
const bars = computed(() => pmBuildGanttBars(props.items, range.value));
const links = computed(() => pmBuildGanttLinks(props.dependencies, bars.value));
const ticks = computed(() => pmGanttHeaderTicks(range.value, scale.value));
const todayDay = computed(() => pmGanttTodayDay(range.value));
const pxPerDay = computed(() => pmGanttPxPerDay(scale.value));
const chartWidth = computed(() => Math.max(range.value.dayCount * pxPerDay.value, 320));
const chartHeight = computed(() => HEADER_H + Math.max(bars.value.length, 1) * ROW_H);

const scaleItems = computed(() => [
  { title: t('projectManagement.gantt.scaleDay'), value: 'day' as const },
  { title: t('projectManagement.gantt.scaleWeek'), value: 'week' as const },
]);

function itemById(id: string) {
  return props.items.find((row) => row.id === id);
}

function workItemHref(id: string) {
  return `/apps/operation-core/work-items/${encodeURIComponent(id)}/profile`;
}

function workItemTo(barId: string) {
  const item = itemById(barId);
  return item?.workItemId ? workItemHref(item.workItemId) : '#';
}

function chipColorFor(barId: string) {
  const item = itemById(barId);
  return item ? stateChipColor(item) : 'default';
}

function onBarClick(id: string) {
  const item = itemById(id);
  if (item) emit('edit', item);
}

function hasRange(bar: (typeof bars.value)[number], kind: 'planned' | 'baseline' | 'actual') {
  if (kind === 'baseline') return bar.baselineStartDay != null && bar.baselineEndDay != null;
  if (kind === 'actual') return bar.actualStartDay != null && bar.actualEndDay != null;
  return !bar.undated;
}

function barStyle(bar: (typeof bars.value)[number], kind: 'planned' | 'baseline' | 'actual') {
  let start = bar.startDay;
  let end = bar.endDay;
  if (kind === 'baseline') {
    start = bar.baselineStartDay ?? 0;
    end = bar.baselineEndDay ?? start;
  }
  if (kind === 'actual') {
    start = bar.actualStartDay ?? 0;
    end = bar.actualEndDay ?? start;
  }
  const left = start * pxPerDay.value;
  const width = Math.max((end - start + 1) * pxPerDay.value, bar.isMilestone ? 0 : 8);
  return { left: `${left}px`, width: `${width}px` };
}

function diamondStyle(bar: (typeof bars.value)[number]) {
  const left = bar.startDay * pxPerDay.value + pxPerDay.value / 2 - 7;
  return { left: `${left}px` };
}

function tooltip(bar: (typeof bars.value)[number]) {
  const item = itemById(bar.id);
  if (!item) return bar.name;
  const planned = [pmDateInput(item.plannedStart), pmDateInput(item.plannedFinish)].filter(Boolean).join(' → ');
  const bits = [bar.wbsCode, bar.name, planned].filter(Boolean);
  if (item.workItemKey) bits.push(item.workItemKey);
  if (item.workItemStateName) bits.push(item.workItemStateName);
  if (bar.drifted) bits.push(t('projectManagement.drift'));
  if (bar.undated) bits.push(t('projectManagement.gantt.undated'));
  return bits.join(' · ');
}

function stateChipColor(item: { workItemClosed?: boolean; workItemStateCategory?: string | null }) {
  if (item.workItemClosed) return 'success';
  if (item.workItemStateCategory === 'in_progress') return 'info';
  return 'default';
}
</script>

<template>
  <div class="pm-gantt">
    <div class="d-flex align-center justify-space-between flex-wrap ga-2 mb-3">
      <div class="d-flex flex-wrap ga-3 text-caption text-medium-emphasis">
        <span class="pm-legend pm-legend--task">{{ t('projectManagement.kind.task') }}</span>
        <span class="pm-legend pm-legend--summary">{{ t('projectManagement.kind.summary') }}</span>
        <span class="pm-legend pm-legend--ms">{{ t('projectManagement.kind.milestone') }}</span>
        <span class="pm-legend pm-legend--baseline">{{ t('projectManagement.fields.baseline') }}</span>
        <span class="pm-legend pm-legend--drift">{{ t('projectManagement.drift') }}</span>
        <span class="pm-legend pm-legend--actual">{{ t('projectManagement.gantt.actual') }}</span>
      </div>
      <v-btn-toggle v-model="scale" mandatory density="compact" color="primary" variant="outlined" divided>
        <v-btn v-for="opt in scaleItems" :key="opt.value" :value="opt.value" size="small">{{ opt.title }}</v-btn>
      </v-btn-toggle>
    </div>

    <div v-if="!items.length" class="text-center py-8 text-medium-emphasis">
      {{ t('projectManagement.emptyWbs') }}
    </div>

    <div v-else class="pm-gantt__frame rounded-lg border">
      <div class="pm-gantt__labels" :style="{ width: `${LABEL_W}px` }">
        <div class="pm-gantt__label-head">{{ t('projectManagement.wbsTitle') }}</div>
        <div
          v-for="bar in bars"
          :key="bar.id"
          class="pm-gantt__label"
          :class="{ 'pm-gantt__label--hover': hoveredId === bar.id }"
          :style="{ paddingLeft: `${8 + bar.depth * 12}px` }"
          :title="tooltip(bar)"
          role="button"
          tabindex="0"
          @click="onBarClick(bar.id)"
          @keydown.enter.prevent="onBarClick(bar.id)"
          @mouseenter="hoveredId = bar.id"
          @mouseleave="hoveredId = null"
        >
          <span class="pm-gantt__code">{{ bar.wbsCode || '—' }}</span>
          <span class="pm-gantt__name text-truncate">{{ bar.name }}</span>
          <NuxtLink
            v-if="itemById(bar.id)?.workItemId && itemById(bar.id)?.workItemKey"
            :to="workItemTo(bar.id)"
            class="text-decoration-none"
            @click.stop
          >
            <v-chip size="x-small" :color="chipColorFor(bar.id)" variant="tonal">
              {{ itemById(bar.id)?.workItemKey }}
            </v-chip>
          </NuxtLink>
          <v-chip v-if="bar.drifted" size="x-small" color="warning" variant="tonal" class="ml-1">
            {{ t('projectManagement.drift') }}
          </v-chip>
        </div>
      </div>

      <div class="pm-gantt__scroll">
        <div class="pm-gantt__canvas" :style="{ width: `${chartWidth}px`, height: `${chartHeight}px` }">
          <div class="pm-gantt__header" :style="{ height: `${HEADER_H}px` }">
            <div
              v-for="tick in ticks"
              :key="tick.day"
              class="pm-gantt__tick"
              :class="{ 'pm-gantt__tick--month': tick.isMonthStart }"
              :style="{ left: `${tick.day * pxPerDay}px`, width: `${(scale === 'week' ? 7 : 1) * pxPerDay}px` }"
            >
              <div v-if="tick.monthLabel" class="pm-gantt__month">{{ tick.monthLabel }}</div>
              <div class="pm-gantt__day">{{ tick.label }}</div>
            </div>
          </div>

          <div
            v-for="(bar, index) in bars"
            :key="bar.id"
            class="pm-gantt__row"
            :class="{ 'pm-gantt__row--alt': index % 2 === 1, 'pm-gantt__row--hover': hoveredId === bar.id }"
            :style="{ top: `${HEADER_H + index * ROW_H}px`, height: `${ROW_H}px`, width: `${chartWidth}px` }"
            @mouseenter="hoveredId = bar.id"
            @mouseleave="hoveredId = null"
          >
            <div
              v-if="hasRange(bar, 'baseline')"
              class="pm-gantt__baseline"
              :style="barStyle(bar, 'baseline')"
            />
            <button
              v-if="bar.isMilestone && !bar.undated"
              type="button"
              class="pm-gantt__diamond"
              :class="{ 'pm-gantt__diamond--drift': bar.drifted }"
              :style="diamondStyle(bar)"
              :title="tooltip(bar)"
              @click="onBarClick(bar.id)"
            />
            <button
              v-else-if="!bar.undated"
              type="button"
              class="pm-gantt__bar"
              :class="{
                'pm-gantt__bar--summary': bar.isSummary,
                'pm-gantt__bar--drift': bar.drifted,
              }"
              :style="barStyle(bar, 'planned')"
              :title="tooltip(bar)"
              @click="onBarClick(bar.id)"
            >
              <span class="pm-gantt__progress" :style="{ width: `${bar.percentComplete}%` }" />
            </button>
            <div
              v-if="hasRange(bar, 'actual')"
              class="pm-gantt__actual"
              :style="barStyle(bar, 'actual')"
            />
            <span v-if="bar.undated" class="pm-gantt__undated">{{ t('projectManagement.gantt.undated') }}</span>
          </div>

          <svg
            class="pm-gantt__svg"
            :width="chartWidth"
            :height="chartHeight"
            :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
          >
            <line
              v-if="todayDay != null"
              class="pm-gantt__today"
              :x1="todayDay * pxPerDay + pxPerDay / 2"
              :x2="todayDay * pxPerDay + pxPerDay / 2"
              y1="0"
              :y2="chartHeight"
            />
            <path
              v-for="link in links"
              :key="link.id"
              class="pm-gantt__link"
              :d="pmFsConnectorPath(link, pxPerDay, ROW_H, HEADER_H)"
              marker-end="url(#pm-fs-arrow)"
            >
              <title>{{ t('projectManagement.gantt.fsLink') }}{{ link.lagDays ? ` (${link.lagDays}d)` : '' }}</title>
            </path>
            <defs>
              <marker id="pm-fs-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M 0 0 L 10 5 L 0 10 z" fill="currentColor" />
              </marker>
            </defs>
          </svg>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.border {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.pm-gantt__frame {
  display: flex;
  max-height: min(70vh, 720px);
  overflow: auto;
  background: rgb(var(--v-theme-surface));
  align-items: flex-start;
}
.pm-gantt__labels {
  flex: 0 0 auto;
  position: sticky;
  left: 0;
  z-index: 3;
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
}
.pm-gantt__label-head,
.pm-gantt__label {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 0 8px;
  width: 100%;
  border: 0;
  background: transparent;
  text-align: left;
  font: inherit;
  color: inherit;
}
.pm-gantt__label-head {
  height: 44px;
  font-weight: 600;
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  position: sticky;
  top: 0;
  z-index: 4;
  background: rgb(var(--v-theme-surface));
}
.pm-gantt__label {
  height: 36px;
  cursor: pointer;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.35);
}
.pm-gantt__label--hover,
.pm-gantt__row--hover {
  background: rgba(var(--v-theme-primary), 0.06);
}
.pm-gantt__code {
  font-variant-numeric: tabular-nums;
  color: rgba(var(--v-theme-on-surface), 0.6);
  min-width: 42px;
}
.pm-gantt__name {
  min-width: 0;
  flex: 1;
}
.pm-gantt__scroll {
  flex: 1 1 auto;
  min-width: 0;
}
.pm-gantt__canvas {
  position: relative;
}
.pm-gantt__header {
  position: sticky;
  top: 0;
  z-index: 1;
  background: rgb(var(--v-theme-surface));
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.pm-gantt__tick {
  position: absolute;
  top: 0;
  bottom: 0;
  border-left: 1px solid rgba(var(--v-border-color), 0.35);
  padding: 2px 4px;
  box-sizing: border-box;
}
.pm-gantt__tick--month {
  border-left-color: rgba(var(--v-theme-on-surface), 0.28);
}
.pm-gantt__month {
  font-size: 11px;
  font-weight: 600;
  line-height: 16px;
  white-space: nowrap;
}
.pm-gantt__day {
  font-size: 11px;
  opacity: 0.75;
  line-height: 14px;
}
.pm-gantt__row {
  position: absolute;
  left: 0;
  border-bottom: 1px solid rgba(var(--v-border-color), 0.25);
}
.pm-gantt__row--alt {
  background: rgba(var(--v-theme-on-surface), 0.025);
}
.pm-gantt__bar,
.pm-gantt__baseline,
.pm-gantt__actual,
.pm-gantt__diamond,
.pm-gantt__undated {
  position: absolute;
}
.pm-gantt__bar {
  top: 8px;
  height: 16px;
  border: 0;
  border-radius: 4px;
  padding: 0;
  overflow: hidden;
  cursor: pointer;
  background: rgb(var(--v-theme-primary));
}
.pm-gantt__bar--summary {
  top: 10px;
  height: 12px;
  background: transparent;
  border: 2px solid rgb(var(--v-theme-primary));
  border-radius: 2px;
}
.pm-gantt__bar--drift {
  background: rgb(var(--v-theme-warning));
}
.pm-gantt__bar--summary.pm-gantt__bar--drift {
  background: transparent;
  border-color: rgb(var(--v-theme-warning));
}
.pm-gantt__progress {
  display: block;
  height: 100%;
  background: rgba(255, 255, 255, 0.35);
}
.pm-gantt__baseline {
  top: 26px;
  height: 4px;
  border-radius: 2px;
  background: rgba(var(--v-theme-on-surface), 0.35);
  pointer-events: none;
}
.pm-gantt__actual {
  top: 6px;
  height: 3px;
  border-radius: 1px;
  background: rgb(var(--v-theme-success));
  pointer-events: none;
}
.pm-gantt__diamond {
  top: 11px;
  width: 14px;
  height: 14px;
  border: 0;
  padding: 0;
  background: rgb(var(--v-theme-primary));
  transform: rotate(45deg);
  cursor: pointer;
}
.pm-gantt__diamond--drift {
  background: rgb(var(--v-theme-warning));
}
.pm-gantt__undated {
  left: 8px;
  top: 8px;
  font-size: 12px;
  opacity: 0.55;
}
.pm-gantt__svg {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: visible;
  color: rgba(var(--v-theme-on-surface), 0.55);
}
.pm-gantt__link {
  fill: none;
  stroke: currentColor;
  stroke-width: 1.4;
}
.pm-gantt__today {
  stroke: rgb(var(--v-theme-error));
  stroke-width: 1.5;
  stroke-dasharray: 4 3;
}
.pm-legend {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.pm-legend::before {
  content: '';
  width: 12px;
  height: 8px;
  border-radius: 2px;
  background: rgb(var(--v-theme-primary));
}
.pm-legend--summary::before {
  background: transparent;
  border: 2px solid rgb(var(--v-theme-primary));
  height: 6px;
}
.pm-legend--ms::before {
  width: 8px;
  height: 8px;
  transform: rotate(45deg);
  border-radius: 0;
}
.pm-legend--baseline::before {
  background: rgba(var(--v-theme-on-surface), 0.35);
  height: 4px;
}
.pm-legend--drift::before {
  background: rgb(var(--v-theme-warning));
}
.pm-legend--actual::before {
  background: rgb(var(--v-theme-success));
  height: 3px;
}
.pm-legend--fs::before {
  width: 16px;
  height: 0;
  border-radius: 0;
  background: transparent;
  border-top: 2px solid rgba(var(--v-theme-on-surface), 0.55);
}
</style>
