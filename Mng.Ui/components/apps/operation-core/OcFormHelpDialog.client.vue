<script setup lang="ts">
import { computed } from 'vue';
import DiMarkdownViewer from '@/components/apps/document-intelligence/DiMarkdownViewer.vue';
import { useAppI18n } from '@/composables/useAppI18n';

defineOptions({ name: 'OcFormHelpDialog' });

const props = defineProps<{
  modelValue: boolean;
  title?: string | null;
  markdown: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const dialogTitle = computed(() => {
  const name = props.title?.trim();
  const base = t('operationCore.formHelp.title');
  return name ? `${base} — ${name}` : base;
});
</script>

<template>
  <v-dialog v-model="open" max-width="720" scrollable>
    <v-card rounded="xl">
      <v-card-title class="d-flex align-center ga-2 py-4 px-5">
        <v-icon icon="mdi-help-circle-outline" color="primary" />
        <span class="text-h6 font-weight-bold text-truncate">{{ dialogTitle }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" @click="open = false" />
      </v-card-title>
      <v-divider />
      <v-card-text class="px-5 py-4">
        <DiMarkdownViewer
          :content="markdown"
          :empty-label="t('operationCore.formHelp.empty')"
        />
      </v-card-text>
      <v-divider />
      <v-card-actions class="px-5 py-3">
        <v-spacer />
        <v-btn variant="tonal" color="primary" class="text-none" @click="open = false">
          {{ t('operationCore.formHelp.close') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
