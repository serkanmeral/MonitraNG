import { defineStore } from 'pinia';
import { fetchFromDataGateway } from '@/services/apiService';
import type { SideMenuItem } from './sideMenu';

interface SideMenuManagerState {
  menuItems: SideMenuItem[];
  menuItemsTree: SideMenuItem[];
  selectedItem: SideMenuItem | null;
  loading: boolean;
  error: string | null;
  searchQuery: string;
}

export const useSideMenuManagerStore = defineStore('sideMenuManager', {
  state: (): SideMenuManagerState => ({
    menuItems: [],
    menuItemsTree: [],
    selectedItem: null,
    loading: false,
    error: null,
    searchQuery: '',
  }),

  getters: {
    /**
     * Filtered menu items tree (by search query)
     */
    filteredMenuItemsTree(state): SideMenuItem[] {
      if (!state.searchQuery.trim()) {
        return state.menuItemsTree || [];
      }

      const query = state.searchQuery.toLowerCase().trim();
      const filtered = this.filterTreeItemsRecursive(state.menuItemsTree, query);
      
      return filtered;
    },
  },

  actions: {
    /**
     * Load all menu items from API
     */
    async loadMenuItems() {
      this.loading = true;
      this.error = null;

      try {
        const response = await fetchFromDataGateway('/api/v1/data/@side_menu?limit=10000');

        // Response format: { items: [...], totalCount: number } or direct array
        let loadedItems: SideMenuItem[] = [];
        
        if (response && Array.isArray(response)) {
          loadedItems = response;
        } else if (response && response.items && Array.isArray(response.items)) {
          loadedItems = response.items;
        } else if (response && response.data && Array.isArray(response.data)) {
          loadedItems = response.data;
        } else {
          throw new Error('Invalid response format');
        }

        this.menuItems = loadedItems;
        this.buildMenuTree();
      } catch (error: any) {
        this.error = error.message || 'Menu items yüklenirken hata oluştu';
        this.menuItems = [];
        this.menuItemsTree = [];
      } finally {
        this.loading = false;
      }
    },

    /**
     * Build hierarchical tree structure from flat array
     * Sorting: Headers first (by order), then items under each header (by order)
     */
    buildMenuTree() {
      if (!this.menuItems || this.menuItems.length === 0) {
        this.menuItemsTree = [];
        return;
      }

      // First, sort by itemType (headers first) then by order
      const sortedItems = [...this.menuItems].sort((a, b) => {
        // Headers come first
        if (a.itemType === 'header' && b.itemType !== 'header') {
          return -1;
        }
        if (a.itemType !== 'header' && b.itemType === 'header') {
          return 1;
        }
        // Same type: sort by order
        return (a.order || 0) - (b.order || 0);
      });

      // Create a map for quick lookup
      const itemMap = new Map<string, SideMenuItem>();
      const rootItems: SideMenuItem[] = [];

      // First pass: create map and add children property
      sortedItems.forEach(item => {
        item.children = [];
        if (item.__dataId) {
          itemMap.set(item.__dataId, item);
        }
      });

      // Second pass: build tree
      sortedItems.forEach(item => {
        if (!item.parentId || !itemMap.has(item.parentId)) {
          // Root item
          rootItems.push(item);
        } else {
          // Child item
          const parent = itemMap.get(item.parentId);
          if (parent) {
            if (!parent.children) {
              parent.children = [];
            }
            parent.children.push(item);
          }
        }
      });

      // Third pass: Sort children of each item by order
      const sortChildren = (items: SideMenuItem[]) => {
        items.forEach(item => {
          if (item.children && item.children.length > 0) {
            // Sort children by order
            item.children.sort((a, b) => (a.order || 0) - (b.order || 0));
            // Recursively sort nested children
            sortChildren(item.children);
          }
        });
      };

      // Sort root items by order (headers first, then items)
      rootItems.sort((a, b) => {
        // Headers come first
        if (a.itemType === 'header' && b.itemType !== 'header') {
          return -1;
        }
        if (a.itemType !== 'header' && b.itemType === 'header') {
          return 1;
        }
        // Same type: sort by order
        return (a.order || 0) - (b.order || 0);
      });

      // Sort all children recursively
      sortChildren(rootItems);

      this.menuItemsTree = rootItems;
    },

    /**
     * Select a menu item
     */
    selectItem(item: SideMenuItem | null) {
      this.selectedItem = item;
    },

    /**
     * Create a new menu item
     */
    async createMenuItem(itemData: Partial<SideMenuItem>): Promise<SideMenuItem> {
      this.loading = true;
      this.error = null;

      try {
        // Clean itemData: Remove internal fields that shouldn't be sent to API
        const cleanData: any = { ...itemData };
        delete cleanData.__dataId; // MongoDB internal field
        delete cleanData.children; // Tree structure field (not in DB schema)
        delete cleanData._id; // MongoDB internal field
        
        // Remove undefined fields
        Object.keys(cleanData).forEach((key) => {
          if (cleanData[key] === undefined) {
            delete cleanData[key];
          }
        });

        const response = await fetchFromDataGateway(
          '/api/v1/data/@side_menu',
          'POST',
          cleanData
        );

        if (response) {
          // Reload menu items to get updated tree
          await this.loadMenuItems();
          return response;
        }

        throw new Error('Create işlemi başarısız');
      } catch (error: any) {
        // Error handled by store error state
        this.error = error.message || 'Menu item oluşturulurken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update a menu item
     */
    async updateMenuItem(itemId: string, itemData: Partial<SideMenuItem>): Promise<SideMenuItem> {
      this.loading = true;
      this.error = null;

      try {
        // Clean itemData: Remove internal fields that shouldn't be sent to API
        const cleanData: any = { ...itemData };
        delete cleanData.__dataId; // MongoDB internal field
        delete cleanData.children; // Tree structure field (not in DB schema)
        delete cleanData._id; // MongoDB internal field
        
        // Remove undefined fields
        Object.keys(cleanData).forEach((key) => {
          if (cleanData[key] === undefined) {
            delete cleanData[key];
          }
        });

        const response = await fetchFromDataGateway(
          `/api/v1/data/@side_menu/${itemId}`,
          'PUT',
          cleanData
        );

        if (response) {
          // Reload menu items to get updated tree
          await this.loadMenuItems();
          
          // Update selected item if it's the updated one
          if (this.selectedItem?.__dataId === itemId) {
            this.selectedItem = response;
          }
          
          return response;
        }

        throw new Error('Update işlemi başarısız');
      } catch (error: any) {
        // Error handled by store error state
        this.error = error.message || 'Menu item güncellenirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Delete a menu item
     */
    async deleteMenuItem(itemId: string): Promise<void> {
      this.loading = true;
      this.error = null;

      try {
        await fetchFromDataGateway(
          `/api/v1/data/@side_menu/${itemId}`,
          'DELETE'
        );

        // Deselect if deleted item was selected
        if (this.selectedItem?.__dataId === itemId) {
          this.selectedItem = null;
        }

        // Reload menu items to get updated tree
        await this.loadMenuItems();
      } catch (error: any) {
        // Error handled by store error state
        this.error = error.message || 'Menu item silinirken hata oluştu';
        throw error;
      } finally {
        this.loading = false;
      }
    },

    /**
     * Update menu item order (for drag & drop)
     */
    async updateMenuItemOrder(itemId: string, newOrder: number, newParentId: string | null): Promise<void> {
      try {
        await this.updateMenuItem(itemId, {
          order: newOrder,
          parentId: newParentId,
          // Recalculate level based on parent
          level: newParentId ? this.calculateLevel(newParentId) + 1 : 0,
        });
      } catch (error) {
        // Error handled by store error state
        throw error;
      }
    },

    /**
     * Calculate level based on parent
     */
    calculateLevel(parentId: string | null): number {
      if (!parentId) return -1;

      const findItem = (items: SideMenuItem[]): SideMenuItem | null => {
        for (const item of items) {
          if (item.__dataId === parentId) {
            return item;
          }
          if (item.children) {
            const found = findItem(item.children);
            if (found) return found;
          }
        }
        return null;
      };

      const parent = findItem(this.menuItemsTree);
      return parent ? parent.level : -1;
    },

    /**
     * Filter tree items recursively by search query
     */
    filterTreeItemsRecursive(items: SideMenuItem[], query: string): SideMenuItem[] {
      const filtered: SideMenuItem[] = [];

      for (const item of items) {
        const matchesQuery = 
          item.title?.toLowerCase().includes(query) ||
          item.header?.toLowerCase().includes(query) ||
          item.to?.toLowerCase().includes(query) ||
          item.subCaption?.toLowerCase().includes(query) ||
          item.pageCode?.toLowerCase().includes(query);

        const filteredChildren = item.children 
          ? this.filterTreeItemsRecursive(item.children, query)
          : [];

        if (matchesQuery || filteredChildren.length > 0) {
          filtered.push({
            ...item,
            children: filteredChildren.length > 0 ? filteredChildren : item.children,
          });
        }
      }

      return filtered;
    },

    /**
     * Set search query
     */
    setSearchQuery(query: string) {
      this.searchQuery = query;
    },

    /**
     * Reset selected item
     */
    resetSelection() {
      this.selectedItem = null;
    },
  },
});
