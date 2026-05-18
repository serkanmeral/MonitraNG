<script setup lang="ts">
import { ref, watch } from 'vue';
import {
  ChevronDownIcon,
  ChevronRightIcon,
  FoldersIcon,
  FolderIcon,
  LayoutKanbanIcon,
  FilterIcon,
  UserCheckIcon,
} from 'vue-tabler-icons';
import type { TmTreeProjectNode } from '@/types/apps/taskManager';
import {
  TM_WORKSPACE_ROOT_FILTERS as TM_ROOT_FILTERS,
  TM_WORKSPACE_ROOT_PROJECTS as TM_ROOT_PROJECTS,
  TM_WORKSPACE_FILTER_ASSIGNED_TO_ME as TM_FILTER_ASSIGNED_TO_ME,
} from '@/types/apps/taskManager';

const props = defineProps<{
  projectNodes: TmTreeProjectNode[];
  selectedProjectId: string | null;
  selectedBoardId: string | null;
  selectedFilterId: string | null;
  emptyLabel?: string;
  labelProjectsRoot: string;
  labelFiltersRoot: string;
  labelAssignedFilter: string;
}>();

const emit = defineEmits<{
  'select-project': [projectId: string];
  'select-board': [projectId: string, boardId: string];
  'select-filter': [filterId: string];
}>();

const expandedIds = ref<Set<string>>(new Set());

function defaultExpanded(): Set<string> {
  const s = new Set<string>([TM_ROOT_PROJECTS, TM_ROOT_FILTERS]);
  props.projectNodes.forEach((n) => s.add(n.data.__dataId));
  return s;
}

watch(
  () => props.projectNodes,
  () => {
    expandedIds.value = defaultExpanded();
  },
  { immediate: true }
);

