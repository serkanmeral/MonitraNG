<script setup lang="ts">
import { ref, onMounted, onBeforeUnmount, computed, watch, nextTick } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DynamicFormField from '@/components/apps/automated-forms/DynamicFormField.vue';
import { useAutomatedFormsStore } from '@/stores/apps/automatedForms';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useUserStore } from '@/stores/apps/user';
import { useGroupStore } from '@/stores/apps/group';
import { useAuthStore } from '@/stores/auth';
import { useLocaleStore } from '@/stores/locale';
import { fetchFromDataGateway } from '@/services/apiService';
import { FileCodeIcon, PlusIcon, RefreshIcon, EditIcon, TrashIcon, EyeIcon, XIcon, CheckIcon, DownloadIcon } from 'vue-tabler-icons';
import { useFieldLabel } from '@/composables/useFieldLabel';

const { getFieldLabel } = useFieldLabel();

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

const route = useRoute();
const router = useRouter();
const formStore = useAutomatedFormsStore();
const datasetStore = useDatasetStore();
const userStore = useUserStore();
const groupStore = useGroupStore();
const authStore = useAuthStore();
const localeStore = useLocaleStore();

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
  itemsPerPage: 10,
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
const formDialogSuccess = ref('');
const formDialogLoading = ref(false);
const formDialogRef = ref();
// Field-level error messages (field name -> error message array)
const fieldErrors = ref<Record<string, string[]>>({});

// Delete dialog state
const showDeleteDialog = ref(false);
const itemToDelete = ref<any>(null);
const deleteDialogLoading = ref(false);
const deleteDialogError = ref('');

// Track which item is being edited (for update operation)
const currentEditingItemId = ref<string | null>(null);

// Page info
const page = computed(() => ({
  title: form.value?.formName || t('automated-forms.view.title'),
}));

const breadcrumbs = computed(() => [
  {
    text: t('automated-forms.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('automated-forms.breadcrumbs.automatedForms'),
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
      errorMessage.value = t('automated-forms.form.messages.notFound');
      return;
    }
    form.value = loadedForm;
    
    // Load dataset
    const loadedDataset = await datasetStore.fetchDatasetByName(loadedForm.datasetName);
    if (!loadedDataset) {
      errorMessage.value = t('automated-forms.view.messages.loadError');
      return;
    }
    dataset.value = loadedDataset;
    
    // Load data items
    await fetchDataItems();
    
    // Mark initial load as complete
    isInitialLoad.value = false;
  } catch (error: any) {
    errorMessage.value = error.message || t('automated-forms.view.messages.loadError');
    isInitialLoad.value = false;
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
    const pageSize = tableOptions.value.itemsPerPage || 10;
    
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
    
    // API response format: Always array (QueryResultDto.Data is returned as array by controller)
    let itemsArray: any[] = [];
    
    if (Array.isArray(response)) {
      itemsArray = response;
    } else if (response?.data && Array.isArray(response.data)) {
      // If wrapped in data property
      itemsArray = response.data;
    } else if (response?.items && Array.isArray(response.items)) {
      // If wrapped in items property
      itemsArray = response.items;
    } else if (response?.Items && Array.isArray(response.Items)) {
      // If wrapped in Items property (capital I)
      itemsArray = response.Items;
    } else {
      itemsArray = [];
    }
    
    // Get totalCount from response header (added by server route)
    // Check if response has _totalCount property (added by apiService)
    if (response && typeof response === 'object' && '_totalCount' in response) {
      totalCount.value = (response as any)._totalCount;
    } else {
      // Fallback: estimate based on current page data
      const currentItemCount = itemsArray.length;
      const isLastPage = currentItemCount < pageSize;
      
      if (isLastPage) {
        // Last page: we can calculate exact totalCount
        totalCount.value = (pageNumber - 1) * pageSize + currentItemCount;
      } else {
        // Not last page: we estimate minimum totalCount
        totalCount.value = pageNumber * pageSize + 1;
      }
    }
    
    console.log('[AutomatedFormView] Parsed items:', itemsArray.length, 'Total:', totalCount.value, 'Page:', pageNumber);
    dataItems.value = itemsArray;
  } catch (error: any) {
    console.error('Data items yüklenirken hata:', error);
    errorMessage.value = error.message || t('automated-forms.view.messages.dataLoadError');
    dataItems.value = [];
    totalCount.value = 0;
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
          title: getFieldLabel(field.name, field, form.value, form.value?.datasetName),
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
        title: getFieldLabel(field.name, field, form.value, form.value?.datasetName),
        key: field.name,
        sortable: true,
        filterable: false,
      });
    });
  }
  
  // Add actions column
  headers.push({
    title: t('automated-forms.view.table.headers.actions'),
    key: 'actions',
    sortable: false,
    filterable: false,
    align: 'end',
  });
  
  return headers;
});

// Server items length
const serverItemsLength = computed(() => totalCount.value);

// Computed properties for pagination
const currentPage = computed({
  get: () => tableOptions.value.page,
  set: (value) => {
    tableOptions.value.page = value;
  }
});

const itemsPerPage = computed({
  get: () => tableOptions.value.itemsPerPage,
  set: (value) => {
    tableOptions.value.itemsPerPage = value;
    tableOptions.value.page = 1; // Reset to first page when itemsPerPage changes
  }
});

const totalPages = computed(() => {
  if (totalCount.value === 0) return 1;
  return Math.ceil(totalCount.value / tableOptions.value.itemsPerPage);
});

// Flag to prevent handler from triggering on initial load
const isInitialLoad = ref(true);

// Handle table options update (when user changes page or items per page via v-data-table)
const handleTableOptionsUpdate = (newOptions: any) => {
  // Update tableOptions with new values
  tableOptions.value = {
    ...tableOptions.value,
    ...newOptions,
  };
  
  // Skip initial load (handled in onMounted)
  if (isInitialLoad.value) {
    return;
  }
  
  // Fetch data items (will use updated tableOptions.value)
  fetchDataItems();
};

// Watch tableOptions.itemsPerPage for changes (when user changes items per page via custom select)
watch(
  () => tableOptions.value.itemsPerPage,
  async (newItemsPerPage, oldItemsPerPage) => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Only fetch if itemsPerPage actually changed
    if (newItemsPerPage !== oldItemsPerPage && oldItemsPerPage !== undefined) {
      // Wait for next tick to ensure tableOptions.value is fully updated
      await nextTick();
      // Reset to first page when items per page changes
      tableOptions.value.page = 1;
      // Wait again after page reset
      await nextTick();
      fetchDataItems();
    }
  }
);

// Watch tableOptions.page for changes (when user changes page via pagination)
watch(
  () => tableOptions.value.page,
  async (newPage, oldPage) => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Only fetch if page actually changed
    if (newPage !== oldPage && oldPage !== undefined) {
      // Wait for next tick to ensure tableOptions.value is fully updated
      await nextTick();
      fetchDataItems();
    }
  }
);

// Watch tableOptions.sortBy for changes (when user changes sorting)
watch(
  () => tableOptions.value.sortBy,
  async (newSortBy, oldSortBy) => {
    // Skip initial load (handled in onMounted)
    if (isInitialLoad.value) {
      return;
    }
    
    // Only fetch if sortBy actually changed
    if (JSON.stringify(newSortBy) !== JSON.stringify(oldSortBy) && oldSortBy !== undefined) {
      // Reset to first page when sorting changes
      if (tableOptions.value.page !== 1) {
        tableOptions.value.page = 1;
      }
      await nextTick();
      fetchDataItems();
    }
  },
  { deep: true }
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
    
    // Reset totalCount when filters change
    totalCount.value = 0;
    
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
    
    // Reset totalCount when search changes
    totalCount.value = 0;
    
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

// Keyboard shortcuts handler
const handleKeyboardShortcuts = (event: KeyboardEvent) => {
  // Only handle if form dialog is open
  if (!showFormDialog.value) return;
  
  // Prevent default for Enter key in form inputs
  if (event.key === 'Enter' && (event.target as HTMLElement)?.tagName === 'INPUT') {
    // Don't prevent default for text inputs (allow normal behavior)
    if ((event.target as HTMLInputElement)?.type === 'text' || 
        (event.target as HTMLInputElement)?.type === 'number') {
      return;
    }
  }
  
  // Enter key: Save form
  if (event.key === 'Enter' && !event.shiftKey && !event.ctrlKey && !event.altKey) {
    const target = event.target as HTMLElement;
    // Don't trigger if user is typing in an input/textarea
    if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) {
      return;
    }
    event.preventDefault();
    if (!formDialogLoading.value) {
      saveForm();
    }
  }
  
  // Esc key: Close dialog
  if (event.key === 'Escape') {
    if (!formDialogLoading.value) {
      closeFormDialog();
    }
  }
};

onMounted(async () => {
  await loadForm();
  // Add keyboard event listener
  window.addEventListener('keydown', handleKeyboardShortcuts);
});

