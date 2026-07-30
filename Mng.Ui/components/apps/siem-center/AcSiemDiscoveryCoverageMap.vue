<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  DISCOVERY_FACETS,
  useSiemDiscoveryData,
  hostEventsLink,
} from '@/composables/useSiemDiscoveryData';
import { coverageColor } from '@/composables/useSiemDiscoveryMock';
import type {
  SiemDiscoveryBranch,
  SiemDiscoveryFacet,
  SiemDiscoveryHost,
} from '@/types/apps/siemDiscovery';

type LayoutMode = 'tree' | 'split' | 'tiers' | 'graph';

const LAYOUTS: LayoutMode[] = ['tree', 'split', 'tiers', 'graph'];
const LAYOUT_STORAGE_KEY = 'siem.discovery.layout';

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

const { t, locale } = useAppI18n();

const facet = ref<SiemDiscoveryFacet>('vlan');
const layout = ref<LayoutMode>(loadLayoutPreference());

const {
  loading,
  error,
  lastRefreshedAt,
  branches: rawBranches,
  legend,
  kpis,
  refresh,
} = useSiemDiscoveryData(facet);

const branches = computed((): SiemDiscoveryBranch[] =>
  rawBranches.value.map((b) => {
    if (b.id !== '__live-unplaced') return b;
    return {
      ...b,
      label: t('siemCenter.discovery.liveBranchLabel'),
      detail: t('siemCenter.discovery.liveBranchDetail'),
    };
  }),
);