function toggleExpand(id: string) {
  const next = new Set(expandedIds.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  expandedIds.value = next;
}

function expandAll() {
  expandedIds.value = defaultExpanded();
}

function collapseAll() {
  expandedIds.value = new Set();
}

function onRootProjectsClick(e: MouseEvent) {
  const t = e.target as HTMLElement | null;
  if (t?.closest?.('.tm-wst-chevron')) return;
  toggleExpand(TM_ROOT_PROJECTS);
}

function onRootFiltersClick(e: MouseEvent) {
  const t = e.target as HTMLElement | null;
  if (t?.closest?.('.tm-wst-chevron')) return;
  toggleExpand(TM_ROOT_FILTERS);
}

function onProjectClick(node: TmTreeProjectNode, e: MouseEvent) {
  const t = e.target as HTMLElement | null;
  if (t?.closest?.('.tm-wst-chevron')) return;
  emit('select-project', node.data.__dataId);
}

function onBoardClick(projectId: string, boardId: string, e: Event) {
  e.stopPropagation();
  emit('select-board', projectId, boardId);
}

function onAssignedClick(e: Event) {
  e.stopPropagation();
  emit('select-filter', TM_FILTER_ASSIGNED_TO_ME);
}

function projectSelected(pid: string) {
  return props.selectedProjectId === pid && !props.selectedBoardId && !props.selectedFilterId;
}

function boardSelected(bid: string) {
  return props.selectedBoardId === bid;
}

function filterSelected(fid: string) {
  return props.selectedFilterId === fid;
}

defineExpose({ expandAll, collapseAll });
</script>

<template>
  <div class="tm-workspace-tree">
      <!-- Kök: Projeler -->
      <div class="tm-wst-node">
        <div
          :class="['tm-wst-row tm-wst-root', { 'tm-wst-row-selected': false }]"
          @click="onRootProjectsClick"
        >
          <v-btn
            class="tm-wst-chevron mr-1"
            icon
            size="x-small"
            variant="text"
            @click.stop="toggleExpand(TM_ROOT_PROJECTS)"
          >
            <ChevronDownIcon v-if="expandedIds.has(TM_ROOT_PROJECTS)" size="18" />
            <ChevronRightIcon v-else size="18" />
          </v-btn>
          <FoldersIcon size="20" class="mr-2 tm-wst-icon-root flex-shrink-0" />
          <span class="text-body-2 font-weight-bold flex-grow-1 text-truncate">{{ labelProjectsRoot }}</span>
        </div>
        <div v-show="expandedIds.has(TM_ROOT_PROJECTS)" class="tm-wst-children tm-wst-depth-1">
          <template v-if="!projectNodes.length">
            <div class="tm-wst-empty py-1 text-caption text-medium-emphasis">
              {{ emptyLabel || '—' }}
            </div>
          </template>
          <div v-for="node in projectNodes" :key="node.data.__dataId" class="tm-wst-node">
            <div
              :class="['tm-wst-row tm-wst-project', { 'tm-wst-row-selected': projectSelected(node.data.__dataId) }]"
              @click="onProjectClick(node, $event)"
            >
              <v-btn
                class="tm-wst-chevron mr-1"
                icon
                size="x-small"
                variant="text"
                @click.stop="toggleExpand(node.data.__dataId)"
              >
                <ChevronDownIcon v-if="expandedIds.has(node.data.__dataId)" size="18" />
                <ChevronRightIcon v-else size="18" />
              </v-btn>
              <FolderIcon size="20" class="mr-2 tm-wst-icon-project flex-shrink-0" />
              <span class="text-body-2 text-truncate flex-grow-1 font-weight-medium">{{ node.data.name }}</span>
              <span class="text-caption text-medium-emphasis ml-1 flex-shrink-0">{{ node.data.key }}</span>
            </div>
            <div v-show="expandedIds.has(node.data.__dataId)" class="tm-wst-children tm-wst-depth-2">
              <div
                v-for="ch in node.children"
                :key="ch.data.__dataId"
                :class="['tm-wst-row tm-wst-board', { 'tm-wst-row-selected': boardSelected(ch.data.__dataId) }]"
                @click="onBoardClick(node.data.__dataId, ch.data.__dataId, $event)"
              >
                <LayoutKanbanIcon size="18" class="mr-2 tm-wst-icon-board flex-shrink-0" />
                <span class="text-body-2 text-truncate">{{ ch.data.name }}</span>
              </div>
              <div v-if="!node.children.length" class="tm-wst-empty py-1 text-caption text-medium-emphasis">—</div>
            </div>
          </div>
        </div>
      </div>

      <!-- Kök: Filtreler -->
      <div class="tm-wst-node mt-1">
        <div class="tm-wst-row tm-wst-root" @click="onRootFiltersClick">
          <v-btn
            class="tm-wst-chevron mr-1"
            icon
            size="x-small"
            variant="text"
            @click.stop="toggleExpand(TM_ROOT_FILTERS)"
          >
            <ChevronDownIcon v-if="expandedIds.has(TM_ROOT_FILTERS)" size="18" />
            <ChevronRightIcon v-else size="18" />
          </v-btn>
          <FilterIcon size="20" class="mr-2 text-secondary flex-shrink-0" />
          <span class="text-body-2 font-weight-bold flex-grow-1 text-truncate">{{ labelFiltersRoot }}</span>
        </div>
        <div v-show="expandedIds.has(TM_ROOT_FILTERS)" class="tm-wst-children tm-wst-depth-1">
          <div
            :class="['tm-wst-row tm-wst-filter', { 'tm-wst-row-selected': filterSelected(TM_FILTER_ASSIGNED_TO_ME) }]"
            @click="onAssignedClick"
          >
            <UserCheckIcon size="18" class="mr-2 tm-wst-icon-filter-leaf flex-shrink-0" />
            <span class="text-body-2 text-truncate">{{ labelAssignedFilter }}</span>
          </div>
        </div>
      </div>
  </div>
</template>

<style scoped>
.tm-workspace-tree {
  --tm-wst-chevron: 28px;
  --tm-wst-gap1: 12px;
  /** Board’lar: proje satırındaki chevron + boşluk kadar içeriden başlasın */
  --tm-wst-board-indent: calc(var(--tm-wst-chevron) + 10px);
}

.tm-wst-row {
  display: flex;
  align-items: center;
  min-height: 40px;
  padding: 4px 8px;
  border-radius: 8px;
  cursor: pointer;
  user-select: none;
  transition: background-color 0.15s ease;
}
.tm-wst-row:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
}
.tm-wst-row-selected {
  background-color: rgba(var(--v-theme-primary), 0.14);
  font-weight: 500;
}
.tm-wst-root {
  cursor: pointer;
}

/* Kök satırlar (Projeler / Filtreler): tam genişlik */
.tm-wst-root {
  padding-left: 4px;
}

/* Bir alt seviye: kökün altındaki liste — sol rehber çizgi + girinti */
.tm-wst-depth-1 {
  margin: 2px 0 6px 0;
  padding: 4px 0 4px var(--tm-wst-gap1);
  border-left: 2px solid rgba(var(--v-theme-on-surface), 0.12);
}

/* Board’lar: proje altında ek girinti (chevron sütununu geçerek hiyerarşi netleşir) */
.tm-wst-depth-2 {
  margin-top: 4px;
  margin-bottom: 6px;
  margin-left: 2px;
  padding: 4px 0 4px var(--tm-wst-board-indent);
  border-left: 2px solid rgba(var(--v-theme-primary), 0.22);
}

.tm-wst-board {
  padding-left: 6px;
}

.tm-wst-filter {
  padding-left: 2px;
}

.tm-wst-empty {
  opacity: 0.7;
  padding-left: 2px;
}

/* İkon renkleri: kök grup ≠ tek proje */
.tm-wst-icon-root {
  color: rgb(var(--v-theme-primary));
  opacity: 0.95;
}
.tm-wst-icon-project {
  color: rgba(var(--v-theme-on-surface), 0.75);
}
.tm-wst-icon-board {
  color: rgba(var(--v-theme-on-surface), 0.55);
}
.tm-wst-icon-filter-leaf {
  color: rgba(var(--v-theme-on-surface), 0.6);
}
</style>
