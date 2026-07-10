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
import { bootstrapReportingCatalog } from '@/utils/reportingCatalogBootstrap';
import { ArrowLeftIcon } from 'vue-tabler-icons';

const router = useRouter();
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

const categoryService = computed(() => new ReportingCategoryService(domainKey.value));
const catalogService = computed(() => new ReportingCatalogService(domainKey.value));

const page = computed(() => ({ title: t('reporting.categories.title') }));
const breadcrumbs = computed(() => [
  { text: t('reporting.breadcrumbs.home'), disabled: false, href: '/' },
  { text: t('reporting.breadcrumbs.reporting'), disabled: false, href: '/apps/reporting' },
  { text: t('reporting.categories.title'), disabled: true, href: '#' },
]);

const categoryTree = ref<DiTreeNode[]>([]);
const categoryTreeLoading = ref(false);
const selectedCategoryId = ref<string | null>(null);
const error = ref('');
const notify = ref('');

const newCategoryDialog = ref(false);
const newCategoryName = ref('');
const creatingCategory = ref(false);

const renameCategoryDialog = ref(false);
const renameCategoryName = ref('');
const renamingCategory = ref(false);

const deleteCategoryDialog = ref(false);
const deletingCategory = ref(false);

const selectedCategoryName = computed(() => {
  if (!selectedCategoryId.value) return '';
  return findReportingCategoryNodeName(categoryTree.value, selectedCategoryId.value) ?? '';
});

function loadTree() {
  categoryTreeLoading.value = true;
  try {
    categoryTree.value = categoryService.value.getTree();
  } finally {
    categoryTreeLoading.value = false;
  }
}

function selectCategory(categoryId: string | null) {
  selectedCategoryId.value = categoryId;
}

function openNewCategoryDialog() {
  newCategoryName.value = '';
  newCategoryDialog.value = true;
}

async function submitNewCategory() {
  const name = newCategoryName.value.trim();
  if (!name) return;
  creatingCategory.value = true;
  error.value = '';
  try {
    const created = await categoryService.value.create(
      { name, parentId: selectedCategoryId.value },
      authStore.userInfo?.username ?? null
    );
    loadTree();
    newCategoryDialog.value = false;
    selectedCategoryId.value = created.id;
    notify.value = t('reporting.categories.created');
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('reporting.errors.saveFailed');
  } finally {
    creatingCategory.value = false;
  }
}

function openRenameCategoryDialog() {
  if (!selectedCategoryId.value) return;
  renameCategoryName.value = selectedCategoryName.value;
  renameCategoryDialog.value = true;
}

async function submitRenameCategory() {
  const id = selectedCategoryId.value;
  const name = renameCategoryName.value.trim();
  if (!id || !name) return;
  renamingCategory.value = true;
  error.value = '';
  try {
    await categoryService.value.rename(id, { name });
    loadTree();
    renameCategoryDialog.value = false;
    notify.value = t('reporting.categories.renamed');
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('reporting.errors.saveFailed');
  } finally {
    renamingCategory.value = false;
  }
}

function openDeleteCategoryDialog() {
  if (!selectedCategoryId.value) return;
  deleteCategoryDialog.value = true;
}

async function submitDeleteCategory() {
  const id = selectedCategoryId.value;
  if (!id) return;
  deletingCategory.value = true;
  error.value = '';
  try {
    await categoryService.value.delete(id, (categoryId) =>
      catalogService.value.hasReportsInCategory(categoryId)
    );
    loadTree();
    deleteCategoryDialog.value = false;
    selectedCategoryId.value = null;
    notify.value = t('reporting.categories.deleted');
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '';
    if (msg === 'CATEGORY_HAS_CHILDREN') {
      error.value = t('reporting.categories.errors.hasChildren');
    } else if (msg === 'CATEGORY_HAS_REPORTS') {
      error.value = t('reporting.categories.errors.hasReports');
    } else {
      error.value = msg || t('reporting.errors.saveFailed');
    }
  } finally {
    deletingCategory.value = false;
  }
}

function goBackToCatalog() {
  const query = selectedCategoryId.value ? { categoryId: selectedCategoryId.value } : undefined;
  void router.push({ path: '/apps/reporting', query });
}

