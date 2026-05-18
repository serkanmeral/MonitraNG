<script setup lang="ts">
import { watch, ref, computed } from 'vue';
import { fetchFromDataGateway } from '@/services/apiService';
import type { MapPosition } from './MonitoringMapView.vue';

const METRICS_DATASET = 'mon_metrics';
const ASSETS_DATASET = 'mon_assets';

const props = defineProps<{
  open: boolean;
  /** Seçilen konum (assetId zorunlu; name, lat, lon, updatedAt modal başlık/özet için) */
  position: MapPosition | null;
  mt?: (key: string, fallback: string) => string;
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
}>();

function t(key: string, fallback: string): string {
  return props.mt?.(key, fallback) ?? fallback;
}

const asset = ref<Record<string, unknown> | null>(null);
const metricsRows = ref<Array<{ code: string; value: unknown; timestamp: string }>>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

const title = computed(() => {
  const p = props.position;
  if (!p) return t('monitoring.map.assetDetails', 'Varlık detayı');
  return p.name ?? p.trainId ?? p.assetId ?? t('monitoring.map.assetDetails', 'Varlık detayı');
});

const subtitle = computed(() => {
  const p = props.position;
  if (!p) return '';
  const parts: string[] = [];
  if (p.lat != null && p.lon != null) parts.push(`${Number(p.lat).toFixed(5)}, ${Number(p.lon).toFixed(5)}`);
  if (p.routeId) parts.push(p.routeId);
  if (p.updatedAt) parts.push(new Date(p.updatedAt).toLocaleString('tr-TR'));
  return parts.join(' · ');
});

const assetName = computed(() => (asset.value && typeof asset.value === 'object' && 'name' in asset.value ? String(asset.value.name) : '—'));
const assetType = computed(() => (asset.value && typeof asset.value === 'object' && 'type' in asset.value ? String(asset.value.type) : ''));
const assetStatus = computed(() => (asset.value && typeof asset.value === 'object' && 'status' in asset.value ? String(asset.value.status) : ''));

async function loadAssetAndMetrics() {
  const assetId = props.position?.assetId;
  if (!assetId) {
    asset.value = null;
    metricsRows.value = [];
    return;
  }
  loading.value = true;
  loadError.value = null;
  try {
    const [assetRes, metricsRes] = await Promise.all([
      fetchFromDataGateway(`/api/v1/data/${ASSETS_DATASET}/${encodeURIComponent(assetId)}`).catch(() => null),
      fetchFromDataGateway(
        `/api/v1/data/${METRICS_DATASET}?filter=${encodeURIComponent(`meta.assetId:eq:${assetId}`)}&sort=-timestamp&limit=100`
      ),
    ]);

    asset.value = assetRes && typeof assetRes === 'object' && !Array.isArray(assetRes) ? assetRes : null;

    const raw = Array.isArray(metricsRes) ? metricsRes : metricsRes?.data ?? metricsRes?.items ?? [];
    const byCode = new Map<string, { value: unknown; timestamp: string }>();
    for (const row of raw) {
      const code = (row.meta?.collectibleCode as string) ?? '—';
      if (!byCode.has(code)) byCode.set(code, { value: row.value, timestamp: row.timestamp ?? '' });
    }
    metricsRows.value = Array.from(byCode.entries()).map(([code, v]) => ({ code, value: v.value, timestamp: v.timestamp }));
  } catch (e: any) {
    loadError.value = e?.data?.errorDescription ?? e?.message ?? String(e);
    asset.value = null;
    metricsRows.value = [];
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.open, props.position?.assetId],
  () => {
    if (props.open && props.position?.assetId) loadAssetAndMetrics();
    else if (!props.open) {
      asset.value = null;
      metricsRows.value = [];
      loadError.value = null;
    }
  },
  { immediate: true }
);

function close() {
  emit('update:open', false);
}

function formatMetricValue(value: unknown): string {
  if (value == null) return '—';
  if (typeof value === 'number') return Number.isInteger(value) ? String(value) : value.toFixed(4);
  const s = String(value);
  if (s.length <= 24) return s;
  if (/^\d{4}-\d{2}-\d{2}T[\d:.]+Z?$/i.test(s)) {
    try {
      return new Date(s).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
    } catch (_) {
      return s.slice(0, 20) + '…';
    }
  }
  return s.slice(0, 22) + '…';
}

function formatMetricTime(timestamp: string): string {
  if (!timestamp) return '—';
  try {
    return new Date(timestamp).toLocaleString('tr-TR', { dateStyle: 'short', timeStyle: 'short' });
  } catch (_) {
    return timestamp.length > 16 ? timestamp.slice(0, 16) + '…' : timestamp;
  }
}
</script>

