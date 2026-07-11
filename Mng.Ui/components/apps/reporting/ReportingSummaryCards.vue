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
  /** Smaller cards for runner chrome. */
  dense?: boolean;
}>();
</script>

<template>
  <v-row dense :class="dense ? 'mb-2' : 'mb-3'">
    <v-col
      v-for="metric in config.metrics"
      :key="metric.id"
      :cols="dense ? 6 : 12"
      :sm="dense ? 4 : 6"
      :md="dense ? 3 : 4"
      :lg="dense ? 2 : 3"
    >
      <v-card variant="outlined" :class="dense ? 'pa-2 h-100' : 'pa-3 h-100'">
        <div class="text-caption text-medium-emphasis mb-1">{{ metric.label }}</div>
        <div :class="dense ? 'text-h6 font-weight-medium' : 'text-h5 font-weight-medium'">
          <v-skeleton-loader v-if="loading" type="text" width="80" />
          <template v-else>
            {{ formatReportingSummaryValue(values[metric.id], metric.format) }}
          </template>
        </div>
      </v-card>
    </v-col>
  </v-row>
</template>
