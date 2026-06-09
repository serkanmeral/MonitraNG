<script setup lang="ts">
import { computed } from 'vue';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';
import { scenarioEventsLink } from '@/composables/useSiemScenarioCatalog';
import { formatRelativeTimeSimple } from '@/utils/widgets/widgetTableFormats';
import { useLocaleStore } from '@/stores/locale';
import {
  buildScenarioCardsFromRollup,
  scenarioStripState,
  type ScenarioCard,
} from '@/utils/siem/siemScenarioCards';
import { coerceWidgetDataToScenarioRollups } from '@/utils/alarm/alarmScenarioRollupNormalize';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
}>();

const localeStore = useLocaleStore();

const widgetConfig = computed(() => (props.widget.config ?? {}) as Record<string, unknown>);
/** Dashboard özet panosunda varsayılan: kompakt şerit (uzun tablo yok) */
const compact = computed(() => widgetConfig.value.compact !== false);

const rollups = computed(() => coerceWidgetDataToScenarioRollups(props.data?.data));

const scenarioCards = computed(() => buildScenarioCardsFromRollup(rollups.value));
const openScenarioCards = computed(() => scenarioCards.value.filter((c) => c.open));
const otherScenarioCards = computed(() => scenarioCards.value.filter((c) => !c.open));

const hasScenarioChart = computed(() =>
  scenarioCards.value.some((c) => c.totalAlarms > 0 || c.open),
);

const stripSummary = computed(() => {
  let open = 0;
  let seen = 0;
  let clean = 0;
  for (const card of scenarioCards.value) {
    const state = scenarioStripState(card);
    if (state === 'open') open += 1;
    else if (state === 'seen') seen += 1;
    else clean += 1;
  }
  return { open, seen, clean };
});

const chartHeight = computed(() =>
  compact.value ? 160 : Math.max(280, scenarioCards.value.length * 30 + 48),
);

function scenarioTitle(def: ScenarioCard['def']): string {
  const key = `siemCenter.scenarios.${def.id}.title`;
  const tr = props.t?.(key);
  return tr && tr !== key ? tr : def.id;
}

function scenarioDescription(def: ScenarioCard['def']): string {
  const key = `siemCenter.scenarios.${def.id}.desc`;
  const tr = props.t?.(key);
  return tr && tr !== key ? tr : '';
}

function statusLabel(card: ScenarioCard): string {
  if (card.open) return lbl('scenarioOpen');
  if (card.totalAlarms > 0) return lbl('scenarioSeen');
  return lbl('scenarioClean');
}

function statusColor(card: ScenarioCard): string {
  if (card.open) return 'error';
  if (card.totalAlarms > 0) return 'warning';
  return 'default';
}

function formatLastSeen(value: string | null): string {
  if (!value) return lbl('scenarioNever');
  return formatRelativeTimeSimple(value, localeStore.locale);
}

const chartLabels = computed(() =>
  scenarioCards.value.map((c) => `${c.def.id} · ${scenarioTitle(c.def)}`),
);

const chartSeries = computed(() => [
  {
    name: lbl('scenarioChartSeries'),
    data: scenarioCards.value.map((c) => Math.max(c.totalAlarms, c.open ? c.openCount : 0)),
  },
]);

const chartOptions = computed(() => ({
  chart: {
    type: 'bar',
    height: chartHeight.value,
    toolbar: { show: false },
    events: {
      dataPointSelection(_e: unknown, _ctx: unknown, config: { dataPointIndex?: number }) {
        const card = scenarioCards.value[config.dataPointIndex ?? -1];
        if (card) void navigateTo(scenarioEventsLink(card.def));
      },
    },
  },
  plotOptions: { bar: { horizontal: true, borderRadius: 4 } },
  xaxis: { categories: chartLabels.value },
  colors: scenarioCards.value.map((c) => {
    const state = scenarioStripState(c);
    if (state === 'open') return '#F44336';
    if (state === 'seen') return '#FB8C00';
    return '#9E9E9E';
  }),
}));

const lbl = (key: string) => props.t?.(`siemCenter.dashboard.${key}`) ?? key;
</script>

