<script setup lang="ts">
import { computed, ref, watch, onMounted } from 'vue';
import { useTheme } from 'vuetify';
import type { OrganizationSelectedNode, OrganizationTreeNode } from '@/types/apps/organization';
import type { MonItem, MonAsset } from '@/types/apps/organization';
import { FolderIcon, DeviceDesktopIcon, ChartLineIcon, ChevronRightIcon } from 'vue-tabler-icons';
import OrganizationMapView from '@/components/apps/organization/OrganizationMapView.vue';
import OrganizationItemDetailModal from '@/components/apps/organization/OrganizationItemDetailModal.vue';
import { fetchFromDataGateway } from '@/services/apiService';
import { useAssetTypeDefinitionsStore } from '@/stores/apps/assetTypeDefinitions';

const theme = useTheme();

/** Chart tipi: auto | line | area | bar */
type ChartTypeOption = 'auto' | 'line' | 'area' | 'bar';
const CHART_STORAGE_KEY = 'monitoring-metric-chart-type';
const METRICS_TIME_RANGE_KEY = 'monitoring-metric-time-range';
const METRICS_LIMIT_KEY = 'monitoring-metric-limit';

const METRICS_DATASET = 'mon_metrics';

/** Zaman aralığı: dakika cinsinden veya null (tümü) */
type TimeRangeValue = number | null;
const TIME_RANGE_OPTIONS: { value: TimeRangeValue; labelKey: string }[] = [
  { value: 20, labelKey: 'monitoring.control.last20min' },
  { value: 60, labelKey: 'monitoring.control.last1h' },
  { value: 360, labelKey: 'monitoring.control.last6h' },
  { value: 1440, labelKey: 'monitoring.control.last1d' },
  { value: 10080, labelKey: 'monitoring.control.last7d' },
  { value: null, labelKey: 'monitoring.control.allTime' },
];

const props = defineProps<{
  selectedNode: OrganizationSelectedNode | null;
  itemChildCount?: number;
  assetChildCount?: number;
  treeNodes?: OrganizationTreeNode[];
  mt?: (key: string, fallback: string) => string;
}>();

const emit = defineEmits<{
  'marker-click': [itemId: string];
}>();

function t(key: string, fallback: string): string {
  return props.mt?.(key, fallback) ?? fallback;
}

const isEmpty = computed(() => !props.selectedNode);

const isItem = computed(() => props.selectedNode?.type === 'item');
const isAsset = computed(() => props.selectedNode?.type === 'asset');

const itemData = computed(() => (props.selectedNode?.type === 'item' ? (props.selectedNode.data as MonItem) : null));
const assetData = computed(() => (props.selectedNode?.type === 'asset' ? (props.selectedNode.data as MonAsset) : null));

const itemDetailModalOpen = ref(false);
const itemDetailModalItemId = ref<string | null>(null);

function onItemMapMarkerClick(item: { __dataId: string }) {
  itemDetailModalItemId.value = item.__dataId;
  itemDetailModalOpen.value = true;
  emit('marker-click', item.__dataId);
}

/** Item konumlu ise harita için MapItem; yoksa boş */
const itemMapData = computed(() => {
  const item = itemData.value;
  if (!item?.location || typeof item.location.lat !== 'number' || typeof item.location.lon !== 'number') return [];
  if (Number.isNaN(item.location.lat) || Number.isNaN(item.location.lon)) return [];
  return [{
    __dataId: item.__dataId ?? '',
    name: item.name ?? '',
    location: { lat: item.location.lat, lon: item.location.lon },
    description: item.description ?? null,
    childCount: props.itemChildCount,
  }];
});

const collectibleCodes = computed(() => {
  const a = assetData.value;
  if (!a?.collectible_config) return [];
  return (a.collectible_config as Array<{ code: string; enabled?: boolean }>)
    .filter((c) => c.enabled !== false)
    .map((c) => c.code);
});

