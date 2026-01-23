<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useWidgetStore, type Widget, type WidgetCategory } from '@/stores/apps/widget';

/** Widget Picker Modal - Widget seçimi için modal dialog */
const props = defineProps<{
  modelValue: boolean;
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  'select': [widgetId: string];
}>();

const widgetStore = useWidgetStore();

const isOpen = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
});

const search = ref('');
const selectedCategory = ref<string | 'all'>('all');
const selectedType = ref<'card' | 'chart' | 'table' | 'banner' | 'all'>('all');

// Load widgets and categories when modal opens
watch(isOpen, async (open) => {
  if (open) {
    try {
      await widgetStore.fetchWidgetCategories();
      await widgetStore.fetchWidgets({ limit: 100, filter: 'isActive:eq:true' });
    } catch (e) {
      console.error('Widget listesi yüklenirken hata:', e);
    }
  }
});

const filteredWidgets = computed(() => {
  let result = widgetStore.activeWidgets;

  // Search filter
  if (search.value.trim()) {
    const q = search.value.toLowerCase();
    result = result.filter((w) => {
      const name = (w.name ?? '').toLowerCase();
      const title = (w.title ?? '').toLowerCase();
      const description = (w.description ?? '').toLowerCase();
      return name.includes(q) || title.includes(q) || description.includes(q);
    });
  }

  // Category filter
  if (selectedCategory.value !== 'all') {
    result = result.filter((w) => {
      const cat = typeof w.category === 'string' ? w.category : w.category.__dataId ?? w.category.dataId;
      return cat === selectedCategory.value;
    });
  }

  // Type filter
  if (selectedType.value !== 'all') {
    result = result.filter((w) => w.type === selectedType.value);
  }

  return result;
});

const categories = computed(() => {
  return widgetStore.activeCategories.map((cat) => ({
    value: cat.__dataId ?? cat.dataId ?? '',
    title: cat.name,
    icon: cat.icon,
    color: cat.color,
  }));
});

const typeOptions = [
  { value: 'all' as const, title: 'Tümü' },
  { value: 'card' as const, title: 'Card' },
  { value: 'chart' as const, title: 'Chart' },
  { value: 'table' as const, title: 'Table' },
  { value: 'banner' as const, title: 'Banner' },
];

function selectWidget(widgetId: string) {
  emit('select', widgetId);
  isOpen.value = false;
}

function close() {
  isOpen.value = false;
  search.value = '';
  selectedCategory.value = 'all';
  selectedType.value = 'all';
}

function getWidgetCategoryName(widget: Widget): string {
  if (typeof widget.category === 'string') {
    const cat = widgetStore.getCategoryById(widget.category);
    return cat?.name ?? widget.category;
  }
  return widget.category.name ?? '';
}

const lbl = (key: string) => props.t?.(`dashboards.builder.widgetPicker.${key}`) ?? key;
</script>

<template>
  <v-dialog v-model="isOpen" max-width="800px" persistent scrollable>
    <v-card>
      <v-card-title class="d-flex align-center pa-4 bg-primary text-white">
        <v-icon class="mr-2" color="white">mdi-widgets</v-icon>
        <span>{{ lbl('title') }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" color="white" :disabled="disabled" @click="close" />
      </v-card-title>

      <v-divider />

      <v-card-text class="pa-4">
        <!-- Search and filters -->
        <div class="d-flex flex-wrap ga-3 mb-4">
          <v-text-field
            v-model="search"
            prepend-inner-icon="mdi-magnify"
            :label="lbl('search')"
            :placeholder="lbl('searchPlaceholder')"
            variant="outlined"
            density="compact"
            hide-details
            clearable
            :disabled="disabled || widgetStore.loading"
            style="flex: 1; min-width: 200px;"
          />
          <v-select
            v-model="selectedCategory"
            :items="[{ value: 'all', title: lbl('allCategories') }, ...categories]"
            item-title="title"
            item-value="value"
            :label="lbl('category')"
            variant="outlined"
            density="compact"
            hide-details
            :disabled="disabled || widgetStore.loading"
            style="max-width: 180px;"
          />
          <v-select
            v-model="selectedType"
            :items="typeOptions"
            item-title="title"
            item-value="value"
            label="Tip"
            variant="outlined"
            density="compact"
            hide-details
            :disabled="disabled || widgetStore.loading"
            style="max-width: 140px;"
          />
        </div>

        <!-- Loading state -->
        <div v-if="widgetStore.loading" class="d-flex justify-center align-center py-8">
          <v-progress-circular indeterminate color="primary" size="32" />
        </div>

        <!-- Widget list -->
        <div v-else-if="filteredWidgets.length" class="widget-list">
          <v-card
            v-for="widget in filteredWidgets"
            :key="widget.__dataId ?? widget.dataId"
            variant="outlined"
            class="mb-2 widget-item"
            @click="selectWidget(widget.__dataId ?? widget.dataId ?? '')"
          >
            <v-card-text class="pa-3">
              <div class="d-flex align-center justify-space-between">
                <div class="d-flex align-center ga-3" style="flex: 1;">
                  <v-icon color="primary" size="24">mdi-widgets</v-icon>
                  <div>
                    <div class="text-subtitle-2 font-weight-medium">{{ widget.title }}</div>
                    <div class="text-caption text-medium-emphasis">{{ widget.name }}</div>
                    <div v-if="widget.description" class="text-caption text-disabled mt-1">
                      {{ widget.description }}
                    </div>
                  </div>
                </div>
                <div class="d-flex align-center ga-2">
                  <v-chip size="small" variant="tonal" color="info">{{ widget.type }}</v-chip>
                  <v-chip size="small" variant="tonal" color="secondary">
                    {{ getWidgetCategoryName(widget) }}
                  </v-chip>
                  <v-btn
                    size="small"
                    variant="flat"
                    color="primary"
                    :disabled="disabled"
                    @click.stop="selectWidget(widget.__dataId ?? widget.dataId ?? '')"
                  >
                    {{ lbl('select') }}
                  </v-btn>
                </div>
              </div>
            </v-card-text>
          </v-card>
        </div>

        <!-- Empty state -->
        <div v-else class="text-center py-8">
          <v-icon size="48" color="grey-lighten-1">mdi-widgets-outline</v-icon>
          <p class="text-subtitle-1 text-medium-emphasis mt-4">{{ lbl('noWidgets') }}</p>
          <p class="text-caption text-medium-emphasis">
            {{ widgetStore.error || lbl('noWidgetsHint') }}
          </p>
        </div>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="disabled" @click="close">{{ lbl('cancel') }}</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.widget-item {
  cursor: pointer;
  transition: all 0.2s;
}

.widget-item:hover {
  background-color: rgba(var(--v-theme-primary), 0.05);
  border-color: rgb(var(--v-theme-primary));
}
</style>
