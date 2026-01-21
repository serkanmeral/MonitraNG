<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick, shallowRef } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAutomatedFormsStore } from '@/stores/apps/automatedForms';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useAuthStore } from '@/stores/auth';
import { FileCodeIcon, CheckIcon, XIcon, LanguageIcon } from 'vue-tabler-icons';
import { fetchFromMngKeeper, fetchFromMngLLM } from '@/services/apiService';

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
const authStore = useAuthStore();

// Props to determine mode
const props = defineProps<{
  formCode?: string;
}>();

// Check if edit mode
const isEditMode = computed(() => {
  return !!props.formCode;
});

const page = computed(() => ({
  title: isEditMode.value ? t('automated-forms.form.title.edit') : t('automated-forms.form.title.create'),
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

// Tab state
const currentTab = ref('basic');

// Form data
const formData = ref({
  formName: '',
  formCode: '',
  description: '',
  datasetName: '',
  listConfig: {
    columns: [] as Array<{
      fieldName: string;
      visible: boolean;
      order: number;
      sortable: boolean;
      filterable: boolean;
      width?: number;
      displayField?: string; // For object/relation fields: which field to display in list
      arrayDisplayStyle?: 'chip' | 'badge' | 'pill' | 'outlined' | 'text-separator' | 'comma-separated' | 'list' | 'tag'; // For array fields: how to display array items
      arraySeparator?: string; // For text-separator style: separator character (default: ' | ')
      format?: {
        type?: 'none' | 'regex' | 'number' | 'date' | 'currency' | 'text-transform' | 'color' | 'conditional-color';
        pattern?: string; // For regex: pattern to match
        replacement?: string; // For regex: replacement string
        decimalPlaces?: number; // For number/currency: decimal places
        thousandSeparator?: boolean; // For number/currency: use thousand separator
        currencySymbol?: string; // For currency: currency symbol (e.g., '₺', '$', '€')
        dateFormat?: string; // For date: date format (e.g., 'DD/MM/YYYY', 'MM/DD/YYYY')
        showTime?: boolean; // For date: show time (HH:mm or HH:mm:ss)
        timeFormat?: 'HH:mm' | 'HH:mm:ss'; // For date: time format
        textTransform?: 'uppercase' | 'lowercase' | 'capitalize'; // For text-transform
        // For color formatting
        textColor?: string; // For color: text color (Vuetify color name or 'custom')
        customTextColor?: string; // For color: custom text color (hex or rgb) when textColor is 'custom'
        backgroundColor?: string; // For color: background color (Vuetify color name or 'custom')
        customBackgroundColor?: string; // For color: custom background color (hex or rgb) when backgroundColor is 'custom'
        // For conditional-color formatting
        conditions?: Array<{
          field?: string; // Field to check (default: current field)
          operator?: 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'startsWith' | 'endsWith' | 'in' | 'notIn';
          value?: any; // Value to compare against
          textColor?: string; // Text color when condition matches (Vuetify color name or 'custom')
          customTextColor?: string; // Custom text color when textColor is 'custom'
          backgroundColor?: string; // Background color when condition matches (Vuetify color name or 'custom')
          customBackgroundColor?: string; // Custom background color when backgroundColor is 'custom'
        }>;
        defaultTextColor?: string; // Default text color when no condition matches (Vuetify color name or 'custom')
        customDefaultTextColor?: string; // Custom default text color when defaultTextColor is 'custom'
        defaultBackgroundColor?: string; // Default background color when no condition matches (Vuetify color name or 'custom')
        customDefaultBackgroundColor?: string; // Custom default background color when defaultBackgroundColor is 'custom'
      };
    }>,
    defaultSortBy: '' as string | undefined,
    defaultSortOrder: 'asc' as 'asc' | 'desc',
    enableSearch: false,
  },
  formConfig: {
    visibleFields: [] as string[],
    readonlyFields: [] as string[],
    fieldOrder: [] as string[],
    relationFieldConfig: {} as { [fieldName: string]: { idField: string; displayField: string } },
    fieldLayout: {} as { [fieldName: string]: { columnSpan?: number; group?: string } },
  },
  isActive: true,
});

// Available datasets for dropdown
const availableDatasets = computed(() => datasetStore.datasets || []);

// Dataset options for dropdown
const datasetOptions = computed(() => {
  return availableDatasets.value.map(ds => ({
    title: ds.name,
    value: ds.name,
  }));
});

// Selected dataset fields (for listConfig and formConfig)
// CRITICAL: Guard against reactive updates during form loading
const selectedDataset = computed(() => {
  // CRITICAL: If form is loading, return null to prevent reactive loops
  // This prevents computed from reading datasetStore during loadForm
  if (isFormLoading) {
    return null;
  }
  
  if (!formData.value.datasetName) return null;
  // Check both currentDataset and datasets list
  if (datasetStore.currentDataset && datasetStore.currentDataset.name === formData.value.datasetName) {
    return datasetStore.currentDataset;
  }
  return datasetStore.getDatasetByName(formData.value.datasetName);
});

const availableFields = computed(() => {
  // CRITICAL: Use datasetStore directly instead of selectedDataset computed to avoid reactive loops
  if (!formData.value.datasetName) return [];
  const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
  if (!dataset || !dataset.fields) return [];
  return dataset.fields.map(f => ({
    title: f.title || f.name,
    value: f.name,
  }));
});

// List column configs (computed from dataset fields)
// IMPORTANT: This computed property only reads from formData.value.listConfig.columns
// It does NOT modify formData.value.listConfig.columns to avoid infinite loops
// CRITICAL: Guard against reactive updates during form loading
const listColumnConfigs = computed(() => {
  // CRITICAL: If form is loading, return empty array to prevent reactive loops
  // This prevents the computed from reading selectedDataset during loadForm
  if (isFormLoading) {
    return [];
  }
  
  // CRITICAL: Use datasetStore directly instead of selectedDataset computed to avoid reactive loops
  // This ensures we can read dataset even if selectedDataset computed returns null
  if (!formData.value.datasetName) return [];
  
  const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
  if (!dataset || !dataset.fields) return [];
  
  const fields = dataset.fields;
  const existingConfigs = formData.value.listConfig.columns || [];
  
  // Merge existing configs with new fields (read-only merge, no mutations)
  const merged = fields.map((field, index) => {
    const existingConfig = existingConfigs.find(c => c.fieldName === field.name);
    if (existingConfig) {
      // Preserve all properties including displayField, arrayDisplayStyle, arraySeparator, and format - use explicit property access
      return {
        fieldName: field.name, // Ensure fieldName is correct
        visible: existingConfig.visible !== undefined ? existingConfig.visible : true,
        order: existingConfig.order !== undefined ? existingConfig.order : index,
        sortable: existingConfig.sortable !== undefined ? existingConfig.sortable : true,
        filterable: existingConfig.filterable !== undefined ? existingConfig.filterable : true,
        width: existingConfig.width,
        displayField: existingConfig.displayField !== undefined && existingConfig.displayField !== null && existingConfig.displayField !== '' ? existingConfig.displayField : undefined, // Explicitly preserve displayField
        arrayDisplayStyle: existingConfig.arrayDisplayStyle || 'chip', // Preserve arrayDisplayStyle
        arraySeparator: existingConfig.arraySeparator || ' | ', // Preserve arraySeparator
        format: existingConfig.format || { type: 'none' }, // Preserve format
      };
    }
    // Default config for new field (only returned, not saved to formData)
    return {
      fieldName: field.name,
      visible: true,
      order: index,
      sortable: true,
      filterable: true,
      displayField: undefined, // No displayField for new fields
      arrayDisplayStyle: 'chip', // Default array display style
      arraySeparator: ' | ', // Default array separator
      format: { type: 'none' }, // Default format
    };
  }).sort((a, b) => a.order - b.order);
  
  return merged;
});

// Validation rules
const formNameRules = [
  (v: string) => !!v || t('automated-forms.form.validation.formNameRequired'),
  (v: string) => (v && v.length >= 3) || t('automated-forms.form.validation.formNameMinLength'),
  (v: string) => (v && v.length <= 100) || t('automated-forms.form.validation.formNameMaxLength'),
];

const formCodeRules = [
  (v: string) => !!v || t('automated-forms.form.validation.formCodeRequired'),
  (v: string) => (v && v.length >= 3) || t('automated-forms.form.validation.formCodeMinLength'),
  (v: string) => (v && v.length <= 50) || t('automated-forms.form.validation.formCodeMaxLength'),
  (v: string) => /^[a-zA-Z0-9_-]+$/.test(v) || t('automated-forms.form.validation.formCodeInvalid'),
];

const datasetNameRules = [
  (v: string) => !!v || t('automated-forms.form.validation.datasetRequired'),
];

// Form state
const formRef = ref();
const loading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

// Column settings modal state
const showColumnSettingsModal = ref(false);
const selectedColumnForSettings = ref<{
  fieldName: string;
  visible: boolean;
  sortable: boolean;
  filterable: boolean;
  order: number;
  displayField?: string;
  arrayDisplayStyle?: 'chip' | 'badge' | 'pill' | 'outlined' | 'text-separator' | 'comma-separated' | 'list' | 'tag';
  arraySeparator?: string;
  format?: {
    type?: 'none' | 'regex' | 'number' | 'date' | 'currency' | 'text-transform';
    pattern?: string;
    replacement?: string;
    decimalPlaces?: number;
    thousandSeparator?: boolean;
    currencySymbol?: string;
    dateFormat?: string;
    showTime?: boolean;
    timeFormat?: 'HH:mm' | 'HH:mm:ss';
    textTransform?: 'uppercase' | 'lowercase' | 'capitalize';
    // For color formatting
    textColor?: string;
    customTextColor?: string;
    backgroundColor?: string;
    customBackgroundColor?: string;
    // For conditional-color formatting
    conditions?: Array<{
      field?: string;
      operator?: 'eq' | 'ne' | 'gt' | 'gte' | 'lt' | 'lte' | 'contains' | 'startsWith' | 'endsWith' | 'in' | 'notIn';
      value?: any;
      textColor?: string;
      customTextColor?: string;
      backgroundColor?: string;
      customBackgroundColor?: string;
    }>;
    defaultTextColor?: string;
    customDefaultTextColor?: string;
    defaultBackgroundColor?: string;
    customDefaultBackgroundColor?: string;
  };
} | null>(null);

// Locale update state
const updatingLocales = ref(false);

// Format type options
const formatTypeOptions = [
  { title: t('automated-forms.form.listConfig.modal.formatting.types.none'), value: 'none' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.regex'), value: 'regex' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.number'), value: 'number' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.date'), value: 'date' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.currency'), value: 'currency' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.textTransform'), value: 'text-transform' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.color'), value: 'color' },
  { title: t('automated-forms.form.listConfig.modal.formatting.types.conditionalColor'), value: 'conditional-color' },
];

// Color options (Vuetify colors + custom)
const colorOptions = [
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.primary'), value: 'primary' },
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.secondary'), value: 'secondary' },
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.success'), value: 'success' },
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.error'), value: 'error' },
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.warning'), value: 'warning' },
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.info'), value: 'info' },
  { title: t('automated-forms.form.listConfig.modal.formatting.colors.custom'), value: 'custom' },
];

// Operator options for conditional formatting
const operatorOptions = [
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.eq'), value: 'eq' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.ne'), value: 'ne' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.gt'), value: 'gt' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.gte'), value: 'gte' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.lt'), value: 'lt' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.lte'), value: 'lte' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.contains'), value: 'contains' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.startsWith'), value: 'startsWith' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.endsWith'), value: 'endsWith' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.in'), value: 'in' },
  { title: t('automated-forms.form.listConfig.modal.formatting.operators.notIn'), value: 'notIn' },
];

