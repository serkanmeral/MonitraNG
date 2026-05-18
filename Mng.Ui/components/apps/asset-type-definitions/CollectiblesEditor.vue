<script setup lang="ts">
import { computed } from 'vue';
import type { CollectibleDefinition } from '@/types/apps/assetTypeDefinitions';
import { PlusIcon, TrashIcon } from 'vue-tabler-icons';

const props = withDefaults(
  defineProps<{
    modelValue: Array<CollectibleDefinition & { overridable_params_str?: string }>;
    collectionMethod?: string;
    disabled?: boolean;
  }>(),
  { collectionMethod: '', disabled: false }
);

const emit = defineEmits<{
  'update:modelValue': [v: Array<CollectibleDefinition & { overridable_params_str?: string }>];
}>();

const DATA_TYPES = [
  { title: 'number', value: 'number' },
  { title: 'string', value: 'string' },
  { title: 'object', value: 'object' },
];

/** Toplama metoduna göre birincil alan: SNMP -> OID, REST/HTTP -> Path, diğer -> metric_key */
const primaryField = computed(() => {
  const m = (props.collectionMethod || '').toUpperCase();
  if (m === 'SNMP' || m === 'SNMP_V3') return 'oid';
  if (m === 'REST' || m === 'HTTP') return 'path';
  return 'metric_key';
});

const primaryFieldLabel = computed(() => {
  if (primaryField.value === 'oid') return 'OID';
  if (primaryField.value === 'path') {
    const m = (props.collectionMethod || '').toUpperCase();
    return m === 'HTTP' ? 'JSON Path' : 'Path / URL yolu';
  }
  return 'Metrik anahtarı';
});

const pathPlaceholder = computed(() => {
  const m = (props.collectionMethod || '').toUpperCase();
  return m === 'HTTP' ? 'örn. $.storage.disk.usagePercent' : 'örn. /api/metrics/cpu';
});

function getOverridableParamsStr(c: CollectibleDefinition & { overridable_params_str?: string }) {
  return c?.overridable_params_str ?? '';
}

function setOverridableParamsStr(index: number, v: string) {
  const list = [...props.modelValue];
  if (list[index]) (list[index] as any).overridable_params_str = v;
  emit('update:modelValue', list);
}

function updateField(index: number, field: string, value: string) {
  const list = [...props.modelValue];
  if (list[index]) (list[index] as any)[field] = value;
  emit('update:modelValue', list);
}

function addCollectible() {
  emit('update:modelValue', [
    ...props.modelValue,
    { code: '', name: '', data_type: 'number', overridable_params_str: '' },
  ]);
}

function removeCollectible(index: number) {
  const list = props.modelValue.filter((_, i) => i !== index);
  if (list.length === 0)
    emit('update:modelValue', [{ code: '', name: '', data_type: 'number', overridable_params_str: '' }]);
  else emit('update:modelValue', list);
}

function getPrimaryValue(c: CollectibleDefinition & { overridable_params_str?: string }) {
  if (primaryField.value === 'oid') return c.oid ?? '—';
  if (primaryField.value === 'path') return c.path ?? '—';
  return c.metric_key ?? '—';
}
</script>

