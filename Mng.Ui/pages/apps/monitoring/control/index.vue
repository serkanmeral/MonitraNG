<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import OrganizationTreeView from '@/components/apps/organization/OrganizationTreeView.vue';
import ControlAssetDetailPanel from '@/components/apps/monitoring/control/ControlAssetDetailPanel.vue';
import { useMonitoringStore } from '@/stores/apps/monitoring';
import { useOrganizationStore } from '@/stores/apps/organization';
import { useAuthStore } from '@/stores/auth';
import type { MonEngine, MonAgent } from '@/types/apps/monitoring';
import type { OrganizationSelectedNode } from '@/types/apps/organization';
import { RefreshIcon, KeyIcon, CheckIcon, XIcon, DeviceDesktopIcon, UsersIcon, BoxIcon, ChevronDownIcon, ChevronUpIcon, ActivityHeartbeatIcon, LayoutSidebarLeftCollapseIcon, LayoutSidebarLeftExpandIcon } from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const nuxtApp = useNuxtApp();
function mt(key: string, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key) || fallback;
    if (i18n?.t) return i18n.t(key) || fallback;
  } catch (_) {}
  return fallback;
}
function mtParam(key: string, params: Record<string, string | number>, fallback: string): string {
  try {
    const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
    if (i18n?.global?.t) return i18n.global.t(key, params) || fallback;
    if (i18n?.t) return i18n.t(key, params) || fallback;
  } catch (_) {}
  return fallback;
}

const authStore = useAuthStore();
const store = useMonitoringStore();
const orgStore = useOrganizationStore();
const canEdit = computed(() => authStore.isManager);

/** Asset Explorer: Kontrol sayfasına özel seçim (org sayfasından bağımsız) */
const controlSelectedNode = ref<OrganizationSelectedNode | null>(null);
const controlTreeViewRef = ref<InstanceType<typeof OrganizationTreeView> | null>(null);

/** Tree panel: resize + collapse (localStorage ile kalıcı) */
const TREE_STORAGE_KEY = 'monitoring-control-tree';
const TREE_MIN_WIDTH = 200;
const TREE_MAX_WIDTH = 480;
const TREE_DEFAULT_WIDTH = 320;

function loadTreeState() {
  if (typeof window === 'undefined') return { width: TREE_DEFAULT_WIDTH, collapsed: false };
  try {
    const raw = localStorage.getItem(TREE_STORAGE_KEY);
    if (raw) {
      const o = JSON.parse(raw);
      return {
        width: Math.min(TREE_MAX_WIDTH, Math.max(TREE_MIN_WIDTH, o.width ?? TREE_DEFAULT_WIDTH)),
        collapsed: !!o.collapsed,
      };
    }
  } catch (_) {}
  return { width: TREE_DEFAULT_WIDTH, collapsed: false };
}

function saveTreeState(width: number, collapsed: boolean) {
  try {
    localStorage.setItem(TREE_STORAGE_KEY, JSON.stringify({ width, collapsed }));
  } catch (_) {}
}

const treeWidth = ref(loadTreeState().width);
const treeCollapsed = ref(loadTreeState().collapsed);
const resizeActive = ref(false);

function startResize() {
  resizeActive.value = true;
}

function onResizeMove(e: MouseEvent) {
  if (!resizeActive.value) return;
  const w = Math.min(TREE_MAX_WIDTH, Math.max(TREE_MIN_WIDTH, e.clientX));
  treeWidth.value = w;
  saveTreeState(w, treeCollapsed.value);
}

function stopResize() {
  resizeActive.value = false;
}

function toggleTreeCollapse() {
  treeCollapsed.value = !treeCollapsed.value;
  saveTreeState(treeWidth.value, treeCollapsed.value);
}

onMounted(() => {
  window.addEventListener('mousemove', onResizeMove);
  window.addEventListener('mouseup', stopResize);
});
onUnmounted(() => {
  window.removeEventListener('mousemove', onResizeMove);
  window.removeEventListener('mouseup', stopResize);
});

const page = computed(() => ({ title: mt('monitoring.control.pageTitle', 'Kontrol') }));
const breadcrumbs = computed(() => [
  { text: mt('breadcrumbs.home', 'Dashboard'), disabled: false, href: '/dashboards/analytical' },
  { text: mt('monitoring.pageTitle', 'Monitoring tanımları'), disabled: false, href: '/apps/monitoring' },
  { text: mt('monitoring.control.pageTitle', 'Kontrol'), disabled: true, href: '#' },
]);

/** Online eşiği: son X dakika içinde lastSeenAt varsa online */
const ONLINE_THRESHOLD_MINUTES = 10;

