<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OrganizationMapView from '@/components/apps/organization/OrganizationMapView.vue';
import type { MapItem } from '@/components/apps/organization/OrganizationMapView.vue';
import OrganizationItemDetailModal from '@/components/apps/organization/OrganizationItemDetailModal.vue';
import MonitoringMapView from '@/components/apps/monitoring/MonitoringMapView.vue';
import MonitoringMapAssetModal from '@/components/apps/monitoring/MonitoringMapAssetModal.vue';
import type { MapPosition } from '@/components/apps/monitoring/MonitoringMapView.vue';
import { useOrganizationStore } from '@/stores/apps/organization';
import { useMapPositions } from '@/composables/useMapPositions';
import type { OrganizationTreeNode } from '@/types/apps/organization';
import { MapPinIcon, RefreshIcon, DeviceDesktopIcon } from 'vue-tabler-icons';

const AUTO_REFRESH_INTERVALS = [15, 30, 60] as const;

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
const config = useRuntimeConfig();

function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}

const orgStore = useOrganizationStore();
const { positions: mapPositions, loading: positionsLoading, error: positionsError, lastUpdated, refresh: refreshPositions } = useMapPositions();

const geoServerAvailable = computed(() => !!((config.public as any).geoServerBaseUrl as string)?.trim?.());

const page = computed(() => ({ title: mt('monitoring.map.pageTitle', 'Harita') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: mt('monitoring.map.pageTitle', 'Harita'), disabled: true, href: '#' },
]);

const mapTab = ref<'organization' | 'trains'>('trains');

function hasValidLocation(item: { location?: { lat?: number; lon?: number } | null }): boolean {
  const loc = item.location;
  return (
    loc != null &&
    typeof loc.lat === 'number' &&
    typeof loc.lon === 'number' &&
    !Number.isNaN(loc.lat) &&
    !Number.isNaN(loc.lon)
  );
}

function getChildCount(node: OrganizationTreeNode): number {
  return node.children?.length ?? 0;
}

function flattenItemsWithLocations(
  nodes: OrganizationTreeNode[],
  acc: MapItem[] = []
): MapItem[] {
  for (const node of nodes) {
    if (node.type === 'item' && node.data.location && hasValidLocation(node.data)) {
      acc.push({
        __dataId: node.data.__dataId ?? '',
        name: node.data.name ?? '',
        location: { lat: node.data.location!.lat!, lon: node.data.location!.lon! },
        description: node.data.description ?? null,
        childCount: getChildCount(node),
      });
    }
    if (node.type === 'item' && node.children?.length) {
      flattenItemsWithLocations(node.children, acc);
    }
  }
  return acc;
}

const mapItems = computed(() => flattenItemsWithLocations(orgStore.treeNodes));

const detailModalOpen = ref(false);
const detailModalItemId = ref<string | null>(null);

function onOrgMarkerClick(item: MapItem) {
  detailModalItemId.value = item.__dataId;
  detailModalOpen.value = true;
}

const trainDetailOpen = ref(false);
const selectedPosition = ref<MapPosition | null>(null);

function onTrainMarkerClick(position: MapPosition) {
  selectedPosition.value = position;
  trainDetailOpen.value = true;
}

async function refresh() {
  if (mapTab.value === 'organization') {
    await orgStore.loadAll();
  } else {
    await refreshPositions();
  }
  if (autoRefreshEnabled.value) {
    countdownSeconds.value = autoRefreshIntervalSeconds.value;
  }
}

const autoRefreshEnabled = ref(true);
const autoRefreshIntervalSeconds = ref(30);
const countdownSeconds = ref(0);

let refreshTimerId: ReturnType<typeof setInterval> | null = null;
let countdownTimerId: ReturnType<typeof setInterval> | null = null;

function stopAutoRefresh() {
  if (refreshTimerId) {
    clearInterval(refreshTimerId);
    refreshTimerId = null;
  }
  if (countdownTimerId) {
    clearInterval(countdownTimerId);
    countdownTimerId = null;
  }
  countdownSeconds.value = 0;
}

