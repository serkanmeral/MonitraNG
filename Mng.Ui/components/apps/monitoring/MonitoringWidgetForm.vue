<script setup lang="ts">
import { ref, computed, watch, onMounted, reactive } from 'vue';
import { useWidgetStore } from '@/stores/apps/widget';
import { useOrganizationStore } from '@/stores/apps/organization';
import { useAssetTypeDefinitionsStore } from '@/stores/apps/assetTypeDefinitions';
import { fetchFromDataGateway } from '@/services/apiService';
import type { MonAsset } from '@/types/apps/organization';
import type { CreateWidgetDto, UpdateWidgetDto, Widget } from '@/stores/apps/widget';

const props = defineProps<{
  /** Edit modu: mevcut widget verisi */
  initial?: Widget | null;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  submit: [dto: CreateWidgetDto];
  update: [dto: UpdateWidgetDto];
  cancel: [];
}>();

const isEditMode = computed(() => !!props.initial);

function mt(key: string, fallback: string): string {
  return props.t?.(key) ?? fallback;
}

const widgetStore = useWidgetStore();
const orgStore = useOrganizationStore();
const assetTypeStore = useAssetTypeDefinitionsStore();

type AssetScope = 'byType' | 'manual';
const step = ref(1);

const TIME_RANGE_OPTIONS = [
  { value: 20, labelKey: 'monitoring.control.last20min' },
  { value: 60, labelKey: 'monitoring.control.last1h' },
  { value: 360, labelKey: 'monitoring.control.last6h' },
  { value: 1440, labelKey: 'monitoring.control.last1d' },
  { value: 10080, labelKey: 'monitoring.control.last7d' },
  { value: null, labelKey: 'monitoring.control.allTime' },
] as const;
const LIMIT_OPTIONS = [10, 50, 100, 250, 500, 1000, 2000];
const REFRESH_OPTIONS = [
  { value: 0, labelKey: 'monitoring.widgets.refreshOff' },
  { value: 30, labelKey: 'monitoring.widgets.refresh30s' },
  { value: 60, labelKey: 'monitoring.widgets.refresh60s' },
  { value: 120, labelKey: 'monitoring.widgets.refresh120s' },
  { value: 300, labelKey: 'monitoring.widgets.refresh300s' },
] as const;
const assetScope = ref<AssetScope>('byType');
const assetTypeId = ref<string>('');
const selectedAssetIds = ref<string[]>([]);
const collectibleCode = ref<string>('');
const widgetType = ref<'chart' | 'card' | 'map' | 'gauge'>('chart');
const chartType = ref<'line' | 'bar' | 'area' | 'pie' | 'donut'>('line');
const timeRangeMinutes = ref<number | null>(60);
const limit = ref(500);
const refreshIntervalSeconds = ref<number>(60);
const title = ref('');
const name = ref('');
const description = ref('');
const category = ref<string>('');

/** Harita widget: varsayılan ayarlar */
const MAP_ZOOM_OPTIONS = Array.from({ length: 11 }, (_, i) => i + 4); // 4–14
const MAP_BASE_LAYER_OPTIONS = [
  { value: 'osm' as const, title: 'Çevrimiçi (OSM)' },
  { value: 'geoserver' as const, title: 'Çevrimdışı (GeoServer)' },
];
const MAP_LAYER_KEYS = ['landuse', 'roads', 'waterways', 'water_areas', 'railways', 'stations', 'places'] as const;
const MAP_LAYER_LABELS: Record<string, string> = {
  landuse: 'Arazi kullanımı',
  roads: 'Yollar',
  waterways: 'Su yolları',
  water_areas: 'Su alanları',
  railways: 'Demiryolları',
  stations: 'İstasyonlar',
  places: 'Yerleşimler',
};
const mapDefaultZoom = ref(6);
const mapDefaultBaseLayer = ref<'osm' | 'geoserver'>('osm');
const mapDefaultLayerVisibility = reactive<Record<string, boolean>>({
  landuse: true,
  roads: true,
  waterways: true,
  water_areas: true,
  railways: true,
  stations: true,
  places: true,
});

/** Card widget: format ve görünüm */
const cardFormat = ref<'number' | 'boolean' | 'text'>('number');
const cardDisplay = ref<'default' | 'badge'>('default');
const cardBooleanTrueLabel = ref('Kapalı');
const cardBooleanFalseLabel = ref('Açık');

