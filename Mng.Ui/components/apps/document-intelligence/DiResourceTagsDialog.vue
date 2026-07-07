<script setup lang="ts">
import { ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import DiTagPicker from '@/components/apps/document-intelligence/DiTagPicker.vue';
import { diUpdateResourceMetadata } from '@/services/documentIntelligenceService';
import type { DiResource } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  modelValue: boolean;
  resource: DiResource | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  saved: [resource: DiResource];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = ref(false);
const editTags = ref<string[]>([]);
const saving = ref(false);
const error = ref<string | null>(null);

watch(
  () => props.modelValue,
  (isOpen) => {
    open.value = isOpen;
    if (isOpen && props.resource) {
      editTags.value = [...(props.resource.tags ?? [])];
      error.value = null;
    }
  },
  { immediate: true }
);

watch(open, (v) => emit('update:modelValue', v));

async function save() {
  if (!props.resource) return;
  saving.value = true;
  error.value = null;
  try {
    const updated = await diUpdateResourceMetadata(props.resource.id, { tags: editTags.value });
    emit('saved', updated);
    open.value = false;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.tags.saveError');
  } finally {
    saving.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="460">
    <v-card v-if="resource" rounded="lg">
      <v-card-title class="text-subtitle-1 font-weight-bold">
        {{ t('documentIntelligence.tags.editTitle') }}
      </v-card-title>
      <v-card-text>
        <p class="text-caption text-medium-emphasis mb-3">
          {{ t('documentIntelligence.tags.editHint') }}
        </p>
        <DiTagPicker v-model="editTags" density="comfortable" />
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">
          {{ error }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" class="text-none" :disabled="saving" @click="open = false">
          {{ t('documentIntelligence.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" class="text-none" :loading="saving" @click="save">
          {{ t('documentIntelligence.save') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