<template>
  <v-card variant="outlined" class="siem-scenario-widget" :class="{ 'siem-scenario-widget--compact': compact }">
    <v-card-item class="pb-0">
      <div class="d-flex align-center flex-wrap ga-2">
        <v-card-title class="text-h6 pa-0">{{ widget.title || lbl('scenariosTitle') }}</v-card-title>
        <v-spacer />
        <v-btn
          variant="text"
          size="small"
          prepend-icon="mdi-shield-search"
          to="/apps/siem-center/events"
        >
          {{ lbl('openEvents') }}
        </v-btn>
      </div>
    </v-card-item>

    <v-card-text>
      <div v-if="!scenarioCards.length" class="text-medium-emphasis text-center py-6">
        {{ lbl('scenarioChartEmpty') }}
      </div>

      <template v-else>
        <div class="scenario-status-strip mb-3">
          <div class="d-flex flex-wrap align-center justify-space-between gap-2 mb-2">
            <span class="text-caption text-medium-emphasis">
              {{ lbl('scenarioStripHint') }}
            </span>
            <div class="d-flex flex-wrap gap-3 text-caption text-medium-emphasis">
              <span class="d-inline-flex align-center gap-1">
                <span class="scenario-strip-dot scenario-strip-dot--open" />
                {{ lbl('scenarioOpen') }}
              </span>
              <span class="d-inline-flex align-center gap-1">
                <span class="scenario-strip-dot scenario-strip-dot--seen" />
                {{ lbl('scenarioSeen') }}
              </span>
              <span class="d-inline-flex align-center gap-1">
                <span class="scenario-strip-dot scenario-strip-dot--clean" />
                {{ lbl('scenarioClean') }}
              </span>
            </div>
          </div>

          <div class="scenario-status-strip__grid">
            <v-tooltip
              v-for="card in scenarioCards"
              :key="card.def.id"
              location="top"
              :text="`${card.def.id} · ${scenarioTitle(card.def)} — ${statusLabel(card)}`"
            >
              <template #activator="{ props: tipProps }">
                <NuxtLink
                  v-bind="tipProps"
                  :to="scenarioEventsLink(card.def)"
                  class="scenario-strip-cell"
                  :class="`scenario-strip-cell--${scenarioStripState(card)}`"
                >
                  {{ card.def.id }}
                </NuxtLink>
              </template>
            </v-tooltip>
          </div>
        </div>

        <div class="d-flex flex-wrap align-center gap-2 mb-3 text-caption text-medium-emphasis">
          <v-chip size="x-small" :color="stripSummary.open ? 'error' : 'default'" variant="tonal">
            {{ lbl('scenarioSummaryOpen').replace('{n}', String(stripSummary.open)) }}
          </v-chip>
          <v-chip size="x-small" color="warning" variant="tonal">
            {{ lbl('scenarioSummarySeen').replace('{n}', String(stripSummary.seen)) }}
          </v-chip>
          <v-chip size="x-small" variant="tonal">
            {{ lbl('scenarioSummaryClean').replace('{n}', String(stripSummary.clean)) }}
          </v-chip>
        </div>

        <template v-if="!compact">
          <div class="mb-4">
            <div class="text-body-2 font-weight-bold mb-1">{{ lbl('scenarioChartTitle') }}</div>
            <div v-if="!hasScenarioChart" class="siem-chart-empty siem-chart-empty--compact">
              {{ lbl('scenarioChartEmpty') }}
            </div>
            <ClientOnly v-else>
              <apexchart
                type="bar"
                :height="chartHeight"
                :options="chartOptions"
                :series="chartSeries"
              />
            </ClientOnly>
          </div>
        </template>

        <div v-if="openScenarioCards.length" class="mb-3">
          <div class="text-subtitle-2 font-weight-medium mb-2">
            {{ lbl('scenariosOpenTitle').replace('{n}', String(openScenarioCards.length)) }}
          </div>
          <v-row dense>
            <v-col
              v-for="card in openScenarioCards"
              :key="card.def.id"
              :cols="compact ? 12 : 12"
              :sm="compact ? 12 : 6"
              :lg="compact ? 6 : 4"
            >
              <NuxtLink :to="scenarioEventsLink(card.def)" class="text-decoration-none">
                <v-card variant="outlined" class="scenario-card scenario-card--active pa-3 h-100">
                  <div class="d-flex justify-space-between align-start ga-2">
                    <div>
                      <div class="text-subtitle-2 font-weight-bold">{{ card.def.id }}</div>
                      <div class="text-caption text-medium-emphasis">{{ scenarioTitle(card.def) }}</div>
                    </div>
                    <v-chip size="x-small" color="error" variant="flat">{{ lbl('scenarioOpen') }}</v-chip>
                  </div>
                  <div v-if="!compact" class="text-caption text-medium-emphasis mt-1">
                    {{ scenarioDescription(card.def) }}
                  </div>
                </v-card>
              </NuxtLink>
            </v-col>
          </v-row>
        </div>

        <div
          v-else-if="compact"
          class="text-body-2 text-medium-emphasis mb-2"
        >
          {{ lbl('scenariosNoOpen') }}
        </div>
        <div v-else class="text-body-2 text-medium-emphasis mb-3">
          {{ lbl('scenariosNoOpen') }}
        </div>

        <template v-if="!compact && otherScenarioCards.length">
          <div class="text-subtitle-2 font-weight-medium mb-2">{{ lbl('scenariosOthersTitle') }}</div>
          <v-table density="comfortable" class="siem-scenario-table">
            <thead>
              <tr>
                <th>{{ lbl('scenarioColId') }}</th>
                <th>{{ lbl('scenarioColStatus') }}</th>
                <th class="text-no-wrap">{{ lbl('scenarioColCount') }}</th>
                <th class="text-no-wrap">{{ lbl('scenarioColLastSeen') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr
                v-for="card in otherScenarioCards"
                :key="card.def.id"
                class="siem-scenario-table__row"
                @click="void navigateTo(scenarioEventsLink(card.def))"
              >
                <td>
                  <div class="font-weight-medium">{{ card.def.id }}</div>
                  <div class="text-caption text-medium-emphasis">{{ scenarioTitle(card.def) }}</div>
                </td>
                <td>
                  <v-chip size="x-small" :color="statusColor(card)" variant="tonal">
                    {{ statusLabel(card) }}
                  </v-chip>
                </td>
                <td>{{ card.totalAlarms }}</td>
                <td class="text-no-wrap">{{ formatLastSeen(card.lastSeenAt) }}</td>
              </tr>
            </tbody>
          </v-table>
        </template>

        <div v-if="compact" class="d-flex justify-end mt-1">
          <v-btn variant="text" size="small" to="/apps/siem-center" append-icon="mdi-arrow-right">
            {{ lbl('scenarioViewFullPanel') }}
          </v-btn>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.siem-scenario-widget--compact :deep(.v-card-text) {
  padding-top: 8px;
}

.scenario-status-strip__grid {
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 8px;
}

@media (min-width: 960px) {
  .scenario-status-strip__grid {
    grid-template-columns: repeat(10, minmax(0, 1fr));
  }
}

.siem-scenario-widget--compact .scenario-status-strip__grid {
  grid-template-columns: repeat(5, minmax(0, 1fr));
}

@media (min-width: 600px) {
  .siem-scenario-widget--compact .scenario-status-strip__grid {
    grid-template-columns: repeat(10, minmax(0, 1fr));
  }
}

.scenario-strip-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 36px;
  border-radius: 8px;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.02em;
  text-decoration: none;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  transition: transform 0.15s ease, box-shadow 0.15s ease;
}

.scenario-strip-cell:hover {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}

.scenario-strip-cell--open {
  color: rgb(var(--v-theme-error));
  background: rgba(var(--v-theme-error), 0.12);
  border-color: rgba(var(--v-theme-error), 0.45);
}

.scenario-strip-cell--seen {
  color: rgb(var(--v-theme-warning));
  background: rgba(var(--v-theme-warning), 0.1);
  border-color: rgba(var(--v-theme-warning), 0.35);
}

.scenario-strip-cell--clean {
  color: rgba(var(--v-theme-on-surface), 0.55);
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.scenario-strip-dot {
  width: 8px;
  height: 8px;
  border-radius: 999px;
  flex-shrink: 0;
}

.scenario-strip-dot--open {
  background: rgb(var(--v-theme-error));
}

.scenario-strip-dot--seen {
  background: rgb(var(--v-theme-warning));
}

.scenario-strip-dot--clean {
  background: rgba(var(--v-theme-on-surface), 0.28);
}

.siem-chart-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 120px;
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-size: 0.875rem;
}

.siem-scenario-table__row {
  cursor: pointer;
}

.siem-scenario-table__row:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.scenario-card--active {
  border-color: rgb(var(--v-theme-error));
}
</style>
