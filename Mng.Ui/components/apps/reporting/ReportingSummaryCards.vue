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
  <v-row dense class="mb-3">
    <v-col
      v-for="metric in config.metrics"
      :key="metric.id"
      cols="12"
      sm="6"
      md="4"
      lg="3"
    >
      <v-card variant="outlined" class="pa-3 h-100">
        <div class="text-caption text-medium-emphasis mb-1">{{ metric.label }}</div>
        <div class="text-h5 font-weight-medium">
          <v-skeleton-loader v-if="loading" type="text" width="80" />
          <template v-else>
            {{ formatReportingSummaryValue(values[metric.id], metric.format) }}
          </template>
        </div>
      </v-card>
    </v-col>
  </v-row>
</template>
