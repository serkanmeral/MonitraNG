<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import type { SideMenuItem } from '@/stores/apps/sideMenu';
import { FolderIcon, FileIcon, ChevronRightIcon, ChevronDownIcon } from 'vue-tabler-icons';
import TreeItem from './TreeItem.vue';

const props = defineProps<{
  items: SideMenuItem[];
  selectedItem: SideMenuItem | null;
  loading?: boolean;
}>();

const emit = defineEmits<{
  'item-select': [item: SideMenuItem];
  'item-order-change': [itemId: string, newOrder: number, newParentId: string | null];
}>();

// Expanded items tracking - auto-expand all items with children by default
const expandedItems = ref<Set<string>>(new Set());

// Auto-expand all items with children
const autoExpandItems = (items: SideMenuItem[]) => {
  items.forEach(item => {
    if (item.__dataId && item.children && item.children.length > 0) {
      expandedItems.value.add(item.__dataId);
      // Recursively expand children
      if (item.children) {
        autoExpandItems(item.children);
      }
    }
  });
};

// Local items for drag & drop (reactive) - initialize from props
const localItems = ref<SideMenuItem[]>(props.items ? [...props.items] : []);

// Store original items before drag (to track what was moved)
const originalItemsBeforeDrag = ref<SideMenuItem[]>([]);

// Track if we're in a drag operation to prevent watch from overwriting
const isDragging = ref(false);

// Watch props.items and update localItems (only if not dragging)
watch(() => props.items, (newItems) => {
  // Don't update localItems if we're in the middle of a drag operation
  if (isDragging.value) {
    return;
  }
  
  if (newItems && Array.isArray(newItems)) {
    localItems.value = [...newItems];
    
    // Auto-expand all items with children
    expandedItems.value.clear();
    autoExpandItems(newItems);
  } else {
    localItems.value = [];
    expandedItems.value.clear();
  }
}, { immediate: true, deep: false });

const toggleExpand = (item: SideMenuItem, event?: Event) => {
  if (event) {
    event.stopPropagation();
  }
  
  if (!item.__dataId) return;
  
  if (expandedItems.value.has(item.__dataId)) {
    expandedItems.value.delete(item.__dataId);
  } else {
    expandedItems.value.add(item.__dataId);
  }
};

const handleItemSelect = (item: SideMenuItem) => {
  emit('item-select', item);
  
  // Auto expand on select if has children
  if (item.children && item.children.length > 0 && !expandedItems.value.has(item.__dataId || '')) {
    toggleExpand(item);
  }
};

// Handle drag start
const handleDragStart = (event: any) => {
  isDragging.value = true;
  // Store original items before drag to track what was moved
  originalItemsBeforeDrag.value = [...localItems.value];
};

