<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useWidgetStore, type Widget } from '@/stores/apps/widget';
import WidgetTemplateCatalog from '@/components/widgets/designer/WidgetTemplateCatalog.vue';
import {
  createDraftFromTemplate,
  draftToCreateWidgetDto,
  resolveCategoryIdFromTemplate,
} from '@/utils/widgets/widgetDesignerHelpers';
import { buildModuleCategorySelectOptions, getWidgetCategoryDisplayName } from '@/utils/widgets/widgetCategoryDomains';
import { useLocaleStore } from '@/stores/locale';
import type { WidgetTemplateRecord } from '@/types/apps/widgetManifest';
import { pickLocalized, normalizeTemplateRecord } from '@/utils/widgets/widgetManifestAdapter';

/** Widget Picker Modal — mevcut widget veya şablondan oluştur */
const props = defineProps<{
  modelValue: boolean;
  disabled?: boolean;
  t?: (key: string) => string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  select: [widgetId: string];
}>();

const widgetStore = useWidgetStore();
const localeStore = useLocaleStore();

const isOpen = computed({
  get: () => props.modelValue,
  set: (v) => emit('update:modelValue', v),
});

const activeTab = ref<'existing' | 'template'>('existing');
const search = ref('');
const selectedCategory = ref<string | 'all'>('all');
const selectedType = ref<'card' | 'chart' | 'table' | 'banner' | 'all'>('all');

const pendingTemplate = ref<WidgetTemplateRecord | null>(null);
const quickName = ref('');
const quickTitle = ref('');
const creating = ref(false);
const createError = ref<string | null>(null);

