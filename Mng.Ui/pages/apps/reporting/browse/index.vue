<script setup lang="ts">
/**
 * Son kullanıcı rapor tarayıcısı — sol ağaç (kategori + canView raporlar), sağda runner.
 * Admin araçları (tasarım, DG pipeline) yok. Sol gezgin DI gibi daraltılabilir.
 */
import { computed, onMounted, ref, watch } from 'vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import ReportingRunnerView from '@/components/apps/reporting/ReportingRunnerView.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useAuthStore } from '@/stores/auth';
import {
  ReportingCatalogService,
  reportingDomainKey,
} from '@/services/reportingCatalogService';
import { ReportingCategoryService } from '@/services/reportingCategoryService';
import type { DiTreeNode } from '@/types/apps/documentIntelligence';
import { DI_ROOT_ID } from '@/types/apps/documentIntelligence';
import {
  buildReportingBrowseTree,
  findReportingBrowseAncestorIds,
  parseReportingBrowseReportId,
  reportingBrowseReportNodeId,
} from '@/utils/reportingBrowseTree';
import { filterVisibleReportingReports } from '@/utils/reportingReportAccess';
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { LayoutSidebarLeftCollapseIcon, LayoutSidebarLeftExpandIcon } from 'vue-tabler-icons';

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();

const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: Record<string, unknown>) => {
  if (i18n?.t) return i18n.t(key, params);
  if (i18n?.global?.t) return i18n.global.t(key, params);
  return key;
};

const domainKey = computed(() =>
  reportingDomainKey(authStore.userInfo?.domain_id, authStore.userInfo?.domain_name)
);

const catalogService = computed(() => new ReportingCatalogService(domainKey.value));
const categoryService = computed(() => new ReportingCategoryService(domainKey.value));

const {
  treeWidth,
  treeCollapsed,
  resizeActive,
  startResize,
  toggleTreeCollapse,
} = useResizableTreePanel('reporting-browse-tree', {
  minWidth: 220,
  maxWidth: 480,
  defaultWidth: 300,
});

const page = computed(() => ({ title: t('reporting.browse.title') }));
const breadcrumbs = computed(() => [
  { text: t('reporting.breadcrumbs.home'), disabled: false, href: '/' },
  { text: t('reporting.browse.menuTitle'), disabled: true, href: '#' },
]);

const treeLoading = ref(false);
const browseTree = ref<DiTreeNode[]>([]);
const selectedNodeId = ref<string | null>(null);
const selectedReportId = ref<string | null>(null);
const initialExpandedIds = ref<string[]>([]);

const visibleReports = computed(() =>
  filterVisibleReportingReports(catalogService.value.listReports(), authStore.userGroups)
);

function rebuildTree() {
  browseTree.value = buildReportingBrowseTree({
    categoryTree: categoryService.value.getTree(),
    reports: visibleReports.value,
    uncategorizedLabel: t('reporting.browse.uncategorized'),
  });
}

function syncSelectionFromQuery() {
  const fromQuery = route.query.reportId;
  const reportId =
    typeof fromQuery === 'string' && fromQuery.trim() ? fromQuery.trim() : null;

  if (!reportId) {
    selectedReportId.value = null;
    selectedNodeId.value = null;
    return;
  }

  const visible = visibleReports.value.some((r) => r.id === reportId);
  if (!visible) {
    selectedReportId.value = null;
    selectedNodeId.value = null;
    return;
  }

  const nodeId = reportingBrowseReportNodeId(reportId);
  selectedReportId.value = reportId;
  selectedNodeId.value = nodeId;
  const ancestors = findReportingBrowseAncestorIds(browseTree.value, nodeId) ?? [];
  initialExpandedIds.value = [DI_ROOT_ID, ...ancestors];
}

