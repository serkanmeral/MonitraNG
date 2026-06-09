<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import { useWidgetStore } from '@/stores/apps/widget';
import type { WidgetTemplateRecord } from '@/types/apps/widgetManifest';
import {
  WIDGET_DESIGNER_DOMAINS,
  PRESENTATION_KIND_ICONS,
  widgetModulesForDesigner,
} from '@/utils/widgets/widgetDesignerHelpers';
import { useAppI18n } from '@/composables/useAppI18n';
import { normalizeTemplateRecord, pickLocalized } from '@/utils/widgets/widgetManifestAdapter';

const props = defineProps<{
  selectedTemplateId?: string | null;
  /** Tek domain kilitle (MO editörü gibi) */
  domainFilter?: import('@/types/apps/widgetManifest').WidgetDomain;
  t?: (key: string) => string;
  compact?: boolean;
}>();

const emit = defineEmits<{
  select: [record: WidgetTemplateRecord];
}>();

const widgetStore = useWidgetStore();
const { locale } = useAppI18n();
const search = ref('');
const selectedDomain = ref<string>(props.domainFilter ?? 'all');

const moduleFilters = computed(() => [
  { domain: 'all', label: lbl('allModules'), icon: 'mdi-view-grid' },
  ...widgetModulesForDesigner(locale.value),
]);

const lbl = (key: string) => props.t?.(`widgets.designer.catalog.${key}`) ?? key;

async function loadTemplates() {
  await widgetStore.fetchWidgetTemplates({ activeOnly: true, limit: 100 });
}

onMounted(loadTemplates);

watch(selectedDomain, loadTemplates);

const effectiveDomain = computed(
  () => props.domainFilter ?? selectedDomain.value,
);

const filteredTemplates = computed(() => {
  let items = widgetStore.templates.filter((t) => t.isActive);

  if (effectiveDomain.value !== 'all') {
    items = items.filter((t) => t.domain === effectiveDomain.value);
  }

  if (search.value.trim()) {
    const q = search.value.toLowerCase();
    items = items.filter((t) => {
      const manifest = normalizeTemplateRecord(t);
      const title = pickLocalized(manifest.title).toLowerCase();
      const desc = pickLocalized(manifest.description).toLowerCase();
      const tags = (t.tags ?? manifest.tags ?? []).join(' ').toLowerCase();
      return (
        t.templateId.toLowerCase().includes(q) ||
        title.includes(q) ||
        desc.includes(q) ||
        tags.includes(q)
      );
    });
  }

  return items.sort((a, b) => (a.order ?? 0) - (b.order ?? 0));
});

function selectTemplate(record: WidgetTemplateRecord) {
  emit('select', record);
}

function kindIcon(record: WidgetTemplateRecord): string {
  const kind = record.manifest?.presentation?.kind ?? 'stat';
  return PRESENTATION_KIND_ICONS[kind] ?? 'mdi-widgets';
}
</script>

<template>
  <div>
    <div v-if="!domainFilter" class="d-flex flex-wrap ga-2 mb-3">
      <v-chip
        v-for="mod in moduleFilters"
        :key="mod.domain"
        :color="selectedDomain === mod.domain ? 'primary' : undefined"
        :variant="selectedDomain === mod.domain ? 'flat' : 'outlined'"
        size="small"
        :prepend-icon="mod.icon"
        @click="selectedDomain = mod.domain"
      >
        {{ mod.label }}
      </v-chip>
    </div>

    <v-text-field
      v-model="search"
      prepend-inner-icon="mdi-magnify"
      :label="lbl('search')"
      :placeholder="lbl('searchPlaceholder')"
      variant="outlined"
      density="compact"
      hide-details
      clearable
      class="mb-4"
    />

    <div v-if="widgetStore.loading" class="d-flex justify-center py-8">
      <v-progress-circular indeterminate color="primary" />
    </div>

    <div v-else-if="filteredTemplates.length" class="template-grid">
      <v-card
        v-for="record in filteredTemplates"
        :key="record.templateId"
        variant="outlined"
        :class="[
          'template-card',
          { 'template-card--selected': selectedTemplateId === record.templateId },
        ]"
        @click="selectTemplate(record)"
      >
        <v-card-text :class="compact ? 'pa-3' : 'pa-4'">
          <div class="d-flex align-start ga-3">
            <v-avatar :size="compact ? 36 : 44" color="primary" variant="tonal">
              <v-icon :icon="kindIcon(record)" />
            </v-avatar>
            <div class="flex-grow-1 min-width-0">
              <div class="text-subtitle-2 font-weight-medium text-truncate">
                {{ pickLocalized(normalizeTemplateRecord(record).title) }}
              </div>
              <div class="text-caption text-medium-emphasis text-truncate">
                {{ record.templateId }}
                <span v-if="record.domain" class="text-disabled"> · {{ record.domain }}</span>
              </div>
              <div
                v-if="record.description || normalizeTemplateRecord(record).description"
                class="text-caption text-disabled mt-1"
                :class="{ 'text-truncate': compact }"
              >
                {{ pickLocalized(normalizeTemplateRecord(record).description) }}
              </div>
              <div v-if="record.tags?.length" class="d-flex flex-wrap ga-1 mt-2">
                <v-chip v-for="tag in record.tags" :key="tag" size="x-small" variant="tonal">
                  {{ tag }}
                </v-chip>
              </div>
            </div>
            <v-icon
              v-if="selectedTemplateId === record.templateId"
              color="primary"
              icon="mdi-check-circle"
            />
          </div>
        </v-card-text>
      </v-card>
    </div>

    <div v-else class="text-center py-8">
      <v-icon size="48" color="grey-lighten-1">mdi-puzzle-outline</v-icon>
      <p class="text-subtitle-2 text-medium-emphasis mt-3">{{ lbl('empty') }}</p>
    </div>
  </div>
</template>

<style scoped>
.template-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
}

.template-card {
  cursor: pointer;
  transition: border-color 0.2s, background-color 0.2s;
}

.template-card:hover {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.04);
}

.template-card--selected {
  border-color: rgb(var(--v-theme-primary));
  background-color: rgba(var(--v-theme-primary), 0.08);
}

.min-width-0 {
  min-width: 0;
}
</style>
