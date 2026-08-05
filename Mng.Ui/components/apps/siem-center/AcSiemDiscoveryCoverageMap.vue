<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  useSiemDiscoveryData,
} from '@/composables/useSiemDiscoveryData';
import { DISCOVERY_FACETS, coverageColor } from '@/composables/useSiemDiscoveryMock';
import AcSiemDiscoveryHostDetailDialog from '@/components/apps/siem-center/AcSiemDiscoveryHostDetailDialog.vue';
import AcSiemDiscoveryHostNode from '@/components/apps/siem-center/AcSiemDiscoveryHostNode.vue';
import AcSiemDiscoveryScanDialog from '@/components/apps/siem-center/AcSiemDiscoveryScanDialog.vue';
import AcSiemDiscoveryPrefixesPanel from '@/components/apps/siem-center/AcSiemDiscoveryPrefixesPanel.vue';
import AcSiemDiscoveryLogSourcesPanel from '@/components/apps/siem-center/AcSiemDiscoveryLogSourcesPanel.vue';
import { clearDiscoveryHosts } from '@/services/siemDiscoveryService';
import type {
  SiemCoverageStatus,
  SiemDiscoveryBranch,
  SiemDiscoveryFacet,
  SiemDiscoveryHost,
} from '@/types/apps/siemDiscovery';

type LayoutMode = 'tree' | 'split' | 'tiers' | 'graph';
type DiscoverySegment = 'endpoints' | 'logSources';

const props = defineProps<{
  rootLabel?: string;
}>();

const LAYOUTS: LayoutMode[] = ['tree', 'split', 'tiers', 'graph'];
const LAYOUT_STORAGE_KEY = 'siem.discovery.layout';
const KPI_OPEN_STORAGE_KEY = 'siem.discovery.kpiOpen';
const SEGMENT_STORAGE_KEY = 'siem.discovery.segment';

function loadSegmentPreference(): DiscoverySegment {
  if (!import.meta.client) return 'endpoints';
  try {
    const raw = localStorage.getItem(SEGMENT_STORAGE_KEY);
    if (raw === 'logSources' || raw === 'endpoints') return raw;
  } catch {
    // ignore
  }
  return 'endpoints';
}

function loadLayoutPreference(): LayoutMode {
  if (!import.meta.client) return 'tree';
  try {
    const raw = localStorage.getItem(LAYOUT_STORAGE_KEY);
    if (raw && (LAYOUTS as string[]).includes(raw)) return raw as LayoutMode;
  } catch {
    // ignore
  }
  return 'tree';
}

function loadKpiOpenPreference(): boolean {
  if (!import.meta.client) return true;
  try {
    const raw = localStorage.getItem(KPI_OPEN_STORAGE_KEY);
    if (raw === '0' || raw === 'false') return false;
    if (raw === '1' || raw === 'true') return true;
  } catch {
    // ignore
  }
  return true;
}

const { t, locale } = useAppI18n();
const resolvedRootLabel = computed(() =>
  props.rootLabel?.trim() || t('siemCenter.discovery.rootLabel'),
);

const facet = ref<SiemDiscoveryFacet>('subnet');
const layout = ref<LayoutMode>(loadLayoutPreference());
const kpiOpen = ref(loadKpiOpenPreference());
const segment = ref<DiscoverySegment>(loadSegmentPreference());
const logSourcesPanel = ref<{ refresh: () => Promise<void> } | null>(null);

const {
  loading,
  syncing,
  coverageRefreshing,
  error,
  lastRefreshedAt,
  autoRefresh,
  pollIntervalMs,
  branches: rawBranches,
  kpis,
  refresh,
  syncNow,
  toggleAutoRefresh,
  staleMs,
} = useSiemDiscoveryData(facet);

const detailOpen = ref(false);
const selectedHost = ref<SiemDiscoveryHost | null>(null);
const scanOpen = ref(false);
const prefixesOpen = ref(false);
const clearOpen = ref(false);
const clearing = ref(false);
const clearScope = ref<'all' | 'scan' | 'ad'>('all');
const clearSnackbar = ref(false);
const clearSnackbarText = ref('');
const clearSnackbarColor = ref<'success' | 'error'>('success');

async function confirmClear() {
  clearing.value = true;
  try {
    const source = clearScope.value === 'all' ? '' : clearScope.value;
    const res = await clearDiscoveryHosts({ source });
    if (res.status === 'error' || res.error) {
      clearSnackbarColor.value = 'error';
      clearSnackbarText.value = res.error || t('siemCenter.discovery.clear.toastFailed');
      clearSnackbar.value = true;
      return;
    }
    clearOpen.value = false;
    clearSnackbarColor.value = 'success';
    clearSnackbarText.value = t('siemCenter.discovery.clear.toastOk', { n: res.deleted });
    clearSnackbar.value = true;
    await refresh();
  } catch (e: unknown) {
    clearSnackbarColor.value = 'error';
    clearSnackbarText.value =
      (e instanceof Error ? e.message : String(e)) || t('siemCenter.discovery.clear.toastFailed');
    clearSnackbar.value = true;
  } finally {
    clearing.value = false;
  }
}

/** Empty set = show all coverage statuses (multi-select toggle via legend). */
const coverageFilter = ref<Set<SiemCoverageStatus>>(new Set());

function isCoverageFilterActive(status: SiemCoverageStatus): boolean {
  return coverageFilter.value.has(status);
}

function toggleCoverageFilter(status: SiemCoverageStatus) {
  const next = new Set(coverageFilter.value);
  if (next.has(status)) next.delete(status);
  else next.add(status);
  coverageFilter.value = next;
}