// Date format options
const dateFormatOptions = [
  { title: 'DD/MM/YYYY', value: 'DD/MM/YYYY' },
  { title: 'MM/DD/YYYY', value: 'MM/DD/YYYY' },
  { title: 'YYYY-MM-DD', value: 'YYYY-MM-DD' },
  { title: 'DD.MM.YYYY', value: 'DD.MM.YYYY' },
  { title: 'DD-MM-YYYY', value: 'DD-MM-YYYY' },
  { title: 'DD MMM YYYY', value: 'DD MMM YYYY' },
  { title: 'DD MMMM YYYY', value: 'DD MMMM YYYY' },
];

// Time format options
const timeFormatOptions = [
  { title: t('automated-forms.form.listConfig.modal.formatting.timeFormats.hhmm'), value: 'HH:mm' },
  { title: t('automated-forms.form.listConfig.modal.formatting.timeFormats.hhmmss'), value: 'HH:mm:ss' },
];

// Text transform options
const textTransformOptions = [
  { title: t('automated-forms.form.listConfig.modal.formatting.textTransformTypes.uppercase'), value: 'uppercase' },
  { title: t('automated-forms.form.listConfig.modal.formatting.textTransformTypes.lowercase'), value: 'lowercase' },
  { title: t('automated-forms.form.listConfig.modal.formatting.textTransformTypes.capitalize'), value: 'capitalize' },
];

// Array display style options
const arrayDisplayStyleOptions = [
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.chip'), value: 'chip', icon: 'mdi-label' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.badge'), value: 'badge', icon: 'mdi-numeric' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.pill'), value: 'pill', icon: 'mdi-pill' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.outlined'), value: 'outlined', icon: 'mdi-label-outline' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.textSeparator'), value: 'text-separator', icon: 'mdi-format-text' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.commaSeparated'), value: 'comma-separated', icon: 'mdi-format-list-bulleted' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.list'), value: 'list', icon: 'mdi-format-list-numbered' },
  { title: t('automated-forms.form.listConfig.modal.arrayDisplay.styles.tag'), value: 'tag', icon: 'mdi-tag' },
];
const localeUpdateMessage = ref('');
const localeUpdateError = ref('');

// Watch dataset selection to update available fields and column configs
// IMPORTANT: Only run when dataset actually changes, not during form load
let isFormLoading = false;
// TEMPORARILY DISABLED: This watch causes infinite loops when loading relation datasets
// TODO: Re-enable with better guards or refactor to avoid recursion
// watch(() => formData.value.datasetName, async (newDatasetName, oldDatasetName) => {
//   if (!newDatasetName) {
//     return;
//   }
//   
//   // Skip if form is currently loading (to avoid recursion)
//   if (isFormLoading) {
//     return;
//   }
//   
//   // Skip if dataset hasn't actually changed (initial load or same dataset)
//   if (!oldDatasetName || newDatasetName === oldDatasetName) {
//     return; // Initial load or no change, skip watch
//   }
//   
//   // Reset configs when dataset changes
//   if (newDatasetName !== oldDatasetName) {
//     formData.value.listConfig.columns = [];
//     formData.value.formConfig.visibleFields = [];
//     formData.value.formConfig.readonlyFields = [];
//     formData.value.formConfig.fieldOrder = [];
//   }
//   
//   // Check if dataset is already loaded with fields
//   const existingDataset = datasetStore.getDatasetByName(newDatasetName);
//   if (existingDataset && existingDataset.fields && existingDataset.fields.length > 0) {
//     // Initialize column configs if not already set
//     if (!formData.value.listConfig.columns || formData.value.listConfig.columns.length === 0) {
//       initializeColumnConfigs();
//     }
//     return; // Dataset already loaded with fields
//   }
//   
//   // Load dataset with fields
//   try {
//     isFormLoading = true; // Set flag to prevent recursion
//     await loadDataset(newDatasetName);
//     
//     // Load related datasets for relation fields (for displayField options)
//     const dataset = datasetStore.getDatasetByName(newDatasetName);
//     if (dataset && dataset.fields) {
//       const relationFields = dataset.fields.filter(f => f.fieldType === 'relation' && f.relationDataset);
//       const relationDatasetNames = [...new Set(relationFields.map(f => f.relationDataset!))];
//       
//       // Load each related dataset (if not already loaded)
//       for (const relationDatasetName of relationDatasetNames) {
//         const existingRelatedDataset = datasetStore.getDatasetByName(relationDatasetName);
//         if (!existingRelatedDataset || !existingRelatedDataset.fields) {
//           try {
//             await datasetStore.fetchDatasetByName(relationDatasetName);
//           } catch (error) {
//             // Continue with other datasets
//           }
//         }
//       }
//     }
//     
//     // Initialize column configs after loading
//     if (!formData.value.listConfig.columns || formData.value.listConfig.columns.length === 0) {
//       initializeColumnConfigs();
//     }
//   } catch (error) {
//   } finally {
//     isFormLoading = false; // Clear flag
//   }
// });

// Initialize column configs from dataset fields
const initializeColumnConfigs = () => {
  if (!selectedDataset.value || !selectedDataset.value.fields) return;
  
  const fields = selectedDataset.value.fields;
  // Only initialize if columns array is empty
  if (!formData.value.listConfig.columns || formData.value.listConfig.columns.length === 0) {
    formData.value.listConfig.columns = fields.map((field, index) => ({
      fieldName: field.name,
      visible: true,
      order: index,
      sortable: true,
      filterable: true,
      displayField: undefined, // No displayField initially
    }));
  }
};

// Update column order (not used directly, but kept for future drag-drop functionality)
const updateColumnOrder = (newOrder: Array<{ fieldName: string; visible: boolean; order: number; sortable: boolean; filterable: boolean; width?: number }>) => {
  formData.value.listConfig.columns = newOrder.map((col, index) => ({
    ...col,
    order: index,
  }));
};

// Update displayField for a specific column
const updateColumnDisplayField = (fieldName: string, displayField: string | undefined) => {
  const columnIndex = formData.value.listConfig.columns.findIndex(c => c.fieldName === fieldName);
  if (columnIndex >= 0) {
    // Create a new object to ensure reactivity
    const updatedColumn = {
      ...formData.value.listConfig.columns[columnIndex],
      displayField: displayField || undefined,
    };
    // Replace the entire array to trigger reactivity
    formData.value.listConfig.columns = [
      ...formData.value.listConfig.columns.slice(0, columnIndex),
      updatedColumn,
      ...formData.value.listConfig.columns.slice(columnIndex + 1),
    ];
  } else {
    // Column not found, might be a new field - add it
    const field = selectedDataset.value?.fields?.find(f => f.name === fieldName);
    if (field) {
      const newColumn = {
        fieldName: fieldName,
        visible: true,
        order: formData.value.listConfig.columns.length,
        sortable: true,
        filterable: true,
        displayField: displayField || undefined,
      };
      formData.value.listConfig.columns = [...formData.value.listConfig.columns, newColumn];
    }
  }
};

// Update any column property
const updateColumnProperty = (fieldName: string, property: string, value: any) => {
  const columnIndex = formData.value.listConfig.columns.findIndex(c => c.fieldName === fieldName);
  if (columnIndex >= 0) {
    formData.value.listConfig.columns[columnIndex] = {
      ...formData.value.listConfig.columns[columnIndex],
      [property]: value,
    };
  }
};

// Open column settings modal
const openColumnSettings = (item: any) => {
  selectedColumnForSettings.value = {
    fieldName: item.fieldName,
    visible: item.visible,
    sortable: item.sortable,
    filterable: item.filterable,
    order: item.order,
    displayField: item.displayField,
    arrayDisplayStyle: item.arrayDisplayStyle || 'chip',
    arraySeparator: item.arraySeparator || ' | ',
    format: item.format || { 
      type: 'none',
      showTime: false,
      timeFormat: 'HH:mm',
      conditions: [],
    },
  };
  showColumnSettingsModal.value = true;
  
  // Load relation dataset if needed (for display field options)
  // CRITICAL: Use datasetStore directly instead of selectedDataset computed to avoid reactive loops
  if (formData.value.datasetName) {
    const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
    if (dataset && dataset.fields) {
      const field = dataset.fields.find(f => f.name === item.fieldName);
      if (field && field.fieldType === 'relation' && field.relationDataset) {
        loadRelationDatasetIfNeeded(item.fieldName);
      }
    }
  }
};

// Save column settings from modal
const saveColumnSettings = async () => {
  if (!selectedColumnForSettings.value) return;
  
  const fieldName = selectedColumnForSettings.value.fieldName;
  const columnIndex = formData.value.listConfig.columns.findIndex(c => c.fieldName === fieldName);
  
  if (columnIndex >= 0) {
    // Update existing column
    formData.value.listConfig.columns = [
      ...formData.value.listConfig.columns.slice(0, columnIndex),
      {
        ...formData.value.listConfig.columns[columnIndex],
        visible: selectedColumnForSettings.value.visible,
        sortable: selectedColumnForSettings.value.sortable,
        filterable: selectedColumnForSettings.value.filterable,
        order: selectedColumnForSettings.value.order,
        displayField: selectedColumnForSettings.value.displayField || undefined,
        arrayDisplayStyle: selectedColumnForSettings.value.arrayDisplayStyle || 'chip',
        arraySeparator: selectedColumnForSettings.value.arraySeparator || ' | ',
        format: selectedColumnForSettings.value.format && selectedColumnForSettings.value.format.type !== 'none' 
          ? selectedColumnForSettings.value.format 
          : undefined,
      },
      ...formData.value.listConfig.columns.slice(columnIndex + 1),
    ];
  } else {
    // Add new column
    formData.value.listConfig.columns.push({
      fieldName: fieldName,
      visible: selectedColumnForSettings.value.visible,
      sortable: selectedColumnForSettings.value.sortable,
      filterable: selectedColumnForSettings.value.filterable,
      order: selectedColumnForSettings.value.order,
      displayField: selectedColumnForSettings.value.displayField || undefined,
      arrayDisplayStyle: selectedColumnForSettings.value.arrayDisplayStyle || 'chip',
      arraySeparator: selectedColumnForSettings.value.arraySeparator || ' | ',
      format: selectedColumnForSettings.value.format && selectedColumnForSettings.value.format.type !== 'none' 
        ? selectedColumnForSettings.value.format 
        : undefined,
    });
  }
  
  // Close modal
  showColumnSettingsModal.value = false;
  selectedColumnForSettings.value = null;
  
  // Auto-save form if in edit mode
  if (isEditMode.value && props.formCode) {
    try {
      // Validate form first
      const { valid } = await formRef.value.validate();
      if (!valid) {
        errorMessage.value = t('automated-forms.form.messages.validationError');
        return;
      }
      
      loading.value = true;
      errorMessage.value = '';
      successMessage.value = '';
      
      const formDataToSubmit = {
        formName: formData.value.formName.trim(),
        formCode: formData.value.formCode.trim(),
        description: formData.value.description.trim() || undefined,
        datasetName: formData.value.datasetName,
        listConfig: {
          columns: formData.value.listConfig.columns || [],
          defaultSortBy: formData.value.listConfig.defaultSortBy || undefined,
          defaultSortOrder: formData.value.listConfig.defaultSortOrder || undefined,
          enableSearch: formData.value.listConfig.enableSearch ?? false,
        },
        formConfig: {
          visibleFields: formData.value.formConfig.visibleFields || [],
          readonlyFields: formData.value.formConfig.readonlyFields || [],
          fieldOrder: formData.value.formConfig.fieldOrder || [],
          relationFieldConfig: formData.value.formConfig.relationFieldConfig || {},
          fieldLayout: formData.value.formConfig.fieldLayout || {},
        },
        isActive: formData.value.isActive,
      };
      
      // Get form dataId
      const existingForm = await formStore.fetchFormByCode(props.formCode);
      if (!existingForm || (!existingForm.__dataId && !existingForm.dataId)) {
        throw new Error(t('automated-forms.form.messages.notFound'));
      }
      const dataId = existingForm.__dataId || existingForm.dataId;
      
      // Update form
      await formStore.updateForm(dataId, formDataToSubmit);
      successMessage.value = t('automated-forms.form.messages.updateSuccess');
      
      // Clear success message after 3 seconds
      setTimeout(() => {
        successMessage.value = '';
      }, 3000);
    } catch (error: any) {
      errorMessage.value = error.message || t('automated-forms.form.messages.saveError');
      
      // Handle duplicate form code error
      if (error.message && (error.message.includes('zaten mevcut') || error.message.includes('duplicate'))) {
        errorMessage.value = t('automated-forms.form.messages.duplicateCode');
      }
    } finally {
      loading.value = false;
    }
  }
};