const statusLabel = computed(() => {
  const s = assetData.value?.status;
  if (!s) return '—';
  const map: Record<string, string> = {
    active: t('monitoring.engines.statusActive', 'Aktif'),
    maintenance: t('monitoring.engines.statusMaintenance', 'Bakımda'),
    decommissioned: t('monitoring.control.decommissioned', 'Devre dışı'),
  };
  return map[s] ?? s;
});

const assetTypeDefinitionsStore = useAssetTypeDefinitionsStore();
const typeDisplayName = computed(() => {
  const typeId = assetData.value?.type;
  if (!typeId) return '—';
  const typeDef = assetTypeDefinitionsStore.types.find((t) => t.__dataId === typeId);
  return typeDef?.name ?? typeId;
});

onMounted(() => {
  if (assetTypeDefinitionsStore.types.length === 0) {
    assetTypeDefinitionsStore.loadTypes();
  }
});

/** DG üzerinden mon_metrics verisi (assetId ile filtre) */
const metricsRaw = ref<Array<{ timestamp?: string; meta?: { collectibleCode?: string }; value?: number | string }>>([]);
const metricsLoading = ref(false);
const metricsError = ref<string | null>(null);

/** collectibleCode bazında son değer (en son gelen kayıt) */
const latestByCode = computed(() => {
  const map = new Map<string, { value: number | string; timestamp: string }>();
  for (const row of metricsRaw.value) {
    const code = row.meta?.collectibleCode ?? '—';
    if (!map.has(code)) {
      map.set(code, { value: row.value ?? '—', timestamp: row.timestamp ?? '' });
    }
  }
  return Array.from(map.entries()).map(([code, v]) => ({ code, ...v }));
});

/** Seçilen metrik (tab) */
const selectedMetricCode = ref<string | null>(null);

/** Seçilen metriğe ait tüm veriler (zaman sıralı) */
const selectedMetricRows = computed(() => {
  const code = selectedMetricCode.value;
  if (!code) return [];
  return metricsRaw.value
    .filter((r) => (r.meta?.collectibleCode ?? '') === code)
    .map((r, i) => ({ _key: `${r.timestamp}-${i}`, timestamp: r.timestamp ?? '', value: r.value ?? '—' }));
});

function isNumericValue(v: unknown): v is number {
  if (typeof v === 'number') return !Number.isNaN(v);
  if (typeof v === 'string') {
    const n = Number(v);
    return v.trim() !== '' && !Number.isNaN(n) && Number.isFinite(n);
  }
  return false;
}

/** Numeric mi? (tüm değerler sayı veya sayıya çevrilebilir ise) */
const isMetricNumeric = computed(() => {
  const rows = selectedMetricRows.value;
  if (rows.length === 0) return false;
  return rows.every((r) => isNumericValue(r.value));
});

/** Otomatik chart tipi: code'a göre */
function detectChartType(code: string): 'line' | 'area' | 'bar' {
  const c = (code ?? '').toLowerCase();
  if (/temperature|voltage|power|humidity|current/.test(c)) return 'area';
  if (/\w*count\b|\w*status\b|outlet\w*/.test(c)) return 'bar';
  return 'line';
}

/** Chart tipi seçici (localStorage ile) */
function loadChartTypePref(): ChartTypeOption {
  if (typeof window === 'undefined') return 'auto';
  try {
    const v = localStorage.getItem(CHART_STORAGE_KEY) as ChartTypeOption | null;
    return v && ['auto', 'line', 'area', 'bar'].includes(v) ? v : 'auto';
  } catch {
    return 'auto';
  }
}
function saveChartTypePref(v: ChartTypeOption) {
  try {
    localStorage.setItem(CHART_STORAGE_KEY, v);
  } catch {}
}
function onChartTypeChange(v: unknown) {
  if (v && typeof v === 'string' && ['auto', 'line', 'area', 'bar'].includes(v)) {
    saveChartTypePref(v as ChartTypeOption);
  }
}

const chartTypePref = ref<ChartTypeOption>(loadChartTypePref());

