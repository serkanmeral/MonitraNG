<script setup lang="ts">
import { computed } from 'vue';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import type { ReportingReportParameter } from '@/types/apps/reporting';
import {
  buildReportingParameterSelectItems,
  normalizeReportingParameters,
  reportingParameterYearBounds,
} from '@/utils/reportingParameterModel';
import {
  formatReportingQuarterValue,
  parseReportingQuarterValue,
  reportingParamRangeFromKey,
  reportingParamRangeToKey,
  type ReportingParameterValues,
} from '@/utils/reportingParameterValueKeys';

const props = defineProps<{
  parameters: ReportingReportParameter[];
  modelValue: ReportingParameterValues;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [ReportingParameterValues];
  run: [ReportingParameterValues];
}>();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string) => {
  if (i18n?.t) return i18n.t(key);
  if (i18n?.global?.t) return i18n.global.t(key);
  return key;
};

const normalizedParameters = computed(() => normalizeReportingParameters(props.parameters));

const selectItemsById = computed(() => {
  const map: Record<string, { title: string; value: string }[]> = {};
  const allYears = t('reporting.runner.allYears');
  for (const param of normalizedParameters.value) {
    if (param.widget === 'select') {
      map[param.id] = buildReportingParameterSelectItems(param, allYears);
    }
  }
  return map;
});

const quarterItems = [
  { value: 1, title: 'Q1' },
  { value: 2, title: 'Q2' },
  { value: 3, title: 'Q3' },
  { value: 4, title: 'Q4' },
];

function normalizePatch(patch: ReportingParameterValues): ReportingParameterValues {
  const out: ReportingParameterValues = {};
  for (const [key, value] of Object.entries(patch)) {
    out[key] = value == null ? '' : String(value);
  }
  return out;
}

function patchValues(patch: ReportingParameterValues, triggerRun = false) {
  const next = { ...props.modelValue, ...normalizePatch(patch) };
  emit('update:modelValue', next);
  if (triggerRun) emit('run', next);
}

function updateValue(id: string, value: unknown, triggerRun = false) {
  patchValues({ [id]: value == null ? '' : String(value) }, triggerRun);
}

function onPersonChange(id: string, personId: string | null) {
  updateValue(id, personId?.trim() ?? '', true);
}

function choiceValue(param: (typeof normalizedParameters.value)[0]): string {
  const choices = param.binding.choices ?? [];
  return props.modelValue[param.id] || param.defaultValue || choices[0]?.value || '';
}

function choiceOptions(param: (typeof normalizedParameters.value)[0]) {
  return (
    param.binding.choices ??
    param.statusOptions?.map((o) => ({
      value: o.value,
      title: o.title,
      filters: o.filter ? [o.filter] : [],
    })) ??
    []
  );
}

function isMonthDatePart(param: (typeof normalizedParameters.value)[0]): boolean {
  return param.binding.kind === 'datePartRange' && param.binding.part === 'month';
}

function isQuarterDatePart(param: (typeof normalizedParameters.value)[0]): boolean {
  return param.binding.kind === 'datePartRange' && param.binding.part === 'quarter';
}

function quarterYear(param: (typeof normalizedParameters.value)[0]): number | null {
  const parsed = parseReportingQuarterValue(props.modelValue[param.id] ?? '');
  if (parsed) return parsed.year;
  const bounds = reportingParameterYearBounds(param);
  return bounds.max;
}

function quarterNumber(param: (typeof normalizedParameters.value)[0]): number | null {
  const parsed = parseReportingQuarterValue(props.modelValue[param.id] ?? '');
  return parsed?.quarter ?? null;
}

function updateQuarter(param: (typeof normalizedParameters.value)[0], year: number | null, quarter: number | null) {
  const value =
    year == null || quarter == null ? '' : formatReportingQuarterValue(year, quarter);
  updateValue(param.id, value, true);
}

function yearBounds(param: (typeof normalizedParameters.value)[0]) {
  return reportingParameterYearBounds(param);
}

function yearIncludeAll(param: (typeof normalizedParameters.value)[0]): boolean {
  return param.options?.kind === 'yearRange' ? param.options.includeAll !== false : false;
}

function updateRangeFrom(paramId: string, value: string) {
  patchValues({ [reportingParamRangeFromKey(paramId)]: value }, true);
}

function updateRangeTo(paramId: string, value: string) {
  patchValues({ [reportingParamRangeToKey(paramId)]: value }, true);
}
</script>