function isEngineOnline(engine: MonEngine): boolean {
  const v = engine.lastSeenAt;
  if (!v) return false;
  try {
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return false;
    const diffMs = Date.now() - d.getTime();
    return diffMs < ONLINE_THRESHOLD_MINUTES * 60 * 1000;
  } catch {
    return false;
  }
}

/** Türetilmiş health: offline ise unhealthy, değilse Engine'den gelen health */
function derivedEngineHealth(engine: MonEngine): string {
  if (!isEngineOnline(engine)) return 'offline';
  return (engine.health ?? (engine.lastErrors?.length ? 'degraded' : 'ok')).toLowerCase();
}

function engineHealthLabel(health: string): string {
  if (health === 'offline') return mt('monitoring.control.healthOffline', 'Bağlantı yok');
  if (health === 'degraded') return mt('monitoring.engines.healthDegraded', 'Bozuk');
  if (health === 'error') return mt('monitoring.engines.healthError', 'Hata');
  return mt('monitoring.engines.healthOk', 'İyi');
}

function engineHealthColor(health: string): string {
  if (health === 'offline') return 'error';
  if (health === 'degraded') return 'warning';
  if (health === 'error') return 'error';
  return 'success';
}

function formatLastSeen(v: string | null | undefined): string {
  if (!v) return '—';
  try {
    const d = new Date(v);
    return Number.isNaN(d.getTime()) ? v : d.toLocaleString('tr-TR');
  } catch {
    return v;
  }
}

/** Relative time: "5 dakika önce", "az önce" */
function formatRelativeTime(v: string | null | undefined): string {
  if (!v) return '—';
  try {
    const d = new Date(v);
    if (Number.isNaN(d.getTime())) return v;
    const diffMs = Date.now() - d.getTime();
    const diffMin = Math.floor(diffMs / 60000);
    const diffHour = Math.floor(diffMin / 60);
    if (diffMin < 1) return mt('monitoring.control.justNow', 'az önce');
    if (diffMin < 60) return mtParam('monitoring.control.minutesAgo', { n: diffMin }, `${diffMin} dakika önce`);
    return mtParam('monitoring.control.hoursAgo', { n: diffHour }, `${diffHour} saat önce`);
  } catch {
    return v;
  }
}

function statusLabel(s: string): string {
  const map: Record<string, string> = {
    active: mt('monitoring.engines.statusActive', 'Aktif'),
    inactive: mt('monitoring.engines.statusInactive', 'Pasif'),
    maintenance: mt('monitoring.engines.statusMaintenance', 'Bakımda'),
  };
  return map[s] ?? s;
}

const engineNameById = computed(() => {
  const m = new Map<string, string>();
  store.engines.forEach((e) => m.set(e.__dataId, e.name));
  return m;
});

const agentCountByEngine = computed(() => {
  const m = new Map<string, number>();
  store.agents.forEach((a) => {
    const id = a.engineId;
    m.set(id, (m.get(id) ?? 0) + 1);
  });
  return m;
});

const agentsWithMeta = computed(() =>
  store.agents.map((a) => ({
    ...a,
    engineName: engineNameById.value.get(a.engineId) ?? a.engineId,
    assetCount: (a.asset_configs ?? []).filter((c) => c.active).length,
  }))
);

// Engine filtre (Agent tablosu için)
const activeTab = ref<'assetExplorer' | 'status'>('assetExplorer');
const selectedEngineFilter = ref<string | null>(null);
const filteredAgents = computed(() => {
  if (!selectedEngineFilter.value) return agentsWithMeta.value;
  return agentsWithMeta.value.filter((a) => a.engineId === selectedEngineFilter.value);
});

const engineFilterOptions = computed(() => [
  { title: mt('monitoring.control.allEngines', 'Tümü'), value: null },
  ...store.engines.map((e) => ({ title: e.name, value: e.__dataId })),
]);

// Config String modal
const configStringModalOpen = ref(false);
const configStringEngineId = ref('');
const configStringEngineName = ref('');
const configStringValue = ref('');
const configStringLoading = ref(false);
const configStringError = ref('');
const configStringCopied = ref(false);

async function openConfigStringModal(engine: MonEngine) {
  configStringEngineId.value = engine.__dataId;
  configStringEngineName.value = engine.name ?? '';
  configStringValue.value = '';
  configStringError.value = '';
  configStringModalOpen.value = true;
  configStringLoading.value = true;
  try {
    const res = await $fetch<{ configString?: string }>('/api/reactor/v1/engine/config-string', {
      query: { engineId: engine.__dataId },
      credentials: 'include',
    });
    configStringValue.value = res?.configString ?? (res as any) ?? '';
    if (!configStringValue.value && typeof res === 'string') configStringValue.value = res;
  } catch (e: any) {
    configStringError.value = e?.data?.message ?? e?.message ?? mt('monitoring.engines.configStringError', 'Config string alınamadı.');
  } finally {
    configStringLoading.value = false;
  }
}

