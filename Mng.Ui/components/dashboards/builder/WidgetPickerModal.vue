<script setup lang="ts">
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

const isOpen = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
});

const search = ref('');
const selectedCategory = ref<string | 'all'>('all');

// TODO: @widgets dataset hazır olunca buraya bağlanacak
// Şimdilik placeholder widget listesi
const widgets = ref<Array<{ id: string; name: string; title: string; category: string; type: string }>>([]);

const filteredWidgets = computed(() => {
  let result = widgets.value;
  if (search.value.trim()) {
    const q = search.value.toLowerCase();
    result = result.filter((w) => w.name.toLowerCase().includes(q) || w.title.toLowerCase().includes(q));
  }
  if (selectedCategory.value !== 'all') {
    result = result.filter((w) => w.category === selectedCategory.value);
  }
  return result;
});

const categories = computed(() => {
  const cats = new Set(widgets.value.map((w) => w.category));
  return Array.from(cats).sort();
});

function selectWidget(widgetId: string) {
  emit('select', widgetId);
  isOpen.value = false;
}

function close() {
  isOpen.value = false;
  search.value = '';
  selectedCategory.value = 'all';
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
            style="flex: 1; min-width: 200px;"
          />
          <v-select
            v-model="selectedCategory"
            :items="[{ value: 'all', title: lbl('allCategories') }, ...categories.map((c) => ({ value: c, title: c }))]"
            :label="lbl('category')"
            variant="outlined"
            density="compact"
            hide-details
            style="max-width: 180px;"
          />
        </div>

        <!-- Widget list -->
        <div v-if="filteredWidgets.length" class="widget-list">
          <v-card
            v-for="widget in filteredWidgets"
            :key="widget.id"
            variant="outlined"
            class="mb-2 widget-item"
            :class="{ 'border-primary': false }"
            @click="selectWidget(widget.id)"
          >
            <v-card-text class="pa-3">
              <div class="d-flex align-center justify-space-between">
                <div class="d-flex align-center ga-3" style="flex: 1;">
                  <v-icon color="primary" size="24">mdi-widgets</v-icon>
                  <div>
                    <div class="text-subtitle-2 font-weight-medium">{{ widget.title }}</div>
                    <div class="text-caption text-medium-emphasis">{{ widget.name }}</div>
                  </div>
                </div>
                <div class="d-flex align-center ga-2">
                  <v-chip size="small" variant="tonal" color="info">{{ widget.type }}</v-chip>
                  <v-chip size="small" variant="tonal" color="secondary">{{ widget.category }}</v-chip>
                  <v-btn size="small" variant="flat" color="primary" @click.stop="selectWidget(widget.id)">
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
          <p class="text-caption text-medium-emphasis">{{ lbl('noWidgetsHint') }}</p>
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
