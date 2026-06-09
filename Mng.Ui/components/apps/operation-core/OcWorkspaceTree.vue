<script setup lang="ts">
import { ref, watch } from 'vue';
import { ChevronDownIcon, ChevronRightIcon, FoldersIcon, FolderIcon, LayoutKanbanIcon } from 'vue-tabler-icons';
import type { OcWorkspaceTreeNode } from '@/types/apps/operationCore';
import { OC_ROOT_WORKSPACES } from '@/types/apps/operationCore';

const props = defineProps<{
  workspaceNodes: OcWorkspaceTreeNode[];
  selectedWorkspaceId: string | null;
  selectedBoardId: string | null;
  emptyLabel?: string;
  labelWorkspacesRoot: string;
}>();

const emit = defineEmits<{
  'select-workspace': [workspaceId: string];
  'select-board': [workspaceId: string, boardId: string];
  'expand-workspace': [workspaceId: string];
  'expand-all-workspaces': [];
}>();

const expandedIds = ref<Set<string>>(new Set());

function defaultExpanded(): Set<string> {
  return new Set<string>([OC_ROOT_WORKSPACES]);
}

watch(
  () => props.workspaceNodes,
  () => {
    const next = defaultExpanded();
    if (props.selectedWorkspaceId) next.add(props.selectedWorkspaceId);
    expandedIds.value = next;
  },
  { immediate: true }
);

watch(
  () => props.selectedWorkspaceId,
  (id) => {
    if (!id) return;
    const next = new Set(expandedIds.value);
    next.add(OC_ROOT_WORKSPACES);
    next.add(id);
    expandedIds.value = next;
  },
  { immediate: true }
);

function isWorkspaceId(id: string): boolean {
  return id !== OC_ROOT_WORKSPACES && props.workspaceNodes.some((n) => n.data.__dataId === id);
}

function toggleExpand(id: string) {
  const next = new Set(expandedIds.value);
  const willExpand = !next.has(id);
  if (willExpand) next.add(id);
  else next.delete(id);
  expandedIds.value = next;
  if (willExpand && isWorkspaceId(id)) {
    emit('expand-workspace', id);
  }
}

function expandAll() {
  const next = defaultExpanded();
  props.workspaceNodes.forEach((n) => next.add(n.data.__dataId));
  expandedIds.value = next;
  emit('expand-all-workspaces');
}

function collapseAll() {
  expandedIds.value = new Set();
}

function onRootWorkspacesClick(e: MouseEvent) {
  const t = e.target as HTMLElement | null;
  if (t?.closest?.('.oc-wst-chevron')) return;
  toggleExpand(OC_ROOT_WORKSPACES);
}

function onWorkspaceClick(node: OcWorkspaceTreeNode, e: MouseEvent) {
  const t = e.target as HTMLElement | null;
  if (t?.closest?.('.oc-wst-chevron')) return;
  emit('select-workspace', node.data.__dataId);
}

function onBoardClick(workspaceId: string, boardId: string, e: Event) {
  e.stopPropagation();
  emit('select-board', workspaceId, boardId);
}

function workspaceSelected(wid: string) {
  return props.selectedWorkspaceId === wid && !props.selectedBoardId;
}

function boardSelected(bid: string) {
  return props.selectedBoardId === bid;
}

defineExpose({ expandAll, collapseAll });
</script>

