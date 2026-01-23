<script setup lang="ts">
import { ref, onMounted, computed, watch, nextTick } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDatasetStore, type CreateDatasetDto, type UpdateDatasetDto } from '@/stores/apps/dataset';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';
import { useAuthStore } from '@/stores/auth';
import { DatabaseIcon, CheckIcon, XIcon, ArrowLeftIcon, PlusIcon, EditIcon, TrashIcon, FileCodeIcon, KeyIcon, ShieldIcon } from 'vue-tabler-icons';
import type { FieldDefinition, FieldType, QueryDefinitionDto, IndexDefinition, QueryParameterDefinition, ValidationDefinition, PermissionsDefinition, PermissionDefinition } from '@/stores/apps/dataset';
import { fetchFromMngKeeper } from '@/services/apiService';

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
const datasetStore = useDatasetStore();
const categoryStore = useDatasetCategoryStore();
const authStore = useAuthStore();

// Check if user is admin
const isAdmin = computed(() => authStore.isAdmin);

// Props to determine mode (can be used as component or page)
const props = defineProps<{
  editName?: string;
}>();

// Check if edit mode
const isEditMode = computed(() => {
  if (props.editName) {
    return true;
  }
  if (route.path.includes('/create')) {
    return false;
  }
  return !!route.params.name && route.params.name !== 'create';
});

const datasetName = computed(() => {
  if (props.editName) {
    return props.editName;
  }
  return route.params.name as string | undefined;
});

const page = computed(() => ({
  title: isEditMode.value ? t('datasets.breadcrumbs.edit') : t('datasets.breadcrumbs.create'),
}));

const breadcrumbs = computed(() => [
  {
    text: t('datasets.breadcrumbs.home'),
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: t('datasets.breadcrumbs.datasets'),
    disabled: false,
    href: '/apps/datasets',
  },
  {
    text: page.value.title,
    disabled: true,
    href: '#',
  },
]);

// Stepper state
const currentStep = ref(1);
const steps = computed(() => [
  { title: t('datasets.form.steps.basicInfo'), value: 1, required: true },
  { title: t('datasets.form.steps.fieldDefinitions'), value: 2, required: false },
  { title: t('datasets.form.steps.predefinedQueries'), value: 3, required: false },
  { title: t('datasets.form.steps.validationDefinitions'), value: 4, required: false },
  { title: t('datasets.form.steps.indexDefinitions'), value: 5, required: false },
  { title: t('datasets.form.steps.permissions'), value: 6, required: false },
]);

// Form data
const formData = ref<CreateDatasetDto>({
  name: '',
  description: '',
  category: undefined,
  forceSchema: true,
  logging: 'none',
  publishMode: 'none',
  fields: [],
  validations: [],
  queries: [],
  indexList: [],
  permissions: undefined,
});

// Validation rules
const datasetNameRules = computed(() => [
  (v: string) => !!v || t('datasets.form.validation.nameRequired'),
  (v: string) => (v && v.length >= 2) || t('datasets.form.validation.nameMinLength'),
  (v: string) => (v && v.length <= 100) || t('datasets.form.validation.nameMaxLength'),
  (v: string) => {
    if (!v) return true;
    // @ prefix opsiyonel, backend otomatik ekler
    const withoutPrefix = v.startsWith('@') ? v.substring(1) : v;
    const pattern = /^[a-zA-Z][a-zA-Z0-9_-]*$/;
    return pattern.test(withoutPrefix) || t('datasets.form.validation.nameInvalidFormat');
  },
]);

const descriptionRules = computed(() => [
  (v: string) => !v || v.length <= 1000 || t('datasets.form.validation.descriptionMaxLength'),
]);

// Categories for dropdown
const categories = computed(() => categoryStore.categories || []);

// Filtered categories: Admin olmayan kullanıcılar için isSystemCategory: true olanları filtrele
const filteredCategories = computed(() => {
  if (isAdmin.value) {
    // Admin kullanıcılar tüm kategorileri görebilir
    return categories.value;
  }
  // Admin olmayan kullanıcılar için isSystemCategory: true olanları filtrele
  return categories.value.filter(cat => !cat.isSystemCategory);
});

// Category options for select
const categoryOptions = computed(() => {
  return filteredCategories.value.map(cat => ({
    title: cat.categoryName,
    value: cat.dataId,
  }));
});

// Form state
const formRef = ref();
const loading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

// Field management state
const showFieldModal = ref(false);
const fieldModalMode = ref<'create' | 'edit'>('create');
const fieldModalIndex = ref<number | null>(null);
const fieldFormData = ref<FieldDefinition>({
  fieldType: 'text',
  name: '',
  title: '',
  mandatory: false,
  unique: false,
  isArray: false,
  defaultValue: undefined,
  relationDataset: undefined,
  relationField: '__dataId',
  incrementalOptions: undefined,
  datetimeOptions: undefined,
  objectSchema: undefined,
  validation: undefined,
});
const fieldFormRef = ref();
const fieldFormError = ref('');

// Field type options
const fieldTypeOptions = computed(() => [
  { title: t('datasets.form.fieldTypes.text.title'), value: 'text' as FieldType, description: t('datasets.form.fieldTypes.text.description') },
  { title: t('datasets.form.fieldTypes.number.title'), value: 'number' as FieldType, description: t('datasets.form.fieldTypes.number.description') },
  { title: t('datasets.form.fieldTypes.bool.title'), value: 'bool' as FieldType, description: t('datasets.form.fieldTypes.bool.description') },
  { title: t('datasets.form.fieldTypes.datetime.title'), value: 'datetime' as FieldType, description: t('datasets.form.fieldTypes.datetime.description') },
  { title: t('datasets.form.fieldTypes.object.title'), value: 'object' as FieldType, description: t('datasets.form.fieldTypes.object.description') },
  { title: t('datasets.form.fieldTypes.relation.title'), value: 'relation' as FieldType, description: t('datasets.form.fieldTypes.relation.description') },
  { title: t('datasets.form.fieldTypes.persons.title'), value: 'persons' as FieldType, description: t('datasets.form.fieldTypes.persons.description') },
  { title: t('datasets.form.fieldTypes.personGroups.title'), value: 'personGroups' as FieldType, description: t('datasets.form.fieldTypes.personGroups.description') },
  { title: t('datasets.form.fieldTypes.incremental.title'), value: 'incremental' as FieldType, description: t('datasets.form.fieldTypes.incremental.description') },
]);

// Available datasets for relation field (will be loaded from datasetStore)
const availableDatasets = computed(() => datasetStore.datasets || []);

// Dataset options for relation field
const datasetOptions = computed(() => {
  return availableDatasets.value
    .filter(ds => ds.name !== formData.value.name) // Exclude current dataset
    .map(ds => ({
      title: ds.name,
      value: ds.name,
    }));
});

// Load categories
const loadCategories = async () => {
  try {
    if (categories.value.length === 0) {
      await categoryStore.fetchCategories({ pageNumber: 1, pageSize: 1000 });
    }
  } catch (error) {
    // Error handled by store
  }
};

// Groups for permissions
const groups = ref<Array<{ id: string; name: string; isActive: boolean }>>([]);
const loadingGroups = ref(false);

// Load groups from MngKeeper
const loadGroups = async () => {
  loadingGroups.value = true;
  try {
    const response = await fetchFromMngKeeper('/group?page=1&pageSize=1000', 'GET');
    
    let loadedGroups: any[] = [];
    
    if (response && Array.isArray(response)) {
      loadedGroups = response;
    } else if (response && response.groups && Array.isArray(response.groups)) {
      loadedGroups = response.groups;
    } else if (response && response.data && Array.isArray(response.data)) {
      loadedGroups = response.data;
    } else if (response && response.data && response.data.groups && Array.isArray(response.data.groups)) {
      loadedGroups = response.data.groups;
    } else if (response && response.items && Array.isArray(response.items)) {
      loadedGroups = response.items;
    }
    
    // Filter active groups
    groups.value = loadedGroups.filter((g: any) => {
      const isActive = g.isActive !== undefined ? g.isActive : (g.IsActive !== undefined ? g.IsActive : true);
      return isActive !== false;
    });
  } catch (error) {
    groups.value = [];
  } finally {
    loadingGroups.value = false;
  }
};

// Initialize permissions if not exists
const initPermissions = () => {
  if (!formData.value.permissions) {
    formData.value.permissions = {
      read: undefined,
      create: undefined,
      update: undefined,
      delete: undefined,
    };
  }
};

// Get selected groups for a permission type
const getSelectedGroups = (permissionType: 'read' | 'create' | 'update' | 'delete'): string[] => {
  if (!formData.value.permissions) return [];
  const permission = formData.value.permissions[permissionType];
  if (!permission || !permission.groups) return [];
  return Array.isArray(permission.groups) ? permission.groups : [];
};

// Set selected groups for a permission type
const setSelectedGroups = (permissionType: 'read' | 'create' | 'update' | 'delete', selectedGroups: string[]) => {
  initPermissions();
  if (!formData.value.permissions) return;
  
  if (selectedGroups.length === 0) {
    // Remove permission if no groups selected
    formData.value.permissions[permissionType] = undefined;
  } else {
    // Create or update permission
    if (!formData.value.permissions[permissionType]) {
      formData.value.permissions[permissionType] = { groups: [] };
    }
    formData.value.permissions[permissionType]!.groups = selectedGroups;
  }
};

// Group options for select
const groupOptions = computed(() => {
  return groups.value.map(g => ({
    title: g.name,
    value: g.name,
  }));
});

// Computed properties for v-model binding
const readGroups = computed({
  get: () => getSelectedGroups('read'),
  set: (value: string[]) => setSelectedGroups('read', value),
});

const createGroups = computed({
  get: () => getSelectedGroups('create'),
  set: (value: string[]) => setSelectedGroups('create', value),
});

const updateGroups = computed({
  get: () => getSelectedGroups('update'),
  set: (value: string[]) => setSelectedGroups('update', value),
});

const deleteGroups = computed({
  get: () => getSelectedGroups('delete'),
  set: (value: string[]) => setSelectedGroups('delete', value),
});

// Load dataset if edit mode
const loadDataset = async () => {
  if (!isEditMode.value || !datasetName.value) return;
  
  loading.value = true;
  try {
    const dataset = await datasetStore.fetchDatasetByName(decodeURIComponent(datasetName.value));
    if (dataset) {
      formData.value = {
        name: dataset.name,
        description: dataset.description || '',
        category: dataset.category || undefined,
        forceSchema: dataset.forceSchema,
        logging: dataset.logging as 'none' | 'self' | 'common',
        publishMode: dataset.publishMode as 'none' | 'basic' | 'full',
        fields: dataset.fields || [],
        validations: dataset.validations || [],
        queries: dataset.queries?.map(q => ({
          name: q.name,
          description: q.description,
          pipeline: q.pipeline,
          parameters: q.parameters,
        })) || [],
        indexList: dataset.indexList || [],
        permissions: dataset.permissions || undefined,
      };
      
      // Ensure permissions object exists (even if all permission types are null)
      // This ensures computed properties work correctly
      if (!formData.value.permissions) {
        formData.value.permissions = {
          read: undefined,
          create: undefined,
          update: undefined,
          delete: undefined,
        };
      }
    } else {
      errorMessage.value = t('datasets.form.messages.notFound');
      setTimeout(() => {
        router.push('/apps/datasets');
      }, 2000);
    }
  } catch (error: any) {
    errorMessage.value = error.message || t('datasets.form.messages.loadError');
    setTimeout(() => {
      router.push('/apps/datasets');
    }, 2000);
  } finally {
    loading.value = false;
  }
};

// Watch for permissions changes to ensure computed properties update
watch(() => formData.value.permissions, (newPermissions) => {
  // Force reactivity update when permissions change
  // This ensures computed properties (readGroups, createGroups, etc.) update when permissions are loaded
}, { deep: true, immediate: true });

onMounted(async () => {
  // Load categories first
  await loadCategories();
  
  // Load datasets for relation field dropdown
  await loadDatasets();
  
  // Load groups for permissions
  await loadGroups();
  
  // Load dataset if edit mode
  if (isEditMode.value) {
    await loadDataset();
    // Wait for next tick to ensure reactive updates
    await nextTick();
  } else {
    // Initialize permissions only if not in edit mode (edit mode'da loadDataset zaten permissions'ı yükler)
    initPermissions();
  }
});

// Validate current step
const validateStep = async (step: number): Promise<boolean> => {
  if (step === 1) {
    // Validate Step 1: Basic Info
    if (!formRef.value) {
      return true; // Form not ready yet, allow navigation
    }
    const { valid } = await formRef.value.validate();
    return valid;
  }
  // Other steps don't require validation (optional)
  return true;
};

// Navigate to step
const goToStep = async (step: number) => {
  // Validate current step before moving
  if (currentStep.value < step) {
    const isValid = await validateStep(currentStep.value);
    if (!isValid) {
      return;
    }
  }
  currentStep.value = step;
};

// Next step
const nextStep = async () => {
  const isValid = await validateStep(currentStep.value);
  if (!isValid) {
    return;
  }
  
  const nextStepValue = steps.value.find(s => s.value > currentStep.value)?.value;
  if (nextStepValue) {
    currentStep.value = nextStepValue;
  }
};

// Previous step
const prevStep = () => {
  const prevStepValue = steps.value.slice().reverse().find(s => s.value < currentStep.value)?.value;
  if (prevStepValue) {
    currentStep.value = prevStepValue;
  }
};

