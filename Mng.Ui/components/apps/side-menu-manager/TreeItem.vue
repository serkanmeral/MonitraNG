<script lang="ts">
import { defineComponent, computed, ref, watch } from 'vue';
import { useRouter } from 'vue-router';
import type { SideMenuItem } from '@/stores/apps/sideMenu';
import { FolderIcon, FileIcon, ChevronRightIcon, ChevronDownIcon, ExternalLinkIcon, GripVerticalIcon } from 'vue-tabler-icons';

export default defineComponent({
  name: 'TreeItem',
  components: {
    TreeItem: () => import('./TreeItem.vue'),
  },
  props: {
    item: {
      type: Object as () => SideMenuItem,
      required: true,
    },
    selectedItem: {
      type: Object as () => SideMenuItem | null,
      default: null,
    },
    expandedItems: {
      type: Object as () => Set<string>,
      required: true,
    },
    level: {
      type: Number,
      default: 0,
    },
  },
  emits: ['toggle-expand', 'item-select', 'item-order-change'],
  setup(props, { emit }) {
    const router = useRouter();
    
    // Local children for drag & drop (reactive)
    const localChildren = ref<SideMenuItem[]>([...(props.item.children || [])]);
    
    // Store original children before drag (to track what was moved)
    const originalChildrenBeforeDrag = ref<SideMenuItem[]>([]);
    
    // Watch props.item.children and update localChildren
    watch(() => props.item.children, (newChildren) => {
      localChildren.value = [...(newChildren || [])];
    }, { deep: true, immediate: true });
    
    const hasChildren = computed(() => {
      return localChildren.value && localChildren.value.length > 0;
    });
    
    // Check if item can have children
    // In Side Menu Manager, ALL items can have children (not just headers)
    // This allows users to create nested menu structures even if an item doesn't have children yet
    const canHaveChildren = computed(() => {
      // In Side Menu Manager context, all items can accept children
      // This solves the "chicken-egg" problem: items can be expanded to add children
      // even if they don't have any children yet
      return true; // All items can have children in Side Menu Manager
    });

    const isExpanded = computed(() => {
      return props.item.__dataId ? props.expandedItems.has(props.item.__dataId) : false;
    });

    const isSelected = computed(() => {
      return props.item.__dataId === props.selectedItem?.__dataId;
    });

    const itemLabel = computed(() => {
      return props.item.header || props.item.title || 'Untitled';
    });

    const ItemIcon = computed(() => {
      return props.item.itemType === 'header' ? FolderIcon : FileIcon;
    });

    const handleToggleExpand = (event: Event) => {
      event.stopPropagation();
      emit('toggle-expand', props.item, event);
    };

    const handleItemClick = () => {
      emit('item-select', props.item);
    };

    const handleNavigateToRoute = async (event: Event) => {
      event.stopPropagation();
      
      if (!props.item.to || !props.item.to.trim()) {
        return;
      }
      
      try {
        const route = props.item.to.trim();
        
        // Check if external link
        if (props.item.type === 'external' || route.startsWith('http://') || route.startsWith('https://')) {
          window.open(route, '_blank');
          return;
        }
        
        // Internal route - navigate using Vue Router
        await router.push(route);
      } catch (error) {
        // Fallback: try window.location
        if (typeof window !== 'undefined') {
          window.location.href = props.item.to || '#';
        }
      }
    };

    const hasRoute = computed(() => {
      return props.item.itemType === 'item' && props.item.to && props.item.to.trim().length > 0;
    });

    // Handle drag start - store original state
    const handleChildrenDragStart = (event: any) => {
      // Store original children before drag to track what was moved
      originalChildrenBeforeDrag.value = [...localChildren.value];
    };
    
    // Handle drag end for children
    const handleChildrenDragEnd = (event: any) => {
      // Check if item was moved to a different list (cross-level drag)
      const isDifferentParent = event.to !== event.from;
      
      let movedItem: SideMenuItem | null = null;
      
      if (!isDifferentParent) {
        // Same parent: item is still in localChildren at newIndex
        movedItem = localChildren.value[event.newIndex];
      } else {
        // Different parent: item was moved FROM this list TO another
        // Get the moved item from originalChildrenBeforeDrag using oldIndex
        if (event.oldIndex !== null && event.oldIndex !== undefined && event.oldIndex >= 0) {
          if (originalChildrenBeforeDrag.value.length > event.oldIndex) {
            movedItem = originalChildrenBeforeDrag.value[event.oldIndex];
          }
        }
        
        // If item was moved FROM this list, we don't need to update it here
        // The destination handler will handle the update
        if (!movedItem) {
          return;
        }
      }
      
      if (!movedItem || !movedItem.__dataId) {
        return;
      }
      
      // Get parent ID from destination
      const toElement = event.to;
      let finalParentId = props.item.__dataId || null;
      
      if (toElement) {
        const wrapperDiv = toElement.parentElement;
        if (wrapperDiv) {
          const parentIdAttr = wrapperDiv.getAttribute('data-parent-id');
          if (parentIdAttr !== null && parentIdAttr !== undefined) {
            finalParentId = parentIdAttr === 'null' ? null : parentIdAttr;
          } else {
            const parentElement = toElement.closest?.('[data-parent-id]');
            if (parentElement) {
              const parentIdAttr2 = parentElement.getAttribute('data-parent-id');
              finalParentId = parentIdAttr2 === 'null' ? null : parentIdAttr2;
            }
          }
        }
      }
      
      // Check if parent changed or order changed
      const oldParentId = movedItem.parentId || null;
      const oldOrder = movedItem.order || 0;
      const newOrder = isDifferentParent ? -1 : event.newIndex; // If moved to different parent, order will be set by destination
      
      const parentChanged = oldParentId !== finalParentId;
      const orderChanged = !isDifferentParent && oldOrder !== newOrder;
      
      // Only update if something actually changed
      // If moved to different parent, destination handler will handle it
      if (!isDifferentParent && (parentChanged || orderChanged)) {
        emit('item-order-change', movedItem.__dataId, newOrder, finalParentId);
      }
    };
    
    // Handle add event (when item is added to this list from another)
    const handleChildrenDragAdd = (event: any) => {
      // Get the added item from localChildren (already added by SortableJS)
      const addedItem = localChildren.value[event.newIndex];
      if (!addedItem || !addedItem.__dataId) {
        return;
      }
      
      // Get parent ID - this is our current item's __dataId
      const finalParentId = props.item.__dataId || null;
      const newOrder = event.newIndex;
      
      // Check if parent changed
      const oldParentId = addedItem.parentId || null;
      const parentChanged = oldParentId !== finalParentId;
      
      // Update the added item's parent and order
      if (parentChanged) {
        emit('item-order-change', addedItem.__dataId, newOrder, finalParentId);
      }
    };

    return {
      localChildren,
      originalChildrenBeforeDrag,
      hasChildren,
      canHaveChildren,
      isExpanded,
      isSelected,
      itemLabel,
      ItemIcon,
      handleToggleExpand,
      handleItemClick,
      handleNavigateToRoute,
      hasRoute,
      handleChildrenDragStart,
      handleChildrenDragEnd,
      handleChildrenDragAdd,
      GripVerticalIcon,
    };
  },
});
</script>

