<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DynamicFormField from '@/components/apps/automated-forms/DynamicFormField.vue';
import { useAutomatedFormsStore } from '@/stores/apps/automatedForms';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useUserStore } from '@/stores/apps/user';
import { useGroupStore } from '@/stores/apps/group';
import { useAuthStore } from '@/stores/auth';
import { fetchFromDataGateway } from '@/services/apiService';
import { FileCodeIcon, PlusIcon, RefreshIcon, EditIcon, TrashIcon, EyeIcon, XIcon, CheckIcon } from 'vue-tabler-icons';

const route = useRoute();
const router = useRouter();
const formStore = useAutomatedFormsStore();
const datasetStore = useDatasetStore();
const userStore = useUserStore();
const groupStore = useGroupStore();
const authStore = useAuthStore();

const formCode = computed(() => route.params.formCode as string);

// Form metadata
const form = ref<any>(null);
const dataset = ref<any>(null);
const loading = ref(false);
const errorMessage = ref('');

// Data items (for list view)
const dataItems = ref<any[]>([]);
const selectedItems = ref<any[]>([]);
const totalCount = ref(0);
const tableOptions = ref({
  page: 1,
  itemsPerPage: 20,
  sortBy: [] as Array<{ key: string; order: 'asc' | 'desc' }>,
});

// Filters (field-based)
const filters = ref<Record<string, string>>({});
const showFiltersPanel = ref<number[]>([]);

// Search
const searchQuery = ref('');

// Form dialog state
const showFormDialog = ref(false);
const formDialogMode = ref<'create' | 'edit'>('create');
const formDialogData = ref<Record<string, any>>({});
const formDialogError = ref('');
const formDialogLoading = ref(false);
const formDialogRef = ref();

// Delete dialog state
const showDeleteDialog = ref(false);
const itemToDelete = ref<any>(null);
const deleteDialogLoading = ref(false);
const deleteDialogError = ref('');

// Track which item is being edited (for update operation)
const currentEditingItemId = ref<string | null>(null);

// Page info
const page = computed(() => ({
  title: form.value?.formName || 'Form Görüntüle',
}));

const breadcrumbs = computed(() => [
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Otomatik Formlar',
    disabled: false,
    href: '/apps/automated-forms',
  },
  {
    text: page.value.title,
    disabled: true,
    href: '#',
  },
]);