async function copyConfigStringToClipboard() {
  if (!configStringValue.value) return;
  try {
    await navigator.clipboard.writeText(configStringValue.value);
    configStringCopied.value = true;
    setTimeout(() => { configStringCopied.value = false; }, 2000);
  } catch {
    // fallback
  }
}

function closeConfigStringModal() {
  configStringModalOpen.value = false;
  configStringEngineId.value = '';
  configStringValue.value = '';
  configStringError.value = '';
}

// Health status
const healthStatus = ref<Record<string, { status: string; message?: string }>>({});
const healthLoading = ref(false);

async function loadHealthStatus() {
  healthLoading.value = true;
  healthStatus.value = {};
  try {
    const res = await $fetch<{ status?: string; checks?: Record<string, { status?: string; message?: string }> }>('/api/reactor/v1/health', {
      credentials: 'include',
    }).catch(() => ({ status: 'unhealthy', checks: {} }));
    const checks = res?.checks ?? {};
    const entries: Record<string, { status: string; message?: string }> = {};
    for (const [k, v] of Object.entries(checks)) {
      const entry = v as { status?: string; message?: string };
      entries[k] = { status: entry?.status ?? 'unknown', message: entry?.message };
    }
    entries['reactor'] = { status: res?.status ?? 'unknown', message: res?.status === 'healthy' ? 'Bağlı' : 'Bağlantı yok' };
    healthStatus.value = entries;
  } catch {
    healthStatus.value = { reactor: { status: 'unhealthy', message: 'Reactor erişilemedi' } };
  } finally {
    healthLoading.value = false;
  }
}

const lastRefreshedAt = ref<Date | null>(null);
/** Varsayılan: otomatik yenileme açık (Engine offline iken refresh zorunda kalmamak için) */
const autoRefreshEnabled = ref(true);
const AUTO_REFRESH_INTERVAL_MS = 30000; // 30 saniye
let autoRefreshTimer: ReturnType<typeof setInterval> | null = null;

async function refresh() {
  await Promise.all([
    store.loadEnginesAndAgents(),
    orgStore.loadAll(),
    loadHealthStatus(),
  ]);
  lastRefreshedAt.value = new Date();
}

function handleControlNodeSelect(node: OrganizationSelectedNode) {
  controlSelectedNode.value = node;
}

function controlExpandAll() {
  controlTreeViewRef.value?.expandAll();
}

function controlCollapseAll() {
  controlTreeViewRef.value?.collapseAll();
}

function findNodeInTree(nodes: any[], id: string, type: 'item' | 'asset'): any {
  for (const n of nodes) {
    if (n.type === type && n.data.__dataId === id) return n;
    if (n.type === 'item' && n.children?.length) {
      const found = findNodeInTree(n.children, id, type);
      if (found) return found;
    }
  }
  return null;
}

/** Seçili node için alt öğe sayısı (Item ise) */
const itemChildCount = computed(() => {
  const n = controlSelectedNode.value;
  if (n?.type !== 'item' || !n.data?.__dataId) return 0;
  const node = findNodeInTree(orgStore.treeNodes, n.data.__dataId, 'item');
  return node && node.type === 'item' ? node.children.length : 0;
});

// Özet istatistikler
const onlineEngineCount = computed(() => store.engines.filter((e) => isEngineOnline(e)).length);
const totalAssetCount = computed(() =>
  store.agents.reduce((sum, a) => sum + (a.asset_configs ?? []).filter((c) => c.active).length, 0)
);

// Engine durumu donut chart
const engineChartSeries = computed(() => {
  const online = onlineEngineCount.value;
  const offline = store.engines.length - online;
  return [online, offline];
});
const engineChartLabels = computed(() => [
  mt('monitoring.control.online', 'Çevrimiçi'),
  mt('monitoring.control.offline', 'Çevrimdışı'),
]);
const engineChartOptions = computed(() => ({
  chart: { type: 'donut' as const },
  labels: engineChartLabels.value,
  colors: ['#22c55e', '#94a3b8'],
  legend: { position: 'bottom' },
  plotOptions: {
    pie: {
      donut: { size: '65%' },
      dataLabels: { minAngleToShowLabel: 10 },
    },
  },
}));