<template>
  <div class="collectibles-editor">
    <div class="text-subtitle-2 text-medium-emphasis mb-2">
      Toplanacak metrikler
    </div>
    <p class="text-caption text-medium-emphasis mb-3">
      Her satır bir metrik tanımıdır. Toplama metoduna göre OID, path veya metrik anahtarı kullanın.
    </p>

    <v-expansion-panels v-if="modelValue.length > 0" variant="accordion" class="collectibles-panels mb-3">
      <v-expansion-panel
        v-for="(col, index) in modelValue"
        :key="index"
        class="collectible-panel"
        elevation="0"
      >
        <v-expansion-panel-title class="py-2">
          <div class="d-flex align-center flex-wrap gap-2 w-100">
            <span class="text-medium-emphasis mr-1" style="min-width: 1.5rem;">{{ index + 1 }}.</span>
            <span class="font-weight-medium text-body-2">{{ col.code || '(Kod yok)' }}</span>
            <v-chip v-if="col.name" size="small" variant="tonal" density="compact">
              {{ col.name }}
            </v-chip>
            <v-chip size="x-small" variant="outlined" density="compact" color="primary">
              {{ col.data_type || 'number' }}
            </v-chip>
            <span class="text-caption text-medium-emphasis ms-1 text-truncate" style="max-width: 240px;" :title="getPrimaryValue(col)">
              {{ primaryFieldLabel }}: {{ getPrimaryValue(col) }}
            </span>
          </div>
        </v-expansion-panel-title>
        <v-expansion-panel-text class="pt-0">
          <v-card variant="outlined" class="pa-3">
            <v-row dense>
              <v-col cols="12" sm="4">
                <v-text-field
                  :model-value="col.code"
                  @update:model-value="(v) => updateField(index, 'code', v ?? '')"
                  label="Kod *"
                  variant="outlined"
                  density="compact"
                  hide-details
                  placeholder="örn. sysUpTime"
                  :disabled="disabled"
                />
              </v-col>
              <v-col cols="12" sm="4">
                <v-text-field
                  :model-value="col.name"
                  @update:model-value="(v) => updateField(index, 'name', v ?? '')"
                  label="Görünen ad"
                  variant="outlined"
                  density="compact"
                  hide-details
                  placeholder="Raporlarda görünecek ad"
                  :disabled="disabled"
                />
              </v-col>
              <v-col cols="12" sm="4">
                <v-select
                  :model-value="col.data_type"
                  @update:model-value="(v) => updateField(index, 'data_type', v ?? 'number')"
                  :items="DATA_TYPES"
                  item-title="title"
                  item-value="value"
                  label="Veri tipi"
                  variant="outlined"
                  density="compact"
                  hide-details
                  :disabled="disabled"
                />
              </v-col>
              <v-col v-if="primaryField === 'oid'" cols="12" sm="6">
                <v-text-field
                  :model-value="col.oid"
                  @update:model-value="(v) => updateField(index, 'oid', v ?? '')"
                  label="OID *"
                  variant="outlined"
                  density="compact"
                  hide-details
                  placeholder="örn. 1.3.6.1.2.1.1.3.0"
                  :disabled="disabled"
                />
              </v-col>
              <v-col v-if="primaryField === 'path'" cols="12" sm="6">
                <v-text-field
                  :model-value="col.path"
                  @update:model-value="(v) => updateField(index, 'path', v ?? '')"
                  :label="primaryFieldLabel + ' *'"
                  variant="outlined"
                  density="compact"
                  hide-details
                  :placeholder="pathPlaceholder"
                  :disabled="disabled"
                />
              </v-col>
              <v-col v-if="primaryField === 'metric_key'" cols="12" sm="6">
                <v-text-field
                  :model-value="col.metric_key"
                  @update:model-value="(v) => updateField(index, 'metric_key', v ?? '')"
                  label="Metrik anahtarı *"
                  variant="outlined"
                  density="compact"
                  hide-details
                  placeholder="örn. cpu_usage"
                  :disabled="disabled"
                />
              </v-col>
              <!-- Opsiyonel: OID (SNMP dışı) -->
              <v-col v-if="primaryField !== 'oid'" cols="12" sm="6">
                <v-text-field
                  :model-value="col.oid"
                  @update:model-value="(v) => updateField(index, 'oid', v ?? '')"
                  label="OID (opsiyonel)"
                  variant="outlined"
                  density="compact"
                  hide-details
                  :disabled="disabled"
                />
              </v-col>
              <!-- Opsiyonel: Path (REST/HTTP dışı) -->
              <v-col v-if="primaryField !== 'path'" cols="12" sm="6">
                <v-text-field
                  :model-value="col.path"
                  @update:model-value="(v) => updateField(index, 'path', v ?? '')"
                  label="Path (opsiyonel)"
                  variant="outlined"
                  density="compact"
                  hide-details
                  :disabled="disabled"
                />
              </v-col>
              <!-- Opsiyonel: Metrik anahtarı (SSH/WMI dışı) -->
              <v-col v-if="primaryField !== 'metric_key'" cols="12" sm="6">
                <v-text-field
                  :model-value="col.metric_key"
                  @update:model-value="(v) => updateField(index, 'metric_key', v ?? '')"
                  label="Metrik anahtarı (opsiyonel)"
                  variant="outlined"
                  density="compact"
                  hide-details
                  :disabled="disabled"
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field
                  :model-value="getOverridableParamsStr(col)"
                  @update:model-value="(v) => setOverridableParamsStr(index, v)"
                  label="Overridable params"
                  variant="outlined"
                  density="compact"
                  hide-details
                  placeholder="virgülle ayrılmış: oid, interval"
                  :disabled="disabled"
                />
              </v-col>
              <v-col cols="12" class="d-flex justify-end">
                <v-btn
                  v-if="!disabled"
                  size="small"
                  variant="text"
                  color="error"
                  @click="removeCollectible(index)"
                >
                  <TrashIcon size="18" class="mr-1" />
                  Bu metrik satırını kaldır
                </v-btn>
              </v-col>
            </v-row>
          </v-card>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <v-card v-else variant="outlined" class="pa-4 mb-3 text-center">
      <p class="text-body-2 text-medium-emphasis mb-2">Henüz metrik eklenmedi.</p>
      <p class="text-caption text-medium-emphasis mb-3">Şablon uygulayabilir veya aşağıdaki düğme ile tek tek ekleyebilirsiniz.</p>
      <v-btn v-if="!disabled" size="small" variant="outlined" color="primary" @click="addCollectible">
        <PlusIcon size="18" class="mr-1" />
        İlk metrik ekle
      </v-btn>
    </v-card>

    <v-btn
      v-if="!disabled && modelValue.length > 0"
      size="small"
      variant="outlined"
      color="primary"
      block
      @click="addCollectible"
    >
      <PlusIcon size="18" class="mr-1" />
      Metrik ekle
    </v-btn>
  </div>
</template>

<style scoped>
.collectibles-panels :deep(.v-expansion-panel-title) {
  min-height: 48px;
}
.collectible-panel {
  border: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
  border-radius: 8px;
  margin-bottom: 8px;
}
.collectible-panel:last-of-type {
  margin-bottom: 0;
}
</style>
