<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OcDashboardWidget } from '@/types/apps/operationCore';

const props = defineProps<{
  widget: OcDashboardWidget;
}>();

const { t } = useAppI18n();

const execution = computed(() => props.widget.execution ?? null);
const failed = computed(() => execution.value != null && execution.value.success === false);
const value = computed(() => execution.value?.total ?? 0);
const title = computed(() => props.widget.title?.trim() || props.widget.key);
</script>

<template>
  <v-card variant="outlined" class="rounded-lg h-100 oc-dash-summary">
    <v-card-text class="pa-4 d-flex flex-column h-100">
      <div class="d-flex align-center ga-2 mb-1">
        <v-icon icon="mdi-counter" size="20" color="primary" />
        <span class="text-subtitle-2 font-weight-medium text-truncate">{{ title }}</span>
      </div>

      <template v-if="failed">
        <div class="d-flex align-center ga-1 mt-2 text-error">
          <v-icon icon="mdi-alert-circle-outline" size="18" />
          <span class="text-caption">
            {{ execution?.errorMessage || t('operationCore.dashboards.widgetError') }}
          </span>
        </div>
      </template>
      <template v-else>
        <div class="text-h3 font-weight-bold mt-auto">{{ value }}</div>
        <div class="text-caption text-medium-emphasis">
          {{ t('operationCore.dashboards.totalRecords') }}
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.oc-dash-summary {
  min-height: 132px;
}
</style>
