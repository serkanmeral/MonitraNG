<script setup lang="ts">
import { computed } from 'vue';
import type { Widget, WidgetDataResponse } from '@/stores/apps/widget';
import type { WidgetInteractions } from '@/utils/widgets/surfaceInteractions';
import { hasDrillDown } from '@/utils/widgets/surfaceInteractions';
import {
  alarmStatusChipColor,
  formatAlarmRowSummary,
  formatAlarmStatusLabel,
  formatRelativeTimeSimple,
  formatSeverityChipLabel,
  formatWidgetDateTime,
  severityChipColor,
} from '@/utils/widgets/widgetTableFormats';
import { useLocaleStore } from '@/stores/locale';

const props = defineProps<{
  widget: Widget;
  data?: WidgetDataResponse | null;
  t?: (key: string) => string;
  interactions?: WidgetInteractions | null;
}>();

const emit = defineEmits<{
  'row-activate': [row: Record<string, unknown>];
  'drill-down': [row: Record<string, unknown>];
}>();

const localeStore = useLocaleStore();

const config = computed(() => (props.widget.config ?? {}) as Record<string, unknown>);
const presentationStyle = computed(() =>
  typeof config.value.presentationStyle === 'string' ? config.value.presentationStyle : 'default',
);
const titleField = computed(() => (config.value.titleField as string | undefined) ?? 'title');
const subtitleField = computed(() => (config.value.subtitleField as string | undefined) ?? 'subtitle');
const timeField = computed(() => (config.value.timeField as string | undefined) ?? 'updatedAt');
const severityField = computed(() => config.value.severityField as string | undefined);
const statusField = computed(() => config.value.statusField as string | undefined);
const useAlarmSummary = computed(() => config.value.useAlarmSummary === true);
const clickable = computed(
  () => hasDrillDown(props.interactions ?? null) || !!props.interactions?.crossFilter,
);

const items = computed(() => {
  const rows = props.data?.data;
  return Array.isArray(rows) ? (rows as Record<string, unknown>[]) : [];
});

function fieldValue(row: Record<string, unknown>, key: string): string {
  const val = row[key];
  if (val == null) return '—';
  return String(val);
}

function rowTitle(row: Record<string, unknown>): string {
  if (useAlarmSummary.value) return formatAlarmRowSummary(row);
  return fieldValue(row, titleField.value);
}

function rowSubtitle(row: Record<string, unknown>): string {
  if (statusField.value && row[statusField.value] != null) {
    return formatAlarmStatusLabel(row[statusField.value]);
  }
  const sub = fieldValue(row, subtitleField.value);
  return sub === '—' ? '' : sub;
}

function onItemClick(row: Record<string, unknown>) {
  emit('row-activate', row);
}

const lbl = (key: string) => props.t?.(`widgets.listActivity.${key}`) ?? key;
</script>

<template>
  <v-card
    variant="outlined"
    class="list-activity-widget h-100"
    :class="{ 'list-activity-widget--inbox': presentationStyle === 'inbox' }"
  >
    <v-card-item v-if="widget.title" class="pb-0">
      <v-card-title class="text-h6">{{ widget.title }}</v-card-title>
    </v-card-item>
    <v-card-text class="pt-2">
      <div v-if="!items.length" class="text-center text-medium-emphasis py-8">
        <v-icon
          :icon="presentationStyle === 'inbox' ? 'mdi-bell-off-outline' : 'mdi-format-list-bulleted'"
          size="40"
          color="primary"
          class="mb-2 opacity-60"
        />
        <div>{{ lbl('noData') }}</div>
      </div>
      <v-list v-else density="compact" lines="two" class="py-0 rounded-lg">
        <v-list-item
          v-for="(row, idx) in items"
          :key="String(row.__dataId ?? row.id ?? idx)"
          :class="{ 'list-activity-item--clickable': clickable }"
          rounded="lg"
          class="mb-1"
          @click="clickable ? onItemClick(row) : undefined"
        >
          <template #prepend>
            <v-chip
              v-if="severityField && row[severityField] != null"
              size="small"
              :color="severityChipColor(row[severityField])"
              variant="flat"
              class="mr-2"
            >
              {{ formatSeverityChipLabel(row[severityField]) }}
            </v-chip>
            <v-avatar v-else color="primary" variant="tonal" size="36">
              <v-icon size="20">mdi-format-list-bulleted</v-icon>
            </v-avatar>
          </template>
          <v-list-item-title class="text-body-2 font-weight-medium">
            {{ rowTitle(row) }}
          </v-list-item-title>
          <v-list-item-subtitle class="text-caption">
            <span v-if="rowSubtitle(row)">{{ rowSubtitle(row) }}</span>
            <span v-if="timeField && row[timeField]" class="text-medium-emphasis">
              <span v-if="rowSubtitle(row)"> · </span>
              {{ formatWidgetDateTime(row[timeField]) }}
              <span class="ms-1">({{ formatRelativeTimeSimple(row[timeField], localeStore.locale) }})</span>
            </span>
          </v-list-item-subtitle>
          <template v-if="statusField && row[statusField] != null && severityField" #append>
            <v-chip size="x-small" :color="alarmStatusChipColor(row[statusField])" variant="tonal">
              {{ formatAlarmStatusLabel(row[statusField]) }}
            </v-chip>
          </template>
        </v-list-item>
      </v-list>
    </v-card-text>
  </v-card>
</template>

<style scoped>
.list-activity-item--clickable {
  cursor: pointer;
}
.list-activity-item--clickable:hover {
  background: rgba(var(--v-theme-on-surface), 0.04);
}
.list-activity-widget--inbox :deep(.v-list) {
  background: transparent;
}
</style>
