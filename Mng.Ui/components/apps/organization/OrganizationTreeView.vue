<script setup lang="ts">
import { ref, watch } from 'vue';
import { ChevronDownIcon, ChevronUpIcon } from 'vue-tabler-icons';
import type { OrganizationTreeNode, OrganizationSelectedNode } from '@/types/apps/organization';
import OrganizationTreeItem from './OrganizationTreeItem.vue';

const props = defineProps<{
  items: OrganizationTreeNode[];
  selectedNode: OrganizationSelectedNode | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  'node-select': [node: OrganizationSelectedNode];
}>();

const expandedIds = ref<Set<string>>(new Set());

function autoExpand(nodes: OrganizationTreeNode[]) {
  nodes.forEach((node) => {
    if (node.type === 'item' && node.data.__dataId && node.children.length > 0) {
      expandedIds.value.add(node.data.__dataId);
      autoExpand(node.children);
    }
  });
}

watch(
  () => props.items,
  (newItems) => {
    expandedIds.value.clear();
    if (newItems?.length) autoExpand(newItems);
  },
  { immediate: true }
);

function toggleExpand(node: OrganizationTreeNode) {
  if (node.type !== 'item' || !node.data.__dataId) return;
  if (expandedIds.value.has(node.data.__dataId)) {
    expandedIds.value.delete(node.data.__dataId);
  } else {
    expandedIds.value.add(node.data.__dataId);
  }
}

function expandAll() {
  expandedIds.value = new Set();
  const collect = (nodes: OrganizationTreeNode[]) => {
    nodes.forEach((n) => {
      if (n.type === 'item' && n.data.__dataId && n.children.length > 0) {
        expandedIds.value.add(n.data.__dataId);
        collect(n.children);
      }
    });
  };
  collect(props.items);
}

function collapseAll() {
  expandedIds.value.clear();
}

function onNodeSelect(node: OrganizationSelectedNode) {
  emit('node-select', node);
}

defineExpose({ expandAll, collapseAll });
</script>

<template>
  <div class="org-tree-view">
    <v-progress-linear v-if="loading" indeterminate color="primary" />
    <div
      v-else-if="!items?.length"
      class="text-center pa-4 text-medium-emphasis"
    >
      Organizasyon ağacı boş. Yeni Item veya Asset ekleyin.
    </div>
    <div v-else class="tree-container pa-3">
      <OrganizationTreeItem
        v-for="node in items"
        :key="node.type === 'item' ? node.data.__dataId : node.data.__dataId"
        :node="node"
        :selected-node="selectedNode"
        :expanded-ids="expandedIds"
        :level="0"
        @node-select="onNodeSelect"
        @toggle-expand="toggleExpand"
      />
    </div>
  </div>
</template>

<style scoped>
.org-tree-view {
  min-height: 200px;
}
.tree-container {
  max-height: calc(100vh - 300px);
  overflow-y: auto;
}
</style>