onBeforeUnmount(() => {
  // Remove keyboard event listener
  window.removeEventListener('keydown', handleKeyboardShortcuts);
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
  formDialogSuccess.value = '';
  fieldErrors.value = {}; // Clear field errors
  showFormDialog.value = true;
};

// Open edit dialog
const editItem = async (item: any) => {
  if (!form.value || !dataset.value) return;
  
  formDialogMode.value = 'edit';
  formDialogError.value = '';
  formDialogSuccess.value = '';
  fieldErrors.value = {}; // Clear field errors
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
        let fieldValue = itemData[field.name];
        
        // Handle relation fields: convert expanded objects to __dataId
        if (field.fieldType === 'relation') {
          if (field.isArray && Array.isArray(fieldValue)) {
            // Array relation: convert each object to __dataId
            fieldValue = fieldValue.map((item: any) => {
              if (typeof item === 'object' && item !== null) {
                return item.__dataId || item.id || item._id || item;
              }
              return item;
            });
          } else if (!field.isArray && typeof fieldValue === 'object' && fieldValue !== null) {
            // Single relation: convert object to __dataId
            fieldValue = fieldValue.__dataId || fieldValue.id || fieldValue._id || fieldValue;
          }
        }
        
        // Handle person fields: convert expanded objects to user ID
        // Priority: id (matches userStore.users[].id) > userId > __dataId > _id
        if (field.fieldType === 'persons') {
          if (field.isArray && Array.isArray(fieldValue)) {
            // Array person: convert each object to user ID
            fieldValue = fieldValue.map((item: any) => {
              if (typeof item === 'object' && item !== null) {
                return item.id || item.userId || item.__dataId || item._id || item;
              }
              return item;
            });
          } else if (!field.isArray && typeof fieldValue === 'object' && fieldValue !== null) {
            // Single person: convert object to user ID
            fieldValue = fieldValue.id || fieldValue.userId || fieldValue.__dataId || fieldValue._id || fieldValue;
          }
        }
        
        // Handle personGroup fields: convert expanded objects to group ID
        // Priority: id (matches groupStore.groups[].id) > groupId > __dataId > _id
        if (field.fieldType === 'personGroups') {
          if (field.isArray && Array.isArray(fieldValue)) {
            // Array personGroup: convert each object to group ID
            fieldValue = fieldValue.map((item: any) => {
              if (typeof item === 'object' && item !== null) {
                return item.id || item.groupId || item.__dataId || item._id || item;
              }
              return item;
            });
          } else if (!field.isArray && typeof fieldValue === 'object' && fieldValue !== null) {
            // Single personGroup: convert object to group ID
            fieldValue = fieldValue.id || fieldValue.groupId || fieldValue.__dataId || fieldValue._id || fieldValue;
          }
        }
        
        data[field.name] = fieldValue;
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
    formDialogError.value = error.message || t('automated-forms.view.messages.itemLoadError');
    console.error('Error loading item for edit:', error);
  }
};

