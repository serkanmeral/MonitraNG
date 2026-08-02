<script setup lang="ts">
import { onMounted, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  fetchDiscoveryPrefixes,
  putDiscoveryPrefixes,
  type DiscoveryPrefixDto,
} from '@/services/siemDiscoveryService';

const emit = defineEmits<{
  saved: [];
}>();

const { t } = useAppI18n();

const loading = ref(true);
const saving = ref(false);
const error = ref<string | null>(null);
const flash = ref<string | null>(null);
const source = ref('config');
const rows = ref<DiscoveryPrefixDto[]>([]);

function blankRow(): DiscoveryPrefixDto {
  return { cidr: '', label: '', vlanName: null };
}

async function load() {
  loading.value = true;
  error.value = null;
  try {
    const res = await fetchDiscoveryPrefixes();
    source.value = res.source;
    rows.value = res.prefixes.length ? res.prefixes.map((p) => ({ ...p })) : [blankRow()];
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
    rows.value = [blankRow()];
  } finally {
    loading.value = false;
  }
}

function addRow() {
  rows.value = [...rows.value, blankRow()];
}

function removeRow(index: number) {
  const next = rows.value.filter((_, i) => i !== index);
  rows.value = next.length ? next : [blankRow()];
}

function isValidCidr(cidr: string): boolean {
  const m = cidr.trim().match(/^(\d{1,3}\.){3}\d{1,3}\/(\d|[12]\d|3[0-2])$/);
  if (!m) return false;
  const parts = cidr.trim().split('/')[0]!.split('.').map(Number);
  return parts.every((n) => n >= 0 && n <= 255);
}

async function save() {
  saving.value = true;
  error.value = null;
  flash.value = null;
  try {
    const cleaned = rows.value
      .map((r) => ({
        cidr: (r.cidr || '').trim(),
        label: (r.label || '').trim(),
        vlanName: (r.vlanName || '').trim() || null,
      }))
      .filter((r) => r.cidr);

    for (const r of cleaned) {
      if (!isValidCidr(r.cidr)) {
        throw new Error(t('siemCenter.settings.prefixes.invalidCidr', { cidr: r.cidr }));
      }
      if (!r.label) {
        throw new Error(t('siemCenter.settings.prefixes.labelRequired', { cidr: r.cidr }));
      }
    }

    const res = await putDiscoveryPrefixes(cleaned);
    if (res.error) throw new Error(res.error);
    source.value = res.source;
    rows.value = res.prefixes.length ? res.prefixes.map((p) => ({ ...p })) : [blankRow()];
    flash.value = t('siemCenter.settings.prefixes.saved');
    emit('saved');
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : String(e);
  } finally {
    saving.value = false;
  }
}

onMounted(() => {
  void load();
});

defineExpose({ load });
</script>

<template>
  <div class="discovery-prefixes-panel">
    <div class="d-flex flex-wrap align-center justify-space-between ga-2 mb-3">
      <div>
        <div class="text-body-1 font-weight-medium">
          {{ t('siemCenter.settings.prefixes.title') }}
        </div>
        <div class="text-caption text-medium-emphasis">
          {{ t('siemCenter.settings.prefixes.hint') }}
          <span v-if="source">
            · {{ t('siemCenter.settings.prefixes.source', { source }) }}
          </span>
        </div>
      </div>
      <div class="d-flex ga-2">
        <v-btn size="small" variant="text" :loading="loading" @click="load">
          {{ t('siemCenter.settings.prefixes.refresh') }}
        </v-btn>
        <v-btn size="small" variant="tonal" prepend-icon="mdi-plus" @click="addRow">
          {{ t('siemCenter.settings.prefixes.add') }}
        </v-btn>
        <v-btn
          size="small"
          color="primary"
          variant="flat"
          :loading="saving"
          prepend-icon="mdi-content-save"
          @click="save"
        >
          {{ t('siemCenter.settings.prefixes.save') }}
        </v-btn>
      </div>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" density="comfortable" class="mb-3">
      {{ error }}
    </v-alert>
    <v-alert v-if="flash" type="success" variant="tonal" density="comfortable" class="mb-3">
      {{ flash }}
    </v-alert>

    <v-table density="compact" class="prefix-table">
      <thead>
        <tr>
          <th>{{ t('siemCenter.settings.prefixes.colCidr') }}</th>
          <th>{{ t('siemCenter.settings.prefixes.colLabel') }}</th>
          <th>{{ t('siemCenter.settings.prefixes.colVlan') }}</th>
          <th style="width: 48px" />
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, index) in rows" :key="index">
          <td>
            <v-text-field
              v-model="row.cidr"
              density="compact"
              variant="outlined"
              hide-details
              placeholder="192.168.20.0/24"
              class="font-mono"
            />
          </td>
          <td>
            <v-text-field
              v-model="row.label"
              density="compact"
              variant="outlined"
              hide-details
              :placeholder="t('siemCenter.settings.prefixes.labelPlaceholder')"
            />
          </td>
          <td>
            <v-text-field
              v-model="row.vlanName"
              density="compact"
              variant="outlined"
              hide-details
              :placeholder="t('siemCenter.settings.prefixes.vlanPlaceholder')"
            />
          </td>
          <td>
            <v-btn
              icon="mdi-delete-outline"
              size="small"
              variant="text"
              color="error"
              @click="removeRow(index)"
            />
          </td>
        </tr>
      </tbody>
    </v-table>
  </div>
</template>

<style scoped>
.prefix-table :deep(.v-field__input) {
  font-size: 0.875rem;
}
.font-mono :deep(input) {
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace;
}
</style>
