<script setup lang="ts">
import { computed } from 'vue';
import { FolderIcon, DeviceDesktopIcon, ChevronRightIcon, ChevronDownIcon } from 'vue-tabler-icons';
import type { OrganizationTreeNode, OrganizationSelectedNode } from '@/types/apps/organization';
import OrganizationTreeItem from './OrganizationTreeItem.vue';

const props = withDefaults(
  defineProps<{
    node: OrganizationTreeNode;
    selectedNode: OrganizationSelectedNode | null;
    expandedIds: Set<string>;
    level?: number;
  }>(),
  { level: 0 }
);

const emit = defineEmits<{
  'node-select': [node: OrganizationSelectedNode];
  'toggle-expand': [node: OrganizationTreeNode];
}>();

const hasChildren = computed(() => props.node.type === 'item' && props.node.children.length > 0);
const canExpand = computed(() => props.node.type === 'item');
const isExpanded = computed(() =>
  props.node.type === 'item' && props.node.data.__dataId
    ? props.expandedIds.has(props.node.data.__dataId)
    : false
);
const isSelected = computed(() => {
  if (!props.selectedNode) return false;
  const id = props.node.data.__dataId;
  return props.selectedNode.type === props.node.type && props.selectedNode.data?.__dataId === id;
});
const label = computed(() => props.node.data.name ?? '');

function toggleExpand(e: Event) {
  e.stopPropagation();
  if (props.node.type === 'item') emit('toggle-expand', props.node);
}

function onNodeClick() {
  if (props.node.type === 'item') {
    emit('node-select', { type: 'item', data: props.node.data });
  } else {
    emit('node-select', { type: 'asset', data: props.node.data });
  }
}
</script>

<template>
  <div class="org-tree-item-wrapper">
    <div
      :class="['org-tree-item', { 'org-tree-item-selected': isSelected }]"
      :style="{ paddingLeft: `${level * 24 + 12}px` }"
      @click="onNodeClick"
    >
      <div class="d-flex align-center org-tree-item-content">
        <v-btn
          v-if="canExpand"
          icon
          size="small"
          variant="text"
          class="mr-1"
          @click="toggleExpand"
        >
          <ChevronDownIcon v-if="isExpanded" size="20" />
          <ChevronRightIcon v-else size="20" />
        </v-btn>
        <div v-else class="mr-3" style="width: 28px;" />
        <component
          :is="node.type === 'item' ? FolderIcon : DeviceDesktopIcon"
          size="22"
          :class="['mr-2', node.type === 'item' ? 'text-primary' : 'text-secondary']"
        />
        <span class="text-body-1 flex-grow-1">{{ label }}</span>
      </div>
    </div>
    <div v-if="canExpand && isExpanded && node.type === 'item'" class="org-tree-item-children">
      <OrganizationTreeItem
        v-for="child in node.children"
        :key="child.type === 'item' ? child.data.__dataId : child.data.__dataId"
        :node="child"
        :selected-node="selectedNode"
        :expanded-ids="expandedIds"
        :level="level + 1"
        @node-select="$emit('node-select', $event)"
        @toggle-expand="$emit('toggle-expand', $event)"
      />
    </div>
  </div>
</template>

<style scoped>
.org-tree-item-wrapper {
  user-select: none;
}
.org-tree-item {
  transition: background-color 0.2s;
  border-radius: 6px;
  margin: 2px 0;
}
.org-tree-item-content {
  min-height: 44px;
  cursor: pointer;
  padding: 4px 8px;
}
.org-tree-item:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
}
.org-tree-item-selected {
  background-color: rgba(var(--v-theme-primary), 0.12);
  font-weight: 500;
}
.org-tree-item-children {
  margin-left: 0;
}
</style>