const chartTypeItems = computed(() => [
  { title: t('monitoring.control.chartTypeAuto', 'Otomatik'), value: 'auto' as const },
  { title: t('monitoring.control.chartTypeLine', 'Çizgi'), value: 'line' as const },
  { title: t('monitoring.control.chartTypeArea', 'Alan'), value: 'area' as const },
  { title: t('monitoring.control.chartTypeBar', 'Çubuk'), value: 'bar' as const },
]);

/** Gerçek chart tipi: auto ise detectChartType, yoksa seçilen */
const effectiveChartType = computed((): 'line' | 'area' | 'bar' => {
  const code = selectedMetricCode.value ?? '';
  if (chartTypePref.value === 'auto') return detectChartType(code);
  return chartTypePref.value as 'line' | 'area' | 'bar';
});

/** Chart için veri: [[timestampMs, value], ...] */
const chartSeries = computed(() => {
  const rows = selectedMetricRows.value;
  const code = selectedMetricCode.value ?? '';
  if (!isMetricNumeric.value || rows.length === 0) return [];
  const data = rows
    .map((r) => {
      const ts = r.timestamp ? new Date(r.timestamp).getTime() : 0;
      const val = typeof r.value === 'number' ? r.value : Number(String(r.value)) || 0;
      return [ts, val];
    })
    .sort((a, b) => (a[0] as number) - (b[0] as number));
  return [{ name: code, data }];
});

const chartOptions = computed(() => {
  const isDark = theme.current.value.dark;
  const type = effectiveChartType.value;
  const opts: Record<string, unknown> = {
    chart: {
      type,
      height: 260,
      toolbar: { show: false },
      zoom: { enabled: true },
      fontFamily: 'inherit',
      foreColor: isDark ? '#a1aab2' : '#5a6a85',
    },
    stroke: { width: 2, curve: 'smooth' as const },
    dataLabels: { enabled: false },
    xaxis: {
      type: 'datetime' as const,
      labels: { datetimeUTC: false },
    },
    yaxis: { labels: { formatter: (v: number) => (Number.isInteger(v) ? String(v) : v.toFixed(2)) } },
    colors: ['#6366f1'],
    grid: {
      borderColor: isDark ? 'rgba(255,255,255,0.12)' : 'rgba(0,0,0,0.06)',
      strokeDashArray: 4,
    },
    tooltip: { theme: isDark ? 'dark' : 'light' },
  };
  if (type === 'bar') {
    opts.plotOptions = { bar: { horizontal: false, columnWidth: '60%', borderRadius: 4 } };
  }
  return opts;
});

const metricTableHeaders = computed(() => [
  { title: t('monitoring.control.timestamp', 'Zaman'), key: 'timestamp', sortable: true },
  { title: t('monitoring.control.value', 'Değer'), key: 'value', sortable: false },
]);

const slotTimestamp = 'item.timestamp';
const slotValue = 'item.value';

/** Tab listesi: collectibleCodes + gelen veride olup config'de olmayan kodlar */
const metricTabs = computed(() => {
  const fromConfig = new Set(collectibleCodes.value);
  const fromData = new Set(metricsRaw.value.map((r) => r.meta?.collectibleCode).filter(Boolean) as string[]);
  const all = new Set([...fromConfig, ...fromData]);
  return Array.from(all).sort();
});

function formatMetricTimestamp(ts: string): string {
  if (!ts) return '—';
  try {
    const d = new Date(ts);
    return Number.isNaN(d.getTime()) ? ts : d.toLocaleString('tr-TR');
  } catch {
    return ts;
  }
}

/** Zaman aralığı ve limit (localStorage ile) */
function loadTimeRangePref(): TimeRangeValue {
  if (typeof window === 'undefined') return null;
  try {
    const v = localStorage.getItem(METRICS_TIME_RANGE_KEY);
    if (v === 'null') return null;
    if (!v || v === '') return null;
    const n = parseInt(v, 10);
    return Number.isNaN(n) ? null : n;
  } catch {
    return null;
  }
}
function loadLimitPref(): number {
  if (typeof window === 'undefined') return 1000;
  try {
    const v = localStorage.getItem(METRICS_LIMIT_KEY);
    const n = parseInt(v ?? '1000', 10);
    return [50, 100, 200, 500, 1000].includes(n) ? n : 1000;
  } catch {
    return 1000;
  }
}

