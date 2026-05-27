<script setup lang="ts">
import OcWorkItemCard from '@/components/apps/operation-core/OcWorkItemCard.vue';
import type { OcBoardColumn, OcColumnItemsState } from '@/types/apps/operationCore';
import { useAppI18n } from '@/composables/useAppI18n';

const props = defineProps<{
  columns: OcBoardColumn[];
  columnItems: Record<string, OcColumnItemsState>;
  columnLoading: Record<string, boolean>;
  boardId: string;
}>();

const { t } = useAppI18n();

function columnTitle(col: OcBoardColumn): string {
  return col.title?.trim() || col.stateId;
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

        <div v-else class="d-flex flex-column ga-2">
          <OcWorkItemCard
            v-for="card in columnItems[col.stateId]?.items ?? []"
            :key="card.id"
            :card="card"
            :board-id="boardId"
          />
          <div
            v-if="!(columnItems[col.stateId]?.items?.length)"
            class="text-caption text-medium-emphasis text-center py-4"
          >
            {{ t('operationCore.board.columnEmpty') }}
          </div>
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
</style>