// Save form (create or update)
const saveForm = async () => {
  if (!form.value || !dataset.value || !formDialogRef.value) return;
  
  // Validate form
  const { valid } = await formDialogRef.value.validate();
  if (!valid) {
    // Show validation summary alert
    formDialogError.value = t('automated-forms.view.formDialog.validationSummary.message');
    return;
  }
  
  formDialogLoading.value = true;
  formDialogError.value = '';
  fieldErrors.value = {}; // Clear field errors
  
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
        throw new Error(t('automated-forms.view.messages.itemIdNotFound'));
      }
      
      const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}/${encodeURIComponent(currentEditingItemId.value)}`;
      await fetchFromDataGateway(url, 'PUT', dataToSend);
    }
    
    // Show success message and close dialog after delay
    formDialogSuccess.value = formDialogMode.value === 'create' 
      ? t('automated-forms.view.messages.createSuccess')
      : t('automated-forms.view.messages.updateSuccess');
    
    // Close dialog and refresh data after a short delay
    setTimeout(async () => {
      showFormDialog.value = false;
      formDialogSuccess.value = '';
      await fetchDataItems();
    }, 1500);
  } catch (error: any) {
    // Error logging removed - error parsing logic handles all cases
    
    // Clear previous field errors
    fieldErrors.value = {};
    
    // Parse error message from API response
    let errorMessage = '';
    const fieldErrorMap: Record<string, string[]> = {};
    
    // Helper function to extract field name from error message
    const extractFieldName = (message: string): string | null => {
        // Pattern: "field 'fieldName'" or "Field 'fieldName'" or "'fieldName'"
        const patterns = [
          /(?:field|Field)\s+['"]([^'"]+)['"]/i,
          /['"]([^'"]+)['"]\s+(?:field|Field)/i,
          /Incremental\s+field\s+['"]([^'"]+)['"]/i,
        ];
        
        for (const pattern of patterns) {
          const match = message.match(pattern);
          if (match && match[1]) {
            return match[1];
          }
        }
        return null;
      };
    
    // Try to parse validation errors from error.data
    // Priority: error.data.error.details.innerException > error.data.error.details.message > error.data.error.message > error.data.message > error.message > error.statusMessage
    
    // First, check if error.data itself is the error structure (from server route)
    // Backend response structure: { success: false, error: { code, message, details: { innerException, ... } } }
    // Nuxt server route wraps it: { url, statusCode, statusMessage, message, stack, data: { success: false, error: {...} } }
    let errorData = null;
    if (error?.data) {
      // Check if error.data has nested error structure: { success: false, error: {...} }
      if (error.data.error && typeof error.data.error === 'object') {
        errorData = error.data.error;
      } 
      // Check if error.data.data has the backend error structure (Nuxt server route wraps it)
      else if (error.data.data && error.data.data.error && typeof error.data.data.error === 'object') {
        errorData = error.data.data.error;
      }
      // Check if error.data itself is the error structure (nested)
      else if (error.data.success === false && error.data.error) {
        errorData = error.data.error;
      }
      // Check if error.data.data has success: false structure
      else if (error.data.data && error.data.data.success === false && error.data.data.error) {
        errorData = error.data.data.error;
      }
      // Check if error.data is a string (simple error message)
      else if (typeof error.data === 'string') {
        errorMessage = error.data;
      }
    }
    
    if (errorData) {
      
      // Check for validation error details (array format)
      if (errorData.details && Array.isArray(errorData.details) && errorData.details.length > 0) {
        // Separate dataset-level and field-level errors
        const datasetLevelErrors: string[] = [];
        
        // Group errors by field
        errorData.details.forEach((detail: any) => {
          const fieldName = detail.field || '';
          const message = detail.message || t('automated-forms.view.messages.validationError');
          
          // Handle dataset-level validation errors (_expression field)
          if (fieldName === '_expression') {
            // Dataset-level validation - collect for summary
            datasetLevelErrors.push(message);
            return; // Skip field mapping for dataset-level validations
          }
          
          // Get field label if field exists
          let fieldLabel = fieldName;
          if (fieldName && dataset.value?.fields) {
            const field = dataset.value.fields.find((f: any) => f.name === fieldName);
            if (field) {
              fieldLabel = getFieldLabel(fieldName, field, form.value, form.value?.datasetName);
            }
          }
          
          // Add to field error map
          if (fieldName) {
            if (!fieldErrorMap[fieldName]) {
              fieldErrorMap[fieldName] = [];
            }
            // Use field label in error message
            fieldErrorMap[fieldName].push(message.replace(`Field '${fieldName}'`, fieldLabel).replace(`'${fieldName}'`, fieldLabel));
          }
        });
        
        // Set field errors
        fieldErrors.value = fieldErrorMap;
        
        // Build summary message
        if (!errorMessage) {
          errorMessage = errorData.message || t('automated-forms.view.messages.validationError');
        }
        
        // Build detailed error message for summary
        const errorParts: string[] = [];
        
        // Add dataset-level validation errors
        if (datasetLevelErrors.length > 0) {
          errorParts.push(...datasetLevelErrors.map(msg => `• ${msg}`));
        }
        
        // Add field-level validation errors
        const fieldErrorsList = Object.entries(fieldErrorMap).map(([fieldName, messages]) => {
          const field = dataset.value?.fields?.find((f: any) => f.name === fieldName);
          const fieldLabel = field ? getFieldLabel(fieldName, field, form.value, form.value?.datasetName) : fieldName;
          return `• ${fieldLabel}: ${messages.join(', ')}`;
        });
        
        if (fieldErrorsList.length > 0) {
          errorParts.push(...fieldErrorsList);
        }
        
        if (errorParts.length > 0) {
          errorMessage = `${errorMessage}\n\n${errorParts.join('\n')}`;
        }
      } 
      // Check for error details (object format - e.g., CREATE_FAILED)
      else if (errorData.details && typeof errorData.details === 'object' && !Array.isArray(errorData.details)) {
        const detailsObj = errorData.details as any;
        // Priority: innerException > message > errorData.message
        const detailMessage = detailsObj.innerException || detailsObj.message || errorData.message || '';
        
        // Try to extract field name from error message
        const extractedFieldName = extractFieldName(detailMessage);
        
        if (extractedFieldName) {
          // Get field label if field exists
          let fieldLabel = extractedFieldName;
          if (dataset.value?.fields) {
            const field = dataset.value.fields.find((f: any) => f.name === extractedFieldName);
            if (field) {
              fieldLabel = getFieldLabel(extractedFieldName, field, form.value, form.value?.datasetName);
            }
          }
          
          // Add to field error map
          if (!fieldErrorMap[extractedFieldName]) {
            fieldErrorMap[extractedFieldName] = [];
          }
          // Replace field name with field label in message
          const formattedMessage = detailMessage
            .replace(`field '${extractedFieldName}'`, fieldLabel)
            .replace(`Field '${extractedFieldName}'`, fieldLabel)
            .replace(`'${extractedFieldName}'`, fieldLabel);
          fieldErrorMap[extractedFieldName].push(formattedMessage);
          
          // Set field errors
          fieldErrors.value = fieldErrorMap;
          
          // Build error message
          errorMessage = errorData.message || t('automated-forms.view.messages.validationError');
          const validationErrors = Object.entries(fieldErrorMap).map(([fieldName, messages]) => {
            const field = dataset.value?.fields?.find((f: any) => f.name === fieldName);
            const fieldLabel = field ? getFieldLabel(fieldName, field, form.value, form.value?.datasetName) : fieldName;
            return `• ${fieldLabel}: ${messages.join(', ')}`;
          }).join('\n');
          
          if (validationErrors) {
            errorMessage = `${errorMessage}\n\n${validationErrors}`;
          }
        } else {
          // No field name extracted, show as general error (use innerException if available)
          errorMessage = detailMessage || errorData.message || t('automated-forms.view.messages.validationError');
        }
      } 
      // Fallback to error message
      else if (errorData.message) {
        errorMessage = errorData.message;
      } else if (typeof errorData === 'string') {
        errorMessage = errorData;
      }
    } 
    // Check error.data.message (if error.data.error doesn't exist)
    else if (error?.data?.message) {
      errorMessage = error.data.message;
    }
    
    // If we still have generic messages or empty, try to extract from error.data structure
    // This handles cases where error.data.error exists but wasn't caught above
    if ((!errorMessage || 
         errorMessage === 'internal server error' || 
         errorMessage === 'Internal Server Error' ||
         errorMessage.toLowerCase() === 'internal server error') && 
        error?.data) {
      const data = error.data;
      // Try nested error structure (for CREATE_FAILED, etc.)
      // First check error.data.error
      if (data.error && typeof data.error === 'object' && data.error.details?.innerException) {
        const detailMessage = data.error.details.innerException;
        errorMessage = detailMessage;
        
        // Try to extract field name and map to field
        const extractedFieldName = extractFieldName(detailMessage);
        if (extractedFieldName && dataset.value?.fields) {
          const field = dataset.value.fields.find((f: any) => f.name === extractedFieldName);
          if (field) {
            const fieldLabel = getFieldLabel(extractedFieldName, field, form.value, form.value?.datasetName);
            if (!fieldErrorMap[extractedFieldName]) {
              fieldErrorMap[extractedFieldName] = [];
            }
            const formattedMessage = detailMessage
              .replace(`field '${extractedFieldName}'`, fieldLabel)
              .replace(`Field '${extractedFieldName}'`, fieldLabel)
              .replace(`'${extractedFieldName}'`, fieldLabel);
            fieldErrorMap[extractedFieldName].push(formattedMessage);
            fieldErrors.value = fieldErrorMap;
            
            // Build error message with field label
            errorMessage = data.error.message || t('automated-forms.view.messages.validationError');
            const validationErrors = Object.entries(fieldErrorMap).map(([fieldName, messages]) => {
              const f = dataset.value?.fields?.find((f: any) => f.name === fieldName);
              const fl = f ? getFieldLabel(fieldName, f, form.value, form.value?.datasetName) : fieldName;
              return `• ${fl}: ${messages.join(', ')}`;
            }).join('\n');
            if (validationErrors) {
              errorMessage = `${errorMessage}\n\n${validationErrors}`;
            }
          } else {
            errorMessage = detailMessage;
          }
        } else {
          errorMessage = detailMessage;
        }
      } 
      // Check error.data.data.error (Nuxt wrapped structure)
      else if (data.data && data.data.error && typeof data.data.error === 'object' && data.data.error.details?.innerException) {
        const detailMessage = data.data.error.details.innerException;
        errorMessage = detailMessage;
        
        // Try to extract field name and map to field
        const extractedFieldName = extractFieldName(detailMessage);
        if (extractedFieldName && dataset.value?.fields) {
          const field = dataset.value.fields.find((f: any) => f.name === extractedFieldName);
          if (field) {
            const fieldLabel = getFieldLabel(extractedFieldName, field, form.value, form.value?.datasetName);
            if (!fieldErrorMap[extractedFieldName]) {
              fieldErrorMap[extractedFieldName] = [];
            }
            const formattedMessage = detailMessage
              .replace(`field '${extractedFieldName}'`, fieldLabel)
              .replace(`Field '${extractedFieldName}'`, fieldLabel)
              .replace(`'${extractedFieldName}'`, fieldLabel);
            fieldErrorMap[extractedFieldName].push(formattedMessage);
            fieldErrors.value = fieldErrorMap;
            
            // Build error message with field label
            errorMessage = data.data.error.message || t('automated-forms.view.messages.validationError');
            const validationErrors = Object.entries(fieldErrorMap).map(([fieldName, messages]) => {
              const f = dataset.value?.fields?.find((f: any) => f.name === fieldName);
              const fl = f ? getFieldLabel(fieldName, f, form.value, form.value?.datasetName) : fieldName;
              return `• ${fl}: ${messages.join(', ')}`;
            }).join('\n');
            if (validationErrors) {
              errorMessage = `${errorMessage}\n\n${validationErrors}`;
            }
          } else {
            errorMessage = detailMessage;
          }
        } else {
          errorMessage = detailMessage;
        }
      }
      else if (data.error && typeof data.error === 'object' && data.error.details?.message) {
        errorMessage = data.error.details.message;
      } else if (data.data && data.data.error && typeof data.data.error === 'object' && data.data.error.details?.message) {
        errorMessage = data.data.error.details.message;
      } else if (data.error && typeof data.error === 'object' && data.error.message && 
                 data.error.message !== 'internal server error' && 
                 data.error.message !== 'Internal Server Error') {
        errorMessage = data.error.message;
      } else if (data.data && data.data.error && typeof data.data.error === 'object' && data.data.error.message && 
                 data.data.error.message !== 'internal server error' && 
                 data.data.error.message !== 'Internal Server Error') {
        errorMessage = data.data.error.message;
      } else if (typeof data === 'string') {
        errorMessage = data;
      }
    }
    
    // Check error.message (generic error message - should be last, and skip generic messages)
    if ((!errorMessage || 
         errorMessage === 'internal server error' || 
         errorMessage === 'Internal Server Error' ||
         errorMessage.toLowerCase() === 'internal server error') &&
        error?.message && 
        error.message !== 'internal server error' && 
        error.message !== 'Internal Server Error' &&
        error.message.toLowerCase() !== 'internal server error') {
      errorMessage = error.message;
    } 
    // Check error.statusMessage (should be last, and skip generic messages)
    else if ((!errorMessage || 
              errorMessage === 'internal server error' || 
              errorMessage === 'Internal Server Error' ||
              errorMessage.toLowerCase() === 'internal server error') &&
             error?.statusMessage && 
             error.statusMessage !== 'Internal Server Error' &&
             error.statusMessage.toLowerCase() !== 'internal server error') {
      errorMessage = error.statusMessage;
    }
    
    // Fallback to default message if still empty or generic
    if (!errorMessage || 
        errorMessage === 'internal server error' || 
        errorMessage === 'Internal Server Error' ||
        errorMessage.toLowerCase() === 'internal server error') {
      errorMessage = formDialogMode.value === 'create' 
        ? t('automated-forms.view.messages.createError') 
        : t('automated-forms.view.messages.updateError');
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
  formDialogSuccess.value = '';
  fieldErrors.value = {}; // Clear field errors
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
    deleteDialogError.value = error.message || t('automated-forms.view.messages.deleteError');
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

// Get responsive column span (for better mobile/tablet/desktop layout)
const getResponsiveColumnSpan = (fieldName: string) => {
  const span = getFieldColumnSpan(fieldName);
  // For better responsive behavior:
  // - On mobile (xs): always 12 (full width)
  // - On tablet (sm): use span if <= 6, otherwise 12
  // - On desktop (md+): use configured span
  return {
    xs: 12,
    sm: span <= 6 ? span : 12,
    md: span,
    lg: span,
  };
};

// Get field group from layout config
const getFieldGroup = (fieldName: string): string => {
  if (!form.value || !form.value.formConfig || !form.value.formConfig.fieldLayout) {
    return '';
  }
  
  const layout = form.value.formConfig.fieldLayout[fieldName];
  return layout?.group || '';
};

// Group form fields by their group
const groupedFormFields = computed(() => {
  if (!formFields.value || formFields.value.length === 0) {
    return [];
  }
  
  const groups: { [groupName: string]: any[] } = {};
  const ungrouped: any[] = [];
  
  formFields.value.forEach(field => {
    const groupName = getFieldGroup(field.name);
    if (groupName && groupName.trim()) {
      if (!groups[groupName]) {
        groups[groupName] = [];
      }
      groups[groupName].push(field);
    } else {
      ungrouped.push(field);
    }
  });
  
  // Convert to array format: [{ groupName: string, fields: any[] }]
  const result: Array<{ groupName: string; fields: any[] }> = [];
  
  // Add grouped fields
  Object.keys(groups).forEach(groupName => {
    result.push({
      groupName: groupName,
      fields: groups[groupName]
    });
  });
  
  // Add ungrouped fields as a special group (empty string means no group)
  if (ungrouped.length > 0) {
    result.push({
      groupName: '',
      fields: ungrouped
    });
  }
  
  return result;
});

// Format cell value
const formatCellValue = (value: any, fieldName: string) => {
  if (value === null || value === undefined) return '-';
  
  // Get field definition
  const field = dataset.value?.fields?.find(f => f.name === fieldName);
  const fieldType = field?.fieldType || 'text';
  
  // CRITICAL: Check if value is already a string "[object Object]" - this means it was incorrectly stringified
  if (typeof value === 'string' && value === '[object Object]') {
    // This shouldn't happen, but if it does, try to get the actual object
    // This is a fallback for edge cases
    return value;
  }
  
  // Check if there's a displayField configured for this column
  const columnConfig = form.value?.listConfig?.columns?.find(c => c.fieldName === fieldName);
  const displayField = columnConfig?.displayField;
  
  // CRITICAL: Check for arrays FIRST (before type-specific checks)
  // Arrays are handled separately in template with chips, but we still need to handle them here
  // for cases where they're not displayed as chips
  if (Array.isArray(value)) {
    // For array of objects, handle based on field type
    if (value.length > 0 && typeof value[0] === 'object') {
      if (fieldType === 'person' || fieldType === 'persons') {
        // For person arrays, show DisplayName (firstName + lastName combination)
        return value.map(v => {
          if (typeof v === 'object' && v !== null) {
            // Try DisplayName first
            if (v.DisplayName !== undefined && v.DisplayName !== null && v.DisplayName !== '') {
              return String(v.DisplayName);
            }
            if (v.displayName !== undefined && v.displayName !== null && v.displayName !== '') {
              return String(v.displayName);
            }
            // Build from firstName + lastName
            const firstName = v.firstName || v.FirstName || '';
            const lastName = v.lastName || v.LastName || '';
            if (firstName || lastName) {
              return `${firstName} ${lastName}`.trim();
            }
            // Fallback to username, email, or __dataId
            return v.username || v.userName || v.Username || v.email || v.Email || v.__dataId || JSON.stringify(v);
          }
          return String(v);
        }).join(', ');
      } else if (fieldType === 'personGroup' || fieldType === 'personGroups') {
        // For personGroup arrays, show group name
        return value.map(v => v.Name || v.name || v.groupName || v.__dataId || JSON.stringify(v)).join(', ');
      } else if (displayField) {
        // For other arrays with displayField
        return value.map(v => v[displayField] || JSON.stringify(v)).join(', ');
      }
    }
    return value.join(', ');
  }
  
  // Handle object/relation fields with displayField
  if ((fieldType === 'object' || fieldType === 'relation') && displayField && typeof value === 'object' && value !== null) {
    // If value is an object and displayField is specified, try to get that field
    if (value[displayField] !== undefined) {
      return String(value[displayField]);
    }
    // If displayField not found in object, fall back to JSON stringify
    return JSON.stringify(value);
  }
  
  if (fieldType === 'bool') {
    return value ? t('automated-forms.view.cellFormat.yes') : t('automated-forms.view.cellFormat.no');
  }
  
  if (fieldType === 'datetime') {
    // Check if there's custom formatting configured for this column
    const columnConfig = form.value?.listConfig?.columns?.find(c => c.fieldName === fieldName);
    if (columnConfig?.format && columnConfig.format.type === 'date') {
      // Use custom date formatting
      try {
        const date = new Date(value);
        if (isNaN(date.getTime())) return value;
        
        // Simple date formatting
        const day = String(date.getDate()).padStart(2, '0');
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const year = date.getFullYear();
        const monthNames = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'];
        const monthNamesShort = ['Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'];
        
        let formatted = columnConfig.format.dateFormat
          .replace('DD', day)
          .replace('MM', month)
          .replace('YYYY', String(year))
          .replace('MMM', monthNamesShort[date.getMonth()])
          .replace('MMMM', monthNames[date.getMonth()]);
        
        // Add time if showTime is enabled
        if (columnConfig.format.showTime) {
          const hours = String(date.getHours()).padStart(2, '0');
          const minutes = String(date.getMinutes()).padStart(2, '0');
          const seconds = String(date.getSeconds()).padStart(2, '0');
          
          const timeFormat = columnConfig.format.timeFormat || 'HH:mm';
          let timeString = timeFormat
            .replace('HH', hours)
            .replace('mm', minutes)
            .replace('ss', seconds);
          
          formatted = `${formatted} ${timeString}`;
        }
        
        return formatted;
      } catch (e) {
        // Fall back to default formatting if custom formatting fails
      }
    }
    
    // Default datetime formatting (if no custom format configured)
    try {
      const locale = localeStore.locale || 'tr';
      const localeMap: Record<string, string> = { tr: 'tr-TR', en: 'en-US', fr: 'fr-FR', ar: 'ar-SA', zh: 'zh-CN' };
      return new Date(value).toLocaleString(localeMap[locale] || 'tr-TR');
    } catch {
      return value;
    }
  }
  
  // CRITICAL: Check person and personGroup types BEFORE object type
  // Otherwise object type will catch them and return JSON.stringify
  // Support both singular (person, personGroup) and plural (persons, personGroups) field types
  if (fieldType === 'person' || fieldType === 'persons') {
    // For person fields, show DisplayName (firstName + lastName combination)
    // Check if value is an object (expanded person data)
    if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
      // PRIORITY 1: Try DisplayName first (if it exists as computed property)
      if (value.DisplayName !== undefined && value.DisplayName !== null && value.DisplayName !== '') {
        return String(value.DisplayName);
      }
      if (value.displayName !== undefined && value.displayName !== null && value.displayName !== '') {
        return String(value.displayName);
      }
      
      // PRIORITY 2: Build DisplayName from firstName + lastName
      const firstName = value.firstName || value.FirstName || '';
      const lastName = value.lastName || value.LastName || '';
      if (firstName || lastName) {
        return `${firstName} ${lastName}`.trim();
      }
      
      // PRIORITY 3: Fallback to username if available
      if (value.username !== undefined && value.username !== null && value.username !== '') {
        return String(value.username);
      }
      if (value.userName !== undefined && value.userName !== null && value.userName !== '') {
        return String(value.userName);
      }
      if (value.Username !== undefined && value.Username !== null && value.Username !== '') {
        return String(value.Username);
      }
      
      // PRIORITY 4: Fallback to email
      if (value.email !== undefined && value.email !== null && value.email !== '') {
        return String(value.email);
      }
      if (value.Email !== undefined && value.Email !== null && value.Email !== '') {
        return String(value.Email);
      }
      
      // Last fallback: __dataId
      if (value.__dataId) {
        return String(value.__dataId);
      }
      
      return JSON.stringify(value);
    }
    // If not an object or is array, return as string (arrays handled separately)
    if (Array.isArray(value)) {
      return value.join(', ');
    }
    // If value is a string, return as is (might be ID if expansion not done)
    if (typeof value === 'string') {
      return value;
    }
    // For other types, convert to string safely
    return String(value);
  }
  
  if (fieldType === 'personGroup' || fieldType === 'personGroups') {
    // For personGroup fields, show group name
    // Check if value is an object (expanded group data)
    if (typeof value === 'object' && value !== null && !Array.isArray(value)) {
      // Try Name first (case-sensitive, then case-insensitive)
      if (value.Name !== undefined && value.Name !== null) {
        return String(value.Name);
      }
      if (value.name !== undefined && value.name !== null) {
        return String(value.name);
      }
      // Fallback: try common field names
      const commonFields = ['GroupName', 'groupName', 'title', 'label'];
      for (const commonField of commonFields) {
        if (value[commonField] !== undefined && value[commonField] !== null) {
          return String(value[commonField]);
        }
      }
      // Last fallback: __dataId
      if (value.__dataId) {
        return String(value.__dataId);
      }
      // If still nothing found, return object keys for debugging
      const keys = Object.keys(value);
      if (keys.length > 0) {
        // Return first available property value
        return String(value[keys[0]]);
      }
      return JSON.stringify(value);
    }
    // If not an object or is array, return as string (arrays handled separately)
    if (Array.isArray(value)) {
      return value.join(', ');
    }
    // If value is a string, return as is (might be ID if expansion not done)
    if (typeof value === 'string') {
      return value;
    }
    // For other types, convert to string safely
    return String(value);
  }
  
  if (fieldType === 'object') {
    // If no displayField configured, show JSON
    return JSON.stringify(value);
  }
  
  if (fieldType === 'relation') {
    // PRIORITY 1: Use displayField from listConfig.columns (for list display)
    if (displayField && typeof value === 'object' && value !== null) {
      if (value[displayField] !== undefined) {
        return String(value[displayField]);
      }
    }
    
    // PRIORITY 2: Try to use displayField from formConfig if available
    const relationConfig = form.value?.formConfig?.relationFieldConfig?.[fieldName];
    if (relationConfig?.displayField && typeof value === 'object' && value !== null) {
      if (value[relationConfig.displayField] !== undefined) {
        return String(value[relationConfig.displayField]);
      }
    }
    
    // Fallback: try common field names (name, title, label)
    if (typeof value === 'object' && value !== null) {
      const commonFields = ['name', 'title', 'label', 'text'];
      for (const commonField of commonFields) {
        if (value[commonField] !== undefined) {
          return String(value[commonField]);
        }
      }
    }
    
    // Last fallback: try __dataId or show as object
    if (value?.__dataId) {
      return value.__dataId;
    }
    if (typeof value === 'object') {
      return JSON.stringify(value);
    }
  }
  
  // Convert to string first
  let stringValue = String(value);
  
  // Apply formatting if configured (after all type-specific formatting)
  // Note: columnConfig is already defined above, reuse it
  if (columnConfig?.format && columnConfig.format.type && columnConfig.format.type !== 'none') {
    stringValue = applyFormatting(stringValue, columnConfig.format);
  }
  
  return stringValue;
};

// Apply formatting to a value
const applyFormatting = (value: string, format: any): string => {
  if (!format || !format.type || format.type === 'none') {
    return value;
  }
  
  try {
    switch (format.type) {
      case 'regex':
        if (format.pattern && format.replacement !== undefined) {
          try {
            // Try to parse as regex pattern (e.g., "/pattern/flags" or just "pattern")
            let regex: RegExp;
            if (format.pattern.startsWith('/') && format.pattern.lastIndexOf('/') > 0) {
              // Full regex pattern: /pattern/flags
              const lastSlash = format.pattern.lastIndexOf('/');
              const pattern = format.pattern.substring(1, lastSlash);
              const flags = format.pattern.substring(lastSlash + 1);
              regex = new RegExp(pattern, flags);
            } else {
              // Simple pattern
              regex = new RegExp(format.pattern, 'g');
            }
            return value.replace(regex, format.replacement || '');
          } catch (e) {
            console.error('Invalid regex pattern:', format.pattern, e);
            return value;
          }
        }
        return value;
        
      case 'number':
        const numValue = parseFloat(value);
        if (isNaN(numValue)) return value;
        
        let formatted = numValue.toFixed(format.decimalPlaces ?? 2);
        
        if (format.thousandSeparator) {
          const parts = formatted.split('.');
          parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
          formatted = parts.join('.');
        }
        
        return formatted;
        
      case 'currency':
        const currencyValue = parseFloat(value);
        if (isNaN(currencyValue)) return value;
        
        let currencyFormatted = currencyValue.toFixed(format.decimalPlaces ?? 2);
        
        if (format.thousandSeparator) {
          const parts = currencyFormatted.split('.');
          parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, ',');
          currencyFormatted = parts.join('.');
        }
        
        const symbol = format.currencySymbol || '₺';
        return `${symbol} ${currencyFormatted}`;
        
      case 'date':
        if (!format.dateFormat) return value;
        
        try {
          const date = new Date(value);
          if (isNaN(date.getTime())) return value;
          
          // Simple date formatting (can be enhanced with a library like date-fns)
          const day = String(date.getDate()).padStart(2, '0');
          const month = String(date.getMonth() + 1).padStart(2, '0');
          const year = date.getFullYear();
          const monthNames = ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'];
          const monthNamesShort = ['Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'];
          
          let formatted = format.dateFormat
            .replace('DD', day)
            .replace('MM', month)
            .replace('YYYY', String(year))
            .replace('MMM', monthNamesShort[date.getMonth()])
            .replace('MMMM', monthNames[date.getMonth()]);
          
          // Add time if showTime is enabled
          if (format.showTime) {
            const hours = String(date.getHours()).padStart(2, '0');
            const minutes = String(date.getMinutes()).padStart(2, '0');
            const seconds = String(date.getSeconds()).padStart(2, '0');
            
            const timeFormat = format.timeFormat || 'HH:mm';
            let timeString = timeFormat
              .replace('HH', hours)
              .replace('mm', minutes)
              .replace('ss', seconds);
            
            formatted = `${formatted} ${timeString}`;
          }
          
          return formatted;
        } catch (e) {
          return value;
        }
        
      case 'text-transform':
        if (format.textTransform === 'uppercase') {
          return value.toUpperCase();
        } else if (format.textTransform === 'lowercase') {
          return value.toLowerCase();
        } else if (format.textTransform === 'capitalize') {
          return value.charAt(0).toUpperCase() + value.slice(1).toLowerCase();
        }
        return value;
        
      case 'color':
        // Simple color formatting - return value as is, color will be applied via style binding
        return value;
        
      case 'conditional-color':
        // Conditional color formatting - return value as is, color will be applied via style binding
        return value;
        
      default:
        return value;
    }
  } catch (error) {
    console.error('Error applying formatting:', error);
    return value;
  }
};

// Check if field is an array relation field
const isArrayRelationField = (fieldName: string): boolean => {
  const field = dataset.value?.fields?.find(f => f.name === fieldName);
  return field?.fieldType === 'relation' && field?.isArray === true;
};

// Check if field is an array person field
const isArrayPersonField = (fieldName: string): boolean => {
  const field = dataset.value?.fields?.find(f => f.name === fieldName);
  return (field?.fieldType === 'person' || field?.fieldType === 'persons') && field?.isArray === true;
};

// Check if field is an array personGroup field
const isArrayPersonGroupField = (fieldName: string): boolean => {
  const field = dataset.value?.fields?.find(f => f.name === fieldName);
  return (field?.fieldType === 'personGroup' || field?.fieldType === 'personGroups') && field?.isArray === true;
};

// Get array display style for a field
const getArrayDisplayStyle = (fieldName: string): string => {
  if (!form.value || !form.value.listConfig || !form.value.listConfig.columns) {
    return 'chip'; // Default if form not loaded
  }
  const columnConfig = form.value.listConfig.columns.find(c => c.fieldName === fieldName);
  if (!columnConfig) {
    return 'chip'; // Default if column config not found
  }
  return columnConfig.arrayDisplayStyle || 'chip';
};

// Get array separator for text-separator style
const getArraySeparator = (fieldName: string): string => {
  if (!form.value || !form.value.listConfig || !form.value.listConfig.columns) {
    return ' | '; // Default if form not loaded
  }
  const columnConfig = form.value.listConfig.columns.find(c => c.fieldName === fieldName);
  if (!columnConfig) {
    return ' | '; // Default if column config not found
  }
  return columnConfig.arraySeparator || ' | ';
};

// Get cell style based on color formatting
const getCellStyle = (value: any, fieldName: string, rowData?: any): Record<string, string> => {
  if (!form.value || !form.value.listConfig || !form.value.listConfig.columns) {
    return {};
  }
  
  const columnConfig = form.value.listConfig.columns.find(c => c.fieldName === fieldName);
  if (!columnConfig || !columnConfig.format) {
    return {};
  }
  
  const format = columnConfig.format;
  const style: Record<string, string> = {};
  
  // Simple color formatting
  if (format.type === 'color') {
    if (format.textColor) {
      if (format.textColor === 'custom' && format.customTextColor) {
        // Custom color from customTextColor field
        style.color = format.customTextColor;
      } else if (format.textColor.startsWith('#') || format.textColor.startsWith('rgb')) {
        // Direct color value (hex or rgb)
        style.color = format.textColor;
      } else {
        // Vuetify color name - try to use theme color
        const vuetifyColors: Record<string, string> = {
          primary: 'rgb(var(--v-theme-primary))',
          secondary: 'rgb(var(--v-theme-secondary))',
          success: 'rgb(var(--v-theme-success))',
          error: 'rgb(var(--v-theme-error))',
          warning: 'rgb(var(--v-theme-warning))',
          info: 'rgb(var(--v-theme-info))',
        };
        style.color = vuetifyColors[format.textColor] || format.textColor;
      }
    }
    
    if (format.backgroundColor) {
      if (format.backgroundColor === 'custom' && format.customBackgroundColor) {
        // Custom color from customBackgroundColor field
        style.backgroundColor = format.customBackgroundColor;
      } else if (format.backgroundColor.startsWith('#') || format.backgroundColor.startsWith('rgb')) {
        // Direct color value (hex or rgb)
        style.backgroundColor = format.backgroundColor;
      } else {
        // Vuetify color name - try to use theme color
        const vuetifyColors: Record<string, string> = {
          primary: 'rgb(var(--v-theme-primary))',
          secondary: 'rgb(var(--v-theme-secondary))',
          success: 'rgb(var(--v-theme-success))',
          error: 'rgb(var(--v-theme-error))',
          warning: 'rgb(var(--v-theme-warning))',
          info: 'rgb(var(--v-theme-info))',
        };
        style.backgroundColor = vuetifyColors[format.backgroundColor] || format.backgroundColor;
      }
    }
    
    return style;
  }
  
  // Conditional color formatting
  if (format.type === 'conditional-color' && format.conditions && format.conditions.length > 0) {
    // Check each condition
    for (const condition of format.conditions) {
      if (evaluateCondition(value, condition, fieldName, rowData)) {
        // Condition matches - apply colors
        if (condition.textColor) {
          if (condition.textColor === 'custom' && condition.customTextColor) {
            style.color = condition.customTextColor;
          } else if (condition.textColor.startsWith('#') || condition.textColor.startsWith('rgb')) {
            style.color = condition.textColor;
          } else {
            const vuetifyColors: Record<string, string> = {
              primary: 'rgb(var(--v-theme-primary))',
              secondary: 'rgb(var(--v-theme-secondary))',
              success: 'rgb(var(--v-theme-success))',
              error: 'rgb(var(--v-theme-error))',
              warning: 'rgb(var(--v-theme-warning))',
              info: 'rgb(var(--v-theme-info))',
            };
            style.color = vuetifyColors[condition.textColor] || condition.textColor;
          }
        }
        
        if (condition.backgroundColor) {
          if (condition.backgroundColor === 'custom' && condition.customBackgroundColor) {
            style.backgroundColor = condition.customBackgroundColor;
          } else if (condition.backgroundColor.startsWith('#') || condition.backgroundColor.startsWith('rgb')) {
            style.backgroundColor = condition.backgroundColor;
          } else {
            const vuetifyColors: Record<string, string> = {
              primary: 'rgb(var(--v-theme-primary))',
              secondary: 'rgb(var(--v-theme-secondary))',
              success: 'rgb(var(--v-theme-success))',
              error: 'rgb(var(--v-theme-error))',
              warning: 'rgb(var(--v-theme-warning))',
              info: 'rgb(var(--v-theme-info))',
            };
            style.backgroundColor = vuetifyColors[condition.backgroundColor] || condition.backgroundColor;
          }
        }
        
        return style; // Return first matching condition
      }
    }
    
    // No condition matched - use default colors
    if (format.defaultTextColor) {
      if (format.defaultTextColor === 'custom' && format.customDefaultTextColor) {
        style.color = format.customDefaultTextColor;
      } else if (format.defaultTextColor.startsWith('#') || format.defaultTextColor.startsWith('rgb')) {
        style.color = format.defaultTextColor;
      } else {
        const vuetifyColors: Record<string, string> = {
          primary: 'rgb(var(--v-theme-primary))',
          secondary: 'rgb(var(--v-theme-secondary))',
          success: 'rgb(var(--v-theme-success))',
          error: 'rgb(var(--v-theme-error))',
          warning: 'rgb(var(--v-theme-warning))',
          info: 'rgb(var(--v-theme-info))',
        };
        style.color = vuetifyColors[format.defaultTextColor] || format.defaultTextColor;
      }
    }
    
    if (format.defaultBackgroundColor) {
      if (format.defaultBackgroundColor === 'custom' && format.customDefaultBackgroundColor) {
        style.backgroundColor = format.customDefaultBackgroundColor;
      } else if (format.defaultBackgroundColor.startsWith('#') || format.defaultBackgroundColor.startsWith('rgb')) {
        style.backgroundColor = format.defaultBackgroundColor;
      } else {
        const vuetifyColors: Record<string, string> = {
          primary: 'rgb(var(--v-theme-primary))',
          secondary: 'rgb(var(--v-theme-secondary))',
          success: 'rgb(var(--v-theme-success))',
          error: 'rgb(var(--v-theme-error))',
          warning: 'rgb(var(--v-theme-warning))',
          info: 'rgb(var(--v-theme-info))',
        };
        style.backgroundColor = vuetifyColors[format.defaultBackgroundColor] || format.defaultBackgroundColor;
      }
    }
    
    return style;
  }
  
  return {};
};

// Evaluate condition for conditional color formatting
const evaluateCondition = (value: any, condition: any, currentFieldName: string, rowData?: any): boolean => {
  if (!condition || !condition.operator) {
    return false;
  }
  
  // Get the value to compare (from specified field or current field)
  let compareValue = value;
  if (condition.field && condition.field !== currentFieldName && rowData) {
    // Get value from another field in the same row
    compareValue = rowData[condition.field];
  }
  
  const conditionValue = condition.value;
  const operator = condition.operator;
  
  try {
    switch (operator) {
      case 'eq':
        return String(compareValue) === String(conditionValue);
      case 'ne':
        return String(compareValue) !== String(conditionValue);
      case 'gt':
        return Number(compareValue) > Number(conditionValue);
      case 'gte':
        return Number(compareValue) >= Number(conditionValue);
      case 'lt':
        return Number(compareValue) < Number(conditionValue);
      case 'lte':
        return Number(compareValue) <= Number(conditionValue);
      case 'contains':
        return String(compareValue).toLowerCase().includes(String(conditionValue).toLowerCase());
      case 'startsWith':
        return String(compareValue).toLowerCase().startsWith(String(conditionValue).toLowerCase());
      case 'endsWith':
        return String(compareValue).toLowerCase().endsWith(String(conditionValue).toLowerCase());
      case 'in':
        if (Array.isArray(conditionValue)) {
          return conditionValue.includes(compareValue);
        }
        return String(conditionValue).split(',').map(v => v.trim()).includes(String(compareValue));
      case 'notIn':
        if (Array.isArray(conditionValue)) {
          return !conditionValue.includes(compareValue);
        }
        return !String(conditionValue).split(',').map(v => v.trim()).includes(String(compareValue));
      default:
        return false;
    }
  } catch (error) {
    return false;
  }
};

// Get display value for array item (relation, person, personGroup field)
const getArrayItemDisplayValue = (arrayItem: any, fieldName: string): string => {
  if (!arrayItem || typeof arrayItem !== 'object') {
    return String(arrayItem || '-');
  }
  
  // Get field definition
  const field = dataset.value?.fields?.find(f => f.name === fieldName);
  const fieldType = field?.fieldType;
  
  // Handle person fields (support both singular and plural)
  if (fieldType === 'person' || fieldType === 'persons') {
    // PRIORITY 1: Try DisplayName first (if it exists as computed property)
    if (arrayItem.DisplayName !== undefined && arrayItem.DisplayName !== null && arrayItem.DisplayName !== '') {
      return String(arrayItem.DisplayName);
    }
    if (arrayItem.displayName !== undefined && arrayItem.displayName !== null && arrayItem.displayName !== '') {
      return String(arrayItem.displayName);
    }
    
    // PRIORITY 2: Build DisplayName from firstName + lastName
    const firstName = arrayItem.firstName || arrayItem.FirstName || '';
    const lastName = arrayItem.lastName || arrayItem.LastName || '';
    if (firstName || lastName) {
      return `${firstName} ${lastName}`.trim();
    }
    
    // PRIORITY 3: Fallback to username
    if (arrayItem.username !== undefined && arrayItem.username !== null && arrayItem.username !== '') {
      return String(arrayItem.username);
    }
    if (arrayItem.userName !== undefined && arrayItem.userName !== null && arrayItem.userName !== '') {
      return String(arrayItem.userName);
    }
    if (arrayItem.Username !== undefined && arrayItem.Username !== null && arrayItem.Username !== '') {
      return String(arrayItem.Username);
    }
    
    // PRIORITY 4: Fallback to email
    if (arrayItem.email !== undefined && arrayItem.email !== null && arrayItem.email !== '') {
      return String(arrayItem.email);
    }
    if (arrayItem.Email !== undefined && arrayItem.Email !== null && arrayItem.Email !== '') {
      return String(arrayItem.Email);
    }
    
    // Last fallback: __dataId
    if (arrayItem.__dataId) {
      return String(arrayItem.__dataId);
    }
  }
  
  // Handle personGroup fields (support both singular and plural)
  if (fieldType === 'personGroup' || fieldType === 'personGroups') {
    if (arrayItem.Name !== undefined) {
      return String(arrayItem.Name);
    }
    // Fallback: try common field names (case-insensitive)
    const commonFields = ['Name', 'name', 'GroupName', 'groupName', 'title', 'label'];
    for (const commonField of commonFields) {
      if (arrayItem[commonField] !== undefined) {
        return String(arrayItem[commonField]);
      }
    }
    // Last fallback: __dataId
    if (arrayItem.__dataId) {
      return String(arrayItem.__dataId);
    }
  }
  
  // Handle relation fields (existing logic)
  // Check if there's a displayField configured for this column
  const columnConfig = form.value?.listConfig?.columns?.find(c => c.fieldName === fieldName);
  const displayField = columnConfig?.displayField;
  
  // Try to use displayField from listConfig
  if (displayField && arrayItem[displayField] !== undefined) {
    return String(arrayItem[displayField]);
  }
  
  // Try to use displayField from formConfig relationFieldConfig
  const relationConfig = form.value?.formConfig?.relationFieldConfig?.[fieldName];
  if (relationConfig?.displayField && arrayItem[relationConfig.displayField] !== undefined) {
    return String(arrayItem[relationConfig.displayField]);
  }
  
  // Fallback: try common field names (name, title, label)
  const commonFields = ['name', 'title', 'label', 'text'];
  for (const commonField of commonFields) {
    if (arrayItem[commonField] !== undefined) {
      return String(arrayItem[commonField]);
    }
  }
  
  // Last fallback: use __dataId
  if (arrayItem.__dataId) {
    return String(arrayItem.__dataId);
  }
  
  // If nothing found, return a truncated JSON
  const jsonStr = JSON.stringify(arrayItem);
  return jsonStr.length > 30 ? jsonStr.substring(0, 30) + '...' : jsonStr;
};

// Export functions
const exportToJSON = async () => {
  if (!form.value || !dataset.value) return;
  
  try {
    loading.value = true;
    
    // Fetch all data (without pagination)
    const queryParams = new URLSearchParams();
    
    // Add filter if available
    const filterString = buildFilterString();
    if (filterString) {
      queryParams.append('filter', filterString);
    }
    
    // Add search if enabled and provided
    if (form.value.listConfig?.enableSearch && searchQuery.value && searchQuery.value.trim()) {
      queryParams.append('search', searchQuery.value.trim());
    }
    
    // Fetch all data (use a large limit or fetch without limit)
    queryParams.append('limit', '10000'); // Large limit to get all data
    
    const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}?${queryParams.toString()}`;
    const response = await fetchFromDataGateway(url, 'GET');
    
    let itemsArray: any[] = [];
    if (Array.isArray(response)) {
      itemsArray = response;
    } else {
      itemsArray = response.items || response.Items || [];
    }
    
    // Get visible columns (excluding actions)
    const visibleHeaders = tableHeaders.value.filter(h => h.key !== 'actions');
    
    // Create export data with field names as keys
    const exportData = itemsArray.map(item => {
      const exportItem: any = {};
      visibleHeaders.forEach(header => {
        exportItem[header.key] = item[header.key] ?? null;
      });
      return exportItem;
    });
    
    // Create JSON blob
    const jsonString = JSON.stringify(exportData, null, 2);
    const blob = new Blob([jsonString], { type: 'application/json' });
    const url_blob = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url_blob;
    link.download = `${form.value.formCode || 'export'}_${new Date().toISOString().split('T')[0]}.json`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url_blob);
  } catch (error: any) {
    console.error('JSON export error:', error);
    errorMessage.value = error.message || t('automated-forms.view.messages.exportError');
  } finally {
    loading.value = false;
  }
};

