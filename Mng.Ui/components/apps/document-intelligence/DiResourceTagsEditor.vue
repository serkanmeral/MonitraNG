<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';

const props = withDefaults(
  defineProps<{
    modelValue: string[];
    readonly?: boolean;
    clickable?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
  }>(),
  {
    modelValue: () => [],
    readonly: false,
    clickable: false,
    density: 'comfortable',
  }
);

const emit = defineEmits<{
  'update:modelValue': [string[]];
  'tag-click': [string];
}>();

const { t } = useAppI18n();

const tags = computed({
  get: () => props.modelValue ?? [],
  set: (value: string[]) => {
    const normalized = value
      .map((tag) => tag.trim())
      .filter((tag, index, arr) => tag.length > 0 && arr.indexOf(tag) === index);
    emit('update:modelValue', normalized);
  },
});

function onTagClick(tag: string) {
  if (props.clickable) emit('tag-click', tag);
}
</script>

<template>
  <div class="di-resource-tags">
    <v-combobox
      v-if="!readonly"
      v-model="tags"
      :label="t('documentIntelligence.tags.label')"
      :placeholder="t('documentIntelligence.tags.placeholder')"
      variant="outlined"
      :density="density"
      hide-details
      multiple
      chips
      closable-chips
      clearable
    />
    <div v-else-if="tags.length" class="d-flex flex-wrap ga-1">
      <v-chip
        v-for="tag in tags"
        :key="tag"
        size="small"
        variant="tonal"
        color="primary"
        :class="{ 'di-resource-tags__chip--clickable': clickable }"
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
.di-resource-tags__chip--clickable {
  cursor: pointer;
}
</style>