function clearCoverageFilter() {
  coverageFilter.value = new Set();
}

function localizeBranchLabel(label: string): string {
  if (label === 'Unscoped') return t('siemCenter.discovery.site.unscoped');
  if (label === 'No IP') return t('siemCenter.discovery.site.noIp');
  return label;
}

const branches = computed((): SiemDiscoveryBranch[] => {
  const source =
    coverageFilter.value.size === 0
      ? rawBranches.value
      : rawBranches.value
          .map((b) => ({
            ...b,
            hosts: b.hosts.filter((h) => coverageFilter.value.has(h.coverage)),
          }))
          .filter((b) => b.hosts.length > 0);

  return source.map((b) => ({
    ...b,
    label: localizeBranchLabel(b.label),
  }));
});

const hostCount = computed(() => branches.value.reduce((n, b) => n + b.hosts.length, 0));
const filterActive = computed(() => coverageFilter.value.size > 0);

const lastRefreshedLabel = computed(() => {
  if (!lastRefreshedAt.value) return '';
  try {
    return new Intl.DateTimeFormat(locale.value === 'tr' ? 'tr-TR' : 'en-GB', {
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    }).format(new Date(lastRefreshedAt.value));
  } catch {
    return '';
  }
});

const pollSeconds = computed(() => Math.round(pollIntervalMs / 1000));

const expanded = ref<Set<string>>(new Set());
const selectedBranchId = ref<string | null>(null);

watch(layout, (value) => {
  if (!import.meta.client) return;
  try {
    localStorage.setItem(LAYOUT_STORAGE_KEY, value);
  } catch {
    // ignore
  }
});

watch(kpiOpen, (value) => {
  if (!import.meta.client) return;
  try {
    localStorage.setItem(KPI_OPEN_STORAGE_KEY, value ? '1' : '0');
  } catch {
    // ignore
  }
});

watch(segment, (value) => {
  if (!import.meta.client) return;
  try {
    localStorage.setItem(SEGMENT_STORAGE_KEY, value);
  } catch {
    // ignore
  }
});

const segmentItems = computed(() => [
  {
    id: 'endpoints' as const,
    title: t('siemCenter.discovery.segments.endpoints'),
    icon: 'mdi-desktop-classic',
  },
  {
    id: 'logSources' as const,
    title: t('siemCenter.discovery.segments.logSources'),
    icon: 'mdi-firewall',
  },
]);

const pageSubtitle = computed(() =>
  segment.value === 'logSources'
    ? t('siemCenter.discovery.logSources.pageSubtitle')
    : t('siemCenter.discovery.pageSubtitle'),
);

async function refreshActiveSegment() {
  if (segment.value === 'logSources') {
    await logSourcesPanel.value?.refresh();
    return;
  }
  await refresh();
}

function toggleKpiOpen() {
  kpiOpen.value = !kpiOpen.value;
}

const facetItems = computed(() =>
  DISCOVERY_FACETS.map((id) => ({
    id,
    title: t(`siemCenter.discovery.facets.${id}`),
  })),
);

const layoutItems = computed(() =>
  LAYOUTS.map((id) => ({
    id,
    title: t(`siemCenter.discovery.layouts.${id}`),
    icon:
      id === 'tree'
        ? 'mdi-file-tree'
        : id === 'split'
          ? 'mdi-view-split-vertical'
          : id === 'tiers'
            ? 'mdi-layers-triple'
            : 'mdi-vector-polyline',
  })),
);

const selectedBranch = computed((): SiemDiscoveryBranch | null => {
  if (!selectedBranchId.value) return null;
  return branches.value.find((b) => b.id === selectedBranchId.value) ?? null;
});

function syncDefaults() {
  const ids = branches.value.map((b) => b.id);
  expanded.value = new Set(ids.slice(0, 1));
  selectedBranchId.value = ids[0] ?? null;
}

watch(facet, syncDefaults, { immediate: true });

watch(branches, (list) => {
  if (!selectedBranchId.value) return;
  if (!list.some((b) => b.id === selectedBranchId.value)) {
    selectedBranchId.value = list[0]?.id ?? null;
  }
});

function isExpanded(id: string): boolean {
  return expanded.value.has(id);
}

