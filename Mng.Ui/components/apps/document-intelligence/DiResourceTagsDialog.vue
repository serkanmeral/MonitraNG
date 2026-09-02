<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import DiTagPicker from '@/components/apps/document-intelligence/DiTagPicker.vue';
import { diListTags, diUpdateResourceMetadata } from '@/services/documentIntelligenceService';
import type { DiResource, DiTag } from '@/types/apps/documentIntelligence';

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
const classificationId = ref<string | null>(null);
const classOptions = ref<DiTag[]>([]);
const saving = ref(false);
const error = ref<string | null>(null);

const classItems = computed(() => [
  { title: t('documentIntelligence.classification.none'), value: null as string | null },
  ...classOptions.value.map((c) => ({ title: c.name, value: c.id })),
]);

watch(
  () => props.modelValue,
  async (isOpen) => {
    open.value = isOpen;
    if (isOpen && props.resource) {
      editTags.value = [...(props.resource.tags ?? [])];
      classificationId.value = props.resource.classificationTagId;
      error.value = null;
      try {
        const res = await diListTags(true, 'classification');
        classOptions.value = res.items;
      } catch {
        classOptions.value = [];
      }
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
    const updated = await diUpdateResourceMetadata(props.resource.id, {
      tags: editTags.value,
      classificationTagId: classificationId.value ?? '',
    });
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
        <v-select
          v-model="classificationId"
          :items="classItems"
          item-title="title"
          item-value="value"
          :label="t('documentIntelligence.classification.label')"
          variant="outlined"
          density="comfortable"
          clearable
          class="mb-3"
        />
        <DiTagPicker v-model="editTags" density="comfortable" kind="organizational" />
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
