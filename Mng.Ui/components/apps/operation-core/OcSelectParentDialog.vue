<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import { ocListDatasetPage, ocUpdateWorkItem } from '@/services/operationCoreService';

const props = defineProps<{
  modelValue: boolean;
  workItemId: string;
  workspaceId: string;
  /** Bu işin tipini childTypeKeys ile kabul eden üst tipler. */
  parentTypeIds: string[];
  currentParentId?: string | null;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  moved: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
});

const search = ref('');
const selectedId = ref<string | null>(null);
const loading = ref(false);
const submitting = ref(false);
const errorLocal = ref<string | null>(null);
const results = ref<{ id: string; key: string; title: string }[]>([]);

const selectedItem = computed(() => results.value.find((row) => row.id === selectedId.value) ?? null);

let searchTimer: ReturnType<typeof setTimeout> | null = null;

function rowId(row: Record<string, unknown>): string {
  return String(row.__dataId ?? row.dataId ?? '').trim();
}

function rowText(row: Record<string, unknown>, key: string): string {
  const value = row[key];
  if (typeof value === 'string') return value.trim();
  return '';
}

async function runSearch() {
  const ws = props.workspaceId.trim();
  const typeIds = props.parentTypeIds.map((id) => id.trim()).filter(Boolean);
  if (!ws || typeIds.length === 0) {
    results.value = [];
    return;
  }

  loading.value = true;
  errorLocal.value = null;
  try {
    const typeFilter =
      typeIds.length === 1
        ? `typeId:eq:${typeIds[0]}`
        : `typeId:in:[${typeIds.map((id) => `"${id}"`).join(',')}]`;
    const page = await ocListDatasetPage('op_work_items', {
      filter: `workspaceId:eq:${ws},${typeFilter}`,
      search: search.value.trim() || undefined,
      limit: 50,
      sort: 'key',
      expand: false,
    });
    const excluded = new Set(
      [props.workItemId, props.currentParentId ?? '']
        .map((id) => id.trim().toLowerCase())
        .filter(Boolean)
    );
    results.value = page.items
      .filter((row): row is Record<string, unknown> => !!row && typeof row === 'object')
      .map((row) => ({
        id: rowId(row),
        key: rowText(row, 'key'),
        title: rowText(row, 'title'),
        parentItemId: rowText(row, 'parentItemId'),
      }))
      .filter((row) => !!row.id && !row.parentItemId && !excluded.has(row.id.toLowerCase()))
      .map(({ id, key, title }) => ({ id, key: key || id, title }));
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.childrenCard.searchError');
    results.value = [];
  } finally {
    loading.value = false;
  }
}

function scheduleSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void runSearch(), 280);
}

watch(open, (isOpen) => {
  if (!isOpen) return;
  search.value = '';
  selectedId.value = null;
  errorLocal.value = null;
  results.value = [];
  void runSearch();
});

watch(search, () => {
  if (open.value) scheduleSearch();
});

async function submit() {
  const parentId = selectedId.value?.trim();
  if (!parentId) return;

  submitting.value = true;
  errorLocal.value = null;
  try {
    await ocUpdateWorkItem(props.workItemId, { fields: { parentItemId: parentId } });
    open.value = false;
    emit('moved');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.childrenCard.submitError');
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="560" persistent scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-3">
        <span>{{ t('operationCore.profile.childrenCard.dialogTitle') }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" :disabled="submitting" @click="open = false" />
      </v-card-title>
      <v-divider />
      <v-card-text class="pt-4">
        <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorLocal }}
        </v-alert>
        <v-text-field
          v-model="search"
          :label="t('operationCore.profile.childrenCard.search')"
          :placeholder="t('operationCore.profile.childrenCard.searchPlaceholder')"
          prepend-inner-icon="mdi-magnify"
          variant="outlined"
          density="comfortable"
          hide-details
          clearable
          class="mb-2"
        />
        <v-list
          v-if="results.length"
          density="compact"
          class="oc-parent-picker-list rounded border"
          :disabled="submitting"
        >
          <v-list-item
            v-for="item in results"
            :key="item.id"
            :active="selectedId === item.id"
            @click="selectedId = item.id"
          >
            <template #prepend>
              <v-icon :icon="selectedId === item.id ? 'mdi-radiobox-marked' : 'mdi-radiobox-blank'" size="small" />
            </template>
            <v-list-item-title class="text-body-2 font-weight-medium">
              {{ item.key }}
              <span v-if="item.title" class="text-medium-emphasis font-weight-regular"> — {{ item.title }}</span>
            </v-list-item-title>
          </v-list-item>
        </v-list>
        <div v-else-if="loading" class="text-center py-4">
          <v-progress-circular indeterminate size="28" />
        </div>
        <div v-else class="text-caption text-medium-emphasis">
          {{ t('operationCore.profile.childrenCard.noResults') }}
        </div>
      </v-card-text>
      <v-divider />
      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="submitting" @click="open = false">
          {{ t('operationCore.profile.childrenCard.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" :loading="submitting" :disabled="!selectedItem" @click="submit">
          {{ t('operationCore.profile.childrenCard.submit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-parent-picker-list {
  max-height: 280px;
  overflow-y: auto;
}
</style>
