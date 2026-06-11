<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  ocExtractDgErrorMessage,
  ocListDatasetPage,
  ocSimulateWorkspaceAutomation,
} from '@/services/operationCoreService';
import type { OcAutomationSimulateResult, OpWorkspaceAutomation } from '@/types/apps/operationCore';

const props = defineProps<{
  modelValue: boolean;
  automation: OpWorkspaceAutomation | null;
  workspaceId: string;
  boardNameById: Map<string, string>;
  typeNameById: Map<string, string>;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  executed: [];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const search = ref('');
const selectedWorkItemId = ref<string | null>(null);
const loading = ref(false);
const previewing = ref(false);
const executing = ref(false);
const errorLocal = ref<string | null>(null);
const result = ref<OcAutomationSimulateResult | null>(null);
const results = ref<{ id: string; key: string; title: string }[]>([]);

const selectedItem = computed(() =>
  results.value.find((r) => r.id === selectedWorkItemId.value) ?? null
);

const previewFields = computed(() => {
  const fields = result.value?.preview?.resolvedFields;
  if (!fields || typeof fields !== 'object') return [];
  return Object.entries(fields).map(([key, value]) => ({
    key,
    value: value == null ? '—' : String(value),
  }));
});

let searchTimer: ReturnType<typeof setTimeout> | null = null;

async function runSearch() {
  const ws = props.workspaceId.trim();
  if (!ws) {
    results.value = [];
    return;
  }

  loading.value = true;
  try {
    const page = await ocListDatasetPage('op_work_items', {
      filter: `workspaceId:eq:${ws}`,
      search: search.value.trim() || undefined,
      limit: 25,
      sort: '-createdAt',
    });
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
  } catch {
    results.value = [];
  } finally {
    loading.value = false;
  }
}

function scheduleSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => void runSearch(), 280);
}

function resetState() {
  search.value = '';
  selectedWorkItemId.value = null;
  errorLocal.value = null;
  result.value = null;
  results.value = [];
}

watch(open, (isOpen) => {
  if (isOpen) {
    resetState();
    void runSearch();
  }
});

watch(search, () => scheduleSearch());

async function preview() {
  const automationId = props.automation?.__dataId?.trim();
  const workItemId = selectedWorkItemId.value?.trim();
  if (!automationId || !workItemId) {
    errorLocal.value = t('operationCore.workspaceDefinitions.automations.simulate.targetRequired');
    return;
  }

  previewing.value = true;
  errorLocal.value = null;
  result.value = null;
  try {
    result.value = await ocSimulateWorkspaceAutomation(automationId, workItemId, false);
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.automations.simulate.error')
    );
  } finally {
    previewing.value = false;
  }
}

async function execute() {
  const automationId = props.automation?.__dataId?.trim();
  const workItemId = selectedWorkItemId.value?.trim();
  if (!automationId || !workItemId) return;

  executing.value = true;
  errorLocal.value = null;
  try {
    result.value = await ocSimulateWorkspaceAutomation(automationId, workItemId, true);
    if (result.value.executed) {
      emit('executed');
    }
  } catch (e: unknown) {
    errorLocal.value = ocExtractDgErrorMessage(
      e,
      t('operationCore.workspaceDefinitions.automations.simulate.executeError')
    );
  } finally {
    executing.value = false;
  }
}

function targetLabel(boardId?: string | null, typeId?: string | null): string {
  const board = boardId ? props.boardNameById.get(boardId) ?? boardId : '—';
  const type = typeId ? props.typeNameById.get(typeId) ?? typeId : '—';
  return `${board} / ${type}`;
}
</script>

