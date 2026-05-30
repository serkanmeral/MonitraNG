<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OpBoardColumnConfig, OpStateFlow } from '@/types/apps/operationCore';
import {
  boardColumnStateIds,
  moveBoardColumn,
  outgoingTransitionsForState,
  pickDefaultTransitionForState,
  suggestBoardColumnsFromFlow,
} from '@/utils/ocBoardColumns';

const props = defineProps<{
  columns: OpBoardColumnConfig[];
  stateFlowId: string;
  stateFlows: OpStateFlow[];
  stateItems: { value: string; title: string }[];
  enabledStateIds: string[];
  viewType: string;
}>();

const emit = defineEmits<{
  'update:columns': [OpBoardColumnConfig[]];
}>();

const { t } = useAppI18n();

const activeFlow = computed(
  () => props.stateFlows.find((f) => f.__dataId === props.stateFlowId) ?? null
);

const stateTitleById = computed(
  () => new Map(props.stateItems.map((s) => [s.value, s.title]))
);

const enabledStateIdSet = computed(() => new Set(props.enabledStateIds));

const usedStateIds = computed(() => boardColumnStateIds(props.columns));

const availableStates = computed(() =>
  props.stateItems.filter((s) => !usedStateIds.value.has(s.value))
);

const disabledColumnStates = computed(() =>
  props.columns.filter(
    (c) =>
      c.stateId &&
      enabledStateIdSet.value.size > 0 &&
      !enabledStateIdSet.value.has(c.stateId)
  )
);

const previewColumns = computed(() =>
  props.columns
    .filter((c) => c.stateId)
    .map((c, idx) => ({
      key: `${c.stateId}-${idx}`,
      title: c.title?.trim() || stateTitleById.value.get(c.stateId) || c.stateId,
      transition: c.defaultTransitionKey,
    }))
);

function patchColumns(next: OpBoardColumnConfig[]) {
  emit('update:columns', next);
}

function applyFromFlow() {
  const flow = activeFlow.value;
  if (!flow) return;
  patchColumns(suggestBoardColumnsFromFlow(flow, stateTitleById.value));
}

function clearColumns() {
  patchColumns([]);
}

function addState(stateId: string) {
  if (!stateId || usedStateIds.value.has(stateId)) return;
  const flow = activeFlow.value;
  patchColumns([
    ...props.columns,
    {
      stateId,
      title: stateTitleById.value.get(stateId) ?? null,
      queryKey: 'wi_board_column',
      defaultTransitionKey: pickDefaultTransitionForState(flow, stateId),
    },
  ]);
}

function removeColumn(index: number) {
  const next = [...props.columns];
  next.splice(index, 1);
  patchColumns(next);
}

function moveColumn(index: number, direction: -1 | 1) {
  patchColumns(moveBoardColumn(props.columns, index, direction));
}

function updateColumn(index: number, patch: Partial<OpBoardColumnConfig>) {
  const next = props.columns.map((col, i) => (i === index ? { ...col, ...patch } : col));
  patchColumns(next);
}

function onStateChange(index: number, stateId: string) {
  const flow = activeFlow.value;
  updateColumn(index, {
    stateId,
    title: stateTitleById.value.get(stateId) ?? null,
    defaultTransitionKey: pickDefaultTransitionForState(flow, stateId),
  });
}

function columnDisplayTitle(col: OpBoardColumnConfig): string {
  return col.title?.trim() || stateTitleById.value.get(col.stateId) || col.stateId;
}

function transitionLabel(col: OpBoardColumnConfig): string | null {
  if (!col.defaultTransitionKey) return null;
  const opts = outgoingTransitionsForState(activeFlow.value, col.stateId);
  const match = opts.find((o) => o.value === col.defaultTransitionKey);
  return match?.title ?? col.defaultTransitionKey;
}
</script>

