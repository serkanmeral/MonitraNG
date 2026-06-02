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

// Widget'a özel renk/ikon meta yok → key'den kararlı bir aksan rengi ve makul bir ikon türet.
const PALETTE = ['primary', 'success', 'info', 'warning', 'secondary', 'error'] as const;

const accent = computed(() => {
  const key = props.widget.key || props.widget.title || '';
  let hash = 0;
  for (let i = 0; i < key.length; i++) hash = (hash * 31 + key.charCodeAt(i)) >>> 0;
  return PALETTE[hash % PALETTE.length];
});

const icon = computed(() => {
  const k = `${props.widget.key} ${props.widget.title}`.toLowerCase();
  if (k.includes('sla') || k.includes('breach') || k.includes('ihlal')) return 'mdi-alarm-light-outline';
  if (k.includes('progress') || k.includes('devam')) return 'mdi-progress-clock';
  if (k.includes('open') || k.includes('acik') || k.includes('açık')) return 'mdi-folder-open-outline';
  if (k.includes('done') || k.includes('closed') || k.includes('kapal')) return 'mdi-check-circle-outline';
  if (k.includes('assigned') || k.includes('atan')) return 'mdi-account-check-outline';
  return 'mdi-counter';
});
</script>

<template>
  <v-card variant="flat" class="rounded-lg h-100 oc-dash-summary" :class="`oc-accent-${accent}`">
    <v-card-text class="pa-4 d-flex flex-column h-100">
      <div class="d-flex align-center justify-space-between mb-3">
        <span class="text-overline text-medium-emphasis text-truncate oc-summary-title">{{ title }}</span>
        <div class="oc-summary-badge d-flex align-center justify-center flex-shrink-0">
          <v-icon :icon="icon" size="20" />
        </div>
      </div>

      <template v-if="failed">
        <div class="d-flex align-center ga-1 mt-auto text-error">
          <v-icon icon="mdi-alert-circle-outline" size="18" />
          <span class="text-caption">
            {{ execution?.errorMessage || t('operationCore.dashboards.widgetError') }}
          </span>
        </div>
      </template>
      <template v-else>
        <div class="d-flex align-end ga-2 mt-auto">
          <span class="oc-summary-value">{{ value }}</span>
          <span class="text-body-2 text-medium-emphasis pb-1">
            {{ t('operationCore.dashboards.totalRecords') }}
          </span>
        </div>
      </template>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.oc-dash-summary {
  min-height: 132px;
  position: relative;
  overflow: hidden;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  transition: box-shadow 0.18s ease, transform 0.18s ease;
}
.oc-dash-summary::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 4px;
  background: rgb(var(--oc-accent));
}
.oc-dash-summary:hover {
  box-shadow: 0 6px 20px rgba(var(--oc-accent), 0.18);
  transform: translateY(-2px);
}
.oc-summary-title {
  letter-spacing: 0.6px;
  line-height: 1.2;
}
.oc-summary-badge {
  width: 38px;
  height: 38px;
  border-radius: 10px;
  background: rgba(var(--oc-accent), 0.14);
  color: rgb(var(--oc-accent));
}
.oc-summary-value {
  font-size: 2.4rem;
  font-weight: 800;
  line-height: 1;
  color: rgb(var(--oc-accent));
}

.oc-accent-primary {
  --oc-accent: var(--v-theme-primary);
}
.oc-accent-success {
  --oc-accent: var(--v-theme-success);
}
.oc-accent-info {
  --oc-accent: var(--v-theme-info);
}
.oc-accent-warning {
  --oc-accent: var(--v-theme-warning);
}
.oc-accent-error {
  --oc-accent: var(--v-theme-error);
}
.oc-accent-secondary {
  --oc-accent: var(--v-theme-secondary);
}
</style>
