<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useWidgetStore, type Widget, type CreateWidgetDto, type UpdateWidgetDto, type DataSourceConfigData } from '@/stores/apps/widget';
import { useDatasetStore } from '@/stores/apps/dataset';
import WidgetRenderer from './WidgetRenderer.vue';
import AggregatePipelineBuilder from './AggregatePipelineBuilder.vue';
import type { WidgetCategory } from '@/stores/apps/widget';

const props = defineProps<{
  initial?: Widget | null;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'submit': [dto: CreateWidgetDto | UpdateWidgetDto];
  'cancel': [];
}>();

const widgetStore = useWidgetStore();
const datasetStore = useDatasetStore();

// Form data
const formData = ref<CreateWidgetDto>({
  name: '',
  title: '',
  description: '',
  category: '',
  type: 'card',
  isActive: true,
  order: 0,
  dataSource: {
    type: 'data',
    dataset: '',
    getMethod: 'default',
    default: {
      limit: 10,
    },
  },
  config: {},
});

// Form validation
const errors = ref<Record<string, string>>({});
const isValid = computed(() => {
  return !errors.value.name && !errors.value.title && !errors.value.category && !errors.value.dataset;
});

// Load categories
const categories = computed(() => widgetStore.activeCategories);
const categoryOptions = computed(() => {
  return categories.value.map((cat) => ({
    value: cat.__dataId ?? cat.dataId ?? '',
    title: cat.name,
    subtitle: cat.description,
    icon: cat.icon,
    color: cat.color,
  }));
});

// Load datasets
const datasets = computed(() => datasetStore.datasets);
const datasetOptions = computed(() => {
  return datasets.value.map((ds) => ({
    value: ds.name || '',
    title: ds.name || '',
    subtitle: ds.description || '',
  })).sort((a, b) => a.title.localeCompare(b.title));
});

const typeOptions = [
  { value: 'card' as const, title: 'Card', icon: 'mdi-card' },
  { value: 'chart' as const, title: 'Chart', icon: 'mdi-chart-line' },
  { value: 'table' as const, title: 'Table', icon: 'mdi-table' },
  { value: 'banner' as const, title: 'Banner', icon: 'mdi-view-dashboard' },
];

// Data source getMethod options
const getMethodOptions = [
  { value: 'default' as const, title: 'Default (GET)', icon: 'mdi-database-search' },
  { value: 'query' as const, title: 'Query (POST with match)', icon: 'mdi-filter' },
  { value: 'aggregate' as const, title: 'Aggregate (POST with pipeline)', icon: 'mdi-chart-box' },
  { value: 'predefined' as const, title: 'Predefined Query', icon: 'mdi-bookmark' },
];

// Current getMethod
const currentGetMethod = computed(() => formData.value.dataSource.getMethod);

// Initialize form
function initializeForm() {
  if (props.initial) {
    const w = props.initial;
    formData.value = {
      name: w.name ?? '',
      title: w.title ?? '',
      description: w.description ?? '',
      category: typeof w.category === 'string' ? w.category : w.category.__dataId ?? w.category.dataId ?? '',
      type: w.type ?? 'card',
      isActive: w.isActive ?? true,
      order: w.order ?? 0,
      dataSource: w.dataSource ?? {
        type: 'data',
        dataset: '',
        getMethod: 'default',
        default: { limit: 10 },
      },
      layout: w.layout,
      style: w.style,
      config: w.config ?? {},
    };
  }
}

// Validate form
function validate() {
  errors.value = {};
  if (!formData.value.name?.trim()) {
    errors.value.name = props.t?.('widgets.form.validation.nameRequired') ?? 'Widget adı zorunludur';
  }
  if (!formData.value.title?.trim()) {
    errors.value.title = props.t?.('widgets.form.validation.titleRequired') ?? 'Widget başlığı zorunludur';
  }
  if (!formData.value.category) {
    errors.value.category = props.t?.('widgets.form.validation.categoryRequired') ?? 'Kategori seçimi zorunludur';
  }
  if (!formData.value.dataSource?.dataset?.trim()) {
    errors.value.dataset = props.t?.('widgets.form.validation.datasetRequired') ?? 'Dataset belirtilmelidir';
  }
  return Object.keys(errors.value).length === 0;
}

// Submit form
function submit() {
  if (!validate()) return;
  emit('submit', props.initial ? { ...formData.value } : formData.value);
}

// Cancel
function cancel() {
  emit('cancel');
}