function startAutoRefresh() {
  stopAutoRefresh();
  if (!autoRefreshEnabled.value) return;
  countdownSeconds.value = autoRefreshIntervalSeconds.value;
  refreshTimerId = setInterval(() => {
    refresh();
  }, autoRefreshIntervalSeconds.value * 1000);
  countdownTimerId = setInterval(() => {
    countdownSeconds.value = Math.max(0, countdownSeconds.value - 1);
  }, 1000);
}

watch([autoRefreshEnabled, autoRefreshIntervalSeconds], () => {
  startAutoRefresh();
});

onMounted(() => {
  if (orgStore.treeNodes.length === 0) {
    orgStore.loadAll();
  }
  refreshPositions().then(() => {
    if (autoRefreshEnabled.value) countdownSeconds.value = autoRefreshIntervalSeconds.value;
  });
  startAutoRefresh();
});

onUnmounted(() => {
  stopAutoRefresh();
});
</script>

<template>
  <div class="map-page">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-container fluid class="map-page-container">
      <div class="map-page-body">
        <v-alert v-if="orgStore.error" type="warning" variant="tonal" dismissible class="mb-4" @click:close="orgStore.error = null">
          {{ orgStore.error }}
        </v-alert>
        <v-alert v-if="positionsError && mapTab === 'trains'" type="warning" variant="tonal" dismissible class="mb-4">
          {{ positionsError }}
        </v-alert>

        <div class="d-flex align-center flex-wrap gap-2 mb-4">
        <v-btn-toggle v-model="mapTab" mandatory density="compact" color="primary" variant="outlined">
          <v-btn value="trains" size="small">
            <DeviceDesktopIcon size="18" class="mr-1" /> {{ mt('monitoring.map.trainsTab', 'Tren / varlıklar') }}
          </v-btn>
          <v-btn value="organization" size="small">
            <MapPinIcon size="18" class="mr-1" /> {{ mt('monitoring.map.organizationTab', 'Organizasyon') }}
          </v-btn>
        </v-btn-toggle>
        <v-btn variant="tonal" color="primary" size="small" to="/apps/monitoring/control">
          {{ mt('monitoring.control.pageTitle', 'Kontrol') }}
        </v-btn>
        <v-btn variant="outlined" size="small" :disabled="mapTab === 'organization' ? orgStore.loading : positionsLoading" @click="refresh">
          <RefreshIcon size="18" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }}
        </v-btn>
        <v-divider vertical class="mx-1" />
        <div class="d-flex align-center gap-2">
          <v-switch
            v-model="autoRefreshEnabled"
            density="compact"
            hide-details
            color="primary"
            :label="mt('monitoring.map.autoRefresh', 'Otomatik yenileme')"
            class="map-auto-refresh-switch"
          />
          <v-select
            v-model="autoRefreshIntervalSeconds"
            :items="AUTO_REFRESH_INTERVALS.map((s) => ({ title: `${s} sn`, value: s }))"
            density="compact"
            hide-details
            variant="outlined"
            class="map-auto-refresh-interval"
            style="max-width: 90px;"
          />
          <span v-if="autoRefreshEnabled && countdownSeconds > 0" class="text-caption text-medium-emphasis">
            {{ mt('monitoring.map.nextRefresh', 'Sonraki') }}: {{ countdownSeconds }} sn
          </span>
        </div>
        <span v-if="mapTab === 'organization' && mapItems.length > 0" class="text-caption text-medium-emphasis">
          {{ mapItems.length }} {{ mt('monitoring.map.itemsWithLocation', 'konumlu öğe') }}
        </span>
        <span v-else-if="mapTab === 'trains' && mapPositions.length > 0" class="text-caption text-medium-emphasis">
          {{ mapPositions.length }} {{ mt('monitoring.map.positionsFromHub', 'konum (MngHub/DataGateway)') }}
          <span v-if="lastUpdated"> · {{ mt('monitoring.map.lastUpdate', 'Son güncelleme') }}: {{ new Date(lastUpdated).toLocaleTimeString('tr-TR') }}</span>
        </span>
      </div>

      <v-card variant="outlined" class="map-card">
        <v-card-text class="pa-0 map-card-content">
          <!-- Harita alanı: viewport kalan yüksekliğini doldurur, altında boşluk kalmaz -->
          <div class="map-area">
            <div class="map-area-inner">
              <!-- Tren / varlık haritası -->
              <template v-if="mapTab === 'trains'">
                <MonitoringMapView
                  :positions="mapPositions"
                  :geo-server-available="geoServerAvailable"
                  height="100%"
                  :popup-hint="mt('organization.mapModal.clickForDetails', 'Detaylar için tıklayın')"
                  @marker-click="onTrainMarkerClick"
                />
                <div v-if="positionsLoading && mapPositions.length === 0" class="map-overlay-loading d-flex align-center justify-center">
                  <v-progress-circular indeterminate color="primary" />
                </div>
                <div v-else-if="!positionsLoading && mapPositions.length === 0" class="map-overlay-empty text-center py-12 text-medium-emphasis">
                  <DeviceDesktopIcon size="48" class="mb-2 d-block mx-auto opacity-50" />
                  {{ mt('monitoring.map.noPositionsYet', 'Henüz konum verisi yok. Engine\'den lat/lon metrikleri geldiğinde trenler burada görünecek.') }}
                </div>
              </template>

              <!-- Organizasyon haritası -->
              <template v-else>
                <div v-if="orgStore.loading && mapItems.length === 0" class="d-flex align-center justify-center map-area-fill">
                  <v-progress-circular indeterminate color="primary" />
                </div>
                <template v-else-if="mapItems.length > 0">
                  <OrganizationMapView
                    :items="mapItems"
                    height="100%"
                    :popup-hint="mt('organization.mapModal.clickForDetails', 'Detaylar için tıklayın')"
                    @marker-click="onOrgMarkerClick"
                  />
                </template>
                <div v-else class="text-center py-12 text-medium-emphasis map-area-fill">
                  <MapPinIcon size="48" class="mb-2 d-block mx-auto opacity-50" />
                  {{ mt('monitoring.map.noItemsWithLocation', 'Konum tanımlı öğe bulunamadı. Organizasyon sayfasından item\'lere konum ekleyebilirsiniz.') }}
                </div>
              </template>
            </div>
          </div>
        </v-card-text>
      </v-card>
      </div>
    </v-container>

      <OrganizationItemDetailModal
        :open="detailModalOpen"
        :item-id="detailModalItemId"
        :tree-nodes="orgStore.treeNodes"
        :mt="mt"
        @update:open="detailModalOpen = $event"
      />

      <MonitoringMapAssetModal
        :open="trainDetailOpen"
        :position="selectedPosition"
        :mt="mt"
        @update:open="trainDetailOpen = $event"
      />
  </div>