// Handle form submit
const handleSubmit = async () => {
  errorMessage.value = '';
  successMessage.value = '';
  
  // Validate all required steps
  const isValid = await validateStep(currentStep.value);
  if (!isValid) {
    return;
  }
  
  // Ensure dataset name has @ prefix (backend adds it automatically, but we'll ensure consistency)
  const datasetNameToSend = formData.value.name.startsWith('@') 
    ? formData.value.name 
    : `@${formData.value.name}`;
  
  loading.value = true;
  
  try {
    if (isEditMode.value && datasetName.value) {
      // Update existing dataset
      const updateDto: UpdateDatasetDto = {
        description: formData.value.description?.trim() || undefined,
        category: formData.value.category || undefined,
        forceSchema: formData.value.forceSchema,
        logging: formData.value.logging,
        publishMode: formData.value.publishMode,
        fields: formData.value.fields && formData.value.fields.length > 0 ? formData.value.fields : undefined,
        validations: formData.value.validations && formData.value.validations.length > 0 ? formData.value.validations : undefined,
        queries: formData.value.queries && formData.value.queries.length > 0 ? formData.value.queries : undefined,
        indexList: formData.value.indexList && formData.value.indexList.length > 0 ? formData.value.indexList : undefined,
        permissions: formData.value.permissions,
      };
      
      await datasetStore.updateDataset(decodeURIComponent(datasetName.value), updateDto);
      successMessage.value = t('datasets.form.messages.updateSuccess');
    } else {
      // Create new dataset
      const createDto: CreateDatasetDto = {
        ...formData.value,
        name: datasetNameToSend,
        description: formData.value.description?.trim() || undefined,
        category: formData.value.category || undefined,
        permissions: formData.value.permissions,
      };
      
      await datasetStore.createDataset(createDto);
      successMessage.value = t('datasets.form.messages.createSuccess');
    }
    
    // Redirect to list after success
    setTimeout(() => {
      router.push('/apps/datasets');
    }, 1500);
  } catch (error: any) {
    errorMessage.value = error.message || t('datasets.form.messages.saveError');
    
    // Handle duplicate dataset name error
    if (error.message && (error.message.includes('zaten mevcut') || error.message.includes('already exists'))) {
      errorMessage.value = t('datasets.form.messages.duplicateName');
    }
  } finally {
    loading.value = false;
  }
};

// Handle cancel
const handleCancel = () => {
  router.push('/apps/datasets');
};

// Check if step is complete
const isStepComplete = (step: number): boolean => {
  if (step === 1) {
    return !!formData.value.name && formData.value.name.trim().length >= 2;
  }
  if (step === 2) {
    // Step 2 is optional, but we can check if at least one field exists
    return true; // Optional step
  }
  return true; // Other steps are optional
};

// Field validation rules
const fieldNameRules = [
  (v: string) => !!v || 'Field adı gereklidir',
  (v: string) => {
    if (!v) return true;
    const pattern = /^[a-zA-Z][a-zA-Z0-9_]*$/;
    return pattern.test(v) || 'Field adı geçersiz format (harf ile başlamalı, sadece harf, rakam ve _ içerebilir)';
  },
  (v: string) => {
    if (!v) return true;
    // Check for duplicate field names (excluding current field being edited)
    const existingFields = formData.value.fields || [];
    const duplicateIndex = existingFields.findIndex((f, idx) => 
      f.name === v && idx !== fieldModalIndex.value
    );
    return duplicateIndex === -1 || 'Bu field adı zaten kullanılıyor';
  },
];

const fieldTitleRules = [
  (v: string) => !v || v.length <= 200 || 'Field başlığı en fazla 200 karakter olabilir',
];

const formatTemplateRules = [
  (v: string) => !!v || 'Format template gereklidir',
  (v: string) => {
    if (!v) return true;
    // Check if {0} placeholder exists
    return v.includes('{0}') || 'Format template {0} placeholder içermelidir';
  },
];

// Field management functions
const openFieldModal = (mode: 'create' | 'edit', index?: number) => {
  fieldModalMode.value = mode;
  fieldModalIndex.value = index ?? null;
  fieldFormError.value = '';
  
  if (mode === 'edit' && index !== undefined && formData.value.fields?.[index]) {
    // Load existing field data
    const existingField = formData.value.fields[index];
    fieldFormData.value = {
      fieldType: existingField.fieldType,
      name: existingField.name,
      title: existingField.title || '',
      mandatory: existingField.mandatory || false,
      unique: existingField.unique || false,
      isArray: existingField.isArray || false,
      defaultValue: existingField.defaultValue !== undefined ? existingField.defaultValue : undefined,
      relationDataset: existingField.relationDataset,
      relationField: existingField.relationField || '__dataId',
      incrementalOptions: existingField.incrementalOptions ? {
        format: existingField.incrementalOptions.format || '',
        startValue: existingField.incrementalOptions.startValue || 1,
        incrementStep: existingField.incrementalOptions.incrementStep || 1,
      } : undefined,
      datetimeOptions: existingField.datetimeOptions ? {
        showTime: existingField.datetimeOptions.showTime !== undefined ? existingField.datetimeOptions.showTime : true,
      } : undefined,
      objectSchema: existingField.objectSchema ? { ...existingField.objectSchema } : undefined,
      validation: existingField.validation ? { ...existingField.validation } : undefined,
    };
  } else {
    // Reset to default for create mode
    fieldFormData.value = {
      fieldType: 'text',
      name: '',
      title: '',
      mandatory: false,
      unique: false,
      isArray: false,
      relationDataset: undefined,
      relationField: '__dataId',
      incrementalOptions: undefined,
      datetimeOptions: undefined,
      objectSchema: undefined,
      validation: undefined,
    };
  }
  
  showFieldModal.value = true;
};

const closeFieldModal = () => {
  showFieldModal.value = false;
  fieldFormError.value = '';
  fieldModalIndex.value = null;
};

const saveField = async () => {
  fieldFormError.value = '';
  
  // Validate field form
  const { valid } = await fieldFormRef.value.validate();
  if (!valid) {
    return;
  }
  
  // Additional validation based on field type
  if (fieldFormData.value.fieldType === 'relation' && !fieldFormData.value.relationDataset) {
    fieldFormError.value = t('datasets.form.validation.relationDatasetRequired');
    return;
  }
  
  if (fieldFormData.value.fieldType === 'incremental') {
    if (!fieldFormData.value.incrementalOptions || !fieldFormData.value.incrementalOptions.format) {
      fieldFormError.value = t('datasets.form.validation.incrementalFormatRequired');
      return;
    }
    if (!fieldFormData.value.incrementalOptions.format.includes('{0}')) {
      fieldFormError.value = t('datasets.form.validation.incrementalFormatPlaceholder');
      return;
    }
    // Incremental field must be unique and mandatory
    fieldFormData.value.unique = true;
    fieldFormData.value.mandatory = true;
    fieldFormData.value.isArray = false;
  }
  
  // Validate object schema JSON if object type
  if (fieldFormData.value.fieldType === 'object' && objectSchemaJson.value) {
    try {
      const parsed = JSON.parse(objectSchemaJson.value);
      if (typeof parsed !== 'object' || parsed === null || Array.isArray(parsed)) {
        fieldFormError.value = t('datasets.form.validation.objectSchemaInvalid');
        return;
      }
    } catch (error) {
      fieldFormError.value = t('datasets.form.validation.objectSchemaInvalidJSON');
      return;
    }
  }
  
  // Prepare field data
  const fieldToSave: FieldDefinition = {
    fieldType: fieldFormData.value.fieldType,
    name: fieldFormData.value.name.trim(),
    title: fieldFormData.value.title?.trim() || undefined,
    mandatory: fieldFormData.value.mandatory,
    unique: fieldFormData.value.unique,
    isArray: fieldFormData.value.isArray,
  };
  
  // Add defaultValue if provided
  if (fieldFormData.value.defaultValue !== undefined && fieldFormData.value.defaultValue !== null && fieldFormData.value.defaultValue !== '') {
    fieldToSave.defaultValue = fieldFormData.value.defaultValue;
  }
  
  // Add type-specific fields
  if (fieldFormData.value.fieldType === 'relation') {
    fieldToSave.relationDataset = fieldFormData.value.relationDataset;
    fieldToSave.relationField = fieldFormData.value.relationField || '__dataId';
  }
  
  if (fieldFormData.value.fieldType === 'incremental' && fieldFormData.value.incrementalOptions) {
    fieldToSave.incrementalOptions = {
      format: fieldFormData.value.incrementalOptions.format,
      startValue: fieldFormData.value.incrementalOptions.startValue || 1,
      incrementStep: fieldFormData.value.incrementalOptions.incrementStep || 1,
    };
  }
  
  if (fieldFormData.value.fieldType === 'datetime' && fieldFormData.value.datetimeOptions) {
    fieldToSave.datetimeOptions = {
      showTime: fieldFormData.value.datetimeOptions.showTime !== undefined ? fieldFormData.value.datetimeOptions.showTime : true,
    };
  }
  
  if (fieldFormData.value.fieldType === 'object') {
    if (objectSchemaJson.value) {
      try {
        const parsed = JSON.parse(objectSchemaJson.value);
        if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
          fieldToSave.objectSchema = parsed;
        }
      } catch {
        // Error already handled in validation
      }
    }
  }
  
  // Add validation rules if provided
  if (fieldFormData.value.validation) {
    // Only include validation rules that have values
    const validation: any = {};
    if (fieldFormData.value.validation.min !== undefined && fieldFormData.value.validation.min !== null) {
      validation.min = fieldFormData.value.validation.min;
    }
    if (fieldFormData.value.validation.max !== undefined && fieldFormData.value.validation.max !== null) {
      validation.max = fieldFormData.value.validation.max;
    }
    if (fieldFormData.value.validation.minLength !== undefined && fieldFormData.value.validation.minLength !== null) {
      validation.minLength = fieldFormData.value.validation.minLength;
    }
    if (fieldFormData.value.validation.maxLength !== undefined && fieldFormData.value.validation.maxLength !== null) {
      validation.maxLength = fieldFormData.value.validation.maxLength;
    }
    if (fieldFormData.value.validation.pattern !== undefined && fieldFormData.value.validation.pattern !== null && fieldFormData.value.validation.pattern !== '') {
      validation.pattern = fieldFormData.value.validation.pattern;
    }
    if (fieldFormData.value.validation.minItems !== undefined && fieldFormData.value.validation.minItems !== null) {
      validation.minItems = fieldFormData.value.validation.minItems;
    }
    if (fieldFormData.value.validation.maxItems !== undefined && fieldFormData.value.validation.maxItems !== null) {
      validation.maxItems = fieldFormData.value.validation.maxItems;
    }
    if (fieldFormData.value.validation.minDate !== undefined && fieldFormData.value.validation.minDate !== null && fieldFormData.value.validation.minDate !== '') {
      validation.minDate = fieldFormData.value.validation.minDate;
    }
    if (fieldFormData.value.validation.maxDate !== undefined && fieldFormData.value.validation.maxDate !== null && fieldFormData.value.validation.maxDate !== '') {
      validation.maxDate = fieldFormData.value.validation.maxDate;
    }
    if (fieldFormData.value.validation.message !== undefined && fieldFormData.value.validation.message !== null && fieldFormData.value.validation.message !== '') {
      validation.message = fieldFormData.value.validation.message;
    }
    
    // Only add validation if it has at least one rule
    if (Object.keys(validation).length > 0) {
      fieldToSave.validation = validation;
    }
  }
  
  // Initialize fields array if not exists
  if (!formData.value.fields) {
    formData.value.fields = [];
  }
  
  // Add or update field
  if (fieldModalMode.value === 'edit' && fieldModalIndex.value !== null) {
    formData.value.fields[fieldModalIndex.value] = fieldToSave;
  } else {
    formData.value.fields.push(fieldToSave);
  }
  
  closeFieldModal();
};

const deleteField = (index: number) => {
  if (formData.value.fields && index >= 0 && index < formData.value.fields.length) {
    formData.value.fields.splice(index, 1);
  }
};

const getFieldTypeLabel = (fieldType: FieldType): string => {
  const option = fieldTypeOptions.value.find(opt => opt.value === fieldType);
  return option?.title || fieldType;
};

// Load datasets for relation field dropdown
const loadDatasets = async () => {
  try {
    if (availableDatasets.value.length === 0) {
      await datasetStore.fetchDatasets({ pageNumber: 1, pageSize: 1000 });
    }
  } catch (error) {
    // Error handled by store
  }
};

// Object Schema JSON (computed for two-way binding with textarea)
const objectSchemaJson = computed({
  get: () => {
    if (!fieldFormData.value.objectSchema) return '';
    try {
      return JSON.stringify(fieldFormData.value.objectSchema, null, 2);
    } catch {
      return '';
    }
  },
  set: (value: string) => {
    if (!value || !value.trim()) {
      fieldFormData.value.objectSchema = undefined;
      return;
    }
    try {
      const parsed = JSON.parse(value);
      if (typeof parsed === 'object' && parsed !== null && !Array.isArray(parsed)) {
        fieldFormData.value.objectSchema = parsed;
      } else {
        fieldFormError.value = 'Object schema geçerli bir JSON object olmalıdır';
      }
    } catch (error) {
      fieldFormError.value = 'Geçersiz JSON formatı';
    }
  },
});

// Default Value JSON (computed for two-way binding with textarea for object type)
const defaultValueJson = computed({
  get: () => {
    if (fieldFormData.value.fieldType === 'object' && fieldFormData.value.defaultValue) {
      try {
        return JSON.stringify(fieldFormData.value.defaultValue, null, 2);
      } catch {
        return '';
      }
    }
    return '';
  },
  set: (value: string) => {
    if (fieldFormData.value.fieldType === 'object') {
      if (!value || !value.trim()) {
        fieldFormData.value.defaultValue = undefined;
        return;
      }
      try {
        const parsed = JSON.parse(value);
        fieldFormData.value.defaultValue = parsed;
      } catch {
        fieldFormData.value.defaultValue = undefined;
      }
    }
  },
});

