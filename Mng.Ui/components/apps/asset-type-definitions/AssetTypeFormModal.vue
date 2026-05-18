<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import type { MonAssetTypeFull, CollectibleDefinition, MonCollectibleTemplate } from '@/types/apps/assetTypeDefinitions';
import CollectiblesEditor from './CollectiblesEditor.vue';

const props = defineProps<{
  modelValue: boolean;
  type: MonAssetTypeFull | Partial<MonAssetTypeFull> | null;
  familyOptions: Array<{ title: string; value: string }>;
  /** Şablon uygula dropdown için (metoda göre filtrelenmiş liste sayfada verilir) */
  templateOptions?: Array<{ title: string; value: string }>;
  templates?: MonCollectibleTemplate[];
  loading?: boolean;
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [v: boolean];
  save: [data: Partial<MonAssetTypeFull>];
}>();

const COLLECTION_METHODS = [
  { title: 'SSH', value: 'SSH' },
  { title: 'WMI', value: 'WMI' },
  { title: 'SNMP', value: 'SNMP' },
  { title: 'SNMP v3', value: 'SNMP_V3' },
  { title: 'HTTP', value: 'HTTP' },
  { title: 'REST', value: 'REST' },
  { title: 'OPC UA', value: 'OPC_UA' },
];

const form = ref<Partial<MonAssetTypeFull>>({
  name: '',
  family: '',
  collection_method: '',
  description: null,
  collectibles: [],
});

const collectibles = ref<Array<CollectibleDefinition & { overridable_params_str?: string }>>([]);
const selectedTemplateId = ref<string | null>(null);

watch(
  () => props.type,
  (v) => {
    if (v) {
      form.value = {
        name: v.name ?? '',
        family: v.family ?? '',
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
      form.value = { name: '', family: '', collection_method: '', description: null, collectibles: [] };
      collectibles.value = [{ code: '', name: '', data_type: 'number', overridable_params_str: '' }];
    }
    selectedTemplateId.value = null;
  },
  { immediate: true }
);

const isEdit = computed(() => !!(props.type && '__dataId' in props.type && (props.type as any).__dataId));

/** Seçilen toplama metoduna göre şablon seçenekleri */
const templateOptionsForMethod = computed(() => {
  const method = (form.value.collection_method || '').trim().toLowerCase();
  if (!method) return [];
  const list = props.templates ?? [];
  return list
    .filter((t) => (t.collection_method || '').toLowerCase() === method)
    .map((t) => ({ title: t.name, value: t.__dataId }));
});

function applyTemplate(templateId: string | null) {
  if (!templateId || !props.templates) return;
  const t = props.templates.find((x) => x.__dataId === templateId);
  if (!t?.collectibles?.length) return;
  collectibles.value = t.collectibles.map((c) => ({
    ...c,
    overridable_params_str: Array.isArray(c.overridable_params) ? c.overridable_params.join(', ') : '',
  }));
}

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
  const family = form.value.family ?? '';
  const collection_method = (form.value.collection_method ?? '').trim();
  if (!name || !family || !collection_method) return;
  const built = buildCollectibles();
  if (built.length === 0) return;
  emit('save', { ...form.value, name, family, collection_method, collectibles: built });
  emit('update:modelValue', false);
}

function close() {
  emit('update:modelValue', false);
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="840" persistent scrollable @update:model-value="(v) => emit('update:modelValue', v)">
    <v-card>
      <v-card-title>{{ isEdit ? 'Tip düzenle' : 'Yeni tip' }}</v-card-title>
      <v-card-text class="pb-0">
        <v-text-field
          v-model="form.name"
          label="Tip adı *"
          variant="outlined"
          density="comfortable"
          class="mb-3"
          hide-details
        />
        <v-select
          v-model="form.family"
          :items="familyOptions"
          item-title="title"
          item-value="value"
          label="Aile *"
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
          class="mb-3"
          hide-details
        />

        <v-select
          v-if="templateOptionsForMethod.length > 0"
          v-model="selectedTemplateId"
          :items="templateOptionsForMethod"
          item-title="title"
          item-value="value"
          label="Şablon uygula"
          placeholder="Collectible şablonu seçin..."
          variant="outlined"
          density="comfortable"
          clearable
          class="mb-3"
          hide-details
          @update:model-value="applyTemplate"
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
