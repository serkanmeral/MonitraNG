<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { SecEventFilterTreeNode } from '@/types/apps/secEventFilterCatalog';
import AcSecEventFilterCatalogTreeItem from '@/components/apps/siem-center/AcSecEventFilterCatalogTreeItem.vue';

const props = defineProps<{
  nodes: SecEventFilterTreeNode[];
  selectedId: string | null;
  search: string;
}>();

const emit = defineEmits<{
  select: [node: SecEventFilterTreeNode];
  'update:search': [value: string];
  action: [payload: { action: string; node: SecEventFilterTreeNode }];
}>();

const { t } = useAppI18n();
const expandedIds = ref<Set<string>>(new Set());

function matchesSearch(node: SecEventFilterTreeNode, q: string): boolean {
  if (!q) return true;
  if (node.name.toLowerCase().includes(q)) return true;
  return (node.children ?? []).some((c) => matchesSearch(c, q));
}

function filterNode(node: SecEventFilterTreeNode, q: string): SecEventFilterTreeNode | null {
  if (!matchesSearch(node, q)) return null;
  if (node.kind === 'filter') return node;
  const children = (node.children ?? [])
    .map((c) => filterNode(c, q))
    .filter((c): c is SecEventFilterTreeNode => c != null);
  return { ...node, children };
}

const filteredNodes = computed(() => {
  const q = (props.search ?? '').trim().toLowerCase();
  if (!q) return props.nodes;
  return props.nodes
    .map((n) => filterNode(n, q))
    .filter((n): n is SecEventFilterTreeNode => n != null);
});

watch(
  () => props.nodes,
  (nodes) => {
    const next = new Set(expandedIds.value);
    for (const n of nodes) {
      if (n.kind === 'category') next.add(n.id);
    }
    expandedIds.value = next;
  },
  { immediate: true },
);

watch(
  () => props.search,
  (s) => {
    if (!(s ?? '').trim()) return;
    const next = new Set(expandedIds.value);
    function expandAll(nodes: SecEventFilterTreeNode[]) {
      for (const n of nodes) {
        if (n.kind === 'category') {
          next.add(n.id);
          if (n.children) expandAll(n.children);
        }
      }
    }
    expandAll(filteredNodes.value);
    expandedIds.value = next;
  },
);

function onSelect(node: SecEventFilterTreeNode) {
  if (node.kind === 'category') {
    const next = new Set(expandedIds.value);
    if (next.has(node.id)) next.delete(node.id);
    else next.add(node.id);
    expandedIds.value = next;
  }
  emit('select', node);
}

function onToggle(id: string) {
  const next = new Set(expandedIds.value);
  if (next.has(id)) next.delete(id);
  else next.add(id);
  expandedIds.value = next;
}
</script>

<template>
  <div class="sec-filter-tree">
    <v-text-field
      :model-value="search"
      density="compact"
      variant="outlined"
      hide-details
      clearable
      prepend-inner-icon="mdi-magnify"
      :placeholder="t('siemCenter.events.filterCatalog.treeSearch')"
      class="mb-2"
      @update:model-value="emit('update:search', String($event ?? ''))"
    />

    <div v-if="!filteredNodes.length" class="text-caption text-medium-emphasis px-1">
      {{ t('siemCenter.events.filterCatalog.treeEmpty') }}
    </div>

    <ul v-else class="sec-filter-tree__list">
      <AcSecEventFilterCatalogTreeItem
        v-for="node in filteredNodes"
        :key="node.id"
        :node="node"
        :depth="0"
        :selected-id="selectedId"
        :expanded-ids="expandedIds"
        @select="onSelect"
        @toggle="onToggle"
        @action="emit('action', $event)"
      />
    </ul>
  </div>
</template>

<style scoped>
.sec-filter-tree__list {
  list-style: none;
  margin: 0;
  padding: 0;
}

.sec-filter-tree :deep(.sec-filter-tree__list) {
  list-style: none;
  margin: 0;
  padding: 0;
}

.sec-filter-tree :deep(.sec-filter-tree__row) {
  border-radius: 6px;
}

.sec-filter-tree :deep(.sec-filter-tree__row:hover) {
  background: rgba(var(--v-theme-on-surface), 0.04);
}

.sec-filter-tree :deep(.sec-filter-tree__row--selected) {
  background: rgba(var(--v-theme-primary), 0.1);
  color: rgb(var(--v-theme-primary));
}

.sec-filter-tree :deep(.sec-filter-tree__icon) {
  opacity: 0.8;
}

.sec-filter-tree :deep(.sec-filter-tree__label) {
  flex: 1;
  min-width: 0;
  font-size: 0.8125rem;
}

.sec-filter-tree :deep(.sec-filter-tree__lock) {
  opacity: 0.45;
}
</style>
