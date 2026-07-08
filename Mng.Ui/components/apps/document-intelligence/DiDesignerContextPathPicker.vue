<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { DiDocumentContextField } from '@/types/apps/documentIntelligence';
import {
  buildContextPathTree,
  contextPathTreeToVuetifyItems,
  filterContextPathTree,
} from '@/utils/diDesignerContextPaths';

const props = defineProps<{
  modelValue: string;
  fields: DiDocumentContextField[];
  label: string;
  clearable?: boolean;
  disabled?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: string];
}>();

const { t } = useAppI18n();
const search = ref('');
const activated = ref<string[]>([]);

const treeItems = computed(() => {
  const tree = buildContextPathTree(props.fields);
  const filtered = filterContextPathTree(tree, search.value);
  return contextPathTreeToVuetifyItems(filtered);
});

const flatOptions = computed(() =>
  props.fields.map((field) => ({
    title: `${field.label} — ${field.path}`,
    value: field.path,
  }))
);

watch(
  () => props.modelValue,
  (path) => {
    activated.value = path ? [path] : [];
  },
  { immediate: true }
);

function onActivated(values: unknown) {
  const list = Array.isArray(values) ? values.map(String) : [];
  const next = list.find((value) => props.fields.some((field) => field.path === value)) ?? list[0] ?? '';
  emit('update:modelValue', next);
}
</script>

<template>
  <div>
    <v-text-field
      v-model="search"
      :label="t('documentIntelligence.designer.parameterStudio.contextSearch')"
      density="compact"
      variant="outlined"
      hide-details
      clearable
      prepend-inner-icon="mdi-magnify"
      class="mb-2"
      :disabled="disabled"
    />

    <v-treeview
      v-if="treeItems.length"
      :items="treeItems"
      :activated="activated"
      activatable
      density="compact"
      open-all
      class="di-context-path-tree rounded-lg border pa-1 mb-2"
      :disabled="disabled"
      @update:activated="onActivated"
    />

    <v-select
      :model-value="modelValue"
      :items="flatOptions"
      :label="label"
      density="compact"
      variant="outlined"
      hide-details
      :clearable="clearable"
      :disabled="disabled"
      @update:model-value="emit('update:modelValue', $event ?? '')"
    />
  </div>
</template>

<style scoped>
.di-context-path-tree {
  max-height: 220px;
  overflow: auto;
  border-radius: 8px;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