<template>
  <div class="tree-item-wrapper">
    <div
      :class="[
        'tree-item',
        {
          'tree-item-selected': isSelected,
          'tree-item-header': item.itemType === 'header',
        },
      ]"
      :style="{ paddingLeft: `${level * 20 + 8}px` }"
      @click="handleItemClick"
    >
      <div class="d-flex align-center" style="min-height: 36px; cursor: pointer;">
        <!-- Drag Handle -->
        <div class="drag-handle mr-1" style="cursor: move; opacity: 0.5;">
          <GripVerticalIcon size="16" />
        </div>

        <!-- Expand/Collapse Icon -->
        <v-btn
          v-if="canHaveChildren"
          icon
          size="x-small"
          variant="text"
          class="mr-1"
          @click="handleToggleExpand"
        >
          <ChevronDownIcon v-if="isExpanded" size="16" />
          <ChevronRightIcon v-else size="16" />
        </v-btn>
        <div v-else style="width: 24px;"></div>

        <!-- Item Icon -->
        <component :is="ItemIcon" size="18" :class="['mr-2', item.itemType === 'header' ? 'text-primary' : '']" />

        <!-- Item Label -->
        <span class="text-body-2 flex-grow-1">
          {{ itemLabel }}
        </span>

        <!-- Navigate Button (if has route) -->
        <v-btn
          v-if="hasRoute"
          icon
          size="x-small"
          variant="text"
          class="ml-1"
          @click="handleNavigateToRoute"
          title="Sayfaya git"
        >
          <ExternalLinkIcon size="16" />
        </v-btn>

        <!-- Page Type Badge -->
        <v-chip v-if="item.pageType" size="x-small" variant="tonal" class="ml-2">
          {{ item.pageType }}
        </v-chip>
      </div>
    </div>

    <!-- Children - Recursive with Drag & Drop -->
    <!-- Render draggable area if expanded and (has children OR is a header that can accept children) -->
    <div v-if="isExpanded && canHaveChildren" class="tree-item-children">
      <div :data-parent-id="item.__dataId || null">
        <draggable
          :list="localChildren"
          :animation="200"
          ghost-class="ghost-item"
          handle=".drag-handle"
          item-key="__dataId"
          group="menu-items"
          @start="handleChildrenDragStart"
          @end="handleChildrenDragEnd"
          @add="handleChildrenDragAdd"
          class="draggable-children"
          :empty-insert-threshold="10"
        >
          <TreeItem
            v-for="child in localChildren"
            :key="child.__dataId || `child-${child.order}`"
            :item="child"
            :selected-item="selectedItem"
            :expanded-items="expandedItems"
            :level="level + 1"
            @toggle-expand="$emit('toggle-expand', $event, $event2)"
            @item-select="$emit('item-select', $event)"
            @item-order-change="(itemId, newOrder, newParentId) => $emit('item-order-change', itemId, newOrder, newParentId)"
          />
        </draggable>
      </div>
    </div>
  </div>
