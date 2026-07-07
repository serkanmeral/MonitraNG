<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { diListTags } from '@/services/documentIntelligenceService';
import type { DiTag } from '@/types/apps/documentIntelligence';
import { isTmStatusThemeColor } from '@/utils/taskManagerStatusColor';

const props = withDefaults(
  defineProps<{
    modelValue: string[];
    readonly?: boolean;
    clickable?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
    activeOnly?: boolean;
  }>(),
  {
    modelValue: () => [],
    readonly: false,
    clickable: false,
    density: 'comfortable',
    activeOnly: true,
  }
);

const emit = defineEmits<{
  'update:modelValue': [string[]];
  'tag-click': [string];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const catalog = ref<DiTag[]>([]);
const loading = ref(false);
const loadError = ref<string | null>(null);

const tags = computed({
  get: () => props.modelValue ?? [],
  set: (value: string[]) => emit('update:modelValue', value),
});

const tagByLowerName = computed(() => {
  const map = new Map<string, DiTag>();
  for (const tag of catalog.value) map.set(tag.name.trim().toLowerCase(), tag);
  return map;
});

const selectableItems = computed(() =>
  catalog.value
    .filter((tag) => !props.activeOnly || tag.isActive)
    .map((tag) => tag.name)
    .sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }))
);

function chipColor(name: string): string | undefined {
  const tag = tagByLowerName.value.get(name.trim().toLowerCase());
  const color = tag?.color?.trim();
  return color && isTmStatusThemeColor(color) ? color : undefined;
}

async function loadCatalog() {
  loading.value = true;
  loadError.value = null;
  try {
    const res = await diListTags(false);
    catalog.value = res.items;
  } catch (e: unknown) {
    loadError.value = panelError(e, 'documentIntelligence.tags.loadError');
    catalog.value = [];
  } finally {
    loading.value = false;
  }
}

function onTagClick(tag: string) {
  if (props.clickable) emit('tag-click', tag);
}

onMounted(() => void loadCatalog());
watch(() => props.activeOnly, () => void loadCatalog());
</script>

<template>
  <div class="di-tag-picker">
    <v-autocomplete
      v-if="!readonly"
      v-model="tags"
      :items="selectableItems"
      :label="t('documentIntelligence.tags.label')"
      :placeholder="t('documentIntelligence.tags.catalogPlaceholder')"
      :hint="t('documentIntelligence.tags.catalogHint')"
      :loading="loading"
      variant="outlined"
      :density="density"
      persistent-hint
      hide-details="auto"
      multiple
      chips
      closable-chips
      clearable
    >
      <template #chip="{ props: chipProps, item }">
        <v-chip
          v-bind="chipProps"
          size="small"
          variant="tonal"
          :color="chipColor(String(item.raw)) ?? 'primary'"
        />
      </template>
    </v-autocomplete>

    <v-alert v-else-if="loadError" type="warning" variant="tonal" density="compact" class="mb-2">
      {{ loadError }}
    </v-alert>

    <div v-if="readonly && tags.length" class="d-flex flex-wrap ga-1">
      <v-chip
        v-for="tag in tags"
        :key="tag"
        size="small"
        variant="tonal"
        :color="chipColor(tag) ?? 'primary'"
        :class="{ 'di-tag-picker__chip--clickable': clickable }"
        @click="onTagClick(tag)"
      >
        {{ tag }}
      </v-chip>
    </div>
    <span v-else-if="readonly" class="text-caption text-medium-emphasis">
      {{ t('documentIntelligence.tags.empty') }}
    </span>
  </div>
</template>

<style scoped>
.di-tag-picker__chip--clickable {
  cursor: pointer;
}
</style>
