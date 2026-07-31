<script setup lang="ts">
import { computed } from 'vue';
import type { HostAnalyticsKpis } from '@/composables/useSiemHostAnalytics';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  kpis: HostAnalyticsKpis;
  loading?: boolean;
}>();

const { t } = useAppI18n();

function formatAge(sec: number | null): string {
  if (sec == null) return '—';
  if (sec < 60) return t('siemCenter.hostDashboard.ageSeconds', { n: sec });
  const min = Math.round(sec / 60);
  if (min < 60) return t('siemCenter.hostDashboard.ageMinutes', { n: min });
  const hr = Math.floor(min / 60);
  const rem = min % 60;
  return rem
    ? t('siemCenter.hostDashboard.ageHoursMinutes', { h: hr, m: rem })
    : t('siemCenter.hostDashboard.ageHours', { n: hr });
}

function heartbeatTone(sec: number | null): string {
  if (sec == null) return 'warning';
  if (sec <= 120) return 'success';
  if (sec <= 600) return 'warning';
  return 'error';
}

function cpuTone(v: number | null): string {
  if (v == null) return 'default';
  if (v >= 90) return 'error';
  if (v >= 75) return 'warning';
  return 'success';
}

function diskTone(v: number | null): string {
  if (v == null) return 'default';
  if (v >= 90) return 'error';
  if (v >= 80) return 'warning';
  return 'success';
}

const cards = computed(() => {
  const k = props.kpis;
  return [
    {
      key: 'heartbeat',
      title: t('siemCenter.hostDashboard.kpiHeartbeat'),
      value: formatAge(k.heartbeatAgeSec),
      hint: t('siemCenter.hostDashboard.kpiHeartbeatHint'),
      color: heartbeatTone(k.heartbeatAgeSec),
      icon: 'mdi-heart-pulse',
    },
    {
      key: 'cpu',
      title: t('siemCenter.hostDashboard.kpiCpu'),
      value: k.cpuLast != null ? `${k.cpuLast}%` : '—',
      hint:
        k.cpuAvg != null && k.cpuMax != null
          ? t('siemCenter.hostDashboard.kpiCpuHint', { avg: k.cpuAvg, max: k.cpuMax })
          : t('siemCenter.hostDashboard.kpiNoData'),
      color: k.cpuLast == null ? 'primary' : cpuTone(k.cpuLast),
      icon: 'mdi-cpu-64-bit',
    },
    {
      key: 'memory',
      title: t('siemCenter.hostDashboard.kpiMemory'),
      value: k.memoryAvailableMb != null ? `${k.memoryAvailableMb} MB` : '—',
      hint: t('siemCenter.hostDashboard.kpiMemoryHint'),
      color: 'info',
      icon: 'mdi-memory',
    },
    {
      key: 'disk',
      title: t('siemCenter.hostDashboard.kpiDisk'),
      value: k.diskCriticalUsedPct != null ? `${k.diskCriticalUsedPct}%` : '—',
      hint: k.diskCriticalVolume
        ? t('siemCenter.hostDashboard.kpiDiskHint', { volume: k.diskCriticalVolume })
        : t('siemCenter.hostDashboard.kpiNoData'),
      color: k.diskCriticalUsedPct == null ? 'primary' : diskTone(k.diskCriticalUsedPct),
      icon: 'mdi-harddisk',
    },
    {
      key: 'watch',
      title: t('siemCenter.hostDashboard.kpiWatch'),
      value: k.watchUnhealthy != null ? String(k.watchUnhealthy) : '—',
      hint:
        k.watchHealthy != null
          ? t('siemCenter.hostDashboard.kpiWatchHint', { healthy: k.watchHealthy })
          : t('siemCenter.hostDashboard.kpiNoData'),
      color: (k.watchUnhealthy ?? 0) > 0 ? 'warning' : 'success',
      icon: 'mdi-eye-check-outline',
    },
    {
      key: 'eventlog',
      title: t('siemCenter.hostDashboard.kpiEventLog'),
      value: String(k.eventLogErrors),
      hint: t('siemCenter.hostDashboard.kpiEventLogHint', {
        warn: k.eventLogWarnings,
        total: k.eventLogTotal,
      }),
      color: k.eventLogErrors > 0 ? 'error' : k.eventLogWarnings > 0 ? 'warning' : 'success',
      icon: 'mdi-file-document-alert-outline',
    },
  ];
});
</script>

<template>
  <v-row dense>
    <v-col v-for="card in cards" :key="card.key" cols="12" sm="6" md="4" lg="2">
      <v-card variant="outlined" class="rounded-lg pa-3 h-100 host-kpi-card">
        <div class="d-flex align-start justify-space-between ga-2 mb-2">
          <div class="text-caption text-medium-emphasis">{{ card.title }}</div>
          <v-icon :icon="card.icon" size="18" :color="card.color" />
        </div>
        <v-skeleton-loader v-if="loading" type="text" width="60%" />
        <template v-else>
          <div class="text-h6 font-weight-bold mb-1" :class="`text-${card.color}`">
            {{ card.value }}
          </div>
          <div class="text-caption text-medium-emphasis">{{ card.hint }}</div>
        </template>
      </v-card>
    </v-col>
  </v-row>
</template>

<style scoped>
.host-kpi-card {
  min-height: 108px;
}
</style>