function toggleBranch(id: string) {
  const next = new Set(expanded.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  expanded.value = next;
  selectedBranchId.value = id;
}

function selectBranch(id: string) {
  selectedBranchId.value = id;
  if (!expanded.value.has(id)) {
    const next = new Set(expanded.value);
    next.add(id);
    expanded.value = next;
  }
}

function expandAll() {
  expanded.value = new Set(branches.value.map((b) => b.id));
}

function collapseAll() {
  expanded.value = new Set();
}

function coverageDots(branch: SiemDiscoveryBranch) {
  const counts: Record<string, number> = {};
  for (const h of branch.hosts) {
    counts[h.coverage] = (counts[h.coverage] ?? 0) + 1;
  }
  return Object.entries(counts).map(([status, count]) => ({
    status,
    count,
    color: coverageColor(status as Parameters<typeof coverageColor>[0]),
  }));
}

function openHost(host: SiemDiscoveryHost) {
  selectedHost.value = host;
  detailOpen.value = true;
}

/**
 * Column schematic: each branch is a vertical lane; hosts stack under their branch.
 * Avoids horizontal overlap between adjacent expanded groups.
 */
const graphLayout = computed(() => {
  const list = branches.value;
  const PAD_X = 36;
  const PAD_Y = 28;
  const ROOT_H = 44;
  const BRANCH_W = 188;
  const BRANCH_H = 56;
  // Match AcSiemDiscoveryHostNode (wide) footprint used in other layouts.
  const HOST_W = 260;
  const HOST_H = 118;
  const HOST_GAP = 12;
  const COL_GAP = 28;
  const ROOT_TO_BRANCH = 56;
  const BRANCH_TO_HOST = 36;
  const COL_W = Math.max(BRANCH_W, HOST_W);

  const branchNodes: { branch: SiemDiscoveryBranch; x: number; y: number }[] = [];
  const hostNodes: { host: SiemDiscoveryHost; branchId: string; x: number; y: number }[] = [];

  let cursorX = PAD_X + COL_W / 2;
  let maxHostBottom = 0;

  const rootY = PAD_Y + ROOT_H / 2;
  const branchY = rootY + ROOT_H / 2 + ROOT_TO_BRANCH + BRANCH_H / 2;

  for (const b of list) {
    const x = cursorX;
    branchNodes.push({ branch: b, x, y: branchY });

    if (isExpanded(b.id)) {
      b.hosts.forEach((h, hi) => {
        const y =
          branchY + BRANCH_H / 2 + BRANCH_TO_HOST + HOST_H / 2 + hi * (HOST_H + HOST_GAP);
        hostNodes.push({ host: h, branchId: b.id, x, y });
        maxHostBottom = Math.max(maxHostBottom, y + HOST_H / 2);
      });
    }

    cursorX += COL_W + COL_GAP;
  }

  const contentWidth = Math.max(list.length, 1) * COL_W + Math.max(list.length - 1, 0) * COL_GAP;
  const width = Math.max(640, PAD_X * 2 + contentWidth);
  const rootX = width / 2;
  const height = Math.max(
    320,
    (hostNodes.length ? maxHostBottom : branchY + BRANCH_H / 2) + PAD_Y,
  );

  return {
    width,
    height,
    root: { x: rootX, y: rootY },
    branches: branchNodes,
    hosts: hostNodes,
    metrics: { BRANCH_W, BRANCH_H, HOST_W, HOST_H, ROOT_H },
  };
});
</script>

<template>
  <div class="discovery-map">
    <div class="discovery-header mb-3 pa-3 pa-md-4 rounded-lg">
      <div class="d-flex flex-wrap align-start justify-space-between ga-3">
        <div class="min-w-0">
          <h1 class="text-h5 font-weight-bold mb-1">
            {{ t('siemCenter.discovery.pageTitle') }}
          </h1>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ pageSubtitle }}
            <span
              v-if="segment === 'endpoints' && facetItems.length === 1"
              class="text-medium-emphasis"
            >
              · {{ t('siemCenter.discovery.toolbar.groupFixed', { name: facetItems[0]?.title }) }}
            </span>
          </p>
        </div>

        <div class="d-flex flex-wrap align-center ga-2 flex-shrink-0">
          <v-btn-toggle
            v-model="segment"
            mandatory
            density="compact"
            variant="outlined"
            divided
            color="primary"
            class="discovery-segment-toggle"
          >
            <v-btn
              v-for="item in segmentItems"
              :key="item.id"
              :value="item.id"
              size="small"
              class="text-none"
            >
              <v-icon :icon="item.icon" start size="18" />
              {{ item.title }}
            </v-btn>
          </v-btn-toggle>
          <template v-if="segment === 'endpoints'">
          <v-select
            v-model="layout"
            :items="layoutItems"
            item-title="title"
            item-value="id"
            :label="t('siemCenter.discovery.toolbar.layout')"
            variant="outlined"
            density="compact"
            hide-details
            class="toolbar-layout-select"
          >
            <template #selection="{ item }">
              <span class="d-flex align-center ga-2 text-body-2">
                <v-icon :icon="item.raw.icon" size="18" />
                {{ item.title }}
              </span>
            </template>
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps" :prepend-icon="item.raw.icon" />
            </template>
          </v-select>
          <v-select
            v-if="facetItems.length > 1"
            v-model="facet"
            :items="facetItems"
            item-title="title"
            item-value="id"
            :label="t('siemCenter.discovery.toolbar.groupBy')"
            variant="outlined"
            density="compact"
            hide-details
            class="toolbar-layout-select"
          />
          <v-btn
            size="small"
            variant="flat"
            color="primary"
            prepend-icon="mdi-radar"
            @click="scanOpen = true"
          >
            {{ t('siemCenter.discovery.scan.button') }}
          </v-btn>
          <v-menu location="bottom end">
            <template #activator="{ props: menuProps }">
              <v-btn
                v-bind="menuProps"
                size="small"
                variant="tonal"
                prepend-icon="mdi-dots-vertical"
                append-icon="mdi-chevron-down"
              >
                {{ t('siemCenter.discovery.toolbar.more') }}
              </v-btn>
            </template>
            <v-list density="compact" min-width="220" class="discovery-actions-menu">
              <v-list-item
                prepend-icon="mdi-ip-network"
                :title="t('siemCenter.settings.tabs.prefixes')"
                @click="prefixesOpen = true"
              />
              <v-list-item
                prepend-icon="mdi-arrow-expand-vertical"
                :title="t('siemCenter.discovery.expandAll')"
                @click="expandAll"
              />
              <v-list-item
                prepend-icon="mdi-arrow-collapse-vertical"
                :title="t('siemCenter.discovery.collapseAll')"
                @click="collapseAll"
              />
              <v-list-item
                prepend-icon="mdi-refresh"
                :title="t('siemCenter.discovery.toolbar.refreshShort')"
                :disabled="loading || coverageRefreshing"
                @click="refreshActiveSegment"
              >
                <template v-if="lastRefreshedLabel" #append>
                  <v-tooltip location="left">
                    <template #activator="{ props: tip }">
                      <span v-bind="tip" class="text-caption text-medium-emphasis">
                        {{ lastRefreshedLabel }}
                      </span>
                    </template>
                    {{ t('siemCenter.discovery.refreshCoverage') }}
                  </v-tooltip>
                </template>
              </v-list-item>
              <v-list-item
                :prepend-icon="autoRefresh ? 'mdi-broadcast' : 'mdi-broadcast-off'"
                :title="
                  autoRefresh
                    ? t('siemCenter.discovery.toolbar.autoOn')
                    : t('siemCenter.discovery.toolbar.autoOff')
                "
                @click="toggleAutoRefresh"
              >
                <template #append>
                  <span
                    class="menu-status-dot"
                    :class="autoRefresh ? 'is-on' : 'is-off'"
                    aria-hidden="true"
                  />
                </template>
              </v-list-item>
              <v-divider class="my-1" />
              <v-list-item
                prepend-icon="mdi-sync"
                :title="t('siemCenter.discovery.syncNow')"
                :disabled="syncing"
                @click="syncNow"
              />
              <v-list-item
                prepend-icon="mdi-delete-sweep"
                :title="t('siemCenter.discovery.clear.button')"
                base-color="error"
                @click="clearOpen = true"
              />
            </v-list>
          </v-menu>
          </template>
        </div>
      </div>
    </div>

    <v-alert v-if="segment === 'endpoints' && error" type="warning" variant="tonal" density="comfortable" class="mb-3">
      {{ t('siemCenter.discovery.coverageLoadError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <AcSiemDiscoveryLogSourcesPanel
      v-if="segment === 'logSources'"
      ref="logSourcesPanel"
      class="mb-3"
    />

    <template v-if="segment === 'endpoints'">
    <AcSiemDiscoveryScanDialog v-model:open="scanOpen" @completed="refresh" />

    <v-dialog v-model="prefixesOpen" max-width="920" scrollable>
      <v-card>
        <v-card-title class="d-flex align-center justify-space-between">
          <span>{{ t('siemCenter.settings.prefixes.title') }}</span>
          <v-btn icon="mdi-close" variant="text" @click="prefixesOpen = false" />
        </v-card-title>
        <v-card-text>
          <AcSiemDiscoveryPrefixesPanel
            @saved="() => { prefixesOpen = false; void refresh(); }"
          />
        </v-card-text>
      </v-card>
    </v-dialog>

    <v-dialog v-model="clearOpen" max-width="480">
      <v-card>
        <v-card-title class="text-h6">
          {{ t('siemCenter.discovery.clear.title') }}
        </v-card-title>
        <v-card-subtitle>
          {{ t('siemCenter.discovery.clear.subtitle') }}
        </v-card-subtitle>
        <v-card-text>
          <v-radio-group v-model="clearScope" density="compact" hide-details>
            <v-radio value="all" :label="t('siemCenter.discovery.clear.scopeAll')" />
            <v-radio value="scan" :label="t('siemCenter.discovery.clear.scopeScan')" />
            <v-radio value="ad" :label="t('siemCenter.discovery.clear.scopeAd')" />
          </v-radio-group>
        </v-card-text>
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" :disabled="clearing" @click="clearOpen = false">
            {{ t('siemCenter.discovery.clear.cancel') }}
          </v-btn>
          <v-btn color="error" variant="flat" :loading="clearing" @click="confirmClear">
            {{ t('siemCenter.discovery.clear.confirm') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-snackbar
      v-model="clearSnackbar"
      :color="clearSnackbarColor"
      location="bottom right"
      :timeout="4000"
      rounded="md"
    >
      {{ clearSnackbarText }}
    </v-snackbar>

    <div class="discovery-body" :class="{ 'rail-collapsed': !kpiOpen }">
      <div class="discovery-main">
    <div v-if="layout === 'tree'" class="topo-canvas rounded-lg pa-4 pa-md-6">
      <div class="diagram">
        <div class="diagram-root-row">
          <button type="button" class="node node-root" @click="expanded.size ? collapseAll() : expandAll()">
            <v-icon icon="mdi-lan-connect" size="22" class="me-2" />
            <div class="text-start">
              <div class="text-body-2 font-weight-bold">{{ resolvedRootLabel }}</div>
              <div class="text-caption text-medium-emphasis">
                {{ t('siemCenter.discovery.rootHint', { n: hostCount, b: branches.length }) }}
              </div>
            </div>
          </button>
        </div>
        <div class="diagram-vline" />
        <div class="diagram-bus" />
        <div class="diagram-branches">
          <div
            v-for="branch in branches"
            :key="branch.id"
            class="diagram-col"
          >
            <div class="diagram-col-stem" />
            <button
              type="button"
              class="node node-branch"
              :aria-expanded="isExpanded(branch.id)"
              @click="toggleBranch(branch.id)"
            >
              <v-icon
                :icon="isExpanded(branch.id) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
                size="18"
              />
              <div class="min-w-0 flex-grow-1 text-start">
                <div class="text-body-2 font-weight-bold text-truncate">{{ branch.label }}</div>
                <div v-if="branch.detail" class="text-caption text-medium-emphasis text-truncate">
                  {{ branch.detail }}
                </div>
              </div>
              <div class="d-flex align-center ga-1">
                <span
                  v-for="dot in coverageDots(branch)"
                  :key="dot.status"
                  class="mini-dot"
                  :class="`bg-${dot.color}`"
                />
                <v-chip size="x-small" variant="tonal">{{ branch.hosts.length }}</v-chip>
              </div>
            </button>
            <div v-if="isExpanded(branch.id)" class="diagram-children">
              <div class="diagram-children-vline" />
              <div
                v-for="(host, idx) in branch.hosts"
                :key="host.id"
                class="diagram-host-row"
                :class="{ 'is-last': idx === branch.hosts.length - 1 }"
              >
                <div class="diagram-elbow" />
                <AcSiemDiscoveryHostNode
                  :host="host"
                  density="tree"
                  @click="openHost(host)"
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- ========== C: Split ========== -->
    <div v-else-if="layout === 'split'" class="topo-canvas rounded-lg pa-0 overflow-hidden">
      <v-row no-gutters class="split-row">
        <v-col cols="12" md="4" class="split-left pa-3">
          <div class="text-caption text-medium-emphasis mb-2">
            {{ t('siemCenter.discovery.splitTreeTitle') }}
          </div>
          <div
            v-for="branch in branches"
            :key="branch.id"
            class="split-tree-item mb-1"
            :class="{ active: selectedBranchId === branch.id }"
          >
            <button type="button" class="split-branch-btn" @click="selectBranch(branch.id)">
              <v-icon
                :icon="isExpanded(branch.id) ? 'mdi-folder-open' : 'mdi-folder'"
                size="18"
                class="me-1"
                @click.stop="toggleBranch(branch.id)"
              />
              <span class="text-body-2 text-truncate">{{ branch.label }}</span>
              <v-chip size="x-small" class="ms-auto" variant="tonal">{{ branch.hosts.length }}</v-chip>
            </button>
            <div v-if="isExpanded(branch.id)" class="ps-6 pb-1">
              <button
                v-for="host in branch.hosts"
                :key="host.id"
                type="button"
                class="d-flex align-center ga-2 py-1 host-inline-link"
                @click="openHost(host)"
              >
                <span class="legend-dot" :class="`bg-${coverageColor(host.coverage)}`" />
                <span class="text-caption text-truncate">{{ host.hostname }}</span>
              </button>
            </div>
          </div>
        </v-col>
        <v-col cols="12" md="8" class="split-right pa-4">
          <template v-if="selectedBranch">
            <div class="d-flex align-center ga-2 mb-3">
              <div class="node node-root py-2 px-3">
                <v-icon icon="mdi-lan" size="18" class="me-1" />
                <span class="text-body-2 font-weight-bold">{{ resolvedRootLabel }}</span>
              </div>
              <v-icon icon="mdi-arrow-right" size="18" class="text-medium-emphasis" />
              <div class="node node-branch py-2 px-3" style="width: auto">
                <span class="text-body-2 font-weight-bold">{{ selectedBranch.label }}</span>
              </div>
            </div>
            <div class="text-caption text-medium-emphasis mb-2">{{ selectedBranch.detail }}</div>
            <div class="d-flex flex-wrap ga-2">
              <AcSiemDiscoveryHostNode
                v-for="host in selectedBranch.hosts"
                :key="host.id"
                :host="host"
                density="wide"
                @click="openHost(host)"
              />
            </div>
          </template>
          <div v-else class="text-body-2 text-medium-emphasis">
            {{ t('siemCenter.discovery.splitEmpty') }}
          </div>
        </v-col>
      </v-row>
    </div>

    <!-- ========== D: Tier lanes ========== -->
    <div v-else-if="layout === 'tiers'" class="topo-canvas rounded-lg pa-4">
      <div class="tier-lane mb-3">
        <div class="tier-label">{{ t('siemCenter.discovery.tiers.core') }}</div>
        <div class="tier-track">
          <div class="node node-root">
            <v-icon icon="mdi-lan-connect" size="20" class="me-2" />
            <span class="text-body-2 font-weight-bold">{{ resolvedRootLabel }}</span>
          </div>
        </div>
      </div>
      <div class="tier-lane mb-3">
        <div class="tier-label">{{ t('siemCenter.discovery.tiers.access') }}</div>
        <div class="tier-track">
          <button
            v-for="branch in branches"
            :key="branch.id"
            type="button"
            class="node node-branch tier-node"
            :class="{ 'is-selected': selectedBranchId === branch.id }"
            @click="toggleBranch(branch.id)"
          >
            <v-icon
              :icon="isExpanded(branch.id) ? 'mdi-chevron-down' : 'mdi-chevron-right'"
              size="16"
            />
            <div class="min-w-0 text-start">
              <div class="text-body-2 font-weight-bold text-truncate">{{ branch.label }}</div>
              <div class="text-caption text-medium-emphasis text-truncate">{{ branch.detail }}</div>
            </div>
            <v-chip size="x-small" variant="tonal">{{ branch.hosts.length }}</v-chip>
          </button>
        </div>
      </div>
      <div class="tier-lane">
        <div class="tier-label">{{ t('siemCenter.discovery.tiers.endpoint') }}</div>
        <div class="tier-track">
          <template v-for="branch in branches" :key="`hosts-${branch.id}`">
            <template v-if="isExpanded(branch.id)">
              <AcSiemDiscoveryHostNode
                v-for="host in branch.hosts"
                :key="host.id"
                :host="host"
                density="wide"
                class="tier-node"
                @click="openHost(host)"
              />
            </template>
          </template>
          <div
            v-if="expanded.size === 0"
            class="text-body-2 text-medium-emphasis pa-2"
          >
            {{ t('siemCenter.discovery.tiersEmpty') }}
          </div>
        </div>
      </div>
    </div>

    <!-- ========== B: Graph canvas ========== -->
    <div v-else class="topo-canvas rounded-lg pa-2 pa-md-3">
      <div class="graph-scroll">
        <svg
          class="graph-svg"
          :viewBox="`0 0 ${graphLayout.width} ${graphLayout.height}`"
          :width="graphLayout.width"
          :height="graphLayout.height"
          role="img"
          :aria-label="t('siemCenter.discovery.layouts.graph')"
        >
          <!-- root → branch links -->
          <line
            v-for="bn in graphLayout.branches"
            :key="`rl-${bn.branch.id}`"
            :x1="graphLayout.root.x"
            :y1="graphLayout.root.y + graphLayout.metrics.ROOT_H / 2"
            :x2="bn.x"
            :y2="bn.y - graphLayout.metrics.BRANCH_H / 2"
            class="graph-link"
          />
          <!-- branch → host links -->
          <line
            v-for="hn in graphLayout.hosts"
            :key="`hl-${hn.host.id}`"
            :x1="graphLayout.branches.find((b) => b.branch.id === hn.branchId)?.x ?? 0"
            :y1="
              (graphLayout.branches.find((b) => b.branch.id === hn.branchId)?.y ?? 0) +
              graphLayout.metrics.BRANCH_H / 2
            "
            :x2="hn.x"
            :y2="hn.y - graphLayout.metrics.HOST_H / 2"
            class="graph-link"
          />

          <!-- root node -->
          <g
            class="graph-node-hit"
            @click="expanded.size ? collapseAll() : expandAll()"
          >
            <rect
              :x="graphLayout.root.x - 110"
              :y="graphLayout.root.y - graphLayout.metrics.ROOT_H / 2"
              width="220"
              :height="graphLayout.metrics.ROOT_H"
              rx="10"
              class="graph-node-root"
            />
            <text
              :x="graphLayout.root.x"
              :y="graphLayout.root.y + 5"
              text-anchor="middle"
              class="graph-text"
            >
              {{ resolvedRootLabel }}
            </text>
          </g>

          <!-- branch nodes -->
          <g
            v-for="bn in graphLayout.branches"
            :key="bn.branch.id"
            class="graph-node-hit"
            @click="toggleBranch(bn.branch.id)"
          >
            <rect
              :x="bn.x - graphLayout.metrics.BRANCH_W / 2"
              :y="bn.y - graphLayout.metrics.BRANCH_H / 2"
              :width="graphLayout.metrics.BRANCH_W"
              :height="graphLayout.metrics.BRANCH_H"
              rx="10"
              class="graph-node-branch"
              :class="{ 'is-open': isExpanded(bn.branch.id) }"
            />
            <text :x="bn.x" :y="bn.y - 4" text-anchor="middle" class="graph-text">
              {{
                bn.branch.label.length > 22
                  ? bn.branch.label.slice(0, 20) + '…'
                  : bn.branch.label
              }}
            </text>
            <text :x="bn.x" :y="bn.y + 14" text-anchor="middle" class="graph-text-muted">
              {{ bn.branch.hosts.length }} host
            </text>
          </g>

          <!-- Same host cards as tree/split/tiers (HTML via foreignObject) -->
          <foreignObject
            v-for="hn in graphLayout.hosts"
            :key="hn.host.id"
            :x="hn.x - graphLayout.metrics.HOST_W / 2"
            :y="hn.y - graphLayout.metrics.HOST_H / 2"
            :width="graphLayout.metrics.HOST_W"
            :height="graphLayout.metrics.HOST_H"
          >
            <div xmlns="http://www.w3.org/1999/xhtml" class="graph-host-fo">
              <AcSiemDiscoveryHostNode
                :host="hn.host"
                density="wide"
                @click="openHost(hn.host)"
              />
            </div>
          </foreignObject>
        </svg>
      </div>
      <div class="text-caption text-medium-emphasis px-3 pb-2">
        {{ t('siemCenter.discovery.graphHint') }}
      </div>
    </div>
      </div>

      <aside class="discovery-rail">
        <div class="discovery-rail-sticky">
          <v-card
            v-if="!kpiOpen"
            variant="outlined"
            class="rail-section rail-collapsed-card"
          >
            <v-btn
              block
              variant="text"
              class="rail-collapsed-btn"
              :aria-label="t('siemCenter.discovery.kpiExpand')"
              @click="toggleKpiOpen"
            >
              <v-icon icon="mdi-chevron-left" size="20" class="mb-1" />
              <span class="rail-collapsed-label">{{ t('siemCenter.discovery.kpiTitle') }}</span>
            </v-btn>
          </v-card>

          <v-card v-else variant="outlined" class="rail-section pa-3">
            <div class="d-flex align-center justify-space-between ga-2 mb-1">
              <button
                type="button"
                class="kpi-toggle-title d-flex align-center ga-1 min-w-0"
                :aria-expanded="kpiOpen"
                :aria-label="t('siemCenter.discovery.kpiCollapse')"
                @click="toggleKpiOpen"
              >
                <v-icon icon="mdi-chevron-down" size="18" class="flex-shrink-0" />
                <span class="text-caption text-medium-emphasis text-uppercase text-truncate">
                  {{ t('siemCenter.discovery.kpiTitle') }}
                </span>
              </button>
              <v-btn
                v-if="filterActive"
                size="x-small"
                variant="text"
                color="primary"
                @click="clearCoverageFilter"
              >
                {{ t('siemCenter.discovery.clearCoverageFilter') }}
              </v-btn>
            </div>
            <div class="text-caption text-medium-emphasis mb-2">
              {{ t('siemCenter.discovery.kpiHint') }}
            </div>
            <div class="d-flex flex-column ga-2">
              <div
                v-for="kpi in kpis"
                :key="kpi.key"
                class="kpi-card pa-2 rounded-lg"
                :class="[
                  `kpi-accent-${kpi.color}`,
                  {
                    'kpi-card--clickable': !!kpi.status,
                    'kpi-card--active': kpi.status ? isCoverageFilterActive(kpi.status) : false,
                    'kpi-card--dim': filterActive && kpi.status && !isCoverageFilterActive(kpi.status),
                    'kpi-card--emphasize': kpi.emphasize && kpi.value > 0,
                  },
                ]"
                role="button"
                tabindex="0"
                :aria-pressed="kpi.status ? isCoverageFilterActive(kpi.status) : undefined"
                @click="kpi.status && toggleCoverageFilter(kpi.status)"
                @keydown.enter.prevent="kpi.status && toggleCoverageFilter(kpi.status)"
                @keydown.space.prevent="kpi.status && toggleCoverageFilter(kpi.status)"
              >
                <div class="d-flex align-center ga-2">
                  <v-avatar size="28" :color="kpi.color" variant="tonal">
                    <v-icon :icon="kpi.icon" size="16" />
                  </v-avatar>
                  <div class="min-w-0 flex-grow-1">
                    <div class="text-caption text-medium-emphasis text-truncate">
                      {{ t(kpi.labelKey) }}
                    </div>
                    <div class="text-h6 font-weight-bold lh-sm">{{ kpi.value }}</div>
                  </div>
                </div>
              </div>
            </div>
          </v-card>
        </div>
      </aside>
    </div>

    <AcSiemDiscoveryHostDetailDialog
      v-model:open="detailOpen"
      :host="selectedHost"
      :stale-ms="staleMs"
    />
    </template>
  </div>
</template>

<style scoped>
.discovery-header {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: linear-gradient(
    135deg,
    rgba(var(--v-theme-primary), 0.08) 0%,
    rgba(var(--v-theme-surface), 1) 55%
  );
}
.discovery-segment-toggle {
  flex-shrink: 0;
}
.toolbar-layout-select {
  min-width: 132px;
  max-width: 160px;
}
.discovery-actions-menu :deep(.v-list-item__spacer) {
  width: 16px;
}
.menu-status-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  flex-shrink: 0;
}
.menu-status-dot.is-on {
  background: rgb(var(--v-theme-success));
}
.menu-status-dot.is-off {
  background: rgba(var(--v-theme-on-surface), 0.28);
}
.discovery-body {
  display: grid;
  grid-template-columns: 1fr;
  gap: 16px;
  align-items: start;
}
@media (min-width: 960px) {
  .discovery-body {
    grid-template-columns: minmax(0, 1fr) 260px;
  }
  .discovery-body.rail-collapsed {
    grid-template-columns: minmax(0, 1fr) 48px;
  }
}
.discovery-main {
  min-width: 0;
}
.discovery-rail {
  min-width: 0;
}
.discovery-rail-sticky {
  position: sticky;
  top: 72px;
}
.rail-section {
  background: rgb(var(--v-theme-surface));
}
.kpi-toggle-title {
  border: 0;
  background: transparent;
  padding: 0;
  cursor: pointer;
  color: inherit;
  text-align: start;
}
.kpi-toggle-title:hover {
  color: rgb(var(--v-theme-primary));
}
.rail-collapsed-card {
  overflow: hidden;
}
.rail-collapsed-btn {
  min-height: 140px !important;
  height: auto !important;
  flex-direction: column !important;
  padding: 12px 4px !important;
}
.rail-collapsed-label {
  writing-mode: vertical-rl;
  transform: rotate(180deg);
  font-size: 0.75rem;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  color: rgba(var(--v-theme-on-surface), 0.7);
  line-height: 1.2;
}
.legend-pill {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
  cursor: pointer;
  user-select: none;
  transition: opacity 0.15s ease, border-color 0.15s ease, background 0.15s ease;
}
.legend-pill:hover {
  border-color: rgba(var(--v-theme-primary), 0.45);
}
.legend-pill--active {
  border-color: rgb(var(--v-theme-primary));
  background: rgba(var(--v-theme-primary), 0.08);
}
.legend-pill--dim {
  opacity: 0.45;
}
.legend-pill--gap {
  box-shadow: inset 3px 0 0 rgb(var(--v-theme-error));
}
.legend-dot,
.mini-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  display: inline-block;
  flex-shrink: 0;
}
.mini-dot {
  width: 8px;
  height: 8px;
}
.bg-success {
  background: rgb(var(--v-theme-success));
}
.bg-warning {
  background: rgb(var(--v-theme-warning));
}
.bg-error {
  background: rgb(var(--v-theme-error));
}
.bg-info {
  background: rgb(var(--v-theme-info));
}
.bg-grey {
  background: rgba(var(--v-theme-on-surface), 0.35);
}
.bg-deep-orange {
  background: #ff5722;
}

.kpi-card {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-left: 3px solid transparent;
  background: rgb(var(--v-theme-surface));
}
.kpi-card--clickable {
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease, opacity 0.15s ease;
}
.kpi-card--clickable:hover {
  border-color: rgba(var(--v-theme-primary), 0.35);
}
.kpi-card--active {
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.2);
  border-color: rgba(var(--v-theme-primary), 0.45);
}
.kpi-card--dim {
  opacity: 0.45;
}
.kpi-card--emphasize {
  border-left-width: 4px;
  box-shadow: 0 0 0 1px rgba(var(--v-theme-error), 0.25);
}
.kpi-accent-error {
  border-left-color: rgb(var(--v-theme-error));
}
.kpi-accent-warning {
  border-left-color: rgb(var(--v-theme-warning));
}
.kpi-accent-success {
  border-left-color: rgb(var(--v-theme-success));
}
.kpi-accent-info {
  border-left-color: rgb(var(--v-theme-info));
}
.kpi-accent-grey {
  border-left-color: rgba(var(--v-theme-on-surface), 0.35);
}
.kpi-accent-deep-orange {
  border-left-color: #ff5722;
}

.topo-canvas {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  overflow-x: auto;
  background:
    radial-gradient(circle at 50% 0%, rgba(var(--v-theme-primary), 0.07), transparent 55%),
    repeating-linear-gradient(
      0deg,
      transparent,
      transparent 19px,
      rgba(var(--v-border-color), 0.07) 20px
    ),
    repeating-linear-gradient(
      90deg,
      transparent,
      transparent 19px,
      rgba(var(--v-border-color), 0.07) 20px
    );
}

.node {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border-radius: 12px;
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
  text-align: left;
}
.node-root,
.node-branch {
  cursor: pointer;
}
.node-root {
  padding: 12px 16px;
  border-color: rgba(var(--v-theme-primary), 0.4);
  background: rgba(var(--v-theme-primary), 0.08);
}
.node-branch {
  width: 100%;
  padding: 10px 12px;
}
.node-host {
  width: 100%;
  padding: 8px 10px;
  border-style: dashed;
}
.node-host-link {
  cursor: pointer;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
}
.node-host-link:hover {
  border-color: rgba(var(--v-theme-primary), 0.45);
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.08);
}
.host-inline-link {
  width: 100%;
  border: none;
  background: transparent;
  cursor: pointer;
  text-align: left;
  border-radius: 6px;
}
.host-inline-link:hover {
  background: rgba(var(--v-theme-primary), 0.08);
}