// Update dataSource based on getMethod
watch(currentGetMethod, (newMethod) => {
  const ds = formData.value.dataSource;
  if (newMethod === 'default') {
    ds.default = ds.default || { limit: 10 };
    ds.query = undefined;
    ds.aggregate = undefined;
    ds.predefined = undefined;
  } else if (newMethod === 'query') {
    ds.query = ds.query || { match: {} };
    ds.default = undefined;
    ds.aggregate = undefined;
    ds.predefined = undefined;
  } else if (newMethod === 'aggregate') {
    ds.aggregate = ds.aggregate || { pipeline: [] };
    ds.default = undefined;
    ds.query = undefined;
    ds.predefined = undefined;
    // Initialize pipeline builder
    if (!ds.aggregate.pipeline || ds.aggregate.pipeline.length === 0) {
      ds.aggregate.pipeline = [];
    }
  } else if (newMethod === 'predefined') {
    ds.predefined = ds.predefined || { queryName: '' };
    ds.default = undefined;
    ds.query = undefined;
    ds.aggregate = undefined;
  }
});

// Load categories and datasets on mount
onMounted(async () => {
  try {
    await Promise.all([
      widgetStore.fetchWidgetCategories(),
      datasetStore.fetchDatasets({ pageNumber: 1, pageSize: 1000 }), // Fetch all datasets
    ]);
    
    // Load dataset schema if dataset is already selected
    if (formData.value.dataSource?.dataset) {
      try {
        await datasetStore.fetchDatasetByName(formData.value.dataSource.dataset);
      } catch {
        // Dataset schema not found, ignore
      }
    }
  } catch {
    /* store sets error */
  }
  initializeForm();
  if (formData.value.type === 'card') {
    initializeConfigBuilder();
  }
});

// Watch dataset changes to load schema
watch(
  () => formData.value.dataSource?.dataset,
  async (newDataset) => {
    if (newDataset) {
      try {
        await datasetStore.fetchDatasetByName(newDataset);
      } catch {
        // Dataset schema not found, ignore
      }
    }
  }
);

watch(() => props.initial, initializeForm, { deep: true });

const lbl = (key: string) => props.t?.(`widgets.form.${key}`) ?? key;

// JSON editors for complex fields
const queryMatchJson = computed({
  get: () => {
    try {
      return JSON.stringify(formData.value.dataSource.query?.match ?? {}, null, 2);
    } catch {
      return '{}';
    }
  },
  set: (val) => {
    try {
      const parsed = JSON.parse(val);
      if (formData.value.dataSource.query) {
        formData.value.dataSource.query.match = parsed;
      }
    } catch {
      // Invalid JSON, ignore
    }
  },
});

const aggregatePipelineJson = computed({
  get: () => {
    try {
      return JSON.stringify(formData.value.dataSource.aggregate?.pipeline ?? [], null, 2);
    } catch {
      return '[]';
    }
  },
  set: (val) => {
    try {
      const parsed = JSON.parse(val);
      if (formData.value.dataSource.aggregate) {
        formData.value.dataSource.aggregate.pipeline = parsed;
      }
    } catch {
      // Invalid JSON, ignore
    }
  },
});

// Watch pipeline builder changes to sync with JSON
watch(
  () => formData.value.dataSource.aggregate?.pipeline,
  () => {
    // Pipeline builder updates formData directly, no need to sync
    // availableFields computed will automatically update
  },
  { deep: true }
);

const predefinedParamsJson = computed({
  get: () => {
    try {
      return JSON.stringify(formData.value.dataSource.predefined?.parameters ?? {}, null, 2);
    } catch {
      return '{}';
    }
  },
  set: (val) => {
    try {
      const parsed = JSON.parse(val);
      if (formData.value.dataSource.predefined) {
        formData.value.dataSource.predefined.parameters = parsed;
      }
    } catch {
      // Invalid JSON, ignore
    }
  },
});

const configJson = computed({
  get: () => {
    try {
      return JSON.stringify(formData.value.config ?? {}, null, 2);
    } catch {
      return '{}';
    }
  },
  set: (val) => {
    try {
      const parsed = JSON.parse(val);
      formData.value.config = parsed;
    } catch {
      // Invalid JSON, ignore
    }
  },
});

// Config builder state (for visual config builder)
const useConfigBuilder = ref(true);
const usePipelineBuilder = ref(true);

