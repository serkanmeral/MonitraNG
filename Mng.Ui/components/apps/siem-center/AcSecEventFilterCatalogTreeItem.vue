<script setup lang="ts">
import type { SecEventFilterTreeNode } from '@/types/apps/secEventFilterCatalog';
import { useAppI18n } from '@/composables/useAppI18n';

defineOptions({ name: 'AcSecEventFilterCatalogTreeItem' });

const props = defineProps<{
  node: SecEventFilterTreeNode;
  depth: number;
  selectedId: string | null;
  expandedIds: Set<string>;
}>();

const emit = defineEmits<{
  select: [node: SecEventFilterTreeNode];
  toggle: [id: string];
  action: [payload: { action: string; node: SecEventFilterTreeNode }];
}>();

const { t } = useAppI18n();

function onMenu(action: string, e?: Event) {
  e?.stopPropagation();
  e?.preventDefault();
  emit('action', { action, node: props.node });
}
</script>

<template>
  <li class="sec-filter-tree__item">
    <div
      class="sec-filter-tree__row"
      :class="{ 'sec-filter-tree__row--selected': selectedId === node.id }"
      :style="{ paddingLeft: `${8 + depth * 14}px` }"
    >
      <button type="button" class="sec-filter-tree__main" @click="emit('select', node)">
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

      <v-menu v-if="!node.isSystem" location="end">
        <template #activator="{ props: menuProps }">
          <v-btn
            v-bind="menuProps"
            icon
            size="x-small"
            variant="text"
            class="sec-filter-tree__menu-btn"
            @click.stop
          >
            <v-icon icon="mdi-dots-vertical" size="16" />
          </v-btn>
        </template>
        <v-list density="compact" min-width="180">
          <template v-if="node.kind === 'category'">
            <v-list-item
              :title="t('siemCenter.events.filterCatalog.renameCategory')"
              prepend-icon="mdi-pencil-outline"
              @click="onMenu('rename-category')"
            />
            <v-list-item
              :title="t('siemCenter.events.filterCatalog.deleteCategory')"
              prepend-icon="mdi-delete-outline"
              base-color="error"
              @click="onMenu('delete-category')"
            />
          </template>
          <template v-else>
            <v-list-item
              :title="t('siemCenter.events.filterCatalog.renameFilter')"
              prepend-icon="mdi-pencil-outline"
              @click="onMenu('rename-filter')"
            />
            <v-list-item
              :title="t('siemCenter.events.filterCatalog.moveFilter')"
              prepend-icon="mdi-folder-move-outline"
              @click="onMenu('move-filter')"
            />
            <v-list-item
              :title="t('siemCenter.events.filterCatalog.deleteFilter')"
              prepend-icon="mdi-delete-outline"
              base-color="error"
              @click="onMenu('delete-filter')"
            />
          </template>
        </v-list>
      </v-menu>
    </div>
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
        @action="emit('action', $event)"
      />
    </ul>
  </li>
</template>

<style scoped>
.sec-filter-tree__row {
  display: flex;
  align-items: center;
  gap: 2px;
  width: 100%;
}

.sec-filter-tree__main {
  display: flex;
  align-items: center;
  gap: 6px;
  flex: 1;
  min-width: 0;
  border: 0;
  background: transparent;
  text-align: left;
  cursor: pointer;
  padding: 6px 4px 6px 0;
  border-radius: 6px;
  color: inherit;
  font: inherit;
}

.sec-filter-tree__menu-btn {
  opacity: 0.35;
  flex-shrink: 0;
}

.sec-filter-tree__row:hover .sec-filter-tree__menu-btn,
.sec-filter-tree__row--selected .sec-filter-tree__menu-btn {
  opacity: 1;
}
</style>