function onTreeSelect(nodeId: string | null) {
  if (nodeId == null) {
    selectedNodeId.value = null;
    selectedReportId.value = null;
    void router.replace({ path: '/apps/reporting/browse', query: {} });
    return;
  }

  const reportId = parseReportingBrowseReportId(nodeId);
  if (!reportId) {
    return;
  }

  selectedNodeId.value = nodeId;
  selectedReportId.value = reportId;
  void router.replace({
    path: '/apps/reporting/browse',
    query: { reportId },
  });
}

onMounted(() => {
  bootstrapReportingCatalog(domainKey.value);
  treeLoading.value = true;
  try {
    rebuildTree();
    syncSelectionFromQuery();
  } finally {
    treeLoading.value = false;
  }
});

watch(
  () => route.query.reportId,
  () => {
    syncSelectionFromQuery();
  }
);

watch(domainKey, () => {
  bootstrapReportingCatalog(domainKey.value);
  rebuildTree();
  syncSelectionFromQuery();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-card elevation="0" class="border overflow-hidden">
      <div class="d-flex reporting-browse-layout">
        <div
          v-if="!treeCollapsed"
          class="reporting-browse-tree flex-shrink-0"
          :style="{ width: treeWidth + 'px' }"
        >
          <div class="d-flex align-center justify-space-between px-3 py-2 border-b">
            <span class="text-subtitle-2 font-weight-bold">{{ t('reporting.browse.treeTitle') }}</span>
            <v-btn
              icon
              size="x-small"
              variant="text"
              :title="t('reporting.browse.collapseTree')"
              @click="toggleTreeCollapse"
            >
              <LayoutSidebarLeftCollapseIcon size="18" />
            </v-btn>
          </div>
          <div class="pa-2 reporting-browse-tree-scroll">
            <v-progress-linear v-if="treeLoading" indeterminate color="primary" class="mb-2" />
            <DiResourceTree
              :nodes="browseTree"
              :selected-id="selectedNodeId"
              :root-label="t('reporting.browse.allReports')"
              :empty-label="t('reporting.browse.emptyTree')"
              :initial-expanded-ids="initialExpandedIds"
              @select="onTreeSelect"
            />
          </div>
        </div>

        <div
          v-if="!treeCollapsed"
          :class="['reporting-browse-resize', { 'reporting-browse-resize--active': resizeActive }]"
          @mousedown.prevent="startResize"
        />

        <div class="reporting-browse-content flex-grow-1">
          <div
            v-if="treeCollapsed"
            class="d-flex align-center ga-2 px-3 py-2 border-b"
          >
            <v-btn
              icon
              size="small"
              variant="text"
              :title="t('reporting.browse.expandTree')"
              @click="toggleTreeCollapse"
            >
              <LayoutSidebarLeftExpandIcon size="18" />
            </v-btn>
            <span class="text-caption text-medium-emphasis">{{ t('reporting.browse.treeTitle') }}</span>
          </div>

          <div class="pa-4">
            <ReportingRunnerView
              v-if="selectedReportId"
              :key="selectedReportId"
              :report-id="selectedReportId"
              embedded
              :show-admin-tools="false"
            />
            <div
              v-else
              class="d-flex align-center justify-center text-body-2 text-medium-emphasis reporting-browse-empty"
            >
              {{ t('reporting.browse.pickReport') }}
            </div>
          </div>
        </div>
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.reporting-browse-layout {
  min-height: calc(100vh - 180px);
}

.reporting-browse-tree {
  display: flex;
  flex-direction: column;
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.reporting-browse-tree-scroll {
  flex: 1;
  overflow: auto;
  max-height: calc(100vh - 220px);
}

.reporting-browse-resize {
  width: 5px;
  cursor: col-resize;
  background: transparent;
  flex-shrink: 0;
}

.reporting-browse-resize:hover,
.reporting-browse-resize--active {
  background: rgba(var(--v-theme-primary), 0.3);
}

.reporting-browse-content {
  min-width: 0;
  overflow: auto;
  max-height: calc(100vh - 180px);
}

.reporting-browse-empty {
  min-height: 280px;
}
</style>
