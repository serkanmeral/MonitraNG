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
  /** Özyineleme derinliği (iç kullanım). */
  depth?: number;
}>();

const emit = defineEmits<{
  select: [folderId: string | null];
}>();

// Genişletme durumu yalnızca kök bileşende tutulur; alt bileşenler kendi state'ini yönetir.
const expandedIds = ref<Set<string>>(new Set([DI_ROOT_ID]));

watch(
  () => props.nodes,
  () => {
    // İlk yüklemede kökü ve birinci seviyeyi aç.
    const next = new Set(expandedIds.value);
    next.add(DI_ROOT_ID);
    props.nodes.forEach((n) => next.add(n.id));
    expandedIds.value = next;
  },
  { immediate: true }
);

function toggle(id: string) {
  const next = new Set(expandedIds.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  expandedIds.value = next;
}

function isExpanded(id: string) {
  return expandedIds.value.has(id);
}

const isRoot = props.depth == null || props.depth === 0;

const TOP_AREA_FOLDER_NAMES = new Set(['Sayfalar', 'Dökümanlar']);

function isTopAreaFolder(name: string): boolean {
  return isRoot && TOP_AREA_FOLDER_NAMES.has(name);
}
</script>

<template>
  <div :class="['di-tree', { 'di-tree--root': isRoot }]">
    <!-- Kök "Dokümanlar" düğümü yalnızca en üst seviyede -->
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
        <div v-if="!nodes.length" class="di-tree__empty py-1 text-caption text-medium-emphasis">
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
              v-if="node.children.length"
              class="di-tree__chevron mr-1"
              icon
              size="x-small"
              variant="text"
              @click.stop="toggle(node.id)"
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
          <div v-show="isExpanded(node.id) && node.children.length" class="di-tree__nested">
            <DiResourceTree
              :nodes="node.children"
              :selected-id="selectedId"
              :root-label="rootLabel"
              :depth="(depth ?? 0) + 1"
              @select="emit('select', $event)"
            />
          </div>
        </div>
      </div>
    </template>

    <!-- Alt seviyeler: kök satırı olmadan yalnızca düğümler -->
    <template v-else>
      <div v-for="node in nodes" :key="node.id" class="di-tree__node">
        <div
          :class="['di-tree__row', { 'di-tree__row--selected': selectedId === node.id }]"
          @click="emit('select', node.id)"
        >
          <v-btn
            v-if="node.children.length"
            class="di-tree__chevron mr-1"
            icon
            size="x-small"
            variant="text"
            @click.stop="toggle(node.id)"
          >
            <ChevronDownIcon v-if="isExpanded(node.id)" size="18" />
            <ChevronRightIcon v-else size="18" />
          </v-btn>
          <span v-else class="di-tree__chevron-spacer mr-1" />
          <FolderIcon size="18" class="mr-2 di-tree__icon-folder flex-shrink-0" />
          <span class="text-body-2 text-truncate flex-grow-1">{{ node.name }}</span>
        </div>
        <div v-show="isExpanded(node.id) && node.children.length" class="di-tree__nested">
          <DiResourceTree
            :nodes="node.children"
            :selected-id="selectedId"
            :root-label="rootLabel"
            :depth="(depth ?? 0) + 1"
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
