<script setup lang="ts">
import { ref, onMounted, computed, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useAutomatedFormsStore } from '@/stores/apps/automatedForms';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useAuthStore } from '@/stores/auth';
import { FileCodeIcon, CheckIcon, XIcon } from 'vue-tabler-icons';

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
  title: isEditMode.value ? 'Form Düzenle' : 'Yeni Form',
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
const selectedDataset = computed(() => {
  if (!formData.value.datasetName) return null;
  // Check both currentDataset and datasets list
  if (datasetStore.currentDataset && datasetStore.currentDataset.name === formData.value.datasetName) {
    return datasetStore.currentDataset;
  }
  return datasetStore.getDatasetByName(formData.value.datasetName);
});

const availableFields = computed(() => {
  if (!selectedDataset.value || !selectedDataset.value.fields) return [];
  return selectedDataset.value.fields.map(f => ({
    title: f.title || f.name,
    value: f.name,
  }));
});

// List column configs (computed from dataset fields)
const listColumnConfigs = computed(() => {
  if (!selectedDataset.value || !selectedDataset.value.fields) return [];
  
  const fields = selectedDataset.value.fields;
  const existingConfigs = formData.value.listConfig.columns || [];
  
  // Merge existing configs with new fields
  return fields.map((field, index) => {
    const existingConfig = existingConfigs.find(c => c.fieldName === field.name);
    if (existingConfig) {
      return existingConfig;
    }
    // Default config for new field
    return {
      fieldName: field.name,
      visible: true,
      order: index,
      sortable: true,
      filterable: true,
    };
  }).sort((a, b) => a.order - b.order);
});

// Validation rules
const formNameRules = [
  (v: string) => !!v || 'Form adı gereklidir',
  (v: string) => (v && v.length >= 3) || 'Form adı en az 3 karakter olmalıdır',
  (v: string) => (v && v.length <= 100) || 'Form adı en fazla 100 karakter olabilir',
];

const formCodeRules = [
  (v: string) => !!v || 'Form kodu gereklidir',
  (v: string) => (v && v.length >= 3) || 'Form kodu en az 3 karakter olmalıdır',
  (v: string) => (v && v.length <= 50) || 'Form kodu en fazla 50 karakter olabilir',
  (v: string) => /^[a-zA-Z0-9_-]+$/.test(v) || 'Form kodu sadece harf, rakam, alt çizgi ve tire içerebilir',
];

const datasetNameRules = [
  (v: string) => !!v || 'Dataset seçimi gereklidir',
];

// Form state
const formRef = ref();
const loading = ref(false);
const errorMessage = ref('');
const successMessage = ref('');

// Watch dataset selection to update available fields and column configs
watch(() => formData.value.datasetName, async (newDatasetName, oldDatasetName) => {
  if (!newDatasetName) {
    return;
  }
  
  // Reset configs when dataset changes
  if (newDatasetName !== oldDatasetName) {
    formData.value.listConfig.columns = [];
    formData.value.formConfig.visibleFields = [];
    formData.value.formConfig.readonlyFields = [];
    formData.value.formConfig.fieldOrder = [];
  }
  
  // Check if dataset is already loaded with fields
  const existingDataset = datasetStore.getDatasetByName(newDatasetName);
  if (existingDataset && existingDataset.fields && existingDataset.fields.length > 0) {
    // Initialize column configs if not already set
    if (formData.value.listConfig.columns.length === 0) {
      initializeColumnConfigs();
    }
    return; // Dataset already loaded with fields
  }
  
  // Load dataset with fields
  try {
    await loadDataset(newDatasetName);
    // Initialize column configs after loading
    if (formData.value.listConfig.columns.length === 0) {
      initializeColumnConfigs();
    }
  } catch (error) {
    console.error('Dataset yüklenirken hata:', error);
  }
});

// Initialize column configs from dataset fields
const initializeColumnConfigs = () => {
  if (!selectedDataset.value || !selectedDataset.value.fields) return;
  
  const fields = selectedDataset.value.fields;
  formData.value.listConfig.columns = fields.map((field, index) => ({
    fieldName: field.name,
    visible: true,
    order: index,
    sortable: true,
    filterable: true,
  }));
};