/** Gauge widget: min, max, birim, eşikler (renk bölgeleri) */
const gaugeMin = ref(0);
const gaugeMax = ref(100);
const gaugeUnit = ref('');
const gaugeThreshold1 = ref<number | ''>('');
const gaugeThreshold2 = ref<number | ''>('');

// Asset type seçildiğinde o tipteki asset'leri al
const assetsByType = ref<MonAsset[]>([]);

// Manuel seçim: tree'den asset'ler - flat list
const allAssets = computed(() => {
  const result: MonAsset[] = [];
  function collect(nodes: any[]) {
    for (const n of nodes) {
      if (n.type === 'asset') result.push(n.data as MonAsset);
      if (n.type === 'item' && n.children?.length) collect(n.children);
    }
  }
  collect(orgStore.treeNodes);
  return result;
});

// Asset type'a göre asset listesi
watch(assetTypeId, async (id) => {
  if (!id) {
    assetsByType.value = [];
    return;
  }
  try {
    const res = await fetchFromDataGateway(
      `/api/v1/data/mon_assets?filter=type:eq:${encodeURIComponent(id)},status:eq:active&limit=500`
    );
    const arr = Array.isArray(res) ? res : res?.items ?? res?.data ?? [];
    assetsByType.value = arr;
  } catch {
    assetsByType.value = [];
  }
});

/** Tren / mon_metrics collectible kodları için okunabilir etiketler */
const COLLECTIBLE_LABELS: Record<string, string> = {
  speed: 'Hız (km/h)',
  lat: 'Enlem',
  lon: 'Boylam',
  heading: 'Yön (°)',
  trainId: 'Tren kodu',
  routeId: 'Rota',
  timestamp: 'Zaman',
  'sensors.engineTempC': 'Motor sıcaklığı (°C)',
  'sensors.oilPressureBar': 'Yağ basıncı (bar)',
  'sensors.coolantTempC': 'Soğutucu sıcaklığı (°C)',
  'sensors.batteryVoltageV': 'Batarya (V)',
  'sensors.brakePipePressureBar': 'Fren borusu basıncı (bar)',
  'sensors.cabTempC': 'Kabin sıcaklığı (°C)',
  'sensors.vibrationMs2': 'Titreşim (m/s²)',
  'sensors.doorClosed': 'Kapı kapalı',
};

function collectibleLabel(code: string): string {
  return COLLECTIBLE_LABELS[code] ?? code;
}

// Collectible listesi (görünen ad: collectibleLabel)
const collectibleOptions = computed(() => {
  let raw: Array<{ code: string; name?: string }> = [];
  if (assetScope.value === 'byType' && assetTypeId.value) {
    const type = assetTypeStore.types.find((t) => t.__dataId === assetTypeId.value);
    raw = (type?.collectibles ?? []).map((c) => ({
      code: typeof c === 'string' ? c : (c as any).code ?? (c as any).name ?? '',
      name: typeof c === 'object' && c && (c as any).name ? (c as any).name : undefined,
    })).filter((c) => c.code);
  }
  if (assetScope.value === 'manual' && selectedAssetIds.value.length > 0) {
    const codes = new Set<string>();
    const selected = new Set(selectedAssetIds.value);
    for (const a of allAssets.value) {
      if (!selected.has(a.__dataId)) continue;
      const cfg = a.collectible_config;
      if (Array.isArray(cfg)) {
        cfg.filter((c) => c.enabled !== false).forEach((c) => codes.add(c.code));
      }
    }
    raw = Array.from(codes).map((c) => ({ code: c }));
  }
  return raw.map((c) => ({ code: c.code, name: c.name ?? collectibleLabel(c.code) }));
});

// Seçilen asset ID'leri (byType: assetsByType'dan, manual: selectedAssetIds)
const resolvedAssetIds = computed(() => {
  if (assetScope.value === 'byType') {
    return assetsByType.value.map((a) => a.__dataId);
  }
  return selectedAssetIds.value;
});

// Asset ID -> name map (multi-series legend için)
const assetIdToName = computed(() => {
  const map = new Map<string, string>();
  const assets = assetScope.value === 'byType' ? assetsByType.value : allAssets.value;
  const ids = new Set(resolvedAssetIds.value);
  for (const a of assets) {
    if (ids.has(a.__dataId)) map.set(a.__dataId, a.name ?? a.__dataId);
  }
  return map;
});

// Multi-series: birden fazla asset seçiliyse
const isMultiSeries = computed(() => resolvedAssetIds.value.length > 1 && widgetType.value === 'chart');