// Add condition for conditional color formatting
const addCondition = () => {
  if (!selectedColumnForSettings.value || !selectedColumnForSettings.value.format) return;
  
  if (!selectedColumnForSettings.value.format.conditions) {
    selectedColumnForSettings.value.format.conditions = [];
  }
  
  selectedColumnForSettings.value.format.conditions.push({
    field: selectedColumnForSettings.value.fieldName, // Default to current field
    operator: 'eq',
    value: '',
    textColor: 'primary',
    customTextColor: undefined,
    backgroundColor: undefined,
    customBackgroundColor: undefined,
  });
};

// Close column settings modal
const closeColumnSettings = () => {
  showColumnSettingsModal.value = false;
  selectedColumnForSettings.value = null;
};

// Load dataset
const loadDataset = async (datasetName: string) => {
  try {
    await datasetStore.fetchDatasetByName(datasetName);
  } catch (error) {
    // Error handled by store
  }
};

// Load datasets list
const loadDatasets = async () => {
  try {
    if (availableDatasets.value.length === 0) {
      await datasetStore.fetchDatasets({ pageNumber: 1, pageSize: 1000 });
    }
  } catch (error) {
    // Error handled by store
  }
};

// Load form if edit mode
const loadForm = async () => {
  if (!isEditMode.value || !props.formCode) return;
  
  loading.value = true;
  isFormLoading = true; // Set flag to prevent watch recursion
  try {
    const form = await formStore.fetchFormByCode(props.formCode);
    if (form) {
      // Convert old format to new format if needed
      let listConfig = form.listConfig || {
        columns: [],
        defaultSortBy: undefined,
        defaultSortOrder: 'asc' as 'asc' | 'desc',
        enableSearch: false,
      };
      
      // If old format (defaultColumns), convert to new format
      if (listConfig && 'defaultColumns' in listConfig && !('columns' in listConfig)) {
        const oldConfig = listConfig as any;
        // We'll initialize columns after loading dataset
        listConfig = {
          columns: [],
          defaultSortBy: oldConfig.defaultSortBy,
          defaultSortOrder: oldConfig.defaultSortOrder || 'asc',
          enableSearch: oldConfig.enableSearch ?? false,
        };
      }
      
      formData.value = {
        formName: form.formName,
        formCode: form.formCode,
        description: form.description || '',
        datasetName: form.datasetName,
        listConfig: listConfig,
        formConfig: {
          visibleFields: form.formConfig?.visibleFields || [],
          readonlyFields: form.formConfig?.readonlyFields || [],
          fieldOrder: form.formConfig?.fieldOrder || [],
          relationFieldConfig: form.formConfig?.relationFieldConfig || {},
          fieldLayout: form.formConfig?.fieldLayout || {},
        },
        isActive: form.isActive,
      };
      
      
      // Load selected dataset
      // CRITICAL: Load dataset while isFormLoading is true to prevent reactive loops
      // selectedDataset computed property will return null during loading
      if (formData.value.datasetName) {
        // Load dataset (this will update Pinia store, but selectedDataset will return null due to isFormLoading guard)
        await loadDataset(formData.value.datasetName);
        
        // Wait for next tick to ensure store update is complete
        await nextTick();
        
        // CRITICAL: Don't modify formData.value.listConfig.columns here at all!
        // Backend'den gelen columns zaten doğru, computed property merge işlemini yapacak
        // Only initialize if columns are completely empty AND we're not in edit mode
        // (In edit mode, backend already provides the columns)
        if (!isEditMode.value && (!formData.value.listConfig.columns || formData.value.listConfig.columns.length === 0)) {
          // Initialize column configs (isFormLoading flag prevents watch recursion)
          // CRITICAL: initializeColumnConfigs uses selectedDataset, but we can't access it during loading
          // So we'll initialize after loading is complete (in finally block)
          // For now, skip initialization here
        }
        
        // Reset relation configs initialization flag for new dataset
        relationConfigsInitialized = false;
        
        // Initialize relation field configs if not exists (don't load relation datasets here to avoid recursion)
        // CRITICAL: Use datasetStore directly instead of selectedDataset computed to avoid reactive loops
        const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
        if (dataset && dataset.fields) {
          dataset.fields.forEach(field => {
            if (field.fieldType === 'relation' && field.relationDataset) {
              if (!formData.value.formConfig.relationFieldConfig[field.name]) {
                formData.value.formConfig.relationFieldConfig[field.name] = {
                  idField: '__dataId',
                  displayField: field.relationField || '__dataId',
                };
              }
            }
          });
        }
        
        // Mark as initialized after manual initialization
        relationConfigsInitialized = true;
      }
    } else {
      errorMessage.value = t('automated-forms.form.messages.notFound');
      setTimeout(() => {
        router.push('/apps/automated-forms');
      }, 2000);
    }
  } catch (error: any) {
    errorMessage.value = error.message || t('automated-forms.form.messages.loadError');
    setTimeout(() => {
      router.push('/apps/automated-forms');
    }, 2000);
  } finally {
    loading.value = false;
    isFormLoading = false; // Clear flag after form load
    
    // CRITICAL: Initialize column configs after loading is complete
    // This ensures selectedDataset computed can be accessed safely
    if (formData.value.datasetName && !isEditMode.value && (!formData.value.listConfig.columns || formData.value.listConfig.columns.length === 0)) {
      await nextTick();
      initializeColumnConfigs();
    }
  }
};

onMounted(async () => {
  // Load datasets first
  await loadDatasets();
  
  // Load form if edit mode
  if (isEditMode.value) {
    await loadForm();
  }
});

// Handle form submit
const handleSubmit = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  
  // Validate form
  const { valid } = await formRef.value.validate();
  if (!valid) {
    return;
  }
  
  loading.value = true;
  
  try {
    const formDataToSubmit = {
      formName: formData.value.formName.trim(),
      formCode: formData.value.formCode.trim(),
      description: formData.value.description.trim() || undefined,
      datasetName: formData.value.datasetName,
      listConfig: {
        columns: formData.value.listConfig.columns || [],
        defaultSortBy: formData.value.listConfig.defaultSortBy || undefined,
        defaultSortOrder: formData.value.listConfig.defaultSortOrder || undefined,
        enableSearch: formData.value.listConfig.enableSearch ?? false,
      },
      formConfig: {
        visibleFields: formData.value.formConfig.visibleFields || [],
        readonlyFields: formData.value.formConfig.readonlyFields || [],
        fieldOrder: formData.value.formConfig.fieldOrder || [],
        relationFieldConfig: formData.value.formConfig.relationFieldConfig || {},
        fieldLayout: formData.value.formConfig.fieldLayout || {},
      },
      isActive: formData.value.isActive,
    };
    
    if (isEditMode.value && props.formCode) {
      // Get form dataId first
      const existingForm = await formStore.fetchFormByCode(props.formCode);
      if (!existingForm || (!existingForm.__dataId && !existingForm.dataId)) {
        throw new Error(t('automated-forms.form.messages.notFound'));
      }
      const dataId = existingForm.__dataId || existingForm.dataId;
      
      // Update existing form
      await formStore.updateForm(dataId, formDataToSubmit);
      successMessage.value = t('automated-forms.form.messages.updateSuccess');
    } else {
      // Create new form
      await formStore.createForm(formDataToSubmit);
      successMessage.value = t('automated-forms.form.messages.createSuccess');
    }
    
    // Redirect to list after success
    setTimeout(() => {
      router.push('/apps/automated-forms');
    }, 1500);
  } catch (error: any) {
    errorMessage.value = error.message || t('automated-forms.form.messages.saveError');
    
    // Handle duplicate form code error
    if (error.message && (error.message.includes('zaten mevcut') || error.message.includes('duplicate'))) {
      errorMessage.value = t('automated-forms.form.messages.duplicateCode');
    }
  } finally {
    loading.value = false;
  }
};

// Handle cancel
const handleCancel = () => {
  router.push('/apps/automated-forms');
};

