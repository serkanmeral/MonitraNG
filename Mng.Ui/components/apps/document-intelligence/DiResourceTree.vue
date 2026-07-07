<script setup lang="ts">
import { ref, watch } from 'vue';
import { ChevronDownIcon, ChevronRightIcon, FolderIcon, FoldersIcon } from 'vue-tabler-icons';
import type { DiTreeNode } from '@/types/apps/documentIntelligence';
import { DI_ROOT_ID } from '@/types/apps/documentIntelligence';

const props = defineProps<{
  nodes: DiTreeNode[];
  selectedId: string | null;
  rootLabel: string;
  emptyLabel?: string;
  depth?: number;
  /** Lazy: parentId ile alt klasörleri yükler. */
  loadChildren?: (parentId: string) => Promise<DiTreeNode[]>;
  /** parentId null = kök seviyesi yükleniyor. */
  isLoading?: (parentId: string | null) => boolean;
  /** Derin link: bu düğümler başlangıçta açık. */
  initialExpandedIds?: string[];
}>();

const emit = defineEmits<{
  select: [folderId: string | null];
}>();

const expandedIds = ref<Set<string>>(new Set([DI_ROOT_ID]));
const loadingNodeIds = ref<Set<string>>(new Set());

watch(
  () => props.initialExpandedIds,
  (ids) => {
    if (!ids?.length) return;
    const next = new Set(expandedIds.value);
    next.add(DI_ROOT_ID);
    for (const id of ids) next.add(id);
    expandedIds.value = next;
  },
  { immediate: true },
);

function showChevron(node: DiTreeNode): boolean {
  return node.hasChildren || (node.children?.length ?? 0) > 0;
}

function isExpanded(id: string) {
  return expandedIds.value.has(id);
}

function isNodeLoading(id: string | null) {
  if (props.isLoading?.(id)) return true;
  if (id && loadingNodeIds.value.has(id)) return true;
  return false;
}

async function toggle(id: string, node?: DiTreeNode) {
  const next = new Set(expandedIds.value);
  if (next.has(id)) {
    next.delete(id);
    expandedIds.value = next;
    return;
  }

  if (node && props.loadChildren && node.hasChildren && !(node.children?.length)) {
    loadingNodeIds.value.add(node.id);
    try {
      node.children = await props.loadChildren(node.id);
    } finally {
      loadingNodeIds.value.delete(node.id);
    }
  }

  next.add(id);
  expandedIds.value = next;
}

const isRoot = props.depth == null || props.depth === 0;

const TOP_AREA_FOLDER_NAMES = new Set(['Sayfalar', 'Dökümanlar']);

function isTopAreaFolder(name: string): boolean {
  return isRoot && TOP_AREA_FOLDER_NAMES.has(name);
}
</script>

