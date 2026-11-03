<script setup lang="ts">
import { ref, watch } from 'vue';
import type { ReportingExpandChildListConfig, ReportingExpandTimelineConfig } from '@/types/apps/reporting';
import { useAppI18n } from '@/composables/useAppI18n';
import { fetchReportingChildList } from '@/utils/reportingChildList';
import { reportingCellRaw } from '@/utils/reportingListConfig';
import { formatTrDisplayDate } from '@/utils/trDisplayDate';
import LinkedDatasetDocuments from '@/components/apps/automated-forms/LinkedDatasetDocuments.vue';

const props = defineProps<{
  parentRow: Record<string, unknown>;
  childList: ReportingExpandChildListConfig;
  timeline?: ReportingExpandTimelineConfig;
  active: boolean;
  reloadKey?: number;
  emptyMessage?: string;
}>();

const { t } = useAppI18n();
const loading = ref(false);
const errorMessage = ref('');
const rows = ref<Record<string, unknown>[]>([]);
const loadedFor = ref('');

function parentKey(): string {
  const id = props.parentRow.__dataId ?? props.parentRow.dataId ?? '';
  return String(id);
}

function olayOf(row: Record<string, unknown>): string {
  const field = props.timeline?.titleField || 'olay';
  const raw = row[field];
  return typeof raw === 'string' ? raw : reportingCellRaw(row, field);
}

function titleOf(row: Record<string, unknown>): string {
  const key = olayOf(row);
  return props.timeline?.titleMap?.[key] || key || t('reporting.expand.timelineUntitled');
}

function colorOf(row: Record<string, unknown>): string {
  return props.timeline?.colorMap?.[olayOf(row)] || 'secondary';
}

function whenOf(row: Record<string, unknown>): string {
  const field = props.timeline?.whenField || 'tarih';
  return formatTrDisplayDate(row[field]) || '';
}

function refKeyOf(row: Record<string, unknown>): string {
  const field = props.timeline?.refField || '';
  if (!field) return '';
  const raw = row[field];
  return typeof raw === 'string' ? raw.trim() : '';
}

function rowId(row: Record<string, unknown>): string {
  const id = row.__dataId ?? row.dataId ?? '';
  return String(id);
}

function detailOf(row: Record<string, unknown>): string {
  const parts: string[] = [];
  const personField = props.timeline?.personField || '';
  if (personField) {
    const person = reportingCellRaw(row, personField);
    if (person) parts.push(person);
  }
  for (const field of props.timeline?.placeFields ?? []) {
    const place = reportingCellRaw(row, field);
    if (place) parts.push(place);
  }
  const ref = refKeyOf(row);
  if (ref) parts.push(ref);
  return parts.join(' · ');
}

const documentRootId = () => props.timeline?.documents?.rootFolderId?.trim() || '';

async function load() {
  const key = `${parentKey()}::${props.reloadKey ?? 0}`;
  if (!props.active || !parentKey() || loadedFor.value === key) return;
  loading.value = true;
  errorMessage.value = '';
  try {
    const result = await fetchReportingChildList({
      parentRow: props.parentRow,
      childList: props.childList,
    });
    const whenField = props.timeline?.whenField || 'tarih';
    rows.value = result.rows.slice().sort((a, b) => {
      const left = typeof a[whenField] === 'string' ? String(a[whenField]) : '9999';
      const right = typeof b[whenField] === 'string' ? String(b[whenField]) : '9999';
      return left.localeCompare(right);
    });
    loadedFor.value = key;
  } catch (error: unknown) {
    rows.value = [];
    errorMessage.value = error instanceof Error ? error.message : t('reporting.expand.timelineLoadFailed');
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.active, parentKey(), props.reloadKey ?? 0] as const,
  () => {
    void load();
  },
  { immediate: true }
);
</script>

<template>
  <div>
    <div v-if="loading" class="d-flex justify-center py-6">
      <v-progress-circular indeterminate color="primary" size="28" />
    </div>
    <v-alert v-else-if="errorMessage" type="error" variant="tonal" density="compact">
      {{ errorMessage }}
    </v-alert>
    <p v-else-if="rows.length === 0" class="text-body-2 text-medium-emphasis mb-0">
      {{ emptyMessage?.trim() || t('reporting.expand.timelineEmpty') }}
    </p>
    <v-timeline v-else side="end" density="compact" align="start" truncate-line="both">
      <v-timeline-item
        v-for="(row, index) in rows"
        :key="String(row.__dataId ?? index)"
        :dot-color="colorOf(row)"
        size="x-small"
      >
        <div class="text-body-2 font-weight-medium">{{ titleOf(row) }}</div>
        <div v-if="whenOf(row)" class="text-caption text-medium-emphasis">{{ whenOf(row) }}</div>
        <div v-if="detailOf(row)" class="text-body-2">{{ detailOf(row) }}</div>
        <LinkedDatasetDocuments
          v-if="rowId(row) && documentRootId()"
          dense
          :dataset-name="childList.datasetName"
          :data-id="rowId(row)"
          :also-work-item-key="refKeyOf(row)"
          :can-edit="timeline?.documents?.allowEdit !== false"
          :root-folder-id="documentRootId()"
          :relation-types="[timeline?.documents?.relationType || 'evidence']"
        />
      </v-timeline-item>
    </v-timeline>
  </div>
</template>
