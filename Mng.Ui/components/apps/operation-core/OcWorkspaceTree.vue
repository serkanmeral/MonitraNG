<script setup lang="ts">
import { ref, watch } from 'vue';
import { ChevronDownIcon, ChevronRightIcon, DotsVerticalIcon, FoldersIcon } from 'vue-tabler-icons';
import type { OcNavNode, OpWorkspace, OpWorkspaceFolder } from '@/types/apps/operationCore';
import { OC_ROOT_WORKSPACES } from '@/types/apps/operationCore';
import OcWorkspaceTreeBranch from '@/components/apps/operation-core/OcWorkspaceTreeBranch.vue';
import { useAppI18n } from '@/composables/useAppI18n';

const { t } = useAppI18n();

const props = defineProps<{
  nodes: OcNavNode[];
  selectedWorkspaceId: string | null;
  selectedBoardId: string | null;
  canManage: boolean;
  revealIds?: string[];
  emptyLabel?: string;
  labelWorkspacesRoot: string;
}>();

const emit = defineEmits<{
  'select-workspace': [workspaceId: string];
  'select-board': [workspaceId: string, boardId: string];
  'expand-workspace': [workspaceId: string];
  'expand-all-workspaces': [];
  'create-folder': [parentFolderId: string | null];
  'rename-folder': [folder: OpWorkspaceFolder];
  'move-folder': [folder: OpWorkspaceFolder];
  'delete-folder': [folder: OpWorkspaceFolder];
  'move-workspace': [workspace: OpWorkspace];
}>();

const expandedIds = ref<Set<string>>(new Set([OC_ROOT_WORKSPACES]));

function collectIds(nodes: OcNavNode[], into: Set<string>) {
  for (const node of nodes) {
    if (node.type === 'folder') {
      into.add(node.folder.__dataId);
      collectIds(node.children, into);
    } else {
      into.add(node.data.__dataId);
    }
  }
}

function toggleExpand(id: string) {
  const next = new Set(expandedIds.value);
  const willExpand = !next.has(id);
  if (willExpand) next.add(id);
  else next.delete(id);
  expandedIds.value = next;
  if (willExpand && id !== OC_ROOT_WORKSPACES) {
    const workspace = findWorkspace(props.nodes, id);
    if (workspace) emit('expand-workspace', id);
  }
}

function findWorkspace(nodes: OcNavNode[], id: string): boolean {
  for (const node of nodes) {
    if (node.type === 'workspace' && node.data.__dataId === id) return true;
    if (node.type === 'folder' && findWorkspace(node.children, id)) return true;
  }
  return false;
}

function expandAll() {
  const next = new Set<string>([OC_ROOT_WORKSPACES]);
  collectIds(props.nodes, next);
  expandedIds.value = next;
  emit('expand-all-workspaces');
}

function collapseAll() {
  expandedIds.value = new Set();
}

watch(
  () => [props.selectedWorkspaceId, props.revealIds?.join('|') ?? ''] as const,
  () => {
    const next = new Set(expandedIds.value);
    next.add(OC_ROOT_WORKSPACES);
    if (props.selectedWorkspaceId) next.add(props.selectedWorkspaceId);
    for (const id of props.revealIds ?? []) next.add(id);
    expandedIds.value = next;
  },
  { immediate: true }
);

defineExpose({ expandAll, collapseAll });
</script>

<template>
  <div class="oc-workspace-tree">
    <div class="oc-wst-node">
      <div class="oc-wst-row oc-wst-root" @click="toggleExpand(OC_ROOT_WORKSPACES)">
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
        <v-menu v-if="canManage" location="bottom end">
          <template #activator="{ props: menuProps }">
            <v-btn v-bind="menuProps" class="oc-wst-menu" icon size="x-small" variant="text" @click.stop>
              <DotsVerticalIcon size="16" />
            </v-btn>
          </template>
          <v-list density="compact" min-width="180">
            <v-list-item :title="t('operationCore.workspace.folders.newRoot')" @click="emit('create-folder', null)" />
          </v-list>
        </v-menu>
      </div>
      <OcWorkspaceTreeBranch
        v-show="expandedIds.has(OC_ROOT_WORKSPACES)"
        :nodes="nodes"
        :depth="1"
        :expanded-ids="expandedIds"
        :selected-workspace-id="selectedWorkspaceId"
        :selected-board-id="selectedBoardId"
        :can-manage="canManage"
        @toggle="toggleExpand"
        @select-workspace="emit('select-workspace', $event)"
        @select-board="(workspaceId, boardId) => emit('select-board', workspaceId, boardId)"
        @create-folder="emit('create-folder', $event)"
        @rename-folder="emit('rename-folder', $event)"
        @move-folder="emit('move-folder', $event)"
        @delete-folder="emit('delete-folder', $event)"
        @move-workspace="emit('move-workspace', $event)"
      />
      <div
        v-if="expandedIds.has(OC_ROOT_WORKSPACES) && !nodes.length"
        class="oc-wst-empty py-1 text-caption text-medium-emphasis oc-wst-depth-1"
      >
        {{ emptyLabel || '—' }}
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

.oc-workspace-tree :deep(.oc-wst-row) {
  display: flex;
  align-items: center;
  min-height: 40px;
  padding: 4px 8px;
  border-radius: 8px;
  cursor: pointer;
  user-select: none;
  transition: background-color 0.15s ease;
}
.oc-workspace-tree :deep(.oc-wst-row:hover) {
  background-color: rgba(var(--v-theme-primary), 0.08);
}
.oc-workspace-tree :deep(.oc-wst-row-selected) {
  background-color: rgba(var(--v-theme-primary), 0.14);
  font-weight: 500;
}
.oc-wst-root {
  cursor: pointer;
  padding-left: 4px;
}
.oc-workspace-tree :deep(.oc-wst-depth-1) {
  margin: 2px 0 6px 0;
  padding: 4px 0 4px var(--oc-wst-gap1);
  border-left: 2px solid rgba(var(--v-theme-on-surface), 0.12);
}
.oc-workspace-tree :deep(.oc-wst-depth-n) {
  margin: 2px 0 4px 8px;
  padding: 2px 0 2px 10px;
  border-left: 2px solid rgba(var(--v-theme-on-surface), 0.12);
}
.oc-workspace-tree :deep(.oc-wst-depth-board) {
  margin-top: 4px;
  margin-bottom: 6px;
  margin-left: 8px;
  padding: 4px 0 4px var(--oc-wst-board-indent);
  border-left: 2px solid rgba(var(--v-theme-primary), 0.22);
}
.oc-workspace-tree :deep(.oc-wst-board) {
  padding-left: 6px;
}
.oc-workspace-tree :deep(.oc-wst-empty) {
  opacity: 0.7;
  padding-left: 8px;
}
.oc-wst-icon-root,
.oc-workspace-tree :deep(.oc-wst-icon-folder) {
  color: rgb(var(--v-theme-primary));
  opacity: 0.95;
}
.oc-workspace-tree :deep(.oc-wst-icon-workspace) {
  color: rgba(var(--v-theme-on-surface), 0.75);
}
.oc-workspace-tree :deep(.oc-wst-icon-board) {
  color: rgba(var(--v-theme-on-surface), 0.55);
}
</style>