<template>
  <div :class="['di-tree', { 'di-tree--root': isRoot }]">
    <template v-if="isRoot">
      <div
        :class="['di-tree__row di-tree__row--root', { 'di-tree__row--selected': selectedId === null }]"
        @click="emit('select', null)"
      >
        <v-btn
          class="di-tree__chevron mr-1"
          icon
          size="x-small"
          variant="text"
          @click.stop="toggle(DI_ROOT_ID)"
        >
          <ChevronDownIcon v-if="isExpanded(DI_ROOT_ID)" size="18" />
          <ChevronRightIcon v-else size="18" />
        </v-btn>
        <FoldersIcon size="20" class="mr-2 di-tree__icon-root flex-shrink-0" />
        <span class="text-body-2 font-weight-bold flex-grow-1 text-truncate">{{ rootLabel }}</span>
      </div>

      <div v-show="isExpanded(DI_ROOT_ID)" class="di-tree__children">
        <div v-if="isNodeLoading(null)" class="di-tree__empty py-1 text-caption text-medium-emphasis">
          <v-progress-circular indeterminate size="14" width="2" class="mr-2" />
        </div>
        <div v-else-if="!nodes.length" class="di-tree__empty py-1 text-caption text-medium-emphasis">
          {{ emptyLabel || '—' }}
        </div>
        <div v-for="node in nodes" :key="node.id" class="di-tree__node">
          <div
            :class="[
              'di-tree__row',
              { 'di-tree__row--selected': selectedId === node.id },
              { 'di-tree__row--area-root': isTopAreaFolder(node.name) },
            ]"
            @click="emit('select', node.id)"
          >
            <v-btn
              v-if="showChevron(node)"
              class="di-tree__chevron mr-1"
              icon
              size="x-small"
              variant="text"
              :loading="isNodeLoading(node.id)"
              @click.stop="toggle(node.id, node)"
            >
              <ChevronDownIcon v-if="isExpanded(node.id)" size="18" />
              <ChevronRightIcon v-else size="18" />
            </v-btn>
            <span v-else class="di-tree__chevron-spacer mr-1" />
            <FolderIcon
              size="18"
              :class="[
                'mr-2 flex-shrink-0',
                isTopAreaFolder(node.name) ? 'di-tree__icon-area' : 'di-tree__icon-folder',
              ]"
            />
            <span
              :class="[
                'text-truncate flex-grow-1',
                isTopAreaFolder(node.name) ? 'text-body-2 font-weight-bold' : 'text-body-2',
              ]"
            >{{ node.name }}</span>
          </div>
          <div v-show="isExpanded(node.id) && (node.children?.length || isNodeLoading(node.id))" class="di-tree__nested">
            <div v-if="isNodeLoading(node.id) && !(node.children?.length)" class="di-tree__empty py-1 text-caption text-medium-emphasis pl-6">
              <v-progress-circular indeterminate size="14" width="2" class="mr-2" />
            </div>
            <DiResourceTree
              v-else-if="node.children?.length"
              :nodes="node.children"
              :selected-id="selectedId"
              :root-label="rootLabel"
              :depth="(depth ?? 0) + 1"
              :load-children="loadChildren"
              :is-loading="isLoading"
              :initial-expanded-ids="initialExpandedIds"
              @select="emit('select', $event)"
            />
          </div>
        </div>
      </div>
    </template>

    <template v-else>
      <div v-for="node in nodes" :key="node.id" class="di-tree__node">
        <div
          :class="['di-tree__row', { 'di-tree__row--selected': selectedId === node.id }]"
          @click="emit('select', node.id)"
        >
          <v-btn
            v-if="showChevron(node)"
            class="di-tree__chevron mr-1"
            icon
            size="x-small"
            variant="text"
            :loading="isNodeLoading(node.id)"
            @click.stop="toggle(node.id, node)"
          >
            <ChevronDownIcon v-if="isExpanded(node.id)" size="18" />
            <ChevronRightIcon v-else size="18" />
          </v-btn>
          <span v-else class="di-tree__chevron-spacer mr-1" />
          <FolderIcon size="18" class="mr-2 di-tree__icon-folder flex-shrink-0" />
          <span class="text-body-2 text-truncate flex-grow-1">{{ node.name }}</span>
        </div>
        <div v-show="isExpanded(node.id) && (node.children?.length || isNodeLoading(node.id))" class="di-tree__nested">
          <div v-if="isNodeLoading(node.id) && !(node.children?.length)" class="di-tree__empty py-1 text-caption text-medium-emphasis pl-6">
            <v-progress-circular indeterminate size="14" width="2" class="mr-2" />
          </div>
          <DiResourceTree
            v-else-if="node.children?.length"
            :nodes="node.children"
            :selected-id="selectedId"
            :root-label="rootLabel"
            :depth="(depth ?? 0) + 1"
            :load-children="loadChildren"
            :is-loading="isLoading"
            :initial-expanded-ids="initialExpandedIds"
            @select="emit('select', $event)"
          />
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.di-tree__row {
  display: flex;
  align-items: center;
  min-height: 36px;
  padding: 2px 8px;
  border-radius: 8px;
  cursor: pointer;
  user-select: none;
  transition: background-color 0.15s ease;
}
.di-tree__row:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
}
.di-tree__row--selected {
  background-color: rgba(var(--v-theme-primary), 0.14);
  font-weight: 500;
}
.di-tree__row--root {
  padding-left: 4px;
}
.di-tree__children {
  margin: 2px 0 2px 0;
  padding-left: 8px;
}
.di-tree__nested {
  padding-left: 14px;
  border-left: 2px solid rgba(var(--v-theme-on-surface), 0.1);
  margin-left: 10px;
}
.di-tree__chevron-spacer {
  display: inline-block;
  width: 28px;
}
.di-tree__empty {
  opacity: 0.7;
  padding-left: 8px;
}
.di-tree__icon-root {
  color: rgb(var(--v-theme-primary));
}
.di-tree__row--area-root {
  background-color: rgba(var(--v-theme-primary), 0.04);
}
.di-tree__icon-area {
  color: rgb(var(--v-theme-primary));
}
.di-tree__icon-folder {
  color: rgba(var(--v-theme-on-surface), 0.6);
}
</style>