// Update locale files for field labels
const updateLocaleFiles = async () => {
  // Validate formCode
  if (!formData.value.formCode) {
    localeUpdateError.value = 'Form kodu (formCode) gereklidir. Lütfen önce form kodunu oluşturun.';
    setTimeout(() => {
      localeUpdateError.value = '';
    }, 5000);
    return;
  }

  // Validate dataset
  if (!formData.value.datasetName || !selectedDataset.value || !selectedDataset.value.fields) {
    localeUpdateError.value = 'Dataset seçilmeli ve yüklenmiş olmalıdır.';
    setTimeout(() => {
      localeUpdateError.value = '';
    }, 5000);
    return;
  }

  updatingLocales.value = true;
  localeUpdateMessage.value = '';
  localeUpdateError.value = '';

  try {
    // Available locales
    const locales = ['tr', 'en', 'fr', 'ar', 'zh'];
    
    // Get translations from MngLLM API (skip Turkish - it's the source)
    const targetLocales = locales.filter(locale => locale !== 'tr');
    
    // Helper function to check if text contains Turkish characters
    const hasTurkishCharacters = (text: string): boolean => {
      const turkishChars = /[ığüşöçİĞÜŞÖÇ]/i;
      return turkishChars.test(text);
    };
    
    // Translate form name and description
    let formNameTranslations: Record<string, string> = {};
    let formDescriptionTranslations: Record<string, string> = {};
    let turkishFormName = formData.value.formName;
    let turkishFormDescription = formData.value.description || '';
    
    // Translate form name
    const formName = formData.value.formName;
    if (formName) {
      // If form name doesn't contain Turkish characters, translate it to Turkish
      if (!hasTurkishCharacters(formName)) {
        try {
          const turkishResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
            text: formName,
            sourceLanguage: 'en',
            targetLanguages: ['tr'],
          });
          
          if (turkishResponse?.translations?.tr) {
            turkishFormName = turkishResponse.translations.tr;
          }
        } catch (turkishError: any) {
          // If translation fails, use original
          turkishFormName = formName;
        }
      }
      
      // Translate from Turkish to other languages
      if (targetLocales.length > 0) {
        try {
          const translationResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
            text: turkishFormName,
            sourceLanguage: 'tr',
            targetLanguages: targetLocales,
          });
          
          if (translationResponse?.translations) {
            formNameTranslations = translationResponse.translations;
          }
        } catch (translationError: any) {
          // Translation failed, use Turkish form name as fallback
          targetLocales.forEach(locale => {
            formNameTranslations[locale] = turkishFormName;
          });
        }
      }
    }
    
    // Translate form description (if exists)
    const formDescription = formData.value.description;
    if (formDescription) {
      // If description doesn't contain Turkish characters, translate it to Turkish
      if (!hasTurkishCharacters(formDescription)) {
        try {
          const turkishResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
            text: formDescription,
            sourceLanguage: 'en',
            targetLanguages: ['tr'],
          });
          
          if (turkishResponse?.translations?.tr) {
            turkishFormDescription = turkishResponse.translations.tr;
          }
        } catch (turkishError: any) {
          // If translation fails, use original
          turkishFormDescription = formDescription;
        }
      }
      
      // Translate from Turkish to other languages
      if (targetLocales.length > 0) {
        try {
          const translationResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
            text: turkishFormDescription,
            sourceLanguage: 'tr',
            targetLanguages: targetLocales,
          });
          
          if (translationResponse?.translations) {
            formDescriptionTranslations = translationResponse.translations;
          }
        } catch (translationError: any) {
          // Translation failed, use Turkish description as fallback
          targetLocales.forEach(locale => {
            formDescriptionTranslations[locale] = turkishFormDescription;
          });
        }
      }
    }
    
    // Get all fields from selected dataset
    const fields = selectedDataset.value.fields;
    
    // Translate all field titles
    const fieldTranslations: Record<string, Record<string, string>> = {};
    const turkishTranslations: Record<string, string> = {}; // For Turkish if field.title is not Turkish
    
    for (const field of fields) {
      const fieldTitle = field.title || field.name;
      if (!fieldTitle) continue;
      
      let translations: Record<string, string> = {};
      let turkishText = fieldTitle; // Default to original
      
      try {
        // Check if fieldTitle is already Turkish (contains Turkish characters)
        // If not, try to translate it to Turkish first
        if (!hasTurkishCharacters(fieldTitle)) {
          // Field title doesn't contain Turkish characters, might be English or another language
          // Try to translate from English to Turkish
          try {
            const turkishResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
              text: fieldTitle,
              sourceLanguage: 'en', // Assume English if no Turkish characters
              targetLanguages: ['tr'],
            });
            
            if (turkishResponse?.translations?.tr) {
              turkishText = turkishResponse.translations.tr;
            }
          } catch (turkishError: any) {
            // If translation fails, use original text
            turkishText = fieldTitle;
          }
        }
        
        // Store Turkish translation
        turkishTranslations[field.name] = turkishText;
        
        // Now translate from Turkish to other languages
        if (targetLocales.length > 0) {
          const translationResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
            text: turkishText,
            sourceLanguage: 'tr',
            targetLanguages: targetLocales,
          });
          
          if (translationResponse?.translations) {
            translations = translationResponse.translations;
          }
        }
      } catch (translationError: any) {
        // Translation API failed, using Turkish text as fallback
        targetLocales.forEach(locale => {
          translations[locale] = turkishText;
        });
      }
      
      fieldTranslations[field.name] = translations;
    }
    
    // Update each locale file
    for (const locale of locales) {
      try {
        // Load existing locale file
        let localeData: any = {};
        try {
          localeData = await fetchFromMngKeeper(`/system/locales/${locale}`, 'GET');
        } catch (error: any) {
          // 404 means file doesn't exist, which is OK - we'll create it
          if (error.message?.includes('404') || error.statusCode === 404) {
            localeData = {};
          } else {
            throw error;
          }
        }

        // Ensure automated-forms object exists
        if (!localeData['automated-forms']) {
          localeData['automated-forms'] = {};
        }

        // Ensure fields object exists
        if (!localeData['automated-forms'].fields) {
          localeData['automated-forms'].fields = {};
        }

        // Ensure forms object exists (for form name and description)
        if (!localeData['automated-forms'].forms) {
          localeData['automated-forms'].forms = {};
        }
        
        // Ensure formCode object exists in forms
        if (!localeData['automated-forms'].forms[formData.value.formCode]) {
          localeData['automated-forms'].forms[formData.value.formCode] = {};
        }
        
        // Update form name for this locale
        let formNameText: string;
        if (locale === 'tr') {
          // Turkish: Use Turkish translation (calculated above)
          formNameText = turkishFormName;
        } else if (formNameTranslations[locale]) {
          // Other languages: Use translated text
          formNameText = formNameTranslations[locale];
        } else {
          // Fallback to Turkish form name
          formNameText = turkishFormName;
        }
        localeData['automated-forms'].forms[formData.value.formCode].formName = formNameText;
        
        // Update form description for this locale (if exists)
        if (formData.value.description) {
          let formDescriptionText: string;
          if (locale === 'tr') {
            // Turkish: Use Turkish translation (calculated above)
            formDescriptionText = turkishFormDescription;
          } else if (formDescriptionTranslations[locale]) {
            // Other languages: Use translated text
            formDescriptionText = formDescriptionTranslations[locale];
          } else {
            // Fallback to Turkish description
            formDescriptionText = turkishFormDescription;
          }
          localeData['automated-forms'].forms[formData.value.formCode].description = formDescriptionText;
        }
        
        // Ensure fields object exists
        if (!localeData['automated-forms'].fields) {
          localeData['automated-forms'].fields = {};
        }
        
        // Ensure formCode object exists in fields
        if (!localeData['automated-forms'].fields[formData.value.formCode]) {
          localeData['automated-forms'].fields[formData.value.formCode] = {};
        }

        // Update field labels for this locale
        for (const field of fields) {
          const fieldTitle = field.title || field.name;
          if (!fieldTitle) continue;
          
          let translationText: string;
          if (locale === 'tr') {
            // Turkish: Use Turkish translation if available, otherwise use original fieldTitle
            translationText = turkishTranslations[field.name] || fieldTitle;
          } else if (fieldTranslations[field.name]?.[locale]) {
            // Other languages: Use translated text
            translationText = fieldTranslations[field.name][locale];
          } else {
            // Fallback: Use Turkish translation or original fieldTitle
            translationText = turkishTranslations[field.name] || fieldTitle;
          }
          
          localeData['automated-forms'].fields[formData.value.formCode][field.name] = translationText;
        }

        // Save locale file
        await fetchFromMngKeeper(`/system/locales/${locale}`, 'PUT', localeData);
      } catch (error: any) {
        localeUpdateError.value = `Dil dosyası güncellenirken hata oluştu: ${error.message || error}`;
        updatingLocales.value = false;
        setTimeout(() => {
          localeUpdateError.value = '';
        }, 10000);
        return;
      }
    }

    // Invalidate cache and reload locales from MinIO
    if (process.client) {
      try {
        const nuxtApp = useNuxtApp();
        const reloadLocales = (nuxtApp as any).$reloadLocales;
        if (reloadLocales) {
          await reloadLocales(); // Invalidate cache and reload from MinIO
        } else {
          // Fallback: just invalidate cache if reloadLocales is not available
          const invalidateCache = (nuxtApp as any).$invalidateLocaleCache;
          if (invalidateCache) {
            invalidateCache();
          }
        }
      } catch (cacheError) {
        // Failed to reload locales, silently continue
      }
    }

    localeUpdateMessage.value = `Dil dosyaları başarıyla güncellendi! (${formData.value.formCode})`;
    setTimeout(() => {
      localeUpdateMessage.value = '';
    }, 5000);
  } catch (error: any) {
    localeUpdateError.value = `Dil dosyaları güncellenirken hata oluştu: ${error.message || error}`;
    setTimeout(() => {
      localeUpdateError.value = '';
    }, 10000);
  } finally {
    updatingLocales.value = false;
  }
};

// Relation fields (fields with type 'relation')
const relationFields = computed(() => {
  if (!selectedDataset.value || !selectedDataset.value.fields) return [];
  return selectedDataset.value.fields.filter(f => f.fieldType === 'relation' && f.relationDataset);
});

// Get relation dataset fields for dropdown (returns computed ref for each relation field)
const getRelationDatasetFields = (datasetName: string | undefined) => {
  if (!datasetName) {
    return computed(() => [{ title: '__dataId', value: '__dataId' }]);
  }
  
  return computed(() => {
    const relationDataset = datasetStore.getDatasetByName(datasetName);
    if (relationDataset && relationDataset.fields && relationDataset.fields.length > 0) {
      return [
        { title: '__dataId (ID)', value: '__dataId' },
        ...relationDataset.fields.map(f => ({
          title: `${f.title || f.name} (${f.name})`,
          value: f.name,
        })),
      ];
    }
    
    // If not loaded, return empty (don't try to load here to avoid recursion)
    // Relation datasets should be loaded via loadAllRelationDatasets
    return [{ title: '__dataId (ID)', value: '__dataId' }];
  });
};

// Relation dataset fields cache (to load all relation datasets at once)
// Use shallowRef to prevent deep reactivity that causes infinite loops
const relationDatasetFieldsCache = shallowRef<Record<string, Array<{ title: string; value: string }>>>({});

// Track loaded relation datasets to prevent infinite loops
const loadedRelationDatasets = ref<Set<string>>(new Set());
let relationConfigsInitialized = false;

// loadAllRelationDatasets REMOVED - causes infinite loops
// Use loadRelationDatasetIfNeeded instead for lazy loading per field

// DISABLED: Watch relation fields and load datasets - causes infinite loops
// Instead, we load relation datasets manually in loadForm and when dataset changes
// watch(() => relationFields.value, async (newFields, oldFields) => {
//   // Only load if fields actually changed (not just reference)
//   if (newFields.length === 0) return;
//   
//   // Check if fields actually changed
//   const fieldsChanged = !oldFields || 
//     newFields.length !== oldFields.length ||
//     newFields.some((f, i) => !oldFields[i] || f.name !== oldFields[i].name || f.relationDataset !== oldFields[i].relationDataset);
//   
//   if (fieldsChanged) {
//     await loadAllRelationDatasets();
//   }
// }, { immediate: false });

// DISABLED: Initialize relation field configs when dataset changes - causes infinite loops
// Instead, we initialize manually in loadForm
// watch(() => [selectedDataset.value?.name, relationFields.value.length], ([newDatasetName, newFieldsLength], [oldDatasetName, oldFieldsLength]) => {
//   if (!selectedDataset.value || !relationFields.value.length) return;
//   
//   // Only initialize once per dataset change
//   if (newDatasetName !== oldDatasetName || (newFieldsLength > 0 && !relationConfigsInitialized)) {
//     relationConfigsInitialized = true;
//     
//     // Initialize relation field configs if not exists
//     relationFields.value.forEach(field => {
//       if (!formData.value.formConfig.relationFieldConfig[field.name]) {
//         formData.value.formConfig.relationFieldConfig[field.name] = {
//           idField: '__dataId',
//           displayField: field.relationField || '__dataId',
//         };
//       }
//     });
//   }
// }, { immediate: false });

