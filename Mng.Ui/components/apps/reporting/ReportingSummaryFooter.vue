<script setup lang="ts">
import type { ReportingSummaryConfig } from '@/types/apps/reporting';
import {
  formatReportingSummaryValue,
  type ReportingSummaryValues,
} from '@/utils/reportingSummary';

defineProps<{
  config: ReportingSummaryConfig;
  values: ReportingSummaryValues;
  loading?: boolean;
}>();
</script>

<template>
  <div class="reporting-summary-footer d-flex flex-wrap align-center ga-4 mt-2 px-1">
    <template v-if="loading">
      <v-skeleton-loader type="text" width="160" />
    </template>
    <template v-else>
      <div v-for="metric in config.metrics" :key="metric.id" class="text-body-2">
        <span class="text-medium-emphasis">{{ metric.label }}:</span>
        <span class="font-weight-medium ml-1">
          {{ formatReportingSummaryValue(values[metric.id], metric.format) }}
        </span>
      </div>
    </template>
  </div>
</template>

<style scoped>
.reporting-summary-footer {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  padding-top: 8px;
}
</style>