// Query management state
const showQueryModal = ref(false);
const queryModalMode = ref<'create' | 'edit'>('create');
const queryModalIndex = ref<number | null>(null);
const queryFormData = ref<QueryDefinitionDto>({
  name: '',
  description: '',
  pipeline: [],
  parameters: [],
});
const queryFormRef = ref();
const queryFormError = ref('');

// Query Parameters (new format: QueryParameterDefinition[])
const queryParametersList = ref<QueryParameterDefinition[]>([]);

// Pipeline JSON (computed for two-way binding with textarea)
const pipelineJson = computed({
  get: () => {
    if (!queryFormData.value.pipeline || queryFormData.value.pipeline.length === 0) {
      return '[]';
    }
    try {
      return JSON.stringify(queryFormData.value.pipeline, null, 2);
    } catch {
      return '[]';
    }
  },
  set: (value: string) => {
    if (!value || !value.trim()) {
      queryFormData.value.pipeline = [];
      return;
    }
    try {
      const parsed = JSON.parse(value);
      if (Array.isArray(parsed)) {
        queryFormData.value.pipeline = parsed;
        queryFormError.value = '';
      } else {
        queryFormError.value = 'Pipeline geçerli bir JSON array olmalıdır';
      }
    } catch (error) {
      queryFormError.value = 'Geçersiz JSON formatı: ' + (error instanceof Error ? error.message : 'Bilinmeyen hata');
    }
  },
});

// Parameters string (comma-separated for legacy format, can be extended for new format)
const parametersString = computed({
  get: () => {
    if (!queryFormData.value.parameters) return '';
    if (Array.isArray(queryFormData.value.parameters)) {
      // Check if it's legacy format (string[]) or new format (QueryParameterDefinition[])
      if (queryFormData.value.parameters.length > 0 && typeof queryFormData.value.parameters[0] === 'string') {
        return (queryFormData.value.parameters as string[]).join(', ');
      } else {
        // New format - extract names
        const params = queryFormData.value.parameters as any[];
        return params.map((p: any) => p.name || p.Name || '').filter(Boolean).join(', ');
      }
    }
    return '';
  },
  set: (value: string) => {
    if (!value || !value.trim()) {
      queryFormData.value.parameters = [];
      return;
    }
    // Legacy format: comma-separated string array
    const paramNames = value.split(',').map(p => p.trim()).filter(Boolean);
    queryFormData.value.parameters = paramNames;
  },
});

// Query Parameters management functions
const addQueryParameter = () => {
  queryParametersList.value.push({
    name: '',
    type: 'string',
    required: false,
  });
};

const removeQueryParameter = (index: number) => {
  if (queryParametersList.value.length > index) {
    queryParametersList.value.splice(index, 1);
  }
};

// Parameter type options
const parameterTypeOptions = [
  { title: 'String', value: 'string' },
  { title: 'Number', value: 'number' },
  { title: 'Boolean', value: 'boolean' },
  { title: 'Date', value: 'date' },
  { title: 'Object', value: 'object' },
];

// Query validation rules
const queryNameRules = [
  (v: string) => !!v || 'Query adı gereklidir',
  (v: string) => {
    if (!v) return true;
    const pattern = /^[a-zA-Z][a-zA-Z0-9_]*$/;
    return pattern.test(v) || 'Query adı geçersiz format (harf ile başlamalı, sadece harf, rakam ve _ içerebilir)';
  },
  (v: string) => {
    if (!v) return true;
    // Check for duplicate query names (excluding current query being edited)
    const existingQueries = formData.value.queries || [];
    const duplicateIndex = existingQueries.findIndex((q, idx) => 
      q.name === v && idx !== queryModalIndex.value
    );
    return duplicateIndex === -1 || 'Bu query adı zaten kullanılıyor';
  },
];

const pipelineJsonRules = [
  (v: string) => {
    if (!v || !v.trim()) {
      return 'Pipeline gereklidir (boş array [] olabilir)';
    }
    try {
      const parsed = JSON.parse(v);
      if (!Array.isArray(parsed)) {
        return 'Pipeline geçerli bir JSON array olmalıdır';
      }
      return true;
    } catch (error) {
      return 'Geçersiz JSON formatı';
    }
  },
];

// Query management functions
const openQueryModal = (mode: 'create' | 'edit', index?: number) => {
  queryModalMode.value = mode;
  queryModalIndex.value = index ?? null;
  queryFormError.value = '';
  
  if (mode === 'edit' && index !== undefined && formData.value.queries?.[index]) {
    // Load existing query data
    const existingQuery = formData.value.queries[index];
    queryFormData.value = {
      name: existingQuery.name,
      description: existingQuery.description || '',
      pipeline: existingQuery.pipeline || [],
      parameters: existingQuery.parameters || [],
    };
    
    // Load parameters into list (handle both legacy string[] and new QueryParameterDefinition[] formats)
    if (existingQuery.parameters && Array.isArray(existingQuery.parameters)) {
      if (existingQuery.parameters.length > 0 && typeof existingQuery.parameters[0] === 'string') {
        // Legacy format: convert string[] to QueryParameterDefinition[]
        queryParametersList.value = (existingQuery.parameters as string[]).map(name => ({
          name,
          type: 'string',
          required: false,
        }));
      } else {
        // New format: use as is
        queryParametersList.value = (existingQuery.parameters as QueryParameterDefinition[]).map(p => ({
          name: p.name || '',
          type: p.type || 'string',
          required: p.required || false,
        }));
      }
    } else {
      queryParametersList.value = [];
    }
  } else {
    // Reset to default for create mode
    queryFormData.value = {
      name: '',
      description: '',
      pipeline: [],
      parameters: [],
    };
    queryParametersList.value = [];
  }
  
  showQueryModal.value = true;
};

const closeQueryModal = () => {
  showQueryModal.value = false;
  queryFormError.value = '';
  queryModalIndex.value = null;
  queryParametersList.value = [];
};

const saveQuery = async () => {
  queryFormError.value = '';
  
  // Validate query form
  const { valid } = await queryFormRef.value.validate();
  if (!valid) {
    return;
  }
  
  // Validate pipeline JSON
  if (!queryFormData.value.pipeline || !Array.isArray(queryFormData.value.pipeline)) {
    queryFormError.value = 'Pipeline geçerli bir JSON array olmalıdır';
    return;
  }
  
  // Validate parameters
  const validParameters = queryParametersList.value
    .filter(p => p.name && p.name.trim())
    .map(p => ({
      name: p.name.trim(),
      type: p.type || 'string',
      required: p.required || false,
    }));
  
  // Check for duplicate parameter names
  const paramNames = validParameters.map(p => p.name.toLowerCase());
  const duplicateIndex = paramNames.findIndex((name, idx) => paramNames.indexOf(name) !== idx);
  if (duplicateIndex !== -1) {
    queryFormError.value = `Parameter adı tekrar ediyor: ${validParameters[duplicateIndex].name}`;
    return;
  }
  
  // Prepare query data
  const queryToSave: QueryDefinitionDto = {
    name: queryFormData.value.name.trim(),
    description: queryFormData.value.description?.trim() || undefined,
    pipeline: queryFormData.value.pipeline,
    parameters: validParameters.length > 0 ? validParameters : undefined,
  };
  
  // Initialize queries array if not exists
  if (!formData.value.queries) {
    formData.value.queries = [];
  }
  
  // Add or update query
  if (queryModalMode.value === 'edit' && queryModalIndex.value !== null) {
    formData.value.queries[queryModalIndex.value] = queryToSave;
  } else {
    formData.value.queries.push(queryToSave);
  }
  
  closeQueryModal();
};

const deleteQuery = (index: number) => {
  if (formData.value.queries && index >= 0 && index < formData.value.queries.length) {
    formData.value.queries.splice(index, 1);
  }
};

const formatPipelineJson = () => {
  try {
    if (pipelineJson.value) {
      const parsed = JSON.parse(pipelineJson.value);
      pipelineJson.value = JSON.stringify(parsed, null, 2);
      queryFormError.value = '';
    }
  } catch (error) {
    queryFormError.value = 'JSON formatlanırken hata oluştu';
  }
};

const getParametersDisplay = (parameters: string[] | any[] | undefined): string => {
  if (!parameters || parameters.length === 0) return '-';
  if (Array.isArray(parameters)) {
    if (parameters.length > 0 && typeof parameters[0] === 'string') {
      return (parameters as string[]).join(', ');
    } else {
      const params = parameters as any[];
      return params.map((p: any) => p.name || p.Name || '').filter(Boolean).join(', ');
    }
  }
  return '-';
};

// Validation management state
const showValidationModal = ref(false);
const validationModalMode = ref<'create' | 'edit'>('create');
const validationModalIndex = ref<number | null>(null);
const validationFormData = ref<ValidationDefinition>({
  name: '',
  description: '',
  type: 'expression',
  expression: '',
  url: undefined,
  method: 'POST',
  fields: undefined,
  when: 'both',
  order: 0,
});
const validationFormRef = ref();
const validationFormError = ref('');

// Validation type options
const validationTypeOptions = [
  { title: 'Expression', value: 'expression' },
  { title: 'HTTP', value: 'http' },
];

// Validation when options
const validationWhenOptions = [
  { title: 'Create', value: 'create' },
  { title: 'Update', value: 'update' },
  { title: 'Both', value: 'both' },
];

// HTTP method options
const httpMethodOptions = [
  { title: 'GET', value: 'GET' },
  { title: 'POST', value: 'POST' },
];

// Available field names for validation (from formData.fields)
const availableFieldsForValidation = computed(() => {
  const fields = formData.value.fields || [];
  return fields.map(f => ({
    title: f.title || f.name,
    value: f.name,
  }));
});

// Validation validation rules
const validationNameRules = [
  (v: string) => !!v || 'Validation adı gereklidir',
  (v: string) => {
    if (!v) return true;
    const pattern = /^[a-zA-Z][a-zA-Z0-9_]*$/;
    return pattern.test(v) || 'Validation adı geçersiz format (harf ile başlamalı, sadece harf, rakam ve _ içerebilir)';
  },
  (v: string) => {
    if (!v) return true;
    // Check for duplicate validation names (excluding current validation being edited)
    const existingValidations = formData.value.validations || [];
    const duplicateIndex = existingValidations.findIndex((val, valIndex) => 
      val.name === v && valIndex !== validationModalIndex.value
    );
    return duplicateIndex === -1 || 'Bu validation adı zaten kullanılıyor';
  },
];

const expressionRules = [
  (v: string) => {
    if (validationFormData.value.type === 'expression') {
      return !!v || 'Expression gereklidir';
    }
    return true;
  },
];

const urlRules = [
  (v: string) => {
    if (validationFormData.value.type === 'http') {
      return !!v || 'URL gereklidir';
    }
    return true;
  },
  (v: string) => {
    if (!v || validationFormData.value.type !== 'http') return true;
    try {
      new URL(v);
      return true;
    } catch {
      return 'Geçerli bir URL giriniz';
    }
  },
];

// Validation management functions
const openValidationModal = (mode: 'create' | 'edit', index?: number) => {
  validationModalMode.value = mode;
  validationModalIndex.value = index ?? null;
  validationFormError.value = '';
  
  if (mode === 'edit' && index !== undefined && formData.value.validations?.[index]) {
    // Load existing validation data
    const existingValidation = formData.value.validations[index];
    validationFormData.value = {
      name: existingValidation.name,
      description: existingValidation.description || '',
      type: existingValidation.type || 'expression',
      expression: existingValidation.expression || '',
      url: existingValidation.url,
      method: existingValidation.method || 'POST',
      fields: existingValidation.fields ? [...existingValidation.fields] : undefined,
      when: existingValidation.when || 'both',
      order: existingValidation.order ?? 0,
    };
  } else {
    // Reset to default for create mode
    validationFormData.value = {
      name: '',
      description: '',
      type: 'expression',
      expression: '',
      url: undefined,
      method: 'POST',
      fields: undefined,
      when: 'both',
      order: 0,
    };
  }
  
  showValidationModal.value = true;
};

const closeValidationModal = () => {
  showValidationModal.value = false;
  validationFormError.value = '';
  validationModalIndex.value = null;
};

const saveValidation = async () => {
  validationFormError.value = '';
  
  // Validate validation form
  const { valid } = await validationFormRef.value.validate();
  if (!valid) {
    return;
  }
  
  // Additional validation based on type
  if (validationFormData.value.type === 'expression') {
    if (!validationFormData.value.expression || !validationFormData.value.expression.trim()) {
      validationFormError.value = 'Expression gereklidir';
      return;
    }
  } else if (validationFormData.value.type === 'http') {
    if (!validationFormData.value.url || !validationFormData.value.url.trim()) {
      validationFormError.value = 'URL gereklidir';
      return;
    }
    try {
      new URL(validationFormData.value.url);
    } catch {
      validationFormError.value = 'Geçerli bir URL giriniz';
      return;
    }
  }
  
  // Prepare validation data
  const validationToSave: ValidationDefinition = {
    name: validationFormData.value.name.trim(),
    description: validationFormData.value.description?.trim() || undefined,
    type: validationFormData.value.type,
    when: validationFormData.value.when || 'both',
    order: validationFormData.value.order ?? 0,
  };
  
  // Add type-specific fields
  if (validationFormData.value.type === 'expression') {
    validationToSave.expression = validationFormData.value.expression.trim();
  } else if (validationFormData.value.type === 'http') {
    validationToSave.url = validationFormData.value.url.trim();
    validationToSave.method = validationFormData.value.method || 'POST';
    if (validationFormData.value.fields && validationFormData.value.fields.length > 0) {
      validationToSave.fields = validationFormData.value.fields;
    }
  }
  
  // Initialize validations array if not exists
  if (!formData.value.validations) {
    formData.value.validations = [];
  }
  
  // Add or update validation
  if (validationModalMode.value === 'edit' && validationModalIndex.value !== null) {
    formData.value.validations[validationModalIndex.value] = validationToSave;
  } else {
    formData.value.validations.push(validationToSave);
  }
  
  closeValidationModal();
};

