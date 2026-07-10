<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAuthStore } from '@/stores/auth';
import {
  ReportingCatalogService,
  reportingDomainKey,
} from '@/services/reportingCatalogService';
import { ReportingCategoryService } from '@/services/reportingCategoryService';
import type { DiTreeNode } from '@/types/apps/documentIntelligence';
import { findReportingCategoryNodeName } from '@/utils/reportingCategoryTree';
import type { ReportingReportDefinition } from '@/types/apps/reporting';
import { filterVisibleReportingReports } from '@/utils/reportingReportAccess';
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { reportingDataTableRow } from '@/utils/reportingListConfig';
import { PlusIcon } from 'vue-tabler-icons';

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

const page = computed(() => ({ title: t('reporting.catalog.title') }));
const breadcrumbs = computed(() => [
  { text: t('reporting.breadcrumbs.home'), disabled: false, href: '/' },
  { text: t('reporting.breadcrumbs.reporting'), disabled: true, href: '#' },
]);

const categoryTree = ref<DiTreeNode[]>([]);
const categoryTreeLoading = ref(false);
const selectedCategoryId = ref<string | null>(null);
const reports = ref(catalogService.value.listReports());

const tableHeaders = computed(() => [
  { title: t('reporting.catalog.colTitle'), key: 'title' },
  { title: t('reporting.catalog.colDataset'), key: 'datasetName' },
  { title: t('reporting.catalog.colUpdated'), key: 'updatedAt' },
  { title: '', key: 'actions', sortable: false, align: 'end' as const, width: 160 },
]);

const selectedCategoryName = computed(() => {
  if (!selectedCategoryId.value) return '';
  return findReportingCategoryNodeName(categoryTree.value, selectedCategoryId.value) ?? '';
});

const visibleReports = computed(() =>
  filterVisibleReportingReports(reports.value, authStore.userGroups)
);

const filteredReports = computed(() => {
  if (!selectedCategoryId.value) return [];
  return visibleReports.value.filter((r) => r.categoryId === selectedCategoryId.value);
});

function reload() {
  categoryTree.value = categoryService.value.getTree();
  reports.value = catalogService.value.listReports();
}

async function loadCategoryTree() {
  categoryTreeLoading.value = true;
  try {
    reload();
    const fromQuery = route.query.categoryId;
    if (typeof fromQuery === 'string' && fromQuery.trim()) {
      selectedCategoryId.value = fromQuery.trim();
    }
  } finally {
    categoryTreeLoading.value = false;
  }
}

function selectCategory(categoryId: string | null) {
  selectedCategoryId.value = categoryId;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleString('tr-TR');
  } catch {
    return iso;
  }
}

function catalogReportRow(item: unknown): ReportingReportDefinition {
  return reportingDataTableRow(item as Record<string, unknown>) as unknown as ReportingReportDefinition;
}

function openNewReport() {
  const query = selectedCategoryId.value ? { categoryId: selectedCategoryId.value } : undefined;
  void router.push({ path: '/apps/reporting/designer', query });
}

function openReport(item: unknown) {
  const id = catalogReportRow(item).id;
  if (!id) return;
  void router.push(`/apps/reporting/run/${encodeURIComponent(id)}`);
}

function openReportDesigner(item: unknown) {
  const id = catalogReportRow(item).id;
  if (!id) return;
  void router.push(`/apps/reporting/designer/${encodeURIComponent(id)}`);
}

function deleteReport(item: unknown) {
  const row = catalogReportRow(item);
  if (!row.id) return;
  if (!confirm(t('reporting.catalog.confirmDelete', { title: row.title }))) return;
  void catalogService.value.deleteReport(row.id).then(() => reload());
}

onMounted(() => {
  bootstrapReportingCatalog(domainKey.value).then(() => {
    void loadCategoryTree();
  });
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      {{ t('reporting.catalog.intro') }}
    </v-alert>

    <div class="d-flex flex-wrap ga-2 mb-4">
      <v-btn variant="tonal" class="text-none" to="/apps/reporting/categories" prepend-icon="mdi-folder-cog-outline">
        {{ t('reporting.catalog.manageCategories') }}
      </v-btn>
    </div>

    <v-card elevation="0" class="border overflow-hidden">
      <div class="d-flex reporting-catalog-layout">
        <div class="reporting-catalog-tree flex-shrink-0 border-e">
          <div class="px-3 py-2 border-b">
            <span class="text-subtitle-2 font-weight-bold">{{ t('reporting.catalog.categoryTree') }}</span>
          </div>
          <div class="pa-2">
            <v-progress-linear v-if="categoryTreeLoading" indeterminate color="primary" class="mb-2" />
            <DiResourceTree
              :nodes="categoryTree"
              :selected-id="selectedCategoryId"
              :root-label="t('reporting.categories.allCategories')"
              :empty-label="t('reporting.categories.empty')"
              @select="selectCategory"
            />
          </div>
        </div>

        <div class="reporting-catalog-content flex-grow-1">
          <div class="d-flex align-center flex-wrap ga-2 px-4 py-3 border-b">
            <template v-if="selectedCategoryId">
              <span class="text-subtitle-1 font-weight-medium">{{ selectedCategoryName }}</span>
              <v-spacer />
              <v-btn color="primary" variant="flat" size="small" @click="openNewReport">
                <PlusIcon size="16" class="mr-1" />
                {{ t('reporting.catalog.newReport') }}
              </v-btn>
            </template>
            <span v-else class="text-body-2 text-medium-emphasis">
              {{ t('reporting.catalog.pickCategory') }}
            </span>
          </div>

          <v-card-text v-if="selectedCategoryId">
            <v-data-table
              :headers="tableHeaders"
              :items="filteredReports"
              density="compact"
              class="border rounded"
              item-value="id"
              @click:row="(_e, ctx) => openReport(ctx.item)"
            >
              <template #item.title="{ item }">
                <span class="font-weight-medium">{{ catalogReportRow(item).title }}</span>
                <div v-if="catalogReportRow(item).description" class="text-caption text-medium-emphasis">
                  {{ catalogReportRow(item).description }}
                </div>
              </template>
              <template #item.updatedAt="{ item }">
                {{ formatDate(catalogReportRow(item).updatedAt) }}
              </template>
              <template #item.actions="{ item }">
                <v-btn
                  icon="mdi-play-outline"
                  size="x-small"
                  variant="text"
                  color="primary"
                  :title="t('reporting.catalog.runReport')"
                  @click.stop="openReport(item)"
                />
                <v-btn
                  icon="mdi-pencil"
                  size="x-small"
                  variant="text"
                  :title="t('reporting.catalog.editDesign')"
                  @click.stop="openReportDesigner(item)"
                />
                <v-btn
                  icon="mdi-delete"
                  size="x-small"
                  variant="text"
                  color="error"
                  @click.stop="deleteReport(item)"
                />
              </template>
              <template #no-data>
                <div class="text-body-2 text-medium-emphasis py-8 text-center">
                  {{ t('reporting.catalog.emptyReports') }}
                </div>
              </template>
            </v-data-table>
          </v-card-text>
        </div>
      </div>
    </v-card>
  </div>
</template>

<style scoped>
.reporting-catalog-layout {
  min-height: 420px;
}

.reporting-catalog-tree {
  width: 280px;
  min-width: 220px;
}

:deep(.v-data-table tbody tr) {
  cursor: pointer;
}
</style>
