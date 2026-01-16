<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';
import { useAuthStore } from '@/stores/auth';
import { EditIcon, EyeIcon, TrashIcon, DatabaseIcon, PlusIcon, RefreshIcon, TagIcon, DownloadIcon } from 'vue-tabler-icons';
import { exportToCSV, exportArrayToJSON } from '@/utils/exportUtils';

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

const datasetStore = useDatasetStore();
const categoryStore = useDatasetCategoryStore();
const authStore = useAuthStore();
const router = useRouter();

const page = computed(() => ({ title: t('datasets.title') }));
const breadcrumbs = computed(() => [
  {
    text: t('datasets.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('datasets.breadcrumbs.datasets'),
    disabled: true,
    href: '#',
  },
]);

const search = ref('');
const selectedDatasets = ref([]);
const showDeleteDialog = ref(false);
const datasetToDelete = ref<string | null>(null);
const showViewDialog = ref(false);
const datasetToView = ref<any>(null);

// Server-side pagination options
const tableOptions = ref({
  page: 1,
  itemsPerPage: 20,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

const headers = computed(() => [
  { title: t('datasets.table.headers.name'), key: 'name', sortable: true },
  { title: t('datasets.table.headers.category'), key: 'category', sortable: false },
  { title: t('datasets.table.headers.description'), key: 'description', sortable: false },
  { title: t('datasets.table.headers.fieldsCount'), key: 'fieldsCount', sortable: true },
  { title: t('datasets.table.headers.createdAt'), key: 'createInfo.createdAt', sortable: true },
  { title: t('datasets.table.headers.createdBy'), key: 'createInfo.userInfo.userName', sortable: false },
  { title: t('datasets.table.headers.updatedAt'), key: 'lastUpdateInfo.updatedAt', sortable: true },
  { title: t('datasets.table.headers.actions'), key: 'actions', sortable: false, align: 'end' },
]);

// Computed: Server items length (reactive)
const serverItemsLength = computed(() => {
  return datasetStore.totalCount || 0;
});

// Fetch categories for display
const loadCategories = async () => {
  try {
    await categoryStore.fetchCategories({ pageNumber: 1, pageSize: 1000 });
  } catch (error) {
    // Error handled by store
  }
};

// Get category name by ID
const getCategoryName = (categoryId: string | undefined | null): string => {
  if (!categoryId) return '-';
  const category = categoryStore.getCategoryById(categoryId);
  return category?.categoryName || categoryId;
};

// Fetch datasets function
const fetchDatasets = async (options?: any) => {
  const currentOptions = options || tableOptions.value;
  
  const params: {
    pageNumber?: number;
    pageSize?: number;
  } = {
    pageNumber: currentOptions.page || 1,
    pageSize: currentOptions.itemsPerPage || 20,
  };
  
  // Note: search parameter is not yet implemented in backend
  // if (search.value && search.value.trim()) {
  //   params.search = search.value.trim();
  // }
  
  try {
    await datasetStore.fetchDatasets(params);
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
    if (isInitialLoad.value) {
      return;
    }
    fetchDatasets();
  }
);

// Watch for search changes (debounced) - Note: Backend search not yet implemented
let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(search, () => {
  // Reset to first page when search changes
  if (tableOptions.value.page !== 1) {
    tableOptions.value.page = 1;
  }
  
  // Debounce search input (500ms)
  // Note: Backend search is not yet implemented, so this is just UI preparation
  if (searchTimeout) {
    clearTimeout(searchTimeout);
  }
  searchTimeout = setTimeout(() => {
    // fetchDatasets(); // Uncomment when backend search is implemented
  }, 500);
});

onMounted(async () => {
  // Load categories first (for category name display)
  await loadCategories();
  
  // Initial load
  isInitialLoad.value = true;
  await fetchDatasets();
  isInitialLoad.value = false;
});

const editDataset = (dataset: any) => {
  router.push(`/apps/datasets/edit/${encodeURIComponent(dataset.name)}`);
};

const viewDataset = (dataset: any) => {
  router.push(`/apps/datasets/${encodeURIComponent(dataset.name)}`);
};

const deleteDataset = (dataset: any) => {
  datasetToDelete.value = dataset.name;
  showDeleteDialog.value = true;
};

const confirmDelete = async () => {
  if (datasetToDelete.value) {
    try {
      await datasetStore.deleteDataset(datasetToDelete.value);
      showDeleteDialog.value = false;
      datasetToDelete.value = null;
      
      // Refresh list
      await fetchDatasets();
    } catch (error) {
      // Error handled by store
    }
  }
};

const refreshDatasets = async () => {
  await fetchDatasets();
};

const createNewDataset = () => {
  router.push('/apps/datasets/create');
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

// Export functions
const exporting = ref(false);

// Fetch all datasets for export (without pagination)
const fetchAllDatasetsForExport = async (): Promise<any[]> => {
  const allDatasets: any[] = [];
  let currentPage = 1;
  const pageSize = 100; // Large page size to minimize requests
  let hasMore = true;
  
  while (hasMore) {
    try {
      const params: {
        pageNumber?: number;
        pageSize?: number;
      } = {
        pageNumber: currentPage,
        pageSize: pageSize,
      };
      
      // Note: If search is implemented in backend, add it here
      // if (search.value && search.value.trim()) {
      //   params.search = search.value.trim();
      // }
      
      await datasetStore.fetchDatasets(params);
      
      if (datasetStore.datasets && datasetStore.datasets.length > 0) {
        allDatasets.push(...datasetStore.datasets);
        
        // Check if there are more pages
        const totalPages = datasetStore.totalPages || 1;
        if (currentPage >= totalPages) {
          hasMore = false;
        } else {
          currentPage++;
        }
      } else {
        hasMore = false;
      }
    } catch (error) {
      console.error('Error fetching datasets for export:', error);
      hasMore = false;
    }
  }
  
  return allDatasets;
};

// Prepare dataset data for export (flatten nested objects)
const prepareDatasetForExport = (dataset: any): any => {
  return {
    [t('datasets.table.headers.name')]: dataset.name || '',
    [t('datasets.table.headers.category')]: getCategoryName(dataset.category),
    [t('datasets.table.headers.description')]: dataset.description || '',
    [t('datasets.table.headers.fieldsCount')]: dataset.fieldsCount || 0,
    [t('datasets.export.createdAt')]: formatDate(dataset.createInfo?.createdAt) || '',
    [t('datasets.table.headers.createdBy')]: dataset.createInfo?.userInfo?.userName || '',
    [t('datasets.table.headers.updatedAt')]: formatDate(dataset.lastUpdateInfo?.updatedAt) || '',
    [t('datasets.export.forceSchema')]: dataset.forceSchema ? t('datasets.common.yes') : t('datasets.common.no'),
    [t('datasets.export.logging')]: dataset.logging || 'none',
    [t('datasets.export.publishMode')]: dataset.publishMode || 'none',
  };
};

// Export to CSV
const handleExportCSV = async () => {
  exporting.value = true;
  try {
    const allDatasets = await fetchAllDatasetsForExport();
    
    if (allDatasets.length === 0) {
      // Show message or use toast
      return;
    }
    
    const exportData = allDatasets.map(prepareDatasetForExport);
    exportToCSV(exportData, 'datasets');
  } catch (error) {
    console.error('Error exporting to CSV:', error);
  } finally {
    exporting.value = false;
  }
};

// Export to JSON
const handleExportJSON = async () => {
  exporting.value = true;
  try {
    const allDatasets = await fetchAllDatasetsForExport();
    
    if (allDatasets.length === 0) {
      return;
    }
    
    exportArrayToJSON(allDatasets, 'datasets');
  } catch (error) {
    console.error('Error exporting to JSON:', error);
  } finally {
    exporting.value = false;
  }
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
            :label="t('datasets.search.placeholder')"
            :placeholder="t('datasets.search.placeholder')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 300px;"
            clearable
            disabled
            :hint="t('datasets.search.comingSoon')"
            persistent-hint
          />
          
          <v-btn
            icon
            size="small"
            variant="outlined"
            color="default"
            @click="refreshDatasets"
            :loading="datasetStore.loading"
          >
            <RefreshIcon size="18" />
            <v-tooltip activator="parent" location="top">{{ t('datasets.buttons.refresh') }}</v-tooltip>
          </v-btn>
          
          <!-- Export Buttons -->
          <v-menu>
            <template v-slot:activator="{ props }">
              <v-btn
                v-bind="props"
                icon
                size="small"
                variant="outlined"
                color="success"
                :loading="exporting"
                :disabled="datasetStore.loading || exporting"
              >
                <DownloadIcon size="18" />
                <v-tooltip activator="parent" location="top">{{ t('datasets.buttons.export') }}</v-tooltip>
              </v-btn>
            </template>
            <v-list density="compact">
              <v-list-item @click="handleExportCSV" :disabled="exporting">
                <template v-slot:prepend>
                  <v-icon size="small">mdi-file-delimited</v-icon>
                </template>
                <v-list-item-title>{{ t('datasets.export.csv') }}</v-list-item-title>
              </v-list-item>
              <v-list-item @click="handleExportJSON" :disabled="exporting">
                <template v-slot:prepend>
                  <v-icon size="small">mdi-code-json</v-icon>
                </template>
                <v-list-item-title>{{ t('datasets.export.json') }}</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>
        </div>
        
        <v-btn 
          color="primary" 
          variant="flat"
          @click="createNewDataset"
        >
          <PlusIcon class="mr-2" size="20" />
          {{ t('datasets.buttons.newDataset') }}
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
        <strong>{{ t('datasets.warning.title') }}</strong> {{ t('datasets.warning.message') }}
      </v-alert>

      <!-- Search Not Implemented Warning -->
      <v-alert
        type="info"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
      >
        <strong>{{ t('datasets.search.notImplemented.title') }}</strong> {{ t('datasets.search.notImplemented.message') }}
      </v-alert>

      <!-- Error Message -->
      <v-alert
        v-if="datasetStore.error"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="datasetStore.clearError"
      >
        {{ datasetStore.error }}
      </v-alert>

      <!-- Data Table -->
      <v-data-table
        v-model="selectedDatasets"
        v-model:options="tableOptions"
        :headers="headers"
        :items="datasetStore.datasets"
        :loading="datasetStore.loading"
        :server-items-length="serverItemsLength"
        :items-per-page-options="[20, 50, 100]"
        item-value="name"
        class="border rounded-md"
        hide-default-footer
      >
        <!-- Dataset Name Column -->
        <template v-slot:item.name="{ item }">
          <div class="d-flex align-center ga-2">
            <DatabaseIcon size="18" class="text-primary" />
            <span class="text-subtitle-1 font-weight-medium">
              {{ item.name }}
            </span>
          </div>
        </template>

        <!-- Category Column -->
        <template v-slot:item.category="{ item }">
          <v-chip
            v-if="item.category"
            size="small"
            variant="tonal"
            color="primary"
          >
            <TagIcon size="14" class="mr-1" />
            {{ getCategoryName(item.category) }}
          </v-chip>
          <span v-else class="text-caption text-medium-emphasis">-</span>
        </template>

        <!-- Description Column -->
        <template v-slot:item.description="{ item }">
          <span class="text-body-2 text-medium-emphasis" :title="item.description || ''">
            {{ truncateText(item.description, 60) }}
          </span>
        </template>

        <!-- Fields Count Column -->
        <template v-slot:item.fieldsCount="{ value }">
          <v-chip size="small" variant="tonal" color="info">
            {{ value || 0 }} {{ t('datasets.table.fields') }}
          </v-chip>
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

        <!-- Actions Column -->
        <template v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <v-btn
              icon
              size="small"
              variant="text"
              color="primary"
              @click="viewDataset(item)"
            >
              <EyeIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('datasets.buttons.view') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="info"
              @click="editDataset(item)"
            >
              <EditIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('datasets.buttons.edit') }}</v-tooltip>
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteDataset(item)"
            >
              <TrashIcon size="18" />
              <v-tooltip activator="parent" location="top">{{ t('datasets.buttons.delete') }}</v-tooltip>
            </v-btn>
          </div>
        </template>

        <!-- No Data -->
        <template v-slot:no-data>
          <div class="text-center py-8">
            <p class="text-subtitle-1 text-medium-emphasis">{{ t('datasets.noData.title') }}</p>
            <v-btn 
              color="primary" 
              variant="flat"
              @click="createNewDataset" 
              class="mt-4"
            >
              <PlusIcon class="mr-2" size="20" />
              {{ t('datasets.noData.button') }}
            </v-btn>
          </div>
        </template>

        <!-- Custom bottom slot with total count and pagination -->
        <template v-slot:bottom>
          <div class="d-flex justify-space-between align-center pa-3 border-top">
            <div class="text-caption text-medium-emphasis">
              <strong>{{ t('datasets.pagination.total') }}</strong> {{ datasetStore.totalCount }} {{ t('datasets.pagination.records') }}
              <span v-if="datasetStore.totalPages > 1" class="ml-2">
                ({{ t('datasets.pagination.page') }} {{ tableOptions.page }} / {{ datasetStore.totalPages }})
              </span>
              <span class="ml-2">
                | {{ ((tableOptions.page - 1) * tableOptions.itemsPerPage) + 1 }} - 
                {{ Math.min(tableOptions.page * tableOptions.itemsPerPage, datasetStore.totalCount) }} 
                / {{ datasetStore.totalCount }} {{ t('datasets.pagination.showing') }}
              </span>
            </div>
            <div class="d-flex align-center ga-2">
              <span class="text-caption text-medium-emphasis">{{ t('datasets.pagination.itemsPerPage') }}</span>
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
              :length="datasetStore.totalPages"
              :total-visible="7"
              density="compact"
            />
          </div>
        </template>
      </v-data-table>
    </v-card-item>
  </v-card>

  <!-- Delete Confirmation Dialog -->
  <v-dialog v-model="showDeleteDialog" max-width="500px">
    <v-card>
      <v-card-title class="pa-4 bg-error text-white">
        <span class="text-h6">{{ t('datasets.delete.title') }}</span>
      </v-card-title>
      <v-card-text class="pa-6">
        <p class="text-subtitle-1">
          {{ t('datasets.delete.message') }}
        </p>
        <p class="text-caption text-medium-emphasis mt-2">
          <strong>{{ t('datasets.delete.note') }}</strong> {{ t('datasets.delete.noteDetail') }}
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn color="error" variant="flat" @click="showDeleteDialog = false">
          {{ t('datasets.delete.cancel') }}
        </v-btn>
        <v-btn color="success" variant="flat" @click="confirmDelete">
          {{ t('datasets.delete.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