// Update column order (not used directly, but kept for future drag-drop functionality)
const updateColumnOrder = (newOrder: Array<{ fieldName: string; visible: boolean; order: number; sortable: boolean; filterable: boolean; width?: number }>) => {
  formData.value.listConfig.columns = newOrder.map((col, index) => ({
    ...col,
    order: index,
  }));
};

// Load dataset
const loadDataset = async (datasetName: string) => {
  try {
    await datasetStore.fetchDatasetByName(datasetName);
  } catch (error) {
    // Error handled by store
    console.error('Dataset yüklenirken hata:', error);
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
      if (formData.value.datasetName) {
        await loadDataset(formData.value.datasetName);
        // Merge existing column configs with dataset fields
        if (formData.value.listConfig.columns && formData.value.listConfig.columns.length > 0) {
          // Column configs already exist, keep them
        } else {
          // Initialize column configs
          initializeColumnConfigs();
        }
        
        // Load all relation datasets for relation field configs
        await loadAllRelationDatasets();
        
        // Initialize relation field configs if not exists
        if (selectedDataset.value && selectedDataset.value.fields) {
          selectedDataset.value.fields.forEach(field => {
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
      }
    } else {
      errorMessage.value = 'Form bulunamadı';
      setTimeout(() => {
        router.push('/apps/automated-forms');
      }, 2000);
    }
  } catch (error: any) {
    errorMessage.value = error.message || 'Form yüklenirken bir hata oluştu';
    setTimeout(() => {
      router.push('/apps/automated-forms');
    }, 2000);
  } finally {
    loading.value = false;
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
        throw new Error('Form bulunamadı');
      }
      const dataId = existingForm.__dataId || existingForm.dataId;
      
      // Update existing form
      await formStore.updateForm(dataId, formDataToSubmit);
      successMessage.value = 'Form başarıyla güncellendi!';
    } else {
      // Create new form
      await formStore.createForm(formDataToSubmit);
      successMessage.value = 'Form başarıyla oluşturuldu!';
    }
    
    // Redirect to list after success
    setTimeout(() => {
      router.push('/apps/automated-forms');
    }, 1500);
  } catch (error: any) {
    errorMessage.value = error.message || 'Form kaydedilirken bir hata oluştu';
    
    // Handle duplicate form code error
    if (error.message && (error.message.includes('zaten mevcut') || error.message.includes('duplicate'))) {
      errorMessage.value = 'Bu form kodu zaten kullanılıyor. Lütfen farklı bir kod seçin.';
    }
  } finally {
    loading.value = false;
  }
};

// Handle cancel
const handleCancel = () => {
  router.push('/apps/automated-forms');
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
    
    // If not loaded, try to load it (async, but we'll show default for now)
    datasetStore.fetchDatasetByName(datasetName).catch(err => {
      console.error('Error loading relation dataset:', err);
    });
    
    return [{ title: '__dataId (ID)', value: '__dataId' }];
  });
};

// Relation dataset fields cache (to load all relation datasets at once)
const relationDatasetFieldsCache = ref<{ [datasetName: string]: any[] }>({});

// Load all relation datasets
const loadAllRelationDatasets = async () => {
  if (!selectedDataset.value || !relationFields.value.length) return;
  
  const datasetsToLoad = new Set<string>();
  relationFields.value.forEach(field => {
    if (field.relationDataset) {
      datasetsToLoad.add(field.relationDataset);
    }
  });
  
  for (const datasetName of datasetsToLoad) {
    if (!relationDatasetFieldsCache.value[datasetName]) {
      try {
        await datasetStore.fetchDatasetByName(datasetName);
        const relationDataset = datasetStore.getDatasetByName(datasetName);
        if (relationDataset && relationDataset.fields) {
          relationDatasetFieldsCache.value[datasetName] = [
            { title: '__dataId (ID)', value: '__dataId' },
            ...relationDataset.fields.map(f => ({
              title: `${f.title || f.name} (${f.name})`,
              value: f.name,
            })),
          ];
        } else {
          relationDatasetFieldsCache.value[datasetName] = [{ title: '__dataId (ID)', value: '__dataId' }];
        }
      } catch (error) {
        console.error(`Error loading relation dataset ${datasetName}:`, error);
        relationDatasetFieldsCache.value[datasetName] = [{ title: '__dataId (ID)', value: '__dataId' }];
      }
    }
  }
};