// Available fields from pipeline/dataset (for dropdown)
const availableFields = computed(() => {
  const fields: string[] = [];
  
  // 1. Get fields from dataset schema
  if (formData.value.dataSource?.dataset) {
    const dataset = datasetStore.getDatasetByName(formData.value.dataSource.dataset);
    if (dataset?.fields && Array.isArray(dataset.fields)) {
      dataset.fields.forEach((field) => {
        if (field.name && !fields.includes(field.name)) {
          fields.push(field.name);
        }
      });
    }
  }
  
  // 2. Analyze aggregate pipeline to extract output fields
  if (formData.value.dataSource?.aggregate?.pipeline) {
    const pipeline = formData.value.dataSource.aggregate.pipeline;
    
    pipeline.forEach((stage: any) => {
      // $group stage: extract field names from group output
      if (stage.$group) {
        Object.keys(stage.$group).forEach((key) => {
          if (key !== '_id' && !fields.includes(key)) {
            fields.push(key);
          }
        });
      }
      
      // $project stage: extract included fields
      if (stage.$project) {
        Object.keys(stage.$project).forEach((key) => {
          if (stage.$project[key] === 1 && !fields.includes(key)) {
            fields.push(key);
          }
        });
      }
      
      // $addFields stage: extract added field names
      if (stage.$addFields) {
        Object.keys(stage.$addFields).forEach((key) => {
          if (!fields.includes(key)) {
            fields.push(key);
          }
        });
      }
    });
  }
  
  // 3. Common aggregate output fields (if pipeline exists)
  if (formData.value.dataSource?.aggregate?.pipeline?.length > 0) {
    const commonFields = ['count', 'total', 'sum', 'avg', 'min', 'max', 'first', 'last'];
    commonFields.forEach((field) => {
      if (!fields.includes(field)) {
        fields.push(field);
      }
    });
  }
  
  return fields.sort();
});

// Initialize config builder data
function initializeConfigBuilder() {
  if (!formData.value.config || Object.keys(formData.value.config).length === 0) {
    formData.value.config = {
      icon: 'mdi-chart-line',
      iconVariant: 'icon',
      color: 'primary',
      format: 'number',
      variant: 'outlined',
    };
  }
}

// Helper functions for config builder fields
function updateConfigField(key: string, value: any) {
  if (!formData.value.config) {
    formData.value.config = {};
  }
  formData.value.config = { ...formData.value.config, [key]: value };
}

function getConfigValue(key: string, defaultValue: any = '') {
  return formData.value.config?.[key] ?? defaultValue;
}

// Watch widget type to reset config
watch(() => formData.value.type, () => {
  if (formData.value.type === 'card') {
    initializeConfigBuilder();
  }
});

// Preview widget (convert formData to Widget format)
const previewWidget = computed<Widget | null>(() => {
  if (!formData.value.name?.trim() || !formData.value.title?.trim() || !formData.value.dataSource?.dataset?.trim()) {
    return null;
  }

  try {
    return {
      __dataId: props.initial?.__dataId ?? props.initial?.dataId ?? '',
      dataId: props.initial?.dataId ?? '',
      name: formData.value.name,
      title: formData.value.title,
      description: formData.value.description,
      category: formData.value.category,
      type: formData.value.type,
      isActive: formData.value.isActive,
      order: formData.value.order,
      dataSource: formData.value.dataSource,
      config: formData.value.config,
      layout: formData.value.layout,
      style: formData.value.style,
    } as Widget;
  } catch {
    return null;
  }
});

const showPreview = ref(false);
</script>