const deleteValidation = (index: number) => {
  if (formData.value.validations && index >= 0 && index < formData.value.validations.length) {
    formData.value.validations.splice(index, 1);
  }
};

const getValidationTypeLabel = (type: string | undefined): string => {
  if (!type) return 'Unknown';
  const option = validationTypeOptions.find(opt => opt.value === type);
  return option?.title || type;
};

const getValidationWhenLabel = (when: string | undefined): string => {
  if (!when) return 'Both';
  const option = validationWhenOptions.find(opt => opt.value === when);
  return option?.title || when;
};

// Index management state
const showIndexModal = ref(false);
const indexModalMode = ref<'create' | 'edit'>('create');
const indexModalIndex = ref<number | null>(null);
const indexFormData = ref<IndexDefinition>({
  name: '',
  fields: {},
  unique: false,
});
const indexFormRef = ref();
const indexFormError = ref('');
const indexFieldsList = ref<Array<{ fieldName: string; order: 1 | -1 }>>([]);

// Available field names for index (from formData.fields)
const availableFieldNames = computed(() => {
  const fields = formData.value.fields || [];
  return fields.map(f => f.name).filter(Boolean);
});

// Index validation rules
const indexNameRules = [
  (v: string) => !!v || 'Index adı gereklidir',
  (v: string) => {
    if (!v) return true;
    const pattern = /^[a-zA-Z][a-zA-Z0-9_]*$/;
    return pattern.test(v) || 'Index adı geçersiz format (harf ile başlamalı, sadece harf, rakam ve _ içerebilir)';
  },
  (v: string) => {
    if (!v) return true;
    // Check for duplicate index names (excluding current index being edited)
    const existingIndexes = formData.value.indexList || [];
    const duplicateIndex = existingIndexes.findIndex((idx, idxIndex) => 
      idx.name === v && idxIndex !== indexModalIndex.value
    );
    return duplicateIndex === -1 || 'Bu index adı zaten kullanılıyor';
  },
];

// Index management functions
const openIndexModal = (mode: 'create' | 'edit', index?: number) => {
  indexModalMode.value = mode;
  indexModalIndex.value = index ?? null;
  indexFormError.value = '';
  indexFieldsList.value = [];
  
  if (mode === 'edit' && index !== undefined && formData.value.indexList?.[index]) {
    // Load existing index data
    const existingIndex = formData.value.indexList[index];
    indexFormData.value = {
      name: existingIndex.name,
      fields: { ...existingIndex.fields },
      unique: existingIndex.unique || false,
    };
    
    // Convert fields object to array for editing
    indexFieldsList.value = Object.entries(existingIndex.fields).map(([fieldName, order]) => ({
      fieldName,
      order: order === 1 ? 1 : -1,
    }));
  } else {
    // Reset to default for create mode
    indexFormData.value = {
      name: '',
      fields: {},
      unique: false,
    };
    // Add one empty field row
    indexFieldsList.value = [{ fieldName: '', order: 1 }];
  }
  
  showIndexModal.value = true;
};

const closeIndexModal = () => {
  showIndexModal.value = false;
  indexFormError.value = '';
  indexModalIndex.value = null;
  indexFieldsList.value = [];
};

const addIndexFieldRow = () => {
  indexFieldsList.value.push({ fieldName: '', order: 1 });
};

const removeIndexFieldRow = (index: number) => {
  if (indexFieldsList.value.length > 1) {
    indexFieldsList.value.splice(index, 1);
  } else {
    indexFormError.value = 'En az bir field tanımlanmalıdır';
  }
};

const saveIndex = () => {
  indexFormError.value = '';
  
  // Validate index name
  if (!indexFormData.value.name || !indexFormData.value.name.trim()) {
    indexFormError.value = 'Index adı gereklidir';
    return;
  }
  
  // Validate at least one field
  const validFields = indexFieldsList.value.filter(f => f.fieldName && f.fieldName.trim());
  if (validFields.length === 0) {
    indexFormError.value = 'En az bir field tanımlanmalıdır';
    return;
  }
  
  // Check for duplicate field names in index
  const fieldNames = validFields.map(f => f.fieldName.trim());
  const uniqueFieldNames = [...new Set(fieldNames)];
  if (fieldNames.length !== uniqueFieldNames.length) {
    indexFormError.value = 'Aynı field birden fazla kez eklenemez';
    return;
  }
  
  // Check if field names exist in dataset fields
  const availableFields = availableFieldNames.value;
  const invalidFields = fieldNames.filter(f => !availableFields.includes(f));
  if (invalidFields.length > 0) {
    indexFormError.value = `Geçersiz field'lar: ${invalidFields.join(', ')}`;
    return;
  }
  
  // Convert fields array to object
  const fieldsObject: { [key: string]: 1 | -1 } = {};
  validFields.forEach(f => {
    fieldsObject[f.fieldName.trim()] = f.order;
  });
  
  // Prepare index data
  const indexToSave: IndexDefinition = {
    name: indexFormData.value.name.trim(),
    fields: fieldsObject,
    unique: indexFormData.value.unique,
  };
  
  // Initialize indexList array if not exists
  if (!formData.value.indexList) {
    formData.value.indexList = [];
  }
  
  // Add or update index
  if (indexModalMode.value === 'edit' && indexModalIndex.value !== null) {
    formData.value.indexList[indexModalIndex.value] = indexToSave;
  } else {
    formData.value.indexList.push(indexToSave);
  }
  
  closeIndexModal();
};

const deleteIndex = (index: number) => {
  if (formData.value.indexList && index >= 0 && index < formData.value.indexList.length) {
    formData.value.indexList.splice(index, 1);
  }
};

const getFieldsDisplay = (fields: { [key: string]: 1 | -1 }): string => {
  if (!fields || Object.keys(fields).length === 0) return '-';
  return Object.entries(fields)
    .map(([fieldName, order]) => `${fieldName} (${order === 1 ? 'asc' : 'desc'})`)
    .join(', ');
};

// Watch field type changes to reset type-specific fields
watch(() => fieldFormData.value.fieldType, (newType) => {
  // Reset type-specific fields when type changes
  if (newType !== 'relation') {
    fieldFormData.value.relationDataset = undefined;
  }
  if (newType === 'incremental') {
    // Initialize incrementalOptions if not already set
    if (!fieldFormData.value.incrementalOptions) {
      fieldFormData.value.incrementalOptions = {
        format: '',
        startValue: 1,
        incrementStep: 1,
      };
    }
    // Reset validation for incremental (must be unique and mandatory)
    fieldFormData.value.unique = true;
    fieldFormData.value.mandatory = true;
    fieldFormData.value.isArray = false;
  }
  if (newType === 'datetime') {
    // Initialize datetimeOptions if not already set
    if (!fieldFormData.value.datetimeOptions) {
      fieldFormData.value.datetimeOptions = {
        showTime: true, // Default: show time
      };
    }
  }
});
</script>