<template>
  <div class="oc-workspace-tree">
    <div class="oc-wst-node">
      <div class="oc-wst-row oc-wst-root" @click="onRootWorkspacesClick">
        <v-btn
          class="oc-wst-chevron mr-1"
          icon
          size="x-small"
          variant="text"
          @click.stop="toggleExpand(OC_ROOT_WORKSPACES)"
        >
          <ChevronDownIcon v-if="expandedIds.has(OC_ROOT_WORKSPACES)" size="18" />
          <ChevronRightIcon v-else size="18" />
        </v-btn>
        <FoldersIcon size="20" class="mr-2 oc-wst-icon-root flex-shrink-0" />
        <span class="text-body-2 font-weight-bold flex-grow-1 text-truncate">{{ labelWorkspacesRoot }}</span>
      </div>
      <div v-show="expandedIds.has(OC_ROOT_WORKSPACES)" class="oc-wst-children oc-wst-depth-1">
        <template v-if="!workspaceNodes.length">
          <div class="oc-wst-empty py-1 text-caption text-medium-emphasis">
            {{ emptyLabel || '—' }}
          </div>
        </template>
        <div v-for="node in workspaceNodes" :key="node.data.__dataId" class="oc-wst-node">
          <div
            :class="['oc-wst-row oc-wst-workspace', { 'oc-wst-row-selected': workspaceSelected(node.data.__dataId) }]"
            @click="onWorkspaceClick(node, $event)"
          >
            <v-btn
              class="oc-wst-chevron mr-1"
              icon
              size="x-small"
              variant="text"
              @click.stop="toggleExpand(node.data.__dataId)"
            >
              <ChevronDownIcon v-if="expandedIds.has(node.data.__dataId)" size="18" />
              <ChevronRightIcon v-else size="18" />
            </v-btn>
            <FolderIcon size="20" class="mr-2 oc-wst-icon-workspace flex-shrink-0" />
            <span class="text-body-2 text-truncate flex-grow-1 font-weight-medium">{{ node.data.name }}</span>
            <span
              v-if="node.data.workItemKeyPrefix"
              class="text-caption text-medium-emphasis ml-1 flex-shrink-0"
            >{{ node.data.workItemKeyPrefix }}</span>
          </div>
          <div v-show="expandedIds.has(node.data.__dataId)" class="oc-wst-children oc-wst-depth-2">
            <div
              v-for="ch in node.children"
              :key="ch.data.__dataId"
              :class="['oc-wst-row oc-wst-board', { 'oc-wst-row-selected': boardSelected(ch.data.__dataId) }]"
              @click="onBoardClick(node.data.__dataId, ch.data.__dataId, $event)"
            >
              <LayoutKanbanIcon size="18" class="mr-2 oc-wst-icon-board flex-shrink-0" />
              <span class="text-body-2 text-truncate">{{ ch.data.name }}</span>
            </div>
            <div v-if="!node.children.length" class="oc-wst-empty py-1 text-caption text-medium-emphasis">—</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.oc-workspace-tree {
  --oc-wst-chevron: 28px;
  --oc-wst-gap1: 12px;
  --oc-wst-board-indent: calc(var(--oc-wst-chevron) + 10px);
}

.oc-wst-row {
  display: flex;
  align-items: center;
  min-height: 40px;
  padding: 4px 8px;
  border-radius: 8px;
  cursor: pointer;
  user-select: none;
  transition: background-color 0.15s ease;
}
.oc-wst-row:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
}
.oc-wst-row-selected {
  background-color: rgba(var(--v-theme-primary), 0.14);
  font-weight: 500;
}
.oc-wst-root {
  cursor: pointer;
  padding-left: 4px;
}

/* Workspace satırları: kök altında girinti + sol çizgi */
.oc-wst-depth-1 {
  margin: 2px 0 6px 0;
  padding: 4px 0 4px var(--oc-wst-gap1);
  border-left: 2px solid rgba(var(--v-theme-on-surface), 0.12);
}

/* Board satırları: workspace altında ek girinti */
.oc-wst-depth-2 {
  margin-top: 4px;
  margin-bottom: 6px;
  margin-left: 2px;
  padding: 4px 0 4px var(--oc-wst-board-indent);
  border-left: 2px solid rgba(var(--v-theme-primary), 0.22);
}

.oc-wst-board {
  padding-left: 6px;
}

.oc-wst-empty {
  opacity: 0.7;
  padding-left: 2px;
}

.oc-wst-icon-root {
  color: rgb(var(--v-theme-primary));
  opacity: 0.95;
}
.oc-wst-icon-workspace {
  color: rgba(var(--v-theme-on-surface), 0.75);
}
.oc-wst-icon-board {
  color: rgba(var(--v-theme-on-surface), 0.55);
}
</style>
