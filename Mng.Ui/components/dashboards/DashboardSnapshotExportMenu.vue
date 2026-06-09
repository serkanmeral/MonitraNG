<script setup lang="ts">
import { computed } from 'vue';
import type { Dashboard } from '@/stores/apps/dashboard';
import type { SurfaceContext } from '@/types/apps/widgetManifest';
import type { WidgetDataResponse } from '@/services/widgetDataService';
import { useDashboardSnapshotExport } from '@/composables/useDashboardSnapshotExport';

const props = defineProps<{
  dashboard: Dashboard;
  widgetIds: string[];
  context: SurfaceContext;
  dataByWidgetId: Map<string, WidgetDataResponse>;
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const { exporting, exportError, exportSnapshotJson, exportAllCsv } = useDashboardSnapshotExport();

function clearExportError() {
  exportError.value = null;
}

const lbl = (key: string) => props.t?.(`dashboards.export.${key}`) ?? key;

const exportInput = computed(() => ({
  dashboard: props.dashboard,
  widgetIds: props.widgetIds,
  context: props.context,
  dataByWidgetId: props.dataByWidgetId,
}));

const errorMessage = computed(() => {
  if (!exportError.value) return '';
  return lbl(exportError.value);
});
</script>

<template>
  <div>
    <v-menu location="bottom end">
      <template #activator="{ props: menuProps }">
        <v-btn
          v-bind="menuProps"
          variant="outlined"
          prepend-icon="mdi-export-variant"
          :loading="exporting"
          :disabled="disabled || !widgetIds.length"
          class="text-none"
        >
          {{ lbl('title') }}
        </v-btn>
      </template>
      <v-list density="compact" min-width="220">
        <v-list-item
          prepend-icon="mdi-camera"
          :title="lbl('snapshotJson')"
          :subtitle="lbl('snapshotJsonHint')"
          @click="exportSnapshotJson(exportInput)"
        />
        <v-list-item
          prepend-icon="mdi-file-delimited"
          :title="lbl('csvAll')"
          :subtitle="lbl('csvAllHint')"
          @click="exportAllCsv(exportInput)"
        />
      </v-list>
    </v-menu>

    <v-snackbar
      :model-value="!!exportError"
      color="warning"
      timeout="4000"
      @update:model-value="(v) => { if (!v) clearExportError(); }"
    >
      {{ errorMessage }}
    </v-snackbar>
  </div>
</template>