// Engine başına Agent bar chart
const agentsByEngineChartSeries = computed(() => [
  {
    name: mt('monitoring.control.agentCount', 'Agent sayısı'),
    data: store.engines.map((e) => agentCountByEngine.value.get(e.__dataId) ?? 0),
  },
]);
const agentsByEngineChartCategories = computed(() => store.engines.map((e) => e.name));
const agentsByEngineChartOptions = computed(() => ({
  chart: { type: 'bar' as const },
  plotOptions: {
    bar: { horizontal: false, columnWidth: '60%', borderRadius: 4 },
  },
  dataLabels: { enabled: false },
  xaxis: { categories: agentsByEngineChartCategories.value },
  colors: ['#6366f1'],
}));

function startAutoRefresh() {
  if (autoRefreshTimer) return;
  autoRefreshTimer = setInterval(() => refresh(), AUTO_REFRESH_INTERVAL_MS);
}
function stopAutoRefresh() {
  if (autoRefreshTimer) {
    clearInterval(autoRefreshTimer);
    autoRefreshTimer = null;
  }
}
function toggleAutoRefresh() {
  autoRefreshEnabled.value = !autoRefreshEnabled.value;
  if (autoRefreshEnabled.value && typeof document !== 'undefined' && document.visibilityState === 'visible') {
    startAutoRefresh();
  } else {
    stopAutoRefresh();
  }
}
function onVisibilityChange() {
  if (typeof document === 'undefined') return;
  if (document.visibilityState === 'visible' && autoRefreshEnabled.value) {
    startAutoRefresh();
    refresh(); // Sekme tekrar görünür olunca hemen yenile
  } else {
    stopAutoRefresh();
  }
}

onUnmounted(() => {
  stopAutoRefresh();
  if (typeof document !== 'undefined') document.removeEventListener('visibilitychange', onVisibilityChange);
});

const engineHeaders = computed(() => [
  { title: mt('monitoring.engines.tableName', 'Ad'), key: 'name', sortable: true },
  { title: mt('monitoring.control.connectionStatus', 'Bağlantı'), key: 'connectionStatus', sortable: false },
  { title: mt('monitoring.engines.tableHealth', 'Sağlık'), key: 'health', sortable: false },
  { title: mt('monitoring.engines.tableHostAddress', 'IP'), key: 'hostAddress', sortable: false },
  { title: mt('monitoring.engines.tableLastSeenAt', 'Son görülme'), key: 'lastSeenAt', sortable: false },
  { title: mt('monitoring.engines.tableErrors', 'Hatalar'), key: 'errors', sortable: false },
  { title: mt('monitoring.control.agentCount', 'Agent sayısı'), key: 'agentCount', sortable: false },
  { title: mt('monitoring.control.actions', 'İşlemler'), key: 'actions', sortable: false, align: 'end' as const },
]);

const agentHeaders = computed(() => [
  { title: mt('monitoring.agents.tableName', 'Ad'), key: 'name', sortable: true },
  { title: mt('monitoring.agents.tableEngine', 'Engine'), key: 'engineName', sortable: false },
  { title: mt('monitoring.agents.tableStatus', 'Durum'), key: 'status', sortable: false },
  { title: mt('monitoring.agents.tableAssetCount', 'Asset sayısı'), key: 'assetCount', sortable: false },
]);

const enginesWithMeta = computed(() =>
  store.engines.map((e) => ({
    ...e,
    connectionStatus: isEngineOnline(e) ? 'online' : 'offline',
    derivedHealth: derivedEngineHealth(e),
    agentCount: agentCountByEngine.value.get(e.__dataId) ?? 0,
  }))
);

const slotName = 'item.name';
const slotConnectionStatus = 'item.connectionStatus';
const slotHealth = 'item.health';
const slotHostAddress = 'item.hostAddress';
const slotLastSeenAt = 'item.lastSeenAt';
const slotErrors = 'item.errors';
const slotAgentCount = 'item.agentCount';
const slotActions = 'item.actions';
const slotEngineName = 'item.engineName';
const slotStatus = 'item.status';
const slotAssetCount = 'item.assetCount';

onMounted(async () => {
  await refresh();
  if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', onVisibilityChange);
    if (autoRefreshEnabled.value && document.visibilityState === 'visible') startAutoRefresh();
  }
});
</script>