// Load form and dataset
const loadForm = async () => {
  loading.value = true;
  errorMessage.value = '';
  
  try {
    // Load form metadata
    const loadedForm = await formStore.fetchFormByCode(formCode.value);
    if (!loadedForm) {
      errorMessage.value = 'Form bulunamadı';
      return;
    }
    form.value = loadedForm;
    console.log('[AutomatedFormView] Loaded form:', loadedForm);
    console.log('[AutomatedFormView] Dataset name:', loadedForm.datasetName);
    
    // Load dataset
    const loadedDataset = await datasetStore.fetchDatasetByName(loadedForm.datasetName);
    console.log('[AutomatedFormView] Loaded dataset:', loadedDataset);
    if (!loadedDataset) {
      errorMessage.value = 'Dataset bulunamadı';
      return;
    }
    dataset.value = loadedDataset;
    
    // Load data items
    await fetchDataItems();
  } catch (error: any) {
    errorMessage.value = error.message || 'Form yüklenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};

// Get filterable fields
const filterableFields = computed(() => {
  if (!dataset.value || !dataset.value.fields || !form.value) return [];
  
  const fields = dataset.value.fields;
  const listConfig = form.value.listConfig;
  const columnConfigs = listConfig?.columns || [];
  
  return fields.filter(field => {
    const colConfig = columnConfigs.find(c => c.fieldName === field.name);
    return colConfig?.filterable ?? false;
  });
});

// Get form fields (filtered and ordered by formConfig)
const formFields = computed(() => {
  if (!dataset.value || !dataset.value.fields || !form.value) return [];
  
  const fields = dataset.value.fields;
  const formConfig = form.value.formConfig;
  
  // If no formConfig, return all fields (except internal fields)
  if (!formConfig) {
    return fields.filter(f => !f.name.startsWith('__'));
  }
  
  // Filter by visibleFields if defined
  let filteredFields = fields;
  if (formConfig.visibleFields && formConfig.visibleFields.length > 0) {
    filteredFields = fields.filter(f => formConfig.visibleFields!.includes(f.name));
  }
  
  // Sort by fieldOrder if defined
  if (formConfig.fieldOrder && formConfig.fieldOrder.length > 0) {
    filteredFields = [...filteredFields].sort((a, b) => {
      const indexA = formConfig.fieldOrder!.indexOf(a.name);
      const indexB = formConfig.fieldOrder!.indexOf(b.name);
      if (indexA === -1 && indexB === -1) return 0;
      if (indexA === -1) return 1;
      if (indexB === -1) return -1;
      return indexA - indexB;
    });
  }
  
  return filteredFields;
});

// Build filter string from filters object
const buildFilterString = (): string => {
  const filterParts: string[] = [];
  
  Object.entries(filters.value).forEach(([fieldName, value]) => {
    if (value && value.trim()) {
      const field = dataset.value?.fields?.find(f => f.name === fieldName);
      if (field) {
        // Choose operator based on field type
        let operator = 'contains'; // default for text
        if (field.fieldType === 'number') {
          operator = 'eq';
        } else if (field.fieldType === 'bool') {
          operator = 'eq';
        } else if (field.fieldType === 'datetime') {
          operator = 'eq';
        }
        filterParts.push(`${fieldName}:${operator}:${value.trim()}`);
      }
    }
  });
  
  return filterParts.join(',');
};

// Clear all filters
const clearFilters = () => {
  filters.value = {};
  tableOptions.value.page = 1;
  fetchDataItems();
};

// Fetch data items from dataset
const fetchDataItems = async () => {
  if (!form.value || !dataset.value) return;
  
  loading.value = true;
  try {
    const pageNumber = tableOptions.value.page || 1;
    const pageSize = tableOptions.value.itemsPerPage || 20;
    
    // Backend uses skip and limit, not pageNumber and pageSize
    const skip = (pageNumber - 1) * pageSize;
    const limit = pageSize;
    
    const queryParams = new URLSearchParams();
    queryParams.append('skip', skip.toString());
    queryParams.append('limit', limit.toString());
    
    // Add sorting if available (backend uses MongoDB style: "field1,-field2")
    if (tableOptions.value.sortBy && tableOptions.value.sortBy.length > 0) {
      const sort = tableOptions.value.sortBy[0];
      const sortDirection = sort.order === 'desc' ? '-' : '';
      queryParams.append('sort', `${sortDirection}${sort.key}`);
    }
    
    // Add filter if available
    const filterString = buildFilterString();
    if (filterString) {
      queryParams.append('filter', filterString);
    }
    
    // Add search if enabled and provided
    if (form.value.listConfig?.enableSearch && searchQuery.value && searchQuery.value.trim()) {
      queryParams.append('search', searchQuery.value.trim());
    }
    
    const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}?${queryParams.toString()}`;
    console.log('[AutomatedFormView] Fetching data from URL:', url);
    const response = await fetchFromDataGateway(url, 'GET');
    console.log('[AutomatedFormView] Response:', response);
    
    // API response format: PagedResultDto or array
    let itemsArray: any[] = [];
    let totalCountValue = 0;
    
    if (Array.isArray(response)) {
      // Direct array response
      itemsArray = response;
      totalCountValue = response.length;
    } else {
      // PagedResultDto format
      itemsArray = response.items || response.Items || [];
      totalCountValue = response.totalCount ?? response.TotalCount ?? itemsArray.length;
    }
    
    console.log('[AutomatedFormView] Parsed items:', itemsArray.length, 'Total:', totalCountValue);
    dataItems.value = itemsArray;
    totalCount.value = totalCountValue;
  } catch (error: any) {
    console.error('Data items yüklenirken hata:', error);
    errorMessage.value = error.message || 'Veriler yüklenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};

// Table headers (dynamic based on dataset fields and form config)
const tableHeaders = computed(() => {
  if (!dataset.value || !dataset.value.fields || !form.value) return [];
  
  const fields = dataset.value.fields;
  const listConfig = form.value.listConfig;
  const columnConfigs = listConfig?.columns || [];
  
  const headers: any[] = [];
  
  // Get visible columns from config, sorted by order
  const visibleColumns = columnConfigs
    .filter(col => col.visible)
    .sort((a, b) => a.order - b.order);
  
  if (visibleColumns.length > 0) {
    // Use column configs
    visibleColumns.forEach(colConfig => {
      const field = fields.find(f => f.name === colConfig.fieldName);
      if (field) {
        headers.push({
          title: field.title || field.name,
          key: field.name,
          sortable: colConfig.sortable ?? true,
          filterable: colConfig.filterable ?? false, // For future use
        });
      }
    });
  } else {
    // Fallback: show all fields if no column config
    fields.forEach(field => {
      headers.push({
        title: field.title || field.name,
        key: field.name,
        sortable: true,
        filterable: false,
      });
    });
  }
  
  // Add actions column
  headers.push({
    title: 'İşlemler',
    key: 'actions',
    sortable: false,
    filterable: false,
    align: 'end',
  });
  
  return headers;
});

// Server items length
const serverItemsLength = computed(() => totalCount.value);

// Watch table options
watch(
  () => [tableOptions.value.page, tableOptions.value.itemsPerPage, tableOptions.value.sortBy],
  () => {
    fetchDataItems();
  }
);

// Watch filters with debounce
let filterTimeout: ReturnType<typeof setTimeout> | null = null;
watch(
  () => filters.value,
  () => {
    // Reset to first page when filter changes
    if (tableOptions.value.page !== 1) {
      tableOptions.value.page = 1;
    }
    
    // Debounce filter input (500ms)
    if (filterTimeout) {
      clearTimeout(filterTimeout);
    }
    filterTimeout = setTimeout(() => {
      fetchDataItems();
    }, 500);
  },
  { deep: true }
);

// Watch search with debounce
let searchTimeout: ReturnType<typeof setTimeout> | null = null;
watch(
  () => searchQuery.value,
  () => {
    // Reset to first page when search changes
    if (tableOptions.value.page !== 1) {
      tableOptions.value.page = 1;
    }
    
    // Debounce search input (500ms)
    if (searchTimeout) {
      clearTimeout(searchTimeout);
    }
    searchTimeout = setTimeout(() => {
      fetchDataItems();
    }, 500);
  }
);

// Watch formCode route parameter - when route changes, reload form
watch(
  () => formCode.value,
  async (newFormCode, oldFormCode) => {
    // Only reload if formCode actually changed
    if (newFormCode && newFormCode !== oldFormCode) {
      console.log('[AutomatedFormView] FormCode changed:', { old: oldFormCode, new: newFormCode });
      
      // Reset state
      form.value = null;
      dataset.value = null;
      dataItems.value = [];
      filters.value = {};
      searchQuery.value = '';
      tableOptions.value.page = 1;
      
      // Clear current form in store to prevent cache issues
      formStore.resetCurrentForm();
      
      // Reload form
      await loadForm();
    }
  },
  { immediate: false }
);

onMounted(async () => {
  await loadForm();
});

// Refresh data
const refreshData = async () => {
  await fetchDataItems();
};

// Initialize form dialog data
const initializeFormDialogData = () => {
  if (!dataset.value || !dataset.value.fields) return {};
  
  const data: Record<string, any> = {};
  
  dataset.value.fields.forEach(field => {
    if (field.fieldType === 'incremental') {
      // Incremental fields are auto-generated, skip
      return;
    }
    
    if (field.defaultValue !== undefined && field.defaultValue !== null) {
      data[field.name] = field.defaultValue;
    } else if (field.fieldType === 'bool') {
      data[field.name] = false;
    } else if (field.fieldType === 'object') {
      data[field.name] = {};
    } else if (field.isArray) {
      data[field.name] = [];
    } else {
      data[field.name] = null;
    }
  });
  
  return data;
};

// Open create dialog
const createNewItem = () => {
  if (!form.value || !dataset.value) return;
  
  formDialogMode.value = 'create';
  formDialogData.value = initializeFormDialogData();
  formDialogError.value = '';
  showFormDialog.value = true;
};

// Open edit dialog
const editItem = async (item: any) => {
  if (!form.value || !dataset.value) return;
  
  formDialogMode.value = 'edit';
  formDialogError.value = '';
  currentEditingItemId.value = item.__dataId;
  
  try {
    // Fetch full item data (with expand if needed)
    const dataId = item.__dataId;
    const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}/${encodeURIComponent(dataId)}?expand=true`;
    const response = await fetchFromDataGateway(url, 'GET');
    
    // Backend always returns array (even for single item)
    // Extract the first item from array
    const itemData = Array.isArray(response) ? (response.length > 0 ? response[0] : {}) : response;
    
    // Initialize form data with fetched item (remove internal fields)
    const data: Record<string, any> = {};
    dataset.value.fields.forEach(field => {
      // Check if field exists in itemData
      if (itemData[field.name] !== undefined) {
        data[field.name] = itemData[field.name];
      } else if (field.defaultValue !== undefined && field.defaultValue !== null) {
        data[field.name] = field.defaultValue;
      } else if (field.fieldType === 'bool') {
        data[field.name] = false;
      } else if (field.fieldType === 'object') {
        data[field.name] = {};
      } else if (field.isArray) {
        data[field.name] = [];
      } else {
        data[field.name] = null;
      }
    });
    
    formDialogData.value = data;
    showFormDialog.value = true;
  } catch (error: any) {
    formDialogError.value = error.message || 'Kayıt yüklenirken bir hata oluştu';
    console.error('Error loading item for edit:', error);
  }
};

// Save form (create or update)
const saveForm = async () => {
  if (!form.value || !dataset.value || !formDialogRef.value) return;
  
  // Validate form
  const { valid } = await formDialogRef.value.validate();
  if (!valid) {
    return;
  }
  
  formDialogLoading.value = true;
  formDialogError.value = '';
  
  try {
    // Prepare data to send (remove null values for optional fields, keep internal fields structure)
    const dataToSend: Record<string, any> = {};
    
    dataset.value.fields.forEach(field => {
      // Skip incremental fields in create mode
      if (field.fieldType === 'incremental' && formDialogMode.value === 'create') {
        return;
      }
      
      const value = formDialogData.value[field.name];
      
      // Only include field if it's not undefined
      if (value !== undefined) {
        // Handle array fields - always convert to array if field.isArray is true
        if (field.isArray) {
          if (value === null || value === undefined || value === '') {
            dataToSend[field.name] = [];
          } else if (Array.isArray(value)) {
            dataToSend[field.name] = value;
          } else {
            // Single value selected but field is array - convert to array
            dataToSend[field.name] = [value];
          }
        } else if (value === '' && !field.mandatory) {
          // Convert empty strings to null for optional fields
          dataToSend[field.name] = null;
        } else {
          dataToSend[field.name] = value;
        }
      }
    });
    
    if (formDialogMode.value === 'create') {
      // Create new item
      const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}`;
      await fetchFromDataGateway(url, 'POST', dataToSend);
    } else {
      // Update existing item
      if (!currentEditingItemId.value) {
        throw new Error('Kayıt ID bulunamadı');
      }
      
      const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}/${encodeURIComponent(currentEditingItemId.value)}`;
      await fetchFromDataGateway(url, 'PUT', dataToSend);
    }
    
    // Close dialog and refresh data
    showFormDialog.value = false;
    await fetchDataItems();
  } catch (error: any) {
    console.error('Error saving form:', error);
    
    // Parse error message from API response
    let errorMessage = '';
    
    // Try to parse validation errors from error.data
    if (error?.data?.error) {
      const errorData = error.data.error;
      
      // Check for validation error details
      if (errorData.details && Array.isArray(errorData.details) && errorData.details.length > 0) {
        // Build detailed error message from validation details
        const validationErrors = errorData.details.map((detail: any) => {
          const fieldName = detail.field || 'Alan';
          const message = detail.message || 'Hata';
          return `• ${fieldName}: ${message}`;
        }).join('\n');
        
        errorMessage = `${errorData.message || 'Validasyon hatası'}\n\n${validationErrors}`;
      } else if (errorData.message) {
        errorMessage = errorData.message;
      } else if (typeof errorData === 'string') {
        errorMessage = errorData;
      }
    } else if (error?.data?.message) {
      errorMessage = error.data.message;
    } else if (error?.message) {
      errorMessage = error.message;
    } else if (error?.statusMessage) {
      errorMessage = error.statusMessage;
    }
    
    // Fallback to default message if still empty
    if (!errorMessage) {
      errorMessage = formDialogMode.value === 'create' 
        ? 'Kayıt oluşturulurken bir hata oluştu' 
        : 'Kayıt güncellenirken bir hata oluştu';
    }
    
    formDialogError.value = errorMessage;
  } finally {
    formDialogLoading.value = false;
  }
};

