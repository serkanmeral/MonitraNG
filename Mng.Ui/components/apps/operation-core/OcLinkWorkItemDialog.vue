<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  ocCreateWorkItemLink,

  ocListDatasetPage,
} from '@/services/operationCoreService';
import type { OcWorkItemLinkSummary } from '@/types/apps/operationCore';

const LINK_TYPES = ['relates_to', 'blocks', 'duplicates'] as const;

const props = defineProps<{
  modelValue: boolean;
  workItemId: string;
  workspaceId: string;
  existingLinks?: OcWorkItemLinkSummary[];
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  linked: [];
}>();

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const search = ref('');
const linkType = ref<(typeof LINK_TYPES)[number]>('relates_to');
const selectedTargetId = ref<string | null>(null);
const description = ref('');
const loading = ref(false);
const submitting = ref(false);
const errorLocal = ref<string | null>(null);
const results = ref<{ id: string; key: string; title: string }[]>([]);
const total = ref(0);

const linkTypeItems = computed(() =>
  LINK_TYPES.map((value) => ({
    value,
    title: t(`operationCore.profile.relations.linkTypes.${value}`),
  }))
);

const excludedIds = computed(() => {
  const ids = new Set<string>([props.workItemId.trim().toLowerCase()]);
  for (const link of props.existingLinks ?? []) {
    const oid = link.otherWorkItemId?.trim();
    if (oid) ids.add(oid.toLowerCase());
  }
  return ids;
});

const filteredResults = computed(() =>
  results.value.filter((r) => !excludedIds.value.has(r.id.trim().toLowerCase()))
);

const selectedItem = computed(() =>
  filteredResults.value.find((r) => r.id === selectedTargetId.value) ?? null
);

let searchTimer: ReturnType<typeof setTimeout> | null = null;

async function runSearch() {
  const ws = props.workspaceId.trim();
  if (!ws) {
    results.value = [];
    total.value = 0;
    return;
  }

  loading.value = true;
  errorLocal.value = null;
  try {
    const page = await ocListDatasetPage('op_work_items', {
      filter: `workspaceId:eq:${ws}`,
      search: search.value.trim() || undefined,
      limit: 25,
      sort: '-createdAt',
    });
    total.value = page.total;
    results.value = page.items
      .filter((row): row is Record<string, unknown> => !!row && typeof row === 'object')
      .map((row) => {
        const id = String(row.__dataId ?? row.dataId ?? '').trim();
        return {
          id,
          key: String(row.key ?? id),
          title: String(row.title ?? ''),
        };
      })
      .filter((r) => !!r.id);
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.relations.linkDialog.searchError');
    results.value = [];
    total.value = 0;
  } finally {
    loading.value = false;
  }
}

function scheduleSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void runSearch(), 280);
}

function resetForm() {
  search.value = '';
  linkType.value = 'relates_to';
  selectedTargetId.value = null;
  description.value = '';
  errorLocal.value = null;
  results.value = [];
  total.value = 0;
}

watch(open, (isOpen) => {
  if (isOpen) {
    resetForm();
    void runSearch();
  }
});

watch(search, () => scheduleSearch());

async function submit() {
  const targetId = selectedTargetId.value?.trim();
  if (!targetId) {
    errorLocal.value = t('operationCore.profile.relations.linkDialog.targetRequired');
    return;
  }

  submitting.value = true;
  errorLocal.value = null;
  try {
    await ocCreateWorkItemLink(props.workItemId, {
      targetWorkItemId: targetId,
      linkType: linkType.value,
      description: description.value.trim() || null,
    });
    open.value = false;
    emit('linked');
  } catch (e: unknown) {
    errorLocal.value = panelError(e, 'operationCore.profile.relations.linkDialog.submitError');
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <v-dialog v-model="open" max-width="560" persistent scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-3">
        <span>{{ t('operationCore.profile.relations.linkDialog.title') }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" :disabled="submitting" @click="open = false" />
      </v-card-title>

      <v-divider />

      <v-card-text class="pt-4">
        <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorLocal }}
        </v-alert>

        <v-select
          v-model="linkType"
          :items="linkTypeItems"
          item-title="title"
          item-value="value"
          :label="t('operationCore.profile.relations.linkDialog.linkType')"
          variant="outlined"
          density="comfortable"
          hide-details
          class="mb-4"
        />

        <v-text-field
          v-model="search"
          :label="t('operationCore.profile.relations.linkDialog.search')"
          :placeholder="t('operationCore.profile.relations.linkDialog.searchPlaceholder')"
          prepend-inner-icon="mdi-magnify"
          variant="outlined"
          density="comfortable"
          hide-details
          clearable
          class="mb-2"
        />

        <v-list
          v-if="filteredResults.length"
          density="compact"
          class="oc-link-picker-list rounded border mb-4"
          :disabled="submitting"
        >
          <v-list-item
            v-for="item in filteredResults"
            :key="item.id"
            :value="item.id"
            :active="selectedTargetId === item.id"
            @click="selectedTargetId = item.id"
          >
            <template #prepend>
              <v-icon :icon="selectedTargetId === item.id ? 'mdi-radiobox-marked' : 'mdi-radiobox-blank'" size="small" />
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
        <div v-else class="text-caption text-medium-emphasis mb-4">
          {{ t('operationCore.profile.relations.linkDialog.noResults') }}
        </div>

        <v-textarea
          v-model="description"
          :label="t('operationCore.profile.relations.linkDialog.description')"
          variant="outlined"
          density="comfortable"
          rows="2"
          auto-grow
          hide-details
        />
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="submitting" @click="open = false">
          {{ t('operationCore.profile.relations.linkDialog.cancel') }}
        </v-btn>
        <v-btn color="primary" variant="flat" :loading="submitting" :disabled="!selectedItem" @click="submit">
          {{ t('operationCore.profile.relations.linkDialog.submit') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-link-picker-list {
  max-height: 220px;
  overflow-y: auto;
}
</style>
