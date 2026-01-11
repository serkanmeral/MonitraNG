<script setup lang="ts">
import { ref, onMounted, computed } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useSideMenuManagerStore } from '@/stores/apps/sideMenuManager';
import MenuTreeView from '@/components/apps/side-menu-manager/MenuTreeView.vue';
import MenuItemForm from '@/components/apps/side-menu-manager/MenuItemForm.vue';
import MenuItemToolbar from '@/components/apps/side-menu-manager/MenuItemToolbar.vue';

definePageMeta({
  layout: 'default',
});

const menuManagerStore = useSideMenuManagerStore();

const page = ref({ title: 'Side Menu Manager' });
const breadcrumbs = ref([
  {
    text: 'Dashboard',
    disabled: false,
    href: '/dashboards/analytical',
  },
  {
    text: 'Side Menu Manager',
    disabled: true,
    href: '#',
  },
]);

// Load menu items on mount
onMounted(async () => {
  // Always reload to ensure fresh data
  await menuManagerStore.loadMenuItems();
});

// Handle item selection from tree
const handleItemSelect = (item: any) => {
  menuManagerStore.selectItem(item);
};

// Handle new item creation
const handleNewItem = (itemType: 'header' | 'item') => {
  menuManagerStore.selectItem({
    order: menuManagerStore.menuItems.length,
    itemType,
    level: 0,
    parentId: null,
    pageType: 'admin',
    disabled: false,
  } as any);
};

// Handle save
const handleSave = async (itemData: any) => {
  try {
    if (itemData.__dataId) {
      // Update existing
      await menuManagerStore.updateMenuItem(itemData.__dataId, itemData);
    } else {
      // Create new
      await menuManagerStore.createMenuItem(itemData);
    }
  } catch (error) {
    throw error;
  }
};

// Handle delete
const handleDelete = async (itemId: string) => {
  if (confirm('Bu menu item\'ı silmek istediğinize emin misiniz?')) {
    try {
      await menuManagerStore.deleteMenuItem(itemId);
    } catch (error) {
      throw error;
    }
  }
};

// Handle search
const handleSearch = (query: string) => {
  menuManagerStore.setSearchQuery(query);
};

// Handle refresh
const handleRefresh = async () => {
  await menuManagerStore.loadMenuItems();
};

// Handle item order change (from drag & drop)
const handleItemOrderChange = async (itemId: string, newOrder: number, newParentId: string | null) => {
  try {
    // Calculate new level based on parent
    const newLevel = newParentId ? menuManagerStore.calculateLevel(newParentId) + 1 : 0;
    
    // Update menu item order
    await menuManagerStore.updateMenuItem(itemId, {
      order: newOrder,
      parentId: newParentId,
      level: newLevel,
    });
    
    // Reload menu items to refresh tree
    await menuManagerStore.loadMenuItems();
  } catch (error) {
    // Reload menu items to restore previous state
    await menuManagerStore.loadMenuItems();
  }
};

// Handle items reordered (from drag & drop) - DEPRECATED: Only used for root level batch updates
// Note: Individual item updates are handled by handleItemOrderChange
const handleItemsReordered = async (items: any[]) => {
  // This should not be called anymore as we only update moved items individually
  // Keeping for backward compatibility but should not trigger batch updates
};
</script>

<template>
  <div class="side-menu-manager">
    <BaseBreadcrumb :title="page.title" :breadcrumbs="breadcrumbs"></BaseBreadcrumb>
    
    <v-container fluid>
      <!-- Toolbar -->
      <v-row>
        <v-col cols="12">
          <MenuItemToolbar
            @new-header="handleNewItem('header')"
            @new-item="handleNewItem('item')"
            @search="handleSearch"
            @refresh="handleRefresh"
            :loading="menuManagerStore.loading"
          />
        </v-col>
      </v-row>

      <!-- Error Message -->
      <v-row v-if="menuManagerStore.error">
        <v-col cols="12">
          <v-alert type="error" variant="tonal" dismissible @click:close="menuManagerStore.error = null">
            {{ menuManagerStore.error }}
          </v-alert>
        </v-col>
      </v-row>

      <!-- Main Content: Tree View + Form -->
      <v-row>
        <!-- Left: Tree View -->
        <v-col cols="12" md="4" lg="3">
          <v-card elevation="2">
            <v-card-title class="d-flex align-center">
              <span>Menu Items</span>
              <v-spacer></v-spacer>
              <v-chip size="small" color="primary">
                {{ menuManagerStore.filteredMenuItemsTree.length }}
              </v-chip>
            </v-card-title>
            <v-divider></v-divider>
            <v-card-text class="pa-0" style="max-height: calc(100vh - 280px); overflow-y: auto;">
              <MenuTreeView
                :items="menuManagerStore.filteredMenuItemsTree"
                :selected-item="menuManagerStore.selectedItem"
                @item-select="handleItemSelect"
                @item-order-change="handleItemOrderChange"
                :loading="menuManagerStore.loading"
              />
            </v-card-text>
          </v-card>
        </v-col>

        <!-- Right: Form/Detail -->
        <v-col cols="12" md="8" lg="9">
          <v-card elevation="2">
            <v-card-title>
              <span v-if="menuManagerStore.selectedItem?.__dataId">
                Menu Item Düzenle
              </span>
              <span v-else>
                Yeni Menu Item
              </span>
            </v-card-title>
            <v-divider></v-divider>
            <v-card-text>
              <MenuItemForm
                :item="menuManagerStore.selectedItem"
                :all-items="menuManagerStore.menuItems"
                @save="handleSave"
                @delete="handleDelete"
                @cancel="menuManagerStore.resetSelection()"
                :loading="menuManagerStore.loading"
              />
            </v-card-text>
          </v-card>
        </v-col>
      </v-row>
    </v-container>
  </div>
</template>

<style scoped>
.side-menu-manager {
  min-height: calc(100vh - 100px);
}
</style>