const metricsTimeRange = ref<TimeRangeValue>(loadTimeRangePref());
const metricsLimit = ref(loadLimitPref());

function saveMetricsPrefs() {
  try {
    localStorage.setItem(METRICS_TIME_RANGE_KEY, metricsTimeRange.value == null ? 'null' : String(metricsTimeRange.value));
    localStorage.setItem(METRICS_LIMIT_KEY, String(metricsLimit.value));
  } catch {}
}

const timeRangeItems = computed(() =>
  TIME_RANGE_OPTIONS.map((o) => ({ value: o.value, title: t(o.labelKey, String(o.value ?? 'Tümü')) }))
);
const limitItems = [50, 100, 200, 500, 1000].map((n) => ({ value: n, title: String(n) }));

/** Metrik verisi yükle. collectibleCode verilirse sadece o metrik çekilir (MongoDB'deki gibi). */
async function loadMetrics(assetId: string, collectibleCode?: string | null) {
  if (!assetId) return;
  metricsLoading.value = true;
  metricsError.value = null;
  try {
    const parts: string[] = [`meta.assetId:eq:${assetId}`];
    if (collectibleCode) {
      parts.push(`meta.collectibleCode:eq:${collectibleCode}`);
    }
    const range = metricsTimeRange.value;
    if (range != null && range > 0) {
      const since = new Date(Date.now() - range * 60 * 1000);
      parts.push(`timestamp:gte:${since.toISOString()}`);
    }
    const filter = parts.join(',');
    const limit = metricsLimit.value;
    const url = `/api/v1/data/${METRICS_DATASET}?filter=${encodeURIComponent(filter)}&sort=-timestamp&limit=${limit}`;
    const res = await fetchFromDataGateway(url);
    const arr = Array.isArray(res) ? res : res?.items ?? res?.data ?? [];
    metricsRaw.value = arr;
  } catch (e: any) {
    metricsError.value = e?.message ?? e?.data?.errorDescription ?? String(e);
    metricsRaw.value = [];
  } finally {
    metricsLoading.value = false;
  }
}

watch(
  () => assetData.value?.__dataId ?? null,
  (assetId) => {
    if (assetId) {
      loadMetrics(assetId);
    } else {
      metricsRaw.value = [];
      metricsError.value = null;
      selectedMetricCode.value = null;
    }
  },
  { immediate: true }
);

watch(selectedMetricCode, (code) => {
  const assetId = assetData.value?.__dataId;
  if (assetId && code) loadMetrics(assetId, code);
});

watch([metricsTimeRange, metricsLimit], () => {
  saveMetricsPrefs();
  const assetId = assetData.value?.__dataId;
  if (assetId) loadMetrics(assetId, selectedMetricCode.value);
});

watch(metricTabs, (tabs) => {
  if (tabs.length > 0 && !selectedMetricCode.value) {
    selectedMetricCode.value = tabs[0];
  } else if (tabs.length === 0) {
    selectedMetricCode.value = null;
  } else if (selectedMetricCode.value && !tabs.includes(selectedMetricCode.value)) {
    selectedMetricCode.value = tabs[0];
  }
}, { immediate: true });
</script>

