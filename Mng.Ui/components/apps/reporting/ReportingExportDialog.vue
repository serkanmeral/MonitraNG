<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { ReportingExportFormat } from '@/utils/reportingExport';
import { REPORTING_EXPORT_SOFT_CAP } from '@/utils/reportingExportFetch';

export interface ReportingExportColumnOption {
  key: string;
  title: string;
}

const props = withDefaults(
  defineProps<{
    modelValue: boolean;
    columns: ReportingExportColumnOption[];
    estimatedTotal: number;
    softCap?: number;
    exporting?: boolean;
  }>(),
  {
    softCap: REPORTING_EXPORT_SOFT_CAP,
    exporting: false,
  }
);

const emit = defineEmits<{
  'update:modelValue': [boolean];
  confirm: [
    {
      format: ReportingExportFormat;
      columnKeys: string[];
    },
  ];
}>();

const { t } = useAppI18n();

const format = ref<ReportingExportFormat>('xlsx');
const selectedKeys = ref<string[]>([]);
const overCapConfirmed = ref(false);

const dialog = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const overCap = computed(() => props.estimatedTotal > props.softCap);

const formatItems = computed(() => [
  { value: 'xlsx' as const, title: t('reporting.export.formatXlsx') },
  { value: 'csv' as const, title: t('reporting.export.formatCsv') },
]);

const canConfirm = computed(() => {
  if (!selectedKeys.value.length) return false;
  if (overCap.value && !overCapConfirmed.value) return false;
  return true;
});

watch(
  () => [props.modelValue, props.columns] as const,
  ([open, cols]) => {
    if (!open) return;
    format.value = 'xlsx';
    selectedKeys.value = cols.map((c) => c.key);
    overCapConfirmed.value = false;
  }
);

function selectAll() {
  selectedKeys.value = props.columns.map((c) => c.key);
}

function selectNone() {
  selectedKeys.value = [];
}

function onConfirm() {
  if (!canConfirm.value) return;
  emit('confirm', {
    format: format.value,
    columnKeys: [...selectedKeys.value],
  });
}
</script>

<template>
  <v-dialog v-model="dialog" max-width="520" :persistent="exporting">
    <v-card>
      <v-card-title>{{ t('reporting.export.title') }}</v-card-title>
      <v-card-text>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ t('reporting.export.hint', { count: estimatedTotal, cap: softCap }) }}
        </p>

        <v-select
          v-model="format"
          :items="formatItems"
          item-title="title"
          item-value="value"
          :label="t('reporting.export.format')"
          density="compact"
          hide-details
          class="mb-4"
          :disabled="exporting"
        />

        <div class="d-flex align-center justify-space-between mb-2">
          <span class="text-subtitle-2">{{ t('reporting.export.columns') }}</span>
          <div class="d-flex ga-1">
            <v-btn size="x-small" variant="text" class="text-none" :disabled="exporting" @click="selectAll">
              {{ t('reporting.export.selectAll') }}
            </v-btn>
            <v-btn size="x-small" variant="text" class="text-none" :disabled="exporting" @click="selectNone">
              {{ t('reporting.export.selectNone') }}
            </v-btn>
          </div>
        </div>

        <div class="reporting-export-columns border rounded pa-2 mb-3">
          <v-checkbox
            v-for="col in columns"
            :key="col.key"
            v-model="selectedKeys"
            :label="col.title"
            :value="col.key"
            density="compact"
            hide-details
            :disabled="exporting"
          />
        </div>

        <v-alert v-if="overCap" type="warning" variant="tonal" density="compact" class="mb-2">
          {{ t('reporting.export.overCapWarning', { count: estimatedTotal, cap: softCap }) }}
          <v-checkbox
            v-model="overCapConfirmed"
            class="mt-2"
            density="compact"
            hide-details
            :label="t('reporting.export.overCapConfirm', { cap: softCap })"
            :disabled="exporting"
          />
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" :disabled="exporting" @click="dialog = false">
          {{ t('reporting.actions.cancel') }}
        </v-btn>
        <v-btn color="primary" :loading="exporting" :disabled="!canConfirm || exporting" @click="onConfirm">
          {{ t('reporting.export.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.reporting-export-columns {
  max-height: 240px;
  overflow-y: auto;
}
</style>