<template>
  <div class="control-page">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-container fluid>
      <v-alert v-if="store.error" type="error" variant="tonal" dismissible class="mb-4" @click:close="store.clearError">
        {{ store.error }}
      </v-alert>
      <v-alert v-if="activeTab === 'assetExplorer' && orgStore.error" type="warning" variant="tonal" dismissible class="mb-4" @click:close="orgStore.error = null">
        {{ orgStore.error }}
      </v-alert>

      <div class="d-flex align-center gap-2 mb-4">
        <v-btn variant="outlined" size="small" to="/apps/monitoring/map">
          {{ mt('monitoring.map.pageTitle', 'Harita') }}
        </v-btn>
      </div>

      <!-- Özet stat kartları -->
      <v-row class="mb-4" dense>
        <v-col cols="12" sm="6" md="4" lg="2">
          <v-skeleton-loader v-if="store.loading && !lastRefreshedAt" type="card" />
          <v-card v-else variant="outlined" class="pa-3 stat-card" height="100%">
            <div class="d-flex align-center gap-3">
              <v-avatar color="primary" variant="tonal" size="48" rounded>
                <DeviceDesktopIcon size="24" />
              </v-avatar>
              <div>
                <div class="text-caption text-medium-emphasis">{{ mt('monitoring.control.totalEngines', 'Toplam Engine') }}</div>
                <div class="text-h5 font-weight-bold">{{ store.engines.length }}</div>
              </div>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="4" lg="2">
          <v-skeleton-loader v-if="store.loading && !lastRefreshedAt" type="card" />
          <v-card v-else variant="outlined" class="pa-3 stat-card" height="100%">
            <div class="d-flex align-center gap-3">
              <v-avatar color="success" variant="tonal" size="48" rounded>
                <CheckIcon size="24" />
              </v-avatar>
              <div>
                <div class="text-caption text-medium-emphasis">{{ mt('monitoring.control.onlineEngines', 'Çevrimiçi') }}</div>
                <div class="text-h5 font-weight-bold text-success">{{ onlineEngineCount }}</div>
              </div>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="4" lg="2">
          <v-skeleton-loader v-if="store.loading && !lastRefreshedAt" type="card" />
          <v-card v-else variant="outlined" class="pa-3 stat-card" height="100%">
            <div class="d-flex align-center gap-3">
              <v-avatar color="info" variant="tonal" size="48" rounded>
                <UsersIcon size="24" />
              </v-avatar>
              <div>
                <div class="text-caption text-medium-emphasis">{{ mt('monitoring.control.totalAgents', 'Toplam Agent') }}</div>
                <div class="text-h5 font-weight-bold">{{ store.agents.length }}</div>
              </div>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="4" lg="2">
          <v-skeleton-loader v-if="store.loading && !lastRefreshedAt" type="card" />
          <v-card v-else variant="outlined" class="pa-3 stat-card" height="100%">
            <div class="d-flex align-center gap-3">
              <v-avatar color="warning" variant="tonal" size="48" rounded>
                <BoxIcon size="24" />
              </v-avatar>
              <div>
                <div class="text-caption text-medium-emphasis">{{ mt('monitoring.control.totalAssets', 'Aktif Asset') }}</div>
                <div class="text-h5 font-weight-bold">{{ totalAssetCount }}</div>
              </div>
            </div>
          </v-card>
        </v-col>
        <v-col cols="12" sm="6" md="4" lg="4">
          <v-skeleton-loader v-if="healthLoading" type="card" />
          <v-card v-else variant="outlined" class="pa-3 stat-card" height="100%">
            <div class="d-flex align-center gap-3 flex-nowrap">
              <div class="d-flex align-center gap-3 flex-shrink-0">
                <v-avatar
                  :color="(healthStatus.reactor?.status ?? 'unknown') === 'healthy' ? 'success' : 'error'"
                  variant="tonal"
                  size="48"
                  rounded
                >
                  <ActivityHeartbeatIcon size="24" />
                </v-avatar>
                <div>
                  <div class="text-caption text-medium-emphasis">{{ mt('monitoring.control.reactorHealth', 'Reactor') }}</div>
                  <div class="text-h5 font-weight-bold" :class="(healthStatus.reactor?.status ?? 'unknown') === 'healthy' ? 'text-success' : 'text-error'">
                    {{ healthStatus.reactor?.status === 'healthy' ? mt('monitoring.control.healthHealthy', 'Sağlıklı') : mt('monitoring.control.healthUnhealthy', 'Sağlıksız') }}
                  </div>
                </div>
              </div>
              <v-spacer />
              <div class="d-flex flex-column align-end gap-1 flex-shrink-0">
                <span v-if="lastRefreshedAt" class="text-caption text-medium-emphasis">
                  {{ mt('monitoring.control.lastRefreshed', 'Son güncelleme') }}: {{ formatRelativeTime(lastRefreshedAt.toISOString()) }}
                </span>
                <div class="d-flex align-center gap-2">
                  <v-btn variant="text" size="small" :color="autoRefreshEnabled ? 'primary' : undefined" @click="toggleAutoRefresh">
                    {{ mt('monitoring.control.autoRefresh', 'Otomatik yenile') }} (30s)
                  </v-btn>
                  <v-btn variant="outlined" size="small" :disabled="store.loading || healthLoading" @click="refresh">
                    <RefreshIcon size="18" class="mr-1" /> {{ mt('monitoring.common.refresh', 'Yenile') }}
                  </v-btn>
                </div>
              </div>
            </div>
          </v-card>
        </v-col>
      </v-row>

      <v-tabs v-model="activeTab" class="mb-4">
        <v-tab value="assetExplorer">{{ mt('monitoring.control.assetExplorer', 'Asset Explorer') }}</v-tab>
        <v-tab value="status">{{ mt('monitoring.control.engineAndAgentStatus', 'Engine & Agent durumu') }}</v-tab>
      </v-tabs>

      <v-window v-model="activeTab">
        <!-- Asset Explorer: Tree (resize + collapse) + Detay (ilk sekme) -->
        <v-window-item value="assetExplorer">
          <div class="d-flex" style="min-height: 400px;">
            <!-- Tree panel (resizable) – genişletildiğinde -->
            <template v-if="!treeCollapsed">
              <div class="flex-shrink-0 overflow-hidden" :style="{ width: treeWidth + 'px' }">
                <v-card variant="outlined" class="h-100 d-flex flex-column">
                  <v-card-title class="d-flex align-center py-3">
                    <span class="text-subtitle-1">{{ mt('monitoring.control.organizationTree', 'Organizasyon ağacı') }}</span>
                    <v-spacer />
                    <v-btn icon size="small" variant="text" :title="mt('monitoring.control.collapseTree', 'Ağacı gizle')" @click="toggleTreeCollapse">
                      <LayoutSidebarLeftCollapseIcon size="18" />
                    </v-btn>
                    <v-btn icon size="small" variant="text" :title="mt('monitoring.control.expandAll', 'Tümünü aç')" @click="controlExpandAll">
                      <ChevronDownIcon size="18" />
                    </v-btn>
                    <v-btn icon size="small" variant="text" :title="mt('monitoring.control.collapseAll', 'Tümünü kapat')" @click="controlCollapseAll">
                      <ChevronUpIcon size="18" />
                    </v-btn>
                  </v-card-title>
                  <v-divider />
                  <v-card-text class="pa-0 flex-grow-1 overflow-auto" style="max-height: calc(100vh - 420px);">
                    <OrganizationTreeView
                      ref="controlTreeViewRef"
                      :items="orgStore.treeNodes"
                      :selected-node="controlSelectedNode"
                      :loading="orgStore.loading"
                      @node-select="handleControlNodeSelect"
                    />
                  </v-card-text>
                </v-card>
              </div>
              <!-- Resize handle -->
              <div
                class="tree-resize-handle flex-shrink-0"
                :class="{ 'tree-resize-active': resizeActive }"
                @mousedown="startResize"
              />
            </template>
            <!-- Detay paneli -->
            <div class="flex-grow-1 min-width-0">
              <v-card variant="outlined" class="h-100">
                <v-card-title class="d-flex align-center py-3">
                  <v-btn
                    v-if="treeCollapsed"
                    icon
                    variant="tonal"
                    size="small"
                    class="mr-2"
                    :title="mt('monitoring.control.showTree', 'Ağacı göster')"
                    @click="toggleTreeCollapse"
                  >
                    <LayoutSidebarLeftExpandIcon size="20" />
                  </v-btn>
                  <span>{{ mt('monitoring.control.detailPanel', 'Detay') }}</span>
                </v-card-title>
                <v-divider />
                <v-card-text class="pa-4">
                  <ControlAssetDetailPanel
                    :selected-node="controlSelectedNode"
                    :item-child-count="itemChildCount"
                    :tree-nodes="orgStore.treeNodes"
                    :mt="mt"
                  />
                </v-card-text>
              </v-card>
            </div>
          </div>
        </v-window-item>

        <!-- Engine & Agent durumu: Grafikler + Tablolar -->
        <v-window-item value="status">
          <div class="d-flex flex-column gap-4">
            <!-- Grafikler -->
            <v-row v-if="store.engines.length > 0" dense>
              <v-col cols="12" md="5">
                <v-card variant="outlined" class="pa-3">
                  <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.control.engineDistribution', 'Engine bağlantı dağılımı') }}</div>
                  <ClientOnly>
                    <template v-if="engineChartSeries.some((s) => s > 0)">
                      <apexchart type="donut" height="220" :options="engineChartOptions" :series="engineChartSeries" />
                    </template>
                    <div v-else class="d-flex align-center justify-center py-8 text-medium-emphasis">
                      {{ mt('monitoring.engines.noData', 'Henüz engine tanımı yok.') }}
                    </div>
                    <template #fallback>
                      <div class="d-flex align-center justify-center py-8"><v-progress-circular indeterminate color="primary" /></div>
                    </template>
                  </ClientOnly>
                </v-card>
              </v-col>
              <v-col cols="12" md="7">
                <v-card variant="outlined" class="pa-3">
                  <div class="text-subtitle-2 text-medium-emphasis mb-2">{{ mt('monitoring.control.agentsByEngine', 'Engine başına Agent') }}</div>
                  <ClientOnly>
                    <template v-if="store.engines.length > 0">
                      <apexchart type="bar" height="220" :options="agentsByEngineChartOptions" :series="agentsByEngineChartSeries" />
                    </template>
                    <div v-else class="d-flex align-center justify-center py-8 text-medium-emphasis">
                      {{ mt('monitoring.engines.noData', 'Henüz engine tanımı yok.') }}
                    </div>
                    <template #fallback>
                      <div class="d-flex align-center justify-center py-8"><v-progress-circular indeterminate color="primary" /></div>
                    </template>
                  </ClientOnly>
                </v-card>
              </v-col>
            </v-row>

            <!-- Engine tablosu -->
            <v-card>
              <v-card-title class="text-subtitle-1 py-3">{{ mt('monitoring.control.engineStatus', 'Engine durumu') }}</v-card-title>
              <v-divider />
              <v-card-text>
                <v-data-table :headers="engineHeaders" :items="enginesWithMeta" :loading="store.loading" item-value="__dataId" class="border rounded" show-expand>
                  <template #[slotName]="{ item }"><span class="font-weight-medium">{{ item.name }}</span></template>
                  <template #[slotConnectionStatus]="{ item }">
                    <v-chip
                      size="small"
                      :color="item.connectionStatus === 'online' ? 'success' : 'error'"
                      variant="tonal"
                    >
                      {{ item.connectionStatus === 'online' ? mt('monitoring.control.online', 'Çevrimiçi') : mt('monitoring.control.offline', 'Çevrimdışı') }}
                    </v-chip>
                  </template>
                  <template #[slotHealth]="{ item }">
                    <v-chip size="small" :color="engineHealthColor(item.derivedHealth)" variant="tonal">
                      {{ engineHealthLabel(item.derivedHealth) }}
                    </v-chip>
                  </template>
                  <template #[slotHostAddress]="{ item }"><span class="text-caption font-mono">{{ item.hostAddress || '—' }}</span></template>
                  <template #[slotLastSeenAt]="{ item }">
                    <span class="text-caption" :title="formatLastSeen(item.lastSeenAt)">{{ formatRelativeTime(item.lastSeenAt) }}</span>
                  </template>
                  <template #[slotErrors]="{ item }">
                    <template v-if="(item.lastErrors?.length ?? 0) > 0">
                      <v-chip size="small" color="warning" variant="tonal">{{ (item.lastErrors?.length ?? 0) }} {{ mt('monitoring.engines.errorsCount', 'hata') }}</v-chip>
                    </template>
                    <span v-else class="text-caption text-medium-emphasis">—</span>
                  </template>
                  <template #expanded-row="{ columns, item }">
                    <tr v-if="(item.lastErrors?.length ?? 0) > 0">
                      <td :colspan="columns.length">
                        <div class="pa-3">
                          <div class="text-caption text-medium-emphasis mb-2">{{ mt('monitoring.engines.lastErrorsTitle', 'Son toplama hataları') }}</div>
                          <v-list density="compact" class="bg-transparent">
                            <v-list-item v-for="(err, idx) in (item.lastErrors ?? []).slice(0, 10)" :key="idx" class="text-caption">
                              <template #prepend>
                                <v-icon size="16" color="error" class="mr-2">mdi-alert-circle</v-icon>
                              </template>
                              <v-list-item-title>{{ err.errorCode }}: {{ (err.message ?? '').slice(0, 60) }}{{ (err.message?.length ?? 0) > 60 ? '…' : '' }}</v-list-item-title>
                              <v-list-item-subtitle>{{ mt('monitoring.engines.errorAsset', 'Asset') }}: {{ err.assetId }} · {{ formatLastSeen(err.occurredAt) }}</v-list-item-subtitle>
                            </v-list-item>
                          </v-list>
                          <p v-if="(item.lastErrors?.length ?? 0) > 10" class="text-caption text-medium-emphasis mt-2">
                            {{ mt('monitoring.engines.moreErrors', '... ve {n} hata daha').replace('{n}', String((item.lastErrors?.length ?? 0) - 10)) }}
                          </p>
                        </div>
                      </td>
                    </tr>
                  </template>
                  <template #[slotAgentCount]="{ item }">{{ item.agentCount }}</template>
                  <template #[slotActions]="{ item }">
                    <v-btn
                      v-if="canEdit"
                      icon
                      size="small"
                      variant="text"
                      :title="mt('monitoring.engines.configStringButton', 'Config string')"
                      @click="openConfigStringModal(item)"
                    >
                      <KeyIcon size="18" />
                    </v-btn>
                  </template>
                  <template #no-data>
                    <div class="text-center py-6 text-medium-emphasis">{{ mt('monitoring.engines.noData', 'Henüz engine tanımı yok.') }}</div>
                  </template>
                </v-data-table>
              </v-card-text>
            </v-card>

            <!-- Agent tablosu -->
            <v-card>
              <v-card-title class="d-flex flex-wrap align-center gap-2 py-3">
                <span class="text-subtitle-1">{{ mt('monitoring.control.agentStatus', 'Agent durumu') }}</span>
                <v-spacer />
                <v-select
                  v-model="selectedEngineFilter"
                  :items="engineFilterOptions"
                  item-title="title"
                  item-value="value"
                  :label="mt('monitoring.control.filterByEngine', 'Engine filtresi')"
                  variant="outlined"
                  density="compact"
                  hide-details
                  clearable
                  style="max-width: 220px;"
                />
              </v-card-title>
              <v-divider />
              <v-card-text>
                <v-data-table :headers="agentHeaders" :items="filteredAgents" :loading="store.loading" item-value="__dataId" class="border rounded">
                  <template #[slotName]="{ item }"><span class="font-weight-medium">{{ item.name }}</span></template>
                  <template #[slotEngineName]="{ item }">{{ item.engineName }}</template>
                  <template #[slotStatus]="{ item }">
                    <v-chip size="small" :color="item.status === 'active' ? 'success' : item.status === 'maintenance' ? 'warning' : 'default'" variant="tonal">
                      {{ statusLabel(item.status) }}
                    </v-chip>
                  </template>
                  <template #[slotAssetCount]="{ item }">{{ item.assetCount }}</template>
                  <template #no-data>
                    <div class="text-center py-6 text-medium-emphasis">{{ mt('monitoring.agents.noData', 'Henüz agent tanımı yok.') }}</div>
                  </template>
                </v-data-table>
              </v-card-text>
            </v-card>
          </div>
        </v-window-item>
      </v-window>

      <!-- Config string modal -->
      <v-dialog v-model="configStringModalOpen" max-width="560" persistent>
        <v-card>
          <v-card-title class="d-flex align-center">
            <KeyIcon size="24" class="mr-2" />
            {{ mt('monitoring.engines.configStringTitle', 'Config string') }}
            <v-chip v-if="configStringEngineName" size="small" class="ml-2" variant="tonal">{{ configStringEngineName }}</v-chip>
          </v-card-title>
          <v-card-text>
            <p class="text-body-2 text-medium-emphasis mb-2">{{ mt('monitoring.engines.configStringHint', 'Bu metni kopyalayıp MngEngine uygulamasına yapıştırın.') }}</p>
            <v-progress-linear v-if="configStringLoading" indeterminate color="primary" class="mb-2" />
            <v-alert v-else-if="configStringError" type="error" variant="tonal" density="compact" class="mb-2">
              {{ configStringError }}
            </v-alert>
            <v-textarea
              v-else
              :model-value="configStringValue"
              readonly
              variant="outlined"
              rows="6"
              class="font-mono text-caption"
              hide-details
              auto-grow
            />
          </v-card-text>
          <v-card-actions>
            <v-spacer />
            <v-btn variant="text" @click="closeConfigStringModal">{{ mt('monitoring.common.cancel', 'İptal') }}</v-btn>
            <v-btn
              color="primary"
              variant="flat"
              :disabled="!configStringValue || configStringLoading"
              @click="copyConfigStringToClipboard"
            >
              {{ configStringCopied ? mt('monitoring.engines.configStringCopied', 'Kopyalandı') : mt('monitoring.engines.configStringCopy', 'Kopyala') }}
            </v-btn>
          </v-card-actions>
        </v-card>
      </v-dialog>
    </v-container>
  </div>
</template>

<style scoped>
.control-page {
  min-height: calc(100vh - 100px);
}

/* Tree panel resize handle */
.tree-resize-handle {
  width: 6px;
  cursor: col-resize;
  background: transparent;
  transition: background 0.15s;
}
.tree-resize-handle:hover,
.tree-resize-handle.tree-resize-active {
  background: rgb(var(--v-theme-primary));
  opacity: 0.5;
}
.stat-card {
  transition: box-shadow 0.2s ease;
}
.stat-card:hover {
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
}
</style>