.diagram {
  min-width: 720px;
}
.diagram-root-row {
  display: flex;
  justify-content: center;
}
.diagram-vline {
  width: 2px;
  height: 20px;
  margin: 0 auto;
  background: rgba(var(--v-theme-primary), 0.45);
}
.diagram-bus {
  height: 2px;
  margin: 0 8%;
  background: rgba(var(--v-theme-primary), 0.35);
}
.diagram-branches {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 12px 16px;
  align-items: start;
}
.diagram-col-stem {
  width: 2px;
  height: 16px;
  margin: 0 auto;
  background: rgba(var(--v-theme-primary), 0.35);
}
.diagram-children {
  position: relative;
  margin-top: 4px;
  padding-left: 18px;
}
.diagram-children-vline {
  position: absolute;
  left: 10px;
  top: 0;
  bottom: 18px;
  width: 2px;
  background: rgba(var(--v-border-color), 0.65);
}
.diagram-host-row {
  position: relative;
  display: flex;
  align-items: center;
  margin-top: 8px;
}
.diagram-elbow {
  position: absolute;
  left: -8px;
  top: 50%;
  width: 16px;
  height: 2px;
  background: rgba(var(--v-border-color), 0.65);
}

.split-row {
  min-height: 360px;
}
.split-left {
  border-right: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-surface), 0.9);
}
.split-branch-btn {
  width: 100%;
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 8px 10px;
  border-radius: 8px;
  border: none;
  background: transparent;
  cursor: pointer;
  text-align: left;
}
.split-tree-item.active > .split-branch-btn,
.split-branch-btn:hover {
  background: rgba(var(--v-theme-primary), 0.1);
}

