<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { diListResourceKinds } from '@/services/documentIntelligenceService';
import type { DiResourceKind } from '@/types/apps/documentIntelligence';

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
const options = ref<DiResourceKind[]>([]);

function kindLabel(code: string | null | undefined): string {
  const value = code?.trim();
  if (!value) return t('documentIntelligence.resourceKind.none');
  const fromCatalog = options.value.find((k) => k.code === value);
  if (fromCatalog?.displayName) return fromCatalog.displayName;
  const key = `documentIntelligence.resourceKinds.${value}`;
  const label = t(key);
  return label === key ? value : label;
}

const items = computed(() => [
  { title: t('documentIntelligence.resourceKind.none'), value: null as string | null },
  ...options.value.map((k) => ({ title: k.displayName || kindLabel(k.code), value: k.code })),
]);

const selectedLabel = computed(() => kindLabel(props.modelValue));

onMounted(async () => {
  try {
    const res = await diListResourceKinds(true);
    options.value = res.items.filter((k) => k.code);
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
    :color="modelValue ? 'primary' : undefined"
    class="text-none"
  >
    {{ t('documentIntelligence.resourceKind.label') }}: {{ selectedLabel }}
  </v-chip>
  <v-select
    v-else
    :model-value="modelValue"
    :items="items"
    item-title="title"
    item-value="value"
    :label="t('documentIntelligence.resourceKind.label')"
    variant="outlined"
    :density="density"
    clearable
    hide-details
    class="mb-2"
    @update:model-value="emit('update:modelValue', $event ?? null)"
  />
</template>
