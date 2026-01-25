<script setup lang="ts">
import { ref, onMounted, computed, nextTick } from 'vue';
import { useRoute } from 'vue-router';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import { useSideMenuManagerStore } from '@/stores/apps/sideMenuManager';
import type { SideMenuItem } from '@/stores/apps/sideMenu';
import MenuTreeView from '@/components/apps/side-menu-manager/MenuTreeView.vue';
import MenuItemForm from '@/components/apps/side-menu-manager/MenuItemForm.vue';
import MenuItemToolbar from '@/components/apps/side-menu-manager/MenuItemToolbar.vue';
import { ChevronDownIcon, ChevronUpIcon } from 'vue-tabler-icons';

definePageMeta({
  layout: 'default',
});

const menuManagerStore = useSideMenuManagerStore();
const menuTreeViewRef = ref<InstanceType<typeof MenuTreeView> | null>(null);

// Page and breadcrumbs will be computed in template using $t()
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

const route = useRoute();

// Load menu items on mount
onMounted(async () => {
  // Always reload to ensure fresh data
  await menuManagerStore.loadMenuItems();
  
  // Check if we have query params from dashboard or form
  const query = route.query;
  if (query.source === 'dashboard' && query.dashboardId) {
    await nextTick();
    const dashboardTitle = query.title as string || '';
    const routePath = query.routePath as string || '';
    const preFilledItem: Partial<SideMenuItem> = {
      order: menuManagerStore.menuItems.length,
      itemType: 'item',
      level: 0,
      parentId: null,
      pageType: 'user',
      disabled: false,
      title: dashboardTitle,
      to: routePath,
      icon: 'mdi-view-dashboard',
      iconType: 'mdi',
      type: 'internal',
      pageCode: `dashboard-${query.dashboardId}`,
    };
    menuManagerStore.selectItem(preFilledItem as any);
  } else if (query.source === 'form' && query.formCode) {
    await nextTick();
    const formCode = String(query.formCode);
    const routePath = (query.routePath as string) || `/apps/automated-forms/view/${encodeURIComponent(formCode)}`;
    const preFilledItem: Partial<SideMenuItem> = {
      order: menuManagerStore.menuItems.length,
      itemType: 'item',
      level: 0,
      parentId: null,
      pageType: 'user',
      disabled: false,
      title: (query.title as string) || formCode,
      to: routePath,
      icon: 'mdi-form-select',
      iconType: 'mdi',
      type: 'internal',
      pageCode: `form-${formCode}`,
    };
    menuManagerStore.selectItem(preFilledItem as any);
  }
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
    pageType: 'user', // Her zaman 'user' olarak başlar
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
  // Note: confirm() doesn't support i18n easily, so we use hardcoded text
  // For better UX, consider using a dialog component with $t() in template
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

// Handle expand all
const handleExpandAll = () => {
  menuTreeViewRef.value?.expandAll();
};

// Handle collapse all
const handleCollapseAll = () => {
  menuTreeViewRef.value?.collapseAll();
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
        <v-col cols="12" md="5" lg="4">
          <v-card elevation="2">
            <v-card-title class="d-flex align-center">
              <span>{{ $t('side-menu-manager.tree.title') }}</span>
              <v-spacer></v-spacer>
              <v-btn
                icon
                size="small"
                variant="text"
                @click="handleExpandAll"
                :title="$t('side-menu-manager.tree.expandAll')"
                class="mr-1"
              >
                <ChevronDownIcon size="18" />
              </v-btn>
              <v-btn
                icon
                size="small"
                variant="text"
                @click="handleCollapseAll"
                :title="$t('side-menu-manager.tree.collapseAll')"
                class="mr-2"
              >
                <ChevronUpIcon size="18" />
              </v-btn>
              <v-chip size="small" color="primary">
                {{ menuManagerStore.filteredMenuItemsTree.length }}
              </v-chip>
            </v-card-title>
            <v-divider></v-divider>
            <v-card-text class="pa-0" style="max-height: calc(100vh - 280px); overflow-y: auto;">
              <MenuTreeView
                ref="menuTreeViewRef"
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
        <v-col cols="12" md="7" lg="8">
          <v-card elevation="2">
            <v-card-title>
              <span v-if="menuManagerStore.selectedItem?.__dataId">
                {{ $t('side-menu-manager.form.title.edit') }}
              </span>
              <span v-else>
                {{ $t('side-menu-manager.form.title.new') }}
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