</template>

<style scoped>
/* Sayfa: flex column, kart kalan yüksekliği doldurur; layout height vermezse vh yedek */
.map-page {
  display: flex;
  flex-direction: column;
  min-height: 0;
  height: 100%;
}
.map-page-container {
  display: flex;
  flex-direction: column;
  flex: 1 1 0;
  min-height: 0;
}
/* Flex zinciri kendi div'imizde; v-container stilleri ezmez */
.map-page-body {
  display: flex;
  flex-direction: column;
  flex: 1 1 0;
  min-height: 0;
}
.map-card {
  flex: 1 1 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
}
.map-card-content {
  position: relative;
  flex: 1 1 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.map-area {
  display: flex;
  flex-direction: column;
  flex: 1 1 0;
  min-height: 0;
  width: 100%;
  position: relative;
  /* Layout height vermezse viewport yedek: breadcrumb + toolbar ~200px */
  min-height: calc(100vh - 220px);
}
.map-area-inner {
  flex: 1 1 0;
  min-height: 0;
  position: relative;
  width: 100%;
}
.map-area-fill {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
}
.map-overlay-loading {
  position: absolute;
  left: 50%;
  top: 50%;
  transform: translate(-50%, -50%);
  pointer-events: none;
  z-index: 10;
}
.map-overlay-empty {
  position: absolute;
  left: 0;
  right: 0;
  top: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 3rem;
}
</style>