// Monitoring category - var mı kontrol et, yoksa ilk kategorinin ID'sini kullan
const monitoringCategoryId = computed(() => {
  const cat = widgetStore.categories.find(
    (c) => (c.name ?? '').toLowerCase() === 'monitoring'
  );
  return cat?.__dataId ?? cat?.dataId ?? widgetStore.categories[0]?.__dataId ?? '';
});

function toggleAssetSelection(id: string) {
  const idx = selectedAssetIds.value.indexOf(id);
  if (idx >= 0) {
    selectedAssetIds.value = selectedAssetIds.value.filter((x) => x !== id);
  } else {
    selectedAssetIds.value = [...selectedAssetIds.value, id];
  }
}

function isAssetSelected(id: string) {
  return selectedAssetIds.value.includes(id);
}

function canGoNextStep(): boolean {
  if (step.value === 1) {
    if (assetScope.value === 'byType') return !!assetTypeId.value;
    return selectedAssetIds.value.length > 0;
  }
  // Adım 2: Harita için collectible zorunlu değil; Chart/Card için Adım 3'te kontrol edilir
  if (step.value === 2) return true;
  if (step.value === 3) {
    const base = !!title.value.trim() && !!name.value.trim();
    if (widgetType.value === 'map') return base;
    if (widgetType.value === 'gauge') return base && !!collectibleCode.value;
    return base && !!collectibleCode.value;
  }
  return false;
}

function nextStep() {
  if (step.value < 3 && canGoNextStep()) step.value++;
}

function prevStep() {
  if (step.value > 1) step.value--;
}

function generateName() {
  if (title.value.trim()) {
    name.value = title.value
      .trim()
      .toLowerCase()
      .replace(/\s+/g, '_')
      .replace(/[^a-z0-9_]/g, '');
  }
}

watch(title, () => {
  if (!name.value?.trim() && !isEditMode.value) generateName();
});

// Edit modu: initial'dan formu doldur
function populateFromInitial() {
  const w = props.initial;
  if (!w?.config) return;
  const cfg = w.config as any;
  assetScope.value = (cfg.assetScope as AssetScope) || 'byType';
  assetTypeId.value = cfg.assetTypeId ?? '';
  selectedAssetIds.value = Array.isArray(cfg.assetIds) ? [...cfg.assetIds] : [];
  collectibleCode.value = cfg.collectibleCode ?? '';
  widgetType.value = (w.type === 'gauge' ? 'gauge' : w.type === 'map' ? 'map' : w.type === 'chart' ? 'chart' : 'card') as 'chart' | 'card' | 'map' | 'gauge';
  chartType.value = (cfg.type === 'bar' ? 'bar' : cfg.type === 'area' ? 'area' : cfg.type === 'pie' ? 'pie' : cfg.type === 'donut' ? 'donut' : 'line') as 'line' | 'bar' | 'area' | 'pie' | 'donut';
  timeRangeMinutes.value = cfg.timeRangeMinutes ?? 60;
  limit.value = cfg.limit ?? 500;
  refreshIntervalSeconds.value = cfg.refreshIntervalSeconds ?? 60;
  if (w.type === 'map') {
    mapDefaultZoom.value = typeof cfg.defaultZoom === 'number' ? cfg.defaultZoom : 6;
    mapDefaultBaseLayer.value = (cfg.defaultBaseLayer === 'geoserver' || cfg.defaultBaseLayer === 'osm') ? cfg.defaultBaseLayer : 'osm';
    if (cfg.defaultLayerVisibility && typeof cfg.defaultLayerVisibility === 'object') {
      MAP_LAYER_KEYS.forEach((key) => {
        if (typeof cfg.defaultLayerVisibility[key] === 'boolean') mapDefaultLayerVisibility[key] = cfg.defaultLayerVisibility[key];
      });
    }
  }
  if (w.type === 'card') {
    cardFormat.value = (cfg.format === 'boolean' || cfg.format === 'text') ? cfg.format : 'number';
    cardDisplay.value = cfg.cardDisplay === 'badge' ? 'badge' : 'default';
    cardBooleanTrueLabel.value = cfg.booleanTrueLabel ?? 'Kapalı';
    cardBooleanFalseLabel.value = cfg.booleanFalseLabel ?? 'Açık';
  }
  if (w.type === 'gauge') {
    gaugeMin.value = typeof cfg.min === 'number' ? cfg.min : 0;
    gaugeMax.value = typeof cfg.max === 'number' ? cfg.max : 100;
    gaugeUnit.value = cfg.unit ?? '';
    const th = Array.isArray(cfg.thresholds) ? cfg.thresholds : [];
    gaugeThreshold1.value = th.length > 0 && typeof th[0]?.to === 'number' ? th[0].to : '';
    gaugeThreshold2.value = th.length > 1 && typeof th[1]?.to === 'number' ? th[1].to : '';
  }
  title.value = w.title ?? '';
  name.value = w.name ?? '';
  description.value = w.description ?? '';
  category.value = (w.category as any)?.__dataId ?? (w.category as any)?.dataId ?? (typeof w.category === 'string' ? w.category : '') ?? '';
  if (assetScope.value === 'byType' && assetTypeId.value) {
    fetchFromDataGateway(
      `/api/v1/data/mon_assets?filter=type:eq:${encodeURIComponent(assetTypeId.value)},status:eq:active&limit=500`
    ).then((res) => {
      const arr = Array.isArray(res) ? res : res?.items ?? res?.data ?? [];
      assetsByType.value = arr;
    }).catch(() => { assetsByType.value = []; });
  }
}