<template>
  <v-dialog v-model="open" max-width="620" persistent scrollable>
    <v-card rounded="lg">
      <v-card-title class="d-flex align-center py-3">
        <span>{{ t('operationCore.workspaceDefinitions.automations.simulate.title') }}</span>
        <v-spacer />
        <v-btn icon="mdi-close" variant="text" size="small" :disabled="executing" @click="open = false" />
      </v-card-title>

      <v-divider />

      <v-card-text class="pt-4">
        <div v-if="automation" class="text-body-2 mb-4">
          <span class="font-weight-medium">{{ automation.name }}</span>
        </div>

        <v-alert v-if="errorLocal" type="error" variant="tonal" density="compact" class="mb-4">
          {{ errorLocal }}
        </v-alert>

        <v-text-field
          v-model="search"
          :label="t('operationCore.workspaceDefinitions.automations.simulate.search')"
          :placeholder="t('operationCore.workspaceDefinitions.automations.simulate.searchPlaceholder')"
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
          class="oc-sim-picker-list rounded border mb-4"
        >
          <v-list-item
            v-for="item in results"
            :key="item.id"
            :active="selectedWorkItemId === item.id"
            @click="selectedWorkItemId = item.id"
          >
            <template #prepend>
              <v-icon
                :icon="selectedWorkItemId === item.id ? 'mdi-radiobox-marked' : 'mdi-radiobox-blank'"
                size="small"
              />
            </template>
            <v-list-item-title class="text-body-2">
              <span class="font-weight-medium">{{ item.key }}</span>
              <span v-if="item.title" class="text-medium-emphasis"> — {{ item.title }}</span>
            </v-list-item-title>
          </v-list-item>
        </v-list>

        <div v-else-if="loading" class="text-center py-4">
          <v-progress-circular indeterminate size="28" />
        </div>
        <div v-else class="text-caption text-medium-emphasis mb-4">
          {{ t('operationCore.workspaceDefinitions.automations.simulate.noResults') }}
        </div>

        <div class="d-flex ga-2 mb-4">
          <v-btn
            color="primary"
            variant="tonal"
            :loading="previewing"
            :disabled="!selectedItem || executing"
            @click="preview"
          >
            {{ t('operationCore.workspaceDefinitions.automations.simulate.preview') }}
          </v-btn>
        </div>

        <v-card v-if="result" variant="outlined" class="rounded-lg">
          <v-card-text>
            <v-chip
              :color="result.matched ? 'success' : 'warning'"
              size="small"
              variant="tonal"
              class="mb-3"
            >
              {{
                result.matched
                  ? t('operationCore.workspaceDefinitions.automations.simulate.matched')
                  : t('operationCore.workspaceDefinitions.automations.simulate.notMatched')
              }}
            </v-chip>

            <div v-if="result.reason && !result.matched" class="text-body-2 text-medium-emphasis mb-2">
              {{ result.reason }}
            </div>

            <template v-if="result.preview && result.matched">
              <div class="text-caption text-medium-emphasis mb-1">
                {{ t('operationCore.workspaceDefinitions.automations.simulate.previewTitle') }}
              </div>
              <div class="text-body-2 font-weight-medium mb-1">{{ result.preview.resolvedTitle }}</div>
              <div class="text-caption text-medium-emphasis mb-2">
                {{ targetLabel(result.preview.targetBoardId, result.preview.targetTypeId) }}
              </div>
              <div v-if="previewFields.length" class="mt-2">
                <div
                  v-for="row in previewFields"
                  :key="row.key"
                  class="d-flex justify-space-between text-caption py-1"
                >
                  <span class="text-medium-emphasis">{{ row.key }}</span>
                  <span class="text-truncate ms-2" style="max-width: 60%">{{ row.value }}</span>
                </div>
              </div>
            </template>

            <v-alert
              v-if="result.executed && result.createdWorkItem"
              type="success"
              variant="tonal"
              density="compact"
              class="mt-3 mb-0"
            >
              {{
                t('operationCore.workspaceDefinitions.automations.simulate.created', {
                  key: result.createdWorkItem.key,
                })
              }}
            </v-alert>
          </v-card-text>
        </v-card>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4">
        <v-spacer />
        <v-btn variant="text" :disabled="executing" @click="open = false">
          {{ t('operationCore.workspaceDefinitions.automations.simulate.close') }}
        </v-btn>
        <v-btn
          color="primary"
          variant="flat"
          :loading="executing"
          :disabled="!result?.matched || previewing"
          @click="execute"
        >
          {{ t('operationCore.workspaceDefinitions.automations.simulate.execute') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-sim-picker-list {
  max-height: 200px;
  overflow-y: auto;
}
</style>