const exportToCSV = async () => {
  if (!form.value || !dataset.value) return;
  
  try {
    loading.value = true;
    
    // Fetch all data (without pagination)
    const queryParams = new URLSearchParams();
    
    // Add filter if available
    const filterString = buildFilterString();
    if (filterString) {
      queryParams.append('filter', filterString);
    }
    
    // Add search if enabled and provided
    if (form.value.listConfig?.enableSearch && searchQuery.value && searchQuery.value.trim()) {
      queryParams.append('search', searchQuery.value.trim());
    }
    
    // Fetch all data (use a large limit or fetch without limit)
    queryParams.append('limit', '10000'); // Large limit to get all data
    
    const url = `/api/v1/data/${encodeURIComponent(form.value.datasetName)}?${queryParams.toString()}`;
    const response = await fetchFromDataGateway(url, 'GET');
    
    let itemsArray: any[] = [];
    if (Array.isArray(response)) {
      itemsArray = response;
    } else {
      itemsArray = response.items || response.Items || [];
    }
    
    // Get visible columns (excluding actions)
    const visibleHeaders = tableHeaders.value.filter(h => h.key !== 'actions');
    
    // Helper function to escape CSV values
    const escapeCSV = (value: any): string => {
      if (value === null || value === undefined) {
        return '';
      }
      const stringValue = String(value);
      // If value contains comma, quote, or newline, wrap in quotes and escape quotes
      if (stringValue.includes(',') || stringValue.includes('"') || stringValue.includes('\n')) {
        return `"${stringValue.replace(/"/g, '""')}"`;
      }
      return stringValue;
    };
    
    // Create CSV header row (escape headers too)
    const headers = visibleHeaders.map(h => escapeCSV(h.title));
    const csvRows: string[] = [headers.join(',')];
    
    // Create CSV data rows
    itemsArray.forEach(item => {
      const row = visibleHeaders.map(header => {
        const value = item[header.key];
        return escapeCSV(value);
      });
      csvRows.push(row.join(','));
    });
    
    // Create CSV blob with UTF-8 BOM for Excel compatibility (Turkish characters)
    const csvString = csvRows.join('\n');
    // Add UTF-8 BOM for proper encoding in Excel
    const BOM = '\uFEFF';
    const csvWithBOM = BOM + csvString;
    const blob = new Blob([csvWithBOM], { type: 'text/csv;charset=utf-8;' });
    const url_blob = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url_blob;
    link.download = `${form.value.formCode || 'export'}_${new Date().toISOString().split('T')[0]}.csv`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url_blob);
  } catch (error: any) {
    console.error('CSV export error:', error);
    errorMessage.value = error.message || t('automated-forms.view.messages.exportError');
  } finally {
    loading.value = false;
  }
};