watch(() => props.initial, (w) => {
  if (w) populateFromInitial();
}, { immediate: true });

onMounted(async () => {
  await Promise.all([
    widgetStore.fetchWidgetCategories(),
    orgStore.loadAll(),
    assetTypeStore.loadTypes(),
  ]);
  await widgetStore.ensureMonitoringCategory();
  if (props.initial) populateFromInitial();
});

async function save() {
  if (!canGoNextStep()) return;

  const assetIds = resolvedAssetIds.value;

  // Harita widget: konum (lat/lon) kullanılır; dataSource minimal, config'de assetIds
  if (widgetType.value === 'map') {
    const dto: CreateWidgetDto = {
      name: name.value.trim() || `monitoring_map_${Date.now()}`,
      title: title.value.trim(),
      description: description.value.trim() || undefined,
      category: isEditMode.value ? (typeof category.value === 'string' ? category.value : (category.value as any)?.__dataId ?? (category.value as any)?.dataId ?? '') : (monitoringCategoryId.value || ''),
      type: 'map',
      isActive: true,
      order: 0,
      dataSource: {
        type: 'data',
        dataset: 'mon_metrics',
        getMethod: 'default',
        default: { limit: 1 },
      },
      config: {
        monitoring: true,
        map: true,
        assetScope: assetScope.value,
        assetTypeId: assetScope.value === 'byType' ? assetTypeId.value : undefined,
        assetIds: assetIds.length > 0 ? assetIds : undefined,
        refreshIntervalSeconds: refreshIntervalSeconds.value || 0,
        defaultZoom: mapDefaultZoom.value,
        defaultBaseLayer: mapDefaultBaseLayer.value,
        defaultLayerVisibility: { ...mapDefaultLayerVisibility },
      },
    };
    if (isEditMode.value) {
      const catId = typeof category.value === 'string' ? category.value : (category.value as any)?.__dataId ?? (category.value as any)?.dataId ?? '';
      emit('update', { name: dto.name, title: dto.title, description: dto.description, category: catId, type: dto.type, dataSource: dto.dataSource, config: dto.config, isActive: dto.isActive, order: dto.order });
    } else {
      emit('submit', dto);
    }
    return;
  }

  const filterParts: string[] = [];
  if (assetIds.length === 1) {
    filterParts.push(`meta.assetId:eq:${assetIds[0]}`);
  } else if (assetIds.length > 1) {
    filterParts.push(`meta.assetId:in:${assetIds.join(',')}`);
  }
  if (collectibleCode.value) {
    filterParts.push(`meta.collectibleCode:eq:${collectibleCode.value}`);
  }

  const dto: CreateWidgetDto = {
    name: name.value.trim() || `monitoring_${Date.now()}`,
    title: title.value.trim(),
    description: description.value.trim() || undefined,
    category: isEditMode.value ? (typeof category.value === 'string' ? category.value : (category.value as any)?.__dataId ?? (category.value as any)?.dataId ?? '') : (monitoringCategoryId.value || ''),
    type: widgetType.value,
    isActive: true,
    order: 0,
    dataSource: {
      type: 'data',
      dataset: 'mon_metrics',
      getMethod: 'default',
      default: {
        filter: filterParts.join(','),
        sort: '-timestamp',
        limit: limit.value,
      },
    },
    config: {
      monitoring: true,
      assetScope: assetScope.value,
      assetTypeId: assetScope.value === 'byType' ? assetTypeId.value : undefined,
      assetIds: assetScope.value === 'manual' ? assetIds : undefined,
      collectibleCode: collectibleCode.value,
      timeRangeMinutes: timeRangeMinutes.value ?? 60,
      limit: limit.value,
      refreshIntervalSeconds: refreshIntervalSeconds.value || 0,
      ...(widgetType.value === 'chart' && {
        type: chartType.value,
        height: 300,
        xAxis: { field: 'timestamp', label: 'Zaman' },
        yAxis: { field: 'value', label: collectibleCode.value },
        ...(isMultiSeries.value && {
          multiSeries: true,
          series: assetIds.map((id) => ({
            name: assetIdToName.value.get(id) ?? id,
            field: id,
            type: chartType.value,
          })),
        }),
      }),
      ...(widgetType.value === 'card' && {
        valueField: 'value',
        format: cardFormat.value,
        cardDisplay: cardDisplay.value,
        icon: cardFormat.value === 'boolean' ? 'mdi-check-circle' : 'mdi-chart-line',
        color: 'primary',
        ...(cardFormat.value === 'boolean' && {
          booleanTrueLabel: cardBooleanTrueLabel.value,
          booleanFalseLabel: cardBooleanFalseLabel.value,
        }),
      }),
      ...(widgetType.value === 'gauge' && (() => {
        const mn = gaugeMin.value;
        const mx = gaugeMax.value;
        const t1 = typeof gaugeThreshold1.value === 'number' ? gaugeThreshold1.value : null;
        const t2 = typeof gaugeThreshold2.value === 'number' ? gaugeThreshold2.value : null;
        let thresholds: Array<{ from: number; to: number; color: string }> = [];
        if (t1 != null && t2 != null) {
          const a = Math.min(t1, t2);
          const b = Math.max(t1, t2);
          thresholds = [
            { from: mn, to: a, color: 'success' },
            { from: a, to: b, color: 'warning' },
            { from: b, to: mx, color: 'error' },
          ];
        } else if (t1 != null) {
          thresholds = [
            { from: mn, to: t1, color: 'success' },
            { from: t1, to: mx, color: 'error' },
          ];
        }
        return {
          valueField: 'value',
          min: mn,
          max: mx,
          unit: gaugeUnit.value.trim() || undefined,
          thresholds: thresholds.length ? thresholds : undefined,
        };
      })()),
    },
  };

  if (isEditMode.value) {
    const catId = typeof category.value === 'string' ? category.value : (category.value as any)?.__dataId ?? (category.value as any)?.dataId ?? '';
    emit('update', {
      name: dto.name,
      title: dto.title,
      description: dto.description,
      category: catId,
      type: dto.type,
      dataSource: dto.dataSource,
      config: dto.config,
      isActive: dto.isActive,
      order: dto.order,
    });
  } else {
    emit('submit', dto);
  }
}