const hostCount = computed(() => branches.value.reduce((n, b) => n + b.hosts.length, 0));

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
  void navigateTo(hostEventsLink(host));
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
  const HOST_W = 148;
  const HOST_H = 44;
  const HOST_GAP = 14;
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
    <v-alert type="info" variant="tonal" density="comfortable" class="mb-3">
      {{ t('siemCenter.discovery.mockBanner') }}
    </v-alert>

    <v-alert v-if="error" type="warning" variant="tonal" density="comfortable" class="mb-3">
      {{ t('siemCenter.discovery.coverageLoadError') }}
      <div class="text-caption mt-1">{{ error }}</div>
    </v-alert>

    <v-card variant="outlined" class="discovery-toolbar mb-3 pa-2">
      <div class="d-flex flex-wrap align-center ga-2 ga-md-3">
        <div class="d-flex align-center ga-2 toolbar-brand pe-md-2">
          <v-icon size="20" color="primary">mdi-sitemap</v-icon>
          <div class="d-none d-sm-block">
            <div class="text-body-2 font-weight-medium lh-sm">
              {{ t('siemCenter.discovery.topologyTitle') }}
            </div>
            <div class="text-caption text-medium-emphasis lh-sm">
              {{ t('siemCenter.discovery.layoutHint') }}
            </div>
          </div>
        </div>

        <v-divider vertical class="d-none d-md-flex toolbar-divider" />

        <div class="d-flex align-center ga-2 flex-grow-1 flex-wrap">
          <span class="text-caption text-medium-emphasis text-no-wrap d-none d-sm-inline">
            {{ t('siemCenter.discovery.toolbar.layout') }}
          </span>
          <v-btn-toggle
            v-model="layout"
            mandatory
            density="compact"
            color="primary"
            variant="outlined"
            divided
            class="toolbar-toggle"
          >
            <v-btn
              v-for="l in layoutItems"
              :key="l.id"
              :value="l.id"
              size="small"
              class="px-2"
            >
              <v-icon :icon="l.icon" size="18" start class="d-none d-sm-inline" />
              {{ l.title }}
              <v-tooltip activator="parent" location="bottom">{{ l.title }}</v-tooltip>
            </v-btn>
          </v-btn-toggle>

          <v-divider vertical class="d-none d-md-flex toolbar-divider" />

          <v-select
            v-model="facet"
            :items="facetItems"
            item-title="title"
            item-value="id"
            :label="t('siemCenter.discovery.toolbar.groupBy')"
            variant="outlined"
            density="compact"
            hide-details
            class="toolbar-select"
          />

          <v-divider vertical class="d-none d-md-flex toolbar-divider" />

          <div class="d-flex align-center ga-1">
            <v-btn
              size="small"
              variant="text"
              prepend-icon="mdi-arrow-expand-vertical"
              @click="expandAll"
            >
              <span class="d-none d-lg-inline">{{ t('siemCenter.discovery.expandAll') }}</span>
              <v-tooltip activator="parent" location="bottom">
                {{ t('siemCenter.discovery.expandAll') }}
              </v-tooltip>
            </v-btn>
            <v-btn
              size="small"
              variant="text"
              prepend-icon="mdi-arrow-collapse-vertical"
              @click="collapseAll"
            >
              <span class="d-none d-lg-inline">{{ t('siemCenter.discovery.collapseAll') }}</span>
              <v-tooltip activator="parent" location="bottom">
                {{ t('siemCenter.discovery.collapseAll') }}
              </v-tooltip>
            </v-btn>
            <v-btn
              size="small"
              variant="text"
              :loading="loading"
              @click="refresh"
            >
              <v-icon>mdi-refresh</v-icon>
              <v-tooltip activator="parent" location="bottom">
                {{ t('siemCenter.discovery.refreshCoverage') }}
                <span v-if="lastRefreshedLabel"> · {{ lastRefreshedLabel }}</span>
              </v-tooltip>
            </v-btn>
          </div>
        </div>
      </div>
    </v-card>

    <div class="discovery-body">
      <div class="discovery-main">
    <div v-if="layout === 'tree'" class="topo-canvas rounded-lg pa-4 pa-md-6">
      <div class="diagram">
        <div class="diagram-root-row">
          <button type="button" class="node node-root" @click="expanded.size ? collapseAll() : expandAll()">
            <v-icon icon="mdi-lan-connect" size="22" class="me-2" />
            <div class="text-start">
              <div class="text-body-2 font-weight-bold">{{ t('siemCenter.discovery.rootLabel') }}</div>
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
                <button
                  type="button"
                  class="node node-host node-host-link"
                  @click="openHost(host)"
                >
                  <span class="legend-dot" :class="`bg-${coverageColor(host.coverage)}`" />
                  <div class="min-w-0">
                    <div class="text-body-2 font-weight-medium text-truncate">{{ host.hostname }}</div>
                    <div class="text-caption text-medium-emphasis text-truncate">
                      {{ host.ip }}<span v-if="host.osHint"> · {{ host.osHint }}</span>
                    </div>
                  </div>
                  <v-icon icon="mdi-open-in-new" size="14" class="text-medium-emphasis ms-1" />
                </button>
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
                <span class="text-body-2 font-weight-bold">{{ t('siemCenter.discovery.rootLabel') }}</span>
              </div>
              <v-icon icon="mdi-arrow-right" size="18" class="text-medium-emphasis" />
              <div class="node node-branch py-2 px-3" style="width: auto">
                <span class="text-body-2 font-weight-bold">{{ selectedBranch.label }}</span>
              </div>
            </div>
            <div class="text-caption text-medium-emphasis mb-2">{{ selectedBranch.detail }}</div>
            <div class="d-flex flex-wrap ga-2">
              <button
                v-for="host in selectedBranch.hosts"
                :key="host.id"
                type="button"
                class="node node-host node-host-link"
                style="width: auto; min-width: 160px"
                @click="openHost(host)"
              >
                <span class="legend-dot" :class="`bg-${coverageColor(host.coverage)}`" />
                <div>
                  <div class="text-body-2 font-weight-medium">{{ host.hostname }}</div>
                  <div class="text-caption text-medium-emphasis">{{ host.ip }}</div>
                </div>
              </button>
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
            <span class="text-body-2 font-weight-bold">{{ t('siemCenter.discovery.rootLabel') }}</span>
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
              <button
                v-for="host in branch.hosts"
                :key="host.id"
                type="button"
                class="node node-host tier-node node-host-link"
                @click="openHost(host)"
              >
                <span class="legend-dot" :class="`bg-${coverageColor(host.coverage)}`" />
                <div class="min-w-0">
                  <div class="text-body-2 font-weight-medium text-truncate">{{ host.hostname }}</div>
                  <div class="text-caption text-medium-emphasis text-truncate">
                    {{ host.ip }} · {{ branch.label.split('·')[0]?.trim() }}
                  </div>
                </div>
              </button>
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
              {{ t('siemCenter.discovery.rootLabel') }}
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

          <!-- host nodes (stacked under branch column) -->
          <g
            v-for="hn in graphLayout.hosts"
            :key="hn.host.id"
            class="graph-node-hit"
            @click="openHost(hn.host)"
          >
            <rect
              :x="hn.x - graphLayout.metrics.HOST_W / 2"
              :y="hn.y - graphLayout.metrics.HOST_H / 2"
              :width="graphLayout.metrics.HOST_W"
              :height="graphLayout.metrics.HOST_H"
              rx="8"
              class="graph-node-host"
            />
            <circle
              :cx="hn.x - graphLayout.metrics.HOST_W / 2 + 16"
              :cy="hn.y"
              r="5"
              :class="`graph-dot bg-${coverageColor(hn.host.coverage)}`"
            />
            <text
              :x="hn.x - graphLayout.metrics.HOST_W / 2 + 28"
              :y="hn.y - 2"
              class="graph-text-sm"
            >
              {{ hn.host.hostname }}
            </text>
            <text
              :x="hn.x - graphLayout.metrics.HOST_W / 2 + 28"
              :y="hn.y + 12"
              class="graph-text-muted"
            >
              {{ hn.host.ip }}
            </text>
          </g>
        </svg>
      </div>
      <div class="text-caption text-medium-emphasis px-3 pb-2">
        {{ t('siemCenter.discovery.graphHint') }}
      </div>
    </div>
      </div>

      <aside class="discovery-rail">
        <div class="discovery-rail-sticky">
          <v-card variant="outlined" class="rail-section pa-3 mb-3">
            <div class="text-caption text-medium-emphasis mb-2 text-uppercase">
              {{ t('siemCenter.discovery.legendTitle') }}
            </div>
            <div class="d-flex flex-column ga-2">
              <div
                v-for="item in legend"
                :key="item.status"
                class="legend-pill d-flex align-center ga-2 px-2 py-2 rounded-lg"
              >
                <span class="legend-dot" :class="`bg-${item.color}`" />
                <span class="text-body-2 flex-grow-1">{{ t(item.labelKey) }}</span>
                <v-chip size="x-small" :color="item.color" variant="flat">{{ item.count }}</v-chip>
              </div>
            </div>
          </v-card>

          <v-card variant="outlined" class="rail-section pa-3">
            <div class="text-caption text-medium-emphasis mb-2 text-uppercase">
              {{ t('siemCenter.discovery.kpiTitle') }}
            </div>
            <div class="d-flex flex-column ga-2">
              <div
                v-for="kpi in kpis"
                :key="kpi.key"
                class="kpi-card pa-2 rounded-lg"
                :class="`kpi-accent-${kpi.color}`"
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
  </div>