// Get form name with i18n support
const getFormName = (): string => {
  if (!form.value) return '';
  
  // Try i18n key first: automated-forms.forms.{formCode}.formName
  if (form.value.formCode) {
    const formNameKey = `automated-forms.forms.${form.value.formCode}.formName`;
    const translated = t(formNameKey);
    if (translated !== formNameKey) {
      return translated;
    }
  }
  
  // Fallback to form.formName
  return form.value.formName || '';
};

// Get form description with i18n support
const getFormDescription = (): string => {
  if (!form.value || !form.value.description) return '';
  
  // Try i18n key first: automated-forms.forms.{formCode}.description
  if (form.value.formCode) {
    const descriptionKey = `automated-forms.forms.${form.value.formCode}.description`;
    const translated = t(descriptionKey);
    if (translated !== descriptionKey) {
      return translated;
    }
  }
  
  // Fallback to form.description
  return form.value.description || '';
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
            {{ t('automated-forms.view.buttons.newRecord') }}
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
          
          <!-- Export Menu -->
          <v-menu v-if="form && dataset">
            <template v-slot:activator="{ props }">
              <v-btn
                icon
                variant="outlined"
                v-bind="props"
                :disabled="loading || dataItems.length === 0"
              >
                <DownloadIcon size="18" />
              </v-btn>
            </template>
            <v-list>
              <v-list-item @click="exportToJSON">
                <template v-slot:prepend>
                  <v-icon>mdi-code-json</v-icon>
                </template>
                <v-list-item-title>{{ t('automated-forms.view.buttons.exportJSON') }}</v-list-item-title>
              </v-list-item>
              <v-list-item @click="exportToCSV">
                <template v-slot:prepend>
                  <v-icon>mdi-file-excel</v-icon>
                </template>
                <v-list-item-title>{{ t('automated-forms.view.buttons.exportCSV') }}</v-list-item-title>
              </v-list-item>
            </v-list>
          </v-menu>
        </div>
        
        <div class="d-flex ga-3 align-center flex-wrap">
          <!-- Search (if enabled) -->
          <v-text-field
            v-if="form && dataset && form.listConfig?.enableSearch"
            v-model="searchQuery"
            prepend-inner-icon="mdi-magnify"
            :placeholder="t('automated-forms.view.search.placeholder')"
            variant="outlined"
            density="compact"
            clearable
            hide-details
            style="max-width: 300px; min-width: 200px;"
            class="flex-grow-0"
          ></v-text-field>
          
          <!-- Filter Button -->
          <v-btn
            v-if="form && dataset && filterableFields.length > 0"
            variant="outlined"
            :color="Object.keys(filters).some(key => filters[key] && filters[key].trim()) ? 'primary' : 'default'"
            @click="showFiltersPanel = showFiltersPanel.length > 0 ? [] : [0]"
          >
            <template #prepend>
              <v-icon>mdi-filter</v-icon>
            </template>
            {{ t('automated-forms.view.filters.title') }}
            <v-chip
              v-if="Object.keys(filters).some(key => filters[key] && filters[key].trim())"
              size="x-small"
              color="primary"
              variant="flat"
              class="ml-2"
            >
              {{ Object.keys(filters).filter(key => filters[key] && filters[key].trim()).length }}
            </v-chip>
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
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">{{ t('automated-forms.view.loading') }}</p>
      </div>
      
      <!-- Form Info -->
      <div v-if="form && dataset" class="mb-4">
        <v-alert type="info" variant="tonal" density="compact" class="mb-4">
          <div class="d-flex align-center ga-2">
            <FileCodeIcon size="18" />
            <div class="flex-grow-1">
              <strong>{{ getFormName() }}</strong>
              <span v-if="getFormDescription()" class="text-caption ml-2">- {{ getFormDescription() }}</span>
            </div>
          </div>
        </v-alert>
      </div>
      
      <!-- Filters Panel -->
      <v-expansion-panels v-if="form && dataset && filterableFields.length > 0" v-model="showFiltersPanel" variant="accordion" class="mb-4">
        <v-expansion-panel>
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
                  :label="getFieldLabel(field.name, field, form, form?.datasetName)"
                  :placeholder="`${getFieldLabel(field.name, field, form, form?.datasetName)} ${t('automated-forms.view.filters.filterPlaceholder')}`"
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
                  {{ t('automated-forms.view.filters.clear') }}
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
            <strong>{{ t('automated-forms.view.empty.title') }}</strong>
            <p class="text-caption mb-0 mt-1">
              {{ t('automated-forms.view.empty.description') }}
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
        :items-per-page="tableOptions.itemsPerPage"
        :items-per-page-options="[10, 20, 50, 100]"
        item-value="__dataId"
        class="border rounded-md"
        hide-default-footer
        @update:options="handleTableOptionsUpdate"
      >
        <!-- Dynamic Field Columns -->
        <template 
          v-for="header in tableHeaders.filter(h => h.key !== 'actions')" 
          :key="header.key"
          #[`item.${header.key}`]="{ value, item }"
        >
          <!-- Array Relation/Person/PersonGroup Fields with Different Display Styles -->
          <template v-if="(isArrayRelationField(header.key) || isArrayPersonField(header.key) || isArrayPersonGroupField(header.key)) && Array.isArray(value)">
            <!-- Empty Array -->
            <span 
              v-if="value.length === 0"
              class="text-body-2 text-medium-emphasis"
            >
              -
            </span>
            
            <!-- Chip Style (default) -->
            <div 
              v-else-if="getArrayDisplayStyle(header.key) === 'chip'"
              class="d-flex flex-wrap ga-1"
            >
              <v-chip
                v-for="(arrayItem, index) in value"
                :key="index"
                size="small"
                variant="tonal"
                color="primary"
                class="ma-0"
              >
                {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </v-chip>
            </div>
            
            <!-- Badge Style -->
            <div 
              v-else-if="getArrayDisplayStyle(header.key) === 'badge'"
              class="d-flex flex-wrap ga-1"
            >
              <v-chip
                v-for="(arrayItem, index) in value"
                :key="index"
                size="x-small"
                variant="flat"
                color="primary"
                class="ma-0"
              >
                {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </v-chip>
            </div>
            
            <!-- Pill Style -->
            <div 
              v-else-if="getArrayDisplayStyle(header.key) === 'pill'"
              class="d-flex flex-wrap ga-1"
            >
              <v-chip
                v-for="(arrayItem, index) in value"
                :key="index"
                size="small"
                variant="flat"
                color="primary"
                class="ma-0"
                rounded="pill"
              >
                {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </v-chip>
            </div>
            
            <!-- Outlined Style -->
            <div 
              v-else-if="getArrayDisplayStyle(header.key) === 'outlined'"
              class="d-flex flex-wrap ga-1"
            >
              <v-chip
                v-for="(arrayItem, index) in value"
                :key="index"
                size="small"
                variant="outlined"
                color="primary"
                class="ma-0"
              >
                {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </v-chip>
            </div>
            
            <!-- Tag Style -->
            <div 
              v-else-if="getArrayDisplayStyle(header.key) === 'tag'"
              class="d-flex flex-wrap ga-1"
            >
              <v-chip
                v-for="(arrayItem, index) in value"
                :key="index"
                size="small"
                variant="flat"
                color="secondary"
                class="ma-0"
              >
                {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </v-chip>
            </div>
            
            <!-- Text Separator Style -->
            <span 
              v-else-if="getArrayDisplayStyle(header.key) === 'text-separator'"
              class="text-body-2"
            >
              {{ value.map(item => getArrayItemDisplayValue(item, header.key)).join(getArraySeparator(header.key)) }}
            </span>
            
            <!-- Comma Separated Style -->
            <span 
              v-else-if="getArrayDisplayStyle(header.key) === 'comma-separated'"
              class="text-body-2"
            >
              {{ value.map(item => getArrayItemDisplayValue(item, header.key)).join(', ') }}
            </span>
            
            <!-- List Style (vertical) -->
            <div 
              v-else-if="getArrayDisplayStyle(header.key) === 'list'"
              class="d-flex flex-column ga-1"
            >
              <div
                v-for="(arrayItem, index) in value"
                :key="index"
                class="text-body-2"
              >
                • {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </div>
            </div>
            
            <!-- Fallback: Default chip style if style not recognized -->
            <div 
              v-else
              class="d-flex flex-wrap ga-1"
            >
              <v-chip
                v-for="(arrayItem, index) in value"
                :key="index"
                size="small"
                variant="tonal"
                color="primary"
                class="ma-0"
              >
                {{ getArrayItemDisplayValue(arrayItem, header.key) }}
              </v-chip>
            </div>
          </template>
          <!-- Regular Fields -->
          <span 
            v-else 
            class="text-body-2"
            :style="getCellStyle(value, header.key, item)"
          >
            {{ formatCellValue(value, header.key) }}
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
              :title="t('automated-forms.view.buttons.edit')"
            >
              <EditIcon size="18" />
            </v-btn>
            <v-btn
              icon
              size="small"
              variant="text"
              color="error"
              @click="deleteItem(item)"
              :title="t('automated-forms.view.buttons.delete')"
            >
              <TrashIcon size="18" />
            </v-btn>
          </div>
        </template>
      </v-data-table>
      
      <!-- Pagination -->
      <div v-if="form && dataset && totalCount > 0" class="d-flex align-center justify-space-between mt-4 flex-wrap ga-3">
        <div class="d-flex align-center ga-3">
          <div class="text-caption text-medium-emphasis">
            {{ t('automated-forms.view.table.pagination.total') }} {{ totalCount }} {{ t('automated-forms.view.table.pagination.records') }}
            <span v-if="totalPages > 1" class="ml-2">
              ({{ t('automated-forms.view.table.pagination.page') }} {{ currentPage }} / {{ totalPages }})
            </span>
          </div>
        </div>
        <div class="d-flex align-center ga-3">
          <span class="text-caption text-medium-emphasis">{{ t('automated-forms.view.table.pagination.itemsPerPage') }}</span>
          <v-select
            v-model="itemsPerPage"
            :items="[10, 20, 50, 100]"
            density="compact"
            variant="outlined"
            hide-details
            style="max-width: 80px;"
          ></v-select>
          <v-pagination
            v-model="currentPage"
            :length="totalPages"
            :total-visible="7"
            density="compact"
          ></v-pagination>
        </div>
      </div>
    </v-card-item>
  </v-card>
  
  <!-- Create/Edit Form Dialog -->
  <v-dialog 
    v-model="showFormDialog" 
    max-width="1200px" 
    persistent 
    scrollable
  >
    <v-card class="overflow-hidden d-flex flex-column" elevation="8" style="max-height: 90vh;">
      <v-card-title class="pa-5 bg-primary text-white d-flex align-center">
        <FileCodeIcon class="mr-3" size="22" />
        <span class="text-h6 font-weight-medium">{{ formDialogMode === 'edit' ? t('automated-forms.view.formDialog.title.edit') : t('automated-forms.view.formDialog.title.create') }}</span>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6 bg-grey-lighten-5 flex-grow-1" style="overflow-y: auto;">
        <!-- Form Success -->
        <v-alert
          v-if="formDialogSuccess"
          type="success"
          variant="tonal"
          density="compact"
          class="mb-4"
          :icon="false"
        >
          <div class="d-flex align-center">
            <v-icon color="success" size="24" class="mr-3">mdi-check-circle</v-icon>
            <div>
              <div class="font-weight-medium">{{ formDialogSuccess }}</div>
            </div>
          </div>
        </v-alert>
        
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
          <!-- Grouped Fields -->
          <template v-if="groupedFormFields.length > 0">
            <div
              v-for="(group, groupIndex) in groupedFormFields"
              :key="`group-${groupIndex}`"
              class="mb-8"
            >
              <!-- Group Header (if group has a name) -->
              <div v-if="group.groupName" class="mb-5">
                <v-card
                  variant="tonal"
                  color="primary"
                  class="mb-4"
                  elevation="0"
                >
                  <v-card-text class="pa-3">
                    <div class="d-flex align-center ga-3">
                      <v-icon color="primary" size="24">mdi-folder-outline</v-icon>
                      <h3 class="text-h6 text-primary font-weight-medium mb-0">{{ group.groupName }}</h3>
                    </div>
                  </v-card-text>
                </v-card>
              </div>
              
              <!-- Group Fields -->
              <v-row>
                <v-col
                  v-for="field in group.fields"
                  :key="field.name"
                  cols="12"
                  :md="getFieldColumnSpan(field.name)"
                  :lg="getFieldColumnSpan(field.name)"
                  class="mb-3"
                >
                  <DynamicFormField
                    :field="field"
                    v-model="formDialogData[field.name]"
                    :readonly="form.formConfig?.readonlyFields?.includes(field.name)"
                    :disabled="formDialogLoading"
                    :error-messages="fieldErrors[field.name] || []"
                    :relation-config="getRelationConfig(field.name)"
                    :form="form"
                    :dataset-name="form?.datasetName"
                  />
                </v-col>
              </v-row>
            </div>
          </template>
          
          <!-- Fallback: Ungrouped Fields (if no grouping) -->
          <v-row v-else>
            <v-col
              v-for="field in formFields"
              :key="field.name"
              cols="12"
              :md="getFieldColumnSpan(field.name)"
              :lg="getFieldColumnSpan(field.name)"
              class="mb-3"
            >
              <DynamicFormField
                :field="field"
                v-model="formDialogData[field.name]"
                :readonly="form.formConfig?.readonlyFields?.includes(field.name)"
                :disabled="formDialogLoading"
                :error-messages="fieldErrors[field.name] || []"
                :relation-config="getRelationConfig(field.name)"
                :form="form"
                :dataset-name="form?.datasetName"
              />
            </v-col>
          </v-row>
        </v-form>
      </v-card-text>
      
      <v-divider></v-divider>
      
      <v-card-actions class="pa-5 bg-grey-lighten-5 flex-shrink-0" style="position: sticky; bottom: 0; z-index: 10; border-top: 1px solid rgba(var(--v-border-color), 0.12); box-shadow: 0 -2px 8px rgba(0,0,0,0.1);">
        <div class="text-caption text-medium-emphasis d-flex align-center">
          <v-icon size="16" class="mr-1">mdi-information-outline</v-icon>
          <span>{{ t('automated-forms.view.formDialog.keyboardShortcuts') }}</span>
        </div>
        <v-spacer />
        <v-btn
          color="default"
          variant="outlined"
          size="large"
          @click="closeFormDialog"
          :disabled="formDialogLoading"
          class="mr-3"
          rounded="lg"
        >
          <XIcon class="mr-2" size="18" />
          {{ t('automated-forms.view.buttons.cancel') }}
          <v-chip size="x-small" variant="flat" color="default" class="ml-2">Esc</v-chip>
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          size="large"
          @click="saveForm"
          :loading="formDialogLoading"
          rounded="lg"
          elevation="2"
        >
          <CheckIcon class="mr-2" size="18" />
          {{ formDialogMode === 'edit' ? t('automated-forms.view.buttons.update') : t('automated-forms.view.buttons.save') }}
          <v-chip size="x-small" variant="flat" color="primary" class="ml-2">Enter</v-chip>
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
  
  <!-- Delete Confirmation Dialog -->
  <v-dialog v-model="showDeleteDialog" max-width="500px">
    <v-card>
      <v-card-title class="pa-4 bg-error text-white">
        <TrashIcon class="mr-2" size="20" />
        {{ t('automated-forms.view.deleteDialog.title') }}
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
          {{ t('automated-forms.view.deleteDialog.message') }}
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
          {{ deleteDialogError ? t('automated-forms.view.buttons.close') : t('automated-forms.view.buttons.cancel') }}
        </v-btn>
        <v-btn
          v-if="!deleteDialogError"
          color="error"
          variant="flat"
          @click="confirmDelete"
          :loading="deleteDialogLoading"
        >
          {{ t('automated-forms.view.deleteDialog.buttons.confirm') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