</script>

<template>
  <v-card variant="outlined">
    <v-card-title class="py-3">
      {{ isEditMode ? mt('monitoring.widgets.editTitle', 'Widget Düzenle') : mt('monitoring.widgets.formTitle', 'Yeni Monitoring Widget') }}
    </v-card-title>
    <v-divider />
    <v-card-text class="pa-4">
      <v-stepper v-model="step" flat>
        <v-stepper-header>
          <v-stepper-item :value="1" :complete="step > 1" :title="mt('monitoring.widgets.stepAssets', 'Asset')" />
          <v-stepper-divider />
          <v-stepper-item :value="2" :complete="step > 2" :title="mt('monitoring.widgets.stepCollectible', 'Collectible')" />
          <v-stepper-divider />
          <v-stepper-item :value="3" :title="mt('monitoring.widgets.stepWidget', 'Widget')" />
        </v-stepper-header>

        <v-stepper-window>
          <!-- Adım 1: Asset -->
          <v-stepper-window-item :value="1">
            <div class="mb-4">
              <div class="text-subtitle-2 mb-2">{{ mt('monitoring.widgets.assetScope', 'Asset kapsamı') }}</div>
              <v-radio-group v-model="assetScope" hide-details>
                <v-radio :label="mt('monitoring.widgets.byType', 'Tipe göre (tüm asset\'ler)')" value="byType" />
                <v-radio :label="mt('monitoring.widgets.manualSelect', 'Manuel seçim')" value="manual" />
              </v-radio-group>
            </div>

            <div v-if="assetScope === 'byType'" class="mb-4">
              <v-select
                v-model="assetTypeId"
                :items="assetTypeStore.types"
                item-title="name"
                item-value="__dataId"
                :label="mt('monitoring.widgets.assetType', 'Asset tipi')"
                variant="outlined"
                density="compact"
                clearable
              />
              <div v-if="assetTypeId && assetsByType.length > 0" class="text-caption text-medium-emphasis mt-2">
                {{ assetsByType.length }} {{ mt('monitoring.widgets.assetsFound', 'asset bulundu') }}
              </div>
            </div>

            <div v-if="assetScope === 'manual'">
              <div class="text-subtitle-2 mb-2">{{ mt('monitoring.widgets.selectAssets', 'Asset\'leri seçin') }}</div>
              <div class="asset-list" style="max-height: 300px; overflow-y: auto;">
                <v-list density="compact">
                  <v-list-item
                    v-for="a in allAssets.filter((x) => x.status === 'active')"
                    :key="a.__dataId"
                    :active="isAssetSelected(a.__dataId)"
                    @click="toggleAssetSelection(a.__dataId)"
                  >
                    <template #prepend>
                      <v-checkbox
                        :model-value="isAssetSelected(a.__dataId)"
                        hide-details
                        density="compact"
                        @click.stop
                        @update:model-value="toggleAssetSelection(a.__dataId)"
                      />
                    </template>
                    <v-list-item-title>{{ a.name }}</v-list-item-title>
                    <v-list-item-subtitle>{{ a.type }}</v-list-item-subtitle>
                  </v-list-item>
                </v-list>
              </div>
              <div v-if="selectedAssetIds.length > 0" class="text-caption text-medium-emphasis mt-2">
                {{ selectedAssetIds.length }} {{ mt('monitoring.widgets.assetsSelected', 'asset seçildi') }}
              </div>
            </div>
          </v-stepper-window-item>

          <!-- Adım 2: Collectible -->
          <v-stepper-window-item :value="2">
            <div class="mb-4">
              <v-select
                v-model="collectibleCode"
                :items="collectibleOptions"
                item-title="name"
                item-value="code"
                :label="mt('monitoring.widgets.collectible', 'Collectible parametresi')"
                variant="outlined"
                density="compact"
                :disabled="collectibleOptions.length === 0"
              />
              <v-alert v-if="collectibleOptions.length === 0" type="info" variant="tonal" density="compact" class="mt-2">
                {{ mt('monitoring.widgets.noCollectibles', 'Seçilen asset(ler) için collectible bulunamadı.') }}
              </v-alert>
              <v-alert type="info" variant="tonal" density="compact" class="mt-2">
                {{ mt('monitoring.widgets.step2MapHint', 'Harita widget\'ı seçecekseniz collectible seçmeden ileri geçebilirsiniz; haritada konum (lat/lon) kullanılır.') }}
              </v-alert>
            </div>
          </v-stepper-window-item>

          <!-- Adım 3: Widget tipi + kaydet -->
          <v-stepper-window-item :value="3">
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="title"
                  :label="mt('monitoring.widgets.title', 'Başlık')"
                  variant="outlined"
                  density="compact"
                  required
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="name"
                  :label="mt('monitoring.widgets.name', 'Name (teknik)')"
                  variant="outlined"
                  density="compact"
                  hint="Boş bırakılırsa başlıktan oluşturulur"
                  persistent-hint
                />
              </v-col>
              <v-col cols="12">
                <v-textarea
                  v-model="description"
                  :label="mt('monitoring.widgets.description', 'Açıklama')"
                  variant="outlined"
                  density="compact"
                  rows="2"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-select
                  v-model="widgetType"
                  :items="[
                    { value: 'chart', title: mt('monitoring.widgets.typeChart', 'Chart') },
                    { value: 'card', title: mt('monitoring.widgets.typeCard', 'Card') },
                    { value: 'map', title: mt('monitoring.widgets.typeMap', 'Harita') },
                    { value: 'gauge', title: mt('monitoring.widgets.typeGauge', 'Gauge') },
                  ]"
                  item-title="title"
                  item-value="value"
                  :label="mt('monitoring.widgets.widgetType', 'Widget tipi')"
                  variant="outlined"
                  density="compact"
                />
              </v-col>
              <v-col v-if="widgetType === 'chart'" cols="12" md="6">
                <v-select
                  v-model="chartType"
                  :items="[
                    { value: 'line', title: mt('monitoring.widgets.chartTypeLine', 'Çizgi') },
                    { value: 'bar', title: mt('monitoring.widgets.chartTypeBar', 'Çubuk') },
                    { value: 'area', title: mt('monitoring.widgets.chartTypeArea', 'Alan') },
                    { value: 'pie', title: mt('monitoring.widgets.chartTypePie', 'Pasta') },
                    { value: 'donut', title: mt('monitoring.widgets.chartTypeDonut', 'Halka') },
                  ]"
                  item-title="title"
                  item-value="value"
                  :label="mt('monitoring.widgets.chartType', 'Chart tipi')"
                  variant="outlined"
                  density="compact"
                />
              </v-col>
              <v-col v-if="widgetType === 'chart' && isMultiSeries" cols="12">
                <v-alert
                  type="info"
                  density="compact"
                  variant="tonal"
                  class="mb-0"
                >
                  {{ mt('monitoring.widgets.chartHintMultiSeries', 'Birden fazla asset seçildi: Her asset ayrı bir seri (çizgi) olarak gösterilecek. Legend\'da isimleri görebilirsiniz.') }}
                </v-alert>
              </v-col>
              <template v-if="widgetType === 'card'">
                <v-col cols="12" md="4">
                  <v-select
                    v-model="cardFormat"
                    :items="[
                      { value: 'number', title: mt('monitoring.widgets.cardFormatNumber', 'Sayı') },
                      { value: 'boolean', title: mt('monitoring.widgets.cardFormatBoolean', 'Açık/Kapalı') },
                      { value: 'text', title: mt('monitoring.widgets.cardFormatText', 'Metin') },
                    ]"
                    item-title="title"
                    item-value="value"
                    :label="mt('monitoring.widgets.cardFormat', 'Card formatı')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" md="4">
                  <v-select
                    v-model="cardDisplay"
                    :items="[
                      { value: 'default', title: mt('monitoring.widgets.cardDisplayDefault', 'Normal kart') },
                      { value: 'badge', title: mt('monitoring.widgets.cardDisplayBadge', 'Badge (kompakt)') },
                    ]"
                    item-title="title"
                    item-value="value"
                    :label="mt('monitoring.widgets.cardDisplay', 'Görünüm')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col v-if="cardFormat === 'boolean'" cols="12" md="4">
                  <v-text-field
                    v-model="cardBooleanTrueLabel"
                    :label="mt('monitoring.widgets.booleanTrueLabel', 'Değer true etiketi')"
                    variant="outlined"
                    density="compact"
                    placeholder="Kapalı"
                  />
                </v-col>
                <v-col v-if="cardFormat === 'boolean'" cols="12" md="4">
                  <v-text-field
                    v-model="cardBooleanFalseLabel"
                    :label="mt('monitoring.widgets.booleanFalseLabel', 'Değer false etiketi')"
                    variant="outlined"
                    density="compact"
                    placeholder="Açık"
                  />
                </v-col>
              </template>
              <v-col cols="12">
                <v-divider class="my-2" />
                <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.widgets.dataSettings', 'Veri ayarları') }}</div>
              </v-col>
              <template v-if="widgetType !== 'map'">
                <v-col cols="12" md="4">
                  <v-select
                    v-model="timeRangeMinutes"
                    :items="TIME_RANGE_OPTIONS.map((o) => ({ value: o.value, title: mt(o.labelKey, String(o.value ?? 'Tümü')) }))"
                    item-title="title"
                    item-value="value"
                    :label="mt('monitoring.control.timeRange', 'Zaman aralığı')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" md="4">
                  <v-select
                    v-model="limit"
                    :items="LIMIT_OPTIONS"
                    :label="mt('monitoring.widgets.maxRecords', 'Maks. kayıt')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
              </template>
              <v-col cols="12" :md="widgetType === 'map' ? 12 : 4">
                <v-select
                  v-model="refreshIntervalSeconds"
                  :items="REFRESH_OPTIONS.map((o) => ({ value: o.value, title: mt(o.labelKey, o.value === 0 ? 'Kapalı' : `${o.value}s`) }))"
                  item-title="title"
                  item-value="value"
                  :label="mt('monitoring.widgets.refreshRate', 'Yenileme')"
                  variant="outlined"
                  density="compact"
                />
              </v-col>
              <template v-if="widgetType === 'gauge'">
                <v-col cols="12">
                  <v-divider class="my-2" />
                  <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.widgets.gaugeSettings', 'Gauge ayarları') }}</div>
                </v-col>
                <v-col cols="12" md="3">
                  <v-text-field
                    v-model.number="gaugeMin"
                    type="number"
                    :label="mt('monitoring.widgets.gaugeMin', 'Min')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" md="3">
                  <v-text-field
                    v-model.number="gaugeMax"
                    type="number"
                    :label="mt('monitoring.widgets.gaugeMax', 'Max')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" md="3">
                  <v-text-field
                    v-model="gaugeUnit"
                    :label="mt('monitoring.widgets.gaugeUnit', 'Birim')"
                    variant="outlined"
                    density="compact"
                    placeholder="°C, bar, km/h"
                  />
                </v-col>
                <v-col cols="12" md="3">
                  <v-text-field
                    v-model.number="gaugeThreshold1"
                    type="number"
                    :label="mt('monitoring.widgets.gaugeThreshold1', 'Eşik 1 (yeşil→sarı)')"
                    variant="outlined"
                    density="compact"
                    placeholder="Opsiyonel"
                  />
                </v-col>
                <v-col cols="12" md="3">
                  <v-text-field
                    v-model.number="gaugeThreshold2"
                    type="number"
                    :label="mt('monitoring.widgets.gaugeThreshold2', 'Eşik 2 (sarı→kırmızı)')"
                    variant="outlined"
                    density="compact"
                    placeholder="Opsiyonel"
                  />
                </v-col>
              </template>
              <template v-if="widgetType === 'map'">
                <v-col cols="12">
                  <v-divider class="my-2" />
                  <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.widgets.mapSettings', 'Harita ayarları') }}</div>
                </v-col>
                <v-col cols="12" md="4">
                  <v-select
                    v-model="mapDefaultZoom"
                    :items="MAP_ZOOM_OPTIONS"
                    :label="mt('monitoring.widgets.mapZoom', 'Varsayılan zoom')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" md="4">
                  <v-select
                    v-model="mapDefaultBaseLayer"
                    :items="MAP_BASE_LAYER_OPTIONS"
                    item-title="title"
                    item-value="value"
                    :label="mt('monitoring.widgets.mapBaseLayer', 'Varsayılan altlık')"
                    variant="outlined"
                    density="compact"
                  />
                </v-col>
                <v-col cols="12" md="4" />
                <v-col cols="12">
                  <div class="text-caption text-medium-emphasis mb-2">{{ mt('monitoring.widgets.mapLayers', 'Varsayılan katmanlar (GeoServer)') }}</div>
                  <div class="d-flex flex-wrap gap-3">
                    <v-checkbox
                      v-for="key in MAP_LAYER_KEYS"
                      :key="key"
                      v-model="mapDefaultLayerVisibility[key]"
                      :label="MAP_LAYER_LABELS[key] ?? key"
                      density="compact"
                      hide-details
                    />
                  </div>
                </v-col>
              </template>
            </v-row>
            <v-alert v-if="widgetType === 'map'" type="info" variant="tonal" density="compact" class="mt-2">
              {{ mt('monitoring.widgets.mapHint', 'Harita widget\'ında seçilen tren/asset\'lerin son konumları (lat/lon) gösterilir.') }}
            </v-alert>
            <v-alert v-else-if="widgetType === 'chart'" type="info" variant="tonal" density="compact" class="mt-2">
              {{ isMultiSeries
                ? mt('monitoring.widgets.chartHintMultiSeries', 'Birden fazla asset seçildi: Her asset ayrı bir seri (çizgi) olarak gösterilecek.')
                : mt('monitoring.widgets.chartHint', 'Chart widget: Zaman serisi (timestamp = x, value = y) otomatik yapılandırılacak.') }}
            </v-alert>
          </v-stepper-window-item>
        </v-stepper-window>
      </v-stepper>
    </v-card-text>
    <v-divider />
    <v-card-actions class="pa-4">
      <v-btn v-if="step > 1" variant="text" @click="prevStep">
        {{ mt('monitoring.common.back', 'Geri') }}
      </v-btn>
      <v-spacer />
      <v-btn variant="text" @click="emit('cancel')">
        {{ mt('monitoring.common.cancel', 'İptal') }}
      </v-btn>
      <v-btn
        v-if="step < 3"
        color="primary"
        variant="flat"
        :disabled="!canGoNextStep()"
        @click="nextStep"
      >
        {{ mt('monitoring.common.next', 'İleri') }}
      </v-btn>
      <v-btn
        v-else
        color="primary"
        variant="flat"
        :disabled="!canGoNextStep() || widgetStore.loading"
        :loading="widgetStore.loading"
        @click="save"
      >
        {{ mt('monitoring.widgets.save', 'Kaydet') }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>