// Field layout configs (computed from dataset fields)
const fieldLayoutConfigs = computed(() => {
  if (!selectedDataset.value || !selectedDataset.value.fields) return [];
  
  const fields = selectedDataset.value.fields;
  const existingLayouts = formData.value.formConfig.fieldLayout || {};
  
  return fields.map(field => {
    const existingLayout = existingLayouts[field.name];
    if (existingLayout) {
      return {
        fieldName: field.name,
        columnSpan: existingLayout.columnSpan || (field.fieldType === 'object' ? 12 : 6),
        group: existingLayout.group || '',
      };
    }
    // Default layout
    return {
      fieldName: field.name,
      columnSpan: field.fieldType === 'object' ? 12 : 6,
      group: '',
    };
  });
});

// Watch field layout configs changes and update formData
watch(
  () => fieldLayoutConfigs.value,
  (newConfigs) => {
    const layout: { [fieldName: string]: { columnSpan?: number; group?: string } } = {};
    newConfigs.forEach(config => {
      layout[config.fieldName] = {
        columnSpan: config.columnSpan,
        group: config.group || undefined,
      };
    });
    formData.value.formConfig.fieldLayout = layout;
  },
  { deep: true }
);

// Remove the watch on formData.listConfig.columns - it was causing infinite loops
// The computed property listColumnConfigs already reads from formData.value.listConfig.columns
// Changes made via updateColumnDisplayField and updateColumnProperty directly modify formData.value.listConfig.columns

// Load a single relation dataset if needed (lazy loading)
// CRITICAL: This function should NOT trigger reactive updates
const loadRelationDatasetIfNeeded = async (fieldName: string) => {
  if (isFormLoading) return; // Skip if form is loading
  
  // Get dataset directly from store without using selectedDataset computed
  const datasetName = formData.value.datasetName;
  if (!datasetName) return;
  
  const dataset = datasetStore.getDatasetByName(datasetName);
  if (!dataset || !dataset.fields) return;
  
  const field = dataset.fields.find(f => f.name === fieldName);
  if (!field || field.fieldType !== 'relation' || !field.relationDataset) return;
  
  const relationDatasetName = field.relationDataset;
  
  // Check if already loaded
  if (relationDatasetFieldsCache.value[relationDatasetName]) {
    return; // Already loaded
  }
  
  // Check if already loading
  if (loadedRelationDatasets.value.has(relationDatasetName)) {
    return; // Already loading
  }
  
  // Load this specific relation dataset
  // Use setTimeout to defer the update and prevent immediate reactivity loops
  try {
    loadedRelationDatasets.value.add(relationDatasetName);
    await datasetStore.fetchDatasetByName(relationDatasetName);
    
    // Defer cache update to next tick to prevent immediate reactivity
    await nextTick();
    
    const relatedDataset = datasetStore.getDatasetByName(relationDatasetName);
    if (relatedDataset && relatedDataset.fields) {
      // Create new object to avoid mutating reactive reference
      // Use setTimeout to defer update even more to break reactivity chain
      setTimeout(() => {
        const newCache = { ...relationDatasetFieldsCache.value };
        newCache[relationDatasetName] = [
          { title: '__dataId (ID)', value: '__dataId' },
          ...relatedDataset.fields
            .filter(f => f.fieldType !== 'object' && f.fieldType !== 'relation')
            .map(f => ({
              title: `${f.title || f.name} (${f.name})`,
              value: f.name,
            })),
        ];
        relationDatasetFieldsCache.value = newCache;
      }, 0);
    } else {
      setTimeout(() => {
        const newCache = { ...relationDatasetFieldsCache.value };
        newCache[relationDatasetName] = [{ title: '__dataId (ID)', value: '__dataId' }];
        relationDatasetFieldsCache.value = newCache;
      }, 0);
    }
  } catch (error) {
    setTimeout(() => {
      const newCache = { ...relationDatasetFieldsCache.value };
      newCache[relationDatasetName] = [{ title: '__dataId (ID)', value: '__dataId' }];
      relationDatasetFieldsCache.value = newCache;
    }, 0);
    loadedRelationDatasets.value.delete(relationDatasetName);
  }
};

// DISABLED: Clear loaded datasets when dataset changes - causes infinite loops
// TODO: Re-enable with better guards or refactor to avoid recursion
// watch(() => selectedDataset.value?.name, (newName, oldName) => {
//   // Skip if form is loading or if it's the initial load
//   if (isFormLoading || !oldName) {
//     return;
//   }
//   
//   if (newName !== oldName) {
//     loadedRelationDatasets.value.clear();
//     displayFieldOptionsCache.value.clear();
//     relationConfigsInitialized = false;
//   }
// });

// List column config table headers
const listColumnConfigHeaders = computed(() => [
  { title: t('automated-forms.form.listConfig.table.headers.fieldName'), key: 'fieldName', sortable: false, width: '200px' },
  { title: t('automated-forms.form.listConfig.table.headers.visible'), key: 'visible', sortable: false, width: '100px', align: 'center' },
  { title: t('automated-forms.form.listConfig.table.headers.sortable'), key: 'sortable', sortable: false, width: '120px', align: 'center' },
  { title: t('automated-forms.form.listConfig.table.headers.filterable'), key: 'filterable', sortable: false, width: '120px', align: 'center' },
  { title: t('automated-forms.form.listConfig.table.headers.order'), key: 'order', sortable: false, width: '80px', align: 'center' },
  { title: '', key: 'actions', sortable: false, width: '100px', align: 'center' },
]);

// Field layout table headers
const fieldLayoutHeaders = computed(() => [
  { title: t('automated-forms.form.formConfig.fieldLayout.table.headers.fieldName'), key: 'fieldName', sortable: false, width: '200px' },
  { title: t('automated-forms.form.formConfig.fieldLayout.table.headers.columnSpan'), key: 'columnSpan', sortable: false, width: '150px' },
  { title: t('automated-forms.form.formConfig.fieldLayout.table.headers.group'), key: 'group', sortable: false },
]);

// Check if field is array type
const isArrayField = (fieldName: string): boolean => {
  if (isFormLoading) {
    return false;
  }
  
  if (!formData.value.datasetName) return false;
  
  const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
  if (!dataset || !dataset.fields) return false;
  
  const field = dataset.fields.find(f => f.name === fieldName);
  return field?.isArray === true;
};

// Check if field is object or relation type
// CRITICAL: Guard against reactive updates during form loading
const isObjectOrRelationField = (fieldName: string): boolean => {
  // CRITICAL: If form is loading, return false to prevent reactive loops
  if (isFormLoading) {
    return false;
  }
  
  // CRITICAL: Use datasetStore directly instead of selectedDataset computed to avoid reactive loops
  if (!formData.value.datasetName) return false;
  
  const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
  if (!dataset || !dataset.fields) return false;
  
  const field = dataset.fields.find(f => f.name === fieldName);
  if (!field) return false;
  return field.fieldType === 'object' || field.fieldType === 'relation';
};

// Cache for display field options to avoid recreating arrays on every render
const displayFieldOptionsCache = ref<Map<string, Array<{ title: string; value: string }>>>(new Map());

// Get display field options for object/relation fields (for modal - safe to use)
const getDisplayFieldOptionsForModal = (fieldName: string): Array<{ title: string; value: string }> => {
  // CRITICAL: Use datasetStore directly instead of selectedDataset computed to avoid reactive loops
  if (!formData.value.datasetName) return [];
  
  const dataset = datasetStore.getDatasetByName(formData.value.datasetName);
  if (!dataset || !dataset.fields) return [];
  
  const field = dataset.fields.find(f => f.name === fieldName);
  if (!field) return [];
  
  // For relation fields, use relationDatasetFieldsCache (populated by loadRelationDatasetIfNeeded)
  if (field.fieldType === 'relation' && field.relationDataset) {
    // Check relationDatasetFieldsCache first
    const cached = relationDatasetFieldsCache.value[field.relationDataset];
    if (cached && cached.length > 0) {
      return [...cached];
    }
    
    // If not in cache, try to get from dataset store
    const relatedDataset = datasetStore.getDatasetByName(field.relationDataset);
    if (relatedDataset && relatedDataset.fields) {
      return [
        { title: '__dataId (ID)', value: '__dataId' },
        ...relatedDataset.fields
          .filter(f => f.fieldType !== 'object' && f.fieldType !== 'relation')
          .map(f => ({
            title: `${f.title || f.name} (${f.name})`,
            value: f.name,
          })),
      ];
    }
    
    // If not loaded, return default
    return [{ title: '__dataId (ID)', value: '__dataId' }];
  }
  
  // For object fields, return empty (user types manually)
  return [];
};

