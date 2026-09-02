<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diListTags } from '@/services/documentIntelligenceService';
import type { DiTag } from '@/types/apps/documentIntelligence';

const props = withDefaults(
  defineProps<{
    modelValue: string | null;
    readonly?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
  }>(),
  {
    modelValue: null,
    readonly: false,
    density: 'comfortable',
  }
);

const emit = defineEmits<{
  'update:modelValue': [string | null];
}>();

const { t } = useAppI18n();
const options = ref<DiTag[]>([]);

const items = computed(() => [
  { title: t('documentIntelligence.classification.none'), value: null as string | null },
  ...options.value.map((c) => ({ title: c.name, value: c.id })),
]);

const selectedLabel = computed(() => {
  if (!props.modelValue) return t('documentIntelligence.classification.none');
  return options.value.find((c) => c.id === props.modelValue)?.name
    ?? t('documentIntelligence.classification.label');
});

onMounted(async () => {
  try {
    const res = await diListTags(true, 'classification');
    options.value = res.items;
  } catch {
    options.value = [];
  }
});
</script>

<template>
  <v-chip
    v-if="readonly"
    size="small"
    variant="tonal"
    :color="modelValue ? 'warning' : undefined"
    class="text-none"
  >
    {{ t('documentIntelligence.classification.label') }}: {{ selectedLabel }}
  </v-chip>
  <v-select
    v-else
    :model-value="modelValue"
    :items="items"
    item-title="title"
    item-value="value"
    :label="t('documentIntelligence.classification.label')"
    variant="outlined"
    :density="density"
    clearable
    hide-details
    class="mb-2"
    @update:model-value="emit('update:modelValue', $event ?? null)"
  />
</template>
