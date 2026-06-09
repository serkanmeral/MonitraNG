<script setup lang="ts">
import { computed } from 'vue';
import type { SurfaceTimePreset } from '@/utils/widgets/surfaceTimeRange';

const props = defineProps<{
  timePreset: SurfaceTimePreset;
  severity: number | null;
  workspaceId: string;
  refreshSeconds: number;
  loading?: boolean;
  /** MO workspace dashboard'ları için; SIEM gibi yüzeylerde false */
  showWorkspaceId?: boolean;
  t?: (key: string) => string;
}>();

const showWorkspace = computed(() => props.showWorkspaceId !== false);

const emit = defineEmits<{
  'update:timePreset': [value: SurfaceTimePreset];
  'update:severity': [value: number | null];
  'update:workspaceId': [value: string];
  'update:refreshSeconds': [value: number];
  refresh: [];
}>();

const lbl = (key: string) => {
  const raw = props.t?.(`dashboards.surface.${key}`) ?? key;
  if (typeof raw === 'string') return raw;
  return key;
};

const timeOptions = computed(() =>
  (['1h', '6h', '24h', '7d'] as SurfaceTimePreset[]).map((value) => ({
    value,
    title: lbl(`timePreset.${value}`),
  })),
);

const severityOptions = computed(() => [
  { value: null, title: lbl('severityAll') },
  { value: 3, title: lbl('severityHigh') },
  { value: 4, title: lbl('severityCritical') },
]);

const refreshOptions = computed(() => [
  { value: 0, title: lbl('refreshOff') },
  { value: 30, title: lbl('refresh30s') },
  { value: 60, title: lbl('refresh1m') },
  { value: 300, title: lbl('refresh5m') },
]);
</script>

<template>
  <v-card variant="outlined" class="pa-2 dashboard-surface-toolbar">
    <div class="d-flex flex-wrap align-center ga-2">
      <v-icon size="20" color="primary">mdi-tune-variant</v-icon>
      <span class="text-body-2 text-medium-emphasis d-none d-sm-inline">{{ lbl('title') }}</span>

      <v-select
        :model-value="timePreset"
        :items="timeOptions"
        item-title="title"
        item-value="value"
        :label="lbl('timeRange')"
        variant="outlined"
        density="compact"
        hide-details
        style="min-width: 120px; max-width: 140px;"
        @update:model-value="emit('update:timePreset', $event)"
      />

      <v-select
        :model-value="severity"
        :items="severityOptions"
        item-title="title"
        item-value="value"
        :label="lbl('severity')"
        variant="outlined"
        density="compact"
        hide-details
        clearable
        style="min-width: 130px; max-width: 160px;"
        @update:model-value="emit('update:severity', $event)"
      />

      <v-text-field
        v-if="showWorkspace"
        :model-value="workspaceId"
        :label="lbl('workspaceId')"
        :placeholder="lbl('workspaceIdPlaceholder')"
        variant="outlined"
        density="compact"
        hide-details
        clearable
        style="min-width: 180px; max-width: 240px;"
        @update:model-value="emit('update:workspaceId', $event)"
      />

      <v-select
        :model-value="refreshSeconds"
        :items="refreshOptions"
        item-title="title"
        item-value="value"
        :label="lbl('autoRefresh')"
        variant="outlined"
        density="compact"
        hide-details
        style="min-width: 120px; max-width: 140px;"
        @update:model-value="emit('update:refreshSeconds', $event)"
      />

      <v-btn
        icon
        variant="text"
        size="small"
        :loading="loading"
        @click="emit('refresh')"
      >
        <v-icon>mdi-refresh</v-icon>
        <v-tooltip activator="parent" location="bottom">{{ lbl('refreshNow') }}</v-tooltip>
      </v-btn>
    </div>
  </v-card>
</template>

<style scoped>
.dashboard-surface-toolbar {
  flex: 1;
  min-width: 280px;
}
</style>
