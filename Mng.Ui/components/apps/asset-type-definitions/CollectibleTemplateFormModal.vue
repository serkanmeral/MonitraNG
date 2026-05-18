<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import type { MonCollectibleTemplate, CollectibleDefinition } from '@/types/apps/assetTypeDefinitions';
import CollectiblesEditor from './CollectiblesEditor.vue';

const props = defineProps<{
  modelValue: boolean;
  template: MonCollectibleTemplate | Partial<MonCollectibleTemplate> | null;
  loading?: boolean;
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [v: boolean];
  save: [data: Partial<MonCollectibleTemplate>];
}>();

const COLLECTION_METHODS = [
  { title: 'SSH', value: 'SSH' },
  { title: 'WMI', value: 'WMI' },
  { title: 'SNMP', value: 'SNMP' },
  { title: 'SNMP v3', value: 'SNMP_V3' },
  { title: 'REST', value: 'REST' },
  { title: 'HTTP', value: 'HTTP' },
  { title: 'OPC UA', value: 'OPC_UA' },
];

const form = ref<Partial<MonCollectibleTemplate>>({
  name: '',
  collection_method: '',
  description: null,
  collectibles: [],
});

const collectibles = ref<Array<CollectibleDefinition & { overridable_params_str?: string }>>([]);

watch(
  () => props.template,
  (v) => {
    if (v) {
      form.value = {
        name: v.name ?? '',
        collection_method: v.collection_method ?? '',
        description: v.description ?? null,
        collectibles: v.collectibles ?? [],
      };
      if ('__dataId' in v && v.__dataId) (form.value as any).__dataId = v.__dataId;
      const coll = (v.collectibles ?? []).map((c) => ({
        ...c,
        overridable_params_str: Array.isArray(c.overridable_params) ? c.overridable_params.join(', ') : '',
      }));
      collectibles.value = coll.length ? coll : [{ code: '', name: '', data_type: 'number', overridable_params_str: '' }];
    } else {
      form.value = { name: '', collection_method: '', description: null, collectibles: [] };
      collectibles.value = [{ code: '', name: '', data_type: 'number', overridable_params_str: '' }];
    }
  },
  { immediate: true }
);

const isEdit = computed(() => !!(props.template && '__dataId' in props.template && (props.template as any).__dataId));

function buildCollectibles(): CollectibleDefinition[] {
  return collectibles.value
    .map((c) => {
      const code = (c.code ?? '').trim();
      if (!code) return null;
      const overridable_params_str = (c as any).overridable_params_str ?? '';
      const overridable_params =
        typeof overridable_params_str === 'string' && overridable_params_str.trim()
          ? overridable_params_str.split(',').map((s) => s.trim()).filter(Boolean)
          : undefined;
      return {
        code,
        name: (c.name ?? '').trim() || undefined,
        data_type: c.data_type || 'number',
        metric_key: (c.metric_key ?? '').trim() || undefined,
        oid: (c.oid ?? '').trim() || undefined,
        path: (c.path ?? '').trim() || undefined,
        overridable_params: overridable_params?.length ? overridable_params : undefined,
      };
    })
    .filter(Boolean) as CollectibleDefinition[];
}

function save() {
  const name = (form.value.name ?? '').trim();
  const collection_method = (form.value.collection_method ?? '').trim();
  if (!name || !collection_method) return;
  const built = buildCollectibles();
  if (built.length === 0) return;
  emit('save', { ...form.value, name, collection_method, collectibles: built });
  emit('update:modelValue', false);
}

function close() {
  emit('update:modelValue', false);
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="840" persistent scrollable @update:model-value="(v) => emit('update:modelValue', v)">
    <v-card>
      <v-card-title>{{ isEdit ? 'Şablon düzenle' : 'Yeni şablon' }}</v-card-title>
      <v-card-text class="pb-0">
        <v-text-field
          v-model="form.name"
          label="Şablon adı *"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
        />
        <v-select
          v-model="form.collection_method"
          :items="COLLECTION_METHODS"
          item-title="title"
          item-value="value"
          label="Toplama metodu *"
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
          class="mb-4"
          hide-details
        />
        <CollectiblesEditor
          v-model="collectibles"
          :collection-method="form.collection_method"
          :disabled="!canEdit"
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