</template>

<style scoped>
.discovery-toolbar {
  background: rgb(var(--v-theme-surface));
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
.toolbar-brand {
  min-width: 0;
}
.toolbar-divider {
  align-self: stretch;
  margin-block: 4px;
  opacity: 0.55;
}
.toolbar-select {
  min-width: 140px;
  max-width: 180px;
}
.toolbar-toggle :deep(.v-btn) {
  text-transform: none;
  letter-spacing: normal;
}

.legend-pill {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  background: rgb(var(--v-theme-surface));
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
.kpi-accent-error {
  border-left-color: rgb(var(--v-theme-error));
}
.kpi-accent-warning {
  border-left-color: rgb(var(--v-theme-warning));
}
.kpi-accent-info {
  border-left-color: rgb(var(--v-theme-info));
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
.graph-node-host {
  fill: rgb(var(--v-theme-surface));
  stroke: rgba(var(--v-border-color), 0.7);
  stroke-dasharray: 4 3;
}
.graph-text {
  font-size: 12px;
  font-weight: 600;
  fill: rgba(var(--v-theme-on-surface), 0.9);
}
.graph-text-sm {
  font-size: 11px;
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
.graph-dot.bg-success {
  fill: rgb(var(--v-theme-success));
}
.graph-dot.bg-warning {
  fill: rgb(var(--v-theme-warning));
}
.graph-dot.bg-error {
  fill: rgb(var(--v-theme-error));
}
.graph-dot.bg-grey {
  fill: rgba(var(--v-theme-on-surface), 0.35);
}
</style>