// DISABLED: Clear cache when dataset changes - causes infinite loops
// Cache will be cleared manually when needed
// watch(() => selectedDataset.value?.name, () => {
//   displayFieldOptionsCache.value.clear();
// });
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
      <FileCodeIcon class="mr-2" size="24" />
      <span>{{ page.title }}</span>
    </v-card-title>
    
    <v-divider></v-divider>
    
    <v-card-text class="pa-6">
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
      
      <!-- Success Message -->
      <v-alert
        v-if="successMessage"
        type="success"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        {{ successMessage }}
      </v-alert>
      
      <!-- Admin/Manager Warning -->
      <v-alert
        v-if="!authStore.isManager"
        type="warning"
        variant="tonal"
        density="compact"
        class="mb-4"
      >
        <strong>{{ t('automated-forms.form.warning').split(':')[0] }}:</strong> {{ t('automated-forms.form.warning').split(':').slice(1).join(':').trim() }}
      </v-alert>
      
      <!-- Loading State (only for edit mode when fetching data) -->
      <div v-if="loading && isEditMode" class="text-center py-12">
        <v-progress-circular
          indeterminate
          color="primary"
          size="64"
        ></v-progress-circular>
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">{{ t('automated-forms.form.messages.loading') }}</p>
      </div>
      
      <!-- Form (always visible in create mode, visible in edit mode after data is loaded) -->
      <v-form
        v-else
        ref="formRef"
        @submit.prevent="handleSubmit"
      >
        <!-- Tabs -->
        <v-tabs v-model="currentTab" bg-color="primary" class="mb-4">
          <v-tab value="basic">{{ t('automated-forms.form.tabs.basic') }}</v-tab>
          <v-tab value="list" :disabled="!formData.datasetName || isFormLoading">{{ t('automated-forms.form.tabs.list') }}</v-tab>
          <v-tab value="form" :disabled="!formData.datasetName || isFormLoading">{{ t('automated-forms.form.tabs.form') }}</v-tab>
        </v-tabs>
        
        <v-divider></v-divider>
        
        <v-card-text class="bg-grey100 mt-4 rounded-md">
          <v-window v-model="currentTab">
            <!-- Tab 1: Temel Bilgiler -->
            <v-window-item value="basic">
              <v-row>
                <v-col cols="12">
                  <h3 class="text-h6 mb-4">{{ t('automated-forms.form.tabs.basic') }}</h3>
                </v-col>
                
                <!-- Form Name -->
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="formData.formName"
                    :rules="formNameRules"
                    :label="t('automated-forms.form.fields.formName.label')"
                    :placeholder="t('automated-forms.form.fields.formName.placeholder')"
                    variant="outlined"
                    required
                    :disabled="loading || isEditMode"
                    :hint="t('automated-forms.form.fields.formName.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-text"
                  ></v-text-field>
                </v-col>
                
                <!-- Form Code -->
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="formData.formCode"
                    :rules="formCodeRules"
                    :label="t('automated-forms.form.fields.formCode.label')"
                    :placeholder="t('automated-forms.form.fields.formCode.placeholder')"
                    variant="outlined"
                    required
                    :disabled="loading || isEditMode"
                    :hint="t('automated-forms.form.fields.formCode.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-code-tags"
                  ></v-text-field>
                </v-col>
                
                <!-- Description -->
                <v-col cols="12">
                  <v-textarea
                    v-model="formData.description"
                    :label="t('automated-forms.form.fields.description.label')"
                    :placeholder="t('automated-forms.form.fields.description.placeholder')"
                    variant="outlined"
                    rows="3"
                    :disabled="loading"
                    prepend-inner-icon="mdi-text"
                    auto-grow
                  ></v-textarea>
                </v-col>
                
                <!-- Dataset Selection -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.datasetName"
                    :items="datasetOptions"
                    :rules="datasetNameRules"
                    :label="t('automated-forms.form.fields.datasetName.label')"
                    :placeholder="t('automated-forms.form.fields.datasetName.placeholder')"
                    variant="outlined"
                    required
                    :disabled="loading"
                    :hint="t('automated-forms.form.fields.datasetName.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-database"
                  ></v-select>
                </v-col>
                
                <!-- Is Active -->
                <v-col cols="12" md="6" class="d-flex align-center">
                  <v-switch
                    v-model="formData.isActive"
                    :label="t('automated-forms.form.fields.isActive.label')"
                    color="primary"
                    :disabled="loading"
                    hide-details
                  ></v-switch>
                </v-col>
              </v-row>
            </v-window-item>
            
            <!-- Tab 2: Liste Ayarları -->
            <v-window-item value="list">
              <v-row v-if="formData.datasetName && !isFormLoading">
                <v-col cols="12">
                  <h3 class="text-h6 mb-4">{{ t('automated-forms.form.listConfig.title') }}</h3>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    {{ t('automated-forms.form.listConfig.description') }}
                  </p>
                </v-col>
                
                <!-- Column Config Table -->
                <v-col cols="12">
                  <v-data-table
                    :headers="listColumnConfigHeaders"
                    :items="listColumnConfigs"
                    item-key="fieldName"
                    class="border rounded-md"
                    hide-default-footer
                    :items-per-page="-1"
                  >
                    <!-- Field Name Column -->
                    <template v-slot:item.fieldName="{ item }">
                      <div class="d-flex align-center ga-2">
                        <span class="font-weight-medium">{{ datasetStore.getDatasetByName(formData.datasetName)?.fields?.find(f => f.name === item.fieldName)?.title || item.fieldName }}</span>
                        <v-chip size="x-small" variant="tonal" color="primary">
                          {{ item.fieldName }}
                        </v-chip>
                      </div>
                    </template>
                    
                    <!-- Visible Column -->
                    <template v-slot:item.visible="{ item }">
                      <v-switch
                        :model-value="item.visible"
                        @update:model-value="(value) => updateColumnProperty(item.fieldName, 'visible', value)"
                        color="primary"
                        hide-details
                        density="compact"
                        :disabled="loading"
                      ></v-switch>
                    </template>
                    
                    <!-- Sortable Column -->
                    <template v-slot:item.sortable="{ item }">
                      <v-switch
                        :model-value="item.sortable"
                        @update:model-value="(value) => updateColumnProperty(item.fieldName, 'sortable', value)"
                        color="primary"
                        hide-details
                        density="compact"
                        :disabled="loading"
                      ></v-switch>
                    </template>
                    
                    <!-- Filterable Column -->
                    <template v-slot:item.filterable="{ item }">
                      <v-switch
                        :model-value="item.filterable"
                        @update:model-value="(value) => updateColumnProperty(item.fieldName, 'filterable', value)"
                        color="primary"
                        hide-details
                        density="compact"
                        :disabled="loading"
                      ></v-switch>
                    </template>
                    
                    <!-- Order Column -->
                    <template v-slot:item.order="{ item }">
                      <v-text-field
                        :model-value="item.order"
                        @update:model-value="(value) => updateColumnProperty(item.fieldName, 'order', Number(value))"
                        type="number"
                        variant="outlined"
                        density="compact"
                        hide-details
                        :disabled="loading"
                        style="max-width: 80px;"
                      ></v-text-field>
                    </template>
                    
                    <!-- Actions Column -->
                    <template v-slot:item.actions="{ item }">
                      <v-btn
                        icon="mdi-cog"
                        variant="text"
                        size="small"
                        color="primary"
                        @click="openColumnSettings(item)"
                        :disabled="loading"
                      >
                        <v-icon>mdi-cog</v-icon>
                        <v-tooltip activator="parent" location="top">
                          {{ t('automated-forms.form.listConfig.table.actions.settings') }}
                        </v-tooltip>
                      </v-btn>
                    </template>
                  </v-data-table>
                </v-col>
                
                <!-- Default Sort By -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.listConfig.defaultSortBy"
                    :items="availableFields"
                    :label="t('automated-forms.form.listConfig.defaultSortBy.label')"
                    :placeholder="t('automated-forms.form.listConfig.defaultSortBy.placeholder')"
                    variant="outlined"
                    :disabled="loading"
                    clearable
                    prepend-inner-icon="mdi-sort"
                  ></v-select>
                </v-col>
                
                <!-- Default Sort Order -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.listConfig.defaultSortOrder"
                    :items="[
                      { title: t('automated-forms.form.listConfig.defaultSortOrder.ascending'), value: 'asc' },
                      { title: t('automated-forms.form.listConfig.defaultSortOrder.descending'), value: 'desc' }
                    ]"
                    :label="t('automated-forms.form.listConfig.defaultSortOrder.label')"
                    variant="outlined"
                    :disabled="loading"
                    prepend-inner-icon="mdi-sort-variant"
                  ></v-select>
                </v-col>
                
                <!-- Enable Search -->
                <v-col cols="12" md="6" class="d-flex align-center">
                  <v-switch
                    v-model="formData.listConfig.enableSearch"
                    :label="t('automated-forms.form.listConfig.enableSearch.label')"
                    color="primary"
                    :disabled="loading"
                    :hint="t('automated-forms.form.listConfig.enableSearch.hint')"
                    persistent-hint
                  ></v-switch>
                </v-col>
              </v-row>
              <v-row v-else>
                <v-col cols="12" class="text-center py-12">
                  <p class="text-body-1 text-medium-emphasis">{{ t('automated-forms.form.listConfig.noDataset') }}</p>
                </v-col>
              </v-row>
            </v-window-item>
            
            <!-- Tab 3: Form Ayarları -->
            <v-window-item value="form">
              <v-row v-if="formData.datasetName && !isFormLoading">
                <v-col cols="12">
                  <h3 class="text-h6 mb-4">{{ t('automated-forms.form.formConfig.title') }}</h3>
                </v-col>
                
                <!-- Visible Fields -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.formConfig.visibleFields"
                    :items="availableFields"
                    :label="t('automated-forms.form.formConfig.visibleFields.label')"
                    :placeholder="t('automated-forms.form.formConfig.visibleFields.placeholder')"
                    variant="outlined"
                    multiple
                    chips
                    :disabled="loading"
                    :hint="t('automated-forms.form.formConfig.visibleFields.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-eye"
                  ></v-select>
                </v-col>
                
                <!-- Readonly Fields -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.formConfig.readonlyFields"
                    :items="availableFields"
                    :label="t('automated-forms.form.formConfig.readonlyFields.label')"
                    :placeholder="t('automated-forms.form.formConfig.readonlyFields.placeholder')"
                    variant="outlined"
                    multiple
                    chips
                    :disabled="loading"
                    :hint="t('automated-forms.form.formConfig.readonlyFields.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-lock"
                  ></v-select>
                </v-col>
                
                <!-- Relation Field Configurations -->
                <v-col cols="12">
                  <v-divider class="my-4"></v-divider>
                  <h4 class="text-subtitle-1 mb-4">{{ t('automated-forms.form.formConfig.relationFields.title') }}</h4>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    {{ t('automated-forms.form.formConfig.relationFields.description') }}
                  </p>
                  
                  <v-card
                    v-for="field in relationFields"
                    :key="field.name"
                    variant="outlined"
                    class="mb-4"
                  >
                    <v-card-title class="text-subtitle-2 pa-3">
                      {{ field.title || field.name }}
                      <v-chip size="x-small" variant="tonal" color="primary" class="ml-2">
                        {{ field.name }}
                      </v-chip>
                      <v-chip size="x-small" variant="tonal" color="info" class="ml-2">
                        {{ field.relationDataset }}
                      </v-chip>
                    </v-card-title>
                    <v-card-text class="pa-3">
                      <v-row>
                        <v-col cols="12" md="6">
                          <v-select
                            v-model="formData.formConfig.relationFieldConfig[field.name].displayField"
                            :items="relationDatasetFieldsCache[field.relationDataset] || [{ title: '__dataId (ID)', value: '__dataId' }]"
                            :label="t('automated-forms.form.formConfig.relationFields.displayField.label')"
                            :placeholder="t('automated-forms.form.formConfig.relationFields.displayField.placeholder')"
                            variant="outlined"
                            density="compact"
                            :disabled="loading"
                            :hint="t('automated-forms.form.formConfig.relationFields.displayField.hint')"
                            persistent-hint
                            required
                          ></v-select>
                        </v-col>
                        <v-col cols="12" md="6">
                          <v-select
                            v-model="formData.formConfig.relationFieldConfig[field.name].idField"
                            :items="relationDatasetFieldsCache[field.relationDataset] || [{ title: '__dataId (ID)', value: '__dataId' }]"
                            :label="t('automated-forms.form.formConfig.relationFields.idField.label')"
                            :placeholder="t('automated-forms.form.formConfig.relationFields.idField.placeholder')"
                            variant="outlined"
                            density="compact"
                            :disabled="loading"
                            :hint="t('automated-forms.form.formConfig.relationFields.idField.hint')"
                            persistent-hint
                          ></v-select>
                        </v-col>
                      </v-row>
                    </v-card-text>
                  </v-card>
                  
                  <v-alert
                    v-if="relationFields.length === 0"
                    type="info"
                    variant="tonal"
                    density="compact"
                  >
                    {{ t('automated-forms.form.formConfig.relationFields.noRelationFields') }}
                  </v-alert>
                </v-col>
                
                <!-- Field Layout Settings -->
                <v-col cols="12">
                  <v-divider class="my-4"></v-divider>
                  <h4 class="text-subtitle-1 mb-4">{{ t('automated-forms.form.formConfig.fieldLayout.title') }}</h4>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    {{ t('automated-forms.form.formConfig.fieldLayout.description') }}
                  </p>
                  
                  <v-data-table
                    :headers="fieldLayoutHeaders"
                    :items="fieldLayoutConfigs"
                    item-key="fieldName"
                    class="border rounded-md"
                    hide-default-footer
                    :items-per-page="-1"
                  >
                    <template v-slot:item.fieldName="{ item }">
                      <div class="d-flex align-center ga-2">
                        <span class="font-weight-medium">{{ selectedDataset?.fields?.find(f => f.name === item.fieldName)?.title || item.fieldName }}</span>
                        <v-chip size="x-small" variant="tonal" color="primary">
                          {{ item.fieldName }}
                        </v-chip>
                      </div>
                    </template>
                    
                    <template v-slot:item.columnSpan="{ item }">
                      <v-select
                        v-model="item.columnSpan"
                        :items="[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]"
                        variant="outlined"
                        density="compact"
                        hide-details
                        :disabled="loading"
                        style="max-width: 100px;"
                      ></v-select>
                    </template>
                    
                    <template v-slot:item.group="{ item }">
                      <v-text-field
                        v-model="item.group"
                        variant="outlined"
                        density="compact"
                        hide-details
                        :placeholder="t('automated-forms.form.formConfig.fieldLayout.groupPlaceholder')"
                        :disabled="loading"
                        clearable
                      ></v-text-field>
                    </template>
                  </v-data-table>
                </v-col>
              </v-row>
              <v-row v-else>
                <v-col cols="12" class="text-center py-12">
                  <p class="text-body-1 text-medium-emphasis">{{ t('automated-forms.form.formConfig.noDataset') }}</p>
                </v-col>
              </v-row>
            </v-window-item>
          </v-window>
        </v-card-text>
        
        <!-- Locale Update Section (only in edit mode) -->
        <v-row v-if="isEditMode && formData.formCode && selectedDataset" class="mt-4">
          <v-col cols="12">
            <v-divider class="mb-4"></v-divider>
            <h3 class="text-h6 mb-4">{{ t('automated-forms.form.locale.title') }}</h3>
            
            <!-- Success Message -->
            <v-alert
              v-if="localeUpdateMessage"
              type="success"
              variant="tonal"
              density="compact"
              class="mb-4"
              closable
              @click:close="localeUpdateMessage = ''"
            >
              {{ localeUpdateMessage }}
            </v-alert>
            
            <!-- Error Message -->
            <v-alert
              v-if="localeUpdateError"
              type="error"
              variant="tonal"
              density="compact"
              class="mb-4"
              closable
              @click:close="localeUpdateError = ''"
            >
              {{ localeUpdateError }}
            </v-alert>
            
            <p class="text-body-2 text-medium-emphasis mb-4">
              {{ t('automated-forms.form.locale.description', { formCode: formData.formCode }) }}
            </p>
            
            <v-btn
              color="info"
              variant="outlined"
              :loading="updatingLocales"
              :disabled="!formData.formCode || updatingLocales || loading || !selectedDataset"
              @click="updateLocaleFiles"
            >
              <template #prepend>
                <LanguageIcon size="18" />
              </template>
              {{ t('automated-forms.form.locale.buttons.updateLocales') }}
            </v-btn>
          </v-col>
        </v-row>

        <!-- Form Actions -->
        <v-row class="mt-4">
          <v-col cols="12" class="d-flex justify-end ga-3">
            <v-btn
              color="default"
              variant="outlined"
              @click="handleCancel"
              :disabled="loading"
            >
              <XIcon class="mr-2" size="18" />
              {{ t('automated-forms.form.buttons.cancel') }}
            </v-btn>
            
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
              :disabled="!formData.formName.trim() || !formData.formCode.trim() || !formData.datasetName"
            >
              <CheckIcon class="mr-2" size="18" />
              {{ isEditMode ? t('automated-forms.form.buttons.update') : t('automated-forms.form.buttons.create') }}
            </v-btn>
          </v-col>
        </v-row>
      </v-form>
    </v-card-text>
  </v-card>
  
  <!-- Column Settings Modal -->
  <v-dialog
    v-model="showColumnSettingsModal"
    max-width="600px"
    persistent
  >
    <v-card v-if="selectedColumnForSettings">
      <v-card-title class="d-flex align-center pa-4 bg-primary text-white">
        <v-icon class="mr-2" color="white">mdi-cog</v-icon>
        <span>{{ t('automated-forms.form.listConfig.modal.title') }}</span>
        <v-spacer></v-spacer>
        <v-btn
          icon="mdi-close"
          variant="text"
          @click="closeColumnSettings"
          color="white"
        ></v-btn>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6">
        <!-- Field Name (Read-only) -->
        <v-row>
          <v-col cols="12">
            <v-text-field
              :model-value="selectedDataset?.fields?.find(f => f.name === selectedColumnForSettings.fieldName)?.title || selectedColumnForSettings.fieldName"
              :label="t('automated-forms.form.listConfig.modal.fieldName')"
              variant="outlined"
              readonly
              prepend-inner-icon="mdi-tag"
            >
              <template v-slot:append>
                <v-chip size="small" variant="tonal" color="primary">
                  {{ selectedColumnForSettings.fieldName }}
                </v-chip>
              </template>
            </v-text-field>
          </v-col>
        </v-row>
        
        <!-- Visible -->
        <v-row>
          <v-col cols="12">
            <v-switch
              v-model="selectedColumnForSettings.visible"
              :label="t('automated-forms.form.listConfig.modal.visible')"
              color="primary"
              hide-details
            ></v-switch>
          </v-col>
        </v-row>
        
        <!-- Sortable -->
        <v-row>
          <v-col cols="12">
            <v-switch
              v-model="selectedColumnForSettings.sortable"
              :label="t('automated-forms.form.listConfig.modal.sortable')"
              color="primary"
              hide-details
            ></v-switch>
          </v-col>
        </v-row>
        
        <!-- Filterable -->
        <v-row>
          <v-col cols="12">
            <v-switch
              v-model="selectedColumnForSettings.filterable"
              :label="t('automated-forms.form.listConfig.modal.filterable')"
              color="primary"
              hide-details
            ></v-switch>
          </v-col>
        </v-row>
        
        <!-- Order -->
        <v-row>
          <v-col cols="12">
            <v-text-field
              v-model.number="selectedColumnForSettings.order"
              :label="t('automated-forms.form.listConfig.modal.order')"
              type="number"
              variant="outlined"
              prepend-inner-icon="mdi-sort-numeric"
              hide-details
            ></v-text-field>
          </v-col>
        </v-row>
        
        <!-- Display Field (only for object/relation fields) -->
        <v-row v-if="formData.datasetName && isObjectOrRelationField(selectedColumnForSettings.fieldName)">
          <v-col cols="12">
            <v-text-field
              v-if="datasetStore.getDatasetByName(formData.datasetName)?.fields?.find(f => f.name === selectedColumnForSettings.fieldName)?.fieldType === 'object'"
              v-model="selectedColumnForSettings.displayField"
              :label="t('automated-forms.form.listConfig.modal.displayField')"
              :placeholder="t('automated-forms.form.listConfig.modal.displayFieldPlaceholder')"
              variant="outlined"
              prepend-inner-icon="mdi-eye"
              hide-details
            >
              <template v-slot:append-inner>
                <v-tooltip activator="parent" location="top">
                  <span>{{ t('automated-forms.form.listConfig.modal.displayFieldObjectHint') }}</span>
                </v-tooltip>
                <v-icon size="small">mdi-information</v-icon>
              </template>
            </v-text-field>
            <v-select
              v-else
              v-model="selectedColumnForSettings.displayField"
              :items="getDisplayFieldOptionsForModal(selectedColumnForSettings.fieldName)"
              item-title="title"
              item-value="value"
              :label="t('automated-forms.form.listConfig.modal.displayField')"
              :placeholder="t('automated-forms.form.listConfig.modal.displayFieldPlaceholder')"
              variant="outlined"
              prepend-inner-icon="mdi-eye"
              clearable
              hide-details
              @update:menu="(value) => { if (value) loadRelationDatasetIfNeeded(selectedColumnForSettings.fieldName); }"
            >
              <template v-slot:append-inner>
                <v-tooltip activator="parent" location="top">
                  <span>{{ t('automated-forms.form.listConfig.modal.displayFieldHint') }}</span>
                </v-tooltip>
                <v-icon size="small">mdi-information</v-icon>
              </template>
            </v-select>
          </v-col>
        </v-row>
        
        <!-- Array Display Style (only for array fields) -->
        <v-row v-if="formData.datasetName && isArrayField(selectedColumnForSettings.fieldName)">
          <v-col cols="12">
            <v-divider class="my-4"></v-divider>
            <div class="text-subtitle-2 mb-2">{{ t('automated-forms.form.listConfig.modal.arrayDisplay.title') }}</div>
            
            <!-- Array Display Style -->
            <v-select
              v-model="selectedColumnForSettings.arrayDisplayStyle"
              :items="arrayDisplayStyleOptions"
              item-title="title"
              item-value="value"
              :label="t('automated-forms.form.listConfig.modal.arrayDisplay.style')"
              variant="outlined"
              prepend-inner-icon="mdi-view-list"
              hide-details
              class="mb-3"
            >
              <template v-slot:item="{ props: itemProps, item: itemData }">
                <v-list-item v-bind="itemProps">
                  <template v-slot:prepend>
                    <v-icon :icon="itemData.raw.icon" class="mr-2"></v-icon>
                  </template>
                </v-list-item>
              </template>
            </v-select>
            
            <!-- Array Separator (only for text-separator style) -->
            <v-text-field
              v-if="selectedColumnForSettings.arrayDisplayStyle === 'text-separator'"
              v-model="selectedColumnForSettings.arraySeparator"
              :label="t('automated-forms.form.listConfig.modal.arrayDisplay.separator')"
              :placeholder="t('automated-forms.form.listConfig.modal.arrayDisplay.separatorPlaceholder')"
              variant="outlined"
              prepend-inner-icon="mdi-minus"
              hide-details
            ></v-text-field>
          </v-col>
        </v-row>
        
        <!-- Formatting Section (only for text, number, datetime fields) -->
        <v-row v-if="formData.datasetName">
          <v-col cols="12">
            <v-divider class="my-4"></v-divider>
            <div class="text-subtitle-2 mb-2">{{ t('automated-forms.form.listConfig.modal.formatting.title') }}</div>
            
            <!-- Format Type -->
            <v-select
              v-model="selectedColumnForSettings.format.type"
              :items="formatTypeOptions"
              item-title="title"
              item-value="value"
              :label="t('automated-forms.form.listConfig.modal.formatting.type')"
              variant="outlined"
              prepend-inner-icon="mdi-format-text"
              hide-details
              class="mb-3"
            ></v-select>
            
            <!-- Regex Pattern -->
            <v-row v-if="selectedColumnForSettings.format.type === 'regex'">
              <v-col cols="12">
                <v-text-field
                  v-model="selectedColumnForSettings.format.pattern"
                  :label="t('automated-forms.form.listConfig.modal.formatting.regexPattern')"
                  :placeholder="t('automated-forms.form.listConfig.modal.formatting.regexPatternPlaceholder')"
                  variant="outlined"
                  prepend-inner-icon="mdi-regex"
                  hide-details
                  class="mb-2"
                ></v-text-field>
                <v-text-field
                  v-model="selectedColumnForSettings.format.replacement"
                  :label="t('automated-forms.form.listConfig.modal.formatting.regexReplacement')"
                  :placeholder="t('automated-forms.form.listConfig.modal.formatting.regexReplacementPlaceholder')"
                  variant="outlined"
                  prepend-inner-icon="mdi-arrow-right"
                  hide-details
                ></v-text-field>
              </v-col>
            </v-row>
            
            <!-- Number Format -->
            <v-row v-if="selectedColumnForSettings.format.type === 'number'">
              <v-col cols="6">
                <v-text-field
                  v-model.number="selectedColumnForSettings.format.decimalPlaces"
                  :label="t('automated-forms.form.listConfig.modal.formatting.decimalPlaces')"
                  type="number"
                  min="0"
                  max="10"
                  variant="outlined"
                  hide-details
                ></v-text-field>
              </v-col>
              <v-col cols="6">
                <v-switch
                  v-model="selectedColumnForSettings.format.thousandSeparator"
                  :label="t('automated-forms.form.listConfig.modal.formatting.thousandSeparator')"
                  color="primary"
                  hide-details
                ></v-switch>
              </v-col>
            </v-row>
            
            <!-- Currency Format -->
            <v-row v-if="selectedColumnForSettings.format.type === 'currency'">
              <v-col cols="6">
                <v-text-field
                  v-model="selectedColumnForSettings.format.currencySymbol"
                  :label="t('automated-forms.form.listConfig.modal.formatting.currencySymbol')"
                  :placeholder="t('automated-forms.form.listConfig.modal.formatting.currencySymbolPlaceholder')"
                  variant="outlined"
                  prepend-inner-icon="mdi-currency-usd"
                  hide-details
                  class="mb-2"
                ></v-text-field>
              </v-col>
              <v-col cols="6">
                <v-text-field
                  v-model.number="selectedColumnForSettings.format.decimalPlaces"
                  :label="t('automated-forms.form.listConfig.modal.formatting.decimalPlaces')"
                  type="number"
                  min="0"
                  max="10"
                  variant="outlined"
                  hide-details
                  class="mb-2"
                ></v-text-field>
              </v-col>
              <v-col cols="12">
                <v-switch
                  v-model="selectedColumnForSettings.format.thousandSeparator"
                  :label="t('automated-forms.form.listConfig.modal.formatting.thousandSeparator')"
                  color="primary"
                  hide-details
                ></v-switch>
              </v-col>
            </v-row>
            
            <!-- Date Format -->
            <v-row v-if="selectedColumnForSettings.format.type === 'date'">
              <v-col cols="12">
                <v-select
                  v-model="selectedColumnForSettings.format.dateFormat"
                  :items="dateFormatOptions"
                  item-title="title"
                  item-value="value"
                  :label="t('automated-forms.form.listConfig.modal.formatting.dateFormat')"
                  variant="outlined"
                  prepend-inner-icon="mdi-calendar"
                  hide-details
                  class="mb-3"
                ></v-select>
                <v-switch
                  v-model="selectedColumnForSettings.format.showTime"
                  :label="t('automated-forms.form.listConfig.modal.formatting.showTime')"
                  color="primary"
                  hide-details
                  class="mb-3"
                ></v-switch>
                <v-select
                  v-if="selectedColumnForSettings.format.showTime"
                  v-model="selectedColumnForSettings.format.timeFormat"
                  :items="timeFormatOptions"
                  item-title="title"
                  item-value="value"
                  :label="t('automated-forms.form.listConfig.modal.formatting.timeFormat')"
                  variant="outlined"
                  prepend-inner-icon="mdi-clock-outline"
                  hide-details
                ></v-select>
              </v-col>
            </v-row>
            
            <!-- Text Transform -->
            <v-row v-if="selectedColumnForSettings.format.type === 'text-transform'">
              <v-col cols="12">
                <v-select
                  v-model="selectedColumnForSettings.format.textTransform"
                  :items="textTransformOptions"
                  item-title="title"
                  item-value="value"
                  :label="t('automated-forms.form.listConfig.modal.formatting.textTransform')"
                  variant="outlined"
                  prepend-inner-icon="mdi-format-letter-case"
                  hide-details
                ></v-select>
              </v-col>
            </v-row>
            
            <!-- Color Format (Simple) -->
            <v-row v-if="selectedColumnForSettings.format.type === 'color'">
              <v-col cols="6">
                <v-select
                  v-model="selectedColumnForSettings.format.textColor"
                  :items="colorOptions"
                  item-title="title"
                  item-value="value"
                  :label="t('automated-forms.form.listConfig.modal.formatting.textColor')"
                  variant="outlined"
                  prepend-inner-icon="mdi-format-color-text"
                  hide-details
                  class="mb-3"
                ></v-select>
                <v-text-field
                  v-if="selectedColumnForSettings.format.textColor === 'custom'"
                  v-model="selectedColumnForSettings.format.customTextColor"
                  :label="t('automated-forms.form.listConfig.modal.formatting.customTextColor')"
                  :placeholder="t('automated-forms.form.listConfig.modal.formatting.customColorPlaceholder')"
                  variant="outlined"
                  prepend-inner-icon="mdi-palette"
                  hide-details
                >
                  <template v-slot:append>
                    <input
                      v-model="selectedColumnForSettings.format.customTextColor"
                      type="color"
                      style="width: 40px; height: 40px; border: none; cursor: pointer;"
                    />
                  </template>
                </v-text-field>
              </v-col>
              <v-col cols="6">
                <v-select
                  v-model="selectedColumnForSettings.format.backgroundColor"
                  :items="colorOptions"
                  item-title="title"
                  item-value="value"
                  :label="t('automated-forms.form.listConfig.modal.formatting.backgroundColor')"
                  variant="outlined"
                  prepend-inner-icon="mdi-format-color-fill"
                  hide-details
                  class="mb-3"
                ></v-select>
                <v-text-field
                  v-if="selectedColumnForSettings.format.backgroundColor === 'custom'"
                  v-model="selectedColumnForSettings.format.customBackgroundColor"
                  :label="t('automated-forms.form.listConfig.modal.formatting.customBackgroundColor')"
                  :placeholder="t('automated-forms.form.listConfig.modal.formatting.customColorPlaceholder')"
                  variant="outlined"
                  prepend-inner-icon="mdi-palette"
                  hide-details
                >
                  <template v-slot:append>
                    <input
                      v-model="selectedColumnForSettings.format.customBackgroundColor"
                      type="color"
                      style="width: 40px; height: 40px; border: none; cursor: pointer;"
                    />
                  </template>
                </v-text-field>
              </v-col>
            </v-row>
            
            <!-- Conditional Color Format -->
            <v-row v-if="selectedColumnForSettings.format.type === 'conditional-color'">
              <v-col cols="12">
                <div class="text-caption mb-2">{{ t('automated-forms.form.listConfig.modal.formatting.conditions.title') }}</div>
                
                <!-- Conditions List -->
                <div v-for="(condition, index) in (selectedColumnForSettings.format.conditions || [])" :key="index" class="mb-3 pa-3 border rounded">
                  <v-row>
                    <v-col cols="12" md="4">
                      <v-select
                        v-model="condition.field"
                        :items="availableFields"
                        item-title="title"
                        item-value="value"
                        :label="t('automated-forms.form.listConfig.modal.formatting.conditions.field')"
                        variant="outlined"
                        density="compact"
                        hide-details
                      ></v-select>
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-select
                        v-model="condition.operator"
                        :items="operatorOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('automated-forms.form.listConfig.modal.formatting.conditions.operator')"
                        variant="outlined"
                        density="compact"
                        hide-details
                      ></v-select>
                    </v-col>
                    <v-col cols="12" md="3">
                      <v-text-field
                        v-model="condition.value"
                        :label="t('automated-forms.form.listConfig.modal.formatting.conditions.value')"
                        variant="outlined"
                        density="compact"
                        hide-details
                      ></v-text-field>
                    </v-col>
                    <v-col cols="12" md="2" class="d-flex align-center">
                      <v-btn
                        icon="mdi-delete"
                        variant="text"
                        size="small"
                        color="error"
                        @click="selectedColumnForSettings.format.conditions?.splice(index, 1)"
                      ></v-btn>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="condition.textColor"
                        :items="colorOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('automated-forms.form.listConfig.modal.formatting.textColor')"
                        variant="outlined"
                        density="compact"
                        hide-details
                        class="mb-2"
                      ></v-select>
                      <v-text-field
                        v-if="condition.textColor === 'custom'"
                        v-model="condition.customTextColor"
                        :label="t('automated-forms.form.listConfig.modal.formatting.customTextColor')"
                        variant="outlined"
                        density="compact"
                        hide-details
                      >
                        <template v-slot:append>
                          <input
                            v-model="condition.customTextColor"
                            type="color"
                            style="width: 30px; height: 30px; border: none; cursor: pointer;"
                          />
                        </template>
                      </v-text-field>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        v-model="condition.backgroundColor"
                        :items="colorOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('automated-forms.form.listConfig.modal.formatting.backgroundColor')"
                        variant="outlined"
                        density="compact"
                        hide-details
                        class="mb-2"
                      ></v-select>
                      <v-text-field
                        v-if="condition.backgroundColor === 'custom'"
                        v-model="condition.customBackgroundColor"
                        :label="t('automated-forms.form.listConfig.modal.formatting.customBackgroundColor')"
                        variant="outlined"
                        density="compact"
                        hide-details
                      >
                        <template v-slot:append>
                          <input
                            v-model="condition.customBackgroundColor"
                            type="color"
                            style="width: 30px; height: 30px; border: none; cursor: pointer;"
                          />
                        </template>
                      </v-text-field>
                    </v-col>
                  </v-row>
                </div>
                
                <!-- Add Condition Button -->
                <v-btn
                  variant="outlined"
                  size="small"
                  color="primary"
                  @click="addCondition"
                  class="mb-3"
                >
                  <v-icon class="mr-2" size="small">mdi-plus</v-icon>
                  {{ t('automated-forms.form.listConfig.modal.formatting.conditions.add') }}
                </v-btn>
                
                <!-- Default Colors -->
                <v-divider class="my-3"></v-divider>
                <div class="text-caption mb-2">{{ t('automated-forms.form.listConfig.modal.formatting.conditions.default') }}</div>
                <v-row>
                  <v-col cols="6">
                    <v-select
                      v-model="selectedColumnForSettings.format.defaultTextColor"
                      :items="colorOptions"
                      item-title="title"
                      item-value="value"
                      :label="t('automated-forms.form.listConfig.modal.formatting.defaultTextColor')"
                      variant="outlined"
                      hide-details
                      class="mb-2"
                    ></v-select>
                    <v-text-field
                      v-if="selectedColumnForSettings.format.defaultTextColor === 'custom'"
                      v-model="selectedColumnForSettings.format.customDefaultTextColor"
                      :label="t('automated-forms.form.listConfig.modal.formatting.customTextColor')"
                      variant="outlined"
                      hide-details
                    >
                      <template v-slot:append>
                        <input
                          v-model="selectedColumnForSettings.format.customDefaultTextColor"
                          type="color"
                          style="width: 30px; height: 30px; border: none; cursor: pointer;"
                        />
                      </template>
                    </v-text-field>
                  </v-col>
                  <v-col cols="6">
                    <v-select
                      v-model="selectedColumnForSettings.format.defaultBackgroundColor"
                      :items="colorOptions"
                      item-title="title"
                      item-value="value"
                      :label="t('automated-forms.form.listConfig.modal.formatting.defaultBackgroundColor')"
                      variant="outlined"
                      hide-details
                      class="mb-2"
                    ></v-select>
                    <v-text-field
                      v-if="selectedColumnForSettings.format.defaultBackgroundColor === 'custom'"
                      v-model="selectedColumnForSettings.format.customDefaultBackgroundColor"
                      :label="t('automated-forms.form.listConfig.modal.formatting.customBackgroundColor')"
                      variant="outlined"
                      hide-details
                    >
                      <template v-slot:append>
                        <input
                          v-model="selectedColumnForSettings.format.customDefaultBackgroundColor"
                          type="color"
                          style="width: 30px; height: 30px; border: none; cursor: pointer;"
                        />
                      </template>
                    </v-text-field>
                  </v-col>
                </v-row>
              </v-col>
            </v-row>
          </v-col>
        </v-row>
      </v-card-text>
      
      <v-divider></v-divider>
      
      <v-card-actions class="pa-4">
        <v-spacer></v-spacer>
        <v-btn
          variant="outlined"
          color="default"
          @click="closeColumnSettings"
        >
          {{ t('automated-forms.form.listConfig.modal.cancel') }}
        </v-btn>
        <v-btn
          variant="flat"
          color="primary"
          @click="saveColumnSettings"
        >
          {{ t('automated-forms.form.listConfig.modal.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
