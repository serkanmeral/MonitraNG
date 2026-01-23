<script setup lang="ts">
import { ref, computed, watch } from 'vue';

const props = defineProps<{
  modelValue: any[]; // Pipeline array
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: any[]];
}>();

const pipeline = computed({
  get: () => props.modelValue || [],
  set: (val) => emit('update:modelValue', val || []),
});

// Available pipeline stages
const stageTypes = [
  { value: '$match', title: '$match - Filtreleme', icon: 'mdi-filter' },
  { value: '$group', title: '$group - Gruplama', icon: 'mdi-group' },
  { value: '$sort', title: '$sort - Sıralama', icon: 'mdi-sort' },
  { value: '$limit', title: '$limit - Limit', icon: 'mdi-numeric' },
  { value: '$skip', title: '$skip - Atlama', icon: 'mdi-skip-next' },
  { value: '$project', title: '$project - Alan Seçimi', icon: 'mdi-view-column' },
  { value: '$addFields', title: '$addFields - Alan Ekleme', icon: 'mdi-plus-circle' },
  { value: '$unwind', title: '$unwind - Array Açma', icon: 'mdi-unfold-more-horizontal' },
];

// Pipeline stage templates
const stageTemplates: Record<string, any> = {
  $match: { $match: {} },
  $group: { $group: { _id: null, count: { $sum: 1 } } },
  $sort: { $sort: { createdAt: -1 } },
  $limit: { $limit: 10 },
  $skip: { $skip: 0 },
  $project: { $project: { _id: 0, name: 1, value: 1 } },
  $addFields: { $addFields: {} },
  $unwind: { $unwind: { path: '$items' } },
};

// Common templates
const commonTemplates = [
  {
    name: 'Toplam Sayı',
    description: 'Tüm kayıtların sayısını hesaplar',
    pipeline: [{ $group: { _id: null, count: { $sum: 1 } } }],
  },
  {
    name: 'Toplam Değer',
    description: 'Belirli bir alanın toplamını hesaplar',
    pipeline: [{ $group: { _id: null, total: { $sum: '$amount' } } }],
  },
  {
    name: 'Ortalama',
    description: 'Belirli bir alanın ortalamasını hesaplar',
    pipeline: [{ $group: { _id: null, avg: { $avg: '$amount' } } }],
  },
  {
    name: 'Filtrele + Say',
    description: 'Filtreleme yapıp sayar',
    pipeline: [
      { $match: { status: 'active' } },
      { $group: { _id: null, count: { $sum: 1 } } },
    ],
  },
  {
    name: 'Grupla + Topla',
    description: 'Kategoriye göre gruplayıp toplar',
    pipeline: [
      { $group: { _id: '$category', total: { $sum: '$amount' } } },
      { $sort: { total: -1 } },
    ],
  },
];

// Add new stage
function addStage(type?: string) {
  const stageType = type || '$match';
  const template = stageTemplates[stageType];
  if (template) {
    pipeline.value = [...(pipeline.value || []), JSON.parse(JSON.stringify(template))];
  }
}

// Remove stage
function removeStage(index: number) {
  const newPipeline = [...(pipeline.value || [])];
  newPipeline.splice(index, 1);
  pipeline.value = newPipeline;
}

// Move stage up
function moveStageUp(index: number) {
  if (index === 0) return;
  const newPipeline = [...(pipeline.value || [])];
  [newPipeline[index - 1], newPipeline[index]] = [newPipeline[index], newPipeline[index - 1]];
  pipeline.value = newPipeline;
}

// Move stage down
function moveStageDown(index: number) {
  if (index >= (pipeline.value || []).length - 1) return;
  const newPipeline = [...(pipeline.value || [])];
  [newPipeline[index], newPipeline[index + 1]] = [newPipeline[index + 1], newPipeline[index]];
  pipeline.value = newPipeline;
}

// Apply template
function applyTemplate(template: any) {
  pipeline.value = JSON.parse(JSON.stringify(template.pipeline));
}

// Get stage type from stage object
function getStageType(stage: any): string {
  if (!stage || typeof stage !== 'object') return '';
  const keys = Object.keys(stage);
  return keys.find((k) => k.startsWith('$')) || '';
}

// Update stage JSON
function updateStageJson(index: number, jsonStr: string) {
  try {
    const parsed = JSON.parse(jsonStr);
    const newPipeline = [...(pipeline.value || [])];
    newPipeline[index] = parsed;
    pipeline.value = newPipeline;
  } catch (e) {
    // Invalid JSON, ignore
  }
}

// Get stage JSON string
function getStageJson(stage: any): string {
  try {
    return JSON.stringify(stage, null, 2);
  } catch {
    return '{}';
  }
}