</template>

<style scoped>
.tree-item-wrapper {
  user-select: none;
}

.tree-item {
  transition: background-color 0.2s;
  border-radius: 4px;
  margin: 2px 0;
}

.tree-item:hover {
  background-color: rgba(var(--v-theme-primary), 0.08);
}

.tree-item-selected {
  background-color: rgba(var(--v-theme-primary), 0.12);
  font-weight: 500;
}

.tree-item-header {
  font-weight: 600;
}

.tree-item-children {
  margin-left: 0;
}

.draggable-children {
  min-height: 40px; /* Boş durumda da drop zone için yeterli alan */
  padding: 4px 0;
  transition: background-color 0.2s;
}

.draggable-children:empty::before {
  content: '';
  display: block;
  height: 30px;
  margin: 4px 8px;
  border: 2px dashed rgba(var(--v-theme-primary), 0.3);
  border-radius: 4px;
  background-color: rgba(var(--v-theme-primary), 0.05);
}

.draggable-children:empty:hover::before {
  border-color: rgba(var(--v-theme-primary), 0.5);
  background-color: rgba(var(--v-theme-primary), 0.1);
}

.drag-handle:hover {
  opacity: 1 !important;
  color: rgb(var(--v-theme-primary));
}

.ghost-item {
  opacity: 0.5;
  background-color: rgba(var(--v-theme-primary), 0.1);
}
</style>