// Close form dialog
const closeFormDialog = () => {
  showFormDialog.value = false;
  formDialogData.value = {};
  formDialogError.value = '';
  currentEditingItemId.value = null;
};

// Open delete confirmation
const deleteItem = (item: any) => {
  itemToDelete.value = item;
  deleteDialogError.value = '';
  showDeleteDialog.value = true;
};

// Confirm delete
const confirmDelete = async () => {
  if (!form.value || !itemToDelete.value) return;
  
  deleteDialogLoading.value = true;
  deleteDialogError.value = '';
  
  try {
    const dataId = itemToDelete.value.__dataId;
    const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}/${encodeURIComponent(dataId)}`;
    await fetchFromDataGateway(url, 'DELETE');
    
    // Close dialog and refresh data
    showDeleteDialog.value = false;
    itemToDelete.value = null;
    await fetchDataItems();
  } catch (error: any) {
    deleteDialogError.value = error.message || 'Kayıt silinirken bir hata oluştu';
    console.error('Error deleting item:', error);
  } finally {
    deleteDialogLoading.value = false;
  }
};

// Close delete dialog
const closeDeleteDialog = () => {
  showDeleteDialog.value = false;
  itemToDelete.value = null;
  deleteDialogError.value = '';
};

// Get relation config for a field
const getRelationConfig = (fieldName: string) => {
  if (!form.value || !form.value.formConfig || !form.value.formConfig.relationFieldConfig) {
    return undefined;
  }
  return form.value.formConfig.relationFieldConfig[fieldName];
};

// Get field column span from layout config
const getFieldColumnSpan = (fieldName: string) => {
  if (!form.value || !form.value.formConfig || !form.value.formConfig.fieldLayout) {
    // Default: 12 for object, 6 for others
    const field = dataset.value?.fields?.find(f => f.name === fieldName);
    return field?.fieldType === 'object' ? 12 : 6;
  }
  
  const layout = form.value.formConfig.fieldLayout[fieldName];
  if (layout && layout.columnSpan) {
    return layout.columnSpan;
  }
  
  // Default: 12 for object, 6 for others
  const field = dataset.value?.fields?.find(f => f.name === fieldName);
  return field?.fieldType === 'object' ? 12 : 6;
};

// Format cell value
const formatCellValue = (value: any, fieldType: string) => {
  if (value === null || value === undefined) return '-';
  
  if (fieldType === 'bool') {
    return value ? 'Evet' : 'Hayır';
  }
  
  if (fieldType === 'datetime') {
    try {
      return new Date(value).toLocaleString('tr-TR');
    } catch {
      return value;
    }
  }
  
  if (fieldType === 'object') {
    return JSON.stringify(value);
  }
  
  if (Array.isArray(value)) {
    return value.join(', ');
  }
  
  return String(value);
};
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-item>
      <!-- Toolbar -->
      <div class="d-flex justify-space-between align-center mb-4 flex-wrap ga-3">
        <div class="d-flex ga-3 align-center">
          <v-btn
            color="primary"
            variant="flat"
            @click="createNewItem"
            :disabled="loading || !form || !dataset"
          >
            <template #prepend>
              <PlusIcon size="18" />
            </template>
            Yeni Kayıt
          </v-btn>
          
          <v-btn
            icon
            variant="outlined"
            @click="refreshData"
            :disabled="loading"
            :loading="loading"
          >
            <RefreshIcon size="18" />
          </v-btn>
        </div>
      </div>
      
      <!-- Error Message -->
      <v-alert
        v-if="errorMessage"
        type="error"
        variant="tonal"
        density="compact"
        class="mb-4"
        closable
        @click:close="errorMessage = ''"
      >
        {{ errorMessage }}
      </v-alert>
      
      <!-- Loading State -->
      <div v-if="loading && !form" class="text-center py-12">
        <v-progress-circular
          indeterminate
          color="primary"
          size="64"
        ></v-progress-circular>
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">Form yükleniyor...</p>
      </div>
      
      <!-- Form Info -->
      <div v-if="form && dataset" class="mb-4">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4">
          <div class="d-flex align-center ga-2">
            <FileCodeIcon size="18" />
            <div>
              <strong>{{ form.formName }}</strong>
              <span v-if="form.description" class="text-caption ml-2">- {{ form.description }}</span>
            </div>
            <v-spacer></v-spacer>
            <v-chip size="small" color="info" variant="tonal">
              {{ form.datasetName }}
            </v-chip>
          </div>
        </v-alert>
      </div>
      
      <!-- Search (if enabled) -->
      <div v-if="form && dataset && form.listConfig?.enableSearch" class="mb-4">
        <v-text-field
          v-model="searchQuery"
          prepend-inner-icon="mdi-magnify"
          label="Ara..."
          placeholder="Arama yapmak için bir terim girin..."
          variant="outlined"
          density="compact"
          clearable
          hide-details
        ></v-text-field>
      </div>
      
      <!-- Filters Panel -->
      <v-expansion-panels v-if="form && dataset && filterableFields.length > 0" v-model="showFiltersPanel" variant="accordion" class="mb-4">
        <v-expansion-panel>
          <v-expansion-panel-title>
            <div class="d-flex align-center ga-2">
              <v-icon>mdi-filter</v-icon>
              <span>Filtreler</span>
              <v-chip
                v-if="Object.keys(filters).some(key => filters[key] && filters[key].trim())"
                size="x-small"
                color="primary"
                variant="flat"
              >
                {{ Object.keys(filters).filter(key => filters[key] && filters[key].trim()).length }}
              </v-chip>
            </div>
          </v-expansion-panel-title>
          <v-expansion-panel-text>
            <v-row>
              <v-col
                v-for="field in filterableFields"
                :key="field.name"
                cols="12"
                md="6"
                lg="4"
              >
                <v-text-field
                  v-model="filters[field.name]"
                  :label="field.title || field.name"
                  :placeholder="`${field.title || field.name} ile filtrele...`"
                  variant="outlined"
                  density="compact"
                  clearable
                  hide-details
                  prepend-inner-icon="mdi-filter-variant"
                ></v-text-field>
              </v-col>
              <v-col cols="12" v-if="filterableFields.length > 0">
                <v-btn
                  color="default"
                  variant="outlined"
                  size="small"
                  @click="clearFilters"
                  :disabled="!Object.keys(filters).some(key => filters[key] && filters[key].trim())"
                >
                  <v-icon size="16" class="mr-2">mdi-filter-off</v-icon>
                  Filtreleri Temizle
                </v-btn>
              </v-col>
            </v-row>
          </v-expansion-panel-text>
        </v-expansion-panel>
      </v-expansion-panels>
      
      <!-- Empty State -->
      <v-alert
        v-if="form && dataset && !loading && dataItems.length === 0"
        type="info"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <div class="d-flex align-center ga-2">
          <v-icon>mdi-information</v-icon>
          <div>
            <strong>Henüz veri yok</strong>
            <p class="text-caption mb-0 mt-1">
              Bu dataset'te henüz kayıt bulunmuyor. "Yeni Kayıt" butonuna tıklayarak ilk kaydı oluşturabilirsiniz.
            </p>
          </div>
        </div>
      </v-alert>
      
      <!-- Data Table -->
      <v-data-table
        v-if="form && dataset"
        v-model="selectedItems"
        v-model:options="tableOptions"
        :headers="tableHeaders"
        :items="dataItems"
        :loading="loading"
        :server-items-length="serverItemsLength"
        :items-per-page-options="[10, 20, 50, 100]"
        item-value="__dataId"
        class="border rounded-md"
        hide-default-footer
      >
        <!-- Dynamic Field Columns -->
        <template 
          v-for="header in tableHeaders.filter(h => h.key !== 'actions')" 
          :key="header.key"
          #[`item.${header.key}`]="{ item, value }"
        >
          <span class="text-body-2">
            {{ formatCellValue(value, dataset?.fields?.find(f => f.name === header.key)?.fieldType || 'text') }}
          </span>
        </template>
        
        <!-- Actions Column -->
        <template v-slot:item.actions="{ item }">
          <div class="d-flex ga-2 justify-end">
            <v-btn
              icon
              size="small"
              variant="text"
              @click="editItem(item)"
              title="Düzenle"
            >
              <EditIcon size="18" />
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteItem(item)"
              title="Sil"
            >
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>
      </v-data-table>
      
      <!-- Pagination -->
      <div v-if="form && dataset && totalCount > 0" class="d-flex align-center justify-space-between mt-4">
        <div class="text-caption text-medium-emphasis">
          Toplam {{ totalCount }} kayıt
        </div>
        <v-pagination
          v-model="tableOptions.page"
          :length="Math.ceil(totalCount / tableOptions.itemsPerPage)"
          :total-visible="7"
        ></v-pagination>
      </div>
    </v-card-item>
  </v-card>
  
  <!-- Create/Edit Form Dialog -->
  <v-dialog v-model="showFormDialog" max-width="900px" persistent scrollable>
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
        <FileCodeIcon class="mr-2" size="20" />
        <span>{{ formDialogMode === 'edit' ? 'Kayıt Düzenle' : 'Yeni Kayıt' }}</span>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6">
        <!-- Form Error -->
        <v-alert
          v-if="formDialogError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="formDialogError = ''"
        >
          <div style="white-space: pre-wrap;">{{ formDialogError }}</div>
        </v-alert>
        
        <v-form ref="formDialogRef" v-if="form && dataset">
          <v-row>
            <v-col
              v-for="field in formFields"
              :key="field.name"
              cols="12"
              :md="getFieldColumnSpan(field.name)"
            >
              <DynamicFormField
                :field="field"
                v-model="formDialogData[field.name]"
                :readonly="form.formConfig?.readonlyFields?.includes(field.name)"
                :disabled="formDialogLoading"
                :error-messages="[]"
                :relation-config="getRelationConfig(field.name)"
              />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      
      <v-divider></v-divider>
      
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn
          color="default"
          variant="outlined"
          @click="closeFormDialog"
          :disabled="formDialogLoading"
        >
          <XIcon class="mr-2" size="18" />
          İptal
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          @click="saveForm"
          :loading="formDialogLoading"
        >
          <CheckIcon class="mr-2" size="18" />
          {{ formDialogMode === 'edit' ? 'Güncelle' : 'Kaydet' }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  
  <!-- Delete Confirmation Dialog -->
  <v-dialog v-model="showDeleteDialog" max-width="500px">
    <v-card>
      <v-card-title class="pa-4 bg-error text-white">
        <TrashIcon class="mr-2" size="20" />
        Kayıt Sil
      </v-card-title>
      <v-card-text class="pa-6">
        <!-- Error Message -->
        <v-alert
          v-if="deleteDialogError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="deleteDialogError = ''"
        >
          {{ deleteDialogError }}
        </v-alert>
        
        <p v-if="!deleteDialogError" class="text-subtitle-1">
          Bu kaydı silmek istediğinizden emin misiniz? Bu işlem geri alınamaz.
        </p>
      </v-card-text>
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn
          color="default"
          variant="outlined"
          @click="closeDeleteDialog"
          :disabled="deleteDialogLoading"
        >
          {{ deleteDialogError ? 'Kapat' : 'İptal' }}
        </v-btn>
        <v-btn
          v-if="!deleteDialogError"
          color="error"
          variant="flat"
          @click="confirmDelete"
          :loading="deleteDialogLoading"
        >
          Evet, Sil
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