<template>
  <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs" />
  
  <v-card elevation="10">
    <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
      <DatabaseIcon class="mr-2" size="24" />
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
        <strong>Uyarı:</strong> Bu sayfaya erişim için manager veya admin yetkisi gereklidir.
      </v-alert>
      
      <!-- Loading State (only for edit mode when fetching data) -->
      <div v-if="loading && isEditMode" class="text-center py-12">
        <v-progress-circular
          indeterminate
          color="primary"
          size="64"
        ></v-progress-circular>
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">Dataset yükleniyor...</p>
      </div>
      
      <!-- Form with Stepper -->
      <v-form
        v-else
        ref="formRef"
        @submit.prevent="handleSubmit"
      >
        <!-- Stepper Header -->
        <v-stepper v-model="currentStep" class="mb-6" v-if="steps && steps.length > 0">
          <v-stepper-header>
            <template v-for="(step, index) in steps" :key="step.value">
              <v-stepper-item
                :title="step.title"
                :value="step.value"
                :complete="isStepComplete(step.value)"
                :editable="false"
              ></v-stepper-item>
              <v-divider v-if="index < steps.length - 1"></v-divider>
            </template>
          </v-stepper-header>

          <v-stepper-window>
            <!-- Step 1: Temel Bilgiler -->
            <v-stepper-window-item :value="1">
              <v-row>
                <!-- Dataset Name -->
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="formData.name"
                    :rules="datasetNameRules"
                    :label="t('datasets.form.fields.name.label') + ' *'"
                    :placeholder="t('datasets.form.fields.name.placeholder')"
                    variant="outlined"
                    required
                    :disabled="loading || isEditMode"
                    :readonly="isEditMode"
                    :hint="t('datasets.form.fields.name.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-database"
                  ></v-text-field>
                  <v-alert
                    v-if="isEditMode"
                    type="info"
                    variant="tonal"
                    density="compact"
                    class="mt-2"
                  >
                    <strong>{{ t('datasets.form.fields.name.editNote').split(':')[0] }}:</strong> {{ t('datasets.form.fields.name.editNote').split(':')[1] }}
                  </v-alert>
                </v-col>
                
                <!-- Category -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.category"
                    :items="categoryOptions"
                    :label="t('datasets.form.fields.category.label')"
                    :placeholder="t('datasets.form.fields.category.placeholder')"
                    variant="outlined"
                    :loading="categoryStore.loading"
                    :disabled="loading"
                    :hint="t('datasets.form.fields.category.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-tag"
                    clearable
                  >
                    <template v-slot:item="{ props, item }">
                      <v-list-item v-bind="props" :title="item.title"></v-list-item>
                    </template>
                  </v-select>
                </v-col>
                
                <!-- Description -->
                <v-col cols="12">
                  <v-textarea
                    v-model="formData.description"
                    :rules="descriptionRules"
                    :label="t('datasets.form.fields.description.label')"
                    :placeholder="t('datasets.form.fields.description.placeholder')"
                    variant="outlined"
                    rows="4"
                    :disabled="loading"
                    :hint="t('datasets.form.fields.description.hint')"
                    persistent-hint
                    prepend-inner-icon="mdi-text"
                    auto-grow
                  ></v-textarea>
                </v-col>
                
                <!-- Schema Settings -->
                <v-col cols="12">
                  <v-divider class="my-4"></v-divider>
                  <h6 class="text-subtitle-1 font-weight-semibold mb-4">{{ t('datasets.form.fields.schemaSettings.title') }}</h6>
                  
                  <!-- Force Schema -->
                  <v-switch
                    v-model="formData.forceSchema"
                    :label="t('datasets.form.fields.schemaSettings.forceSchema.label')"
                    color="primary"
                    hide-details
                    class="mb-4"
                    :disabled="loading"
                    :hint="t('datasets.form.fields.schemaSettings.forceSchema.hint')"
                    persistent-hint
                  >
                    <template v-slot:append>
                      <span class="text-caption text-medium-emphasis">
                        {{ formData.forceSchema ? t('datasets.form.fields.schemaSettings.forceSchema.active') : t('datasets.form.fields.schemaSettings.forceSchema.passive') }}
                      </span>
                    </template>
                  </v-switch>
                  
                  <!-- Logging Mode -->
                  <v-select
                    v-model="formData.logging"
                    :items="[
                      { title: t('datasets.form.fields.schemaSettings.loggingMode.none'), value: 'none' },
                      { title: t('datasets.form.fields.schemaSettings.loggingMode.self'), value: 'self' },
                      { title: t('datasets.form.fields.schemaSettings.loggingMode.common'), value: 'common' },
                    ]"
                    :label="t('datasets.form.fields.schemaSettings.loggingMode.label')"
                    variant="outlined"
                    :disabled="loading"
                    :hint="t('datasets.form.fields.schemaSettings.loggingMode.hint')"
                    persistent-hint
                    class="mb-4"
                  ></v-select>
                  
                  <!-- Publish Mode -->
                  <v-select
                    v-model="formData.publishMode"
                    :items="[
                      { title: t('datasets.form.fields.schemaSettings.publishMode.none'), value: 'none' },
                      { title: t('datasets.form.fields.schemaSettings.publishMode.basic'), value: 'basic' },
                      { title: t('datasets.form.fields.schemaSettings.publishMode.full'), value: 'full' },
                    ]"
                    :label="t('datasets.form.fields.schemaSettings.publishMode.label')"
                    variant="outlined"
                    :disabled="loading"
                    :hint="t('datasets.form.fields.schemaSettings.publishMode.hint')"
                    persistent-hint
                  ></v-select>
                </v-col>
              </v-row>
            </v-stepper-window-item>

            <!-- Step 2: Field Definitions -->
            <v-stepper-window-item :value="2">
              <div class="mb-4">
                <div class="d-flex justify-space-between align-center mb-4">
                  <h6 class="text-subtitle-1 font-weight-semibold">{{ t('datasets.form.fieldList.title') }}</h6>
                  <v-btn
                    color="primary"
                    variant="flat"
                    size="small"
                    @click="openFieldModal('create')"
                    :disabled="loading"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addField') }}
                  </v-btn>
                </div>
                
                <!-- Field List -->
                <div v-if="formData.fields && formData.fields.length > 0" class="d-flex flex-column ga-3">
                  <v-card
                    v-for="(field, index) in formData.fields"
                    :key="index"
                    variant="outlined"
                    class="pa-4"
                  >
                    <div class="d-flex justify-space-between align-start">
                      <div class="flex-grow-1">
                        <div class="d-flex align-center ga-2 mb-2">
                          <v-chip size="small" variant="tonal" color="primary">
                            {{ getFieldTypeLabel(field.fieldType) }}
                          </v-chip>
                          <span class="text-subtitle-2 font-weight-semibold">{{ field.name }}</span>
                          <span v-if="field.title" class="text-body-2 text-medium-emphasis">
                            ({{ field.title }})
                          </span>
                        </div>
                        
                        <div class="d-flex ga-3 flex-wrap">
                          <v-chip
                            v-if="field.mandatory"
                            size="x-small"
                            variant="tonal"
                            color="success"
                          >
                            {{ t('datasets.form.fieldList.properties.mandatory') }}
                          </v-chip>
                          <v-chip
                            v-if="field.unique"
                            size="x-small"
                            variant="tonal"
                            color="info"
                          >
                            {{ t('datasets.form.fieldList.properties.unique') }}
                          </v-chip>
                          <v-chip
                            v-if="field.isArray"
                            size="x-small"
                            variant="tonal"
                            color="warning"
                          >
                            {{ t('datasets.form.fieldList.properties.array') }}
                          </v-chip>
                          <v-chip
                            v-if="field.fieldType === 'relation' && field.relationDataset"
                            size="x-small"
                            variant="tonal"
                            color="secondary"
                          >
                            {{ t('datasets.form.fieldList.properties.relation', { dataset: field.relationDataset }) }}
                          </v-chip>
                          <v-chip
                            v-if="field.fieldType === 'incremental' && field.incrementalOptions"
                            size="x-small"
                            variant="tonal"
                            color="purple"
                          >
                            {{ t('datasets.form.fieldList.properties.format', { format: field.incrementalOptions.format }) }}
                          </v-chip>
                        </div>
                      </div>
                      
                      <div class="d-flex ga-2">
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="info"
                          @click="openFieldModal('edit', index)"
                          :disabled="loading"
                        >
                          <EditIcon size="18" />
                          <v-tooltip activator="parent" location="top">{{ t('datasets.form.buttons.edit') }}</v-tooltip>
                        </v-btn>
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="error"
                          @click="deleteField(index)"
                          :disabled="loading"
                        >
                          <TrashIcon size="18" />
                          <v-tooltip activator="parent" location="top">{{ t('datasets.form.buttons.delete') }}</v-tooltip>
                        </v-btn>
                      </div>
                    </div>
                  </v-card>
                </div>
                
                <!-- Empty State -->
                <v-card v-else variant="outlined" class="pa-8 text-center">
                  <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-database-off</v-icon>
                  <p class="text-subtitle-1 text-medium-emphasis mb-4">
                    {{ t('datasets.form.fieldList.noFields') }}
                  </p>
                  <v-btn
                    color="primary"
                    variant="flat"
                    @click="openFieldModal('create')"
                    :disabled="loading"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addFirstField') }}
                  </v-btn>
                </v-card>
              </div>
            </v-stepper-window-item>

            <!-- Step 3: Predefined Queries -->
            <v-stepper-window-item :value="3">
              <div class="mb-4">
                <div class="d-flex justify-space-between align-center mb-4">
                  <h6 class="text-subtitle-1 font-weight-semibold">{{ t('datasets.form.queryList.title') }}</h6>
                  <v-btn
                    color="primary"
                    variant="flat"
                    size="small"
                    @click="openQueryModal('create')"
                    :disabled="loading"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addQuery') }}
                  </v-btn>
                </div>
                
                <!-- Query List -->
                <div v-if="formData.queries && formData.queries.length > 0" class="d-flex flex-column ga-3">
                  <v-card
                    v-for="(query, index) in formData.queries"
                    :key="index"
                    variant="outlined"
                    class="pa-4"
                  >
                    <div class="d-flex justify-space-between align-start">
                      <div class="flex-grow-1">
                        <div class="d-flex align-center ga-2 mb-2">
                          <FileCodeIcon size="18" class="text-primary" />
                          <span class="text-subtitle-2 font-weight-semibold">{{ query.name }}</span>
                        </div>
                        
                        <div v-if="query.description" class="mb-2">
                          <span class="text-body-2 text-medium-emphasis">{{ query.description }}</span>
                        </div>
                        
                        <div class="mb-2">
                          <span class="text-caption text-medium-emphasis">{{ t('datasets.form.queryList.parameters') }} </span>
                          <v-chip size="x-small" variant="tonal" color="info">
                            {{ getParametersDisplay(query.parameters) }}
                          </v-chip>
                        </div>
                        
                        <div v-if="query.pipeline && query.pipeline.length > 0" class="mt-2">
                          <v-expansion-panels variant="accordion" density="compact">
                            <v-expansion-panel>
                              <v-expansion-panel-title class="text-caption">
                                {{ t('datasets.form.queryList.viewPipeline', { count: query.pipeline.length }) }}
                              </v-expansion-panel-title>
                              <v-expansion-panel-text>
                                <pre class="text-caption bg-grey-lighten-4 pa-3 rounded">{{ JSON.stringify(query.pipeline, null, 2) }}</pre>
                              </v-expansion-panel-text>
                            </v-expansion-panel>
                          </v-expansion-panels>
                        </div>
                      </div>
                      
                      <div class="d-flex ga-2">
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="info"
                          @click="openQueryModal('edit', index)"
                          :disabled="loading"
                        >
                          <EditIcon size="18" />
                          <v-tooltip activator="parent" location="top">{{ t('datasets.form.buttons.edit') }}</v-tooltip>
                        </v-btn>
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="error"
                          @click="deleteQuery(index)"
                          :disabled="loading"
                        >
                          <TrashIcon size="18" />
                          <v-tooltip activator="parent" location="top">{{ t('datasets.form.buttons.delete') }}</v-tooltip>
                        </v-btn>
                      </div>
                    </div>
                  </v-card>
                </div>
                
                <!-- Empty State -->
                <v-card v-else variant="outlined" class="pa-8 text-center">
                  <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-code-braces</v-icon>
                  <p class="text-subtitle-1 text-medium-emphasis mb-4">
                    {{ t('datasets.form.queryList.noQueries') }}
                  </p>
                  <v-btn
                    color="primary"
                    variant="flat"
                    @click="openQueryModal('create')"
                    :disabled="loading"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addFirstQuery') }}
                  </v-btn>
                </v-card>
              </div>
            </v-stepper-window-item>

            <!-- Step 4: Validation Definitions -->
            <v-stepper-window-item :value="4">
              <div class="mb-4">
                <div class="d-flex justify-space-between align-center mb-4">
                  <h6 class="text-subtitle-1 font-weight-semibold">{{ t('datasets.form.validationList.title') }}</h6>
                  <v-btn
                    color="primary"
                    variant="flat"
                    size="small"
                    @click="openValidationModal('create')"
                    :disabled="loading"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addValidation') }}
                  </v-btn>
                </div>
                
                <!-- Validation List -->
                <div v-if="formData.validations && formData.validations.length > 0" class="d-flex flex-column ga-3">
                  <v-card
                    v-for="(validation, index) in formData.validations"
                    :key="index"
                    variant="outlined"
                    class="pa-4"
                  >
                    <div class="d-flex justify-space-between align-start">
                      <div class="flex-grow-1">
                        <div class="d-flex align-center ga-2 mb-2">
                          <FileCodeIcon size="18" class="text-primary" />
                          <span class="text-subtitle-2 font-weight-semibold">{{ validation.name }}</span>
                          <v-chip
                            size="x-small"
                            variant="tonal"
                            :color="validation.type === 'expression' ? 'info' : 'warning'"
                          >
                            {{ getValidationTypeLabel(validation.type) }}
                          </v-chip>
                          <v-chip
                            size="x-small"
                            variant="tonal"
                            color="secondary"
                          >
                            {{ t('datasets.form.validationList.when', { when: getValidationWhenLabel(validation.when) }) }}
                          </v-chip>
                          <v-chip
                            v-if="validation.order !== undefined"
                            size="x-small"
                            variant="tonal"
                            color="grey"
                          >
                            {{ t('datasets.form.validationList.order', { order: validation.order }) }}
                          </v-chip>
                        </div>
                        
                        <div v-if="validation.description" class="mb-2">
                          <span class="text-body-2 text-medium-emphasis">{{ validation.description }}</span>
                        </div>
                        
                        <div v-if="validation.type === 'expression' && validation.expression" class="mb-2">
                          <span class="text-caption text-medium-emphasis">{{ t('datasets.form.validationList.expression') }} </span>
                          <code class="text-body-2">{{ validation.expression }}</code>
                        </div>
                        
                        <div v-if="validation.type === 'http' && validation.url" class="mb-2">
                          <div>
                            <span class="text-caption text-medium-emphasis">{{ t('datasets.form.validationList.url') }} </span>
                            <span class="text-body-2">{{ validation.url }}</span>
                          </div>
                          <div v-if="validation.method">
                            <span class="text-caption text-medium-emphasis">{{ t('datasets.form.validationList.method') }} </span>
                            <span class="text-body-2">{{ validation.method }}</span>
                          </div>
                          <div v-if="validation.fields && validation.fields.length > 0">
                            <span class="text-caption text-medium-emphasis">{{ t('datasets.form.validationList.fields') }} </span>
                            <v-chip
                              v-for="field in validation.fields"
                              :key="field"
                              size="x-small"
                              variant="tonal"
                              color="info"
                              class="mr-1"
                            >
                              {{ field }}
                            </v-chip>
                          </div>
                        </div>
                      </div>
                      
                      <div class="d-flex ga-2">
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="primary"
                          @click="openValidationModal('edit', index)"
                          :disabled="loading"
                        >
                          <EditIcon size="18" />
                        </v-btn>
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="error"
                          @click="deleteValidation(index)"
                          :disabled="loading"
                        >
                          <TrashIcon size="18" />
                        </v-btn>
                      </div>
                    </div>
                  </v-card>
                </div>
                
                <!-- Empty State -->
                <v-card v-else variant="outlined" class="pa-8 text-center">
                  <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-shield-check-outline</v-icon>
                  <p class="text-subtitle-1 text-medium-emphasis mb-4">
                    {{ t('datasets.form.validationList.noValidations') }}
                  </p>
                  <v-btn
                    color="primary"
                    variant="flat"
                    @click="openValidationModal('create')"
                    :disabled="loading"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addFirstValidation') }}
                  </v-btn>
                </v-card>
              </div>
            </v-stepper-window-item>

            <!-- Step 5: Index Definitions -->
            <v-stepper-window-item :value="5">
              <div class="mb-4">
                <div class="d-flex justify-space-between align-center mb-4">
                  <h6 class="text-subtitle-1 font-weight-semibold">{{ t('datasets.form.indexList.title') }}</h6>
                  <v-btn
                    color="primary"
                    variant="flat"
                    size="small"
                    @click="openIndexModal('create')"
                    :disabled="loading || availableFieldNames.length === 0"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addIndex') }}
                  </v-btn>
                </div>
                
                <!-- Info Alert if no fields defined -->
                <v-alert
                  v-if="availableFieldNames.length === 0"
                  type="warning"
                  variant="tonal"
                  density="compact"
                  class="mb-4"
                >
                  <strong>Uyarı:</strong> Index tanımlamak için önce Step 2'de en az bir field tanımlamalısınız.
                </v-alert>
                
                <!-- Index List -->
                <div v-if="formData.indexList && formData.indexList.length > 0" class="d-flex flex-column ga-3">
                  <v-card
                    v-for="(index, indexIdx) in formData.indexList"
                    :key="indexIdx"
                    variant="outlined"
                    class="pa-4"
                  >
                    <div class="d-flex justify-space-between align-start">
                      <div class="flex-grow-1">
                        <div class="d-flex align-center ga-2 mb-2">
                          <KeyIcon size="18" class="text-primary" />
                          <span class="text-subtitle-2 font-weight-semibold">{{ index.name }}</span>
                          <v-chip
                            v-if="index.unique"
                            size="x-small"
                            variant="tonal"
                            color="success"
                          >
                            {{ t('datasets.form.fieldList.properties.unique') }}
                          </v-chip>
                        </div>
                        
                        <div class="mb-2">
                          <span class="text-caption text-medium-emphasis">{{ t('datasets.form.indexList.fields') }} </span>
                          <span class="text-body-2">{{ getFieldsDisplay(index.fields) }}</span>
                        </div>
                      </div>
                      
                      <div class="d-flex ga-2">
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="info"
                          @click="openIndexModal('edit', indexIdx)"
                          :disabled="loading"
                        >
                          <EditIcon size="18" />
                          <v-tooltip activator="parent" location="top">{{ t('datasets.form.buttons.edit') }}</v-tooltip>
                        </v-btn>
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="error"
                          @click="deleteIndex(indexIdx)"
                          :disabled="loading"
                        >
                          <TrashIcon size="18" />
                          <v-tooltip activator="parent" location="top">{{ t('datasets.form.buttons.delete') }}</v-tooltip>
                        </v-btn>
                      </div>
                    </div>
                  </v-card>
                </div>
                
                <!-- Empty State -->
                <v-card v-else variant="outlined" class="pa-8 text-center">
                  <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-key</v-icon>
                  <p class="text-subtitle-1 text-medium-emphasis mb-4">
                    {{ t('datasets.form.indexList.noIndexes') }}
                  </p>
                  <v-btn
                    color="primary"
                    variant="flat"
                    @click="openIndexModal('create')"
                    :disabled="loading || availableFieldNames.length === 0"
                  >
                    <PlusIcon class="mr-2" size="18" />
                    {{ t('datasets.form.buttons.addFirstIndex') }}
                  </v-btn>
                </v-card>
              </div>
            </v-stepper-window-item>
            
            <!-- Step 6: Permissions -->
            <v-stepper-window-item :value="6">
              <div class="mb-4">
                <div class="d-flex justify-space-between align-center mb-4">
                  <h6 class="text-subtitle-1 font-weight-semibold">{{ t('datasets.form.permissions.title') }}</h6>
                  <ShieldIcon size="20" class="text-primary" />
                </div>
                
                <v-alert
                  type="info"
                  variant="tonal"
                  density="compact"
                  class="mb-4"
                >
                  <strong>{{ t('datasets.form.permissions.info.title') }}:</strong> {{ t('datasets.form.permissions.info.description') }}
                </v-alert>
                
                <v-row>
                  <!-- Read Permission -->
                  <v-col cols="12" md="6">
                    <v-card variant="outlined" class="pa-4">
                      <div class="d-flex align-center mb-3">
                        <v-icon color="info" class="mr-2">mdi-eye</v-icon>
                        <span class="text-subtitle-2 font-weight-semibold">{{ t('datasets.form.permissions.read') }}</span>
                      </div>
                      <v-select
                        v-model="readGroups"
                        :items="groupOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('datasets.form.permissions.selectGroups')"
                        multiple
                        chips
                        closable-chips
                        variant="outlined"
                        density="compact"
                        :loading="loadingGroups"
                        :disabled="loading || loadingGroups"
                      ></v-select>
                    </v-card>
                  </v-col>
                  
                  <!-- Create Permission -->
                  <v-col cols="12" md="6">
                    <v-card variant="outlined" class="pa-4">
                      <div class="d-flex align-center mb-3">
                        <v-icon color="success" class="mr-2">mdi-plus</v-icon>
                        <span class="text-subtitle-2 font-weight-semibold">{{ t('datasets.form.permissions.create') }}</span>
                      </div>
                      <v-select
                        v-model="createGroups"
                        :items="groupOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('datasets.form.permissions.selectGroups')"
                        multiple
                        chips
                        closable-chips
                        variant="outlined"
                        density="compact"
                        :loading="loadingGroups"
                        :disabled="loading || loadingGroups"
                      ></v-select>
                    </v-card>
                  </v-col>
                  
                  <!-- Update Permission -->
                  <v-col cols="12" md="6">
                    <v-card variant="outlined" class="pa-4">
                      <div class="d-flex align-center mb-3">
                        <v-icon color="warning" class="mr-2">mdi-pencil</v-icon>
                        <span class="text-subtitle-2 font-weight-semibold">{{ t('datasets.form.permissions.update') }}</span>
                      </div>
                      <v-select
                        v-model="updateGroups"
                        :items="groupOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('datasets.form.permissions.selectGroups')"
                        multiple
                        chips
                        closable-chips
                        variant="outlined"
                        density="compact"
                        :loading="loadingGroups"
                        :disabled="loading || loadingGroups"
                      ></v-select>
                    </v-card>
                  </v-col>
                  
                  <!-- Delete Permission -->
                  <v-col cols="12" md="6">
                    <v-card variant="outlined" class="pa-4">
                      <div class="d-flex align-center mb-3">
                        <v-icon color="error" class="mr-2">mdi-delete</v-icon>
                        <span class="text-subtitle-2 font-weight-semibold">{{ t('datasets.form.permissions.delete') }}</span>
                      </div>
                      <v-select
                        v-model="deleteGroups"
                        :items="groupOptions"
                        item-title="title"
                        item-value="value"
                        :label="t('datasets.form.permissions.selectGroups')"
                        multiple
                        chips
                        closable-chips
                        variant="outlined"
                        density="compact"
                        :loading="loadingGroups"
                        :disabled="loading || loadingGroups"
                      ></v-select>
                    </v-card>
                  </v-col>
                </v-row>
                
                <!-- Empty State -->
                <v-card v-if="!formData.permissions || (!formData.permissions.read && !formData.permissions.create && !formData.permissions.update && !formData.permissions.delete)" variant="outlined" class="pa-8 text-center mt-4">
                  <ShieldIcon size="48" color="grey-lighten-1" class="mb-4" />
                  <p class="text-subtitle-1 text-medium-emphasis mb-4">
                    {{ t('datasets.form.permissions.noPermissions') }}
                  </p>
                  <p class="text-body-2 text-medium-emphasis">
                    {{ t('datasets.form.permissions.noPermissionsHint') }}
                  </p>
                </v-card>
              </div>
            </v-stepper-window-item>
          </v-stepper-window>
        </v-stepper>
        
        <!-- Stepper Actions -->
        <v-row class="mt-4">
          <v-col cols="12" class="d-flex justify-space-between ga-3">
            <div class="d-flex ga-3">
              <v-btn
                v-if="currentStep > 1"
                color="default"
                variant="outlined"
                @click="prevStep"
                :disabled="loading"
              >
                <ArrowLeftIcon class="mr-2" size="18" />
                {{ t('datasets.form.buttons.previous') }}
              </v-btn>
              
              <v-btn
                v-if="steps.length > 0 && currentStep < steps[steps.length - 1].value"
                color="primary"
                variant="outlined"
                @click="nextStep"
                :disabled="loading"
              >
                {{ t('datasets.form.buttons.next') }}
                <v-icon class="ml-2">mdi-arrow-right</v-icon>
              </v-btn>
            </div>
            
            <div class="d-flex ga-3">
              <v-btn
                color="default"
                variant="outlined"
                @click="handleCancel"
                :disabled="loading"
              >
                <XIcon class="mr-2" size="18" />
                {{ t('datasets.form.buttons.cancel') }}
              </v-btn>
              
              <v-btn
                color="primary"
                variant="flat"
                @click="handleSubmit"
                :loading="loading"
                :disabled="!formData.name.trim()"
              >
                <CheckIcon class="mr-2" size="18" />
                {{ isEditMode ? t('datasets.form.buttons.save') : t('datasets.buttons.create') }}
              </v-btn>
            </div>
          </v-col>
        </v-row>
      </v-form>
    </v-card-text>
  </v-card>

  <!-- Field Modal -->
  <v-dialog v-model="showFieldModal" max-width="800px" persistent>
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
        <DatabaseIcon class="mr-2" size="20" />
        <span>{{ fieldModalMode === 'edit' ? 'Field Düzenle' : 'Yeni Field Ekle' }}</span>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6">
        <!-- Field Form Error -->
        <v-alert
          v-if="fieldFormError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="fieldFormError = ''"
        >
          {{ fieldFormError }}
        </v-alert>
        
        <v-form ref="fieldFormRef">
          <v-row>
            <!-- Field Type -->
            <v-col cols="12" md="6">
              <v-select
                v-model="fieldFormData.fieldType"
                :items="fieldTypeOptions"
                item-title="title"
                item-value="value"
                label="Field Type *"
                variant="outlined"
                required
                :disabled="loading || fieldModalMode === 'edit'"
                hint="Field type seçildikten sonra değiştirilemez"
                persistent-hint
              >
                <template v-slot:item="{ props, item }">
                  <v-list-item v-bind="props">
                    <template v-slot:title>{{ item.title }}</template>
                    <template v-slot:subtitle>{{ item.description }}</template>
                  </v-list-item>
                </template>
              </v-select>
            </v-col>
            
            <!-- Field Name -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="fieldFormData.name"
                :rules="fieldNameRules"
                label="Field Name *"
                placeholder="Örn: isbn, title, publisher"
                variant="outlined"
                required
                :disabled="loading || fieldModalMode === 'edit'"
                :readonly="fieldModalMode === 'edit'"
                hint="Harf ile başlamalı, sadece harf, rakam ve _ içerebilir, benzersiz olmalıdır"
                persistent-hint
              ></v-text-field>
            </v-col>
            
            <!-- Field Title -->
            <v-col cols="12">
              <v-text-field
                v-model="fieldFormData.title"
                :rules="fieldTitleRules"
                label="Field Title"
                placeholder="Örn: ISBN, Title, Publisher"
                variant="outlined"
                :disabled="loading"
                hint="Görüntüleme için kullanılır (opsiyonel)"
                persistent-hint
              ></v-text-field>
            </v-col>
            
            <!-- Field Options -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <div class="d-flex ga-4 flex-wrap">
                <v-checkbox
                  v-model="fieldFormData.mandatory"
                  label="Mandatory"
                  color="success"
                  :disabled="loading || fieldFormData.fieldType === 'incremental'"
                  hide-details
                >
                  <template v-slot:label>
                    <span class="text-body-2">Mandatory</span>
                  </template>
                </v-checkbox>
                
                <v-checkbox
                  v-model="fieldFormData.unique"
                  label="Unique"
                  color="info"
                  :disabled="loading || fieldFormData.fieldType === 'incremental'"
                  hide-details
                >
                  <template v-slot:label>
                    <span class="text-body-2">Unique</span>
                  </template>
                </v-checkbox>
                
                <v-checkbox
                  v-model="fieldFormData.isArray"
                  label="Array"
                  color="warning"
                  :disabled="loading || fieldFormData.fieldType === 'incremental'"
                  hide-details
                >
                  <template v-slot:label>
                    <span class="text-body-2">Array</span>
                  </template>
                </v-checkbox>
              </div>
              
              <v-alert
                v-if="fieldFormData.fieldType === 'incremental'"
                type="info"
                variant="tonal"
                density="compact"
                class="mt-2"
              >
                <strong>Not:</strong> Incremental field'lar otomatik olarak unique ve mandatory'dir, array olamaz.
              </v-alert>
            </v-col>
            
            <!-- Relation Field Options -->
            <template v-if="fieldFormData.fieldType === 'relation'">
              <v-col cols="12" md="6">
                <v-select
                  v-model="fieldFormData.relationDataset"
                  :items="datasetOptions"
                  label="Relation Dataset *"
                  placeholder="Dataset seçin"
                  variant="outlined"
                  required
                  :disabled="loading"
                  :loading="datasetStore.loading"
                  hint="Hangi dataset'e referans verilecek?"
                  persistent-hint
                ></v-select>
              </v-col>
              
              <v-col cols="12" md="6">
                <v-text-field
                  v-model="fieldFormData.relationField"
                  label="Relation Field"
                  placeholder="__dataId"
                  variant="outlined"
                  :disabled="loading"
                  hint="Referans edilecek field (default: __dataId)"
                  persistent-hint
                ></v-text-field>
              </v-col>
            </template>
            
            <!-- Incremental Field Options -->
            <template v-if="fieldFormData.fieldType === 'incremental'">
              <v-col cols="12">
                <v-text-field
                  v-model="fieldFormData.incrementalOptions.format"
                  :rules="formatTemplateRules"
                  label="Format Template *"
                  placeholder="Örn: ISBN-{year}-{0:D6}, TASK-{0:D6}"
                  variant="outlined"
                  required
                  :disabled="loading"
                  hint="Placeholders: {0} (counter), {year}, {yy}, {month}, {day}, {domain}, {fieldName}"
                  persistent-hint
                >
                  <template v-slot:append-inner>
                    <v-tooltip location="top">
                      <template v-slot:activator="{ props }">
                        <v-icon v-bind="props" size="small" color="info">mdi-information</v-icon>
                      </template>
                      <div class="text-caption">
                        <div><strong>{0}</strong> - Counter value (required)</div>
                        <div><strong>{0:D6}</strong> - Zero-padded 6 digits</div>
                        <div><strong>{year}</strong> - 2025</div>
                        <div><strong>{yy}</strong> - 25</div>
                        <div><strong>{month}</strong> - 12</div>
                        <div><strong>{day}</strong> - 06</div>
                        <div><strong>{domain}</strong> - seven</div>
                        <div><strong>{fieldName}</strong> - Dynamic field reference</div>
                      </div>
                    </v-tooltip>
                  </template>
                </v-text-field>
              </v-col>
              
              <v-col cols="12" md="6">
                <v-text-field
                  v-model.number="fieldFormData.incrementalOptions.startValue"
                  type="number"
                  label="Start Value"
                  placeholder="1"
                  variant="outlined"
                  :disabled="loading"
                  hint="Başlangıç değeri (default: 1)"
                  persistent-hint
                ></v-text-field>
              </v-col>
              
              <v-col cols="12" md="6">
                <v-text-field
                  v-model.number="fieldFormData.incrementalOptions.incrementStep"
                  type="number"
                  label="Increment Step"
                  placeholder="1"
                  variant="outlined"
                  :disabled="loading"
                  hint="Artış miktarı (default: 1)"
                  persistent-hint
                ></v-text-field>
              </v-col>
            </template>
            
            <!-- DateTime Field Options -->
            <template v-if="fieldFormData.fieldType === 'datetime'">
              <v-col cols="12">
                <v-switch
                  :model-value="fieldFormData.datetimeOptions?.showTime !== false"
                  @update:model-value="(val: boolean) => {
                    if (!fieldFormData.datetimeOptions) {
                      fieldFormData.datetimeOptions = { showTime: val };
                    } else {
                      fieldFormData.datetimeOptions.showTime = val;
                    }
                  }"
                  :label="t('datasets.form.fields.datetimeOptions.showTime')"
                  :hint="t('datasets.form.fields.datetimeOptions.showTimeHint')"
                  persistent-hint
                  color="primary"
                  :disabled="loading"
                  hide-details="auto"
                >
                  <template #label>
                    <span>{{ t('datasets.form.fields.datetimeOptions.showTime') }}</span>
                  </template>
                </v-switch>
              </v-col>
            </template>
            
            <!-- Object Field Options -->
            <template v-if="fieldFormData.fieldType === 'object'">
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact" class="mb-2">
                  <strong>Object Schema:</strong> Object field için schema tanımı JSON formatında olmalıdır.
                  <br>Örnek: <code>{"url": "text", "alt": "text", "width": "number", "height": "number"}</code>
                </v-alert>
                <v-textarea
                  v-model="objectSchemaJson"
                  label="Object Schema (JSON)"
                  placeholder='{"url": "text", "alt": "text", "width": "number", "height": "number"}'
                  variant="outlined"
                  rows="6"
                  :disabled="loading"
                  hint="JSON formatında object schema tanımı"
                  persistent-hint
                ></v-textarea>
              </v-col>
            </template>
            
            <!-- Persons/PersonGroups Field Info -->
            <template v-if="fieldFormData.fieldType === 'persons' || fieldFormData.fieldType === 'personGroups'">
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact">
                  <strong>{{ fieldFormData.fieldType === 'persons' ? 'Persons' : 'Person Groups' }} Field:</strong>
                  Bu field type için MngKeeper'dan kullanıcı/grup seçimi yapılır. Schema'da ek konfigürasyon gerekmez.
                </v-alert>
              </v-col>
            </template>
            
            <!-- Default Value -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <h3 class="text-subtitle-1 mb-2">Default Value (Optional)</h3>
              
              <!-- Text Default Value -->
              <template v-if="fieldFormData.fieldType === 'text'">
                <v-text-field
                  v-model="fieldFormData.defaultValue"
                  label="Default Value"
                  placeholder="Örn: Default text"
                  variant="outlined"
                  :disabled="loading"
                  hint="Field için varsayılan değer"
                  persistent-hint
                  clearable
                ></v-text-field>
              </template>
              
              <!-- Number Default Value -->
              <template v-else-if="fieldFormData.fieldType === 'number'">
                <v-text-field
                  v-model.number="fieldFormData.defaultValue"
                  type="number"
                  label="Default Value"
                  placeholder="Örn: 0, 100"
                  variant="outlined"
                  :disabled="loading"
                  hint="Field için varsayılan sayısal değer"
                  persistent-hint
                  clearable
                ></v-text-field>
              </template>
              
              <!-- Boolean Default Value -->
              <template v-else-if="fieldFormData.fieldType === 'bool'">
                <v-select
                  v-model="fieldFormData.defaultValue"
                  :items="[{ title: 'True', value: true }, { title: 'False', value: false }]"
                  label="Default Value"
                  variant="outlined"
                  :disabled="loading"
                  hint="Field için varsayılan boolean değer"
                  persistent-hint
                  clearable
                ></v-select>
              </template>
              
              <!-- DateTime Default Value -->
              <template v-else-if="fieldFormData.fieldType === 'datetime'">
                <v-text-field
                  v-model="fieldFormData.defaultValue"
                  type="datetime-local"
                  label="Default Value"
                  variant="outlined"
                  :disabled="loading"
                  hint="Field için varsayılan tarih/saat (ISO 8601 format)"
                  persistent-hint
                  clearable
                ></v-text-field>
              </template>
              
              <!-- Object Default Value -->
              <template v-else-if="fieldFormData.fieldType === 'object'">
                <v-textarea
                  v-model="defaultValueJson"
                  label="Default Value (JSON)"
                  placeholder='{"url": "https://example.com", "alt": "Image"}'
                  variant="outlined"
                  rows="4"
                  :disabled="loading"
                  hint="JSON formatında varsayılan object değeri"
                  persistent-hint
                  clearable
                ></v-textarea>
              </template>
              
              <!-- Other types (relation, persons, personGroups, incremental) -->
              <template v-else>
                <v-alert type="info" variant="tonal" density="compact">
                  <strong>{{ fieldFormData.fieldType }}</strong> field type için default value desteklenmiyor.
                </v-alert>
              </template>
            </v-col>
            
            <!-- Validation Rules -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <h3 class="text-subtitle-1 mb-2">Validation Rules (Optional)</h3>
              
              <!-- Initialize validation object if not exists -->
              <template v-if="!fieldFormData.validation">
                <v-btn
                  size="small"
                  variant="outlined"
                  color="primary"
                  @click="fieldFormData.validation = {}"
                  :disabled="loading"
                >
                  <PlusIcon class="mr-1" size="16" />
                  Validation Rules Ekle
                </v-btn>
              </template>
              
              <template v-else>
                <v-btn
                  size="small"
                  variant="text"
                  color="error"
                  @click="fieldFormData.validation = undefined"
                  :disabled="loading"
                  class="mb-2"
                >
                  <XIcon class="mr-1" size="16" />
                  Validation Rules Kaldır
                </v-btn>
                
                <!-- Text Validation Rules -->
                <template v-if="fieldFormData.fieldType === 'text'">
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model.number="fieldFormData.validation.minLength"
                        type="number"
                        label="Min Length"
                        variant="outlined"
                        :disabled="loading"
                        hint="Minimum karakter uzunluğu"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model.number="fieldFormData.validation.maxLength"
                        type="number"
                        label="Max Length"
                        variant="outlined"
                        :disabled="loading"
                        hint="Maximum karakter uzunluğu"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                    <v-col cols="12">
                      <v-text-field
                        v-model="fieldFormData.validation.pattern"
                        label="Pattern (Regex)"
                        placeholder="Örn: ^[A-Z]+$"
                        variant="outlined"
                        :disabled="loading"
                        hint="Regex pattern (örn: ^[A-Z]+$)"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                  </v-row>
                </template>
                
                <!-- Number Validation Rules -->
                <template v-else-if="fieldFormData.fieldType === 'number'">
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model.number="fieldFormData.validation.min"
                        type="number"
                        label="Min Value"
                        variant="outlined"
                        :disabled="loading"
                        hint="Minimum değer"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model.number="fieldFormData.validation.max"
                        type="number"
                        label="Max Value"
                        variant="outlined"
                        :disabled="loading"
                        hint="Maximum değer"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                  </v-row>
                </template>
                
                <!-- DateTime Validation Rules -->
                <template v-else-if="fieldFormData.fieldType === 'datetime'">
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model="fieldFormData.validation.minDate"
                        type="date"
                        label="Min Date"
                        variant="outlined"
                        :disabled="loading"
                        hint="Minimum tarih"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model="fieldFormData.validation.maxDate"
                        type="date"
                        label="Max Date"
                        variant="outlined"
                        :disabled="loading"
                        hint="Maximum tarih"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                  </v-row>
                </template>
                
                <!-- Array Validation Rules -->
                <template v-else-if="fieldFormData.isArray">
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model.number="fieldFormData.validation.minItems"
                        type="number"
                        label="Min Items"
                        variant="outlined"
                        :disabled="loading"
                        hint="Minimum item sayısı"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        v-model.number="fieldFormData.validation.maxItems"
                        type="number"
                        label="Max Items"
                        variant="outlined"
                        :disabled="loading"
                        hint="Maximum item sayısı"
                        persistent-hint
                        clearable
                      ></v-text-field>
                    </v-col>
                  </v-row>
                </template>
                
                <!-- Custom Error Message -->
                <v-col cols="12">
                  <v-text-field
                    v-model="fieldFormData.validation.message"
                    label="Custom Error Message"
                    placeholder="Özel hata mesajı"
                    variant="outlined"
                    :disabled="loading"
                    hint="Validation hatası için özel mesaj (opsiyonel)"
                    persistent-hint
                    clearable
                  ></v-text-field>
                </v-col>
              </template>
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
          @click="closeFieldModal"
          :disabled="loading"
        >
          <XIcon class="mr-2" size="18" />
          İptal
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          @click="saveField"
          :loading="loading"
        >
          <CheckIcon class="mr-2" size="18" />
          Kaydet
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Query Modal -->
  <v-dialog v-model="showQueryModal" max-width="900px" persistent>
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
        <FileCodeIcon class="mr-2" size="20" />
        <span>{{ queryModalMode === 'edit' ? 'Query Düzenle' : 'Yeni Query Ekle' }}</span>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6">
        <!-- Query Form Error -->
        <v-alert
          v-if="queryFormError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="queryFormError = ''"
        >
          {{ queryFormError }}
        </v-alert>
        
        <v-form ref="queryFormRef">
          <v-row>
            <!-- Query Name -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="queryFormData.name"
                :rules="queryNameRules"
                label="Query Name *"
                placeholder="Örn: books_by_publication_date_range"
                variant="outlined"
                required
                :disabled="loading || queryModalMode === 'edit'"
                :readonly="queryModalMode === 'edit'"
                hint="Harf ile başlamalı, sadece harf, rakam ve _ içerebilir, benzersiz olmalıdır"
                persistent-hint
                prepend-inner-icon="mdi-code-tags"
              ></v-text-field>
              <v-alert
                v-if="queryModalMode === 'edit'"
                type="info"
                variant="tonal"
                density="compact"
                class="mt-2"
              >
                <strong>Not:</strong> Query adı düzenlenemez.
              </v-alert>
            </v-col>
            
            <!-- Query Description -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="queryFormData.description"
                label="Description"
                placeholder="Örn: Get books published between two dates"
                variant="outlined"
                :disabled="loading"
                hint="Query açıklaması (opsiyonel)"
                persistent-hint
              ></v-text-field>
            </v-col>
            
            <!-- Parameters (New format: QueryParameterDefinition[]) -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <div class="d-flex align-center justify-space-between mb-2">
                <h3 class="text-subtitle-1">Parameters (Optional)</h3>
                <v-btn
                  size="small"
                  variant="outlined"
                  color="primary"
                  @click="addQueryParameter"
                  :disabled="loading"
                >
                  <PlusIcon class="mr-1" size="16" />
                  Parameter Ekle
                </v-btn>
              </div>
              
              <v-alert type="info" variant="tonal" density="compact" class="mb-3">
                <div class="text-caption">
                  <div><strong>Not:</strong> Pipeline'da parametreler <code>:paramName</code> formatında kullanılır.</div>
                  <div>Örnek: <code>{"$match": {"date": ":startDate"}}</code></div>
                </div>
              </v-alert>
              
              <template v-if="queryParametersList.length === 0">
                <v-alert type="info" variant="tonal" density="compact">
                  Henüz parameter tanımlanmamış. "Parameter Ekle" butonuna tıklayarak ekleyebilirsiniz.
                </v-alert>
              </template>
              
              <template v-else>
                <v-card
                  v-for="(param, index) in queryParametersList"
                  :key="index"
                  variant="outlined"
                  class="mb-3"
                >
                  <v-card-text class="pa-3">
                    <v-row>
                      <v-col cols="12" md="4">
                        <v-text-field
                          v-model="param.name"
                          label="Parameter Name *"
                          placeholder="Örn: startDate"
                          variant="outlined"
                          density="compact"
                          :disabled="loading"
                          :rules="[
                            (v: string) => !!v || 'Parameter adı gereklidir',
                            (v: string) => {
                              if (!v) return true;
                              const pattern = /^[a-zA-Z][a-zA-Z0-9_]*$/;
                              return pattern.test(v) || 'Geçersiz format (harf ile başlamalı)';
                            },
                          ]"
                        ></v-text-field>
                      </v-col>
                      
                      <v-col cols="12" md="3">
                        <v-select
                          v-model="param.type"
                          :items="parameterTypeOptions"
                          label="Type"
                          variant="outlined"
                          density="compact"
                          :disabled="loading"
                        ></v-select>
                      </v-col>
                      
                      <v-col cols="12" md="3">
                        <v-checkbox
                          v-model="param.required"
                          label="Required"
                          color="primary"
                          density="compact"
                          :disabled="loading"
                          hide-details
                        ></v-checkbox>
                      </v-col>
                      
                      <v-col cols="12" md="2" class="d-flex align-center">
                        <v-btn
                          icon
                          size="small"
                          variant="text"
                          color="error"
                          @click="removeQueryParameter(index)"
                          :disabled="loading"
                        >
                          <TrashIcon size="18" />
                        </v-btn>
                      </v-col>
                    </v-row>
                  </v-card-text>
                </v-card>
              </template>
            </v-col>
            
            <!-- Pipeline JSON -->
            <v-col cols="12">
              <div class="d-flex justify-space-between align-center mb-2">
                <label class="text-subtitle-2">MongoDB Aggregation Pipeline (JSON) *</label>
                <v-btn
                  size="x-small"
                  variant="text"
                  color="primary"
                  @click="formatPipelineJson"
                  :disabled="loading || !pipelineJson"
                >
                  <v-icon size="small" class="mr-1">mdi-code-braces</v-icon>
                  Format
                </v-btn>
              </div>
              <v-alert type="info" variant="tonal" density="compact" class="mb-2">
                <strong>Pipeline Formatı:</strong> MongoDB aggregation pipeline array formatında olmalıdır.
                <br>Örnek: <code>[{"$match": {"publicationDate": {"$gte": ":startDate", "$lte": ":endDate"}}}]</code>
                <br><strong>Parametreler:</strong> Pipeline içinde <code>:paramName</code> formatında kullanılır (ör: :startDate, :endDate)
              </v-alert>
              <v-textarea
                v-model="pipelineJson"
                :rules="pipelineJsonRules"
                label="Pipeline (JSON Array)"
                placeholder='[{"$match": {...}}, {"$sort": {...}}]'
                variant="outlined"
                rows="12"
                required
                :disabled="loading"
                hint="MongoDB aggregation pipeline (JSON array formatı)"
                persistent-hint
                class="font-monospace"
              >
                <template v-slot:prepend-inner>
                  <v-icon size="small" color="info">mdi-database</v-icon>
                </template>
              </v-textarea>
              
              <!-- Pipeline Preview -->
              <v-expansion-panels variant="accordion" density="compact" class="mt-2">
                <v-expansion-panel>
                  <v-expansion-panel-title class="text-caption">
                    Pipeline Preview ({{ queryFormData.pipeline?.length || 0 }} stage)
                  </v-expansion-panel-title>
                  <v-expansion-panel-text>
                    <pre v-if="queryFormData.pipeline && queryFormData.pipeline.length > 0" class="text-caption bg-grey-lighten-4 pa-3 rounded font-monospace" style="max-height: 300px; overflow-y: auto;">{{ JSON.stringify(queryFormData.pipeline, null, 2) }}</pre>
                    <p v-else class="text-caption text-medium-emphasis">Henüz pipeline tanımlanmamış</p>
                  </v-expansion-panel-text>
                </v-expansion-panel>
              </v-expansion-panels>
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
          @click="closeQueryModal"
          :disabled="loading"
        >
          <XIcon class="mr-2" size="18" />
          İptal
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          @click="saveQuery"
          :loading="loading"
        >
          <CheckIcon class="mr-2" size="18" />
          Kaydet
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Validation Modal -->
  <v-dialog v-model="showValidationModal" max-width="900px" persistent>
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
        <ShieldIcon class="mr-2" size="20" />
        <span>{{ validationModalMode === 'edit' ? 'Validation Düzenle' : 'Yeni Validation Ekle' }}</span>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6">
        <!-- Validation Form Error -->
        <v-alert
          v-if="validationFormError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="validationFormError = ''"
        >
          {{ validationFormError }}
        </v-alert>
        
        <v-form ref="validationFormRef">
          <v-row>
            <!-- Validation Name -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model="validationFormData.name"
                :rules="validationNameRules"
                label="Validation Name *"
                placeholder="Örn: end_date_after_start_date"
                variant="outlined"
                required
                :disabled="loading || validationModalMode === 'edit'"
                :readonly="validationModalMode === 'edit'"
                hint="Harf ile başlamalı, sadece harf, rakam ve _ içerebilir, benzersiz olmalıdır"
                persistent-hint
              ></v-text-field>
              <v-alert
                v-if="validationModalMode === 'edit'"
                type="info"
                variant="tonal"
                density="compact"
                class="mt-2"
              >
                <strong>Not:</strong> Validation adı düzenlenemez.
              </v-alert>
            </v-col>
            
            <!-- Validation Type -->
            <v-col cols="12" md="6">
              <v-select
                v-model="validationFormData.type"
                :items="validationTypeOptions"
                item-title="title"
                item-value="value"
                label="Validation Type *"
                variant="outlined"
                required
                :disabled="loading"
                hint="Expression veya HTTP validation seçin"
                persistent-hint
              ></v-select>
            </v-col>
            
            <!-- Description -->
            <v-col cols="12">
              <v-textarea
                v-model="validationFormData.description"
                label="Description"
                placeholder="Örn: Ensure end date is after start date"
                variant="outlined"
                rows="2"
                :disabled="loading"
                hint="Validation açıklaması (opsiyonel)"
                persistent-hint
              ></v-textarea>
            </v-col>
            
            <!-- Expression (for expression type) -->
            <template v-if="validationFormData.type === 'expression'">
              <v-col cols="12">
                <v-divider class="my-2"></v-divider>
                <v-textarea
                  v-model="validationFormData.expression"
                  :rules="expressionRules"
                  label="Expression *"
                  placeholder="Örn: endDate > startDate, price / pageCount <= 10"
                  variant="outlined"
                  rows="4"
                  required
                  :disabled="loading"
                  hint="Field names doğrudan kullanılabilir. Örnek: endDate > startDate"
                  persistent-hint
                >
                  <template v-slot:append-inner>
                    <v-tooltip location="top">
                      <template v-slot:activator="{ props }">
                        <v-icon v-bind="props" size="small" color="info">mdi-information</v-icon>
                      </template>
                      <div class="text-caption">
                        <div><strong>Örnekler:</strong></div>
                        <div><code>endDate > startDate</code></div>
                        <div><code>price / pageCount <= 10</code></div>
                        <div><code>quantity >= 0 && quantity <= 100</code></div>
                      </div>
                    </v-tooltip>
                  </template>
                </v-textarea>
              </v-col>
            </template>
            
            <!-- HTTP Validation Fields (for http type) -->
            <template v-if="validationFormData.type === 'http'">
              <v-col cols="12">
                <v-divider class="my-2"></v-divider>
              </v-col>
              
              <v-col cols="12" md="8">
                <v-text-field
                  v-model="validationFormData.url"
                  :rules="urlRules"
                  label="URL *"
                  placeholder="Örn: https://api.example.com/validate"
                  variant="outlined"
                  required
                  :disabled="loading"
                  hint="Validation endpoint URL'i"
                  persistent-hint
                ></v-text-field>
              </v-col>
              
              <v-col cols="12" md="4">
                <v-select
                  v-model="validationFormData.method"
                  :items="httpMethodOptions"
                  item-title="title"
                  item-value="value"
                  label="Method"
                  variant="outlined"
                  :disabled="loading"
                  hint="HTTP method (default: POST)"
                  persistent-hint
                ></v-select>
              </v-col>
              
              <v-col cols="12">
                <v-select
                  v-model="validationFormData.fields"
                  :items="availableFieldsForValidation"
                  item-title="title"
                  item-value="value"
                  label="Fields (Optional)"
                  variant="outlined"
                  multiple
                  chips
                  :disabled="loading || availableFieldsForValidation.length === 0"
                  hint="Belirtilmezse tüm field'lar gönderilir"
                  persistent-hint
                >
                  <template v-slot:no-data>
                    <v-alert type="info" variant="tonal" density="compact">
                      Henüz field tanımlanmamış. Önce Step 2'de field'ları tanımlayın.
                    </v-alert>
                  </template>
                </v-select>
              </v-col>
              
              <v-col cols="12">
                <v-alert type="info" variant="tonal" density="compact">
                  <div class="text-caption">
                    <div><strong>Response Format:</strong></div>
                    <div><code>{"isValid": boolean, "errorMessage": string}</code></div>
                    <div class="mt-1"><strong>Not:</strong> Authorization header otomatik gönderilir.</div>
                  </div>
                </v-alert>
              </v-col>
            </template>
            
            <!-- When -->
            <v-col cols="12" md="6">
              <v-select
                v-model="validationFormData.when"
                :items="validationWhenOptions"
                item-title="title"
                item-value="value"
                label="When *"
                variant="outlined"
                required
                :disabled="loading"
                hint="Ne zaman çalıştırılacak?"
                persistent-hint
              ></v-select>
            </v-col>
            
            <!-- Order -->
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="validationFormData.order"
                type="number"
                label="Execution Order"
                placeholder="0"
                variant="outlined"
                :disabled="loading"
                hint="Düşük sayı = daha önce çalıştırılır (default: 0)"
                persistent-hint
                clearable
              ></v-text-field>
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
          @click="closeValidationModal"
          :disabled="loading"
        >
          <XIcon class="mr-2" size="18" />
          İptal
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          @click="saveValidation"
          :loading="loading"
        >
          <CheckIcon class="mr-2" size="18" />
          Kaydet
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>

  <!-- Index Modal -->
  <v-dialog v-model="showIndexModal" max-width="800px" persistent>
    <v-card>
      <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
        <KeyIcon class="mr-2" size="20" />
        <span>{{ indexModalMode === 'edit' ? 'Index Düzenle' : 'Yeni Index Ekle' }}</span>
      </v-card-title>
      
      <v-divider></v-divider>
      
      <v-card-text class="pa-6">
        <!-- Index Form Error -->
        <v-alert
          v-if="indexFormError"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="indexFormError = ''"
        >
          {{ indexFormError }}
        </v-alert>
        
        <v-form ref="indexFormRef">
          <v-row>
            <!-- Index Name -->
            <v-col cols="12">
              <v-text-field
                v-model="indexFormData.name"
                :rules="indexNameRules"
                label="Index Name *"
                placeholder="Örn: idx_name, idx_title_bookCode"
                variant="outlined"
                required
                :disabled="loading || indexModalMode === 'edit'"
                :readonly="indexModalMode === 'edit'"
                hint="Harf ile başlamalı, sadece harf, rakam ve _ içerebilir, benzersiz olmalıdır"
                persistent-hint
                prepend-inner-icon="mdi-key"
              ></v-text-field>
              <v-alert
                v-if="indexModalMode === 'edit'"
                type="info"
                variant="tonal"
                density="compact"
                class="mt-2"
              >
                <strong>Not:</strong> Index adı düzenlenemez.
              </v-alert>
            </v-col>
            
            <!-- Index Fields -->
            <v-col cols="12">
              <div class="d-flex justify-space-between align-center mb-2">
                <label class="text-subtitle-2">Fields *</label>
                <v-btn
                  size="x-small"
                  variant="text"
                  color="primary"
                  @click="addIndexFieldRow"
                  :disabled="loading || availableFieldNames.length === 0"
                >
                  <PlusIcon size="small" class="mr-1" />
                  Field Ekle
                </v-btn>
              </div>
              
              <v-alert
                v-if="availableFieldNames.length === 0"
                type="warning"
                variant="tonal"
                density="compact"
                class="mb-2"
              >
                <strong>Uyarı:</strong> Index tanımlamak için önce Step 2'de en az bir field tanımlamalısınız.
              </v-alert>
              
              <v-card
                v-for="(fieldRow, fieldIdx) in indexFieldsList"
                :key="fieldIdx"
                variant="outlined"
                class="mb-3 pa-3"
              >
                <div class="d-flex ga-2 align-center">
                  <v-select
                    v-model="fieldRow.fieldName"
                    :items="availableFieldNames"
                    label="Field"
                    placeholder="Field seçin"
                    variant="outlined"
                    density="compact"
                    required
                    :disabled="loading"
                    hide-details
                    class="flex-grow-1"
                  ></v-select>
                  
                  <v-select
                    v-model="fieldRow.order"
                    :items="[
                      { title: 'Ascending (1)', value: 1 },
                      { title: 'Descending (-1)', value: -1 }
                    ]"
                    item-title="title"
                    item-value="value"
                    label="Order"
                    variant="outlined"
                    density="compact"
                    required
                    :disabled="loading"
                    hide-details
                    style="max-width: 180px;"
                  ></v-select>
                  
                  <v-btn
                    icon
                    size="small"
                    variant="text"
                    color="error"
                    @click="removeIndexFieldRow(fieldIdx)"
                    :disabled="loading || indexFieldsList.length <= 1"
                  >
                    <XIcon size="18" />
                    <v-tooltip activator="parent" location="top">Field'ı Kaldır</v-tooltip>
                  </v-btn>
                </div>
              </v-card>
              
              <v-alert
                v-if="indexFieldsList.length > 1"
                type="info"
                variant="tonal"
                density="compact"
                class="mt-2"
              >
                <strong>Composite Index:</strong> Birden fazla field ekleyerek composite index oluşturabilirsiniz. 
                Field sırası önemlidir (MongoDB index prefix kuralı).
              </v-alert>
            </v-col>
            
            <!-- Unique Index -->
            <v-col cols="12">
              <v-divider class="my-2"></v-divider>
              <v-checkbox
                v-model="indexFormData.unique"
                label="Unique Index"
                color="success"
                :disabled="loading"
                hide-details
              >
                <template v-slot:label>
                  <div>
                    <span class="text-body-2">Unique Index</span>
                    <v-alert
                      type="info"
                      variant="tonal"
                      density="compact"
                      class="mt-2"
                    >
                      <strong>Not:</strong> Unique index'ler duplicate değerlere izin vermez. 
                      Aynı değere sahip birden fazla kayıt olamaz.
                    </v-alert>
                  </div>
                </template>
              </v-checkbox>
            </v-col>
            
            <!-- Index Preview -->
            <v-col cols="12" v-if="indexFormData.name && indexFieldsList.some(f => f.fieldName)">
              <v-expansion-panels variant="accordion" density="compact" class="mt-2">
                <v-expansion-panel>
                  <v-expansion-panel-title class="text-caption">
                    Index Preview
                  </v-expansion-panel-title>
                  <v-expansion-panel-text>
                    <div class="text-caption bg-grey-lighten-4 pa-3 rounded font-monospace">
                      <div><strong>Name:</strong> {{ indexFormData.name }}</div>
                      <div><strong>Fields:</strong> {{ getFieldsDisplay(
                        Object.fromEntries(
                          indexFieldsList
                            .filter(f => f.fieldName)
                            .map(f => [f.fieldName, f.order])
                        )
                      ) }}</div>
                      <div><strong>Unique:</strong> {{ indexFormData.unique ? 'true' : 'false' }}</div>
                    </div>
                  </v-expansion-panel-text>
                </v-expansion-panel>
              </v-expansion-panels>
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
          @click="closeIndexModal"
          :disabled="loading"
        >
          <XIcon class="mr-2" size="18" />
          İptal
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          @click="saveIndex"
          :loading="loading"
          :disabled="!indexFormData.name.trim() || indexFieldsList.every(f => !f.fieldName)"
        >
          <CheckIcon class="mr-2" size="18" />
          Kaydet
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