.tier-lane {
  display: grid;
  grid-template-columns: 100px 1fr;
  gap: 12px;
  align-items: start;
}
.tier-label {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: rgba(var(--v-theme-on-surface), 0.55);
  padding-top: 12px;
}
.tier-track {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  min-height: 64px;
  padding: 12px;
  border-radius: 12px;
  border: 1px dashed rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgba(var(--v-theme-surface), 0.65);
}
.tier-node {
  width: auto;
  max-width: 240px;
}
.tier-node.is-selected {
  border-color: rgba(var(--v-theme-primary), 0.5);
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.12);
}

.graph-scroll {
  overflow: auto;
  max-width: 100%;
}
.graph-svg {
  display: block;
  max-width: none;
  height: auto;
}
.graph-link {
  stroke: rgba(100, 100, 120, 0.45);
  stroke-width: 2;
}
.graph-node-root {
  fill: rgba(var(--v-theme-primary), 0.12);
  stroke: rgba(var(--v-theme-primary), 0.5);
  stroke-width: 1.5;
}
.graph-node-branch {
  fill: rgb(var(--v-theme-surface));
  stroke: rgba(var(--v-border-color), 0.8);
  stroke-width: 1.2;
}
.graph-node-branch.is-open {
  stroke: rgba(var(--v-theme-primary), 0.55);
  stroke-width: 1.8;
}
.graph-text {
  font-size: 12px;
  font-weight: 600;
  fill: rgba(var(--v-theme-on-surface), 0.9);
}
.graph-text-muted {
  font-size: 10px;
  fill: rgba(var(--v-theme-on-surface), 0.55);
}
.graph-node-hit {
  cursor: pointer;
}
.graph-host-fo {
  width: 100%;
  height: 100%;
  box-sizing: border-box;
  padding: 2px;
  overflow: hidden;
}
.graph-host-fo :deep(.host-node.density-wide) {
  width: 100%;
  max-width: none;
  min-width: 0;
  height: 100%;
  box-sizing: border-box;
}
</style>
