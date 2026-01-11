<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useDatasetStore, type Dataset } from '@/stores/apps/dataset';
import { useDatasetCategoryStore } from '@/stores/apps/datasetCategory';
import { useAuthStore } from '@/stores/auth';
import { 
  DatabaseIcon, 
  ArrowLeftIcon, 
  EditIcon, 
  TrashIcon, 
  TagIcon, 
  FileCodeIcon, 
  KeyIcon,
  RefreshIcon,
  CheckIcon,
  XIcon,
  DownloadIcon
} from 'vue-tabler-icons';
import { exportObjectToJSON } from '@/utils/exportUtils';

definePageMeta({
  layout: 'default',
});

const route = useRoute();
const router = useRouter();
const datasetStore = useDatasetStore();
const categoryStore = useDatasetCategoryStore();
const authStore = useAuthStore();

const datasetName = computed(() => decodeURIComponent(route.params.name as string));

const page = computed(() => ({
  title: `Dataset: ${datasetName.value}`,
}));

const breadcrumbs = computed(() => [
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Datasets',
    disabled: false,
    href: '/apps/datasets',
  },
  {
    text: datasetName.value,
    disabled: true,
    href: '#',
  },
]);

// State
const loading = ref(false);
const errorMessage = ref('');
const dataset = ref<Dataset | null>(null);
const showDeleteDialog = ref(false);

