<script setup lang="ts">
import type { SecEventFilterTreeNode } from '@/types/apps/secEventFilterCatalog';

defineOptions({ name: 'AcSecEventFilterCatalogTreeItem' });

defineProps<{
  node: SecEventFilterTreeNode;
  depth: number;
  selectedId: string | null;
  expandedIds: Set<string>;
}>();

const emit = defineEmits<{
  select: [node: SecEventFilterTreeNode];
  toggle: [id: string];
}>();
</script>

<template>
  <li class="sec-filter-tree__item">
    <button
      type="button"
      class="sec-filter-tree__row"
      :class="{ 'sec-filter-tree__row--selected': selectedId === node.id }"
      :style="{ paddingLeft: `${8 + depth * 14}px` }"
      @click="emit('select', node)"
    >
      <v-icon
        :icon="
          node.kind === 'filter'
            ? 'mdi-filter-outline'
            : expandedIds.has(node.id)
              ? 'mdi-folder-open-outline'
              : 'mdi-folder-outline'
        "
        size="18"
        class="sec-filter-tree__icon"
      />
      <span class="sec-filter-tree__label text-truncate">{{ node.name }}</span>
      <v-icon v-if="node.isSystem" icon="mdi-lock-outline" size="14" class="sec-filter-tree__lock" />
    </button>
    <ul
      v-if="node.kind === 'category' && expandedIds.has(node.id) && node.children?.length"
      class="sec-filter-tree__list"
    >
      <AcSecEventFilterCatalogTreeItem
        v-for="child in node.children"
        :key="child.id"
        :node="child"
        :depth="depth + 1"
        :selected-id="selectedId"
        :expanded-ids="expandedIds"
        @select="emit('select', $event)"
        @toggle="emit('toggle', $event)"
      />
    </ul>
  </li>
</template>