// Watch relation fields and load datasets
watch(() => relationFields.value, async () => {
  await loadAllRelationDatasets();
}, { immediate: true });

// Initialize relation field configs when dataset changes
watch(() => [selectedDataset.value, relationFields.value], () => {
  if (!selectedDataset.value || !relationFields.value.length) return;
  
  // Initialize relation field configs if not exists
  relationFields.value.forEach(field => {
    if (!formData.value.formConfig.relationFieldConfig[field.name]) {
      formData.value.formConfig.relationFieldConfig[field.name] = {
        idField: '__dataId',
        displayField: field.relationField || '__dataId',
      };
    }
  });
}, { immediate: true });

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

// List column config table headers
const listColumnConfigHeaders = [
  { title: 'Field Adı', key: 'fieldName', sortable: false, width: '200px' },
  { title: 'Görünür', key: 'visible', sortable: false, width: '100px', align: 'center' },
  { title: 'Sıralama', key: 'sortable', sortable: false, width: '120px', align: 'center' },
  { title: 'Filtreleme', key: 'filterable', sortable: false, width: '120px', align: 'center' },
  { title: 'Sıra', key: 'order', sortable: false, width: '80px', align: 'center' },
];

// Field layout table headers
const fieldLayoutHeaders = [
  { title: 'Field Adı', key: 'fieldName', sortable: false, width: '200px' },
  { title: 'Sütun Genişliği', key: 'columnSpan', sortable: false, width: '150px' },
  { title: 'Grup', key: 'group', sortable: false },
];
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
        <strong>Uyarı:</strong> Bu sayfaya erişim için manager veya admin yetkisi gereklidir.
      </v-alert>
      
      <!-- Loading State (only for edit mode when fetching data) -->
      <div v-if="loading && isEditMode" class="text-center py-12">
        <v-progress-circular
          indeterminate
          color="primary"
          size="64"
        ></v-progress-circular>
        <p class="text-subtitle-1 mt-4 text-medium-emphasis">Form yükleniyor...</p>
      </div>
      
      <!-- Form (always visible in create mode, visible in edit mode after data is loaded) -->
      <v-form
        v-else
        ref="formRef"
        @submit.prevent="handleSubmit"
      >
        <!-- Tabs -->
        <v-tabs v-model="currentTab" bg-color="primary" class="mb-4">
          <v-tab value="basic">Temel Bilgiler</v-tab>
          <v-tab value="list" :disabled="!selectedDataset">Liste Ayarları</v-tab>
          <v-tab value="form" :disabled="!selectedDataset">Form Ayarları</v-tab>
        </v-tabs>
        
        <v-divider></v-divider>
        
        <v-card-text class="bg-grey100 mt-4 rounded-md">
          <v-window v-model="currentTab">
            <!-- Tab 1: Temel Bilgiler -->
            <v-window-item value="basic">
              <v-row>
                <v-col cols="12">
                  <h3 class="text-h6 mb-4">Temel Bilgiler</h3>
                </v-col>
                
                <!-- Form Name -->
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="formData.formName"
                    :rules="formNameRules"
                    label="Form Adı *"
                    placeholder="Örn: Books Management"
                    variant="outlined"
                    required
                    :disabled="loading || isEditMode"
                    hint="3-100 karakter arasında"
                    persistent-hint
                    prepend-inner-icon="mdi-text"
                  ></v-text-field>
                </v-col>
                
                <!-- Form Code -->
                <v-col cols="12" md="6">
                  <v-text-field
                    v-model="formData.formCode"
                    :rules="formCodeRules"
                    label="Form Kodu *"
                    placeholder="Örn: books-form"
                    variant="outlined"
                    required
                    :disabled="loading || isEditMode"
                    hint="3-50 karakter, sadece harf, rakam, alt çizgi ve tire"
                    persistent-hint
                    prepend-inner-icon="mdi-code-tags"
                  ></v-text-field>
                </v-col>
                
                <!-- Description -->
                <v-col cols="12">
                  <v-textarea
                    v-model="formData.description"
                    label="Açıklama"
                    placeholder="Form açıklaması (opsiyonel)"
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
                    label="Dataset Seçimi *"
                    placeholder="Bir dataset seçin"
                    variant="outlined"
                    required
                    :disabled="loading"
                    hint="Form için kullanılacak dataset'i seçin"
                    persistent-hint
                    prepend-inner-icon="mdi-database"
                  ></v-select>
                </v-col>
                
                <!-- Is Active -->
                <v-col cols="12" md="6" class="d-flex align-center">
                  <v-switch
                    v-model="formData.isActive"
                    label="Aktif"
                    color="primary"
                    :disabled="loading"
                    hide-details
                  ></v-switch>
                </v-col>
              </v-row>
            </v-window-item>
            
            <!-- Tab 2: Liste Ayarları -->
            <v-window-item value="list">
              <v-row v-if="selectedDataset">
                <v-col cols="12">
                  <h3 class="text-h6 mb-4">Liste Ayarları</h3>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    Her field için liste görünümünde görünürlük, sıralama, filtreleme ve sıra ayarlarını yapabilirsiniz.
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
                  >
                    <!-- Field Name Column -->
                    <template v-slot:item.fieldName="{ item }">
                      <div class="d-flex align-center ga-2">
                        <span class="font-weight-medium">{{ selectedDataset?.fields?.find(f => f.name === item.fieldName)?.title || item.fieldName }}</span>
                        <v-chip size="x-small" variant="tonal" color="primary">
                          {{ item.fieldName }}
                        </v-chip>
                      </div>
                    </template>
                    
                    <!-- Visible Column -->
                    <template v-slot:item.visible="{ item }">
                      <v-switch
                        v-model="item.visible"
                        color="primary"
                        hide-details
                        density="compact"
                        :disabled="loading"
                      ></v-switch>
                    </template>
                    
                    <!-- Sortable Column -->
                    <template v-slot:item.sortable="{ item }">
                      <v-switch
                        v-model="item.sortable"
                        color="primary"
                        hide-details
                        density="compact"
                        :disabled="loading"
                      ></v-switch>
                    </template>
                    
                    <!-- Filterable Column -->
                    <template v-slot:item.filterable="{ item }">
                      <v-switch
                        v-model="item.filterable"
                        color="primary"
                        hide-details
                        density="compact"
                        :disabled="loading"
                      ></v-switch>
                    </template>
                    
                    <!-- Order Column -->
                    <template v-slot:item.order="{ item, index }">
                      <v-text-field
                        v-model.number="item.order"
                        type="number"
                        variant="outlined"
                        density="compact"
                        hide-details
                        :disabled="loading"
                        style="max-width: 80px;"
                      ></v-text-field>
                    </template>
                  </v-data-table>
                </v-col>
                
                <!-- Default Sort By -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.listConfig.defaultSortBy"
                    :items="availableFields"
                    label="Varsayılan Sıralama (Field)"
                    placeholder="Field seçin"
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
                      { title: 'Artan', value: 'asc' },
                      { title: 'Azalan', value: 'desc' }
                    ]"
                    label="Sıralama Yönü"
                    variant="outlined"
                    :disabled="loading"
                    prepend-inner-icon="mdi-sort-variant"
                  ></v-select>
                </v-col>
                
                <!-- Enable Search -->
                <v-col cols="12" md="6" class="d-flex align-center">
                  <v-switch
                    v-model="formData.listConfig.enableSearch"
                    label="Arama Kullan"
                    color="primary"
                    :disabled="loading"
                    hint="Liste görünümünde arama özelliği aktif olsun"
                    persistent-hint
                  ></v-switch>
                </v-col>
              </v-row>
              <v-row v-else>
                <v-col cols="12" class="text-center py-12">
                  <p class="text-body-1 text-medium-emphasis">Liste ayarlarını yapabilmek için önce bir dataset seçmelisiniz.</p>
                </v-col>
              </v-row>
            </v-window-item>
            
            <!-- Tab 3: Form Ayarları -->
            <v-window-item value="form">
              <v-row v-if="selectedDataset">
                <v-col cols="12">
                  <h3 class="text-h6 mb-4">Form Ayarları</h3>
                </v-col>
                
                <!-- Visible Fields -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.formConfig.visibleFields"
                    :items="availableFields"
                    label="Gösterilecek Field'lar"
                    placeholder="Field'ları seçin"
                    variant="outlined"
                    multiple
                    chips
                    :disabled="loading"
                    hint="Form'da gösterilecek field'lar (boş ise tümü gösterilir)"
                    persistent-hint
                    prepend-inner-icon="mdi-eye"
                  ></v-select>
                </v-col>
                
                <!-- Readonly Fields -->
                <v-col cols="12" md="6">
                  <v-select
                    v-model="formData.formConfig.readonlyFields"
                    :items="availableFields"
                    label="Read-only Field'lar"
                    placeholder="Field'ları seçin"
                    variant="outlined"
                    multiple
                    chips
                    :disabled="loading"
                    hint="Read-only field'lar (incremental field'lar otomatik read-only)"
                    persistent-hint
                    prepend-inner-icon="mdi-lock"
                  ></v-select>
                </v-col>
                
                <!-- Relation Field Configurations -->
                <v-col cols="12">
                  <v-divider class="my-4"></v-divider>
                  <h4 class="text-subtitle-1 mb-4">Relation Field Ayarları</h4>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    Relation field'lar için dropdown'da gösterilecek field'ı ve ID olarak kullanılacak field'ı seçebilirsiniz.
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
                            label="Display Field (Gösterilecek)"
                            placeholder="Field seçin"
                            variant="outlined"
                            density="compact"
                            :disabled="loading"
                            hint="Dropdown'da gösterilecek field"
                            persistent-hint
                            required
                          ></v-select>
                        </v-col>
                        <v-col cols="12" md="6">
                          <v-select
                            v-model="formData.formConfig.relationFieldConfig[field.name].idField"
                            :items="relationDatasetFieldsCache[field.relationDataset] || [{ title: '__dataId (ID)', value: '__dataId' }]"
                            label="ID Field (Değer)"
                            placeholder="Field seçin (varsayılan: __dataId)"
                            variant="outlined"
                            density="compact"
                            :disabled="loading"
                            hint="Değer olarak kullanılacak field (genellikle __dataId)"
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
                    Bu dataset'te relation type field bulunmuyor.
                  </v-alert>
                </v-col>
                
                <!-- Field Layout Settings -->
                <v-col cols="12">
                  <v-divider class="my-4"></v-divider>
                  <h4 class="text-subtitle-1 mb-4">Field Layout Ayarları</h4>
                  <p class="text-body-2 text-medium-emphasis mb-4">
                    Her field için form görünümünde kaç sütun kaplayacağını ve hangi gruba ait olduğunu ayarlayabilirsiniz.
                  </p>
                  
                  <v-data-table
                    :headers="fieldLayoutHeaders"
                    :items="fieldLayoutConfigs"
                    item-key="fieldName"
                    class="border rounded-md"
                    hide-default-footer
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
                        placeholder="Grup adı (opsiyonel)"
                        :disabled="loading"
                        clearable
                      ></v-text-field>
                    </template>
                  </v-data-table>
                </v-col>
              </v-row>
              <v-row v-else>
                <v-col cols="12" class="text-center py-12">
                  <p class="text-body-1 text-medium-emphasis">Form ayarlarını yapabilmek için önce bir dataset seçmelisiniz.</p>
                </v-col>
              </v-row>
            </v-window-item>
          </v-window>
        </v-card-text>
        
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
              İptal
            </v-btn>
            
            <v-btn
              color="primary"
              variant="flat"
              type="submit"
              :loading="loading"
              :disabled="!formData.formName.trim() || !formData.formCode.trim() || !formData.datasetName"
            >
              <CheckIcon class="mr-2" size="18" />
              {{ isEditMode ? 'Güncelle' : 'Oluştur' }}
            </v-btn>
          </v-col>
        </v-row>
      </v-form>
    </v-card-text>
  </v-card>
</template>