watch(isOpen, async (open) => {
  if (open) {
    activeTab.value = 'existing';
    pendingTemplate.value = null;
    createError.value = null;
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

  if (search.value.trim()) {
    const q = search.value.toLowerCase();
    result = result.filter((w) => {
      const name = (w.name ?? '').toLowerCase();
      const title = (w.title ?? '').toLowerCase();
      const description = (w.description ?? '').toLowerCase();
      return name.includes(q) || title.includes(q) || description.includes(q);
    });
  }

  if (selectedCategory.value !== 'all') {
    result = result.filter((w) => {
      const cat = typeof w.category === 'string' ? w.category : w.category.__dataId ?? w.category.dataId;
      return cat === selectedCategory.value;
    });
  }

  if (selectedType.value !== 'all') {
    result = result.filter((w) => w.type === selectedType.value);
  }

  return result;
});

const categories = computed(() => {
  const options = [{ value: 'all' as const, title: props.t?.('widgets.list.filters.allModules') ?? 'Tüm modüller' }];
  const modules = buildModuleCategorySelectOptions(widgetStore.activeCategories, localeStore.locale);
  return [...options, ...modules];
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
  pendingTemplate.value = null;
  createError.value = null;
}

function getWidgetCategoryName(widget: Widget): string {
  if (typeof widget.category === 'string') {
    const cat = widgetStore.getCategoryById(widget.category);
    return getWidgetCategoryDisplayName(cat ?? undefined, localeStore.locale);
  }
  return getWidgetCategoryDisplayName(widget.category, localeStore.locale);
}

function onTemplatePick(record: WidgetTemplateRecord) {
  pendingTemplate.value = record;
  const draft = createDraftFromTemplate(record);
  quickName.value = draft.name;
  quickTitle.value = draft.title;
  createError.value = null;
}

function cancelQuickCreate() {
  pendingTemplate.value = null;
  createError.value = null;
}

async function confirmQuickCreate() {
  if (!pendingTemplate.value) return;
  createError.value = null;

  const draft = createDraftFromTemplate(pendingTemplate.value);
  draft.name = quickName.value.trim();
  draft.title = quickTitle.value.trim();

  if (!draft.name || !draft.title) {
    createError.value = lbl('quickCreate.validation');
    return;
  }

  const categoryId = resolveCategoryIdFromTemplate(draft.template, widgetStore.categories);
  if (!categoryId) {
    createError.value = lbl('quickCreate.categoryMissing');
    return;
  }

  creating.value = true;
  try {
    const widget = await widgetStore.createWidget(draftToCreateWidgetDto(draft, categoryId));
    const id = widget.__dataId ?? widget.dataId;
    if (!id) throw new Error('Widget oluşturulamadı');
    selectWidget(id);
  } catch (e: any) {
    createError.value = e?.message ?? lbl('quickCreate.failed');
  } finally {
    creating.value = false;
  }
}

const lbl = (key: string) => props.t?.(`dashboards.builder.widgetPicker.${key}`) ?? key;
</script>

<template>
  <v-dialog v-model="isOpen" max-width="920px" persistent scrollable>
    <v-card>
      <v-card-title class="d-flex align-center pa-4 bg-primary text-white">
        <v-icon class="mr-2" color="white">mdi-widgets</v-icon>
        <span>{{ lbl('title') }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" color="white" :disabled="disabled" @click="close" />
      </v-card-title>

      <v-tabs v-model="activeTab" bg-color="transparent" class="px-4 pt-2">
        <v-tab value="existing">{{ lbl('tabExisting') }}</v-tab>
        <v-tab value="template">{{ lbl('tabTemplate') }}</v-tab>
      </v-tabs>

      <v-divider />

      <v-card-text class="pa-4">
        <v-window v-model="activeTab">
          <v-window-item value="existing">
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
                :items="categories"
                item-title="title"
                item-value="value"
                :label="lbl('module')"
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
                :label="lbl('type')"
                variant="outlined"
                density="compact"
                hide-details
                :disabled="disabled || widgetStore.loading"
                style="max-width: 140px;"
              />
            </div>

            <div v-if="widgetStore.loading" class="d-flex justify-center align-center py-8">
              <v-progress-circular indeterminate color="primary" size="32" />
            </div>

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

            <div v-else class="text-center py-8">
              <v-icon size="48" color="grey-lighten-1">mdi-widgets-outline</v-icon>
              <p class="text-subtitle-1 text-medium-emphasis mt-4">{{ lbl('noWidgets') }}</p>
              <p class="text-caption text-medium-emphasis">
                {{ widgetStore.error || lbl('noWidgetsHint') }}
              </p>
            </div>
          </v-window-item>

          <v-window-item value="template">
            <p class="text-body-2 text-medium-emphasis mb-3">{{ lbl('templateHint') }}</p>

            <WidgetTemplateCatalog
              v-if="!pendingTemplate"
              :selected-template-id="null"
              :t="t"
              compact
              @select="onTemplatePick"
            />

            <v-sheet v-else border rounded class="pa-4">
              <div class="text-subtitle-2 mb-3">
                {{ lbl('quickCreate.title') }}:
                {{ pickLocalized(normalizeTemplateRecord(pendingTemplate).title) }}
              </div>
              <v-text-field
                v-model="quickName"
                :label="lbl('quickCreate.name')"
                variant="outlined"
                density="compact"
                class="mb-3"
              />
              <v-text-field
                v-model="quickTitle"
                :label="lbl('quickCreate.widgetTitle')"
                variant="outlined"
                density="compact"
                class="mb-3"
              />
              <v-alert v-if="createError" type="error" variant="tonal" density="compact" class="mb-3">
                {{ createError }}
              </v-alert>
              <div class="d-flex ga-2">
                <v-btn variant="text" @click="cancelQuickCreate">{{ lbl('cancel') }}</v-btn>
                <v-spacer />
                <v-btn
                  color="primary"
                  variant="flat"
                  :loading="creating"
                  :disabled="disabled"
                  @click="confirmQuickCreate"
                >
                  {{ lbl('quickCreate.confirm') }}
                </v-btn>
              </div>
            </v-sheet>
          </v-window-item>
        </v-window>
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
