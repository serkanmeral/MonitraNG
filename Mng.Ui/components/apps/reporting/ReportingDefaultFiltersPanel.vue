<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import AfListFilters from '@/components/apps/automated-forms/AfListFilters.vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AfFilterColumn, AfListFilter } from '@/utils/afListFilters';
import { cloneAfListFilters } from '@/utils/reportingDefaultFilters';

const props = defineProps<{
  defaultFilters: AfListFilter[];
  columns: AfFilterColumn[];
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:defaultFilters': [AfListFilter[]];
  reset: [];
}>();

const { t } = useAppI18n();

const panelKey = ref(0);

const hasFilterableColumns = computed(() => props.columns.length > 0);

watch(
  () => props.columns.map((c) => c.key).join('|'),
  () => {
    panelKey.value += 1;
  }
);

function onFiltersUpdate(filters: AfListFilter[]) {
  emit('update:defaultFilters', filters);
}

function clearAll() {
  emit('update:defaultFilters', []);
  panelKey.value += 1;
}

function resetPanel() {
  emit('reset');
  panelKey.value += 1;
}
</script>

<template>
  <div>
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      {{ t('reporting.defaultFilters.hint') }}
    </v-alert>

    <div v-if="hasFilterableColumns && !disabled" class="d-flex justify-end ga-2 mb-2">
      <v-btn
        v-if="defaultFilters.length"
        size="small"
        variant="text"
        @click="clearAll"
      >
        {{ t('reporting.defaultFilters.clear') }}
      </v-btn>
      <v-btn size="small" variant="text" @click="resetPanel">
        {{ t('reporting.defaultFilters.reset') }}
      </v-btn>
    </div>

    <AfListFilters
      v-if="hasFilterableColumns && !disabled"
      :key="panelKey"
      :columns="columns"
      :initial-filters="cloneAfListFilters(defaultFilters)"
      initial-panel-open
      @update:filters="onFiltersUpdate"
    />

    <v-alert v-else-if="disabled" type="warning" variant="tonal" density="compact">
      {{ t('reporting.defaultFilters.noSchema') }}
    </v-alert>

    <v-alert v-else type="warning" variant="tonal" density="compact">
      {{ t('reporting.defaultFilters.noFilterableColumns') }}
    </v-alert>
  </div>
</template>