const lbl = (key: string) => props.t?.(`widgets.form.aggregatePipeline.${key}`) || key;
</script>

<template>
  <div class="aggregate-pipeline-builder">
    <!-- Common Templates -->
    <v-alert type="info" variant="tonal" density="compact" class="mb-4">
      <div class="d-flex align-center justify-space-between">
        <span>{{ lbl('templates') || 'Hazır Şablonlar' }}</span>
        <div class="d-flex ga-2 flex-wrap">
          <v-btn
            v-for="template in commonTemplates"
            :key="template.name"
            size="small"
            variant="outlined"
            color="primary"
            :disabled="props.disabled"
            @click="applyTemplate(template)"
          >
            <v-icon start size="16">mdi-content-copy</v-icon>
            {{ template.name }}
          </v-btn>
        </div>
      </div>
    </v-alert>

    <!-- Pipeline Stages -->
    <div v-if="pipeline && pipeline.length > 0" class="mb-4">
      <div
        v-for="(stage, index) in pipeline"
        :key="index"
        class="mb-4"
      >
        <v-card variant="outlined">
          <v-card-title class="d-flex align-center pa-3">
            <v-icon class="mr-2" :color="getStageType(stage) ? 'primary' : 'error'">
              {{ stageTypes.find((s) => s.value === getStageType(stage))?.icon || 'mdi-alert' }}
            </v-icon>
            <span class="text-subtitle-1 font-weight-medium">
              {{ index + 1 }}. {{ getStageType(stage) || 'Bilinmeyen Stage' }}
            </span>
            <v-spacer />
            <div class="d-flex ga-1">
              <v-btn
                icon
                size="small"
                variant="text"
                :disabled="props.disabled || index === 0"
                @click="moveStageUp(index)"
              >
                <v-icon size="18">mdi-arrow-up</v-icon>
                <v-tooltip activator="parent" location="top">Yukarı Taşı</v-tooltip>
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                :disabled="props.disabled || index >= pipeline.length - 1"
                @click="moveStageDown(index)"
              >
                <v-icon size="18">mdi-arrow-down</v-icon>
                <v-tooltip activator="parent" location="top">Aşağı Taşı</v-tooltip>
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                color="error"
                :disabled="props.disabled"
                @click="removeStage(index)"
              >
                <v-icon size="18">mdi-delete</v-icon>
                <v-tooltip activator="parent" location="top">Sil</v-tooltip>
              </v-btn>
            </div>
          </v-card-title>
          <v-card-text class="pa-3">
            <v-textarea
              :model-value="getStageJson(stage)"
              @update:model-value="updateStageJson(index, $event)"
              :label="`Stage ${index + 1} (JSON)`"
              variant="outlined"
              density="compact"
              rows="4"
              :disabled="props.disabled"
              :error="!getStageType(stage)"
              :error-messages="!getStageType(stage) ? 'Geçersiz stage formatı' : undefined"
            />
          </v-card-text>
        </v-card>
      </div>
    </div>

    <!-- Empty State -->
    <v-alert
      v-else
      type="info"
      variant="tonal"
      density="compact"
      class="mb-4"
    >
      {{ lbl('empty') || 'Henüz stage eklenmemiş. Aşağıdan stage ekleyin.' }}
    </v-alert>

    <!-- Add Stage Button -->
    <v-menu>
      <template #activator="{ props: menuProps }">
        <v-btn
          v-bind="menuProps"
          color="primary"
          variant="flat"
          :disabled="props.disabled"
        >
          <v-icon start>mdi-plus</v-icon>
          {{ lbl('addStage') || 'Stage Ekle' }}
        </v-btn>
      </template>
      <v-list>
        <v-list-item
          v-for="stageType in stageTypes"
          :key="stageType.value"
          @click="addStage(stageType.value)"
        >
          <template #prepend>
            <v-icon>{{ stageType.icon }}</v-icon>
          </template>
          <v-list-item-title>{{ stageType.title }}</v-list-item-title>
        </v-list-item>
      </v-list>
    </v-menu>

    <!-- JSON Preview -->
    <v-expansion-panels variant="accordion" class="mt-4">
      <v-expansion-panel>
        <v-expansion-panel-title>
          <v-icon start>mdi-code-json</v-icon>
          {{ lbl('preview') || 'JSON Önizleme' }}
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <pre class="text-caption bg-grey-lighten-4 pa-3 rounded" style="max-height: 300px; overflow-y: auto;">{{ JSON.stringify(pipeline, null, 2) }}</pre>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>
  </div>
</template>

<style scoped>
.aggregate-pipeline-builder {
  width: 100%;
}
</style>