<template>
  <div class="control-detail-panel">
    <!-- Boş durum -->
    <div v-if="isEmpty" class="d-flex flex-column align-center justify-center py-12 text-medium-emphasis">
      <DeviceDesktopIcon size="48" class="mb-3 opacity-50" />
      <p class="text-body-1 mb-1">{{ t('monitoring.control.selectAssetHint', 'Sol ağaçtan bir Item veya Asset seçin') }}</p>
      <p class="text-caption">{{ t('monitoring.control.selectAssetHintDetail', 'Asset seçildiğinde metrik verileri burada görüntülenecektir.') }}</p>
    </div>

    <!-- Item seçildi -->
    <template v-else-if="isItem && itemData">
      <div class="d-flex align-center gap-2 mb-4">
        <FolderIcon size="28" class="text-primary" />
        <div>
          <h3 class="text-h6 font-weight-bold">{{ itemData.name }}</h3>
          <span class="text-caption text-medium-emphasis">{{ t('monitoring.control.itemLabel', 'Item') }}</span>
        </div>
      </div>
      <v-divider class="mb-4" />
      <v-list density="compact" class="bg-transparent">
        <v-list-item v-if="itemData.description" :title="t('monitoring.control.description', 'Açıklama')" :subtitle="itemData.description" />
        <v-list-item v-if="itemData.kind" :title="t('monitoring.control.kind', 'Tür')" :subtitle="itemData.kind" />
        <v-list-item
          :title="t('monitoring.control.childCount', 'Alt öğe sayısı')"
          :subtitle="`${itemChildCount ?? 0} ${t('monitoring.control.itemsOrAssets', 'item/asset')}`"
        />
      </v-list>
      <div v-if="itemMapData.length > 0" class="item-map-section mt-4">
        <div class="text-caption text-medium-emphasis mb-2">{{ t('monitoring.control.location', 'Konum') }}</div>
        <OrganizationMapView
          :items="itemMapData"
          height="280px"
          :popup-hint="t('organization.mapModal.clickForDetails', 'Detaylar için tıklayın')"
          @marker-click="onItemMapMarkerClick"
        />
      </div>
      <OrganizationItemDetailModal
        v-if="(props.treeNodes?.length ?? 0) > 0"
        :open="itemDetailModalOpen"
        :item-id="itemDetailModalItemId"
        :tree-nodes="props.treeNodes ?? []"
        :mt="t"
        @update:open="itemDetailModalOpen = $event"
      />
      <v-alert type="info" variant="tonal" density="compact" class="mt-4">
        {{ t('monitoring.control.itemSelectHint', 'Metrik verileri için ağaçtan bir Asset seçin.') }}
      </v-alert>
    </template>

    <!-- Asset seçildi -->
    <template v-else-if="isAsset && assetData">
      <!-- Asset kartı: Başlık + Time Range + Limit -->
      <v-card variant="outlined" class="mb-4">
        <v-card-text class="pa-4">
          <div class="d-flex flex-wrap align-center gap-3">
            <div class="d-flex align-center gap-2">
              <DeviceDesktopIcon size="28" class="text-secondary" />
              <div>
                <h3 class="text-h6 font-weight-bold mb-0">{{ assetData.name }}</h3>
                <span class="text-caption text-medium-emphasis">{{ t('monitoring.control.assetLabel', 'Asset') }}</span>
              </div>
            </div>
            <div class="d-flex flex-wrap align-center gap-2">
              <v-chip size="small" :color="assetData.status === 'active' ? 'success' : assetData.status === 'maintenance' ? 'warning' : 'default'" variant="tonal">
                {{ statusLabel }}
              </v-chip>
              <v-chip size="small" variant="outlined">{{ typeDisplayName }}</v-chip>
            </div>
            <v-spacer />
            <div v-if="metricTabs.length > 0" class="d-flex flex-wrap align-center gap-2">
              <v-select
                v-model="metricsTimeRange"
                :items="timeRangeItems"
                item-title="title"
                item-value="value"
                density="compact"
                hide-details
                style="max-width: 160px"
                :label="t('monitoring.control.timeRange', 'Zaman aralığı')"
              />
              <v-select
                v-model="metricsLimit"
                :items="limitItems"
                item-title="title"
                item-value="value"
                density="compact"
                hide-details
                style="max-width: 100px"
                :label="t('monitoring.control.limit', 'Limit')"
              />
            </div>
          </div>
        </v-card-text>
        <template v-if="assetData.description">
          <v-divider />
          <v-card-text class="pt-2 pb-3">
            <div class="text-caption text-medium-emphasis">{{ t('monitoring.control.description', 'Açıklama') }}</div>
            <div class="text-body-2">{{ assetData.description }}</div>
          </v-card-text>
        </template>
      </v-card>

      <!-- Metrikler: Sol parametre listesi + Sağ veri alanı -->
      <div v-if="metricTabs.length > 0" class="metrics-split-layout">
        <v-row dense>
          <!-- Sol: Parametre listesi -->
          <v-col cols="12" sm="5" md="4" lg="3">
            <v-card variant="outlined" class="metric-params-card">
              <v-card-title class="py-3 text-subtitle-2 d-flex align-center">
                <ChartLineIcon size="20" class="mr-2 text-primary" />
                {{ t('monitoring.control.expectedMetrics', 'Beklenen metrikler') }}
              </v-card-title>
              <v-divider />
              <v-list class="metric-params-list py-0" density="compact">
                <v-list-item
                  v-for="code in metricTabs"
                  :key="code"
                  :active="selectedMetricCode === code"
                  :class="{ 'metric-param-selected': selectedMetricCode === code }"
                  class="metric-param-item"
                  rounded="lg"
                  @click="selectedMetricCode = code"
                >
                  <template #prepend>
                    <ChevronRightIcon v-if="selectedMetricCode === code" size="18" class="text-primary" />
                    <span v-else class="metric-param-dot" />
                  </template>
                  <v-list-item-title class="metric-param-name">{{ code }}</v-list-item-title>
                  <template #append>
                    <span
                      v-if="latestByCode.find((m) => m.code === code)"
                      class="metric-param-value"
                    >
                      {{ latestByCode.find((m) => m.code === code)?.value }}
                    </span>
                  </template>
                </v-list-item>
              </v-list>
            </v-card>
          </v-col>

          <!-- Sağ: Seçilen parametrenin verileri -->
          <v-col cols="12" sm="7" md="8" lg="9">
            <v-card variant="outlined" class="metric-data-card">
              <v-card-title class="d-flex align-center flex-wrap py-3 text-subtitle-2">
                <span v-if="selectedMetricCode" class="d-flex align-center">
                  <span class="metric-data-title">{{ selectedMetricCode }}</span>
                  <v-chip size="small" variant="tonal" color="primary" class="ml-2">
                    {{ selectedMetricRows.length }} {{ t('monitoring.control.records', 'kayıt') }}
                  </v-chip>
                </span>
                <span v-else class="text-medium-emphasis">
                  {{ t('monitoring.control.selectMetricHint', 'Sol listeden bir parametre seçin.') }}
                </span>
                <v-spacer />
                <v-select
                  v-if="selectedMetricCode && isMetricNumeric && selectedMetricRows.length > 0"
                  v-model="chartTypePref"
                  :items="chartTypeItems"
                  item-title="title"
                  item-value="value"
                  density="compact"
                  hide-details
                  style="max-width: 140px"
                  @update:model-value="onChartTypeChange"
                />
              </v-card-title>
              <v-divider />
              <v-card-text class="pa-0">
                <v-progress-linear v-if="metricsLoading" indeterminate color="primary" class="my-0" />
                <v-alert v-else-if="metricsError" type="error" variant="tonal" density="compact" class="ma-3">
                  {{ metricsError }}
                </v-alert>
                <template v-else>
                  <!-- Metrik grafiği (numeric ise) – key: metrik + chart tipi + veri sayısı ile reaktif güncelleme sağlanır -->
                  <ClientOnly v-if="selectedMetricCode && isMetricNumeric && chartSeries.length > 0" class="metric-chart-wrapper pa-3">
                    <apexchart
                      :key="`${selectedMetricCode}-${effectiveChartType}-${chartSeries[0]?.data?.length ?? 0}`"
                      :type="effectiveChartType"
                      height="260"
                      :options="chartOptions"
                      :series="chartSeries"
                    />
                    <template #fallback>
                      <div class="d-flex align-center justify-center py-6"><v-progress-circular indeterminate color="primary" /></div>
                    </template>
                  </ClientOnly>
                  <v-data-table
                    v-if="selectedMetricCode && selectedMetricRows.length > 0"
                    :headers="metricTableHeaders"
                    :items="selectedMetricRows"
                    :items-per-page="25"
                    :items-per-page-options="[10, 25, 50, 100]"
                    item-value="_key"
                    density="compact"
                    class="metric-data-table"
                  >
                    <template #[slotTimestamp]="{ item }">
                      <span class="text-caption">{{ formatMetricTimestamp(item.timestamp) }}</span>
                    </template>
                    <template #[slotValue]="{ item }">
                      <span class="font-weight-medium">{{ item.value }}</span>
                    </template>
                  </v-data-table>
                  <div v-else-if="selectedMetricCode" class="pa-8 text-center text-medium-emphasis">
                    <ChartLineIcon size="48" class="mb-3 opacity-50" />
                    <p class="text-body-2 mb-0">{{ t('monitoring.control.metricNoData', 'Bu metrik için henüz veri yok.') }}</p>
                  </div>
                  <div v-else class="pa-8 text-center text-medium-emphasis">
                    <ChartLineIcon size="48" class="mb-3 opacity-50" />
                    <p class="text-body-2 mb-0">{{ t('monitoring.control.selectMetricHint', 'Sol listeden bir parametre seçin.') }}</p>
                  </div>
                </template>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>
      </div>

      <!-- Metrik yok -->
      <v-alert v-else type="info" variant="tonal" density="comfortable" class="mt-4">
        <p class="text-body-2 mb-0">
          {{ t('monitoring.control.metricsNoData', 'Henüz bu asset için metrik verisi yok.') }}
        </p>
        <p class="text-caption mt-1">
          {{ t('monitoring.control.metricsNoDataHint', 'Asset tipinde collectible tanımı olmalı veya veri gelmiş olmalı.') }}
        </p>
      </v-alert>
    </template>
  </div>