onMounted(() => {
  void bootstrapReportingCatalog(domainKey.value).then(() => loadTree());
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />

    <div class="d-flex align-center mb-4">
      <v-btn
        variant="text"
        color="primary"
        class="text-none px-0"
        @click="goBackToCatalog"
      >
        <ArrowLeftIcon size="18" class="me-1" />
        {{ t('reporting.categories.backToCatalog') }}
      </v-btn>
    </div>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      {{ t('reporting.categories.intro') }}
    </v-alert>

    <v-alert v-if="error" type="error" variant="tonal" closable class="mb-3" @click:close="error = ''">
      {{ error }}
    </v-alert>
    <v-alert v-if="notify" type="success" variant="tonal" closable class="mb-3" @click:close="notify = ''">
      {{ notify }}
    </v-alert>

    <v-card elevation="0" class="border overflow-hidden">
      <div class="d-flex reporting-categories-layout">
        <div class="reporting-categories-tree flex-shrink-0 border-e">
          <div class="d-flex align-center justify-space-between px-3 py-2 border-b">
            <span class="text-subtitle-2 font-weight-bold">{{ t('reporting.catalog.categoryTree') }}</span>
          </div>
          <div class="px-2 py-2 border-b">
            <v-btn
              block
              size="small"
              variant="tonal"
              color="primary"
              class="text-none"
              prepend-icon="mdi-folder-plus-outline"
              @click="openNewCategoryDialog"
            >
              {{ t('reporting.categories.newCategory') }}
            </v-btn>
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

        <div class="flex-grow-1 pa-4">
          <template v-if="selectedCategoryId">
            <h3 class="text-h6 font-weight-bold mb-4">{{ selectedCategoryName }}</h3>
            <div class="d-flex flex-wrap ga-2">
              <v-btn
                variant="tonal"
                prepend-icon="mdi-pencil-outline"
                class="text-none"
                @click="openRenameCategoryDialog"
              >
                {{ t('reporting.categories.rename') }}
              </v-btn>
              <v-btn
                variant="tonal"
                color="error"
                prepend-icon="mdi-delete-outline"
                class="text-none"
                @click="openDeleteCategoryDialog"
              >
                {{ t('reporting.categories.delete') }}
              </v-btn>
            </div>
          </template>
          <div v-else class="text-body-2 text-medium-emphasis py-8 text-center">
            {{ t('reporting.categories.pickToManage') }}
          </div>
        </div>
      </div>
    </v-card>

    <v-dialog v-model="newCategoryDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('reporting.categories.newCategory') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p v-if="selectedCategoryId" class="text-body-2 text-medium-emphasis mb-3">
            {{ t('reporting.categories.newUnder', { name: selectedCategoryName }) }}
          </p>
          <v-text-field
            v-model="newCategoryName"
            :label="t('reporting.categories.name')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            @keydown.enter="submitNewCategory"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="newCategoryDialog = false">{{ t('reporting.actions.cancel') }}</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="creatingCategory"
            :disabled="!newCategoryName.trim()"
            @click="submitNewCategory"
          >
            {{ t('reporting.categories.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="renameCategoryDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">{{ t('reporting.categories.rename') }}</v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="renameCategoryName"
            :label="t('reporting.categories.name')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            @keydown.enter="submitRenameCategory"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="renameCategoryDialog = false">{{ t('reporting.actions.cancel') }}</v-btn>
          <v-btn
            color="primary"
            variant="flat"
            :loading="renamingCategory"
            :disabled="!renameCategoryName.trim()"
            @click="submitRenameCategory"
          >
            {{ t('reporting.actions.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteCategoryDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">{{ t('reporting.categories.delete') }}</v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{ t('reporting.categories.confirmDelete', { name: selectedCategoryName }) }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" @click="deleteCategoryDialog = false">{{ t('reporting.actions.cancel') }}</v-btn>
          <v-btn color="error" variant="flat" :loading="deletingCategory" @click="submitDeleteCategory">
            {{ t('reporting.categories.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.reporting-categories-layout {
  min-height: 400px;
}

.reporting-categories-tree {
  width: 300px;
  min-width: 240px;
}
</style>