<template>
  <v-dialog
    :model-value="open"
    max-width="400"
    persistent
    transition="dialog-transition"
    content-class="monitoring-asset-modal-dialog"
    @update:model-value="emit('update:open', $event)"
  >
    <v-card class="monitoring-asset-modal-card" elevation="8" rounded="lg">
      <!-- Başlık: ikon + başlık + kapat -->
      <div class="modal-header">
        <div class="modal-header-content">
          <div class="modal-header-icon">
            <span class="modal-header-icon-inner">🚂</span>
          </div>
          <div class="modal-header-text">
            <h2 class="modal-title">{{ title }}</h2>
            <p v-if="subtitle" class="modal-subtitle">{{ subtitle }}</p>
          </div>
        </div>
        <v-btn
          icon
          variant="text"
          size="small"
          class="modal-close-btn"
          aria-label="Kapat"
          @click="close"
        >
          <v-icon size="20">mdi-close</v-icon>
        </v-btn>
      </div>

      <v-divider />

      <v-card-text class="modal-body modal-body--scroll">
        <v-progress-linear v-if="loading" indeterminate color="primary" class="modal-progress" />

        <v-alert
          v-if="loadError"
          type="warning"
          variant="tonal"
          density="compact"
          class="modal-alert"
          closable
        >
          {{ loadError }}
        </v-alert>

        <template v-if="!loading && asset">
          <section class="modal-section">
            <h3 class="modal-section-title">{{ t('monitoring.map.assetDetails', 'Varlık bilgisi') }}</h3>
            <div class="info-grid">
              <div class="info-row">
                <span class="info-label">{{ t('monitoring.control.assetName', 'Varlık') }}</span>
                <span class="info-value">{{ assetName }}</span>
              </div>
              <div v-if="assetType" class="info-row">
                <span class="info-label">{{ t('monitoring.control.type', 'Tip') }}</span>
                <span class="info-value">{{ assetType }}</span>
              </div>
              <div v-if="assetStatus" class="info-row">
                <span class="info-label">{{ t('monitoring.control.status', 'Durum') }}</span>
                <span class="info-value">
                  <v-chip size="small" variant="tonal" color="primary">{{ assetStatus }}</v-chip>
                </span>
              </div>
            </div>
          </section>
        </template>

        <section class="modal-section">
          <h3 class="modal-section-title">{{ t('monitoring.map.latestMetrics', 'Son metrikler') }}</h3>
          <div v-if="metricsRows.length > 0" class="metrics-list">
            <div
              v-for="(row, idx) in metricsRows"
              :key="row.code"
              class="metrics-row"
              :class="{ 'metrics-row--alt': idx % 2 === 1 }"
            >
              <span class="metrics-code">{{ row.code }}</span>
              <span class="metrics-value">{{ formatMetricValue(row.value) }}</span>
              <span class="metrics-time">{{ formatMetricTime(row.timestamp) }}</span>
            </div>
          </div>
          <p v-else-if="!loading" class="modal-empty">
            {{ t('monitoring.map.noMetrics', 'Henüz metrik verisi yok.') }}
          </p>
        </section>
      </v-card-text>

      <v-divider />

      <v-card-actions class="modal-actions">
        <v-spacer />
        <v-btn color="primary" variant="flat" size="small" rounded="lg" @click="close">
          {{ t('monitoring.common.close', 'Kapat') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.monitoring-asset-modal-card {
  overflow: hidden;
}

.modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 16px 12px;
}

.modal-header-content {
  display: flex;
  align-items: flex-start;
  gap: 14px;
  min-width: 0;
}

.modal-header-icon {
  flex-shrink: 0;
  width: 44px;
  height: 44px;
  border-radius: 12px;
  background: rgba(var(--v-theme-primary), 0.15);
  display: flex;
  align-items: center;
  justify-content: center;
}

.modal-header-icon-inner {
  font-size: 1.5rem;
  line-height: 1;
}

.modal-header-text {
  min-width: 0;
}

.modal-title {
  font-size: 1.125rem;
  font-weight: 600;
  line-height: 1.3;
  margin: 0 0 4px 0;
  color: rgb(var(--v-theme-on-surface));
}

.modal-subtitle {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.7);
  margin: 0;
  line-height: 1.4;
}

.modal-close-btn {
  flex-shrink: 0;
}

.modal-body {
  padding: 12px 16px 14px;
}

.modal-body--scroll {
  max-height: min(60vh, 320px);
  overflow-y: auto;
  overflow-x: hidden;
}

.modal-progress {
  margin-bottom: 16px;
}

.modal-alert {
  margin-bottom: 16px;
}

.modal-section {
  margin-bottom: 20px;
}

.modal-section:last-child {
  margin-bottom: 0;
}

.modal-section-title {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: rgba(var(--v-theme-on-surface), 0.6);
  margin: 0 0 10px 0;
}

.info-grid {
  background: rgba(var(--v-theme-on-surface), 0.04);
  border-radius: 10px;
  padding: 12px 14px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.info-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 6px 0;
}

.info-row:not(:last-child) {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.info-label {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.65);
  flex-shrink: 0;
}

.info-value {
  font-size: 0.8125rem;
  font-weight: 500;
  text-align: right;
  min-width: 0;
  color: rgb(var(--v-theme-on-surface));
}

.metrics-list {
  border-radius: 10px;
  overflow: hidden;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
}

.metrics-row {
  display: grid;
  grid-template-columns: 1fr auto auto;
  gap: 10px;
  align-items: center;
  padding: 8px 12px;
  font-size: 0.8125rem;
}

.metrics-row--alt {
  background: rgba(var(--v-theme-on-surface), 0.03);
}

.metrics-code {
  font-weight: 500;
  color: rgba(var(--v-theme-on-surface), 0.85);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.metrics-value {
  font-weight: 600;
  color: rgb(var(--v-theme-primary));
  text-align: right;
  white-space: nowrap;
}

.metrics-time {
  font-size: 0.7rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
  white-space: nowrap;
}

.modal-empty {
  font-size: 0.8125rem;
  color: rgba(var(--v-theme-on-surface), 0.55);
  margin: 0;
  padding: 12px 0;
}

.modal-actions {
  padding: 10px 16px 12px;
}
</style>