<template>
  <v-form @submit.prevent="submit">
    <v-card elevation="10">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon class="mr-2">mdi-widgets</v-icon>
        <span>{{ initial ? lbl('editTitle') : lbl('createTitle') }}</span>
      </v-card-title>

      <v-divider />

      <v-card-text class="pa-4">
        <v-alert
          v-if="widgetStore.error"
          type="error"
          variant="tonal"
          density="compact"
          class="mb-4"
          closable
          @click:close="widgetStore.clearError"
        >
          {{ widgetStore.error }}
        </v-alert>

        <!-- Basic Information -->
        <div class="mb-6">
          <h3 class="text-h6 mb-4">{{ lbl('basicInfo') }}</h3>
          <v-row>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.name"
                :label="lbl('name')"
                :error-messages="errors.name"
                variant="outlined"
                density="comfortable"
                required
                :disabled="widgetStore.loading"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model="formData.title"
                :label="lbl('title')"
                :error-messages="errors.title"
                variant="outlined"
                density="comfortable"
                required
                :disabled="widgetStore.loading"
              />
            </v-col>
            <v-col cols="12">
              <v-textarea
                v-model="formData.description"
                :label="lbl('description')"
                variant="outlined"
                density="comfortable"
                rows="2"
                :disabled="widgetStore.loading"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="formData.category"
                :items="categoryOptions"
                item-title="title"
                item-value="value"
                :label="lbl('category')"
                :error-messages="errors.category"
                variant="outlined"
                density="comfortable"
                required
                :disabled="widgetStore.loading || categories.length === 0"
                :loading="widgetStore.loading"
              >
                <template #item="{ props: itemProps, item }">
                  <v-list-item v-bind="itemProps">
                    <template #prepend>
                      <v-icon :color="item.raw.color">{{ item.raw.icon }}</v-icon>
                    </template>
                    <v-list-item-title>{{ item.raw.title }}</v-list-item-title>
                    <v-list-item-subtitle v-if="item.raw.subtitle">{{ item.raw.subtitle }}</v-list-item-subtitle>
                  </v-list-item>
                </template>
              </v-select>
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="formData.type"
                :items="typeOptions"
                item-title="title"
                item-value="value"
                :label="lbl('type')"
                variant="outlined"
                density="comfortable"
                required
                :disabled="widgetStore.loading"
              >
                <template #item="{ props: itemProps, item }">
                  <v-list-item v-bind="itemProps">
                    <template #prepend>
                      <v-icon>{{ item.raw.icon }}</v-icon>
                    </template>
                    <v-list-item-title>{{ item.raw.title }}</v-list-item-title>
                  </v-list-item>
                </template>
              </v-select>
            </v-col>
            <v-col cols="12" md="6">
              <v-text-field
                v-model.number="formData.order"
                :label="lbl('order')"
                type="number"
                variant="outlined"
                density="comfortable"
                :disabled="widgetStore.loading"
              />
            </v-col>
            <v-col cols="12" md="6">
              <v-switch
                v-model="formData.isActive"
                :label="lbl('isActive')"
                color="success"
                :disabled="widgetStore.loading"
              />
            </v-col>
          </v-row>
        </div>

        <!-- Data Source Configuration -->
        <div class="mb-6">
          <h3 class="text-h6 mb-4">{{ lbl('dataSource') }}</h3>
          <v-row>
            <v-col cols="12" md="6">
              <v-select
                v-model="formData.dataSource.dataset"
                :items="datasetOptions"
                item-title="title"
                item-value="value"
                :label="lbl('dataset')"
                :error-messages="errors.dataset"
                variant="outlined"
                density="comfortable"
                required
                :disabled="widgetStore.loading || datasetStore.loading"
                :loading="datasetStore.loading"
                hint="Dataset seçin veya manuel olarak girin"
                persistent-hint
                clearable
              >
                <template #item="{ props: itemProps, item }">
                  <v-list-item v-bind="itemProps">
                    <v-list-item-title>{{ item.raw.title }}</v-list-item-title>
                    <v-list-item-subtitle v-if="item.raw.subtitle">{{ item.raw.subtitle }}</v-list-item-subtitle>
                  </v-list-item>
                </template>
              </v-select>
            </v-col>
            <v-col cols="12" md="6">
              <v-select
                v-model="formData.dataSource.getMethod"
                :items="getMethodOptions"
                item-title="title"
                item-value="value"
                :label="lbl('getMethod')"
                variant="outlined"
                density="comfortable"
                required
                :disabled="widgetStore.loading"
              >
                <template #item="{ props: itemProps, item }">
                  <v-list-item v-bind="itemProps">
                    <template #prepend>
                      <v-icon>{{ item.raw.icon }}</v-icon>
                    </template>
                    <v-list-item-title>{{ item.raw.title }}</v-list-item-title>
                  </v-list-item>
                </template>
              </v-select>
            </v-col>
          </v-row>

          <!-- Default Method Config -->
          <div v-if="currentGetMethod === 'default'" class="mt-4">
            <v-row>
              <v-col cols="12" md="4">
                <v-text-field
                  v-if="formData.dataSource.default"
                  v-model.number="formData.dataSource.default.skip"
                  :label="lbl('skip')"
                  type="number"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  v-if="formData.dataSource.default"
                  v-model.number="formData.dataSource.default.limit"
                  :label="lbl('limit')"
                  type="number"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  v-if="formData.dataSource.default"
                  v-model="formData.dataSource.default.sort"
                  :label="lbl('sort')"
                  placeholder="name,order"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="formData.dataSource.default"
                  v-model="formData.dataSource.default.filter"
                  :label="lbl('filter')"
                  placeholder="isActive:eq:true"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="formData.dataSource.default"
                  v-model="formData.dataSource.default.fields"
                  :label="lbl('fields')"
                  placeholder="name,title"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
            </v-row>
          </div>

          <!-- Query Method Config -->
          <div v-if="currentGetMethod === 'query'" class="mt-4">
            <v-alert type="info" variant="tonal" density="compact" class="mb-4">
              {{ lbl('queryHint') }}
            </v-alert>
            <v-textarea
              v-model="queryMatchJson"
              :label="lbl('match')"
              placeholder='{"status": "active"}'
              variant="outlined"
              density="compact"
              rows="3"
              :disabled="widgetStore.loading"
            />
            <v-row class="mt-2">
              <v-col cols="12" md="4">
                <v-text-field
                  v-if="formData.dataSource.query"
                  v-model.number="formData.dataSource.query.skip"
                  :label="lbl('skip')"
                  type="number"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
              <v-col cols="12" md="4">
                <v-text-field
                  v-if="formData.dataSource.query"
                  v-model.number="formData.dataSource.query.limit"
                  :label="lbl('limit')"
                  type="number"
                  variant="outlined"
                  density="compact"
                  :disabled="widgetStore.loading"
                />
              </v-col>
            </v-row>
          </div>

          <!-- Aggregate Method Config -->
          <div v-if="currentGetMethod === 'aggregate'" class="mt-4">
            <div class="d-flex justify-space-between align-center mb-4">
              <v-alert type="info" variant="tonal" density="compact" class="flex-grow-1 mr-4">
                {{ lbl('aggregateHint') }}
              </v-alert>
              <v-btn
                variant="text"
                size="small"
                @click="usePipelineBuilder = !usePipelineBuilder"
              >
                <v-icon start>{{ usePipelineBuilder ? 'mdi-code-json' : 'mdi-cog' }}</v-icon>
                {{ usePipelineBuilder ? (lbl('showJson') || 'JSON Göster') : (lbl('useBuilder') || 'Builder Kullan') }}
              </v-btn>
            </div>

            <!-- Pipeline Builder -->
            <AggregatePipelineBuilder
              v-if="usePipelineBuilder && formData.dataSource.aggregate"
              v-model="formData.dataSource.aggregate.pipeline"
              :disabled="widgetStore.loading"
              :t="props.t"
            />

            <!-- JSON Editor (Fallback) -->
            <v-textarea
              v-else
              v-model="aggregatePipelineJson"
              :label="lbl('pipeline')"
              placeholder='[{"$group": {"_id": null, "total": {"$sum": "$amount"}}}]'
              variant="outlined"
              density="compact"
              rows="5"
              :disabled="widgetStore.loading"
            />
          </div>

          <!-- Predefined Method Config -->
          <div v-if="currentGetMethod === 'predefined'" class="mt-4">
            <v-row>
              <v-col cols="12" md="6">
                <v-text-field
                  v-if="formData.dataSource.predefined"
                  v-model="formData.dataSource.predefined.queryName"
                  :label="lbl('queryName')"
                  variant="outlined"
                  density="compact"
                  required
                  :disabled="widgetStore.loading"
                />
              </v-col>
            </v-row>
            <v-textarea
              v-model="predefinedParamsJson"
              :label="lbl('parameters')"
              placeholder='{"param1": "value1"}'
              variant="outlined"
              density="compact"
              rows="3"
              :disabled="widgetStore.loading"
            />
          </div>
        </div>

        <!-- Config (Widget-specific) -->
        <div class="mb-6">
          <div class="d-flex justify-space-between align-center mb-4">
            <h3 class="text-h6 mb-0">{{ lbl('config') }}</h3>
            <v-btn
              v-if="formData.type === 'card'"
              variant="text"
              size="small"
              @click="useConfigBuilder = !useConfigBuilder"
            >
              <v-icon start>{{ useConfigBuilder ? 'mdi-code-json' : 'mdi-cog' }}</v-icon>
              {{ useConfigBuilder ? (lbl('showJson') || 'JSON Göster') : (lbl('useBuilder') || 'Builder Kullan') }}
            </v-btn>
          </div>

          <!-- Chart Widget Config Hint -->
          <v-alert
            v-if="formData.type === 'chart'"
            type="info"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            <div class="text-body-2">
              <strong>{{ lbl('chartConfigHint') || 'Chart Widget Yapılandırması' }}</strong>
              <div class="mt-2">
                <div class="text-caption mb-2">
                  <strong>Basit Chart:</strong> xAxis.field ve yAxis.field belirtin
                </div>
                <div class="text-caption mb-2">
                  <strong>Çoklu Series:</strong> series array'i kullanın
                </div>
                <div class="text-caption mb-2">
                  <strong>Gruplu Chart:</strong> groupBy ve aggregate kullanın
                </div>
                <pre class="mt-2 text-caption" style="font-size: 11px; white-space: pre-wrap;">{{
`{
  "type": "bar",
  "height": 350,
  "xAxis": {
    "field": "title",
    "label": "Kitap Adı"
  },
  "yAxis": {
    "field": "price",
    "label": "Fiyat (TL)"
  }
}`}}
                </pre>
              </div>
            </div>
          </v-alert>

          <!-- Banner Widget Config Hint -->
          <v-alert
            v-if="formData.type === 'banner'"
            type="info"
            variant="tonal"
            density="compact"
            class="mb-4"
          >
            <div class="text-body-2">
              <strong>{{ lbl('bannerConfigHint') || 'Banner Widget Config' }}</strong>
              <div class="mt-2">
                <code class="text-caption">
                  {<br>
                  &nbsp;&nbsp;"type": "info",<br>
                  &nbsp;&nbsp;"variant": "tonal",<br>
                  &nbsp;&nbsp;"title": "Yeni Kitap Eklendi",<br>
                  &nbsp;&nbsp;"titleField": "title",<br>
                  &nbsp;&nbsp;"content": "Yeni bir kitap eklendi",<br>
                  &nbsp;&nbsp;"contentField": "description",<br>
                  &nbsp;&nbsp;"icon": "mdi-book-plus",<br>
                  &nbsp;&nbsp;"showIcon": true,<br>
                  &nbsp;&nbsp;"dismissible": true,<br>
                  &nbsp;&nbsp;"action": {<br>
                  &nbsp;&nbsp;&nbsp;&nbsp;"enabled": true,<br>
                  &nbsp;&nbsp;&nbsp;&nbsp;"label": "Detaylar",<br>
                  &nbsp;&nbsp;&nbsp;&nbsp;"icon": "mdi-arrow-right",<br>
                  &nbsp;&nbsp;&nbsp;&nbsp;"color": "primary"<br>
                  &nbsp;&nbsp;}<br>
                  }
                </code>
              </div>
              <div class="mt-2 text-caption">
                <strong>Tip:</strong> info, warning, success, error, custom<br>
                <strong>Variant:</strong> tonal, filled, outlined, flat<br>
                <strong>Data field:</strong> titleField ve contentField ile data'dan değer çekebilirsiniz.
              </div>
            </div>
          </v-alert>

          <!-- Visual Config Builder for Card Widget -->
          <div v-if="formData.type === 'card' && useConfigBuilder" class="mb-4">
            <v-expansion-panels variant="accordion" multiple>
              <!-- Genel Ayarlar -->
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <v-icon start>mdi-cog</v-icon>
                  {{ lbl('configBuilder.general') || 'Genel Ayarlar' }}
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('icon', 'mdi-chart-line')"
                        @update:model-value="updateConfigField('icon', $event)"
                        :label="lbl('configBuilder.icon') || 'İkon'"
                        placeholder="mdi-chart-line"
                        variant="outlined"
                        density="comfortable"
                        hint="Material Design Icons (mdi-*)"
                        persistent-hint
                        :disabled="widgetStore.loading"
                      >
                        <template #append-inner>
                          <v-icon v-if="getConfigValue('icon')">{{ getConfigValue('icon') }}</v-icon>
                        </template>
                      </v-text-field>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('iconVariant', 'icon')"
                        @update:model-value="updateConfigField('iconVariant', $event)"
                        :items="[
                          { value: 'icon', title: 'İkon' },
                          { value: 'avatar', title: 'Avatar' },
                          { value: 'button', title: 'Buton' }
                        ]"
                        :label="lbl('configBuilder.iconVariant') || 'İkon Stili'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('color', 'primary')"
                        @update:model-value="updateConfigField('color', $event)"
                        :items="[
                          { value: 'primary', title: 'Primary' },
                          { value: 'secondary', title: 'Secondary' },
                          { value: 'success', title: 'Success' },
                          { value: 'info', title: 'Info' },
                          { value: 'warning', title: 'Warning' },
                          { value: 'error', title: 'Error' }
                        ]"
                        :label="lbl('configBuilder.color') || 'Renk'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('subtitle', '')"
                        @update:model-value="updateConfigField('subtitle', $event)"
                        :label="lbl('configBuilder.subtitle') || 'Alt Başlık'"
                        placeholder="Opsiyonel alt başlık"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>

              <!-- Format Ayarları -->
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <v-icon start>mdi-format-text</v-icon>
                  {{ lbl('configBuilder.format') || 'Format Ayarları' }}
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('format', 'number')"
                        @update:model-value="updateConfigField('format', $event)"
                        :items="[
                          { value: 'number', title: 'Sayı' },
                          { value: 'currency', title: 'Para Birimi' },
                          { value: 'percentage', title: 'Yüzde' }
                        ]"
                        :label="lbl('configBuilder.format') || 'Format'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col v-if="getConfigValue('format') === 'currency'" cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('currency', '₺')"
                        @update:model-value="updateConfigField('currency', $event)"
                        :label="lbl('configBuilder.currency') || 'Para Birimi'"
                        placeholder="₺"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('decimalPlaces', 0)"
                        @update:model-value="updateConfigField('decimalPlaces', Number($event))"
                        :label="lbl('configBuilder.decimalPlaces') || 'Ondalık Basamak'"
                        type="number"
                        min="0"
                        max="10"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>

              <!-- Değer Ayarları -->
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <v-icon start>mdi-numeric</v-icon>
                  {{ lbl('configBuilder.value') || 'Değer Ayarları' }}
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-autocomplete
                        :model-value="getConfigValue('valueField', '')"
                        @update:model-value="updateConfigField('valueField', $event)"
                        :label="lbl('configBuilder.valueField') || 'Değer Alanı'"
                        :items="availableFields"
                        placeholder="count, total, amount"
                        hint="Dataset'ten hangi alan kullanılacak? (Manuel girebilir veya listeden seçebilirsiniz)"
                        persistent-hint
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                        clearable
                        :menu-props="{ maxHeight: '300' }"
                      >
                        <template #prepend-inner>
                          <v-icon size="small">mdi-database-search</v-icon>
                        </template>
                      </v-autocomplete>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-switch
                        :model-value="getConfigValue('showSecondaryValue', false)"
                        @update:model-value="updateConfigField('showSecondaryValue', $event)"
                        :label="lbl('configBuilder.showSecondaryValue') || 'İkinci Değer Göster'"
                        color="primary"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col v-if="getConfigValue('showSecondaryValue')" cols="12" md="6">
                      <v-autocomplete
                        :model-value="getConfigValue('secondaryValueField', '')"
                        @update:model-value="updateConfigField('secondaryValueField', $event)"
                        :label="lbl('configBuilder.secondaryValueField') || 'İkinci Değer Alanı'"
                        :items="availableFields"
                        placeholder="monthly"
                        hint="Manuel girebilir veya listeden seçebilirsiniz"
                        persistent-hint
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                        clearable
                        :menu-props="{ maxHeight: '300' }"
                      >
                        <template #prepend-inner>
                          <v-icon size="small">mdi-database-search</v-icon>
                        </template>
                      </v-autocomplete>
                    </v-col>
                    <v-col v-if="getConfigValue('showSecondaryValue')" cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('secondaryLabel', '')"
                        @update:model-value="updateConfigField('secondaryLabel', $event)"
                        :label="lbl('configBuilder.secondaryLabel') || 'İkinci Değer Etiketi'"
                        placeholder="Bu Ay"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>

              <!-- Trend Ayarları -->
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <v-icon start>mdi-trending-up</v-icon>
                  {{ lbl('configBuilder.trend') || 'Trend Ayarları' }}
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-autocomplete
                        :model-value="getConfigValue('trendField', '')"
                        @update:model-value="updateConfigField('trendField', $event)"
                        :label="lbl('configBuilder.trendField') || 'Trend Alanı'"
                        :items="availableFields"
                        placeholder="count, total"
                        hint="Trend hesaplamak için kullanılacak alan (Manuel girebilir veya listeden seçebilirsiniz)"
                        persistent-hint
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                        clearable
                        :menu-props="{ maxHeight: '300' }"
                      >
                        <template #prepend-inner>
                          <v-icon size="small">mdi-database-search</v-icon>
                        </template>
                      </v-autocomplete>
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('trendUpColor', 'success')"
                        @update:model-value="updateConfigField('trendUpColor', $event)"
                        :items="[
                          { value: 'success', title: 'Success (Yeşil)' },
                          { value: 'info', title: 'Info (Mavi)' },
                          { value: 'primary', title: 'Primary' }
                        ]"
                        :label="lbl('configBuilder.trendUpColor') || 'Artış Rengi'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('trendDownColor', 'error')"
                        @update:model-value="updateConfigField('trendDownColor', $event)"
                        :items="[
                          { value: 'error', title: 'Error (Kırmızı)' },
                          { value: 'warning', title: 'Warning (Turuncu)' }
                        ]"
                        :label="lbl('configBuilder.trendDownColor') || 'Azalış Rengi'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>

              <!-- Görünüm Ayarları -->
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <v-icon start>mdi-palette</v-icon>
                  {{ lbl('configBuilder.appearance') || 'Görünüm Ayarları' }}
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('variant', 'outlined')"
                        @update:model-value="updateConfigField('variant', $event)"
                        :items="[
                          { value: 'outlined', title: 'Kenarlıklı' },
                          { value: 'flat', title: 'Düz' },
                          { value: 'elevated', title: 'Gölgeli' }
                        ]"
                        :label="lbl('configBuilder.variant') || 'Kart Stili'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col v-if="getConfigValue('variant') === 'elevated'" cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('elevation', 10)"
                        @update:model-value="updateConfigField('elevation', Number($event))"
                        :label="lbl('configBuilder.elevation') || 'Gölge Seviyesi'"
                        type="number"
                        min="0"
                        max="24"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('bgColor', '')"
                        @update:model-value="updateConfigField('bgColor', $event)"
                        :label="lbl('configBuilder.bgColor') || 'Arka Plan Rengi'"
                        placeholder="bg-primary, bg-secondary"
                        hint="Vuetify color class (bg-*)"
                        persistent-hint
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>

              <!-- Aksiyon Butonu -->
              <v-expansion-panel>
                <v-expansion-panel-title>
                  <v-icon start>mdi-cursor-pointer</v-icon>
                  {{ lbl('configBuilder.action') || 'Aksiyon Butonu' }}
                </v-expansion-panel-title>
                <v-expansion-panel-text>
                  <v-row>
                    <v-col cols="12" md="6">
                      <v-switch
                        :model-value="getConfigValue('showAction', false)"
                        @update:model-value="updateConfigField('showAction', $event)"
                        :label="lbl('configBuilder.showAction') || 'Aksiyon Butonu Göster'"
                        color="primary"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                    <v-col v-if="getConfigValue('showAction')" cols="12" md="6">
                      <v-text-field
                        :model-value="getConfigValue('actionIcon', '')"
                        @update:model-value="updateConfigField('actionIcon', $event)"
                        :label="lbl('configBuilder.actionIcon') || 'Aksiyon İkonu'"
                        placeholder="mdi-dots-vertical"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      >
                        <template #append-inner>
                          <v-icon v-if="getConfigValue('actionIcon')">{{ getConfigValue('actionIcon') }}</v-icon>
                        </template>
                      </v-text-field>
                    </v-col>
                    <v-col v-if="getConfigValue('showAction')" cols="12" md="6">
                      <v-select
                        :model-value="getConfigValue('actionColor', 'default')"
                        @update:model-value="updateConfigField('actionColor', $event)"
                        :items="[
                          { value: 'default', title: 'Default' },
                          { value: 'primary', title: 'Primary' },
                          { value: 'secondary', title: 'Secondary' }
                        ]"
                        :label="lbl('configBuilder.actionColor') || 'Aksiyon Rengi'"
                        variant="outlined"
                        density="comfortable"
                        :disabled="widgetStore.loading"
                      />
                    </v-col>
                  </v-row>
                </v-expansion-panel-text>
              </v-expansion-panel>
            </v-expansion-panels>
          </div>

          <!-- JSON Editor (Fallback) -->
          <div v-else>
            <v-alert type="info" variant="tonal" density="compact" class="mb-4">
              {{ lbl('configHint') }}
            </v-alert>
            <v-textarea
              v-model="configJson"
              :label="lbl('configJson')"
              placeholder='{"icon": "mdi-chart-line", "color": "primary", "format": "number"}'
              variant="outlined"
              density="compact"
              rows="5"
              :disabled="widgetStore.loading"
            />
          </div>
        </div>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-btn
          v-if="previewWidget"
          variant="outlined"
          color="secondary"
          :disabled="widgetStore.loading"
          @click="showPreview = !showPreview"
        >
          <v-icon start>{{ showPreview ? 'mdi-eye-off' : 'mdi-eye' }}</v-icon>
          {{ showPreview ? (lbl('hidePreview') || 'Önizlemeyi Gizle') : (lbl('preview') || 'Önizle') }}
        </v-btn>
        <v-spacer />
        <v-btn variant="text" :disabled="widgetStore.loading" @click="cancel">
          {{ lbl('cancel') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          type="submit"
          :loading="widgetStore.loading"
          :disabled="!isValid"
        >
          {{ lbl('save') }}
        </v-btn>
      </v-card-actions>
    </v-card>

    <!-- Preview Panel -->
    <v-card v-if="showPreview && previewWidget" elevation="10" class="mt-4">
      <v-card-title class="d-flex align-center pa-4">
        <v-icon class="mr-2">mdi-eye</v-icon>
        <span>{{ lbl('preview') || 'Önizleme' }}</span>
      </v-card-title>
      <v-divider />
      <v-card-text class="pa-4">
        <div style="max-width: 400px; margin: 0 auto;">
          <WidgetRenderer :widget="previewWidget" :t="props.t" />
        </div>
      </v-card-text>
    </v-card>
  </v-form>
</template>