<template>
  <div class="oc-board-column-editor">
    <v-alert type="info" variant="tonal" density="compact" class="rounded-lg mb-4">
      <div class="text-body-2 font-weight-medium mb-1">
        {{ t('operationCore.workspaceDefinitions.boards.columnsIntroTitle') }}
      </div>
      <p class="text-body-2 mb-0">
        {{ t('operationCore.workspaceDefinitions.boards.columnsIntroBody') }}
      </p>
    </v-alert>

    <div class="d-flex flex-wrap gap-2 mb-4">
      <v-btn
        color="primary"
        variant="flat"
        rounded="lg"
        class="text-none"
        :disabled="!activeFlow"
        @click="applyFromFlow"
      >
        <v-icon icon="mdi-auto-fix" start />
        {{ t('operationCore.workspaceDefinitions.boards.generateFromFlow') }}
      </v-btn>
      <v-btn
        variant="tonal"
        rounded="lg"
        class="text-none"
        :disabled="columns.length === 0"
        @click="clearColumns"
      >
        {{ t('operationCore.workspaceDefinitions.boards.clearColumns') }}
      </v-btn>
    </div>

    <v-alert
      v-if="!stateFlowId"
      type="warning"
      variant="tonal"
      density="compact"
      class="rounded-lg mb-4"
    >
      {{ t('operationCore.workspaceDefinitions.boards.selectFlowFirst') }}
    </v-alert>

    <div v-if="previewColumns.length > 0" class="oc-board-column-editor__preview mb-4">
      <div class="text-caption text-medium-emphasis mb-2">
        {{ t('operationCore.workspaceDefinitions.boards.previewStrip') }}
      </div>
      <div class="oc-board-column-editor__preview-track">
        <div
          v-for="col in previewColumns"
          :key="col.key"
          class="oc-board-column-editor__preview-col"
        >
          <span class="text-caption font-weight-bold text-truncate">{{ col.title }}</span>
          <span v-if="col.transition && viewType === 'kanban'" class="text-caption text-medium-emphasis">
            {{ col.transition }}
          </span>
        </div>
      </div>
    </div>

    <v-alert
      v-if="disabledColumnStates.length > 0"
      type="warning"
      variant="tonal"
      density="compact"
      class="rounded-lg mb-3"
    >
      {{ t('operationCore.workspaceDefinitions.boards.enabledStateWarning') }}
    </v-alert>

    <v-card v-if="columns.length === 0" variant="outlined" rounded="lg" class="pa-6 text-center mb-4">
      <v-icon icon="mdi-view-column-outline" size="40" color="primary" class="mb-2 opacity-70" />
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('operationCore.workspaceDefinitions.boards.noColumnsYet') }}
      </p>
    </v-card>

    <div v-else class="oc-board-column-editor__list mb-4">
      <v-card
        v-for="(col, idx) in columns"
        :key="`${col.stateId}-${idx}`"
        variant="outlined"
        rounded="lg"
        class="oc-board-column-editor__row mb-2"
      >
        <div class="d-flex align-stretch">
          <div class="oc-board-column-editor__order d-flex flex-column align-center justify-center px-1">
            <v-btn
              icon
              size="x-small"
              variant="text"
              :disabled="idx === 0"
              @click="moveColumn(idx, -1)"
            >
              <v-icon icon="mdi-chevron-up" size="18" />
            </v-btn>
            <span class="text-caption font-weight-bold">{{ idx + 1 }}</span>
            <v-btn
              icon
              size="x-small"
              variant="text"
              :disabled="idx === columns.length - 1"
              @click="moveColumn(idx, 1)"
            >
              <v-icon icon="mdi-chevron-down" size="18" />
            </v-btn>
          </div>

          <div class="flex-grow-1 pa-3 min-width-0">
            <div class="d-flex align-center flex-wrap gap-2 mb-2">
              <v-chip size="small" color="primary" variant="tonal" rounded="lg">
                {{ columnDisplayTitle(col) }}
              </v-chip>
              <v-chip
                v-if="col.stateId && enabledStateIdSet.size > 0 && !enabledStateIdSet.has(col.stateId)"
                size="x-small"
                color="warning"
                variant="tonal"
              >
                {{ t('operationCore.workspaceDefinitions.boards.stateNotEnabled') }}
              </v-chip>
            </div>

            <v-row dense>
              <v-col cols="12" md="6">
                <v-select
                  :model-value="col.stateId"
                  :items="stateItems"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.workspaceDefinitions.boards.fieldColumnState')"
                  density="compact"
                  variant="outlined"
                  hide-details
                  @update:model-value="(v) => onStateChange(idx, String(v || ''))"
                />
              </v-col>
              <v-col cols="12" md="6">
                <v-text-field
                  :model-value="col.title ?? ''"
                  :label="t('operationCore.workspaceDefinitions.boards.fieldColumnTitleOptional')"
                  :placeholder="columnDisplayTitle(col)"
                  density="compact"
                  variant="outlined"
                  hide-details
                  @update:model-value="(v) => updateColumn(idx, { title: String(v || '') || null })"
                />
              </v-col>
              <v-col v-if="viewType === 'kanban'" cols="12">
                <v-select
                  :model-value="col.defaultTransitionKey"
                  :items="outgoingTransitionsForState(activeFlow, col.stateId)"
                  item-title="title"
                  item-value="value"
                  :label="t('operationCore.workspaceDefinitions.boards.fieldDragTransition')"
                  :disabled="!stateFlowId || !col.stateId"
                  :hint="t('operationCore.workspaceDefinitions.boards.fieldDragTransitionHint')"
                  persistent-hint
                  density="compact"
                  variant="outlined"
                  clearable
                  @update:model-value="(v) => updateColumn(idx, { defaultTransitionKey: v ? String(v) : null })"
                />
              </v-col>
            </v-row>
          </div>

          <div class="d-flex align-center px-2">
            <v-btn
              icon
              variant="text"
              size="small"
              color="error"
              :title="t('operationCore.workspaceDefinitions.boards.removeColumn')"
              @click="removeColumn(idx)"
            >
              <v-icon icon="mdi-delete-outline" />
            </v-btn>
          </div>
        </div>
      </v-card>
    </div>

    <div v-if="availableStates.length > 0" class="oc-board-column-editor__add-panel pa-3 rounded-lg">
      <div class="text-subtitle-2 font-weight-medium mb-1">
        {{ t('operationCore.workspaceDefinitions.boards.addStateTitle') }}
      </div>
      <p class="text-caption text-medium-emphasis mb-3">
        {{ t('operationCore.workspaceDefinitions.boards.addStateHint') }}
      </p>
      <div class="d-flex flex-wrap gap-2">
        <v-chip
          v-for="state in availableStates"
          :key="state.value"
          variant="outlined"
          rounded="lg"
          class="cursor-pointer"
          @click="addState(state.value)"
        >
          <v-icon icon="mdi-plus" start size="16" />
          {{ state.title }}
        </v-chip>
      </div>
    </div>
  </div>
</template>

<style scoped>
.oc-board-column-editor__preview-track {
  display: flex;
  gap: 0.5rem;
  overflow-x: auto;
  padding-bottom: 0.25rem;
}

.oc-board-column-editor__preview-col {
  min-width: 120px;
  max-width: 160px;
  flex: 1 0 auto;
  padding: 0.65rem 0.75rem;
  border-radius: 10px;
  border: 1px dashed rgba(var(--v-theme-primary), 0.45);
  background: rgba(var(--v-theme-primary), 0.06);
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.oc-board-column-editor__row {
  overflow: hidden;
}

.oc-board-column-editor__order {
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  min-width: 36px;
}

.oc-board-column-editor__add-panel {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.1);
  background: rgba(var(--v-theme-on-surface), 0.02);
}

.cursor-pointer {
  cursor: pointer;
}
</style>
