<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { FieldDefinition } from '@/stores/apps/dataset';
import type {
  ReportingSummaryConfig,
  ReportingSummaryMetric,
  ReportingSummaryMetricKind,
  ReportingSummaryPlacement,
} from '@/types/apps/reporting';
import { emptyReportingSummaryConfig } from '@/utils/reportingSummary';
import { reportingFieldLabel } from '@/utils/reportingListConfig';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = defineProps<{
  summary: ReportingSummaryConfig;
  fields: FieldDefinition[];
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:summary': [ReportingSummaryConfig];
}>();

const { t } = useAppI18n();

const config = computed({
  get: () => props.summary ?? emptyReportingSummaryConfig(),
  set: (v: ReportingSummaryConfig) => emit('update:summary', v),
});

const placementItems = computed(() =>
  (['none', 'cards', 'footer', 'both'] as ReportingSummaryPlacement[]).map((value) => ({
    value,
    title: t(`reporting.summary.placements.${value}`),
  }))
);

const kindItems = computed(() =>
  (['count', 'sum'] as ReportingSummaryMetricKind[]).map((value) => ({
    value,
    title: t(`reporting.summary.kinds.${value}`),
  }))
);

const numberFieldItems = computed(() =>
  props.fields
    .filter((f) => f.fieldType === 'number' || f.fieldType === 'incremental')
    .map((f) => ({ value: f.name, title: reportingFieldLabel(f, f.name) }))
);

function patch(partial: Partial<ReportingSummaryConfig>) {
  emit('update:summary', { ...config.value, ...partial });
}

function addMetric() {
  const metrics = [...config.value.metrics];
  const id = `m_${Date.now().toString(36)}`;
  metrics.push({
    id,
    label: t('reporting.summary.newMetricLabel'),
    kind: 'count',
  });
  patch({
    metrics,
    placement: config.value.placement === 'none' ? 'cards' : config.value.placement,
  });
}

function updateMetric(index: number, partial: Partial<ReportingSummaryMetric>) {
  const metrics = config.value.metrics.map((m, i) => (i === index ? { ...m, ...partial } : m));
  patch({ metrics });
}

function removeMetric(index: number) {
  const metrics = config.value.metrics.filter((_, i) => i !== index);
  patch({
    metrics,
    placement: metrics.length ? config.value.placement : 'none',
  });
}

function onPlacementChange(value: unknown) {
  const v = String(value ?? 'none') as ReportingSummaryPlacement;
  patch({ placement: v });
}

function onMetricLabelChange(index: number, value: unknown) {
  updateMetric(index, { label: String(value ?? '') });
}

function onMetricKindChange(index: number, value: unknown) {
  const kind = (value === 'sum' ? 'sum' : 'count') as ReportingSummaryMetricKind;
  const metric = config.value.metrics[index];
  updateMetric(index, {
    kind,
    field: kind === 'sum' ? metric?.field : undefined,
  });
}

function onMetricFieldChange(index: number, value: unknown) {
  updateMetric(index, { field: String(value ?? '') });
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t('reporting.summary.hint') }}
    </v-alert>

    <v-select
      :model-value="config.placement"
      :items="placementItems"
      item-title="title"
      item-value="value"
      :label="t('reporting.summary.placement')"
      :disabled="disabled"
      density="compact"
      variant="outlined"
      hide-details
      class="mb-4"
      @update:model-value="onPlacementChange"
    />

    <div class="d-flex justify-space-between align-center mb-3">
      <div class="text-subtitle-2">{{ t('reporting.summary.metricsTitle') }}</div>
      <v-btn
        size="small"
        color="primary"
        variant="tonal"
        :disabled="disabled"
        @click="addMetric"
      >
        <PlusIcon size="16" class="mr-1" />
        {{ t('reporting.summary.addMetric') }}
      </v-btn>
    </div>

    <v-alert
      v-if="!config.metrics.length"
      type="info"
      variant="tonal"
      density="compact"
    >
      {{ t('reporting.summary.empty') }}
    </v-alert>

    <v-card
      v-for="(metric, index) in config.metrics"
      :key="metric.id"
      variant="outlined"
      class="mb-2 pa-3"
    >
      <div class="d-flex flex-wrap ga-2 align-start">
        <v-text-field
          :model-value="metric.label"
          :label="t('reporting.summary.formLabel')"
          :disabled="disabled"
          density="compact"
          variant="outlined"
          hide-details
          class="flex-grow-1"
          style="min-width: 140px"
          @update:model-value="(v) => onMetricLabelChange(index, v)"
        />
        <v-select
          :model-value="metric.kind"
          :items="kindItems"
          item-title="title"
          item-value="value"
          :label="t('reporting.summary.formKind')"
          :disabled="disabled"
          density="compact"
          variant="outlined"
          hide-details
          style="min-width: 120px"
          @update:model-value="(v) => onMetricKindChange(index, v)"
        />
        <v-select
          v-if="metric.kind === 'sum'"
          :model-value="metric.field"
          :items="numberFieldItems"
          item-title="title"
          item-value="value"
          :label="t('reporting.summary.formField')"
          :disabled="disabled"
          density="compact"
          variant="outlined"
          hide-details
          style="min-width: 140px"
          @update:model-value="(v) => onMetricFieldChange(index, v)"
        />
        <v-btn
          icon
          variant="text"
          size="small"
          color="error"
          :disabled="disabled"
          :aria-label="t('reporting.summary.removeMetric')"
          @click="removeMetric(index)"
        >
          <TrashIcon size="16" />
        </v-btn>
      </div>
    </v-card>
  </div>
</template>