// Handle drag end - update order and parentId
const handleDragEnd = (event: any) => {
  // Check if item was moved to a different list (cross-level drag)
  const isDifferentParent = event.to !== event.from;
  
  // Get the moved item:
  // - If moved within same list: use newIndex in localItems
  // - If moved from different list: item was added via @add event (handled separately)
  // - If moved to different list: get from originalItemsBeforeDrag using oldIndex
  let movedItem: SideMenuItem | null = null;
  
  if (!isDifferentParent) {
    // Same parent: item is still in localItems at newIndex
    movedItem = localItems.value[event.newIndex];
  } else if (event.from === event.to) {
    // This shouldn't happen, but handle it
    movedItem = localItems.value[event.newIndex];
  } else {
    // Item was moved FROM this list TO another
    // Get from originalItemsBeforeDrag using oldIndex
    if (event.oldIndex !== null && event.oldIndex !== undefined && event.oldIndex >= 0) {
      if (originalItemsBeforeDrag.value.length > event.oldIndex) {
        movedItem = originalItemsBeforeDrag.value[event.oldIndex];
      }
    }
    
    // If item was moved FROM this list, destination handler will handle the update
    if (!movedItem) {
      isDragging.value = false;
      return;
    }
  }
  
  if (!movedItem || !movedItem.__dataId) {
    isDragging.value = false;
    return;
  }
  
  // Get parent ID from destination
  const toElement = event.to;
  let newParentId = null;
  
  if (toElement) {
    const wrapperDiv = toElement.parentElement;
    if (wrapperDiv) {
      const parentIdAttr = wrapperDiv.getAttribute('data-parent-id');
      if (parentIdAttr !== null && parentIdAttr !== undefined) {
        newParentId = parentIdAttr === 'null' ? null : parentIdAttr;
      } else {
        const parentElement = toElement.closest?.('[data-parent-id]');
        if (parentElement) {
          const parentIdAttr2 = parentElement.getAttribute('data-parent-id');
          newParentId = parentIdAttr2 === 'null' ? null : parentIdAttr2;
        }
      }
    }
  }
  
  // Check if parent changed or order changed
  const oldParentId = movedItem.parentId || null;
  const oldOrder = movedItem.order || 0;
  const newOrder = isDifferentParent ? -1 : event.newIndex; // If moved to different parent, order will be set by destination
  
  const parentChanged = oldParentId !== newParentId;
  const orderChanged = !isDifferentParent && oldOrder !== newOrder;
  
  // Only update if something actually changed and only if moved within same list
  // If moved to different parent, destination handler will handle it
  if (!isDifferentParent && (parentChanged || orderChanged)) {
    emit('item-order-change', movedItem.__dataId, newOrder, newParentId);
  }
  
  // Reset dragging flag after a short delay to allow watch to update
  setTimeout(() => {
    isDragging.value = false;
  }, 100);
};

// Handle add event (when item is added to root level from a child list)
const handleDragAdd = (event: any) => {
  // Get the added item from localItems (already added by SortableJS)
  const addedItem = localItems.value[event.newIndex];
  if (!addedItem || !addedItem.__dataId) {
    return;
  }
  
  // Get parent ID - root level has no parent
  const finalParentId = null;
  const newOrder = event.newIndex;
  
  // Check if parent changed
  const oldParentId = addedItem.parentId || null;
  const parentChanged = oldParentId !== finalParentId;
  
  // Update the added item's parent and order
  if (parentChanged) {
    emit('item-order-change', addedItem.__dataId, newOrder, finalParentId);
  }
};
</script>

<template>
  <div class="menu-tree-view">
    <v-progress-linear v-if="loading" indeterminate color="primary"></v-progress-linear>
    
    <div v-if="!loading && (!items || items.length === 0 || !localItems || localItems.length === 0)" class="text-center pa-4 text-medium-emphasis">
      Menu item bulunamadı
    </div>

    <div v-else-if="localItems && localItems.length > 0" class="tree-view-container">
      <div data-parent-id="null">
        <draggable
          :list="localItems"
          :animation="200"
          ghost-class="ghost-item"
          handle=".drag-handle"
          item-key="__dataId"
          group="menu-items"
          @start="handleDragStart"
          @end="handleDragEnd"
          @add="handleDragAdd"
          class="draggable-list"
        >
          <TreeItem
            v-for="item in localItems"
            :key="item.__dataId || `item-${item.order}`"
            :item="item"
            :selected-item="selectedItem"
            :expanded-items="expandedItems"
            :level="0"
            @toggle-expand="toggleExpand"
            @item-select="handleItemSelect"
            @item-order-change="(itemId, newOrder, newParentId) => emit('item-order-change', itemId, newOrder, newParentId)"
          />
        </draggable>
      </div>
    </div>
  </div>
</template>

<style scoped>
.menu-tree-view {
  min-height: 200px;
}

.tree-view-container {
  padding: 8px 0;
}

.draggable-list {
  min-height: 50px;
}

.ghost-item {
  opacity: 0.5;
  background-color: rgba(var(--v-theme-primary), 0.1);
}
</style>
