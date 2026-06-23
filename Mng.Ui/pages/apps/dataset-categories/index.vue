<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';
import { useAuthStore } from '@/stores/auth';
import { EditIcon, EyeIcon, TrashIcon, TagIcon, PlusIcon, RefreshIcon } from 'vue-tabler-icons';

// Get i18n instance for legacy mode
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const categoryStore = useDatasetCategoryStore();
const authStore = useAuthStore();
const router = useRouter();

// Check if user is admin
const isAdmin = computed(() => authStore.isAdmin);

// Filtered categories: Admin olmayan kullanıcılar için isSystemCategory: true olanları filtrele
const filteredCategories = computed(() => {
  if (isAdmin.value) {
    // Admin kullanıcılar tüm kategorileri görebilir
    return categoryStore.categories;
  }
  // Admin olmayan kullanıcılar için isSystemCategory: true olanları filtrele
  return categoryStore.categories.filter(cat => !cat.isSystemCategory);
});

const page = computed(() => ({ title: t('datasetCategories.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('datasetCategories.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('datasetCategories.breadcrumbs.categories'),
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedCategories = ref([]);
const showDeleteDialog = ref(false);
const categoryToDelete = ref<string | null>(null);
const showViewDialog = ref(false);
const categoryToView = ref<any>(null);

// Server-side pagination options
const tableOptions = ref({
  page: 1,
  itemsPerPage: 20,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = computed(() => [
  { title: t('datasetCategories.table.headers.categoryName'), key: 'categoryName', sortable: true },
  { title: t('datasetCategories.table.headers.description'), key: 'categoryDescription', sortable: false },
  { title: t('datasetCategories.table.headers.createdAt'), key: 'createInfo.createdAt', sortable: true },
  { title: t('datasetCategories.table.headers.createdBy'), key: 'createInfo.userInfo.userName', sortable: false },
  { title: t('datasetCategories.table.headers.updatedAt'), key: 'lastUpdateInfo.updatedAt', sortable: true },
  { title: t('datasetCategories.table.headers.updatedBy'), key: 'lastUpdateInfo.userInfo.userName', sortable: false },
  { title: t('datasetCategories.table.headers.historyCount'), key: 'historyCount', sortable: true },
  { title: t('datasetCategories.table.headers.actions'), key: 'actions', sortable: false, align: 'end' },
]);

// Computed: Server items length — backend totalCount drives pagination;
// non-admin system-category filtering is display-only on the current page.
const serverItemsLength = computed(() => categoryStore.totalCount || 0);

// Fetch categories function
const fetchCategories = async (options?: any) => {
  // Use provided options or current tableOptions
  const currentOptions = options || tableOptions.value;
  
  // Build query parameters for server-side pagination
  const params: {
    pageNumber?: number;
    pageSize?: number;
    search?: string;
  } = {
    pageNumber: currentOptions.page || 1,
    pageSize: currentOptions.itemsPerPage || 20,
  };
  
  // Add search parameter if exists
  if (search.value && search.value.trim()) {
    params.search = search.value.trim();
  }
  
  // Fetch categories with new parameters
  try {
    await categoryStore.fetchCategories(params);
    } catch (error) {
      // Error handled by store
    }
};

// Flag to prevent watch from triggering on initial load
const isInitialLoad = ref(true);

// Watch tableOptions for changes (pagination, itemsPerPage)
watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage],
  () => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    fetchCategories();
  }
);

// Watch for search changes (debounced)
let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  // Reset to first page when search changes
  if (tableOptions.value.page !== 1) {
    tableOptions.value.page = 1;
  }
  
  // Debounce search input (500ms)
  if (searchTimeout) {
    clearTimeout(searchTimeout);
  }
  searchTimeout = setTimeout(() => {
    fetchCategories();
  }, 500);
});

onMounted(async () => {
  // Admin/Manager kontrolü (opsiyonel - sayfa meta'da da yapılabilir)
  // Warning shown in template via v-alert
  
  // Initial load
  isInitialLoad.value = true;
  await fetchCategories();
  isInitialLoad.value = false;
});

const editCategory = (category: any) => {
  router.push(`/apps/dataset-categories/edit/${category.dataId}`);
};

const viewCategory = (category: any) => {
  categoryToView.value = category;
  showViewDialog.value = true;
};

const deleteCategory = (category: any) => {
  categoryToDelete.value = category.dataId;
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (categoryToDelete.value) {
    try {
      await categoryStore.deleteCategory(categoryToDelete.value);
      showDeleteDialog.value = false;
      categoryToDelete.value = null;
      
      // Refresh list
      await fetchCategories();
    } catch (error) {
      // Error handled by store
    }
  }
};

const refreshCategories = async () => {
  await fetchCategories();
};

const createNewCategory = () => {
  router.push('/apps/dataset-categories/create');
};

const formatDate = (date: string | Date | null | undefined) => {
  if (!date) return '-';
  try {
    // Use current locale for date formatting
    const locale = i18n?.locale || i18n?.global?.locale?.value || 'tr';
    return new Date(date).toLocaleDateString(locale === 'en' ? 'en-US' : locale === 'zh' ? 'zh-CN' : locale === 'fr' ? 'fr-FR' : locale === 'ar' ? 'ar-SA' : 'tr-TR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return '-';
  }
};

const truncateText = (text: string | null | undefined, maxLength: number = 50) => {
  if (!text) return '-';
  if (text.length <= maxLength) return text;
  return text.substring(0, maxLength) + '...';
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <!-- Search and Actions -->
      <div class="d-flex justify-space-between align-center mb-4 flex-wrap ga-3">
        <div class="d-flex ga-3 align-center flex-wrap">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            :label="t('datasetCategories.search.placeholder')"
            :placeholder="t('datasetCategories.search.placeholder')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 300px;"
            clearable
            :hint="t('datasetCategories.search.hint')"
            persistent-hint
          />
          
          <v-btn
            icon
            size="small"
            variant="outlined"
            color="default"
            @click="refreshCategories"
            :loading="categoryStore.loading"
          >
            <RefreshIcon size="18" />
            <v-tooltip activator="parent" location="top">{{ t('datasetCategories.buttons.refresh') }}</v-tooltip>
          </v-btn>
        </div>
        
        <v-btn 
          color="primary" 
          variant="flat"
          @click="createNewCategory"
        >
          <PlusIcon class="mr-2" size="20" />
          {{ t('datasetCategories.buttons.newCategory') }}
        </v-btn>
      </div>

      <!-- Admin/Manager Warning -->
      <v-alert
        v-if="!authStore.isManager"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <strong>{{ t('datasetCategories.warning.title') }}</strong> {{ t('datasetCategories.warning.message') }}
      </v-alert>

      <!-- Error Message -->
      <v-alert
        v-if="categoryStore.error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="categoryStore.clearError"
      >
        {{ categoryStore.error }}
      </v-alert>

      <!-- Data Table -->
      <v-data-table
        v-model="selectedCategories"
        v-model:options="tableOptions"
        :headers="headers"
        :items="filteredCategories"
        :loading="categoryStore.loading"
        :server-items-length="serverItemsLength"
        :items-per-page="tableOptions.itemsPerPage"
        :items-per-page-options="[20, 50, 100]"
        item-value="dataId"
        class="border rounded-md"
        hide-default-footer
      >
        <!-- Category Name Column -->
        <template v-slot:item.categoryName="{ item }">
          <div class="d-flex align-center ga-2">
            <TagIcon size="18" class="text-primary" />
            <span class="text-subtitle-1 font-weight-medium">
              {{ item.categoryName }}
            </span>
          </div>
        </template>

        <!-- Description Column -->
        <template v-slot:item.categoryDescription="{ item }">
          <span class="text-body-2 text-medium-emphasis" :title="item.categoryDescription || ''">
            {{ truncateText(item.categoryDescription, 60) }}
          </span>
        </template>

        <!-- Created At Column -->
        <template v-slot:item.createInfo.createdAt="{ item }">
          <span class="text-caption">
            {{ formatDate(item.createInfo?.createdAt) }}
          </span>
        </template>

        <!-- Created By Column -->
        <template v-slot:item.createInfo.userInfo.userName="{ item }">
          <span class="text-caption">
            {{ item.createInfo?.userInfo?.userName || '-' }}
          </span>
        </template>

        <!-- Updated At Column -->
        <template v-slot:item.lastUpdateInfo.updatedAt="{ item }">
          <span class="text-caption">
            {{ formatDate(item.lastUpdateInfo?.updatedAt) }}
          </span>
        </template>

        <!-- Updated By Column -->
        <template v-slot:item.lastUpdateInfo.userInfo.userName="{ item }">
          <span class="text-caption">
            {{ item.lastUpdateInfo?.userInfo?.userName || '-' }}
          </span>
        </template>

        <!-- History Count Column -->
        <template v-slot:item.historyCount="{ value }">
          <v-chip size="small" variant="tonal" color="info">
            {{ value || 0 }}
          </v-chip>
        </template>

        <!-- Actions Column -->
        <template v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <v-btn
              icon
              size="small"
              variant="text"
              color="primary"
              @click="viewCategory(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('datasetCategories.buttons.view') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="info"
              @click="editCategory(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('datasetCategories.buttons.edit') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteCategory(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('datasetCategories.buttons.delete') }}</v-tooltip>
            </v-btn>
          </div>
        </template>

        <!-- No Data -->
        <template v-slot:no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">{{ t('datasetCategories.noData.title') }}</p>
            <v-btn 
              color="primary" 
              variant="flat"
              @click="createNewCategory" 
              class="mt-4"
            >
              <PlusIcon class="mr-2" size="20" />
              {{ t('datasetCategories.noData.button') }}
            </v-btn>
          </div>
        </template>

        <!-- Custom bottom slot with total count and pagination -->
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>{{ t('datasetCategories.pagination.total') }}</strong> {{ serverItemsLength }} {{ t('datasetCategories.pagination.records') }}
              <span v-if="categoryStore.totalPages > 1" class="ml-2">
                ({{ t('datasetCategories.pagination.page') }} {{ tableOptions.page }} / {{ categoryStore.totalPages }})
              </span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} - 
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, serverItemsLength) }} 
                / {{ serverItemsLength }} {{ t('datasetCategories.pagination.showing') }}
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ t('datasetCategories.pagination.itemsPerPage') }}</span>
              <v-select
                v-model="tableOptions.itemsPerPage"
                :items="[20, 50, 100]"
                density="compact"
                variant="outlined"
                hide-details
                style="max-width: 80px;"
              />
            </div>
          </div>
          <div class="d-flex justify-center pa-2 border-top">
            <v-pagination
              v-model="tableOptions.page"
              :length="categoryStore.totalPages"
              :total-visible="7"
              density="compact"
            />
          </div>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>

  <!-- View Category Dialog -->
  <v-dialog v-model="showViewDialog" max-width="600px">
    <v-card v-if="categoryToView">
      <v-card-title class="pa-4 bg-primary text-white">
        <span class="text-h6">{{ categoryToView.categoryName }}</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <div class="mb-4">
          <strong class="text-subtitle-2">{{ t('datasetCategories.view.description') }}</strong>
          <p class="text-body-1 mt-1">
            {{ categoryToView.categoryDescription || t('datasetCategories.view.noDescription') }}
          </p>
        </div>
        
        <v-divider class="my-4"></v-divider>
        
        <div class="mb-3">
          <strong class="text-subtitle-2">{{ t('datasetCategories.view.createdAt') }}</strong>
          <p class="text-body-2 mt-1">
            {{ formatDate(categoryToView.createInfo?.createdAt) }}
            <span v-if="categoryToView.createInfo?.userInfo?.userName" class="text-medium-emphasis">
              ({{ categoryToView.createInfo.userInfo.userName }})
            </span>
          </p>
        </div>
        
        <div v-if="categoryToView.lastUpdateInfo" class="mb-3">
          <strong class="text-subtitle-2">{{ t('datasetCategories.view.updatedAt') }}</strong>
          <p class="text-body-2 mt-1">
            {{ formatDate(categoryToView.lastUpdateInfo?.updatedAt) }}
            <span v-if="categoryToView.lastUpdateInfo?.userInfo?.userName" class="text-medium-emphasis">
              ({{ categoryToView.lastUpdateInfo.userInfo.userName }})
            </span>
          </p>
        </div>
        
        <div>
          <strong class="text-subtitle-2">{{ t('datasetCategories.view.historyCount') }}</strong>
          <p class="text-body-2 mt-1">
            <v-chip size="small" variant="tonal" color="info">
              {{ categoryToView.historyCount || 0 }}
            </v-chip>
          </p>
        </div>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn color="primary" variant="flat" @click="showViewDialog = false">
          {{ t('datasetCategories.buttons.close') }}
        </v-btn>
        <v-btn
          color="info"
          variant="flat"
          @click="editCategory(categoryToView); showViewDialog = false"
        >
          {{ t('datasetCategories.buttons.edit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Delete Confirmation Dialog -->
  <v-dialog v-model="showDeleteDialog" max-width="500px">
    <v-card>
      <v-card-title class="pa-4 bg-error text-white">
        <span class="text-h6">{{ t('datasetCategories.delete.title') }}</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <p class="text-subtitle-1">
          {{ t('datasetCategories.delete.message') }}
        </p>
        <p class="text-caption text-medium-emphasis mt-2">
          {{ t('datasetCategories.delete.note') }}
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn color="error" variant="flat" @click="showDeleteDialog = false">
          {{ t('datasetCategories.delete.cancel') }}
        </v-btn>
        <v-btn color="success" variant="flat" @click="confirmDelete">
          {{ t('datasetCategories.delete.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