// Load categories for display
const loadCategories = async () => {
  try {
    if (categoryStore.categories.length === 0) {
      await categoryStore.fetchCategories({ pageNumber: 1, pageSize: 1000 });
    }
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

// Get field type display name
const getFieldTypeDisplay = (type: string): string => {
  const typeMap: { [key: string]: string } = {
    text: 'Text',
    number: 'Number',
    bool: 'Boolean',
    datetime: 'DateTime',
    object: 'Object',
    relation: 'Relation',
    persons: 'Persons',
    personGroups: 'Person Groups',
    incremental: 'Incremental',
  };
  return typeMap[type] || type;
};

// Get fields display for index
const getFieldsDisplay = (fields: { [key: string]: 1 | -1 }): string => {
  if (!fields || Object.keys(fields).length === 0) return '-';
  return Object.entries(fields)
    .map(([fieldName, order]) => `${fieldName} (${order === 1 ? 'asc' : 'desc'})`)
    .join(', ');
};

// Get parameters display for query
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

// Format date
const formatDate = (date: string | Date | undefined): string => {
  if (!date) return '-';
  try {
    const d = typeof date === 'string' ? new Date(date) : date;
    return d.toLocaleString('tr-TR', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return String(date);
  }
};

// Export dataset to JSON
const handleExportJSON = () => {
  if (!dataset.value) {
    return;
  }
  
  // Clean dataset name for filename (remove @ prefix if exists)
  const cleanName = dataset.value.name.startsWith('@') 
    ? dataset.value.name.substring(1) 
    : dataset.value.name;
  
  exportObjectToJSON(dataset.value, `dataset_${cleanName}`);
};

// Load dataset
const loadDataset = async () => {
  if (!datasetName.value) {
    errorMessage.value = 'Dataset adı bulunamadı';
    return;
  }

  loading.value = true;
  errorMessage.value = '';

  try {
    const fetchedDataset = await datasetStore.fetchDatasetByName(datasetName.value);
    if (fetchedDataset) {
      dataset.value = fetchedDataset;
    } else {
      errorMessage.value = 'Dataset bulunamadı';
    }
  } catch (error: any) {
    errorMessage.value = error.message || 'Dataset yüklenirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};

// Navigate to edit page
const editDataset = () => {
  if (dataset.value?.name) {
    router.push(`/apps/datasets/edit/${encodeURIComponent(dataset.value.name)}`);
  }
};

// Delete dataset
const deleteDataset = async () => {
  if (!dataset.value?.name) return;

  loading.value = true;
  errorMessage.value = '';

  try {
    await datasetStore.deleteDataset(dataset.value.name);
    showDeleteDialog.value = false;
    
    // Redirect to list after successful deletion
    router.push('/apps/datasets?refresh=true');
  } catch (error: any) {
    errorMessage.value = error.message || 'Dataset silinirken bir hata oluştu';
  } finally {
    loading.value = false;
  }
};

// Back to list
const backToList = () => {
  router.push('/apps/datasets');
};

// Load data on mount
onMounted(async () => {
  await loadCategories();
  await loadDataset();
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs"></BaseBreadcrumb>
    
    <v-row>
      <v-col cols="12">
        <!-- Loading State -->
        <v-card v-if="loading && !dataset" class="pa-8 text-center">
          <v-progress-circular indeterminate color="primary" size="64" class="mb-4"></v-progress-circular>
          <p class="text-body-1 text-medium-emphasis">Dataset yükleniyor...</p>
        </v-card>

        <!-- Error State -->
        <v-alert
          v-if="errorMessage && !loading"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="errorMessage = ''"
        >
          {{ errorMessage }}
        </v-alert>

        <!-- Dataset Details -->
        <template v-if="dataset && !loading">
          <!-- Header Actions -->
          <v-card class="mb-4" variant="outlined">
            <v-card-text class="pa-4">
              <div class="d-flex justify-space-between align-center flex-wrap ga-3">
                <div class="d-flex align-center ga-3">
                  <v-btn
                    color="default"
                    variant="outlined"
                    size="small"
                    @click="backToList"
                  >
                    <ArrowLeftIcon class="mr-2" size="18" />
                    Listeye Dön
                  </v-btn>
                  
                  <v-btn
                    color="primary"
                    variant="outlined"
                    size="small"
                    @click="loadDataset"
                    :loading="loading"
                  >
                    <RefreshIcon class="mr-2" size="18" />
                    Yenile
                  </v-btn>
                </div>
                
                <div class="d-flex ga-2">
                  <v-btn
                    color="success"
                    variant="outlined"
                    size="small"
                    @click="handleExportJSON"
                    :disabled="!dataset"
                  >
                    <DownloadIcon class="mr-2" size="18" />
                    Export JSON
                  </v-btn>
                  
                  <v-btn
                    color="info"
                    variant="flat"
                    size="small"
                    @click="editDataset"
                  >
                    <EditIcon class="mr-2" size="18" />
                    Düzenle
                  </v-btn>
                  
                  <v-btn
                    color="error"
                    variant="flat"
                    size="small"
                    @click="showDeleteDialog = true"
                  >
                    <TrashIcon class="mr-2" size="18" />
                    Sil
                  </v-btn>
                </div>
              </div>
            </v-card-text>
          </v-card>

          <!-- Schema Information Card -->
          <v-card class="mb-4" variant="outlined">
            <v-card-title class="pa-4 bg-primary text-white d-flex align-center">
              <DatabaseIcon class="mr-2" size="20" />
              Schema Bilgileri
            </v-card-title>
            
            <v-divider></v-divider>
            
            <v-card-text class="pa-6">
              <v-row>
                <v-col cols="12" md="6">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Dataset Adı</span>
                    <span class="text-body-1 font-weight-medium">{{ dataset.name }}</span>
                  </div>
                </v-col>
                
                <v-col cols="12" md="6">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Kategori</span>
                    <span class="text-body-1">
                      <v-chip size="small" variant="tonal" color="primary" v-if="dataset.category">
                        <TagIcon size="14" class="mr-1" />
                        {{ getCategoryName(dataset.category) }}
                      </v-chip>
                      <span v-else>-</span>
                    </span>
                  </div>
                </v-col>
                
                <v-col cols="12">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Açıklama</span>
                    <span class="text-body-1">{{ dataset.description || '-' }}</span>
                  </div>
                </v-col>
                
                <v-col cols="12" md="4">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Force Schema</span>
                    <v-chip 
                      :color="dataset.forceSchema ? 'success' : 'default'" 
                      size="small" 
                      variant="tonal"
                    >
                      <CheckIcon v-if="dataset.forceSchema" size="14" class="mr-1" />
                      <XIcon v-else size="14" class="mr-1" />
                      {{ dataset.forceSchema ? 'Evet' : 'Hayır' }}
                    </v-chip>
                  </div>
                </v-col>
                
                <v-col cols="12" md="4">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Logging Mode</span>
                    <v-chip size="small" variant="tonal" color="info">
                      {{ dataset.logging || 'none' }}
                    </v-chip>
                  </div>
                </v-col>
                
                <v-col cols="12" md="4">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Publish Mode</span>
                    <v-chip size="small" variant="tonal" color="warning">
                      {{ dataset.publishMode || 'none' }}
                    </v-chip>
                  </div>
                </v-col>
                
                <v-col cols="12" md="4">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Oluşturulma</span>
                    <span class="text-body-2">{{ formatDate(dataset.createInfo?.createdAt) }}</span>
                  </div>
                </v-col>
                
                <v-col cols="12" md="4">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Oluşturan</span>
                    <span class="text-body-2">
                      {{ dataset.createInfo?.userInfo?.userName || '-' }}
                      <span v-if="dataset.createInfo?.userInfo?.domain" class="text-caption text-medium-emphasis">
                        ({{ dataset.createInfo.userInfo.domain }})
                      </span>
                    </span>
                  </div>
                </v-col>
                
                <v-col cols="12" md="4" v-if="dataset.lastUpdateInfo">
                  <div class="mb-3">
                    <span class="text-caption text-medium-emphasis d-block mb-1">Son Güncelleme</span>
                    <span class="text-body-2">{{ formatDate(dataset.lastUpdateInfo?.updatedAt) }}</span>
                  </div>
                </v-col>
              </v-row>
            </v-card-text>
          </v-card>

          <!-- Fields Card -->
          <v-card class="mb-4" variant="outlined">
            <v-card-title class="pa-4 bg-primary text-white d-flex align-center justify-space-between">
              <div class="d-flex align-center">
                <DatabaseIcon class="mr-2" size="20" />
                Fields ({{ dataset.fieldsCount || 0 }})
              </div>
            </v-card-title>
            
            <v-divider></v-divider>
            
            <v-card-text class="pa-6">
              <div v-if="dataset.fields && dataset.fields.length > 0" class="d-flex flex-column ga-3">
                <v-card
                  v-for="(field, index) in dataset.fields"
                  :key="index"
                  variant="outlined"
                  class="pa-4"
                >
                  <div class="d-flex justify-space-between align-start flex-wrap ga-2">
                    <div class="flex-grow-1">
                      <div class="d-flex align-center ga-2 mb-2">
                        <span class="text-subtitle-2 font-weight-semibold">{{ field.name }}</span>
                        <v-chip size="x-small" variant="tonal" color="info">
                          {{ getFieldTypeDisplay(field.fieldType) }}
                        </v-chip>
                        <v-chip v-if="field.mandatory" size="x-small" variant="tonal" color="success">
                          Mandatory
                        </v-chip>
                        <v-chip v-if="field.unique" size="x-small" variant="tonal" color="warning">
                          Unique
                        </v-chip>
                        <v-chip v-if="field.isArray" size="x-small" variant="tonal" color="primary">
                          Array
                        </v-chip>
                      </div>
                      
                      <div v-if="field.title" class="mb-2">
                        <span class="text-body-2 text-medium-emphasis">{{ field.title }}</span>
                      </div>
                      
                      <!-- Relation Field Info -->
                      <div v-if="field.fieldType === 'relation' && field.relationDataset" class="text-caption text-medium-emphasis">
                        <strong>Relation:</strong> {{ field.relationDataset }} → {{ field.relationField || '__dataId' }}
                      </div>
                      
                      <!-- Incremental Field Info -->
                      <div v-if="field.fieldType === 'incremental' && field.incrementalOptions" class="text-caption text-medium-emphasis">
                        <strong>Format:</strong> {{ field.incrementalOptions.format }}
                        <span v-if="field.incrementalOptions.startValue !== undefined">
                          | <strong>Start:</strong> {{ field.incrementalOptions.startValue }}
                        </span>
                        <span v-if="field.incrementalOptions.incrementStep !== undefined">
                          | <strong>Step:</strong> {{ field.incrementalOptions.incrementStep }}
                        </span>
                      </div>
                    </div>
                  </div>
                </v-card>
              </div>
              
              <v-card v-else variant="outlined" class="pa-8 text-center">
                <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-database</v-icon>
                <p class="text-subtitle-1 text-medium-emphasis">
                  Henüz field tanımlanmamış
                </p>
              </v-card>
            </v-card-text>
          </v-card>

          <!-- Queries Card -->
          <v-card class="mb-4" variant="outlined">
            <v-card-title class="pa-4 bg-primary text-white d-flex align-center justify-space-between">
              <div class="d-flex align-center">
                <FileCodeIcon class="mr-2" size="20" />
                Predefined Queries ({{ dataset.queriesCount || 0 }})
              </div>
            </v-card-title>
            
            <v-divider></v-divider>
            
            <v-card-text class="pa-6">
              <div v-if="dataset.queries && dataset.queries.length > 0" class="d-flex flex-column ga-3">
                <v-card
                  v-for="(query, index) in dataset.queries"
                  :key="index"
                  variant="outlined"
                  class="pa-4"
                >
                  <div class="d-flex justify-space-between align-start flex-wrap ga-2">
                    <div class="flex-grow-1">
                      <div class="d-flex align-center ga-2 mb-2">
                        <FileCodeIcon size="18" class="text-primary" />
                        <span class="text-subtitle-2 font-weight-semibold">{{ query.name }}</span>
                      </div>
                      
                      <div v-if="query.description" class="mb-2">
                        <span class="text-body-2 text-medium-emphasis">{{ query.description }}</span>
                      </div>
                      
                      <div class="mb-2">
                        <span class="text-caption text-medium-emphasis">Parameters: </span>
                        <v-chip size="x-small" variant="tonal" color="info">
                          {{ getParametersDisplay(query.parameters) }}
                        </v-chip>
                      </div>
                      
                      <div v-if="query.pipeline && query.pipeline.length > 0" class="mt-2">
                        <v-expansion-panels variant="accordion" density="compact">
                          <v-expansion-panel>
                            <v-expansion-panel-title class="text-caption">
                              Pipeline'i Görüntüle ({{ query.pipeline.length }} stage)
                            </v-expansion-panel-title>
                            <v-expansion-panel-text>
                              <pre class="text-caption bg-grey-lighten-4 pa-3 rounded font-monospace" style="max-height: 400px; overflow-y: auto;">{{ JSON.stringify(query.pipeline, null, 2) }}</pre>
                            </v-expansion-panel-text>
                          </v-expansion-panel>
                        </v-expansion-panels>
                      </div>
                    </div>
                  </div>
                </v-card>
              </div>
              
              <v-card v-else variant="outlined" class="pa-8 text-center">
                <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-code-braces</v-icon>
                <p class="text-subtitle-1 text-medium-emphasis">
                  Henüz predefined query tanımlanmamış
                </p>
              </v-card>
            </v-card-text>
          </v-card>

          <!-- Indexes Card -->
          <v-card class="mb-4" variant="outlined">
            <v-card-title class="pa-4 bg-primary text-white d-flex align-center justify-space-between">
              <div class="d-flex align-center">
                <KeyIcon class="mr-2" size="20" />
                Index Definitions ({{ dataset.indexListCount || 0 }})
              </div>
            </v-card-title>
            
            <v-divider></v-divider>
            
            <v-card-text class="pa-6">
              <div v-if="dataset.indexList && dataset.indexList.length > 0" class="d-flex flex-column ga-3">
                <v-card
                  v-for="(index, indexIdx) in dataset.indexList"
                  :key="indexIdx"
                  variant="outlined"
                  class="pa-4"
                >
                  <div class="d-flex justify-space-between align-start flex-wrap ga-2">
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
                          Unique
                        </v-chip>
                      </div>
                      
                      <div class="mb-2">
                        <span class="text-caption text-medium-emphasis">Fields: </span>
                        <span class="text-body-2">{{ getFieldsDisplay(index.fields) }}</span>
                      </div>
                    </div>
                  </div>
                </v-card>
              </div>
              
              <v-card v-else variant="outlined" class="pa-8 text-center">
                <v-icon size="48" color="grey-lighten-1" class="mb-4">mdi-key</v-icon>
                <p class="text-subtitle-1 text-medium-emphasis">
                  Henüz index tanımlanmamış
                </p>
              </v-card>
            </v-card-text>
          </v-card>
        </template>
      </v-col>
    </v-row>

    <!-- Delete Confirmation Dialog -->
    <v-dialog v-model="showDeleteDialog" max-width="500px" persistent>
      <v-card>
        <v-card-title class="pa-4 bg-error text-white d-flex align-center">
          <TrashIcon class="mr-2" size="20" />
          Dataset Sil
        </v-card-title>
        
        <v-divider></v-divider>
        
        <v-card-text class="pa-6">
          <p class="text-body-1 mb-4">
            <strong>{{ dataset?.name }}</strong> adlı dataset'i silmek istediğinizden emin misiniz?
          </p>
          <v-alert type="warning" variant="tonal" density="compact" class="mb-0">
            <strong>Uyarı:</strong> Bu işlem geri alınamaz. Dataset ve tüm verileri kalıcı olarak silinecektir.
          </v-alert>
        </v-card-text>
        
        <v-divider></v-divider>
        
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn
            color="default"
            variant="outlined"
            @click="showDeleteDialog = false"
            :disabled="loading"
          >
            <XIcon class="mr-2" size="18" />
            İptal
          </v-btn>
          <v-btn
            color="error"
            variant="flat"
            @click="deleteDataset"
            :loading="loading"
          >
            <TrashIcon class="mr-2" size="18" />
            Sil
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