</template>

<style scoped>
.control-detail-panel {
  min-height: 300px;
}

.metrics-split-layout {
  min-height: 320px;
}

.metric-params-card {
  border-radius: 10px;
  overflow: hidden;
  height: 100%;
  min-height: 280px;
}
.metric-params-card :deep(.v-card-title) {
  background: rgba(var(--v-theme-surface-variant), 0.3);
}

.metric-params-list {
  max-height: 320px;
  overflow-y: auto;
}

.metric-param-item {
  cursor: pointer;
  margin: 2px 8px;
  transition: background 0.2s ease, color 0.2s ease;
}
.metric-param-item:hover {
  background: rgba(var(--v-theme-primary), 0.08);
}
.metric-param-item.metric-param-selected {
  background: rgba(var(--v-theme-primary), 0.12);
  color: rgb(var(--v-theme-primary));
}

.metric-param-dot {
  display: inline-block;
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: rgba(var(--v-theme-on-surface), 0.3);
}

.metric-param-name {
  font-size: 0.875rem;
  font-weight: 500;
}

.metric-param-value {
  font-size: 0.75rem;
  font-weight: 600;
  color: rgb(var(--v-theme-primary));
  min-width: 32px;
  text-align: right;
}

.metric-data-card {
  min-height: 280px;
  border-radius: 10px;
  overflow: hidden;
  height: 100%;
}
.metric-data-card :deep(.v-card-title) {
  background: rgba(var(--v-theme-surface-variant), 0.2);
}

.metric-data-title {
  font-weight: 600;
}

.metric-data-table :deep(thead th) {
  font-size: 0.75rem;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  background: rgba(var(--v-theme-surface-variant), 0.5);
}
.metric-data-table :deep(tbody tr:hover) {
  background: rgba(var(--v-theme-primary), 0.04);
}

/* Chart container: genişlik ve boyut sorunlarını önler */
.metric-chart-wrapper {
  width: 100%;
  min-height: 260px;
}

/* Chart ile tablo arasında boşluk */
.metric-chart-wrapper + .metric-data-table {
  margin-top: 16px;
}
</style>
