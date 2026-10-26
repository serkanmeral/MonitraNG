<script setup lang="ts">
import { ChevronDownIcon, ChevronRightIcon, DotsVerticalIcon, FolderIcon, LayoutKanbanIcon } from 'vue-tabler-icons';
import type { OcNavNode, OpWorkspace, OpWorkspaceFolder } from '@/types/apps/operationCore';
import OcWorkspaceTreeBranch from '@/components/apps/operation-core/OcWorkspaceTreeBranch.vue';
import { useAppI18n } from '@/composables/useAppI18n';

const { t } = useAppI18n();

const props = defineProps<{
  nodes: OcNavNode[];
  depth: number;
  expandedIds: Set<string>;
  selectedWorkspaceId: string | null;
  selectedBoardId: string | null;
  canManage: boolean;
}>();

const emit = defineEmits<{
  toggle: [id: string];
  'select-workspace': [workspaceId: string];
  'select-board': [workspaceId: string, boardId: string];
  'create-folder': [parentFolderId: string | null];
  'rename-folder': [folder: OpWorkspaceFolder];
  'move-folder': [folder: OpWorkspaceFolder];
  'delete-folder': [folder: OpWorkspaceFolder];
  'move-workspace': [workspace: OpWorkspace];
}>();

function workspaceSelected(id: string) {
  return props.selectedWorkspaceId === id && !props.selectedBoardId;
}

function onFolderClick(id: string, event: MouseEvent) {
  const target = event.target as HTMLElement | null;
  if (target?.closest?.('.oc-wst-chevron, .oc-wst-menu')) return;
  emit('toggle', id);
}

function onWorkspaceClick(workspaceId: string, event: MouseEvent) {
  const target = event.target as HTMLElement | null;
  if (target?.closest?.('.oc-wst-chevron, .oc-wst-menu')) return;
  emit('select-workspace', workspaceId);
}
</script>

<template>
  <div class="oc-wst-children" :class="depth === 1 ? 'oc-wst-depth-1' : 'oc-wst-depth-n'">
    <div v-for="node in nodes" :key="node.type === 'folder' ? node.folder.__dataId : node.data.__dataId" class="oc-wst-node">
      <template v-if="node.type === 'folder'">
        <div class="oc-wst-row oc-wst-folder" @click="onFolderClick(node.folder.__dataId, $event)">
          <v-btn
            class="oc-wst-chevron mr-1"
            icon
            size="x-small"
            variant="text"
            @click.stop="emit('toggle', node.folder.__dataId)"
          >
            <ChevronDownIcon v-if="expandedIds.has(node.folder.__dataId)" size="18" />
            <ChevronRightIcon v-else size="18" />
          </v-btn>
          <FolderIcon size="20" class="mr-2 oc-wst-icon-folder flex-shrink-0" />
          <span class="text-body-2 text-truncate flex-grow-1 font-weight-medium">{{ node.folder.name }}</span>
          <v-menu v-if="canManage" location="bottom end">
            <template #activator="{ props: menuProps }">
              <v-btn
                v-bind="menuProps"
                class="oc-wst-menu"
                icon
                size="x-small"
                variant="text"
                @click.stop
              >
                <DotsVerticalIcon size="16" />
              </v-btn>
            </template>
            <v-list density="compact" min-width="180">
              <v-list-item :title="t('operationCore.workspace.folders.newChild')" @click="emit('create-folder', node.folder.__dataId)" />
              <v-list-item :title="t('operationCore.workspace.folders.rename')" @click="emit('rename-folder', node.folder)" />
              <v-list-item :title="t('operationCore.workspace.folders.move')" @click="emit('move-folder', node.folder)" />
              <v-list-item :title="t('operationCore.workspace.folders.delete')" @click="emit('delete-folder', node.folder)" />
            </v-list>
          </v-menu>
        </div>
        <OcWorkspaceTreeBranch
          v-show="expandedIds.has(node.folder.__dataId)"
          :nodes="node.children"
          :depth="depth + 1"
          :expanded-ids="expandedIds"
          :selected-workspace-id="selectedWorkspaceId"
          :selected-board-id="selectedBoardId"
          :can-manage="canManage"
          @toggle="emit('toggle', $event)"
          @select-workspace="emit('select-workspace', $event)"
          @select-board="(workspaceId, boardId) => emit('select-board', workspaceId, boardId)"
          @create-folder="emit('create-folder', $event)"
          @rename-folder="emit('rename-folder', $event)"
          @move-folder="emit('move-folder', $event)"
          @delete-folder="emit('delete-folder', $event)"
          @move-workspace="emit('move-workspace', $event)"
        />
      </template>

      <template v-else>
        <div
          :class="['oc-wst-row oc-wst-workspace', { 'oc-wst-row-selected': workspaceSelected(node.data.__dataId) }]"
          @click="onWorkspaceClick(node.data.__dataId, $event)"
        >
          <v-btn
            class="oc-wst-chevron mr-1"
            icon
            size="x-small"
            variant="text"
            @click.stop="emit('toggle', node.data.__dataId)"
          >
            <ChevronDownIcon v-if="expandedIds.has(node.data.__dataId)" size="18" />
            <ChevronRightIcon v-else size="18" />
          </v-btn>
          <FolderIcon size="20" class="mr-2 oc-wst-icon-workspace flex-shrink-0" />
          <span class="text-body-2 text-truncate flex-grow-1 font-weight-medium">{{ node.data.name }}</span>
          <span v-if="node.data.workItemKeyPrefix" class="text-caption text-medium-emphasis ml-1 flex-shrink-0">
            {{ node.data.workItemKeyPrefix }}
          </span>
          <v-menu v-if="canManage" location="bottom end">
            <template #activator="{ props: menuProps }">
              <v-btn v-bind="menuProps" class="oc-wst-menu" icon size="x-small" variant="text" @click.stop>
                <DotsVerticalIcon size="16" />
              </v-btn>
            </template>
            <v-list density="compact" min-width="160">
              <v-list-item :title="t('operationCore.workspace.folders.move')" @click="emit('move-workspace', node.data)" />
            </v-list>
          </v-menu>
        </div>
        <div v-show="expandedIds.has(node.data.__dataId)" class="oc-wst-children oc-wst-depth-board">
          <div
            v-for="board in node.children"
            :key="board.data.__dataId"
            :class="['oc-wst-row oc-wst-board', { 'oc-wst-row-selected': selectedBoardId === board.data.__dataId }]"
            @click.stop="emit('select-board', node.data.__dataId, board.data.__dataId)"
          >
            <LayoutKanbanIcon size="18" class="mr-2 oc-wst-icon-board flex-shrink-0" />
            <span class="text-body-2 text-truncate">{{ board.data.name }}</span>
          </div>
          <div v-if="!node.children.length" class="oc-wst-empty py-1 text-caption text-medium-emphasis">—</div>
        </div>
      </template>
    </div>
  </div>
</template>
