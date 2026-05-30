<script setup lang="ts">
import { ref, watch } from 'vue';
import OcWorkItemCard from '@/components/apps/operation-core/OcWorkItemCard.vue';
import type { OcBoardColumn, OcColumnItemsState, OcWorkItemCard as OcWorkItemCardType } from '@/types/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  columns: OcBoardColumn[];
  columnItems: Record<string, OcColumnItemsState>;
  columnLoading: Record<string, boolean>;
  boardId: string;
  /** Sürükle-bırak (transition) etkin mi — board permissions.canEdit. */
  editable?: boolean;
}>();

const emit = defineEmits<{
  (e: 'transition', payload: { card: OcWorkItemCardType; fromStateId: string; toStateId: string }): void;
}>();

const { t } = useAppI18n();

// vue-draggable-next bağlı listeyi yerinde mutasyona uğratır → store state'i değil
// yerel bir kopya üzerinde çalışırız. Parent transition sonrası kolonları yeniler;
// columnItems değişince yerel kopya yeniden kurulur (optimistic taşımayı düzeltir / geri alır).
const localItems = ref<Record<string, OcWorkItemCardType[]>>({});

watch(
  () => [props.columns, props.columnItems] as const,
  () => {
    const next: Record<string, OcWorkItemCardType[]> = {};
    for (const col of props.columns) {
      next[col.stateId] = [...(props.columnItems[col.stateId]?.items ?? [])];
    }
    localItems.value = next;
  },
  { immediate: true, deep: true }
);

const dragSourceState = ref<string | null>(null);

function columnTitle(col: OcBoardColumn): string {
  return col.title?.trim() || col.stateId;
}

function groupFor(col: OcBoardColumn) {
  // Bu kolona giriş geçişi yoksa (dropEligible=false) bırakma reddedilir; sürükleyip çıkarma serbest.
  return { name: 'oc-board', pull: true, put: props.editable === true && col.dropEligible };
}

function onDragStart(stateId: string) {
  dragSourceState.value = stateId;
}

interface DraggableChangeEvent {
  added?: { element: OcWorkItemCardType; newIndex: number };
  removed?: { element: OcWorkItemCardType; oldIndex: number };
  moved?: unknown;
}

function onColumnChange(targetStateId: string, evt: DraggableChangeEvent) {
  const card = evt?.added?.element;
  if (!card) return;
  const fromStateId = dragSourceState.value;
  dragSourceState.value = null;
  if (!fromStateId || fromStateId === targetStateId) return;
  emit('transition', { card, fromStateId, toStateId: targetStateId });
}
</script>

<template>
  <div class="oc-kanban-wrap">
    <div class="d-flex flex-nowrap pb-2 oc-kanban-scroll">
      <div v-for="col in columns" :key="col.stateId" class="oc-col">
        <div class="oc-col-head d-flex align-center justify-space-between">
          <span>{{ columnTitle(col) }}</span>
          <v-chip v-if="columnItems[col.stateId]" size="x-small" variant="tonal">
            {{ columnItems[col.stateId]?.total ?? 0 }}
          </v-chip>
        </div>

        <div v-if="columnLoading[col.stateId]" class="pa-4 d-flex justify-center">
          <v-progress-circular indeterminate color="primary" size="28" />
        </div>

        <v-alert
          v-else-if="columnItems[col.stateId]?.error"
          type="error"
          variant="tonal"
          density="compact"
          class="ma-2 text-caption"
        >
          {{ columnItems[col.stateId]?.error }}
        </v-alert>

        <draggable
          v-else
          class="d-flex flex-column ga-2 oc-col-drop"
          :list="localItems[col.stateId]"
          :group="groupFor(col)"
          :animation="160"
          :disabled="!editable"
          item-key="id"
          ghost-class="oc-card-ghost"
          @start="onDragStart(col.stateId)"
          @change="onColumnChange(col.stateId, $event)"
        >
          <div v-for="card in localItems[col.stateId] ?? []" :key="card.id" class="oc-card-drag">
            <OcWorkItemCard :card="card" :board-id="boardId" />
          </div>
        </draggable>

        <div
          v-if="!columnLoading[col.stateId] && !columnItems[col.stateId]?.error && !(localItems[col.stateId]?.length)"
          class="text-caption text-medium-emphasis text-center py-4"
        >
          {{ t('operationCore.board.columnEmpty') }}
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.oc-kanban-scroll {
  overflow-x: auto;
  align-items: flex-start;
  gap: 12px;
}

.oc-col-drop {
  min-height: 40px;
}

.oc-card-drag {
  cursor: grab;
}

.oc-card-ghost {
  opacity: 0.5;
}
</style>
