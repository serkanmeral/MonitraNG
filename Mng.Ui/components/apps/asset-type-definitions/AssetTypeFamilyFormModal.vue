<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import type { MonAssetTypeFamily } from '@/types/apps/assetTypeDefinitions';

const props = defineProps<{
  modelValue: boolean;
  family: MonAssetTypeFamily | Partial<MonAssetTypeFamily> | null;
  loading?: boolean;
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [v: boolean];
  save: [data: Partial<MonAssetTypeFamily>];
}>();

const form = ref<Partial<MonAssetTypeFamily>>({
  name: '',
  code: null,
  description: null,
});

watch(
  () => props.family,
  (v) => {
    if (v) {
      form.value = {
        name: v.name ?? '',
        code: v.code ?? null,
        description: v.description ?? null,
      };
      if ('__dataId' in v && v.__dataId) (form.value as any).__dataId = v.__dataId;
    } else {
      form.value = { name: '', code: null, description: null };
    }
  },
  { immediate: true }
);

const isEdit = computed(() => !!(props.family && '__dataId' in props.family && (props.family as any).__dataId));

function save() {
  const name = (form.value.name ?? '').trim();
  if (!name) return;
  emit('save', { ...form.value, name });
  emit('update:modelValue', false);
}

function close() {
  emit('update:modelValue', false);
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="500" persistent @update:model-value="(v) => emit('update:modelValue', v)">
    <v-card>
      <v-card-title>{{ isEdit ? 'Aile düzenle' : 'Yeni aile' }}</v-card-title>
      <v-card-text>
        <v-text-field
          v-model="form.name"
          label="Aile adı *"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
        />
        <v-text-field
          v-model="form.code"
          label="Kod (slug)"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
        />
        <v-textarea
          v-model="form.description"
          label="Açıklama"
          variant="outlined"
          density="comfortable"
          rows="2"
          hide-details
        />
      </v-card-text>
      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="close">İptal</v-btn>
        <v-btn v-if="canEdit" color="primary" variant="flat" :loading="loading" @click="save">Kaydet</v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
