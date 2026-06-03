<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AlarmSummary } from '@/types/apps/alarm';
import { alarmListOpen } from '@/services/alarmService';

const { t, locale } = useAppI18n();

const loading = ref(true);
const errorLocal = ref<string | null>(null);
const rows = ref<AlarmSummary[]>([]);
const total = ref(0);

const headers = computed(() => [
  { title: t('operationCore.adminAlarms.colSeverity'), key: 'severity', sortable: true },
  { title: t('operationCore.adminAlarms.colStatus'), key: 'status', sortable: true },
  { title: t('operationCore.adminAlarms.colDedupKey'), key: 'dedupKey', sortable: false },
  { title: t('operationCore.adminAlarms.colCount'), key: 'count', sortable: true },
  { title: t('operationCore.adminAlarms.colFirstSeen'), key: 'firstSeenAt', sortable: true },
  { title: t('operationCore.adminAlarms.colLastSeen'), key: 'lastSeenAt', sortable: true },
]);

function formatDate(value?: string | null): string {
  if (!value) return '—';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'medium',
    }).format(new Date(value));
  } catch {
    return value;
  }
}

function severityColor(severity: number): string {
  if (severity >= 8) return 'error';
  if (severity >= 5) return 'warning';
  return 'info';
}

function statusColor(status: AlarmSummary['status']): string {
  if (status === 'Active') return 'error';
  if (status === 'Acknowledged') return 'warning';
  if (status === 'Resolved') return 'success';
  return 'default';
}

async function loadRows() {
  loading.value = true;
  errorLocal.value = null;
  try {
    const res = await alarmListOpen({ openOnly: true, limit: 100 });
    rows.value = res.items;
    total.value = res.total;
  } catch (e: unknown) {
    errorLocal.value = e instanceof Error ? e.message : t('operationCore.adminAlarms.loadError');
    rows.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadRows();
});
</script>

<template>
  <div>
    <v-alert v-if="errorLocal" type="error" variant="tonal" class="mb-4" closable @click:close="errorLocal = null">
      {{ errorLocal }}
    </v-alert>

    <div class="d-flex flex-wrap align-center gap-3 mb-4">
      <v-chip variant="tonal" color="primary">
        {{ t('operationCore.adminAlarms.statTotal', { count: total }) }}
      </v-chip>
      <v-spacer />
      <v-btn variant="tonal" prepend-icon="mdi-refresh" :loading="loading" @click="loadRows">
        {{ t('operationCore.adminAlarms.refresh') }}
      </v-btn>
    </div>

    <v-data-table
      :headers="headers"
      :items="rows"
      :loading="loading"
      item-value="id"
      class="rounded-lg"
      density="comfortable"
    >
      <template #item.severity="{ item }">
        <v-chip size="small" :color="severityColor(item.severity)" variant="flat">
          {{ item.severity }}
        </v-chip>
      </template>
      <template #item.status="{ item }">
        <v-chip size="small" :color="statusColor(item.status)" variant="tonal">
          {{ item.status }}
        </v-chip>
      </template>
      <template #item.dedupKey="{ item }">
        <span class="text-body-2">{{ item.dedupKey }}</span>
      </template>
      <template #item.firstSeenAt="{ item }">
        {{ formatDate(item.firstSeenAt) }}
      </template>
      <template #item.lastSeenAt="{ item }">
        {{ formatDate(item.lastSeenAt) }}
      </template>
      <template #no-data>
        <div class="text-center py-8 text-medium-emphasis">
          {{ t('operationCore.adminAlarms.empty') }}
        </div>
      </template>
    </v-data-table>
  </div>
</template>