<template>
  <v-card v-if="parameters.length" elevation="0" variant="outlined" class="mb-4 reporting-parameters-panel">
    <v-card-title class="text-subtitle-2 font-weight-medium py-3 pb-0">
      {{ t('reporting.runner.parametersTitle') }}
    </v-card-title>
    <v-card-text class="d-flex flex-wrap align-end ga-3 pt-3">
      <template v-for="param in normalizedParameters" :key="param.id">
        <v-btn-toggle
          v-if="param.widget === 'buttonGroup'"
          :model-value="choiceValue(param)"
          :disabled="disabled"
          density="compact"
          color="primary"
          divided
          mandatory
          @update:model-value="(v: string) => { updateValue(param.id, v, true); }"
        >
          <v-btn
            v-for="opt in choiceOptions(param)"
            :key="opt.value"
            :value="opt.value"
            size="small"
            class="text-none"
          >
            {{ opt.title }}
          </v-btn>
        </v-btn-toggle>

        <v-select
          v-else-if="param.widget === 'select'"
          :model-value="modelValue[param.id] ?? ''"
          :items="selectItemsById[param.id] ?? []"
          item-title="title"
          item-value="value"
          :label="param.label"
          :disabled="disabled"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          style="min-width: 140px; max-width: 180px"
          @update:model-value="(v: string | null) => { updateValue(param.id, v ?? '', true); }"
        />

        <div
          v-else-if="param.widget === 'number' && isQuarterDatePart(param)"
          class="d-flex flex-wrap align-end ga-2"
        >
          <v-text-field
            :model-value="quarterYear(param)"
            :label="param.label + ' — ' + t('reporting.runner.quarterYear')"
            :disabled="disabled"
            type="number"
            :min="yearBounds(param).min"
            :max="yearBounds(param).max"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 120px; max-width: 140px"
            @update:model-value="(v: string) => {
              const y = v ? Number(v) : null;
              updateQuarter(param, Number.isFinite(y) ? y : null, quarterNumber(param));
            }"
          />
          <v-select
            :model-value="quarterNumber(param)"
            :items="quarterItems"
            item-title="title"
            item-value="value"
            :label="t('reporting.runner.quarter')"
            :disabled="disabled"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 100px; max-width: 120px"
            @update:model-value="(v: number | null) => {
              updateQuarter(param, quarterYear(param), v);
            }"
          />
        </div>

        <v-text-field
          v-else-if="param.widget === 'number'"
          :model-value="modelValue[param.id] ?? ''"
          :label="param.label"
          :disabled="disabled"
          type="number"
          :min="yearBounds(param).min"
          :max="yearBounds(param).max"
          :placeholder="yearIncludeAll(param) ? t('reporting.runner.allYears') : undefined"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          style="min-width: 120px; max-width: 160px"
          @update:model-value="(v: string) => updateValue(param.id, v ?? '', true)"
        />

        <v-text-field
          v-else-if="param.widget === 'date'"
          :model-value="modelValue[param.id] ?? ''"
          :label="param.label"
          :disabled="disabled"
          :type="isMonthDatePart(param) ? 'month' : 'date'"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          style="min-width: 160px; max-width: 220px"
          @update:model-value="(v: string) => updateValue(param.id, v ?? '', true)"
        />

        <div
          v-else-if="param.widget === 'dateRange'"
          class="d-flex flex-wrap align-end ga-2"
          style="min-width: 280px"
        >
          <v-text-field
            :model-value="modelValue[reportingParamRangeFromKey(param.id)] ?? ''"
            :label="param.label + ' — ' + t('reporting.runner.dateFrom')"
            :disabled="disabled"
            type="date"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 150px; max-width: 180px"
            @update:model-value="(v: string) => updateRangeFrom(param.id, v ?? '')"
          />
          <v-text-field
            :model-value="modelValue[reportingParamRangeToKey(param.id)] ?? ''"
            :label="t('reporting.runner.dateTo')"
            :disabled="disabled"
            type="date"
            density="compact"
            variant="outlined"
            hide-details
            clearable
            style="min-width: 150px; max-width: 180px"
            @update:model-value="(v: string) => updateRangeTo(param.id, v ?? '')"
          />
        </div>

        <v-text-field
          v-else-if="param.widget === 'search'"
          :model-value="modelValue[param.id] ?? ''"
          :label="param.label"
          :disabled="disabled"
          density="compact"
          variant="outlined"
          hide-details
          clearable
          prepend-inner-icon="mdi-magnify"
          style="min-width: 220px; max-width: 320px"
          @update:model-value="(v: string) => updateValue(param.id, v)"
          @keydown.enter="emit('run', modelValue)"
        />

        <div v-else-if="param.widget === 'personPicker'" style="min-width: 280px; max-width: 420px; flex: 1">
          <MngDirectoryPickerField
            :model-value="modelValue[param.id] || null"
            entity="user"
            :label="param.label + (param.required ? ' *' : '')"
            :disabled="disabled"
            density="compact"
            @update:model-value="(v: unknown) => onPersonChange(param.id, v == null ? null : String(v))"
          />
        </div>
      </template>

      <v-btn
        color="primary"
        variant="tonal"
        size="small"
        class="text-none"
        :disabled="disabled"
        @click="emit('run', modelValue)"
      >
        {{ t('reporting.actions.run') }}
      </v-btn>
    </v-card-text>
  </v-card>
</template>
